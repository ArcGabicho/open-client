#!/usr/bin/env bash
set -e

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

COMPOSE="docker compose --env-file .env -f docker/docker-compose.yml"

# Prefijo para comandos que requieren privilegios (vacio si ya somos root)
if [ "$(id -u)" -eq 0 ]; then
    SUDO=""
elif command -v sudo &> /dev/null; then
    SUDO="sudo"
else
    SUDO=""
fi
DOCKER_SUDO=""   # se rellena con "sudo" si el usuario aun no esta en el grupo docker

REPO_URL="${OPENCLIENT_REPO_URL:-https://github.com/ArcGabicho/open-client.git}"
DEPLOY_DIR="${OPENCLIENT_DEPLOY_DIR:-$HOME/open-client}"
DEPLOY_BRANCH="${OPENCLIENT_BRANCH:-master}"

show_help() {
    echo -e "Uso: ./scripts/deploy.sh [OPCION]"
    echo -e "Opciones:"
    echo -e "  --vm          Despliegue local/remoto en maquina virtual con Docker Compose (Por defecto)"
    echo -e "  --aca         Despliegue en la nube usando Azure Container Apps"
    echo -e "  --help        Muestra este mensaje de ayuda"
}

# ----------------------------------------------------
# Politica de contrasena (compatible con SQL Server):
# minimo 8 caracteres y al menos 3 de estos 4 grupos:
# mayusculas, minusculas, digitos, simbolos.
# ----------------------------------------------------
password_policy_ok() {
    local p="$1" classes=0
    [ "${#p}" -ge 8 ] || return 1
    [ "${#p}" -le 128 ] || return 1
    [[ "$p" == *[A-Z]* ]] && classes=$((classes + 1))
    [[ "$p" == *[a-z]* ]] && classes=$((classes + 1))
    [[ "$p" == *[0-9]* ]] && classes=$((classes + 1))
    [[ "$p" == *[^A-Za-z0-9]* ]] && classes=$((classes + 1))
    [ "$classes" -ge 3 ]
}

# ----------------------------------------------------
# Generacion interactiva de .env a partir de .env.example
# ----------------------------------------------------
setup_env_interactive() {
    if [ ! -f .env.example ]; then
        echo -e "${RED}Error: No existe .env.example en $(pwd); no puedo generar .env.${NC}"
        exit 1
    fi

    if [ ! -t 0 ] && [ ! -r /dev/tty ]; then
        echo -e "${RED}Error: No hay terminal interactiva para pedir credenciales.${NC}"
        echo -e "Crea manualmente el archivo: ${YELLOW}$(pwd)/.env${NC} (copia de .env.example)."
        exit 1
    fi

    echo -e "${YELLOW}[+] No existe .env. Se creara a partir de .env.example.${NC}"
    echo -e "    Para valores no sensibles, pulsa Enter para aceptar el valor entre [corchetes]."

    local tmp
    tmp="$(mktemp)"

    while IFS= read -r line || [ -n "$line" ]; do
        # Conservar comentarios y lineas sin 'clave=valor'
        if [ -z "$line" ] || [ "${line#\#}" != "$line" ] || [ "${line#*=}" = "$line" ]; then
            printf '%s\n' "$line" >> "$tmp"
            continue
        fi

        local key default current value value2
        key="${line%%=*}"
        default="${line#*=}"
        current="${!key:-$default}"   # respeta variable ya exportada en el entorno

        case "$key" in
            *PASSWORD*|*SECRET*|*TOKEN*)
                while true; do
                    if ! read -r -s -p "Introduce $key: " value < /dev/tty; then
                        echo
                        echo -e "${RED}Entrada cancelada. No se creo .env.${NC}"
                        rm -f "$tmp"
                        exit 1
                    fi
                    echo
                    if [ -z "${value:-}" ]; then
                        if [ -n "${!key:-}" ]; then value="${!key}"; break; fi
                        echo -e "${RED}  No puede estar vacio.${NC}"; continue
                    fi
                    read -r -s -p "Repite $key:   " value2 < /dev/tty; echo
                    if [ "$value" != "$value2" ]; then
                        echo -e "${RED}  No coinciden, intenta de nuevo.${NC}"; continue
                    fi
                    case "$key" in
                        *PASSWORD*)
                            if ! password_policy_ok "$value"; then
                                echo -e "${RED}  Contrasena debil: min 8 caracteres y 3 de 4 grupos (MAYUS, minus, digito, simbolo).${NC}"
                                continue
                            fi
                            ;;
                    esac
                    break
                done
                ;;
            *)
                read -r -p "$key [$current]: " value < /dev/tty
                value="${value:-$current}"
                ;;
        esac

        printf '%s=%s\n' "$key" "$value" >> "$tmp"
    done < .env.example

    mv "$tmp" .env
    chmod 600 .env
    echo -e "${GREEN}[✓] Archivo .env creado en $(pwd)/.env${NC}"
}

# ----------------------------------------------------
# Instalacion de dependencias (Ubuntu/Debian con apt)
# ----------------------------------------------------
apt_install() {
    if ! command -v apt-get &> /dev/null; then
        echo -e "${RED}Gestor de paquetes no soportado (se esperaba apt).${NC}"
        echo -e "Instala manualmente: ${YELLOW}$*${NC}"
        exit 1
    fi
    $SUDO apt-get update -y
    $SUDO DEBIAN_FRONTEND=noninteractive apt-get install -y "$@"
}

ensure_dependencies_vm() {
    echo -e "${YELLOW}[+] Verificando dependencias...${NC}"

    # Utilidades basicas
    local base_missing=()
    for c in git curl ca-certificates; do
        case "$c" in
            ca-certificates) dpkg -s ca-certificates &> /dev/null || base_missing+=("$c") ;;
            *) command -v "$c" &> /dev/null || base_missing+=("$c") ;;
        esac
    done
    if [ ${#base_missing[@]} -gt 0 ]; then
        echo -e "${YELLOW}[+] Instalando: ${base_missing[*]}${NC}"
        apt_install "${base_missing[@]}"
    fi

    # Docker Engine
    if ! command -v docker &> /dev/null; then
        echo -e "${YELLOW}[+] Docker no encontrado. Instalando via get.docker.com...${NC}"
        curl -fsSL https://get.docker.com | $SUDO sh
    fi

    # Plugin 'docker compose' (v2)
    if ! docker compose version &> /dev/null; then
        echo -e "${YELLOW}[+] Plugin 'docker compose' no encontrado. Instalando...${NC}"
        apt_install docker-compose-plugin
    fi

    # Arrancar y habilitar el servicio
    if command -v systemctl &> /dev/null; then
        $SUDO systemctl enable --now docker &> /dev/null || true
    fi

    # Permisos: si el usuario no puede hablar con el daemon, usar sudo para docker
    if ! docker info &> /dev/null; then
        if $SUDO docker info &> /dev/null; then
            DOCKER_SUDO="$SUDO"
            echo -e "${YELLOW}[i] Se usara 'sudo' para Docker (el usuario no esta en el grupo 'docker').${NC}"
            echo -e "    Para evitarlo en el futuro: ${YELLOW}sudo usermod -aG docker $USER${NC} y reinicia sesion."
        else
            echo -e "${RED}Error: Docker esta instalado pero el daemon no responde.${NC}"
            exit 1
        fi
    fi

    # Reconstruir el comando compose con el prefijo adecuado
    COMPOSE="${DOCKER_SUDO:+$DOCKER_SUDO }docker compose --env-file .env -f docker/docker-compose.yml"

    echo -e "${GREEN}[✓] Dependencias listas.${NC}"
}

# ----------------------------------------------------
# Comprobacion de memoria: SQL Server exige >= 2000 MB de RAM fisica
# (el swap NO cuenta para su chequeo de arranque).
# ----------------------------------------------------
check_memory_vm() {
    local min_mb="${OPENCLIENT_MIN_RAM_MB:-2000}"
    local mem_kb mem_mb
    mem_kb="$(awk '/^MemTotal:/ {print $2}' /proc/meminfo 2>/dev/null || echo 0)"
    mem_mb=$(( mem_kb / 1024 ))

    if [ "$mem_mb" -lt "$min_mb" ]; then
        echo -e "${RED}Error: RAM insuficiente. Detectados ${mem_mb} MB; SQL Server necesita >= ${min_mb} MB.${NC}"
        echo -e "El contenedor 'sqlserver' abortara con: 'requires a machine with at least 2000 megabytes of memory'."
        echo -e "Opciones:"
        echo -e "  - Ampliar la RAM de la VM (recomendado 4 GB)."
        echo -e "  - Usar una base de datos externa y levantar solo el servicio 'openclient'."
        echo -e "  - Desplegar en Azure Container Apps: ${YELLOW}... | bash -s -- --aca${NC}"
        echo -e "Para omitir esta comprobacion: ${YELLOW}OPENCLIENT_MIN_RAM_MB=0${NC}"
        exit 1
    fi
    echo -e "${GREEN}[✓] RAM disponible: ${mem_mb} MB.${NC}"
}

# ----------------------------------------------------
# Modo 1: Despliegue en Maquina Virtual / Servidor Ubuntu
# ----------------------------------------------------
deploy_vm() {
    echo -e "${GREEN}=== Desplegando en Maquina Virtual / Servidor Linux ===${NC}"

    ensure_dependencies_vm
    check_memory_vm

    if [ -d .git ]; then
        # Ya estamos dentro de un clon del repo
        echo -e "${YELLOW}[+] Descargando codigo actualizado de Git...${NC}"
        git pull origin "$DEPLOY_BRANCH"
    else
        # Ejecucion fuera del repo (ej. curl | bash): clonar/actualizar en $DEPLOY_DIR
        if [ ! -d "$DEPLOY_DIR/.git" ]; then
            echo -e "${YELLOW}[+] Clonando repositorio en ${DEPLOY_DIR}...${NC}"
            git clone --branch "$DEPLOY_BRANCH" "$REPO_URL" "$DEPLOY_DIR"
        fi
        cd "$DEPLOY_DIR"
        echo -e "${YELLOW}[+] Descargando codigo actualizado de Git...${NC}"
        git pull origin "$DEPLOY_BRANCH"
    fi

    if [ ! -f .env ]; then
        setup_env_interactive
    fi

    if ! grep -q "MSSQL_APP_PASSWORD=" .env || ! grep -q "MSSQL_PASSWORD=" .env; then
        echo -e "${RED}Error: El archivo .env debe definir MSSQL_PASSWORD y MSSQL_APP_PASSWORD.${NC}"
        exit 1
    fi

    if ! grep -q "OPENCLIENT_ADMIN_EMAIL=" .env || ! grep -q "OPENCLIENT_ADMIN_PASSWORD=" .env; then
        echo -e "${RED}Error: El archivo .env debe definir OPENCLIENT_ADMIN_EMAIL y OPENCLIENT_ADMIN_PASSWORD (administrador inicial de dbo.Users).${NC}"
        exit 1
    fi

    echo -e "${YELLOW}[+] Reconstruyendo la imagen de produccion con Docker...${NC}"
    $COMPOSE build openclient

    echo -e "${YELLOW}[+] Iniciando el stack completo (sqlserver + db-init + openclient)...${NC}"
    $COMPOSE up -d

    echo -e "${YELLOW}[+] Verificando estado del servicio (Healthcheck)...${NC}"
    sleep 10

    HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:8080 || true)

    if [ "$HTTP_STATUS" -eq 200 ]; then
        echo -e "${GREEN}[✓] DESPLIEGUE EN VM EXITOSO (HTTP 200).${NC}"
    else
        echo -e "${RED}[!] ADVERTENCIA: La app devolvio el codigo HTTP $HTTP_STATUS.${NC}"
        echo -e "Revisa los logs con: ${YELLOW}$COMPOSE logs openclient${NC}"
    fi
}

# ----------------------------------------------------
# Modo 2: Despliegue en Azure Container Apps (ACA)
# ----------------------------------------------------
deploy_aca() {
    echo -e "${GREEN}=== Desplegando en Azure Container Apps ===${NC}"

    if ! command -v az &> /dev/null; then
        echo -e "${RED}Error: Azure CLI ('az') no esta instalado.${NC}"
        exit 1
    fi

    RESOURCE_GROUP="${AZURE_RESOURCE_GROUP:-rg-openclient}"
    CONTAINER_APP_NAME="${AZURE_APP_NAME:-app-openclient}"
    REGISTRY_NAME="${AZURE_REGISTRY_NAME:-acropenclient}"

    echo -e "${YELLOW}[+] Comprobando sesion activa en Azure...${NC}"
    az account show > /dev/null 2>&1 || az login

    echo -e "${YELLOW}[+] Compilando y subiendo imagen a Azure Container Registry (ACR)...${NC}"
    az acr build \
        --registry "$REGISTRY_NAME" \
        --image openclient-app:latest \
        --file docker/Dockerfile .

    echo -e "${YELLOW}[+] Actualizando Container App con la nueva imagen...${NC}"
    az containerapp update \
        --name "$CONTAINER_APP_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --image "${REGISTRY_NAME}.azurecr.io/openclient-app:latest"

    echo -e "${GREEN}[✓] DESPLIEGUE EN AZURE CONTAINER APPS COMPLETADO EXITOSAMENTE.${NC}"
}

# ----------------------------------------------------
# Flujo Principal y Seleccion
# ----------------------------------------------------
MODE="$1"

if [ -z "$MODE" ]; then
    if [ ! -t 0 ] && [ ! -r /dev/tty ]; then
        echo -e "${RED}Error: No hay terminal interactiva disponible.${NC}"
        echo -e "Ejecuta el script indicando el modo, por ejemplo:"
        echo -e "  ${YELLOW}curl -sSL <URL> | bash -s -- --vm${NC}"
        echo -e "  ${YELLOW}curl -sSL <URL> | bash -s -- --aca${NC}"
        exit 1
    fi
    echo -e "${YELLOW}Selecciona el entorno de despliegue:${NC}"
    echo "1) Maquina Virtual / Ubuntu (Docker Compose)"
    echo "2) Azure Container Apps (Nube Azure)"
    read -p "Opcion [1-2]: " choice < /dev/tty
    case "$choice" in
        1) MODE="--vm" ;;
        2) MODE="--aca" ;;
        *) echo -e "${RED}Opcion no valida.${NC}"; exit 1 ;;
    esac
fi

case "$MODE" in
    --vm)
        deploy_vm
        ;;
    --aca)
        deploy_aca
        ;;
    --help)
        show_help
        ;;
    *)
        echo -e "${RED}Opcion desconocida: $MODE${NC}"
        show_help
        exit 1
        ;;
esac
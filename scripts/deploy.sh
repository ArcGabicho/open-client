#!/usr/bin/env bash
set -e

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

COMPOSE="docker compose --env-file .env -f docker/docker-compose.yml"

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
                    [ "$value" = "$value2" ] && break
                    echo -e "${RED}  No coinciden, intenta de nuevo.${NC}"
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
# Modo 1: Despliegue en Maquina Virtual / Servidor Ubuntu
# ----------------------------------------------------
deploy_vm() {
    echo -e "${GREEN}=== Desplegando en Maquina Virtual / Servidor Linux ===${NC}"

    if ! command -v git &> /dev/null; then
        echo -e "${RED}Error: 'git' no esta instalado.${NC}"
        exit 1
    fi

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
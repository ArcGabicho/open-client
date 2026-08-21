#!/usr/bin/env bash
set -e

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

COMPOSE="docker compose --env-file .env -f docker/docker-compose.yml"

show_help() {
    echo -e "Uso: ./scripts/deploy.sh [OPCIÓN]"
    echo -e "Opciones:"
    echo -e "  --vm          Despliegue local/remoto en máquina virtual con Docker Compose (Por defecto)"
    echo -e "  --aca         Despliegue en la nube usando Azure Container Apps"
    echo -e "  --help        Muestra este mensaje de ayuda"
}

# ----------------------------------------------------
# Modo 1: Despliegue en Máquina Virtual / Servidor Ubuntu
# ----------------------------------------------------
deploy_vm() {
    echo -e "${GREEN}=== Desplegando en Máquina Virtual / Servidor Linux ===${NC}"

    echo -e "${YELLOW}[+] Descargando código actualizado de Git...${NC}"
    git pull origin develop

    if [ ! -f .env ]; then
        echo -e "${RED}Error: No existe el archivo .env en el servidor.${NC}"
        exit 1
    fi

    echo -e "${YELLOW}[+] Reconstruyendo imágenes de producción con Docker...${NC}"
    $COMPOSE --profile prod build app-prod

    echo -e "${YELLOW}[+] Iniciando contenedores...${NC}"
    $COMPOSE --profile prod up -d sqlserver app-prod

    echo -e "${YELLOW}[+] Verificando estado del servicio (Healthcheck)...${NC}"
    sleep 10

    HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:8080 || true)

    if [ "$HTTP_STATUS" -eq 200 ]; then
        echo -e "${GREEN}[✓] DESPLIEGUE EN VM EXITOSO (HTTP 200).${NC}"
    else
        echo -e "${RED}[!] ADVERTENCIA: La app devolvió el código HTTP $HTTP_STATUS.${NC}"
        echo -e "Revisa los logs con: ${YELLOW}$COMPOSE logs app-prod${NC}"
    fi
}

# ----------------------------------------------------
# Modo 2: Despliegue en Azure Container Apps (ACA)
# ----------------------------------------------------
deploy_aca() {
    echo -e "${GREEN}=== Desplegando en Azure Container Apps ===${NC}"

    # 1. Validar que Azure CLI esté instalado
    if ! command -v az &> /dev/null; then
        echo -e "${RED}Error: Azure CLI ('az') no está instalado.${NC}"
        exit 1
    fi

    # Variables configurables de Azure (ajusta a tus nombres de recursos)
    RESOURCE_GROUP="${AZURE_RESOURCE_GROUP:-rg-openclient}"
    CONTAINER_APP_NAME="${AZURE_APP_NAME:-app-openclient}"
    REGISTRY_NAME="${AZURE_REGISTRY_NAME:-acropenclient}"

    echo -e "${YELLOW}[+] Comprobando sesión activa en Azure...${NC}"
    az account show > /dev/null 2>&1 || az login

    echo -e "${YELLOW}[+] Compilando y subiendo imagen a Azure Container Registry (ACR)...${NC}"
    az acr build \
        --registry "$REGISTRY_NAME" \
        --image openclient-app:latest \
        --file docker/Dockerfile \
        --target production .

    echo -e "${YELLOW}[+] Actualizando Container App con la nueva imagen...${NC}"
    az containerapp update \
        --name "$CONTAINER_APP_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --image "${REGISTRY_NAME}.azurecr.io/openclient-app:latest"

    echo -e "${GREEN}[✓] DESPLIEGUE EN AZURE CONTAINER APPS COMPLETADO EXITOSAMENTE.${NC}"
}

# ----------------------------------------------------
# Flujo Principal y Selección
# ----------------------------------------------------
MODE="$1"

if [ -z "$MODE" ]; then
    echo -e "${YELLOW}Selecciona el entorno de despliegue:${NC}"
    echo "1) Máquina Virtual / Ubuntu (Docker Compose)"
    echo "2) Azure Container Apps (Nube Azure)"
    read -p "Opción [1-2]: " choice
    case "$choice" in
        1) MODE="--vm" ;;
        2) MODE="--aca" ;;
        *) echo -e "${RED}Opción no válida.${NC}"; exit 1 ;;
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
        echo -e "${RED}Opción desconocida: $MODE${NC}"
        show_help
        exit 1
        ;;
esac
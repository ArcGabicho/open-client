#!/usr/bin/env bash
set -euo pipefail

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# ---- Configuracion (todo overrideable por variable de entorno) ----
LOCATION="${AZURE_LOCATION:-eastus}"
NAME_PREFIX="${AZURE_NAME_PREFIX:-openclient}"
RESOURCE_GROUP="${AZURE_RESOURCE_GROUP:-rg-${NAME_PREFIX}}"
DEPLOYMENT_NAME="${AZURE_DEPLOYMENT_NAME:-openclient-infra}"
IMAGE_REPOSITORY="openclient-app"
IMAGE_TAG="${IMAGE_TAG:-$(date +%Y%m%d%H%M%S)}"

SQL_ADMIN_LOGIN="${SQL_ADMIN_LOGIN:-openclientadmin}"
APP_ADMIN_EMAIL="${APP_ADMIN_EMAIL:-admin@openclient.local}"

# ---- Politica de contrasena (igual que scripts/deploy.sh) ----
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

prompt_secret() {
    # $1 = nombre de la variable ; $2 = etiqueta
    local var="$1" label="$2" value value2
    if [ -n "${!var:-}" ]; then
        if ! password_policy_ok "${!var}"; then
            echo -e "${RED}Error: ${var} no cumple la politica (8-128 chars, 3 de 4 grupos).${NC}"
            exit 1
        fi
        return
    fi
    if [ ! -t 0 ] && [ ! -r /dev/tty ]; then
        echo -e "${RED}Error: ${var} no definida y no hay terminal interactiva.${NC}"
        echo -e "Exporta ${YELLOW}${var}${NC} antes de ejecutar el script."
        exit 1
    fi
    while true; do
        read -r -s -p "$label: " value < /dev/tty; echo
        read -r -s -p "Repite $label: " value2 < /dev/tty; echo
        if [ "$value" != "$value2" ]; then
            echo -e "${RED}  No coinciden.${NC}"; continue
        fi
        if ! password_policy_ok "$value"; then
            echo -e "${RED}  Debil: min 8 caracteres y 3 de 4 grupos (MAYUS, minus, digito, simbolo).${NC}"; continue
        fi
        printf -v "$var" '%s' "$value"
        break
    done
}

# ---- Comprobaciones previas ----
command -v az &> /dev/null || { echo -e "${RED}Error: Azure CLI ('az') no esta instalado.${NC}"; exit 1; }

echo -e "${YELLOW}[+] Comprobando sesion de Azure...${NC}"
az account show > /dev/null 2>&1 || az login > /dev/null
SUBSCRIPTION="$(az account show --query name -o tsv)"
echo -e "${GREEN}[✓] Suscripcion activa: ${SUBSCRIPTION}${NC}"

prompt_secret SQL_ADMIN_PASSWORD "Password del administrador de Azure SQL"
prompt_secret APP_ADMIN_PASSWORD "Password del administrador de la aplicacion"

# ---- 1. Grupo de recursos ----
# Si ya existe, se reutiliza su region (los recursos usan resourceGroup().location).
if az group show --name "$RESOURCE_GROUP" &> /dev/null; then
    LOCATION="$(az group show --name "$RESOURCE_GROUP" --query location -o tsv)"
    echo -e "${YELLOW}[+] Grupo de recursos ${RESOURCE_GROUP} ya existe (${LOCATION}).${NC}"
else
    echo -e "${YELLOW}[+] Creando grupo de recursos ${RESOURCE_GROUP} (${LOCATION})...${NC}"
    az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none
fi

# ---- 2. Fase 1: infraestructura sin el Container App ----
# Crea ACR, SQL y el entorno de Container Apps. El Container App se omite
# (deployApp=false) porque su imagen aun no existe en el registro.
echo -e "${YELLOW}[+] Fase 1/3: aprovisionando infraestructura (Bicep)...${NC}"
az deployment group create \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --template-file "$SCRIPT_DIR/main.bicep" \
    --parameters \
        namePrefix="$NAME_PREFIX" \
        deployApp=false \
        containerImage="unused-in-phase-1" \
        sqlAdminLogin="$SQL_ADMIN_LOGIN" \
        sqlAdminPassword="$SQL_ADMIN_PASSWORD" \
        appAdminEmail="$APP_ADMIN_EMAIL" \
        appAdminPassword="$APP_ADMIN_PASSWORD" \
    --output none

ACR_NAME="$(az deployment group show --resource-group "$RESOURCE_GROUP" --name "$DEPLOYMENT_NAME" --query properties.outputs.acrName.value -o tsv)"
ACR_LOGIN_SERVER="$(az deployment group show --resource-group "$RESOURCE_GROUP" --name "$DEPLOYMENT_NAME" --query properties.outputs.acrLoginServer.value -o tsv)"

# ---- 3. Fase 2: construir la imagen de la aplicacion en ACR ----
echo -e "${YELLOW}[+] Fase 2/3: construyendo imagen en ACR (${ACR_NAME})...${NC}"
az acr build \
    --registry "$ACR_NAME" \
    --image "${IMAGE_REPOSITORY}:${IMAGE_TAG}" \
    --image "${IMAGE_REPOSITORY}:latest" \
    --file "$REPO_ROOT/docker/Dockerfile" \
    "$REPO_ROOT"

# ---- 4. Fase 3: desplegar el Container App con la imagen real ----
echo -e "${YELLOW}[+] Fase 3/3: publicando ${IMAGE_REPOSITORY}:${IMAGE_TAG}...${NC}"
az deployment group create \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --template-file "$SCRIPT_DIR/main.bicep" \
    --parameters \
        namePrefix="$NAME_PREFIX" \
        deployApp=true \
        sqlAdminLogin="$SQL_ADMIN_LOGIN" \
        sqlAdminPassword="$SQL_ADMIN_PASSWORD" \
        appAdminEmail="$APP_ADMIN_EMAIL" \
        appAdminPassword="$APP_ADMIN_PASSWORD" \
        containerImage="${ACR_LOGIN_SERVER}/${IMAGE_REPOSITORY}:${IMAGE_TAG}" \
    --output none

APP_URL="$(az deployment group show --resource-group "$RESOURCE_GROUP" --name "$DEPLOYMENT_NAME" --query properties.outputs.appUrl.value -o tsv)"

echo -e "${GREEN}=======================================================${NC}"
echo -e "${GREEN}Despliegue en Azure completado.${NC}"
echo -e "URL:          ${YELLOW}${APP_URL}${NC}"
echo -e "Grupo:        ${YELLOW}${RESOURCE_GROUP}${NC}"
echo -e "Admin app:    ${YELLOW}${APP_ADMIN_EMAIL}${NC}"
echo -e "La app aplica migraciones EF Core y siembra datos en el primer arranque;"
echo -e "puede tardar 1-2 minutos en responder 200 en ${YELLOW}${APP_URL}/health${NC}."
echo -e "${GREEN}=======================================================${NC}"
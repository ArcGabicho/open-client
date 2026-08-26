#!/usr/bin/env bash
set -e

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

REPO_URL="https://github.com/ArcGabicho/open-client.git"
TARGET_DIR="$HOME/open-client"
BRANCH="master"

echo -e "${GREEN}=== Instalacion automatica One-Liner de open-client ===${NC}"

echo -e "${YELLOW}[+] Verificando utilidades del sistema (git, curl, docker)...${NC}"

install_dependencies() {
    if [ -f /etc/os-release ]; then
        . /etc/os-release
        case "$ID" in
            ubuntu|debian)
                echo -e "${YELLOW}[+] Instalando paquetes base con apt...${NC}"
                sudo apt-get update -y
                sudo apt-get install -y git curl docker.io docker-compose-v2
                sudo systemctl enable --now docker
                ;;
            cachyos|arch|manjaro)
                echo -e "${YELLOW}[+] Instalando paquetes base con pacman (Arch/CachyOS)...${NC}"
                sudo pacman -Sy --needed --noconfirm git curl docker docker-compose
                sudo systemctl enable --now docker
                ;;
            fedora|rhel|centos)
                echo -e "${YELLOW}[+] Instalando paquetes base con dnf...${NC}"
                sudo dnf install -y git curl docker docker-compose-plugin
                sudo systemctl enable --now docker
                ;;
            *)
                echo -e "${RED}[!] Distribucion no soportada automaticamente: $ID.${NC}"
                echo -e "Por favor, instala git, curl y docker manualmente."
                exit 1
                ;;
        esac
    fi
}

if ! command -v git &> /dev/null || ! command -v docker &> /dev/null || ! command -v curl &> /dev/null; then
    install_dependencies
fi

if ! groups | grep -q docker; then
    echo -e "${YELLOW}[+] Anadiendo usuario al grupo docker...${NC}"
    sudo usermod -aG docker $USER || true
fi

if [ "$(pwd)" != "$TARGET_DIR" ]; then
    if [ -d "$TARGET_DIR" ]; then
        echo -e "${YELLOW}[+] El directorio ya existe. Actualizando codigo...${NC}"
        cd "$TARGET_DIR"
        git fetch origin
        git checkout $BRANCH
        git pull origin $BRANCH
    else
        echo -e "${YELLOW}[+] Clonando repositorio en $TARGET_DIR...${NC}"
        git clone -b $BRANCH $REPO_URL "$TARGET_DIR"
        cd "$TARGET_DIR"
    fi
fi

if [ ! -f .env ]; then
    echo -e "${YELLOW}[+] Generando credenciales seguras para Produccion...${NC}"
    PROD_PASS="ProdPass_$(openssl rand -hex 12)!"
    PROD_APP_PASS="AppPass_$(openssl rand -hex 12)!"
    PROD_ADMIN_PASS="AdminPass_$(openssl rand -hex 12)!"

    cat <<EOF > .env
MSSQL_PASSWORD=${PROD_PASS}
MSSQL_APP_PASSWORD=${PROD_APP_PASS}
OPENCLIENT_ADMIN_EMAIL=admin@openclient.local
OPENCLIENT_ADMIN_PASSWORD=${PROD_ADMIN_PASS}
EOF
    echo -e "${GREEN}[✓] Contrasenas de produccion (SA, usuario de app y admin) generadas en .env de forma segura.${NC}"
fi

chmod +x scripts/*.sh
echo -e "${YELLOW}[+] Desplegando stack con Docker Compose...${NC}"
./scripts/deploy.sh

echo -e "${GREEN}=======================================================${NC}"
echo -e "${GREEN}¡open-client ha sido desplegado exitosamente!${NC}"
echo -e "Directorio: ${YELLOW}$TARGET_DIR${NC}"
echo -e "Recuerda respaldar la contrasena generada en tu archivo ${YELLOW}.env${NC}"
echo -e "${GREEN}=======================================================${NC}"

#!/usr/bin/env bash
set -e

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

echo -e "${GREEN}=== Configurando entorno de desarrollo para open-client ===${NC}"

command -v docker >/dev/null 2>&1 || { echo -e "${RED}Error: Docker no está instalado.${NC}"; exit 1; }
command -v docker compose >/dev/null 2>&1 || { echo -e "${RED}Error: Docker Compose no está instalado.${NC}"; exit 1; }

if [ ! -f .env ]; then
    if [ -f .env.example ]; then
        echo -e "${YELLOW}[+] Creando archivo .env desde .env.example...${NC}"
        cp .env.example .env

        SA_PASS="DevPass_$(openssl rand -hex 6)!"
        APP_PASS="AppPass_$(openssl rand -hex 6)!"
        sed -i "s/MSSQL_PASSWORD=.*/MSSQL_PASSWORD=${SA_PASS}/" .env
        sed -i "s/MSSQL_APP_PASSWORD=.*/MSSQL_APP_PASSWORD=${APP_PASS}/" .env
        echo -e "${GREEN}[✓] Archivo .env generado con claves aleatorias temporales.${NC}"
    else
        echo -e "${YELLOW}[!] No se encontró .env.example. Creando .env básico...${NC}"
        {
            echo "MSSQL_PASSWORD=DevPass_$(openssl rand -hex 6)!"
            echo "MSSQL_APP_PASSWORD=AppPass_$(openssl rand -hex 6)!"
        } > .env
    fi
else
    echo -e "${GREEN}[✓] Archivo .env detectado.${NC}"

    if ! grep -q "MSSQL_APP_PASSWORD=" .env; then
        APP_PASS="AppPass_$(openssl rand -hex 6)!"
        echo "MSSQL_APP_PASSWORD=${APP_PASS}" >> .env
        echo -e "${YELLOW}[+] Variable MSSQL_APP_PASSWORD agregada al .env existente.${NC}"
    fi
fi

if command -v dotnet >/dev/null 2>&1; then
    echo -e "${YELLOW}[+] Restaurando paquetes .NET del proyecto...${NC}"
    dotnet restore core/*.csproj
fi

COMPOSE="docker compose --env-file .env -f docker/docker-compose.yml"

echo -e "${YELLOW}[+] Levantando infraestructura de desarrollo (SQL Server + inicialización de BD)...${NC}"
$COMPOSE up -d sqlserver db-init

echo -e "${YELLOW}[+] Esperando la inicialización de la base de datos (db-init)...${NC}"
$COMPOSE up db-init || { echo -e "${RED}Error: falló la inicialización de la base de datos.${NC}"; exit 1; }

echo -e "${GREEN}=======================================================${NC}"
echo -e "${GREEN}¡Entorno de desarrollo listo!${NC}"
echo -e "• Base de datos:     ${YELLOW}OpenClientDb @ localhost:1433${NC}"
echo -e "• Usuario de app:    ${YELLOW}openclient_user${NC}"
echo -e "Para arrancar la app en el host ejecuta: ${YELLOW}./scripts/run.sh${NC}"
echo -e "${GREEN}=======================================================${NC}"

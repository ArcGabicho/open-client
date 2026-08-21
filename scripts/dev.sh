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
        
        DEV_PASS="DevPass_$(openssl rand -hex 6)!"
        sed -i "s/MSSQL_PASSWORD=.*/MSSQL_PASSWORD=${DEV_PASS}/" .env
        echo -e "${GREEN}[✓] Archivo .env generado con clave aleatoria temporal.${NC}"
    else
        echo -e "${YELLOW}[!] No se encontró .env.example. Creando .env básico...${NC}"
        echo "MSSQL_PASSWORD=DevPass_$(openssl rand -hex 6)!" > .env
    fi
else
    echo -e "${GREEN}[✓] Archivo .env detectado.${NC}"
fi

if command -v dotnet >/dev/null 2>&1; then
    echo -e "${YELLOW}[+] Restaurando paquetes .NET del proyecto...${NC}"
    dotnet restore core/*.csproj
fi

COMPOSE="docker compose --env-file .env -f docker/docker-compose.yml"

echo -e "${YELLOW}[+] Levantando contenedores de desarrollo con Docker Compose...${NC}"
$COMPOSE up -d sqlserver app-dev

echo -e "${GREEN}=======================================================${NC}"
echo -e "${GREEN}¡Entorno de desarrollo listo!${NC}"
echo -e "• App Blazor (Dev): ${YELLOW}http://localhost:5000${NC}"
echo -e "• SQL Server:       ${YELLOW}localhost:1433${NC}"
echo -e "Para ver los logs en vivo ejecuta: ${YELLOW}./scripts/run.sh --logs${NC}"
echo -e "${GREEN}=======================================================${NC}"
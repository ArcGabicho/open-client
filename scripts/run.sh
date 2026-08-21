#!/usr/bin/env bash
set -e

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

COMPOSE="docker compose --env-file .env -f docker/docker-compose.yml"

show_help() {
    echo -e "Uso: ./scripts/run.sh [OPCION]"
    echo -e "Opciones:"
    echo -e "  (ninguna)   Inicia la BD en Docker y la app en el host con dotnet run"
    echo -e "  --stop      Detiene los contenedores de Docker"
    echo -e "  --logs      Muestra los logs de SQL Server"
    echo -e "  --help      Muestra este mensaje de ayuda"
}

MODE=""

for arg in "$@"; do
    case "$arg" in
        --stop|--logs|--help) MODE="$arg" ;;
        *)
            echo -e "${RED}Opcion no valida: $arg${NC}"
            show_help
            exit 1
            ;;
    esac
done

case "$MODE" in
    --stop)
        echo -e "${YELLOW}[+] Deteniendo contenedores...${NC}"
        $COMPOSE down
        echo -e "${GREEN}[✓] Contenedores detenidos.${NC}"
        ;;
    --logs)
        $COMPOSE logs -f sqlserver
        ;;
    --help)
        show_help
        ;;
    "")
        echo -e "${GREEN}[+] Levantando SQL Server en Docker...${NC}"
        $COMPOSE up -d

        echo -e "${YELLOW}[+] Restaurando paquetes .NET...${NC}"
        dotnet restore ./core/*.csproj

        echo -e "${GREEN}[+] Iniciando open-client en el host (dotnet run)...${NC}"
        ASPNETCORE_URLS="http://localhost:5000" dotnet run --project core/openclient.csproj
        ;;
esac
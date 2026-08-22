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
    echo -e "  (ninguna)   Inicia la BD en Docker (sqlserver + db-init) y la app en el host con dotnet run"
    echo -e "  --full      Levanta el stack completo en Docker (BD + app en contenedor, puerto 8080)"
    echo -e "  --stop      Detiene los contenedores de Docker"
    echo -e "  --logs      Muestra los logs de SQL Server"
    echo -e "  --help      Muestra este mensaje de ayuda"
}

MODE=""

for arg in "$@"; do
    case "$arg" in
        --full|--stop|--logs|--help) MODE="$arg" ;;
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
    --full)
        echo -e "${GREEN}[+] Levantando el stack completo en Docker (BD + inicialización + app)...${NC}"
        $COMPOSE up -d --build

        sleep 5
        HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:8080 || true)

        if [ "$HTTP_STATUS" -eq 200 ]; then
            echo -e "${GREEN}[✓] Stack completo en marcha. App disponible en ${YELLOW}http://localhost:8080${NC}"
        else
            echo -e "${YELLOW}[!] La app devolvió el código HTTP $HTTP_STATUS, puede estar iniciando aún.${NC}"
            echo -e "Revisa los logs con: ${YELLOW}$COMPOSE logs -f openclient${NC}"
        fi
        ;;
    "")
        if [ ! -f .env ]; then
            echo -e "${RED}Error: No existe .env. Ejecuta primero ./scripts/dev.sh${NC}"
            exit 1
        fi

        set -a
        # shellcheck disable=SC1091
        source .env
        set +a

        echo -e "${GREEN}[+] Levantando infraestructura de BD en Docker (sqlserver + db-init)...${NC}"
        $COMPOSE up -d sqlserver db-init

        echo -e "${YELLOW}[+] Esperando la inicialización de la base de datos (db-init)...${NC}"
        $COMPOSE up db-init || { echo -e "${RED}Error: falló la inicialización de la base de datos.${NC}"; exit 1; }

        echo -e "${YELLOW}[+] Restaurando paquetes .NET...${NC}"
        dotnet restore ./core/*.csproj

        echo -e "${GREEN}[+] Iniciando open-client en el host (dotnet run)...${NC}"
        ASPNETCORE_URLS="http://localhost:5000" \
        ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=OpenClientDb;User Id=openclient_user;Password=${MSSQL_APP_PASSWORD};TrustServerCertificate=True;" \
            dotnet run --project core/openclient.csproj
        ;;
esac

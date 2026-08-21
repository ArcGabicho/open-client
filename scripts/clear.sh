#!/usr/bin/env bash
set -e

RED='\033[0;31m'
YELLOW='\033[1;33m'
GREEN='\033[0;32m'
NC='\033[0m'

COMPOSE="docker compose --env-file .env -f docker/docker-compose.yml"

echo -e "${RED}=== Limpieza del proyecto open-client ===${NC}"

read -p "¿Deseas eliminar también los volúmenes de la BASE DE DATOS? (s/N): " -n 1 -r
echo
REMOVE_VOLUMES=false
if [[ $REPLY =~ ^[Ss]$ ]]; then
    REMOVE_VOLUMES=true
fi

echo -e "${YELLOW}[+] Deteniendo y eliminando contenedores...${NC}"

if [ "$REMOVE_VOLUMES" = true ]; then
    echo -e "${RED}[!] ELIMINANDO VOLÚMENES Y DATOS DE SQL SERVER...${NC}"
    $COMPOSE down -v
else
    $COMPOSE down
fi

echo -e "${YELLOW}[+] Limpiando artefactos de compilación (core/bin, core/obj)...${NC}"
rm -rf core/bin core/obj

echo -e "${YELLOW}[+] Limpiando caché NuGet local...${NC}"
if command -v dotnet >/dev/null 2>&1; then
    dotnet nuget locals all --clear 2>/dev/null || true
fi

echo -e "${GREEN}[✓] Limpieza completada exitosamente.${NC}"
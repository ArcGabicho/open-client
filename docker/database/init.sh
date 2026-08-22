#!/bin/bash

set -e

SQLCMD="/opt/mssql-tools18/bin/sqlcmd"
SQLSERVER_HOST="sqlserver"

sqlcmd() {
    $SQLCMD \
        -S "$SQLSERVER_HOST" \
        -U sa \
        -P "$MSSQL_PASSWORD" \
        -C \
        -b \
        -f 65001 \
        "$@"
}

get_clients_count() {
    local count
    count=$($SQLCMD \
        -S "$SQLSERVER_HOST" \
        -U sa \
        -P "$MSSQL_PASSWORD" \
        -C \
        -b \
        -h -1 \
        -Q "SET NOCOUNT ON;
            IF OBJECT_ID(N'[OpenClientDb].[dbo].[Clients]', N'U') IS NULL
                SELECT CAST(0 AS INT);
            ELSE
                SELECT COUNT(*) FROM [OpenClientDb].[dbo].[Clients];")
    echo "$count" | tr -d '[:space:]'
}

echo "Esperando a SQL Server..."

until sqlcmd -Q "SELECT 1" > /dev/null 2>&1
do
    sleep 2
done

echo "SQL Server esta listo."

echo "Inicializando la base de datos de Open Client (estructura)."

sed "s|__MSSQL_APP_PASSWORD__|$MSSQL_APP_PASSWORD|g" \
    /init.sql > /tmp/init.sql

sqlcmd -i /tmp/init.sql

echo "Estructura de base de datos verificada."

CLIENTS_BEFORE=$(get_clients_count)

if [ -z "$CLIENTS_BEFORE" ] || ! [ "$CLIENTS_BEFORE" -eq "$CLIENTS_BEFORE" ] 2>/dev/null; then
    echo "ERROR: No se pudo determinar el estado de dbo.Clients." >&2
    exit 1
fi

if [ "$CLIENTS_BEFORE" -gt 0 ]; then
    echo "Seed omitido: dbo.Clients ya contiene ${CLIENTS_BEFORE} registros."
else
    echo "Tabla dbo.Clients vacia. Cargando seed de Open Client (transaccion atomica)..."

    # El seed se envuelve en una unica transaccion: BEGIN TRAN sobrevive a los
    # separadores GO (es de sesion). Con XACT_ABORT ON y sqlcmd -b, cualquier
    # error aborta el proceso y el cierre de conexion hace ROLLBACK total,
    # evitando seeds parciales que el guard confundiria con "ya sembrado".
    {
        echo "SET XACT_ABORT ON;"
        echo "BEGIN TRANSACTION;"
        cat /seed.sql
        echo ""
        echo "IF @@TRANCOUNT > 0 COMMIT TRANSACTION;"
    } > /tmp/seed_tx.sql

    sqlcmd -d OpenClientDb -i /tmp/seed_tx.sql

    CLIENTS_AFTER=$(get_clients_count)

    if [ -z "$CLIENTS_AFTER" ] || [ "$CLIENTS_AFTER" -eq 0 ]; then
        echo "ERROR: El seed no inserto ningun registro en dbo.Clients." >&2
        exit 1
    fi

    echo "Seed aplicado correctamente."
fi

CLIENTS_FINAL=$(get_clients_count)

echo "=============================================="
echo "Registros en dbo.Clients: ${CLIENTS_FINAL}"
echo "Inicialización de base de datos completada."
echo "=============================================="

exit 0

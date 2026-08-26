#!/bin/bash
set -e

SQLCMD="/opt/mssql-tools18/bin/sqlcmd"
SQLSERVER_HOST="sqlserver"

TMP_INIT_SQL="/tmp/init.sql"

cleanup() {
    rm -f "$TMP_INIT_SQL"
}

trap cleanup EXIT INT TERM

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

replace_placeholders() {
    local template="$1" output="$2" line
    : > "$output"
    while IFS= read -r line || [ -n "$line" ]; do
        line="${line//__MSSQL_APP_PASSWORD__/${MSSQL_APP_PASSWORD:-}}"
        printf '%s\n' "$line" >> "$output"
    done < "$template"
}

echo "Esperando a SQL Server..."

until sqlcmd -Q "SELECT 1" > /dev/null 2>&1
do
    sleep 2
done

echo "SQL Server esta listo."

echo "Inicializando login y usuario de Open Client..."

replace_placeholders /init.sql "$TMP_INIT_SQL"

sqlcmd -i "$TMP_INIT_SQL"

unset MSSQL_PASSWORD MSSQL_APP_PASSWORD

rm -f "$TMP_INIT_SQL"

echo "Login y usuario verificados."

exit 0
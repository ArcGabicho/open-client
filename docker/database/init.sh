#!/bin/bash

set -e

SQLCMD="/opt/mssql-tools18/bin/sqlcmd"
SQLSERVER_HOST="sqlserver"

TMP_INIT_SQL="/tmp/init.sql"
TMP_ADMIN_SQL="/tmp/admin.sql"
TMP_SEED_SQL="/tmp/seed_tx.sql"

cleanup() {
    rm -f "$TMP_INIT_SQL" "$TMP_ADMIN_SQL" "$TMP_SEED_SQL"
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

# Escapa comillas simples para literales T-SQL: ' -> ''
tsql_escape() {
    local q="'"
    local s=$1
    printf '%s' "${s//$q/$q$q}"
}

# Sustituye placeholders de forma literal, sin sed ni regex:
# inmune a caracteres especiales (& \ | $ . *) en valores provenientes de .env.
replace_placeholders() {
    local template="$1" output="$2" line
    : > "$output"
    while IFS= read -r line || [ -n "$line" ]; do
        line="${line//__MSSQL_APP_PASSWORD__/${MSSQL_APP_PASSWORD:-}}"
        line="${line//__ADMIN_EMAIL__/${ADMIN_EMAIL_ESCAPED:-}}"
        line="${line//__ADMIN_PASSWORD_HASH__/${ADMIN_PASSWORD_HASH:-}}"
        printf '%s\n' "$line" >> "$output"
    done < "$template"
}

echo "Esperando a SQL Server..."

until sqlcmd -Q "SELECT 1" > /dev/null 2>&1
do
    sleep 2
done

echo "SQL Server esta listo."

echo "Inicializando la base de datos de Open Client (estructura)."

replace_placeholders /init.sql "$TMP_INIT_SQL"

sqlcmd -i "$TMP_INIT_SQL"

echo "Estructura de base de datos verificada."

echo "Configurando administrador inicial..."

if [ -z "${OPENCLIENT_ADMIN_EMAIL:-}" ]; then
    echo "ERROR: OPENCLIENT_ADMIN_EMAIL no está definido." >&2
    exit 1
fi

if [ -z "${OPENCLIENT_ADMIN_PASSWORD:-}" ]; then
    echo "ERROR: OPENCLIENT_ADMIN_PASSWORD no está definido." >&2
    exit 1
fi

if [[ ! "$OPENCLIENT_ADMIN_EMAIL" =~ ^[^@[:space:]]+@[^@[:space:]]+$ ]]; then
    echo "ERROR: OPENCLIENT_ADMIN_EMAIL no tiene un formato valido." >&2
    exit 1
fi

# PasswordHasher (self-contained linux-x64) lee OPENCLIENT_ADMIN_PASSWORD
# desde el entorno; la contraseña nunca pasa por argv ni por archivos.
# Se ejecuta el binario directamente: NO existe /PasswordHasher.dll en la
# imagen porque el publish es single-file.
if ! ADMIN_PASSWORD_HASH=$(/PasswordHasher); then
    echo "ERROR: PasswordHasher falló al generar el hash." >&2
    exit 1
fi

if [ -z "$ADMIN_PASSWORD_HASH" ]; then
    echo "ERROR: No se pudo generar el hash del administrador." >&2
    exit 1
fi

# Formato BCrypt valido: $2a/$2b/$2y + coste (2 digitos) + 53 caracteres
# [./A-Za-z0-9] (22 de salt + 31 de hash = 60 en total).
BCRYPT_RE='^\$2[aby]\$[0-9]{2}\$[./A-Za-z0-9]{53}$'

if ! printf '%s' "$ADMIN_PASSWORD_HASH" | grep -Eq "$BCRYPT_RE"; then
    echo "ERROR: El hash generado no tiene un formato BCrypt valido." >&2
    exit 1
fi

echo "Hash del administrador generado correctamente."

ADMIN_EMAIL_ESCAPED=$(tsql_escape "$OPENCLIENT_ADMIN_EMAIL")

replace_placeholders /admin.sql "$TMP_ADMIN_SQL"

sqlcmd -d OpenClientDb -i "$TMP_ADMIN_SQL"

unset ADMIN_PASSWORD_HASH ADMIN_EMAIL_ESCAPED OPENCLIENT_ADMIN_PASSWORD

rm -f "$TMP_ADMIN_SQL"

echo "Administrador verificado."

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
    } > "$TMP_SEED_SQL"

    sqlcmd -d OpenClientDb -i "$TMP_SEED_SQL"

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

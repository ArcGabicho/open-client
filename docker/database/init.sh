#!/bin/bash

set -e

echo "Esperando a SQL Server..."

until /opt/mssql-tools18/bin/sqlcmd \
	-S sqlserver \
       	-U sa \
       	-P "$MSSQL_PASSWORD" \
       	-C \
       	-Q "SELECT 1" > /dev/null 2>&1
do
	sleep 2
done

echo "SQL Server esta listo."

echo "Inicializando la base de datos de Open Client."

sed "s|__MSSQL_APP_PASSWORD__|$MSSQL_APP_PASSWORD|g" \
	/init.sql > /tmp/init.sql

/opt/mssql-tools18/bin/sqlcmd \
	-S sqlserver \
	-U sa \
	-P "$MSSQL_PASSWORD" \
	-C \
	-i /tmp/init.sql

echo "Inicialización de base de datos completada."

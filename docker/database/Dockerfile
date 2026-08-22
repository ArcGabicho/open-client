FROM mcr.microsoft.com/mssql/server:2022-latest

USER root

COPY init.sh /init.sh
COPY init.sql /init.sql
COPY seed.sql /seed.sql

RUN chmod +x /init.sh

ENTRYPOINT ["/init.sh"]
FROM mcr.microsoft.com/mssql/server:2022-latest

USER root

COPY init.sh /init.sh
COPY init.sql /init.sql
COPY admin.sql /admin.sql
COPY seed.sql /seed.sql
COPY PasswordHasher/publish/PasswordHasher /PasswordHasher

RUN chmod +x /init.sh /PasswordHasher

USER mssql

ENTRYPOINT ["/init.sh"]
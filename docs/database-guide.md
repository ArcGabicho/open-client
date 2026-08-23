# Inicialización de la Base de Datos y PasswordHasher

Este documento describe, de principio a fin, cómo se inicializa SQL Server para
OpenClient: creación de estructura, generación segura del hash del administrador,
inserción en `dbo.Users` y carga del seed de clientes. También documenta por qué
`PasswordHasher` es un binario **self-contained linux-x64 single-file** y por qué
**nunca** debe ejecutarse como `dotnet /PasswordHasher.dll`.

---

## 1. Arquitectura general

```
.env (fuera de Git)
  MSSQL_PASSWORD
  MSSQL_APP_PASSWORD
  OPENCLIENT_ADMIN_EMAIL
  OPENCLIENT_ADMIN_PASSWORD
        |
        |  docker compose --env-file .env  (variables de entorno del contenedor)
        v
   +-----------------+
   | sqlserver       |   mcr.microsoft.com/mssql/server:2022-latest
   | (healthy)       |   healthcheck: SELECT 1 con sqlcmd
   +-----------------+
        ^ depends_on: service_healthy
        |
   +-----------------+     +--------------------------------------+
   | db-init         |     | docker/database/Dockerfile           |
   | ENTRYPOINT      |     | COPY init.sh /init.sh                |
   | /init.sh        | <-- | COPY init.sql admin.sql seed.sql     |
   |                 |     | COPY PasswordHasher/publish/Password |
   +-----------------+     +--------------------------------------+
        |
        |-- 1. espera a SQL Server (sqlcmd SELECT 1)
        |-- 2. init.sql    -> DB, LOGIN/USER openclient_user, rol
        |-- 3. /PasswordHasher -> hash BCrypt de ADMIN_PASSWORD
        |-- 4. genera /tmp/admin.sql (plantilla con placeholders sustituidos)
        |-- 5. sqlcmd -i /tmp/admin.sql -> INSERT en dbo.Users
        |-- 6. seed.sql -> dbo.Clients (~4040 registros, transacción atómica)
        v
   exit 0  ->  recién entonces arranca openclient
```

| Archivo                           | Rol                                                          |
|-----------------------------------|--------------------------------------------------------------|
| `.env`                            | Única fuente de secretos (nunca se versiona)                  |
| `scripts/run.sh`                  | Publica PasswordHasher, construye y ejecuta `db-init`         |
| `docker/database/Dockerfile`      | Imagen `db-init` sobre `mssql/server:2022-latest`             |
| `docker/database/init.sh`         | Orquestador de la inicialización (ENTRYPOINT)                 |
| `docker/database/init.sql`        | DDL idempotente: DB, login, usuario, rol                      |
| `docker/database/admin.sql`       | Plantilla idempotente del administrador (`dbo.Users`)         |
| `docker/database/seed.sql`        | Dataset inicial de clientes (~4040 filas)                     |
| `docker/database/PasswordHasher/` | Utilidad .NET: contraseña -> hash BCrypt                      |
| `docker/docker-compose.yml`       | Servicios `sqlserver`, `db-init`, `openclient` y sus env vars |

---

## 2. Propósito de PasswordHasher

Utilidad mínima de consola (.NET 10, único paquete `BCrypt.Net-Next`):

1. Lee `OPENCLIENT_ADMIN_PASSWORD` desde el entorno.
2. Calcula el hash BCrypt con work factor 12.
3. Escribe **únicamente el hash** en stdout (60 caracteres, formato `$2a$12$...`).
4. Sale con código distinto de 0 si la variable no está definida.

```csharp
var password = Environment.GetEnvironmentVariable("OPENCLIENT_ADMIN_PASSWORD");
// ... validación ...
var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
Console.Write(hash);
```

¿Por qué existe? Porque SQL Server no puede calcular BCrypt y porque la
contraseña en claro **jamás** debe aparecer en un archivo `.sql` versionado, en
los logs o dentro de una imagen Docker. El hashing ocurre en memoria, dentro del
contenedor efímero `db-init`; a SQL solo llega el hash.

### Por qué es self-contained linux-x64

La imagen `db-init` está basada en `mssql/server:2022-latest`: **no incluye
.NET** y corre en Linux x64. Publicar self-contained incrusta el runtime de .NET
junto a la aplicación, de modo que el binario no necesita dependencias externas:

```bash
dotnet publish docker/database/PasswordHasher/PasswordHasher.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -o docker/database/PasswordHasher/publish
```

Con `-p:PublishSingleFile=true`, runtime + app + BCrypt.Net se empaquetan en un
único ejecutable nativo (~73 MB). Así el Dockerfile copia solamente ese archivo:

```dockerfile
COPY PasswordHasher/publish/PasswordHasher /PasswordHasher
RUN chmod +x /init.sh /PasswordHasher
USER mssql
ENTRYPOINT ["/init.sh"]
```

### Por qué NO debe ejecutarse `/PasswordHasher.dll`

Un publish self-contained "normal" (sin single-file) produce muchos archivos:
el lanzador nativo `PasswordHasher` (apphost) **más** `PasswordHasher.dll`,
`PasswordHasher.runtimeconfig.json`, `libcoreclr.so`, etc. El apphost no es
autosuficiente: al arrancar busca su ensamblado administrado hermano y, si falta,
falla exactamente con:

```
The application to execute does not exist: '/PasswordHasher.dll'.
```

Ese fue el fallo original del proyecto: la imagen solo contenía el apphost.
Ejecutar `dotnet /PasswordHasher.dll` tampoco sería posible: la imagen base no
tiene `dotnet`. Con single-file el ejecutable **es** la aplicación completa, se
invoca directamente como `/PasswordHasher` y no existe ningún `.dll` que buscar.

> Regla práctica: si el publish usa `-r linux-x64 --self-contained true
> -p:PublishSingleFile=true`, la única forma correcta de ejecutarlo es
> `/PasswordHasher`. Nunca `dotnet PasswordHasher.dll`.

---

## 3. Flujo detallado de init.sh

`init.sh` corre como usuario `mssql` dentro del contenedor one-shot `db-init`
(`restart: "no"`), con `set -e` y `sqlcmd -b`: cualquier error SQL o de shell
aborta todo el proceso con exit code distinto de 0.

### 3.1 Limpieza garantizada de temporales

```bash
trap cleanup EXIT INT TERM   # rm -f /tmp/init.sql /tmp/admin.sql /tmp/seed_tx.sql
```

Los tres archivos se generan dinámicamente en `/tmp` del contenedor (efímero,
descartado con `--rm`) y además se eliminan vía trap en éxito, error o señal.
Nunca se escriben dentro del repositorio.

### 3.2 Sustitución segura de placeholders (sin sed)

Tanto `init.sql` (`__MSSQL_APP_PASSWORD__`) como `admin.sql` (`__ADMIN_EMAIL__`,
`__ADMIN_PASSWORD_HASH__`) son plantillas. La sustitución usa expansión de
parámetros de bash (`${line//pat/rep}`), que es **literal**: contraseñas con
caracteres especiales (`& \ | $ ! *`) no corrompen el SQL, algo que sí ocurre
con `sed` (donde `&` del reemplazo significa "coincidencia completa").

Adicionalmente el email se escapa para T-SQL duplicando comillas simples
(`'` -> `''`), inmunizándolo contra ruptura del literal SQL.

### 3.3 Variables exclusivamente del entorno

```
OPENCLIENT_ADMIN_EMAIL     <- .env -> compose -> contenedor db-init
OPENCLIENT_ADMIN_PASSWORD  <- .env -> compose -> contenedor db-init
```

Validaciones de fallo rápido antes de continuar:

1. `OPENCLIENT_ADMIN_EMAIL` definido y con formato básico `x@y`.
2. `OPENCLIENT_ADMIN_PASSWORD` definido y no vacío.

La contraseña llega al hasher **por entorno** (heredada por el proceso hijo),
nunca por argv (visible en `ps`) ni por archivos intermedios.

### 3.4 Generación del hash y de /tmp/admin.sql

```bash
if ! ADMIN_PASSWORD_HASH=$(/PasswordHasher); then
    echo "ERROR: PasswordHasher falló al generar el hash." >&2
    exit 1
fi

[ -n "$ADMIN_PASSWORD_HASH" ] || { echo "ERROR: hash vacío." >&2; exit 1; }

ADMIN_EMAIL_ESCAPED=$(tsql_escape "$OPENCLIENT_ADMIN_EMAIL")
replace_placeholders /admin.sql "$TMP_ADMIN_SQL"    # -> /tmp/admin.sql
sqlcmd -d OpenClientDb -i "$TMP_ADMIN_SQL"

unset ADMIN_PASSWORD_HASH ADMIN_EMAIL_ESCAPED OPENCLIENT_ADMIN_PASSWORD
rm -f "$TMP_ADMIN_SQL"
```

- Si `PasswordHasher` falla (exit != 0) o devuelve vacío, el proceso aborta.
- El hash solo existe en memoria, en `/tmp/admin.sql` (efímero) y en la BD.
- Ni el password ni el hash se imprimen jamás; los logs solo dicen
  `Hash del administrador generado correctamente.`

### 3.5 Plantilla admin.sql

```sql
USE [OpenClientDb];
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'__ADMIN_EMAIL__')
BEGIN
    INSERT INTO dbo.Users (Email, PasswordHash, Role, IsActive)
    VALUES (
        N'__ADMIN_EMAIL__',
        N'__ADMIN_PASSWORD_HASH__',
        N'Admin',
        1
    );

    PRINT 'Administrador creado correctamente.';
END
ELSE
BEGIN
    PRINT 'El administrador ya existe.';
END
GO
```

Es idempotente: re-ejecuciones no duplican el administrador ni sobreescriben su
hash (útil si cambiaste `OPENCLIENT_ADMIN_PASSWORD` y quieres conservar el
acceso actual; ver sección de errores comunes). El mismo archivo crea la tabla
`dbo.Users` si aún no existe y otorga `SELECT` al rol `openclient_runtime`.

### 3.6 Seed de Clients

Si `COUNT(*)` de `dbo.Clients` es 0, `seed.sql` se envuelve en una transacción
(`SET XACT_ABORT ON` + `BEGIN TRANSACTION` + `COMMIT`). Con `sqlcmd -b`, un
error a mitad de archivo produce ROLLBACK total: nunca quedan seeds parciales.
Si ya hay registros, el seed se omite (idempotencia).

---

## 4. Cómo scripts/run.sh orquesta todo

Modo por defecto (`./scripts/run.sh`):

1. Valida que `.env` exista y carga sus variables (`set -a; source .env`).
2. `$COMPOSE up -d sqlserver` (espera healthcheck).
3. Publica PasswordHasher:

   ```bash
   dotnet publish docker/database/PasswordHasher/PasswordHasher.csproj \
       -c Release -r linux-x64 --self-contained true \
       -p:PublishSingleFile=true \
       -o docker/database/PasswordHasher/publish
   ```

4. `$COMPOSE build db-init`: construye la imagen copiando `init.sh`, los tres
   `.sql` y **solo el ejecutable single-file** `/PasswordHasher`.
5. `$COMPOSE run --rm db-init`: ejecuta la inicialización en primer plano con
   exit code real; si falla, `run.sh` se detiene.
6. Restaura paquetes y arranca la app en el host (`dotnet run`, puerto 5000).

En modo `--full`, Compose orquesta la cadena `sqlserver -> db-init ->
openclient` vía `depends_on` (la app arranca solo cuando `db-init` termina con
exit 0).

---

## 5. Configurar .env desde .env.example

```bash
cp .env.example .env
# Edita los valores:
#   MSSQL_PASSWORD             Contraseña del SA de SQL Server
#   MSSQL_APP_PASSWORD         Contraseña del login openclient_user
#   OPENCLIENT_ADMIN_EMAIL     Email del administrador inicial (dbo.Users)
#   OPENCLIENT_ADMIN_PASSWORD  Password del administrador (se guarda como hash)
```

- `./scripts/dev.sh` crea `.env` automáticamente (con claves aleatorias) si no
  existe, y completa las variables que falten en un `.env` previo.
- `.env` está ignorado por Git (`.gitignore`) y excluido del contexto Docker
  (`.dockerignore`). Nunca hagas commit ni copies ese archivo a la imagen.
- Cambiar `OPENCLIENT_ADMIN_PASSWORD` en `.env` NO actualiza el hash de un
  administrador ya creado (ver errores comunes).

## 6. Ejecutar ./scripts/run.sh

```bash
./scripts/run.sh          # BD en Docker + inicialización + app en host (:5000)
./scripts/run.sh --full   # Stack completo en Docker (:8080)
./scripts/run.sh --stop   # Detener contenedores
./scripts/run.sh --logs   # Logs de SQL Server
```

Reinicio completo desde cero (borra TODOS los datos del volumen):

```bash
docker compose --env-file .env -f docker/docker-compose.yml down -v
./scripts/run.sh
```

---

## 7. Comprobar que el administrador fue creado (sin revelar secretos)

Consulta el email, el rol y **solo el prefijo** del hash (los hashes BCrypt son
públicos por diseño; aun así evita volcarlos completos a consolas compartidas):

```bash
source .env
docker exec openclient-database /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$MSSQL_PASSWORD" -C -W \
    -Q "SET NOCOUNT ON;
        SELECT Email, Role, IsActive,
               LEFT(PasswordHash, 7) + '...(oculto)' AS HashPrefix,
               LEN(PasswordHash) AS HashLen
        FROM OpenClientDb.dbo.Users;"
```

Salida esperada (hash de 60 caracteres con prefijo `$2a$12$`):

```
Email                    Role  IsActive HashPrefix   HashLen
-----------------------  ----- -------- ------------ -------
admin@openclient.local   Admin 1        $2a$12$...   60
```

Verificación funcional end-to-end (sin exponer la contraseña: viaja expandida
por el shell, no escrita en ningún comando visible):

```bash
curl -i -X POST http://localhost:5000/api/auth/log-in \
     -H 'Content-Type: application/json' \
     -d "{\"email\":\"$OPENCLIENT_ADMIN_EMAIL\",\"password\":\"$OPENCLIENT_ADMIN_PASSWORD\"}"
# HTTP 200 = credenciales correctas | HTTP 401 = rechazadas
```

---

## 8. Higiene de secretos aplicada

- La contraseña del admin vive únicamente en `.env` (fuera de Git) y en las
  variables de entorno del contenedor; nunca en código fuente, Dockerfile,
  `.sql` versionados, logs ni argv de procesos.
- `admin.sql`, `init.sql` y `seed.sql` contienen solo placeholders.
- `/tmp/admin.sql` se genera on-the-fly dentro del contenedor efímero y se
  elimina con trap; nada se escribe en el repositorio.
- Los logs imprimen confirmaciones ("Administrador creado correctamente."),
  jamás valores.
- `.gitignore` excluye `.env`/`.env.*`; `.dockerignore` los excluye del
  contexto de build; `docker/database/PasswordHasher/publish/` no se versiona.
- No se hace `COPY .env` en ningún Dockerfile.
- Nota residual: el healthcheck de compose pasa `-P "$MSSQL_PASSWORD"` dentro
  de la definición del servicio (visible con `docker inspect`). Es el patrón
  oficial de la imagen `mssql/server`; si quisieras endurecerlo, migra a
  `DockerSecrets` o a un script de healthcheck sin credencial inline.

---

## 9. Errores comunes y solución

| Síntoma | Causa | Solución |
|---|---|---|
| `The application to execute does not exist: '/PasswordHasher.dll'.` | Publish sin `-p:PublishSingleFile=true` (el apphost busca su `.dll` hermano que la imagen no copia), o alguien cambió `init.sh` para invocar `dotnet ...dll`. | Publicar con el comando exacto de la sección 4 y ejecutar siempre `/PasswordHasher`. |
| `ERROR: OPENCLIENT_ADMIN_EMAIL/PASSWORD no está definido.` | Falta la variable en `.env`, o compose no la inyecta. | Añadirlas a `.env` (ver sección 5); comprobar `environment:` del servicio `db-init`. |
| Login del admin rechazado tras cambiar `OPENCLIENT_ADMIN_PASSWORD` en `.env` | El INSERT es idempotente: si el usuario ya existe conserva el hash original. | Borrar el usuario (`DELETE FROM dbo.Users WHERE Email = ...`) y re-ejecutar `db-init`, o resetear el volumen con `down -v`. |
| `Seed omitido` cuando esperabas recarga completa | Guard de idempotencia: `dbo.Clients` ya tiene filas. | Resetear volumen (`down -v`) — borra todos los datos. |
| `permission denied` ejecutando `/PasswordHasher` en el contenedor | Se quitó el `RUN chmod +x /init.sh /PasswordHasher` del Dockerfile. | Restaurar el Dockerfile original (sección 2). |
| Puerto 5000 ocupado al arrancar la app | Otra instancia sigue viva. | `fuser -k 5000/tcp` o `ss -tlnp \| grep 5000`. |

---

## 10. Resumen del contrato

1. `ADMIN_EMAIL`/password llegan **exclusivamente** de variables de entorno.
2. `PasswordHasher` se publica self-contained linux-x64 **single-file** y se
   ejecuta como `/PasswordHasher` (nunca `dotnet *.dll`).
3. El SQL con el hash se genera dinámicamente en `/tmp` y muere con el
   contenedor; el repositorio solo contiene plantillas.
4. Ningún log imprime contraseñas ni hashes.
5. Todo es idempotente: correr `run.sh` N veces no duplica admin ni seed.

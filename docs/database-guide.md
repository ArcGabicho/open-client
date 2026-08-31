# Inicializacion de la Base de Datos

Este documento describe como se inicializa y gestiona la base de datos de
Open Client: infraestructura SQL Server, esquema via EF Core, administrador
con BCrypt y seed de clientes.

---

## 1. Arquitectura

```
.env (fuera de Git)
  MSSQL_PASSWORD
  MSSQL_APP_PASSWORD
  ADMIN_EMAIL
  ADMIN_PASSWORD
        |
        |  docker compose --env-file .env
        v
   +-----------------+
   | sqlserver       |   mcr.microsoft.com/mssql/server:2022-latest
   | (healthy)       |   healthcheck: SELECT 1 con sqlcmd
   +-----------------+
        ^ depends_on: service_healthy
        |
   +-----------------+     +--------------------------------------+
   | db-init         |     | docker/database/Dockerfile           |
   | ENTRYPOINT      | <-- | COPY init.sh init.sql                |
   | /init.sh        |     | Solo crea LOGIN, USUARIO y ROL       |
   +-----------------+     +--------------------------------------+
        |
        |-- 1. espera a SQL Server (sqlcmd SELECT 1)
        |-- 2. init.sql -> LOGIN openclient_user, USUARIO, ROL
        v
   exit 0  ->  recien entonces arranca openclient

   openclient (ASP.NET Core)
        |
        |-- Program.cs ejecuta DbInitializer
        |       |
        |       |-- 1. Database.MigrateAsync()  (EF Core migrations)
        |       |-- 2. SeedAdminAsync()         (BCrypt + Users.Add)
        |       |-- 3. SeedClientsAsync()       (C# dataset -> batch INSERT)
        v
   Aplicacion lista
```

| Archivo                           | Rol                                                          |
|-----------------------------------|--------------------------------------------------------------|
| `.env`                            | Unica fuente de secretos (nunca se versiona)                  |
| `scripts/run.sh`                  | Construye y ejecuta `db-init`, luego lanza la app             |
| `docker/database/Dockerfile`      | Imagen `db-init` sobre `mssql/server:2022-latest`             |
| `docker/database/init.sh`         | Espera SQL Server y ejecuta init.sql                         |
| `docker/database/init.sql`        | DDL: LOGIN, USUARIO, ROL (infraestructura SQL Server)         |
| `docker/docker-compose.yml`       | Servicios `sqlserver`, `db-init`, `openclient` y sus env vars |
| `core/Data/DbInitializer.cs`      | Migraciones EF Core + admin + seed                           |
| `core/Data/DbSeeder.cs`           | Seed de clientes via EF Core                                 |
| `core/Data/SeedData/ClientSeedData.cs` | Dataset inicial de clientes (~4018 registros, C#)       |

---

## 2. Infraestructura SQL Server (Docker)

El contenedor `db-init` es la unica parte de Docker que ejecuta SQL.
Se encarga exclusivamente de crear:

- **LOGIN** `openclient_user` (autenticacion SQL Server)
- **Base de datos** `OpenClientDb`
- **USUARIO** `openclient_user` (mapeado al login)
- **ROL** `openclient_runtime`
- **Membria** del usuario en el rol

Esto es infraestructura, no logica de negocio.

### init.sql

```sql
-- Crea LOGIN openclient_user con password de .env
-- Crea base de datos OpenClientDb si no existe
-- Crea USUARIO openclient_user en la BD
-- Crea ROL openclient_runtime
-- Agrega usuario al rol
```

El unico placeholder es `__MSSQL_APP_PASSWORD__`, sustituido por `init.sh`.

---

## 3. EF Core Migrations

El esquema de las tablas `Users` y `Clients` se gestiona via EF Core migrations.
`dotnet-ef` esta fijado como herramienta local (`.config/dotnet-tools.json`):
`dotnet tool restore` antes de usarlo.

Migraciones actuales (en orden):

| Migracion | Contenido |
|-----------|-----------|
| `InitialCreate` | Tablas `Users` y `Clients`. |
| `AddClientSoftDeleteAndAudit` | `Client.UpdatedAt` / `IsDeleted` / `DeletedAt` + indice `IX_Clients_IsDeleted`. |
| `AddClientAnalyticsIndexes` | Indices sobre `Clients.CreatedAt`, `Industry`, `Province`, `District`, `JobTitle` (para las agregaciones de Analiticas). |
| `AddUserAccountFields` | `User.UserName` (unico), `LastLoginAt`, `ConcurrencyStamp` + indices `Role`/`LastName`/`IsActive`. Incluye backfill de filas existentes (`UserName` desde el email, `ConcurrencyStamp` con `NEWID()`) antes de crear el indice unico. |

### Crear una nueva migracion

```bash
export PATH="$PATH:$HOME/.dotnet/tools"
dotnet ef migrations add NombreMigracion \
    --project core/openclient.csproj \
    --output-dir Data/Migrations
```

### Aplicar manualmente

```bash
dotnet ef database update --project core/openclient.csproj
```

### Aplicacion automatica

La aplicacion ejecuta `Database.MigrateAsync()` al iniciar via `DbInitializer`.
No es necesario ejecutar `dotnet ef database update` manualmente.

**Nunca usar `EnsureCreated()`**. Siempre usar migraciones.

### Contexto y fabrica

En runtime se registra `AddDbContextFactory<OpenClientDbContext>` (no un
`DbContext` scoped): cada operacion abre su propio contexto con
`IDbContextFactory.CreateDbContextAsync(ct)`. Para que `dotnet ef` funcione con
ese registro existe `core/Data/Context/OpenClientDbContextFactory.cs`
(`IDesignTimeDbContextFactory`), que crea el contexto leyendo
`ConnectionStrings__DefaultConnection` del entorno.

---

## 3b. Borrado logico y auditoria

La entidad `Client` incluye:

| Campo | Tipo | Uso |
| --- | --- | --- |
| `UpdatedAt` | `datetime2` nullable | Lo fija `ClientService.UpdateAsync` en cada edicion. |
| `IsDeleted` | `bit`, default `0` | Borrado logico. Indice `IX_Clients_IsDeleted`. |
| `DeletedAt` | `datetime2` nullable | Momento del borrado. |

- `DELETE /api/clients/{id}` y el boton "Eliminar" del panel hacen **borrado
  logico**: `IsDeleted = 1`, `DeletedAt = UtcNow`. La fila permanece en la tabla.
- Todas las lecturas del repositorio filtran `!IsDeleted` de forma explicita (no
  hay `HasQueryFilter` global).
- Restaurar un cliente borrado se hace en base de datos
  (`UPDATE Clients SET IsDeleted = 0, DeletedAt = NULL WHERE Id = ...`).
- Migracion: `Data/Migrations/*_AddClientSoftDeleteAndAudit.cs`.

---

## 4. Administrador initial

Ubicacion: `core/Data/DbInitializer.cs`

### Flujo

1. Leer `ADMIN_EMAIL` y `ADMIN_PASSWORD` de la configuracion.
2. Si alguno no esta definido, omitir (warning).
3. Verificar si ya existe un usuario con ese email.
4. Si no existe: crear con `BCrypt.Net.BCrypt.HashPassword(password, 12)`.
5. Si ya existe: no sobreescribir el hash.

### Politica de contrasena

- `ADMIN_EMAIL`/`ADMIN_PASSWORD` se utilizan **unicamente** para provisionar
  el administrador inicial si no existe.
- Si ya existe un administrador con ese email, la aplicacion **NO** sobreescribe
  el hash existente.
- Cambiar `ADMIN_PASSWORD` en `.env` **NO** modifica la contrasena de un admin
  ya creado.
- Para resetear: eliminar la fila manualmente o recrear la BD.

### Seguridad

- La contrasena nunca se almacena en texto plano.
- El hash se genera en memoria con BCrypt (work factor 12).
- Los logs nunca imprimen la contrasena ni el hash.
- Los logs solo confirman: "Administrador creado correctamente."

---

## 5. Seed de clientes

Ubicacion: `core/Data/DbSeeder.cs` + `core/Data/SeedData/ClientSeedData.cs`

### Fuente de datos

Los ~4018 registros de clientes estan compilados en
`core/Data/SeedData/ClientSeedData.cs` como una clase estatica con una
propiedad `IReadOnlyList<Client>`. No hay dependencias de archivos externos.

### Insercion

- Si `Clients` tiene registros -> seed omitido.
- Si `Clients` esta vacio -> insertar todos en batches de 500.
- Cada batch se ejecuta dentro de una transaccion EF Core.
- Si falla a mitad -> rollback completo, no datos parciales.

### Idempotencia

El seed es idempotente:
- Primera ejecucion: inserta ~4018 clientes.
- Segunda ejecucion: omite (ya hay datos).
- Si el seed esta parcialmente aplicado: detecta que hay datos y omite.

---

## 6. Configurar .env

```bash
cp .env.example .env
# Edita los valores:
#   MSSQL_PASSWORD             Contrasena del SA de SQL Server
#   MSSQL_APP_PASSWORD         Contrasena del login openclient_user
#   ADMIN_EMAIL                Email del administrador inicial
#   ADMIN_PASSWORD             Password del administrador (se guarda como hash)
```

- `./scripts/dev.sh` crea `.env` automaticamente (con claves aleatorias) si no
  existe, y completa las variables que falten en un `.env` previo.
- `.env` esta ignorado por Git (`.gitignore`) y excluido del contexto Docker
  (`.dockerignore`). Nunca hagas commit ni copies ese archivo a la imagen.
- Cambiar `ADMIN_PASSWORD` en `.env` NO actualiza el hash de un administrador
  ya creado (ver politica en seccion 4).

---

## 7. Ejecutar ./scripts/run.sh

```bash
./scripts/run.sh          # BD en Docker + init + app en host (:5000)
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

## 8. Comprobar que el administrador fue creado

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

---

## 9. Higiene de secretos

- La contrasena del admin vive unicamente en `.env` (fuera de Git) y en las
  variables de entorno de la aplicacion; nunca en codigo fuente, Dockerfile,
  `.sql` versionados, logs ni argv de procesos.
- `init.sql` contiene solo el placeholder `__MSSQL_APP_PASSWORD__`.
- Los logs imprimen confirmaciones ("Administrador creado correctamente."),
  jamas valores.
- `.gitignore` excluye `.env`/`.env.*`; `.dockerignore` los excluye del
  contexto de build.
- No se hace `COPY .env` en ningun Dockerfile.

---

## 10. Errores comunes y solucion

| Sintoma | Causa | Solucion |
|---|---|---|
| `ADMIN_EMAIL` o `ADMIN_PASSWORD` no definidos | Falta la variable en `.env` o no se inyecta en la app | Anadir a `.env`; verificar `environment:` del servicio `openclient` |
| Login rechazado tras cambiar `ADMIN_PASSWORD` | La app no sobreescribe hashes existentes | Borrar usuario manualmente y reiniciar la app, o recrear BD |
| `Seed omitido` cuando esperabas recarga completa | Guard de idempotencia: `Clients` ya tiene filas | Resetear volumen (`down -v`) -- borra todos los datos |
| Puerto 5000 ocupado al arrancar la app | Otra instancia sigue viva | `fuser -k 5000/tcp` o `ss -tlnp \| grep 5000` |

---

## 11. Resumen del contrato

1. `ADMIN_EMAIL`/`ADMIN_PASSWORD` llegan exclusivamente de variables de entorno.
2. `db-init` solo crea LOGIN/USUARIO/ROL (infraestructura SQL Server).
3. La aplicacion gestiona: migraciones, admin y seed (logica de negocio en C#).
4. BCrypt se ejecuta en C# (`BCrypt.Net.BCrypt.HashPassword`).
5. Ningun log imprime contrasenas ni hashes.
6. Todo es idempotente: correr `run.sh` N veces no duplica admin ni seed.
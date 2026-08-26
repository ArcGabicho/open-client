# Inicializacion de la Base de Despues del rediseno de agosto 2026: todo el
# schema, el administrador y el seed de clientes se gestionan desde C#
# mediante EF Core, DbInitializer y BCrypt.

---

## 1. Arquitectura

```
.env                          (fuera de Git; ver .env.example)
  OPENCLIENT_ADMIN_EMAIL
  OPENCLIENT_ADMIN_PASSWORD
  MSSQL_APP_PASSWORD
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
   | ENTRYPOINT      | <-- | COPY init.sh init.sql                |
   | /init.sh        |     | Solo crea LOGIN, USUARIO y ROL       |
   +-----------------+     +--------------------------------------+
        |
        |-- 1. espera a SQL Server (sqlcmd SELECT 1)
        |-- 2. init.sql    -> LOGIN openclient_user, USUARIO, ROL
        v
   exit 0  ->  recien entonces arranca openclient

   openclient (ASP.NET Core)
        |
        |-- Program.cs ejecuta DbInitializer
        |       |
        |       |-- 1. Database.MigrateAsync()  (EF Core migrations)
        |       |-- 2. SeedAdminAsync()         (BCrypt + INSERT Users)
        |       |-- 3. SeedClientsAsync()       (C# dataset -> batch INSERT)
        v
   Aplicacion lista
```

## 2. Eliminacion de PasswordHasher

El proyecto `docker/database/PasswordHasher/` ha sido eliminado.

La generacion de hashes BCrypt ahora se realiza directamente en C#:

```csharp
var hash = BCrypt.Net.BCrypt.HashPassword(adminPassword, workFactor: 12);
```

No existe ningun binario separado para hashear contrasenas.

## 3. Eliminacion de admin.sql y seed.sql

Los archivos `docker/database/admin.sql` y `docker/database/seed.sql` han
sido eliminados. Toda la logica de negocio ahora vive en C#:

- **admin.sql** -> `DbInitializer.SeedAdminAsync()` en `core/Data/DbInitializer.cs`
- **seed.sql** -> `DbSeeder.SeedClientsAsync()` en `core/Data/DbSeeder.cs`
- **seed data** -> `core/Data/SeedData/ClientSeedData.cs` (~4018 registros en C#)

## 4. DbInitializer

Ubicacion: `core/Data/DbInitializer.cs`

Responsabilidades:
1. Aplicar migraciones EF Core (`Database.MigrateAsync()`)
2. Crear/verificar administrador initial (`BCrypt + Users.Add`)
3. Ejecutar seed de clientes (`DbSeeder`)

El inicializador es idempotente:
- Si la BD ya tiene migraciones aplicadas, no las duplica.
- Si ya existe un administrador con ese email, no lo sobreescribe.
- Si la tabla Clients ya tiene datos, no inserta de nuevo.

## 5. DbSeeder

Ubicacion: `core/Data/DbSeeder.cs`

Obtiene los clientes desde `ClientSeedData.Clients` (compilado en C#)
y los inserta en batches de 500 dentro de una transaccion EF Core.

Comportamiento:
- Si `Clients` tiene registros -> seed omitido.
- Si `Clients` esta vacio -> insertar todos.
- Si falla a mitad -> rollback completo, no datos parciales.

## 6. Politica de contrasena del administrador

- `ADMIN_EMAIL` y `ADMIN_PASSWORD` se utilizan **unicamente** para
  provisionar el administrador inicial si no existe.
- Si ya existe un administrador con ese email, la aplicacion **NO**
  sobreescribe el hash existente.
- Cambiar `ADMIN_PASSWORD` en `.env` **NO** modifica la contrasena
  de un admin ya creado.
- Para resetear: eliminar la fila manualmente o recrear la BD.

## 7. Variables de entorno

| Variable | Descripcion |
|----------|-------------|
| `ADMIN_EMAIL` | Email del administrador initial |
| `ADMIN_PASSWORD` | Password del administrador (se guarda como hash BCrypt) |
| `ConnectionStrings__DefaultConnection` | Cadena de conexion a SQL Server |
| `MSSQL_PASSWORD` | Password del SA de SQL Server |
| `MSSQL_APP_PASSWORD` | Password del login openclient_user |

## 8. Verificacion

```bash
# Primera ejecucion
./scripts/run.sh
# -> SQL Server -> init (login/user) -> app -> MigrateAsync -> Admin -> Seed

# Segunda ejecucion
./scripts/run.sh
# -> Sin duplicados, sin errores

# Verificar admin
curl -X POST http://localhost:5000/auth/log-in \
  -d "Email=admin@openclient.local&Password=TU_PASSWORD"

# Verificar clientes
curl http://localhost:5000/api/clients
```

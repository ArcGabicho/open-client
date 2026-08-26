# Guia de Desarrollo

Entorno local de Open Client: requisitos, arranque, estructura y verificacion
de cambios.

---

## 1. Requisitos

* .NET SDK 10 (`dotnet --list-sdks`)
* Docker + Docker Compose v2
* Bash

## 2. Arranque

```bash
cp .env.example .env        # primera vez; luego edita los valores reales
./scripts/run.sh            # SQL Server (Docker) + init + app en :5000
```

`run.sh` hace, en orden:

1. verifica que `.env` existe y lo exporta al entorno,
2. levanta `sqlserver` (healthcheck `SELECT 1`),
3. comprueba que el puerto 5000 esta libre,
4. construye la imagen `db-init` y ejecuta la inicializacion
   (login/usuario de SQL Server),
5. restaura paquetes y lanza la app con
   `ASPNETCORE_ENVIRONMENT=Development`, `ASPNETCORE_URLS=http://localhost:5000`,
   la cadena de conexion y las variables `ADMIN_EMAIL`/`ADMIN_PASSWORD`
   **por variable de entorno**.
6. La app ejecuta `DbInitializer` al iniciar:
   - `Database.MigrateAsync()` -> crea/esquema via EF Core
   - `SeedAdminAsync()` -> crea admin con BCrypt si no existe
   - `SeedClientsAsync()` -> inserta ~4018 clientes desde `ClientSeedData.cs` si tabla vacia

Otros modos:

```bash
./scripts/run.sh --full     # stack completo en Docker (app en :8080)
./scripts/run.sh --stop     # detener contenedores
./scripts/run.sh --logs     # logs de SQL Server
```

La app queda disponible en <http://localhost:5000>. Login con las credenciales
del `.env` (`OPENCLIENT_ADMIN_EMAIL` / `OPENCLIENT_ADMIN_PASSWORD`).

## 3. Estructura del proyecto

```
core/
├── Data/
│   ├── OpenClientDbContext.cs       # DbContext con DbSet<Client> y DbSet<User>
│   ├── DbInitializer.cs             # Migraciones + admin + seed
│   ├── DbSeeder.cs                  # Seed de clientes via EF Core
│   ├── ClientConfiguration.cs       # Fluent API para Client
│   ├── UserConfiguration.cs         # Fluent API para User
│   ├── SeedData/
│   │   └── ClientSeedData.cs        # ~4018 registros de clientes (C#)
│   └── Migrations/                  # Migraciones EF Core
├── Models/
│   ├── Domain/
│   │   ├── Client.cs
│   │   └── User.cs
│   └── DTO/
│       └── LoginModel.cs
├── Services/
│   └── AuthService.cs               # BCrypt.Verify + claims
├── Controllers/
│   └── AuthController.cs            # POST /auth/log-in, GET /auth/log-out
├── Components/                      # Blazor components
├── Program.cs                       # Entrypoint + DbInitializer
└── openclient.csproj
```

## 4. EF Core Migrations

Para crear una nueva migracion:

```bash
export PATH="$PATH:$HOME/.dotnet/tools"
dotnet ef migrations add NombreMigracion --project core/openclient.csproj --output-dir Data/Migrations
```

Para aplicar manualmente (normalmente la app lo hace automaticamente):

```bash
dotnet ef database update --project core/openclient.csproj
```

La aplicacion ejecuta `Database.MigrateAsync()` al iniciar via `DbInitializer`.

**Nunca usar `EnsureCreated()`**. Siempre usar migraciones.

## 5. Reglas del proyecto

* **Secretos**: solo en `.env` (fuera de Git). Las cadenas de conexion llegan
  por variables de entorno -- no hardcodearlas en `appsettings*.json`.
* **Render modes**: SSR estatico por defecto; interactividad opt-in por pagina
  (`@rendermode InteractiveServer`). No volver a un modo global interactivo.
* **Autenticacion**: un unico mecanismo (formularios HTTP + cookies). No usar
  JS interop ni `fetch` para login/logout.
* **Logging**: eventos de autenticacion via `ILogger`; nunca registrar
  contrasenas, hashes, cookies ni secretos.
* **Errores**: capturar y registrar la excepcion real (`ILogger.LogError`);
  mostrar al usuario mensajes genericos.
* **Base de datos**: todo el schema y seed se gestiona via EF Core.
  No usar T-SQL de negocio en Docker.

## 6. Verificacion antes de commit

```bash
dotnet build core/openclient.csproj
./scripts/run.sh          # segunda ejecucion debe ser idempotente
```

Checklist funcional minimo:

- [ ] `/log-in` carga sin errores de consola (sin circuito Blazor)
- [ ] login incorrecto muestra mensaje generico
- [ ] login correcto llega a `/dashboard`
- [ ] `/dashboard` anonimo redirige a `/log-in?ReturnUrl=%2Fdashboard`
- [ ] logout lleva a `/log-in` sin errores de WebSocket
- [ ] tras logout, `/dashboard` vuelve a exigir login
- [ ] segunda ejecucion de `run.sh`: 1 admin, ~4018 clientes, sin duplicados

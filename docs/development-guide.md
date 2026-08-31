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
├── Api/                            # ApiV1 (constantes) + ApiErrorMiddleware
├── Components/
│   ├── Layout/                     # MainLayout, LoginLayout, DashboardLayout
│   ├── WebComponents/              # Componentes compartidos del panel
│   └── Pages/                      # Público + Login + Dashboard/Clients/Integrations/Analytics/Users
├── Controllers/                    # ClientsController, ApiController (/api/v1), AnalyticsController,
│                                   #   UsersController, AuthController
├── Data/
│   ├── Context/                    # OpenClientDbContext + fábrica de diseño + DbHealthCheck
│   ├── Configurations/             # ClientConfiguration, UserConfiguration
│   ├── Repositories/               # IClientRepository, IUserRepository (+ impl.)
│   ├── Seeds/                      # DbInitializer, DbSeeder
│   ├── SeedData/                   # ClientSeedData.cs (~4018 registros, C#)
│   └── Migrations/                 # Migraciones EF Core
├── Extensions/                     # ServiceExtensions (composición de la DI)
├── Interfaces/                     # IClientService, IAuthService, IApiClientService,
│                                   #   IAnalyticsService, IUserService, IUserAuditLogger, IContactMailer…
├── Models/
│   ├── Domain/                     # Client.cs, User.cs
│   ├── DTO/                        # DTOs de todos los módulos (incl. tipos Api* y de Analíticas/Usuarios)
│   └── Validators/                 # FluentValidation (clientes, usuarios, contraseña, contacto)
├── Services/                       # ClientService, AuthService, ApiClientService, AnalyticsService,
│                                   #   UserService, UserAuditLogger, SmtpContactMailer
├── Program.cs                      # Serilog + ServiceExtensions + pipeline + DbInitializer
└── openclient.csproj

tests/
└── OpenClient.Api.Tests/           # xUnit + WebApplicationFactory<Program> + SQLite en memoria
```

> Convención de carpetas: `Controllers/`, `Services/`, `Interfaces/` se mantienen
> **planas** (un archivo por directorio, sin subcarpetas por feature). El
> `namespace` no tiene por qué reflejar la ruta física.

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
dotnet build OpenClient.slnx          # 0 warnings, 0 errores
dotnet test                           # suite xUnit (API v1, analíticas, usuarios, contacto)
./scripts/run.sh                      # segunda ejecucion debe ser idempotente
```

> Migraciones: si tocas `Client`/`User` o sus `*Configuration`, genera la
> migración con `dotnet ef migrations add … --project core/openclient.csproj
> --output-dir Data/Migrations`. La herramienta `dotnet-ef` está fijada como
> tool local (`.config/dotnet-tools.json`); restaura con `dotnet tool restore`.

Checklist funcional minimo:

- [ ] `/log-in` carga sin errores de consola (sin circuito Blazor)
- [ ] login incorrecto muestra mensaje generico
- [ ] login correcto llega a `/dashboard`; el pie de la barra lateral muestra el nombre y rol reales
- [ ] `/dashboard` anonimo redirige a `/log-in?ReturnUrl=%2Fdashboard`
- [ ] `/dashboard/users` con un usuario no administrador → 403 / "sin permisos"
- [ ] `GET /api/v1/clients` sin sesión → 401 en JSON (no HTML); `/openapi/v1.json` responde
- [ ] logout lleva a `/log-in` sin errores de WebSocket; tras logout, el panel vuelve a exigir login
- [ ] segunda ejecucion de `run.sh`: 1 admin, ~4018 clientes, sin duplicados
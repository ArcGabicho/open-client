# Perfil del Proyecto

open-client es una aplicacion web comercial de clientes, 100 % Open Source y
Self-Hosted, creada con .NET 10, Blazor y SQL Server en Docker. La superficie
visible se compone de:

1. **Sitio publico** — `MainLayout` con las paginas Inicio, Nosotros,
   Documentacion y Contacto. El formulario de Contacto envia un correo a
   `contact@gabicho.dev` (SMTP configurable; ver §Variables de Entorno).
2. **Inicio de sesion** — `/log-in`, autenticacion por cookie + BCrypt.
3. **Panel administrativo** — `/dashboard`, con cuatro modulos sobre la misma
   base de datos:
   - **Clientes** (`/dashboard/clients`) — listado paginado con busqueda,
     filtros, orden, ficha desplegable y alta/edicion/borrado logico.
   - **Integraciones** (`/dashboard/integrations`) — describe la API REST
     versionada de solo lectura (`/api/v1`) y enlaza su documento OpenAPI.
   - **Analiticas** (`/dashboard/analytics`) — metricas agregadas calculadas en
     SQL: altas por periodo, distribucion por industria y geografia, cargos,
     calidad de datos y evolucion temporal.
   - **Usuarios** (`/dashboard/users`, solo administradores) — administracion de
     las cuentas del panel: alta, edicion, roles, activacion, cambio de
     contrasena y auditoria.

Internamente se exponen varias APIs REST JSON, todas autenticadas con la misma
cookie de sesion: `/api/clients` (CRUD administrativo), `/api/v1/clients` (API de
integracion de solo lectura, con OpenAPI en `/openapi/v1.json`),
`/api/analytics/*` y `/api/users/*`. Referencia completa en `rest-api.md`.

## Stack del Proyecto

- **Blazor Web App en .NET 10** — SSR con interactividad de servidor por pagina.
- **SQL Server 2022 en Docker** — Base de datos relacional aislada en contenedor con volumenes persistentes.
- **Entity Framework Core 10** — Acceso a datos vía repositorio + `IDbContextFactory`; migraciones automaticas al arrancar.
- **FluentValidation + Serilog** — Validacion de servidor y logging estructurado (consola + fichero).
- **Bash Scripts de Automatizacion** — Scripts en `scripts/` (`run.sh`, `setup.sh`, `clear.sh`).
- **Docker Multi-stage** — Imagen ligera de produccion sin SDK.
- **GitHub Actions & Azure** — Pipeline de CI/CD para Azure Container Apps / ACR.

## Estructura del Repositorio

```plaintext
open-client/
├── .github/
│   └── workflows/
│       └── deploy.yml          # Pipeline de CI/CD para Azure Container Apps
├── core/
│   ├── Api/                    # ApiV1 (constantes) + ApiErrorMiddleware (/api/v1)
│   ├── Components/
│   │   ├── Layout/             # MainLayout (publico), LoginLayout, DashboardLayout
│   │   ├── WebComponents/      # Componentes compartidos del panel (DistributionList…)
│   │   └── Pages/              # Sitio publico + Login + Dashboard/Clients/Integrations/Analytics/Users
│   ├── Controllers/            # API REST JSON: ClientsController (/api/clients), ApiController
│   │                          #   (/api/v1/clients), AnalyticsController, UsersController, AuthController
│   ├── Data/
│   │   ├── Context/            # OpenClientDbContext + fabrica de diseno + DbHealthCheck
│   │   ├── Configurations/     # Fluent API de EF Core (Client, User)
│   │   ├── Migrations/         # Migraciones EF Core
│   │   ├── Repositories/       # IClientRepository, IUserRepository (+ implementaciones)
│   │   └── Seeds/              # DbInitializer, DbSeeder, ClientSeedData
│   ├── Extensions/             # ServiceExtensions (composicion de la DI)
│   ├── Interfaces/             # IClientService, IAuthService, IApiClientService,
│   │                          #   IAnalyticsService, IUserService, IUserAuditLogger, IContactMailer…
│   ├── Models/                 # Domain/ · DTO/ (incl. Api types) · Validators/ (FluentValidation)
│   ├── Services/               # ClientService, AuthService, ApiClientService, AnalyticsService,
│   │                          #   UserService, UserAuditLogger, SmtpContactMailer
│   ├── wwwroot/                # Recursos estaticos
│   ├── openclient.csproj       # Proyecto .NET 10 (Microsoft.AspNetCore.OpenApi)
│   └── Program.cs              # Serilog + ServiceExtensions + pipeline
├── tests/
│   └── OpenClient.Api.Tests/   # xUnit + WebApplicationFactory + SQLite en memoria
├── docker/
│   ├── docker-compose.yml      # Infraestructura completa (SQL Server + inicializacion + app)
│   ├── Dockerfile              # Multi-stage Dockerfile de la app (solo produccion)
│   └── database/
│       ├── Dockerfile          # Contenedor one-shot de inicializacion (login/usuario)
│       ├── init.sh             # Espera a SQL Server y ejecuta init.sql
│       └── init.sql            # DDL: login, usuario, rol (infraestructura SQL Server)
├── docs/                       # Guias de arquitectura y despliegue
├── scripts/
│   ├── clear.sh                # Limpia artefactos y contenedores
│   ├── deploy.sh               # Automatiza el deploy en Azure/VM
│   ├── dev.sh                  # Configura el entorno de desarrollo
│   ├── run.sh                  # Inicia la BD y la app en el host
│   └── setup.sh                # Prepara dependencias y variables
├── .dockerignore
├── .env.example                # Plantilla de variables de entorno
├── .gitignore
├── OpenClient.slnx             # Solucion .NET
├── LICENSE.md
└── README.md
```

## API REST & Endpoints

El binario tambien responde como API JSON, todas las rutas autenticadas con la
misma cookie de sesion:

| Grupo | Base | Rol |
|-------|------|-----|
| CRUD administrativo de clientes | `/api/clients` | mismo `IClientService` que el panel |
| API de integracion (solo lectura, versionada) | `/api/v1/clients` (+ `/{id}`, `/search`) | DTO propio, errores `{ "error": { "code", "message" } }` |
| Documento OpenAPI de la API v1 | `/openapi/v1.json` | parametros, DTOs, respuestas y codigos |
| Analiticas | `/api/analytics` (+ `/industries`, `/provinces`, `/districts`, `/job-titles`, `/growth`, `/completeness`) | agregaciones en SQL |
| Usuarios | `/api/users` (+ `/{id}`, `/{id}/role`, `/{id}/password`, `/{id}/activate` \| `/deactivate`) | politica `Users.Admin` (rol Admin) |
| Salud | `GET /health`, `GET /health/ready` | readiness/liveness |

Bajo `/api/*` una peticion no autenticada recibe **401** en JSON (no se redirige
al HTML de login). La referencia completa esta en **`rest-api.md`**.

## Conexion a SQL Server

Puedes conectarte al contenedor de la base de datos usando Azure Data Studio, DBeaver o la extension SQL Server (mssql) de VS Code:

| Parametro | Valor |
|-----------|-------|
| Servidor | localhost,1433 |
| Base de datos | OpenClientDb |
| Autenticacion | SQL Server Authentication |
| Usuario (app) | openclient_user |
| Contrasena (app) | Valor de `MSSQL_APP_PASSWORD` en `.env` |
| Usuario (admin) | sa |
| Contrasena (admin) | Valor de `MSSQL_PASSWORD` en `.env` |
| Trust Server Certificate | True |

El usuario `openclient_user`, su login, la base `OpenClientDb` y el rol `openclient_runtime` se crean automaticamente al arrancar el servicio `db-init` del Docker Compose. La aplicacion (.NET/EF Core) ejecuta `DbInitializer` al iniciar, que aplica las migraciones, crea el administrador inicial con BCrypt (email de `ADMIN_EMAIL`, hash generado en C#) y ejecuta el seed de clientes desde `core/Data/SeedData/ClientSeedData.cs`. Detalles: [database.md](database.md).

## Variables de Entorno

| Variable | Descripcion | Valor por defecto / Ejemplo |
|----------|-------------|------------------------------|
| ASPNETCORE_ENVIRONMENT | Entorno de ejecucion (Development / Production) | Development |
| ConnectionStrings__DefaultConnection | Cadena de conexion hacia el servidor SQL Server | Server=sqlserver,1433;Database=OpenClientDb;User Id=openclient_user;Password=...;TrustServerCertificate=True; |
| MSSQL_PASSWORD | Contrasena del usuario administrador sa | Definida en `.env` |
| MSSQL_APP_PASSWORD | Contrasena del login de aplicacion openclient_user | Definida en `.env` |
| OPENCLIENT_ADMIN_EMAIL | Email del administrador inicial (`dbo.Users`) | admin@openclient.local |
| OPENCLIENT_ADMIN_PASSWORD | Contrasena del administrador; se almacena como hash BCrypt | Definida en `.env` |
| Contact__Smtp__Host | Host SMTP para el formulario de Contacto. Vacio = no se envia, el mensaje queda en el log | (vacio) |
| Contact__Smtp__Port | Puerto SMTP | 587 |
| Contact__Smtp__User / Contact__Smtp__Password | Credenciales del relay SMTP (si las exige) | (vacio) |
| Contact__Smtp__From | Direccion `From` de los correos de contacto | (vacio) |
| Contact__Smtp__UseSsl | STARTTLS/SSL | true |
| Analytics__CacheSeconds | TTL de la cache en memoria del dashboard de Analiticas. `0` = desactivada | 0 |
| Api__Cors__AllowedOrigins__0 | Origenes permitidos para la API v1 (lista; vacia = sin CORS). La politica queda registrada pero inactiva | (vacia) |

El destinatario del formulario de Contacto (`contact@gabicho.dev`) esta fijado en
codigo (`SmtpContactMailer.Recipient`); solo el transporte SMTP es configurable.
Consulta la plantilla completa en `.env.example`.

## ☁️ Despliegue en Producción & CI/CD

### Despliegue Local en Producción con Docker Compose

Para empaquetar y levantar la versión ligera de producción compilada:

```bash
cd docker
docker compose --profile prod up app-prod --build -d
```

### CI/CD en Azure mediante GitHub Actions

El repositorio está configurado para realizar compilaciones e integraciones continuas. Al hacer un push a la rama `main` o `develop`, GitHub Actions compila la imagen Docker y la despliega automáticamente en Azure Container Registry (ACR) y Azure Container Apps:

Agrega las siguientes credenciales en los Secrets de GitHub (Settings > Secrets and variables > Actions):

- **AZURE_CREDENTIALS**: JSON del Service Principal de Azure.
- **REGISTRY_LOGIN_SERVER**: tusitio.azurecr.io
- **REGISTRY_USERNAME**: Usuario de ACR.
- **REGISTRY_PASSWORD**: Clave de ACR.

El flujo `.github/workflows/deploy.yml` ejecutará la compilación multi-stage y el push automáticamente a tu infraestructura en la nube.
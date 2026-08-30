# Perfil del Proyecto

open-client es una aplicacion web comercial de clientes, 100 % Open Source y
Self-Hosted, creada con .NET 10, Blazor y SQL Server en Docker. La superficie
visible se limita a tres cosas:

1. **Sitio publico** — `MainLayout` con las paginas Inicio, Nosotros,
   Documentacion y Contacto.
2. **Inicio de sesion** — `/log-in`, autenticacion por cookie + BCrypt.
3. **Panel de clientes** — `/dashboard`: listado paginado con busqueda,
   filtros, ficha desplegable y alta/edicion/borrado logico.

Internamente existe una API REST (`/api/clients`) que comparte la misma capa de
servicio con el panel; ver `rest-api.md`. No se expone ninguna otra pantalla de
aplicacion.

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
│   ├── Components/
│   │   ├── Layout/             # MainLayout (publico), LoginLayout, DashboardLayout
│   │   └── Pages/              # Index/About/Docs/Contact, Login, Dashboard (panel de clientes)
│   ├── Controllers/            # API REST JSON (/api/clients, /auth)
│   ├── Data/
│   │   ├── Context/            # OpenClientDbContext + fabrica de diseno
│   │   ├── Configurations/     # Fluent API de EF Core
│   │   ├── Migrations/         # Migraciones EF Core
│   │   ├── Repositories/       # IClientRepository + ClientRepository
│   │   └── Seeds/              # DbInitializer, DbSeeder, ClientSeedData
│   ├── Extensions/             # ServiceExtensions (composicion de la DI)
│   ├── Interfaces/             # IClientService, IAuthService, IDbInitializer
│   ├── Models/                 # Domain/ · DTO/ · Validators/ (FluentValidation)
│   ├── Services/               # ClientService, AuthService
│   ├── wwwroot/                # Recursos estaticos
│   ├── openclient.csproj       # Proyecto .NET 10
│   └── Program.cs              # Serilog + ServiceExtensions + pipeline
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

El binario tambien responde como API JSON bajo `/api/clients` (autenticada con
la misma cookie de sesion) y expone `GET /health` y `GET /health/ready` para
readiness/liveness. La referencia completa de endpoints, cuerpos y codigos de
estado esta en **`rest-api.md`**.

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
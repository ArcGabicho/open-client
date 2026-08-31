# CLAUDE.md

Contexto para agentes de código (y personas nuevas) que trabajan en **open-client**:
servicio web + panel administrativo + API de consulta comercial de clientes, en
Blazor Web App (Interactive Server), .NET 10, EF Core y SQL Server, desplegable en
VM con Docker Compose o en Azure (Container Apps + Azure SQL).

Visión general: [`docs/app-overview.md`](docs/app-overview.md) ·
Arquitectura: [`docs/architecture-overview.md`](docs/architecture-overview.md).

## Estructura del repositorio

| Ruta | Contenido |
|---|---|
| `core/` | La aplicación (Blazor + API + dominio + datos). Un solo proyecto: `core/openclient.csproj`. |
| `tests/OpenClient.Api.Tests/` | Pruebas de integración de la API (xUnit). |
| `infra/` | Plantilla Bicep (`main.bicep`) y orquestador (`deploy.sh`) del despliegue en Azure. |
| `docker/` | `Dockerfile` (multi-stage) de la app, `docker-compose.yml` y `database/` (init de SQL Server). |
| `scripts/` | Scripts Bash de entorno y despliegue (ver abajo). |
| `docs/` | Documentación temática. |

Las **carpetas son planas**: dentro de `core/` no se anida por capa, y los
namespaces son independientes de la ruta del archivo. No reorganices en carpetas
por "capa" ni asumas que `namespace == ruta`.

## Comandos habituales

Todos desde la raíz del repo. La app necesita un archivo `.env` (copia de
`.env.example`); `setup.sh`/`dev.sh` lo generan si falta.

```bash
# Preparar la máquina (una vez): instala dependencias, clona, genera .env. No arranca nada.
./scripts/setup.sh

# Desarrollo diario: SQL Server en Docker + db-init + app en el host (http://localhost:5000)
./scripts/run.sh
./scripts/run.sh --full     # stack completo en Docker (app en contenedor, :8080)
./scripts/run.sh --stop     # detener contenedores
./scripts/run.sh --logs     # logs de SQL Server

# Build y tests
dotnet build OpenClient.slnx -c Release
dotnet test  OpenClient.slnx -c Release

# Despliegue
./scripts/deploy.sh         # VM / servidor Linux con Docker Compose
./infra/deploy.sh           # Azure (Container Apps + Azure SQL); ver docs/infra-guide.md
```

Detalle de cada script: [`docs/bash-scripts-guide.md`](docs/bash-scripts-guide.md).

## Convenciones

- **.NET 10**, C# con `Nullable` habilitado. Solución en formato `OpenClient.slnx`.
- **Mensajes de commit** en español, en minúscula, con prefijo: `add:`, `fix:`,
  `feat:`, `refactor:`, `docs:`, `migrate:`, `delete:`. Sin scope entre paréntesis.
- **Migraciones EF Core** en `core/Data/Migrations/`. Al arrancar, `DbInitializer`
  (`core/Data/Seeds/DbInitializer.cs`) aplica las migraciones pendientes y siembra
  el administrador (BCrypt) y el dataset de clientes si las tablas están vacías. Es
  idempotente. Ver [`docs/database-initialization.md`](docs/database-initialization.md).
- Config por variables de entorno: `ConnectionStrings__DefaultConnection`,
  `ADMIN_EMAIL`, `ADMIN_PASSWORD` (app); `MSSQL_PASSWORD`, `MSSQL_APP_PASSWORD` (BD).
- Endpoints de salud: `/health` (incluye la BD) y `/health/ready`.

## Cosas a tener en cuenta

- El contenedor de **SQL Server exige ≥ 2000 MB de RAM física** para arrancar (el
  swap no cuenta). Los scripts avisan; se salta con `OPENCLIENT_MIN_RAM_MB=0`.
- Los **tests usan SQLite en memoria** y `WebApplicationFactory`; no requieren SQL
  Server ni Docker.
- **`.env` nunca se commitea** (está en `.gitignore`). Ningún secreto entra a git.
- `infra/main.json` es la salida compilada de Bicep y está en `.gitignore`; no lo
  edites ni lo añadas.
- `infra/deploy.sh` se auto-clona si se ejecuta por tubería (`curl | bash`); dentro
  de un clon usa el checkout tal cual.

## CI

`.github/workflows/ci.yml` corre en cada push a `master` y en cada PR: `dotnet
build` + `dotnet test`, `az bicep build` de `infra/main.bicep` y `docker build` de
la imagen (sin publicar). No usa secretos ni toca Azure. Debe estar en verde antes
de mergear.

## Documentación

| Tema | Archivo |
|---|---|
| Visión general de la app | [`docs/app-overview.md`](docs/app-overview.md) |
| Arquitectura | [`docs/architecture-overview.md`](docs/architecture-overview.md) |
| Base de datos (esquema, EF Core) | [`docs/database-guide.md`](docs/database-guide.md) |
| Inicialización de la BD | [`docs/database-initialization.md`](docs/database-initialization.md) |
| Autenticación | [`docs/authentication.md`](docs/authentication.md) |
| API REST | [`docs/rest-api.md`](docs/rest-api.md) |
| Panel de clientes | [`docs/clients-panel.md`](docs/clients-panel.md) |
| Scripts Bash | [`docs/bash-scripts-guide.md`](docs/bash-scripts-guide.md) |
| Docker | [`docs/docker-guide.md`](docs/docker-guide.md) |
| Desarrollo | [`docs/development-guide.md`](docs/development-guide.md) |
| Infraestructura / Azure | [`docs/infra-guide.md`](docs/infra-guide.md) |
| Cómo contribuir | [`CONTRIBUTING.md`](CONTRIBUTING.md) |
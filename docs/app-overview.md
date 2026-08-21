# Perfil del Proyecto

El proyecto open-client se define como un CRM & Engine de datos comercial 100% Open Source y Self-Hosted, creado con .NET 10, Blazor y SQL Server en Docker que incluye una base de datos de clientes lista para usar y una REST API para conectar Agentes de IA, WhatsApp o Facturacion desde tu propio VPS o Infraestructura Cloud.

## Stack del Proyecto

- **Blazor Web App en .NET 10** — Interfaz interactiva SPA basada en C# sin necesidad de JavaScript complejo.
- **API REST Publica + Panel Web** — Mismo binario respondiendo como API JSON (`/api/clientes`) y como Panel de Administracion.
- **SQL Server 2022 en Docker** — Base de datos relacional aislada en contenedor con volumenes persistentes.
- **Entity Framework Core 10** — ORM robusto con soporte de migraciones automaticas y consultas LINQ optimizadas.
- **Bash Scripts de Automatizacion** — Scripts en la carpeta `scripts/` (`run.sh`, `setup.sh`, `clear.sh`) para acelerar la gestion en Linux.
- **Hot Reload en Desarrollo** — Flujo de desarrollo interactivo con `dotnet watch` ejecutado directamente en el host.
- **Busqueda y Paginacion Eficiente** — Filtrado optimizado por RUC, Razon Social y Nombre Comercial.
- **Docker Multi-stage** — Imagen ligera de produccion sin SDK para minimo tamano de despliegue.
- **GitHub Actions & Azure Integration** — Pipelines listos de CI/CD para compilacion, testing y despliegue automatico en Azure Container Apps / ACR.

## Estructura del Repositorio

```plaintext
open-client/
├── .github/
│   └── workflows/
│       └── deploy.yml          # Pipeline de CI/CD para Azure Container Apps
├── core/
│   ├── Components/             # Componentes e Interfaz UI de Blazor (.razor)
│   ├── Controllers/            # API REST Publica (JSON Controller)
│   ├── Data/                   # AppDbContext y configuraciones de EF Core
│   ├── Models/                 # Entidades del Dominio (Cliente.cs, etc.)
│   ├── Services/               # Capa de Logica de Negocio y Servicios C#
│   ├── wwwroot/                # Recursos estaticos (CSS, JS, imagenes)
│   ├── appsettings.json        # Configuracion del servidor
│   ├── appsettings.Development.json # Configuracion local (localhost)
│   ├── openclient.csproj       # Proyecto .NET 10
│   └── Program.cs              # Entrypoint de ASP.NET Core y Registro de Inyeccion de Dependencias
├── docker/
│   ├── docker-compose.yml      # Infraestructura (SQL Server 2022)
│   └── Dockerfile              # Multi-stage Dockerfile (Solo Produccion)
├── docs/                       # Guias de arquitectura y despliegue
├── scripts/
│   ├── clear.sh                # Limpia artefactos y contenedores
│   ├── deploy.sh               # Automatiza el deploy en Azure/VM
│   ├── dev.sh                  # Configura el entorno de desarrollo
│   ├── run.sh                  # Inicia la BD y la app en el host
│   └── setup.sh                # Prepara dependencias y variables
├── .dockerignore
├── .gitignore
├── OpenClient.sln              # Solucion .NET
├── LICENSE.md
└── README.md
```

## API REST & Endpoints

### Endpoints Disponibles

Ademas del panel web interactivo de Blazor, el sistema expone los siguientes endpoints HTTP en JSON para consumo de clientes externos:

#### Health Check

`HTTP GET /api/health`

#### Listar y Buscar Clientes

`HTTP GET /api/clientes?page=1&limit=20&search=CLINICA`

| Parametro | Tipo | Default | Descripcion |
|-----------|------|---------|-------------|
| page | int | 1 | Numero de pagina actual |
| limit | int | 20 | Resultados por pagina (maximo 100) |
| search | string | — | Busqueda por RUC, Razon Social o Nombre Comercial |

## Conexion a SQL Server

Puedes conectarte al contenedor de la base de datos usando Azure Data Studio, DBeaver o la extension SQL Server (mssql) de VS Code:

| Parametro | Valor |
|-----------|-------|
| Servidor | localhost,1433 |
| Base de datos | OpenClientDb |
| Autenticacion | SQL Server Authentication |
| Usuario | sa |
| Contrasena | TuPasswordSeguro123! |
| Trust Server Certificate | True |


## Variables de Entorno

| Variable | Descripcion | Valor por defecto / Ejemplo |
|----------|-------------|------------------------------|
| ASPNETCORE_ENVIRONMENT | Entorno de ejecucion (Development / Production) | Development |
| ConnectionStrings__DefaultConnection | Cadena de conexion hacia el servidor SQL Server | Server=localhost,1433;Database=OpenClientDb;User Id=sa;Password=TuPasswordSeguro123!;TrustServerCertificate=True; |
| MSSQL_SA_PASSWORD | Contrasena del usuario administrador sa | TuPasswordSeguro123! |

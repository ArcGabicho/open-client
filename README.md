# OPEN CLIENT - BASE DE DATOS DE CLIENTES

![Wallpaper](https://i.imgur.com/XsBqudT.png)

<p align="center">
  <strong>Servicio web y consulta comercial de clientes</strong>
  <br />
  Despliega tu propio sistema de datos comerciales en minutos con .NET 10 y Docker
</p>

<p align="center">
  <a href="#"><img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet" alt=".NET Version"></a>
  <a href="#"><img src="https://img.shields.io/badge/ASP.NET_Core-MVC-512BD4?logo=dotnet" alt="ASP.NET Core 
MVC"></a>
  <a href="#"><img src="https://img.shields.io/badge/EF_Core-10.0-512BD4" alt="EF Core"></a>
  <a href="#"><img src="https://img.shields.io/badge/SQL_Server-2022-CC292B?logo=microsoftsqlserver" alt="SQL 
Server"></a>
  <a href="#"><img src="https://img.shields.io/badge/Docker-Multi-stage-2496ED?logo=docker" alt="Docker"></a>
  <a href="#"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="License"></a>
  <a href="#"><img src="https://img.shields.io/badge/PRs-welcome-brightgreen.svg" alt="PRs welcome"></a>
</p>

## ✨ Características

- ⚡ **ASP.NET Core MVC en .NET 10** — arquitectura moderna, mantenible y de alto rendimiento.
- 🗄 **SQL Server 2022 en Docker** — base de datos relacional aislada en contenedor, sin instalación local.
- 🛠 **Entity Framework Core 10** — ORM robusto con soporte de migraciones y consultas LINQ optimizadas.
- 🔥 **Hot Reload en desarrollo** — flujo dev fluido con `dotnet watch` dentro del contenedor.
- 🔍 **Búsqueda eficiente** — filtrado optimizado por RUC, Razón Social y Nombre Comercial.
- 📄 **Paginación avanzada** — control configurable de página (`page`) y límite (`limit`).
- 🐳 **Docker Multi-stage** — imágenes independientes para desarrollo (SDK) y producción (Runtime ligero).
- ☁ **Azure-ready** — listo para desplegar en Azure Container Apps o Azure App Service.
- 📊 **~5,000 registros** — pre-cargados del mercado comercial peruano.

## 🚀 Inicio rápido (Desarrollo con Docker)

El proyecto incluye un entorno Docker optimizado con **Hot Reload** y **SQL Server 2022** integrado.

### Prerrequisitos

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) instalado y ejecutándose.
- Opcional: [VS Code](https://code.visualstudio.com/) con la extensión **SQL Server (mssql)**.

### Pasos para levantar el proyecto

```bash
# 1. Clonar el repositorio
git clone https://github.com/ArcGabicho/open-client.git
cd open-client

# 2. Copiar archivo de variables de entorno
cp .env.example .env

# 3. Levantar los servicios con Docker Compose
docker compose up --build
```

Navega a `http://localhost:8080` para ver la aplicación web en ejecución. 💡 Nota de Hot Reload: Cualquier cambio 
que realices en el código fuente (.cs, .cshtml) se reflejará automáticamente dentro del contenedor sin reiniciar el 
proceso.

### Conexión a SQL Server desde VS Code

Puedes conectarte a la base de datos en segundo plano usando la extensión **SQL Server (mssql)** en VS Code o Azure 
Data Studio con las siguientes credenciales:

| Parámetro | Valor |
|-----------|-------|
| Servidor  | localhost,1433 |
| Base de datos | OpenClientDb |
| Autenticación | SQL Server Authentication |
| Usuario | sa |
| Contraseña | ArcGabicho05$ |
| Trust Server Certificate | True |

### API / Endpoints

#### Health Check
- **HTTP GET /**

#### Listar y Buscar Clientes
- **HTTP GET /Clientes?page=1&limit=20&search=CLINICA**

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| page      | int  | 1       | Número de página |
| limit     | int  | 20      | Resultados por página (máximo 100) |
| search    | string | — | Búsqueda por RUC, Razón Social o Nombre Comercial |

#### 🔧 Variables de Entorno

| Variable              | Descripción                                   | Valor por defecto / Ejemplo |
|-----------------------|-----------------------------------------------|-------------------------------|
| ASPNETCORE_ENVIRONMENT  | Entorno de ejecución (Development / Production) | Development                 |
| ConnectionStrings__DefaultConnection | Cadena de conexión a SQL Server | 
`Server=open-client-database,1433;Database=OpenClientDb;User 
Id=sa;Password=ArcGabicho05$;TrustServerCertificate=True;MSSQL_SA_PASSWORD=ArcGabicho05$` |
| MSSQL_SA_PASSWORD       | Contraseña del usuario sa de SQL Server     | ArcGabicho05$               |
```

### 📁 Estructura del proyecto

```
open-client/
├── Controllers/            # Controladores MVC (Lógica de endpoints y vistas)
├── Models/                 # Entidades de EF Core (Cliente.cs, etc.)
├── Views/                  # Vistas Razor (HTML/C#)
├── wwwroot/                # Archivos estáticos (CSS, JS, imágenes)
├── Properties/             # Configuración de lanzamiento
├── appsettings.json        # Configuración principal
├── appsettings.Development.json
├── clientes.json           # Data inicial de ~6,000 clientes
├── Dockerfile              # Build Multi-Stage (Dev, Build, Final)
├── docker-compose.yml      # Configuración de contenedores (App + SQL Server)
├── open-client.csproj      # Proyecto .NET 10
└── Program.cs              # Entrypoint y configuración de DI / Middlewares
```

### 🐳 Despliegue en Producción

Para compilar y ejecutar el contenedor usando la imagen final ligera de producción (solo con el runtime de .NET 10):

```bash
# Construir e iniciar en modo producción
docker compose -f docker-compose.prod.yml up --build -d
```

### Publicar en Azure Container Registry (ACR)

```bash
# Build de la imagen final
docker build -t open-client:v1 .

# Tag para tu registro de Azure
docker tag open-client tusitio.azurecr.io/open-client:v1

# Push a ACR
docker push tusitio.azurecr.io/open-client:v1
```

### 📄 Licencia

MIT — consulta `LICENSE.md` para más detalles.
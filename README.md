# OPEN CLIENT - BASE DE DATOS DE CLIENTES

![Wallpaper](https://i.imgur.com/XsBqudT.png)

Open Client es un proyecto que te brinda un servicio web, panel administrativo y consulta comercial de clientes. Podrás desplegar tu propio sistema de datos comerciales en minutos y teniendo el control total de los datos con Blazor Web App, Docker y Azure.

<a href="#"><img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet" alt=".NET Version"></a>
<a href="#"><img src="https://img.shields.io/badge/Blazor-Interactive_Server-512BD4?logo=blazor" alt="Blazor Web App"></a>
<a href="#"><img src="https://img.shields.io/badge/EF_Core-10.0-512BD4" alt="EF Core"></a>
<a href="#"><img src="https://img.shields.io/badge/SQL_Server-2022-CC292B?logo=microsoftsqlserver" alt="SQL Server"></a>
<a href="#"><img src="https://img.shields.io/badge/Docker-Multi--stage-2496ED?logo=docker" alt="Docker"></a>
<a href="#"><img src="https://img.shields.io/badge/Azure-Container_Apps-0089D6?logo=microsoftazure" alt="Azure"></a>
<a href="https://github.com/ArcGabicho/open-client/actions/workflows/ci.yml"><img src="https://github.com/ArcGabicho/open-client/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
---

#### Instalación Local: Usando los Scripts en Bash

```bash
curl -sSL https://raw.githubusercontent.com/ArcGabicho/open-client/master/scripts/setup.sh | bash
```

> [!WARNING]
> `setup.sh` solo prepara el entorno (instala dependencias, clona el repo y genera `.env`); **no despliega ni arranca nada**. Para levantar la aplicación en local usa después `./scripts/run.sh`. Antes de trabajar o contribuir en el proyecto, revisa la [guía de contribución](CONTRIBUTING.md), la [guía de scripts Bash](docs/bash-scripts-guide.md), la [guía de desarrollo](docs/development-guide.md) y la [guía de Docker](docs/docker-guide.md).

#### Deploy en Azure (Container Apps + Azure SQL)

```bash
curl -sSL https://raw.githubusercontent.com/ArcGabicho/open-client/master/infra/deploy.sh | bash
```

> [!WARNING]
> Requiere Azure CLI con sesión iniciada (`az login`) y `git`. El script clona el repo y construye la imagen en tu propio Azure Container Registry. Referencia completa (parámetros, operación, costes y troubleshooting) en la [guía de infraestructura](docs/infra-guide.md).

#### Deploy en máquina virtual (Docker Compose)

```bash
curl -sSL https://raw.githubusercontent.com/ArcGabicho/open-client/master/scripts/deploy.sh | bash
```

> [!WARNING]
> **Requisitos de RAM en máquina virtual.** El despliegue en VM levanta SQL Server 2022 en contenedor, que **exige al menos 2000 MB de RAM física** para arrancar (el swap no cuenta). Con el build de .NET y el sistema operativo, **la VM debe tener como mínimo 4 GB de RAM**. `deploy.sh` comprueba la memoria disponible y aborta con un mensaje si no llega; puedes saltarte la comprobación con `OPENCLIENT_MIN_RAM_MB=0`, pero el contenedor de base de datos fallará igualmente. Si no puedes ampliar la RAM, usa una base de datos externa y levanta solo el servicio `openclient`, o despliega en Azure con la plantilla de `infra/` (ver la [guía de infraestructura](docs/infra-guide.md)).
---

Navega a http://openclient.azure.app para acceder al demo de Open Client con un panel de Blazor.
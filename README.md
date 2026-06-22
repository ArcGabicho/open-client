<p align="center">
  <img src="https://i.imgur.com/XsBqudT.png" alt="Open Client" width="1000"/>
</p>

<h1 align="center">Open Client</h1>

<p align="center">
  <strong>API REST para consulta de clientes peruanos</strong>
  <br />
  Despliega tu propio servicio de datos comerciales en minutos
</p>

<p align="center">
  <a href="#"><img src="https://img.shields.io/badge/Go-1.26-00ADD8?logo=go" alt="Go version"></a>
  <a href="#"><img src="https://img.shields.io/badge/Fiber-v3.3.0-00ACD7?logo=fiber" alt="Fiber version"></a>
  <a href="#"><img src="https://img.shields.io/badge/SQLite-2024-003B57?logo=sqlite" alt="SQLite"></a>
  <a href="#"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="License"></a>
  <a href="#"><img src="https://img.shields.io/badge/PRs-welcome-brightgreen.svg" alt="PRs welcome"></a>
</p>

---

## ✨ Características

- ⚡ **Alto rendimiento** — sobre [Fiber v3](https://gofiber.io/), el framework web más rápido de Go
- 🗄️ **SQLite embebido** — zero CGo, sin dependencias externas, fácil de deployar
- 🔒 **Rate limiting** — 100 req/min por IP
- 🔍 **Búsqueda** por nombre, empresa y RUC
- 📄 **Paginación** con control de page y limit
- 🐳 **Docker multi-stage** — imagen optimizada para producción
- ☁️ **Azure-ready** — health checks, graceful shutdown, PORT configurable
- 📊 **~6,000 registros** de clientes del mercado peruano

## 🚀 Inicio rápido

```bash
git clone https://github.com/ArcGabicho/open-client.git
cd open-client
cp .env.example .env
go run main.go
```

Servidor en **`http://localhost:3000`**.

## 📡 API

### Health Check

```http
GET /
```

```json
{
  "status": "ok",
  "service": "open-client"
}
```

### Listar Clientes

```http
GET /api/v1/clientes?page=1&limit=20&search=CLINICA
```

| Parámetro | Tipo   | Default | Descripción                        |
| --------- | ------ | ------- | ---------------------------------- |
| `page`    | int    | `1`     | Número de página                   |
| `limit`   | int    | `20`    | Resultados por página (max 100)    |
| `search`  | string | —       | Búsqueda por nombre, empresa o RUC |

### Obtener Cliente por ID

```http
GET /api/v1/clientes/:id
```

## 🐳 Despliegue en Azure

### Azure Container Registry + App Service

```bash
# 1. Build para Linux (desde macOS/Windows)
docker buildx build --platform linux/amd64 -t open-client .

# 2. Tag para tu ACR
docker tag open-client tusitio.azurecr.io/open-client:v1

# 3. Push a Azure Container Registry
docker push tusitio.azurecr.io/open-client:v1

# 4. Crear App Service (Azure CLI)
az webapp create \
  --resource-group mi-rg \
  --plan mi-plan \
  --name open-client-api \
  --deployment-container-image-name tusitio.azurecr.io/open-client:v1

# 5. Configurar PORT (Azure lo asigna automáticamente)
az webapp config appsettings set \
  --resource-group mi-rg \
  --name open-client-api \
  --settings PORT=8080
```

La app levanta con **graceful shutdown** ante SIGTERM (Azure envía esta señal al detener/ escalar el container).

## 🔧 Variables de entorno

| Variable         | Descripción                  | Por defecto                  |
| ---------------- | ---------------------------- | ---------------------------- |
| `PORT`           | Puerto del servidor HTTP     | `3000`                       |
| `DATABASE_PATH`  | Ruta a la base de datos      | `./data/database.sqlite`     |

## 📁 Estructura del proyecto

```
open-client/
├── main.go                 # Entry point thin
├── services/
│   └── clientes_api.go     # Servicio: Fiber app, middleware, rutas, handlers
├── config/
│   ├── config.go           # Config desde env vars
│   └── database.go         # Conexión SQLite y queries
├── models/
│   └── cliente.go          # Struct Cliente
├── data/
│   └── database.sqlite     # Base de datos pre-cargada
├── Dockerfile              # Multi-stage, non-root, HEALTHCHECK
├── .env.example
├── go.mod / go.sum
├── README.md
└── LICENSE.md
```

## 🐳 Docker

```bash
# Build
docker build -t open-client .

# Run
docker run -p 3000:3000 -e PORT=3000 open-client
```

La imagen usa **alpine:3.20** con usuario no-root (`appuser`) y HEALTHCHECK integrado.

## 📄 Licencia

MIT — consulta [LICENSE.md](LICENSE.md).

---

<p align="center">
  <sub>Built with ❤️ for the Peruvian business ecosystem</sub>
</p>
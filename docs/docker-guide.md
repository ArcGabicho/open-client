# Guia de Docker

Archivos de configuracion Docker del proyecto, ubicados en el directorio `docker/`.

---

## 1. `docker/Dockerfile` — Multi-stage Build (Produccion)

Imagen multi-stage con 2 targets para produccion. Basada en .NET 10.0.

> **Nota:** El entorno de desarrollo ahora se ejecuta directamente en el host con `dotnet watch`, no dentro de Docker. Esto elimina las colisiones de permisos (MSB3491 / root) y mejora la experiencia del IDE.

### Targets

#### `build` (Restore + Publish)

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
```

- Restaura dependencias desde los archivos `.csproj`.
- Compila y publica la aplicacion con `dotnet publish -c Release -o /app/publish`.

---

#### `final` (Runtime ligero)

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
```

- Imagen ligera con solo el runtime de ASP.NET (sin SDK).
- Copia los archivos publicados desde el target `build`.
- Expone el puerto `8080`.
- Ejecuta `dotnet openclient.dll` como punto de entrada.

---

### Diagrama de dependencias

```
sdk:10.0 (build)
└── aspnet:10.0 (final → imagen ligera de produccion)
```

### Build manual

```bash
# Build de produccion
docker build -t openclient-prod -f docker/Dockerfile .
```

---

## 2. `docker/docker-compose.yml` — Infraestructura

Define un servicio para la base de datos SQL Server.

### Servicios

#### `sqlserver` — Base de datos

| Propiedad        | Valor                                         |
|------------------|-----------------------------------------------|
| Imagen           | `mcr.microsoft.com/mssql/server:2022-latest`  |
| Contenedor       | `openclient-database`                         |
| Puerto           | `1433:1433`                                   |
| Volumen          | `openclient_data:/var/opt/mssql`              |

**Variables de entorno:**

| Variable           | Valor                        | Descripcion                         |
|--------------------|------------------------------|-------------------------------------|
| `ACCEPT_EULA`      | `Y`                          | Acepta la licencia de SQL Server    |
| `MSSQL_SA_PASSWORD`| `${MSSQL_PASSWORD}`          | Contraseña del SA (desde `.env`)    |

**Healthcheck:**

- Ejecuta `SELECT 1` cada 10 segundos con `sqlcmd`.
- Timeout de 5 segundos, hasta 5 reintentos.

---

### Volumenes

| Volumen            | Montado en                 | Proposito                        |
|--------------------|----------------------------|----------------------------------|
| `openclient_data`  | `/var/opt/mssql`           | Persistencia de datos de SQL Server |

---

### Uso con Docker Compose

```bash
COMPOSE="docker compose --env-file .env -f docker/docker-compose.yml"

# Arrancar SQL Server
$COMPOSE up -d

# Detener SQL Server
$COMPOSE down

# Detener y eliminar volumenes (borra datos)
$COMPOSE down -v

# Ver logs en vivo
$COMPOSE logs -f sqlserver
```

---

## Arquitectura de desarrollo vs produccion

### Desarrollo (en el host)

```
Host:
  dotnet watch run (puerto 5000)
      ↓ se conecta a
Docker:
  SQL Server (puerto 1433)
```

- La app se ejecuta directamente en el host con `dotnet watch run`.
- SQL Server se ejecuta en Docker.
- La conexion usa `localhost` (definido en `appsettings.Development.json`).
- Sin colisiones de permisos: el SDK y los artefactos de compilacion (`bin/`, `obj/`) pertenecen al usuario local.

### Produccion (en Docker)

```
Docker:
  SQL Server (puerto 1433)
  openclient (puerto 8080) → imagen multi-stage
```

- Tanto la BD como la app se ejecutan en Docker.
- La imagen de la app es ligera (solo runtime, sin SDK).

---

## Resumen de puertos

| Puerto | Servicio             | Modo       |
|--------|----------------------|------------|
| 1433   | SQL Server           | Siempre    |
| 5000   | App Blazor (Dev)     | Desarrollo |
| 8080   | App Blazor (Prod)    | Produccion |

---

## Archivo `.env` requerido

El `docker-compose.yml` lee la variable `MSSQL_PASSWORD` desde un archivo `.env` en la raiz del proyecto. Ejemplo:

```
MSSQL_PASSWORD=ProdPass_abc123def456!
```

Los scripts `setup.sh` y `dev.sh` generan este archivo automaticamente con contraseñas aleatorias.

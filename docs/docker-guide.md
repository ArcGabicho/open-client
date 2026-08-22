# Guia de Docker

Archivos de configuracion Docker del proyecto, ubicados en el directorio `docker/`.

```
docker/
├── docker-compose.yml        # Infraestructura completa (BD + inicializacion + app)
├── Dockerfile                # Multi-stage build de la app (produccion)
└── database/
    ├── Dockerfile            # Contenedor de inicializacion de la BD
    ├── init.sh               # Script de arranque del init-container
    └── init.sql              # DDL idempotente (DB, login, usuario, rol)
```

---

## 1. `docker/Dockerfile` — Multi-stage Build (App)

Imagen multi-stage con 2 stages para produccion. Basada en .NET 10.0.

### Stages

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
- Copia los archivos publicados desde el stage `build`.
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
docker build -t openclient-app -f docker/Dockerfile .
```

> **Nota:** El entorno de desarrollo se ejecuta directamente en el host con `dotnet run` / `dotnet watch`, no dentro de Docker. Esto elimina las colisiones de permisos (MSB3491 / root) y mejora la experiencia del IDE.

---

## 2. `docker/database/` — Inicializacion de la Base de Datos

Contenedor one-shot que prepara SQL Server para la aplicacion: crea la estructura y carga el seed. Termina con exit code 0 unicamente si todo fue correcto (usa `sqlcmd -b`, los errores SQL abortan el proceso).

### `database/Dockerfile`

- Basado en la imagen oficial `mssql/server:2022-latest` (incluye `sqlcmd`).
- Copia `init.sh`, `init.sql` y `seed.sql` dentro de la imagen.

> **Importante:** Compose no reconstruye imagenes automaticamente. Los scripts ejecutan `build db-init` antes de cada corrida para garantizar que `/seed.sql` este presente y actualizado.

### `database/init.sh`

Flujo de inicializacion:

1. Espera a que SQL Server responda (`sqlcmd SELECT 1` en bucle).
2. Sustituye el placeholder `__MSSQL_APP_PASSWORD__` en `init.sql` por el valor real.
3. Ejecuta `init.sql` con `-b` (errores SQL → exit code distinto de 0).
4. Consulta `COUNT(*)` de `dbo.Clients`.
5. Si ya hay registros: **omite el seed** (idempotente, nunca duplica datos).
6. Si esta vacia: ejecuta `seed.sql` envuelto en una **transaccion atomica** (`SET XACT_ABORT ON` + `BEGIN TRANSACTION` + `COMMIT`). Si cualquier lote falla, el cierre de conexion hace rollback total y no quedan filas parciales.
7. Verifica post-condicion: si el seed no inserto registros → exit 1.
8. Imprime claramente el total final: `Registros en dbo.Clients: N`.

Todas las llamadas usan `-f 65001` para preservar los caracteres UTF-8 del dataset.

### `database/init.sql`

Script **idempotente** (se puede re-ejecutar sin errores):

| Objeto                    | Tipo   | Proposito                                        |
|---------------------------|--------|--------------------------------------------------|
| `OpenClientDb`            | DB     | Base de datos principal                          |
| `openclient_user`         | LOGIN  | Login de SQL Server para la app                  |
| `openclient_user`         | USER   | Usuario mapeado en `OpenClientDb`                |
| `openclient_runtime`      | ROLE   | Rol de ejecucion al que pertenece el usuario     |
| `dbo.Clients`             | TABLE  | Tabla principal (columnas alineadas al modelo EF `Client.cs`) |

La contraseña del login se inyecta via variable de entorno `MSSQL_APP_PASSWORD` (nunca se guarda en el repositorio).

### `database/seed.sql`

Dataset inicial (~4040 clientes peruanos) en 9 lotes `INSERT ... GO`. El script genera el contenido; no debe editarse a mano. Su ejecucion esta protegida por el guard de idempotencia descrito arriba.

**Reiniciar la base de datos desde cero** (borra TODOS los datos):

```bash
docker compose --env-file .env -f docker/docker-compose.yml down -v
./scripts/run.sh
```

---

## 3. `docker/docker-compose.yml` — Infraestructura

Define tres servicios encadenados por dependencias.

### Servicios

#### `sqlserver` — Base de datos

| Propiedad        | Valor                                         |
|------------------|-----------------------------------------------|
| Imagen           | `mcr.microsoft.com/mssql/server:2022-latest`  |
| Contenedor       | `openclient-database`                         |
| Puerto           | `1433:1433`                                   |
| Volumen          | `openclient_data:/var/opt/mssql`              |

**Variables de entorno:**

| Variable             | Valor                 | Descripcion                      |
|----------------------|-----------------------|----------------------------------|
| `ACCEPT_EULA`        | `Y`                   | Acepta la licencia de SQL Server |
| `MSSQL_SA_PASSWORD`  | `${MSSQL_PASSWORD}`   | Contraseña del SA (desde `.env`) |

**Healthcheck:**

- Ejecuta `SELECT 1` cada 10 segundos con `sqlcmd`.
- Timeout de 5 segundos, hasta 10 reintentos, periodo inicial de gracia de 20s.

---

#### `db-init` — Inicializador de la BD (one-shot)

| Propiedad      | Valor                              |
|----------------|------------------------------------|
| Build          | `./database`                       |
| Contenedor     | `openclient-db-init`               |
| Reinicio       | `no` (ejecuta y sale)              |

**Variables de entorno:**

| Variable             | Valor                     | Descripcion                                    |
|----------------------|---------------------------|------------------------------------------------|
| `MSSQL_PASSWORD`     | `${MSSQL_PASSWORD}`       | Contraseña SA para conectar con sqlcmd         |
| `MSSQL_APP_PASSWORD` | `${MSSQL_APP_PASSWORD}`   | Contraseña para crear el login `openclient_user`|

**Dependencia:** arranca solo cuando `sqlserver` esta `healthy`.

---

#### `openclient` — App Blazor + API REST

| Propiedad      | Valor                                  |
|----------------|----------------------------------------|
| Build          | contexto `..` con `docker/Dockerfile`  |
| Contenedor     | `openclient`                           |
| Puerto         | `8080:8080`                            |

**Variables de entorno:**

| Variable                               | Valor                                                    |
|----------------------------------------|----------------------------------------------------------|
| `ASPNETCORE_ENVIRONMENT`               | `Development`                                            |
| `ConnectionStrings__DefaultConnection` | Conecta a `sqlserver:1433` con el usuario `openclient_user` |

**Dependencia:** arranca solo cuando `db-init` termino con exito (`service_completed_successfully`).

---

### Cadena de arranque

```
sqlserver (healthy)
    ↓ depends_on: service_healthy
db-init (exit 0 = estructura + seed verificados)
    ↓ depends_on: service_completed_successfully
openclient (puerto 8080)
```

En modo desarrollo (`run.sh` / `dev.sh`), `db-init` se ejecuta con `docker compose run --rm db-init`: corre en primer plano, muestra su salida en vivo y propaga el exit code real. En modo `--full`, Compose lo orquesta via `depends_on` dentro del stack.

---

### Volumenes

| Volumen            | Montado en       | Proposito                           |
|--------------------|------------------|-------------------------------------|
| `openclient_data`  | `/var/opt/mssql` | Persistencia de datos de SQL Server |

---

### Uso con Docker Compose

```bash
COMPOSE="docker compose --env-file .env -f docker/docker-compose.yml"

# Arrancar solo SQL Server (la inicializacion la manejan los scripts)
$COMPOSE up -d sqlserver

# Ejecutar inicializacion + seed manualmente (primer plano, exit code real)
$COMPOSE run --rm db-init

# Arrancar todo el stack (BD + app en contenedor)
$COMPOSE up -d --build

# Detener el stack
$COMPOSE down

# Detener y eliminar volumenes (borra datos; reinicia la BD desde cero)
$COMPOSE down -v

# Ver logs
$COMPOSE logs -f sqlserver
$COMPOSE logs -f openclient
```

### Verificar el estado del seed

```bash
source .env
docker exec openclient-database /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$MSSQL_PASSWORD" -C \
    -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM OpenClientDb.dbo.Clients;"
```

---

## Arquitectura de desarrollo vs produccion

### Desarrollo (app en el host)

```
Host:
  dotnet run / dotnet watch (puerto 5000)
      ↓ se conecta vía localhost:1433
Docker:
  sqlserver (puerto 1433)
  db-init (one-shot, crea BD y usuario)
```

- La app se ejecuta directamente en el host; la infraestructura vive en Docker.
- La cadena `sqlserver → db-init` garantiza que `OpenClientDb` y `openclient_user` existan antes de arrancar la app.
- Sin colisiones de permisos: el SDK y los artefactos de compilacion (`bin/`, `obj/`) pertenecen al usuario local.

### Produccion (todo en Docker)

```
Docker:
  sqlserver (puerto 1433)
  db-init (one-shot)
  openclient (puerto 8080) → imagen multi-stage ligera
```

- Tanto la BD como la app se ejecutan en Docker.
- La app se conecta al host `sqlserver` (red interna de compose), no a `localhost`.

---

## Resumen de puertos

| Puerto | Servicio             | Modo       |
|--------|----------------------|------------|
| 1433   | SQL Server           | Siempre    |
| 5000   | App Blazor (Dev)     | Desarrollo |
| 8080   | App Blazor (Prod)    | Produccion |

---

## Archivo `.env` requerido

El `docker-compose.yml` lee las variables desde un archivo `.env` en la raiz del proyecto:

```
MSSQL_PASSWORD=ProdPass_abc123def456!        # Contraseña del SA
MSSQL_APP_PASSWORD=AppPass_xyz789!           # Contraseña de openclient_user
```

Los scripts `setup.sh` y `dev.sh` generan este archivo automaticamente con contraseñas aleatorias.

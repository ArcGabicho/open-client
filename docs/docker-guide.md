# Guia de Docker

Archivos de configuracion Docker del proyecto, ubicados en el directorio `docker/`.

```
docker/
├── docker-compose.yml        # Infraestructura completa (BD + inicializacion + app)
├── Dockerfile                # Multi-stage build de la app (produccion)
└── database/
    ├── Dockerfile            # Contenedor de inicializacion (login/usuario SQL Server)
    ├── init.sh               # Script de arranque del init-container
    └── init.sql              # DDL: login, usuario, rol (infraestructura SQL Server)
```

---

## 1. `docker/Dockerfile` -- Multi-stage Build (App)

Imagen multi-stage con 2 stages para produccion. Basada en .NET 10.0.

### Stages

#### `build` (Restore + Publish)

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
```

- Restaura dependencias desde los archivos `.csproj`.
- Compila y publica la aplicacion con `dotnet publish -c Release -o /app/publish`.

#### `final` (Runtime ligero)

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
```

- Imagen ligera con solo el runtime de ASP.NET (sin SDK).
- Copia los archivos publicados desde el stage `build`.
- Expone el puerto `8080`.
- Ejecuta `dotnet openclient.dll` como punto de entrada.

### Build manual

```bash
docker build -t openclient-app -f docker/Dockerfile .
```

---

## 2. `docker/database/` -- Inicializacion SQL Server

Contenedor one-shot que prepara SQL Server: crea el login, usuario y rol
necesarios para que la aplicacion .NET pueda conectarse.

> La logica de negocio (migraciones, admin, seed) la gestiona la aplicacion
> via `DbInitializer` en C#, no estos scripts.

### `database/Dockerfile`

- Basado en la imagen oficial `mssql/server:2022-latest` (incluye `sqlcmd`).
- Copia `init.sh` y `init.sql`.
- Ejecuta `init.sh` como ENTRYPOINT.

### `database/init.sh`

Flujo de inicializacion:

1. Espera a que SQL Server responda (`sqlcmd SELECT 1` en bucle).
2. Sustituye el placeholder `__MSSQL_APP_PASSWORD__` en `init.sql`.
3. Ejecuta `init.sql` con `-b` (errores SQL -> exit code distinto de 0).
4. Limpia temporales via `trap`.
5. Termina con exit 0.

### `database/init.sql`

Script idempotente (se puede re-ejecutar sin errores):

| Objeto                    | Tipo   | Proposito                                        |
|---------------------------|--------|--------------------------------------------------|
| `openclient_user`         | LOGIN  | Login de SQL Server para la app                  |
| `OpenClientDb`            | DB     | Base de datos principal (si no existe)           |
| `openclient_user`         | USER   | Usuario mapeado en `OpenClientDb`                |
| `openclient_runtime`      | ROLE   | Rol de ejecucion al que pertenece el usuario     |
| (membria)                 | ROLE   | Agrega usuario al rol                            |

La contrasena del login se inyecta via variable de entorno `MSSQL_APP_PASSWORD`.

---

## 3. `docker/docker-compose.yml` -- Infraestructura

Define tres servicios encadenados por dependencias.

### Servicios

#### `sqlserver` -- Base de datos

| Propiedad        | Valor                                         |
|------------------|-----------------------------------------------|
| Imagen           | `mcr.microsoft.com/mssql/server:2022-latest`  |
| Contenedor       | `openclient-database`                         |
| Puerto           | `1433:1433`                                   |
| Volumen          | `openclient_data:/var/opt/mssql`              |

**Healthcheck:** `SELECT 1` cada 10s, timeout 5s, 10 reintentos, 20s de gracia.

---

#### `db-init` -- Inicializador SQL Server (one-shot)

| Propiedad      | Valor                              |
|----------------|------------------------------------|
| Build          | `./database`                       |
| Contenedor     | `openclient-db-init`               |
| Reinicio       | `no` (ejecuta y sale)              |

**Variables de entorno:**

| Variable             | Valor                   | Descripcion                                    |
|----------------------|-------------------------|------------------------------------------------|
| `MSSQL_PASSWORD`     | `${MSSQL_PASSWORD}`     | Contrasena SA para conectar con sqlcmd         |
| `MSSQL_APP_PASSWORD` | `${MSSQL_APP_PASSWORD}` | Contrasena para crear el login openclient_user |

**Dependencia:** arranca solo cuando `sqlserver` esta `healthy`.

---

#### `openclient` -- App Blazor + APIs REST (/api/clients, /api/v1, /api/analytics, /api/users)

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
| `ADMIN_EMAIL`                          | `${OPENCLIENT_ADMIN_EMAIL}`                              |
| `ADMIN_PASSWORD`                       | `${OPENCLIENT_ADMIN_PASSWORD}`                           |

**Opcionales** (todas con valor por defecto seguro; ver `app-overview.md`):
`Contact__Smtp__Host` / `__Port` / `__User` / `__Password` / `__From` / `__UseSsl`
(formulario de contacto → `contact@gabicho.dev`; sin `Host` el mensaje solo se
registra en el log), `Analytics__CacheSeconds` (por defecto `0`),
`Api__Cors__AllowedOrigins__0…` (vacio = sin CORS).

**Dependencia:** arranca solo cuando `db-init` termino con exito.

---

### Cadena de arranque

```
sqlserver (healthy)
    | depends_on: service_healthy
db-init (exit 0 = login/usuario/rol creados)
    | depends_on: service_completed_successfully
openclient (puerto 8080)
    |
    |-- DbInitializer ejecuta:
    |   1. Database.MigrateAsync()
    |   2. SeedAdminAsync()
    |   3. SeedClientsAsync()
    v
Aplicacion lista
```

---

### Volumenes

| Volumen            | Montado en       | Proposito                           |
|--------------------|------------------|-------------------------------------|
| `openclient_data`  | `/var/opt/mssql` | Persistencia de datos de SQL Server |

---

### Uso con Docker Compose

```bash
COMPOSE="docker compose --env-file .env -f docker/docker-compose.yml"

$COMPOSE up -d sqlserver       # Solo SQL Server
$COMPOSE run --rm db-init      # Inicializar login/usuario
$COMPOSE up -d --build         # Stack completo
$COMPOSE down                  # Detener
$COMPOSE down -v               # Detener y borrar datos
$COMPOSE logs -f sqlserver     # Ver logs
```

---

## 4. Arquitectura de desarrollo vs produccion

### Desarrollo (app en el host)

```
Host:
  dotnet run / dotnet watch (puerto 5000)
      | se conecta via localhost:1433
Docker:
  sqlserver (puerto 1433)
  db-init (one-shot, crea login/usuario)
```

### Produccion (todo en Docker)

```
Docker:
  sqlserver (puerto 1433)
  db-init (one-shot)
  openclient (puerto 8080) -> imagen multi-stage ligera
```

---

## 5. Resumen de puertos

| Puerto | Servicio             | Modo       |
|--------|----------------------|------------|
| 1433   | SQL Server           | Siempre    |
| 5000   | App Blazor (Dev)     | Desarrollo |
| 8080   | App Blazor (Prod)    | Produccion |

---

## 6. Archivo `.env` requerido

```
MSSQL_PASSWORD=ProdPass_abc123def456!
MSSQL_APP_PASSWORD=AppPass_xyz789!
OPENCLIENT_ADMIN_EMAIL=admin@openclient.local
OPENCLIENT_ADMIN_PASSWORD=SuperPassword123!
```

Los scripts `setup.sh` y `dev.sh` generan este archivo automaticamente.
Detalle completo: [database-guide.md](database-guide.md).
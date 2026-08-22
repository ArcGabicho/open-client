# Guia de Scripts Bash

Todos los scripts se encuentran en el directorio `scripts/` y deben ejecutarse desde la raiz del proyecto.

### Resolucion de variables de entorno (`.env`)

Todos los scripts definen una variable base estandar para Docker Compose:

```bash
COMPOSE="docker compose --env-file .env -f docker/docker-compose.yml"
```

Esto garantiza que Docker Compose lea el archivo `.env` desde la raiz del proyecto y no desde el subdirectorio `docker/` (donde esta el `docker-compose.yml`). Sin esta bandera, `MSSQL_PASSWORD` y `MSSQL_APP_PASSWORD` no se detectarian y los contenedores fallarian al iniciar.

---

## 1. `scripts/setup.sh` — Instalacion automatica (One-Liner)

Script de instalacion completo que configura el proyecto desde cero. Disenado para ejecutarse directamente desde GitHub con `curl`.

### Que hace

1. Detecta la distribucion Linux (Ubuntu/Debian, Arch/CachyOS, Fedora/RHEL/CentOS) e instala `git`, `curl`, `docker` y `docker-compose`.
2. Annade el usuario al grupo `docker` si no esta en el.
3. Clona o actualiza el repositorio en `$HOME/open-client` (rama `master`).
4. Genera un archivo `.env` con dos contrasenas aleatorias seguras si no existe: `MSSQL_PASSWORD` (usuario SA) y `MSSQL_APP_PASSWORD` (login de aplicacion).
5. Ejecuta `deploy.sh` para levantar el stack completo con Docker Compose.

### Uso

```bash
# Ejecucion remota (one-liner)
curl -sSL https://raw.githubusercontent.com/ArcGabicho/open-client/master/scripts/setup.sh | bash

# Ejecucion local
chmod +x scripts/setup.sh
./scripts/setup.sh
```

### Notas

- Requiere permisos `sudo` para instalar paquetes.
- Si el directorio `$HOME/open-client` ya existe, ejecuta `git pull` en vez de clonar de nuevo.
- Las contrasenas generadas se guardan en `.env` y **deben respaldarse**.

---

## 2. `scripts/dev.sh` — Configuracion del entorno de desarrollo

Prepara toda la infraestructura necesaria para trabajar en modo desarrollo.

### Que hace

1. Verifica que Docker y Docker Compose esten instalados.
2. Crea el archivo `.env` desde `.env.example` (con claves aleatorias) si no existe, o agrega `MSSQL_APP_PASSWORD` si falta en un `.env` previo.
3. Restaura paquetes .NET (`dotnet restore`) si `dotnet` esta disponible.
4. Levanta `sqlserver` en Docker.
5. Construye la imagen de `db-init` (garantiza que `seed.sql` este dentro).
6. Ejecuta `docker compose run --rm db-init`, que:
   - espera a SQL Server,
   - crea `OpenClientDb`, el login/usuario `openclient_user`, el rol `openclient_runtime` y la tabla `dbo.Clients`,
   - carga el seed (~4040 clientes) solo si la tabla esta vacia (nunca duplica),
   - imprime el total de registros y termina con exit code real.

### Uso

```bash
chmod +x scripts/dev.sh
./scripts/dev.sh
```

### Resultado

| Servicio       | Valor                 |
|----------------|-----------------------|
| Base de datos  | `OpenClientDb @ localhost:1433` |
| Usuario app    | `openclient_user`     |
| Seed           | ~4040 registros en `dbo.Clients` |

### Notas

- La app se ejecuta en el host con `./scripts/run.sh` (o `dotnet watch run`).
- Los logs de SQL Server se pueden ver con `./scripts/run.sh --logs`.

---

## 3. `scripts/run.sh` — Control del entorno

Script para gestionar el ciclo de vida del entorno.

### Uso

```bash
./scripts/run.sh [OPCION]
```

### Opciones

| Opcion    | Descripcion                                                                                          | URL / Puerto          |
|-----------|------------------------------------------------------------------------------------------------------|-----------------------|
| (ninguna) | Levanta SQL Server, ejecuta la inicializacion + seed de la BD y la app en el host. **Por defecto**   | http://localhost:5000 |
| `--full`  | Levanta el stack completo en Docker (BD + inicializacion + app en contenedor)                        | http://localhost:8080 |
| `--stop`  | Detiene los contenedores de Docker                                                                   | —                     |
| `--logs`  | Muestra los logs en vivo de SQL Server                                                               | —                     |
| `--help`  | Muestra el mensaje de ayuda                                                                          | —                     |

### Ejemplos

```bash
# Iniciar entorno de desarrollo (BD + app en host)
./scripts/run.sh

# Stack completo en Docker
./scripts/run.sh --full

# Detener contenedores
./scripts/run.sh --stop

# Ver logs de SQL Server
./scripts/run.sh --logs
```

### Que hace el modo por defecto (paso a paso)

1. Valida que exista `.env` y carga sus variables.
2. `$COMPOSE up -d sqlserver`: levanta SQL Server con su healthcheck.
3. `$COMPOSE build db-init`: reconstruye la imagen del inicializador para que `seed.sql` este siempre actualizado dentro del contenedor.
4. `$COMPOSE run --rm db-init`: ejecuta la inicializacion en primer plano; si falla, `run.sh` se detiene mostrando el error (exit code real, sin falsos exitos).
5. `dotnet restore`.
6. `dotnet run --no-launch-profile` en el host, inyectando:
   - `ASPNETCORE_ENVIRONMENT=Development`
   - `ASPNETCORE_URLS=http://localhost:5000`
   - `ConnectionStrings__DefaultConnection` apuntando a `localhost:1433`, BD `OpenClientDb`, usuario `openclient_user`, contraseña `$MSSQL_APP_PASSWORD`.

### Inicializacion e idempotencia del seed

- `db-init` crea estructura (`init.sql`) y luego consulta `COUNT(*)` de `dbo.Clients`.
- Si ya hay registros, **omite el seed**: ejecutar `./scripts/run.sh` repetidamente nunca duplica los ~4040 clientes.
- El seed corre dentro de una transaccion atomica: un fallo a mitad de archivo no deja filas parciales.
- Para forzar una recarga completa desde cero:

```bash
docker compose --env-file .env -f docker/docker-compose.yml down -v
./scripts/run.sh
```

---

## 4. `scripts/deploy.sh` — Despliegue a produccion

Script de despliegue que soporta dos destinos: una maquina virtual (VM) con Docker o Azure Container Apps (ACA).

### Uso

```bash
./scripts/deploy.sh [OPCION]
```

Si no se pasa ninguna opcion, el script solicita elegir interactivamente.

### Opciones

| Opcion    | Descripcion                                                         |
|-----------|---------------------------------------------------------------------|
| `--vm`    | Despliega en una VM/Servidor Linux usando Docker Compose. **Por defecto** |
| `--aca`   | Despliega en Azure Container Apps (nube)                            |
| `--help`  | Muestra el mensaje de ayuda                                         |

---

### Modo `--vm` (Maquina Virtual)

1. Ejecuta `git pull origin develop` para obtener el codigo actualizado.
2. Valida que exista el archivo `.env` y que defina `MSSQL_PASSWORD` y `MSSQL_APP_PASSWORD`.
3. Reconstruye la imagen de produccion del servicio `openclient`.
4. Levanta el stack completo con `$COMPOSE up -d`: la cadena de dependencias ordena `sqlserver → db-init → openclient`.
5. Espera 10 segundos y verifica el healthcheck con `curl` en el puerto 8080 (HTTP 200 = exito).

**Requisitos previos en la VM:**
- Docker y Docker Compose instalados.
- Git instalado.
- Archivo `.env` presente en la raiz del proyecto con ambas contrasenas.

---

### Modo `--aca` (Azure Container Apps)

1. Valida que Azure CLI (`az`) este instalado.
2. Verifica la sesion activa en Azure (o ejecuta `az login`).
3. Compila la imagen Docker (`docker/Dockerfile`) y la sube a Azure Container Registry (ACR).
4. Actualiza la Container App con la nueva imagen.

**Variables de entorno configurables:**

| Variable                  | Valor por defecto     | Descripcion                     |
|---------------------------|-----------------------|---------------------------------|
| `AZURE_RESOURCE_GROUP`    | `rg-openclient`       | Grupo de recursos de Azure      |
| `AZURE_APP_NAME`          | `app-openclient`      | Nombre de la Container App      |
| `AZURE_REGISTRY_NAME`     | `acropenclient`       | Nombre del Container Registry   |

**Ejemplo con variables personalizadas:**

```bash
AZURE_RESOURCE_GROUP=mi-rg AZURE_APP_NAME=mi-app ./scripts/deploy.sh --aca
```

---

## 5. `scripts/clear.sh` — Limpieza de artefactos y contenedores

Detiene los contenedores de Docker, elimina los artefactos de compilacion locales y limpia la cache de NuGet.

### Uso

```bash
chmod +x scripts/clear.sh
./scripts/clear.sh
```

### Comportamiento

1. Solicita confirmacion interactiva antes de eliminar los volumenes de la base de datos.
2. Detiene los contenedores con `docker compose down` (o `down -v` si se confirma, lo que elimina tambien el volumen `openclient_data`).
3. Elimina las carpetas `core/bin` y `core/obj` con `rm -rf` (sin sudo).
4. Ejecuta `dotnet nuget locals all --clear` para limpiar la cache de paquetes.

```
¿Deseas eliminar también los volúmenes de la BASE DE DATOS? (s/N):
```

| Respuesta | Accion                                                             |
|-----------|--------------------------------------------------------------------|
| `s` / `S` | Elimina contenedores y volumenes (datos de SQL Server se pierden) |
| `n` / Enter | Elimina contenedores, conserva los volumenes (datos intactos)  |

---

## Flujo recomendado de uso

```
Primera instalacion:  setup.sh
Infraestructura dev:  dev.sh
Desarrollo diario:    run.sh
Stack completo:       run.sh --full
Ver logs:             run.sh --logs
Desplegar a VM:       deploy.sh --vm
Desplegar a Azure:    deploy.sh --aca
Limpiar todo:         clear.sh
```

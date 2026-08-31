# Guia de Scripts Bash

Todos los scripts se encuentran en el directorio `scripts/` y deben ejecutarse desde la raiz del proyecto.

### Resolucion de variables de entorno (`.env`)

Todos los scripts definen una variable base estandar para Docker Compose:

```bash
COMPOSE="docker compose --env-file .env -f docker/docker-compose.yml"
```

Esto garantiza que Docker Compose lea el archivo `.env` desde la raiz del proyecto y no desde el subdirectorio `docker/` (donde esta el `docker-compose.yml`). Sin esta bandera, `MSSQL_PASSWORD` y `MSSQL_APP_PASSWORD` no se detectarian y los contenedores fallarian al iniciar.

---

## 1. `scripts/setup.sh` -- Instalacion automatica (One-Liner)

Script de instalacion completo que configura el proyecto desde cero. Disenado para ejecutarse directamente desde GitHub con `curl`.

### Que hace

1. Detecta la distribucion Linux (Ubuntu/Debian, Arch/CachyOS, Fedora/RHEL/CentOS) e instala `git`, `curl`, `docker` y `docker-compose`.
2. Annade el usuario al grupo `docker` si no esta en el.
3. Clona o actualiza el repositorio en `$HOME/open-client` (rama `master`).
4. Genera un archivo `.env` con credenciales aleatorias seguras si no existe.
5. Ejecuta `deploy.sh` para levantar el stack completo con Docker Compose.

### Uso

```bash
# Ejecucion remota (one-liner)
curl -sSL https://raw.githubusercontent.com/ArcGabicho/open-client/master/scripts/setup.sh | bash

# Ejecucion local
chmod +x scripts/setup.sh
./scripts/setup.sh
```

---

## 2. `scripts/dev.sh` -- Configuracion del entorno de desarrollo

Prepara toda la infraestructura necesaria para trabajar en modo desarrollo.

### Que hace

1. Verifica que Docker y Docker Compose esten instalados.
2. Crea el archivo `.env` desde `.env.example` (con claves aleatorias) si no existe.
3. Restaura paquetes .NET (`dotnet restore`) si `dotnet` esta disponible.
4. Levanta `sqlserver` en Docker.
5. Construye y ejecuta `db-init` (login/usuario de SQL Server).

### Uso

```bash
chmod +x scripts/dev.sh
./scripts/dev.sh
```

---

## 3. `scripts/run.sh` -- Control del entorno

Script para gestionar el ciclo de vida del entorno.

### Uso

```bash
./scripts/run.sh [OPCION]
```

### Opciones

| Opcion    | Descripcion |
|-----------|-------------|
| (ninguna) | Levanta SQL Server, ejecuta db-init y la app en el host. **Por defecto** |
| `--full`  | Levanta el stack completo en Docker (BD + app en contenedor) |
| `--stop`  | Detiene los contenedores de Docker |
| `--logs`  | Muestra los logs en vivo de SQL Server |
| `--help`  | Muestra el mensaje de ayuda |

### Que hace el modo por defecto (paso a paso)

1. Valida que exista `.env` y carga sus variables.
2. `$COMPOSE up -d sqlserver`: levanta SQL Server con su healthcheck.
3. `$COMPOSE build db-init`: construye la imagen del inicializador.
4. `$COMPOSE run --rm db-init`: ejecuta la inicializacion de login/usuario.
5. `dotnet restore`.
6. `dotnet run --no-launch-profile` en el host, inyectando:
   - `ASPNETCORE_ENVIRONMENT=Development`
   - `ASPNETCORE_URLS=http://localhost:5000`
   - `ConnectionStrings__DefaultConnection`
   - `ADMIN_EMAIL` y `ADMIN_PASSWORD`

La aplicacion ejecuta `DbInitializer` al iniciar, que:
- Aplica migraciones EF Core
- Crea el administrador si no existe (BCrypt)
- Carga seed de clientes desde JSON si la tabla esta vacia

---

## 4. `scripts/deploy.sh` -- Despliegue a produccion

Script de despliegue que soporta dos destinos: una maquina virtual (VM) con Docker o Azure Container Apps (ACA).

### Uso

```bash
./scripts/deploy.sh [OPCION]
```

### Opciones

| Opcion    | Descripcion |
|-----------|-------------|
| `--vm`    | Despliega en una VM/Servidor Linux usando Docker Compose |
| `--aca`   | Despliega en Azure Container Apps (nube) |
| `--help`  | Muestra el mensaje de ayuda |

---

## 5. `scripts/clear.sh` -- Limpieza de artefactos y contenedores

Detiene los contenedores de Docker, elimina los artefactos de compilacion locales y limpia la cache de NuGet.

### Uso

```bash
chmod +x scripts/clear.sh
./scripts/clear.sh
```

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
# Guia de Scripts Bash

Todos los scripts se encuentran en el directorio `scripts/` y deben ejecutarse desde la raiz del proyecto.

### Resolucion de variables de entorno (`.env`)

Todos los scripts definen una variable base estandar para Docker Compose:

```bash
COMPOSE="docker compose --env-file .env -f docker/docker-compose.yml"
```

Esto garantiza que Docker Compose lea el archivo `.env` desde la raiz del proyecto y no desde el subdirectorio `docker/` (donde esta el `docker-compose.yml`). Sin esta bandera, `MSSQL_PASSWORD` no se detectaria y el contenedor de SQL Server fallaria al iniciar.

---

## 1. `scripts/setup.sh` — Instalacion automatica (One-Liner)

Script de instalacion completo que configura el proyecto desde cero. Disenado para ejecutarse directamente desde GitHub con `curl`.

### Que hace

1. Detecta la distribucion Linux (Ubuntu/Debian, Arch/CachyOS, Fedora/RHEL/CentOS) e instala `git`, `curl`, `docker` y `docker-compose`.
2. Annade el usuario al grupo `docker` si no esta en el.
3. Clona o actualiza el repositorio en `$HOME/open-client` (rama `master`).
4. Genera un archivo `.env` con una contrasena aleatoria segura para SQL Server si no existe.
5. Ejecuta `deploy.sh` para levantar el stack con Docker Compose.

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
- La contrasena generada se guarda en `.env` y **debe respaldarse**.

---

## 2. `scripts/dev.sh` — Configuracion del entorno de desarrollo

Prepara todo lo necesario para trabajar en modo desarrollo.

### Que hace

1. Verifica que Docker y Docker Compose esten instalados.
2. Crea el archivo `.env` desde `.env.example` (con contrasena aleatoria) si no existe, o crea uno basico.
3. Restaura paquetes .NET (`dotnet restore`) si `dotnet` esta disponible.
4. Levanta el contenedor `sqlserver` con Docker Compose usando `--env-file .env`.

### Uso

```bash
chmod +x scripts/dev.sh
./scripts/dev.sh
```

### Resultado

| Servicio       | URL / Puerto       |
|----------------|--------------------|
| App Blazor Dev | http://localhost:5000 |
| SQL Server     | localhost:1433     |

### Notas

- La app se ejecuta en el host con `dotnet run`.
- Los logs se pueden ver con `./scripts/run.sh --logs`.

---

## 3. `scripts/run.sh` — Control del entorno

Script para gestionar el ciclo de vida del entorno de desarrollo.

### Uso

```bash
./scripts/run.sh [OPCION]
```

### Opciones

| Opcion    | Descripcion                                                                              | URL / Puerto         |
|-----------|------------------------------------------------------------------------------------------|----------------------|
| (ninguna) | Levanta la BD en Docker y la app en el host con `dotnet run`. **Por defecto**            | http://localhost:5000 |
| `--stop`  | Detiene los contenedores de Docker                                                       | —                    |
| `--logs`  | Muestra los logs en vivo de SQL Server                                                   | —                    |
| `--help`  | Muestra el mensaje de ayuda                                                              | —                    |

### Ejemplos

```bash
# Iniciar entorno completo (BD + app en host)
./scripts/run.sh

# Detener contenedores
./scripts/run.sh --stop

# Ver logs de SQL Server
./scripts/run.sh --logs
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
2. Valida que exista el archivo `.env` en la raiz del proyecto.
3. Reconstruye la imagen de produccion con Docker (usando `--env-file .env`).
4. Levanta los contenedores `sqlserver` y la app de produccion.
5. Espera 10 segundos y verifica el healthcheck con `curl` (HTTP 200 = exito).

**Requisitos previos en la VM:**
- Docker y Docker Compose instalados.
- Git instalado.
- Archivo `.env` presente en la raiz del proyecto.

---

### Modo `--aca` (Azure Container Apps)

1. Valida que Azure CLI (`az`) este instalado.
2. Verifica la sesion activa en Azure (o ejecuta `az login`).
3. Compila la imagen Docker y la sube a Azure Container Registry (ACR).
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
2. Detiene los contenedores con `docker compose down` (o `down -v` si se confirma).
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
Desarrollo diario:    run.sh
Ver logs:             run.sh --logs
Desplegar a VM:       deploy.sh --vm
Desplegar a Azure:    deploy.sh --aca
Limpiar todo:         clear.sh
```

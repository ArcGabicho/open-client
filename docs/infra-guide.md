# Guia de Infraestructura (Azure)

El directorio `infra/` contiene la plantilla **Bicep** y el orquestador que despliegan
open-client en Azure sobre **Azure Container Apps** + **Azure SQL Database** +
**Azure Container Registry**.

Es el equivalente en la nube de `scripts/deploy.sh --vm` (que usa Docker Compose en
una maquina Linux). Los dos caminos son independientes: `scripts/deploy.sh` ya **no**
despliega en Azure.

```
infra/
  main.bicep             Definicion de toda la infraestructura
  main.parameters.json   Plantilla de parametros (para uso manual / CI)
  deploy.sh              Orquestador idempotente (login -> provisiona -> build -> publica)
```

---

## 1. Que se crea

| Recurso | Tipo | Nombre (derivado de `namePrefix`) |
|---|---|---|
| Identidad administrada | `Microsoft.ManagedIdentity/userAssignedIdentities` | `<prefix>-id` |
| Container Registry | `Microsoft.ContainerRegistry/registries` (Basic) | `acr<prefix><hash>` |
| Asignacion de rol **AcrPull** | `Microsoft.Authorization/roleAssignments` | (GUID determinista) |
| Log Analytics Workspace | `Microsoft.OperationalInsights/workspaces` | `<prefix>-logs` |
| Entorno de Container Apps | `Microsoft.App/managedEnvironments` | `<prefix>-env` |
| Servidor logico SQL | `Microsoft.Sql/servers` | `<prefix>-sql-<hash>` |
| Regla de firewall SQL | `Microsoft.Sql/servers/firewallRules` | `AllowAllWindowsAzureIps` (0.0.0.0) |
| Base de datos | `Microsoft.Sql/servers/databases` (Basic) | `OpenClientDb` |
| Container App | `Microsoft.App/containerApps` | `<prefix>-app` |

`<hash>` es `uniqueString(resourceGroup().id)`; garantiza nombres globalmente unicos
para el registro y el servidor SQL. El grupo de recursos (`rg-<prefix>` por defecto)
lo crea el script, no la plantilla.

### Diagrama

```
                    az acr build (build remoto, no necesita Docker local)
                             |
                             v
  +-------------------------------------------------+
  | Azure Container Registry  (acr<prefix><hash>)   |
  |   openclient-app:<timestamp>  /  :latest        |
  +-------------------------------------------------+
                             ^  pull vía identidad administrada (AcrPull)
                             |
  +-------------------------------------------------+       +--------------------------+
  | Container App  (<prefix>-app)                   |  TLS  |  Ingress externo :443    |
  |   contenedor openclient  ->  :8080              | <---- |  (HTTPS publico)         |
  |   ASPNETCORE_ENVIRONMENT=Production             |       +--------------------------+
  |   ASPNETCORE_FORWARDEDHEADERS_ENABLED=true      |
  |   ConnectionStrings__DefaultConnection (secret) |
  |   ADMIN_EMAIL / ADMIN_PASSWORD (secret)         |
  +-------------------------------------------------+
                             |  TDS 1433 (Encrypt=True)
                             v
  +-------------------------------------------------+
  | Azure SQL Database  (OpenClientDb)              |
  |   firewall: "Permitir servicios de Azure"       |
  +-------------------------------------------------+
        ^
        |  Al arrancar, la app ejecuta DbInitializer:
        |    1. Database.MigrateAsync()   (crea el esquema)
        |    2. SeedAdminAsync()          (admin BCrypt)
        |    3. SeedClientsAsync()        (seed de clientes)
```

> El script `docker/database/init.sql` (LOGIN/USUARIO/ROL de SQL Server) **no se usa**
> en Azure. La app conecta como administrador del servidor SQL y crea el esquema con
> las migraciones de EF Core. Ver la seccion 7 para el modelo de minimo privilegio.

---

## 2. Requisitos previos

- **Azure CLI** (`az`) 2.53 o superior.
- Extension Bicep: `az bicep install` (Azure CLI la instala sola la primera vez).
- **git** (el script clona el repo si se ejecuta fuera de un clon).
- Una **suscripcion de Azure** y permisos para crear grupos de recursos y
  asignaciones de rol (rol `Owner` o `Contributor` + `User Access Administrator`
  sobre la suscripcion o un grupo de recursos ya existente).
- **No** hace falta Docker en la maquina: la imagen se construye en la nube con
  `az acr build`.

```bash
az login
az account set --subscription "<ID o nombre de la suscripcion>"
```

---

## 3. Uso rapido

Desde un clon del repositorio:

```bash
./infra/deploy.sh
```

O directamente desde GitHub (el script clona/actualiza el repo en
`$HOME/open-client` antes de continuar):

```bash
curl -sSL https://raw.githubusercontent.com/ArcGabicho/open-client/master/infra/deploy.sh | bash
```

El script pide de forma interactiva (con confirmacion doble) dos contrasenas:

- **Password del administrador de Azure SQL** -> `sqlAdminPassword`
- **Password del administrador de la aplicacion** -> `appAdminPassword` (se guarda
  como hash BCrypt en `dbo.Users`)

Ambas deben cumplir la politica: 8-128 caracteres y al menos 3 de 4 grupos
(mayusculas, minusculas, digitos, simbolos).

Al terminar imprime la URL publica (`https://<prefix>-app.<region>.azurecontainerapps.io`).

### Ejecucion no interactiva (CI)

Exporta las contrasenas y el script no preguntara:

```bash
export SQL_ADMIN_PASSWORD='...'
export APP_ADMIN_PASSWORD='...'
./infra/deploy.sh
```

---

## 4. Variables de entorno del orquestador

Todas son opcionales; entre corchetes el valor por defecto.

| Variable | Descripcion |
|---|---|
| `AZURE_LOCATION` | Region de Azure. `[eastus]` |
| `AZURE_NAME_PREFIX` | Prefijo de nombres de recursos. `[openclient]` |
| `AZURE_RESOURCE_GROUP` | Grupo de recursos. `[rg-<prefix>]` |
| `AZURE_DEPLOYMENT_NAME` | Nombre del despliegue ARM. `[openclient-infra]` |
| `IMAGE_TAG` | Etiqueta de la imagen. `[<timestamp> yyyymmddHHMMSS]` |
| `SQL_ADMIN_LOGIN` | Usuario admin de Azure SQL. `[openclientadmin]` |
| `APP_ADMIN_EMAIL` | Email del admin inicial de la app. `[admin@openclient.local]` |
| `SQL_ADMIN_PASSWORD` | Si se define, no se pregunta. |
| `APP_ADMIN_PASSWORD` | Si se define, no se pregunta. |

---

## 5. Que hace el script paso a paso

1. **Sesion**: verifica `az`, hace `az login` si no hay sesion y muestra la
   suscripcion activa.
2. **Grupo de recursos**: `az group create` (idempotente).
3. **Fase 1/3 -- Infraestructura**: `az deployment group create` con
   `deployApp=false`. Crea ACR, Log Analytics, entorno de Container Apps, servidor
   y base de datos SQL, la identidad administrada y su rol **AcrPull**. **No** crea
   todavia el Container App (su imagen aun no existe).
4. **Fase 2/3 -- Build**: `az acr build` construye `docker/Dockerfile` en el
   registro y publica `openclient-app:<IMAGE_TAG>` y `openclient-app:latest`.
5. **Fase 3/3 -- Publicacion**: segundo `az deployment group create` con
   `deployApp=true` y `containerImage=<acr>.azurecr.io/openclient-app:<IMAGE_TAG>`.
   Crea (o actualiza) el Container App con la imagen real.
6. **Salida**: consulta los outputs del despliegue e imprime la URL.

El modo de despliegue ARM es **incremental**: re-ejecutar el script actualiza los
recursos existentes y publica una revision nueva del Container App; nunca borra
recursos que no esten en la plantilla.

---

## 6. Configuracion inyectada en el Container App

Definida en `main.bicep` (`template.containers[0].env`):

| Variable | Origen | Motivo |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Activa el pipeline de produccion (`UseExceptionHandler`, HSTS). |
| `ASPNETCORE_HTTP_PORTS` | `8080` | Puerto que expone el contenedor y al que apunta el ingress. |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` | El ingress termina TLS y reenvia HTTP; sin esto `UseHttpsRedirection()` entra en bucle de redireccion 307. |
| `ConnectionStrings__DefaultConnection` | secreto `connection-string` | Cadena a Azure SQL con `Encrypt=True;TrustServerCertificate=False`. |
| `ADMIN_EMAIL` | `appAdminEmail` | Email del admin inicial. |
| `ADMIN_PASSWORD` | secreto `app-admin-password` | Password del admin inicial (hash BCrypt en el arranque). |

Los valores sensibles se guardan como **secrets** del Container App, no como texto
plano en las variables de entorno.

**Sondas (probes):**

- *Liveness*: TCP al puerto 8080 (basta con el socket abierto; `/health` tambien
  consulta la BD y no sirve como liveness).
- *Readiness*: `GET /health/ready` (el health check `database`, etiquetado `ready`).
  Margen amplio (`initialDelaySeconds=20`, `failureThreshold=30`) para el primer
  arranque, en el que `DbInitializer` aplica migraciones y siembra datos antes de
  aceptar trafico.

**Escala:** `minReplicas=1`, `maxReplicas=1`. Blazor Server mantiene estado por
circuito; el ingress ya activa `stickySessions`, pero escalar a mas de una replica
requiere ademas compartir las claves de Data Protection (ver seccion 7).

---

## 7. Base de datos y endurecimiento

### Como se crea el esquema

La app conecta como **administrador del servidor SQL** y, al arrancar,
`core/Data/Seeds/DbInitializer.cs` ejecuta `Database.MigrateAsync()`, que crea todas
las tablas. Por eso no se necesita `docker/database/init.sql` ni herramientas
`sqlcmd` en la maquina que lanza el despliegue.

### Usuario de minimo privilegio (opcional)

Para que la app no use el administrador del servidor:

1. Con las migraciones ya aplicadas, conectate a la base `OpenClientDb` (por
   ejemplo con `sqlcmd`/`go-sqlcmd` tras abrir temporalmente una regla de firewall
   para tu IP publica) y crea un **usuario contenido** (sin `USE`, sin
   `CREATE LOGIN` de servidor, que no aplican en Azure SQL):

   ```sql
   CREATE USER [openclient_app] WITH PASSWORD = '<password fuerte>';
   ALTER ROLE db_datareader ADD MEMBER [openclient_app];
   ALTER ROLE db_datawriter ADD MEMBER [openclient_app];
   ALTER ROLE db_ddladmin   ADD MEMBER [openclient_app];  -- necesario para MigrateAsync
   ```

2. Cambia el secreto `connection-string` del Container App para usar
   `User ID=openclient_app` y su password, y reinicia la revision.

### Otras mejoras de seguridad

- **Red privada**: integrar el entorno de Container Apps en una VNet y sustituir la
  regla de firewall `0.0.0.0` por un **Private Endpoint** de Azure SQL
  (`publicNetworkAccess=Disabled`).
- **Azure Key Vault** + referencias de secretos en lugar de pasar contrasenas por
  linea de comandos (visibles en la lista de procesos).
- **Microsoft Entra ID** como autenticacion de Azure SQL (identidad administrada del
  Container App como usuario de BD), eliminando contrasenas de SQL.
- **Escalado horizontal**: persistir las claves de Data Protection (Blob Storage o
  Key Vault) y mantener `stickySessions` para poder subir `maxReplicas`.

---

## 8. Despliegue manual (sin el orquestador)

```bash
PREFIX=openclient
RG=rg-$PREFIX

az group create -n "$RG" -l eastus

# 1. Infra sin la app
az deployment group create -g "$RG" -n openclient-infra \
  -f infra/main.bicep \
  -p namePrefix=$PREFIX deployApp=false containerImage=unused \
     sqlAdminLogin=openclientadmin sqlAdminPassword='<...>' \
     appAdminEmail=admin@openclient.local appAdminPassword='<...>'

ACR=$(az deployment group show -g "$RG" -n openclient-infra \
       --query properties.outputs.acrName.value -o tsv)

# 2. Imagen
az acr build --registry "$ACR" --image openclient-app:latest \
  --file docker/Dockerfile .

# 3. App
az deployment group create -g "$RG" -n openclient-infra \
  -f infra/main.bicep \
  -p namePrefix=$PREFIX deployApp=true \
     containerImage="$ACR.azurecr.io/openclient-app:latest" \
     sqlAdminLogin=openclientadmin sqlAdminPassword='<...>' \
     appAdminEmail=admin@openclient.local appAdminPassword='<...>'
```

`infra/main.parameters.json` sirve como plantilla de parametros para CI
(`--parameters infra/main.parameters.json --parameters containerImage=...`);
recuerda rellenar `sqlAdminPassword`, `appAdminPassword` y `containerImage`.

---

## 9. Operacion

### Publicar una version nueva

```bash
./infra/deploy.sh          # reconstruye la imagen y publica una revision nueva
```

### Solo redeploy de codigo (imagen ya construida)

```bash
az containerapp update -g rg-openclient -n openclient-app \
  --image <acr>.azurecr.io/openclient-app:<tag>
```

### Ver logs

```bash
az containerapp logs show -g rg-openclient -n openclient-app --follow
# o en Log Analytics (tabla ContainerAppConsoleLogs_CL)
```

### Verificar salud

```bash
URL=$(az deployment group show -g rg-openclient -n openclient-infra \
        --query properties.outputs.appUrl.value -o tsv)
curl -i "$URL/health"        # 200 cuando la BD responde
curl -i "$URL/health/ready"
```

### Login del administrador

```bash
curl -X POST "$URL/auth/log-in" \
  -d "Email=admin@openclient.local&Password=<APP_ADMIN_PASSWORD>"
```

### Eliminar toda la infraestructura

```bash
az group delete -n rg-openclient --yes --no-wait
```

---

## 10. Problemas frecuentes

| Sintoma | Causa / solucion |
|---|---|
| `az acr build` falla con *"registry not found"* | La Fase 1 no termino. Revisa el despliegue `openclient-infra` en el portal. |
| La app responde 307 en bucle | Falta `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` (ya incluido en la plantilla; comprobar que la revision activa lo tiene). |
| La revision no pasa a *Healthy* | Primer arranque largo por el seed; revisa los logs. Si persiste, suele ser la cadena de conexion o el firewall de SQL. |
| `Login failed for user` en los logs | Password de SQL incorrecta o el servidor no admite el trafico. Verifica la regla `AllowAllWindowsAzureIps`. |
| El pull de imagen falla con *unauthorized* | La asignacion `AcrPull` tarda en propagarse (~1 min). El flujo de 3 fases del script deja tiempo suficiente; en manual, reintenta la Fase 3. |
| `RoleAssignmentUpdateNotPermitted` al re-desplegar | El nombre del rol es determinista; si cambiaste `namePrefix` quedan asignaciones huerfanas. Borra el grupo o la asignacion antigua. |

---

## 11. Coste aproximado (referencia, region este de EE. UU.)

| Recurso | SKU | Orden de magnitud |
|---|---|---|
| Azure SQL Database | Basic (2 GB) | ~5 USD/mes |
| Container Apps | 0.5 vCPU / 1 GiB, 1 replica | consumo; unos pocos USD/mes con trafico bajo |
| Container Registry | Basic | ~5 USD/mes |
| Log Analytics | PerGB2018, 30 dias | primeros 5 GB/mes gratis |

Ajusta `databaseSkuName`/`databaseSkuTier`, `containerCpu`/`containerMemory` y la
escala segun la carga real.

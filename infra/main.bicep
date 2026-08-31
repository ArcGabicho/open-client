metadata description = 'Infraestructura de open-client en Azure: Azure Container Apps + Azure SQL Database + Azure Container Registry, con identidad administrada para el pull de imagenes.'

// ============================================================
// Parametros
// ============================================================

@description('Prefijo para nombrar los recursos. Solo minusculas, numeros y guiones.')
@minLength(3)
@maxLength(20)
param namePrefix string = 'openclient'

@description('Region de Azure. Por defecto, la del grupo de recursos.')
param location string = resourceGroup().location

@description('Usuario administrador del servidor logico de Azure SQL.')
@minLength(4)
param sqlAdminLogin string = 'openclientadmin'

@description('Password del administrador de Azure SQL. Politica: 8-128 caracteres y 3 de 4 grupos (mayus, minus, digito, simbolo).')
@secure()
@minLength(8)
@maxLength(128)
param sqlAdminPassword string

@description('Email del administrador inicial de la aplicacion (fila en dbo.Users).')
param appAdminEmail string = 'admin@openclient.local'

@description('Password del administrador inicial de la aplicacion. Se guarda como hash BCrypt; cambiarlo despues no reescribe un admin ya creado.')
@secure()
@minLength(8)
param appAdminPassword string

@description('Si es false, se aprovisiona todo menos el Container App (ACR, SQL, entorno). infra/deploy.sh lo usa para crear el registro, construir la imagen y solo despues desplegar la app con esa imagen real.')
param deployApp bool = true

@description('Imagen del contenedor a desplegar (ej. <acr>.azurecr.io/openclient-app:latest). Debe existir antes de poner deployApp=true. Con deployApp=false su valor se ignora.')
param containerImage string

@description('vCPU asignadas al contenedor. Debe combinar con containerMemory segun la tabla de Azure Container Apps.')
param containerCpu string = '0.5'

@description('Memoria asignada al contenedor.')
param containerMemory string = '1.0Gi'

@description('Nombre de la base de datos de la aplicacion.')
param databaseName string = 'OpenClientDb'

@description('SKU de la base de datos (Basic, S0, S1, ...).')
param databaseSkuName string = 'Basic'

@description('Nivel de servicio de la base de datos.')
param databaseSkuTier string = 'Basic'

// ============================================================
// Nombres derivados
// ============================================================

var affix = uniqueString(resourceGroup().id)
var acrName = toLower('acr${take(replace(namePrefix, '-', ''), 10)}${affix}')
var sqlServerName = toLower('${namePrefix}-sql-${affix}')
var logAnalyticsName = '${namePrefix}-logs'
var containerEnvName = '${namePrefix}-env'
var containerAppName = '${namePrefix}-app'
var identityName = '${namePrefix}-id'

// AcrPull: rol integrado de Azure.
var acrPullRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')

// Cadena de conexion: la app conecta como administrador del servidor SQL y aplica
// las migraciones de EF Core al arrancar (DbInitializer), por lo que necesita
// permisos DDL. Encrypt=True es obligatorio en Azure SQL.
var connectionString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${databaseName};User ID=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'

// ============================================================
// Identidad administrada (pull de imagenes desde ACR)
// ============================================================

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
}

// ============================================================
// Azure Container Registry
// ============================================================

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
  }
}

// Concede AcrPull a la identidad administrada sobre el registro.
resource acrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, identity.id, 'AcrPull')
  scope: registry
  properties: {
    principalId: identity.properties.principalId
    roleDefinitionId: acrPullRoleId
    principalType: 'ServicePrincipal'
  }
}

// ============================================================
// Log Analytics + entorno de Container Apps
// ============================================================

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource containerEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: containerEnvName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// ============================================================
// Azure SQL Database
// ============================================================

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// Permite el trafico de servicios de Azure (incluido Container Apps, que no tiene
// IP de salida estable sin integracion de red virtual).
resource sqlAllowAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: databaseSkuName
    tier: databaseSkuTier
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
  }
}

// ============================================================
// Container App
// ============================================================

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = if (deployApp) {
  name: containerAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerEnv.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
        // Blazor Server mantiene estado por circuito; si se escala a >1 replica
        // hace falta afinidad de sesion.
        stickySessions: {
          affinity: 'sticky'
        }
      }
      registries: [
        {
          server: registry.properties.loginServer
          identity: identity.id
        }
      ]
      secrets: [
        {
          name: 'connection-string'
          value: connectionString
        }
        {
          name: 'app-admin-password'
          value: appAdminPassword
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'openclient'
          image: containerImage
          resources: {
            cpu: json(containerCpu)
            memory: containerMemory
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_HTTP_PORTS'
              value: '8080'
            }
            {
              // Evita el bucle de redireccion de UseHttpsRedirection() detras del
              // ingress de Container Apps, que termina TLS y reenvia HTTP.
              name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
              value: 'true'
            }
            {
              name: 'ConnectionStrings__DefaultConnection'
              secretRef: 'connection-string'
            }
            {
              name: 'ADMIN_EMAIL'
              value: appAdminEmail
            }
            {
              name: 'ADMIN_PASSWORD'
              secretRef: 'app-admin-password'
            }
          ]
          probes: [
            {
              // La app no expone un endpoint puramente de proceso: /health tambien
              // consulta la BD. Para "liveness" basta con el socket abierto.
              type: 'Liveness'
              tcpSocket: {
                port: 8080
              }
              initialDelaySeconds: 20
              periodSeconds: 30
              failureThreshold: 3
            }
            {
              // En el primer arranque DbInitializer aplica migraciones y siembra
              // datos antes de aceptar trafico; damos un margen amplio.
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: 8080
              }
              initialDelaySeconds: 20
              periodSeconds: 10
              failureThreshold: 30
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
  dependsOn: [
    acrPull
  ]
}

// ============================================================
// Salidas
// ============================================================

// Con deployApp=false el Container App no existe todavia; el operador de
// desreferencia segura (.?) evita el aviso BCP318 y devuelve cadena vacia.
var ingressFqdn = containerApp.?properties.configuration.ingress.fqdn ?? ''

output acrName string = registry.name
output acrLoginServer string = registry.properties.loginServer
output containerAppName string = containerApp.?name ?? ''
output appFqdn string = ingressFqdn
output appUrl string = empty(ingressFqdn) ? '' : 'https://${ingressFqdn}'
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = databaseName
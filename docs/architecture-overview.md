# Arquitectura

Visión de las capas de Open Client tras el refactor a repositorio +
`IDbContextFactory` + validación con FluentValidation + logging con Serilog.

---

## 1. Capas

```
Componentes Blazor (.razor)          ── UI, estado de pantalla
Controllers/  (API REST JSON)        ── contrato HTTP, códigos de estado
   ClientsController      → /api/clients        (CRUD administrativo)
   Api/V1 ClientsController→ /api/v1/clients     (integración, solo lectura)
   AnalyticsController    → /api/analytics/*     (métricas agregadas)
   UsersController        → /api/users/*         (política Users.Admin)
        │
        ▼
Services/  casos de uso: validación, mapeo, orquestación
   IClientService · IAuthService · IApiClientService · IAnalyticsService
   IUserService (+ IUserAuditLogger) · IContactMailer
        │
        ▼
Data/Repositories/  (IClientRepository, IUserRepository)  ── acceso a datos, traducción a SQL
        │
        ▼
Data/  (OpenClientDbContext, IDbContextFactory)  ── EF Core
        │
        ▼
SQL Server 2022 (Docker / Azure)
```

Los módulos de **Analíticas** y **API de integración** no usan repositorio: abren
su propio `OpenClientDbContext` desde el factory y resuelven todo con `IQueryable`
+ agregaciones traducibles a SQL (`GROUP BY`, `COUNT`, `SUM`), sin materializar
tablas completas en memoria.

Reglas de dependencia:

- La UI (Blazor y controladores) depende **solo de interfaces de servicio**
  (`OpenClient.Interfaces`).
- Los servicios dependen del repositorio (interfaz) y de `IValidator<T>`. **No**
  referencian `Microsoft.EntityFrameworkCore`.
- El repositorio es el único que usa EF Core.
- Los servicios devuelven y aceptan **DTOs** (`OpenClient.Models.DTO`); las
  entidades de dominio (`OpenClient.Models.Domain`) no cruzan hacia la UI.

---

## 2. Acceso a datos: `IDbContextFactory`

Toda la aplicación usa **`AddDbContextFactory<OpenClientDbContext>`**; no hay un
`DbContext` scoped. Cada operación abre su propio contexto y lo libera:

```csharp
await using var db = await _contextFactory.CreateDbContextAsync(ct);
```

Motivo: en Blazor Server el circuito es de larga duración y un `DbContext`
scoped se comparte entre renders concurrentes (no es thread-safe). El factory
da una instancia fresca por operación.

Consumidores del factory: `ClientRepository`, `UserRepository`, `AuthService`,
`ApiClientService`, `AnalyticsService`, `DbInitializer`, `DbHealthCheck` y el
evento `OnValidatePrincipal` de la cookie.

**Tiempo de diseño**: `Data/Context/OpenClientDbContextFactory.cs`
(`IDesignTimeDbContextFactory<OpenClientDbContext>`) permite que
`dotnet ef migrations` cree el contexto sin arrancar la DI. Lee
`ConnectionStrings__DefaultConnection` del entorno o usa un marcador (para
`migrations add` no se abre conexión).

---

## 3. Repositorio de clientes

`IClientRepository` (`core/Data/Repositories/`):

| Método | Nota |
| --- | --- |
| `GetByIdAsync(id)` | `AsNoTracking`, excluye `IsDeleted`. |
| `GetPagedAsync(filter)` | Búsqueda (`EF.Functions.Like`), filtro de industria, orden y `Skip`/`Take` **en SQL**. Devuelve `(IReadOnlyList<Client> Items, int TotalCount)`. |
| `GetRawIndustriesAsync()` | `SELECT DISTINCT Industry` sin normalizar. |
| `AddAsync(client)` | Inserta y guarda; devuelve el id. |
| `UpdateAsync(id, Action<Client> apply)` | Carga la entidad rastreada, ejecuta `apply` y guarda. No usa `SetValues` para no pisar `Id`/`CreatedAt`/`IsDeleted`. |
| `SoftDeleteAsync(id)` | `IsDeleted = true`, `DeletedAt = UtcNow`. |
| `ExistsAsync(id)` | `AnyAsync`, excluye borrados. |

El listado paginado se resuelve **siempre en la base de datos**: nunca se carga
la tabla completa (~4018 filas) en memoria.

---

## 4. Borrado lógico y auditoría

`Client` incorpora:

| Campo | Uso |
| --- | --- |
| `UpdatedAt` (`DateTime?`) | Lo fija `ClientService.UpdateAsync` en cada edición. |
| `IsDeleted` (`bool`, default `false`) | Marca de borrado lógico. Índice `IX_Clients_IsDeleted`. |
| `DeletedAt` (`DateTime?`) | Momento del borrado. |

El filtro `!IsDeleted` se aplica **explícitamente** en cada consulta del
repositorio (no hay `HasQueryFilter` global, para que ninguna consulta oculte
filas de forma implícita). El seed y la inicialización no se ven afectados.

Migración: `Data/Migrations/*_AddClientSoftDeleteAndAudit.cs`.

---

## 5. Validación (FluentValidation)

- `core/Models/Validators/ClientEditModelValidator.cs` — reglas de servidor para
  crear/editar (obligatoriedad de `CompanyName`, formato de email, RUC de 11
  dígitos, URL absoluta, longitudes máximas).
- `ClientSearchFilterValidator.cs` — comprobación defensiva del filtro de
  consulta.
- `CreateUserRequestValidator` / `UpdateUserRequestValidator` /
  `ChangePasswordRequestValidator` / `UserSearchFilterValidator` — módulo de
  Usuarios. `PasswordRules` centraliza la política de contraseña (mínimo 8, con
  letra y dígito, confirmación exacta).
- `ContactMessageValidator` — formulario de contacto público.
- Registro: `AddValidatorsFromAssemblyContaining<ClientEditModelValidator>()`
  descubre **todos** los validadores del ensamblado.
- `ClientService.CreateAsync`/`UpdateAsync` llaman a `ValidateAndThrowAsync`. Un
  fallo lanza `FluentValidation.ValidationException`, que:
  - en `ClientsController` se traduce a **HTTP 400** con errores por campo
    (`ValidationProblem`),
  - en el panel Blazor lo captura `SaveFormAsync` y lo muestra en `formError`.

---

## 6. Inyección de dependencias

`core/Extensions/ServiceExtensions.cs` agrupa el registro:

| Método | Registra |
| --- | --- |
| `AddApplicationServices(config)` | `AddDbContextFactory`, `IClientRepository`, `IAuthService`, `IClientService`, `IDbInitializer`, `IContactMailer`, validadores. |
| `AddIntegrationApi(config)` | `IApiClientService`, documento OpenAPI `v1` (acotado a `api/v1`), política CORS con nombre `ApiV1.Cors` (leída de `Api:Cors:AllowedOrigins`, inactiva mientras esté vacía). |
| `AddAnalytics()` | `IAnalyticsService` + `AddMemoryCache` (caché opcional, `Analytics:CacheSeconds`). |
| `AddUserManagement()` | `IUserRepository`, `IUserService`, `IUserAuditLogger`. |
| `AddCookieAuthentication()` | Esquema de cookies (`/log-in`, expiración 8 h, sliding). Eventos: bajo `/api` responde 401/403 en vez de redirigir al HTML; `OnValidatePrincipal` revalida (máx. cada 3 min) que el usuario siga activo y lo expulsa si fue desactivado/eliminado. `AddAuthorization` con políticas `ApiV1.Read` (roles `Admin`/`Integrations`) y `Users.Admin` (rol `Admin`). |
| `AddObservability()` | Health checks (`DbHealthCheck` con etiqueta `ready`). |

`Program.cs` encadena estos métodos, añade `AddControllers`,
`AddHttpContextAccessor`, `AddCascadingAuthenticationState`, configura Serilog y
compone el pipeline (`UseApiErrorHandling` antes de la autenticación,
`MapOpenApi` tras `MapControllers`).

---

## 7. Logging (Serilog)

`Program.cs` → `builder.Host.UseSerilog(...)`:

- Nivel mínimo `Information`; `Microsoft.AspNetCore` a `Warning`.
- Sinks: consola + fichero `logs/openclient-.log` (rotación diaria, 7 días).
- `app.UseSerilogRequestLogging()` para el resumen de cada petición HTTP.

Los servicios y repositorios registran altas, ediciones, borrados y consultas
mediante `ILogger<T>`. **Nunca** se registran contraseñas, hashes ni secretos.

---

## 8. Health checks

| Endpoint | Contenido |
| --- | --- |
| `GET /health` | Estado global (todas las comprobaciones). |
| `GET /health/ready` | Solo comprobaciones con la etiqueta `ready` (actualmente `DbHealthCheck`, que valida `CanConnectAsync`). |

Útiles para readiness/liveness probes en Azure Container Apps o Docker.

---

## 9. API REST

Ver `docs/rest-api.md`. Cuatro grupos de controladores, todos `[Authorize]` y
autenticados con la cookie de sesión:

| Controlador | Base | Notas |
| --- | --- | --- |
| `ClientsController` | `/api/clients` | CRUD administrativo; comparte `IClientService` con el panel. |
| `Api.V1.ClientsController` | `/api/v1/clients` | Solo lectura, versionada. DTO propio (`ApiClientDto`), envoltura `{ data, pagination }`, errores `{ "error": { "code", "message" } }`. Política `ApiV1.Read`. `ApiErrorMiddleware` da formato homogéneo a 401/403/404/500 bajo `/api/v1`. Documento OpenAPI en `/openapi/v1.json`. |
| `AnalyticsController` | `/api/analytics/*` | Métricas agregadas en SQL; filtro temporal `from`/`to`, `top`, `bucket`; comparación contra el período anterior. Caché opcional. |
| `UsersController` | `/api/users/*` | Política `Users.Admin`. El `UserService` revalida el `ClaimsPrincipal` (chokepoint), aplica las protecciones (autoprotección, último administrador, rol permitido), usa un testigo de concurrencia (`User.ConcurrencyStamp`) y audita vía `IUserAuditLogger` (Serilog estructurado). |

## 10. Módulo de Usuarios — reglas transversales

- **Identidad existente, sin sistema paralelo**: entidad `User` + BCrypt + cookie.
  El rol es la columna `User.Role` (claim `ClaimTypes.Role`); el módulo reconoce
  un conjunto cerrado `{Admin, Manager, User}`.
- **Nuevos campos de `User`**: `UserName` (único), `LastLoginAt` (lo sella
  `AuthService.RecordSuccessfulLoginAsync` tras el login) y `ConcurrencyStamp`.
  Migración `*_AddUserAccountFields` (con backfill de filas existentes).
- **Auditoría**: `USER_AUDIT action= actor= actorId= targetUserId= timestamp=`.
  Nunca registra contraseñas, hashes ni tokens.
- **Nunca se exponen** `PasswordHash` ni security stamps; `ConcurrencyStamp` solo
  viaja en el detalle (necesario para la edición optimista).
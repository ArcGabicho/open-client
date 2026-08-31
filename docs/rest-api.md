# API REST

Todas las APIs comparten la autenticación por cookie de sesión
(`.OpenClient.Auth`), responden `application/json` y propagan `CancellationToken`.
Bajo `/api/*` una petición no autenticada recibe **401** (no se redirige al HTML
de login); autenticada sin permiso, **403**.

| Grupo | Base | Autorización | Sección |
|-------|------|--------------|---------|
| CRUD administrativo de clientes | `/api/clients` | `[Authorize]` (cualquier sesión) | [1](#1-crud-administrativo-de-clientes-apiclients) |
| API de integración (solo lectura, versionada) | `/api/v1/clients` | política `ApiV1.Read` (roles `Admin`/`Integrations`) | [2](#2-api-de-integración-apiv1) |
| Analíticas | `/api/analytics/*` | `[Authorize]` | [3](#3-analíticas-apianalytics) |
| Usuarios | `/api/users/*` | política `Users.Admin` (rol `Admin`) | [4](#4-usuarios-apiusers) |
| OpenAPI de la API v1 | `/openapi/v1.json` | anónimo | — |
| Salud | `/health`, `/health/ready` | anónimo | — |

---

## 1. CRUD administrativo de clientes (`/api/clients`)

`ClientsController` (`core/Controllers/ClientsController.cs`) expone el catálogo
de clientes como JSON. Es la misma capa de servicio (`IClientService`) que
consume el panel Blazor; el panel la usa **en proceso**, no por HTTP.

- **Base**: `/api/clients`
- **Autenticación**: `[Authorize]`; sin cookie válida → **302** a `/log-in`
  (navegador) o **401** (cliente API).

---

## Endpoints

### `GET /api/clients`

Listado paginado. Parámetros por query string (`ClientSearchFilter`):

| Parámetro | Tipo | Por defecto | Notas |
| --- | --- | --- | --- |
| `search` | string | — | Coincidencia parcial (`LIKE`) en razón comercial, razón social, nombre, apellido, correo y RUC. |
| `industry` | string | — | Industria exacta (se comparan valores recortados). |
| `sortBy` | string | `recent` | `recent` \| `name` \| `oldest`. |
| `page` | int | `1` | Se fuerza a ≥ 1. |
| `pageSize` | int | `10` | Solo `10`, `25`, `50`, `100`; otro valor → `10`. |

**200** → `PagedResult<ClientListItemDto>`:

```json
{
  "items": [
    {
      "id": 42,
      "companyName": "NOBEX FOODS",
      "legalName": "AGROINDUSTRIAS NOBEX S.A.",
      "firstName": "VANESA", "lastName": "BELLIDO", "jobTitle": "AREA COMPRAS",
      "industry": "ELAB. DE VINOS.", "taxId": "20342015108",
      "email": null, "phoneNumber": null,
      "website": "http://www.agronobex.com",
      "address": "AV. LOS FAISANES NRO. 148", "district": null, "province": "CHINCHA",
      "createdAt": "2026-07-29T14:18:17Z", "updatedAt": null
    }
  ],
  "page": 1, "pageSize": 10, "totalCount": 4018,
  "totalPages": 402, "firstItemIndex": 1, "lastItemIndex": 10,
  "hasPrevious": false, "hasNext": true
}
```

Las filas con borrado lógico (`isDeleted = true`) nunca aparecen.

---

### `GET /api/clients/industries`

**200** → `string[]` con las industrias distintas, recortadas, sin vacíos,
sin duplicados por mayúsculas/espacios y ordenadas alfabéticamente.

```json
["Agroindustria", "Comercio", "ELAB. DE VINOS.", "Servicios"]
```

---

### `GET /api/clients/{id}`

| Respuesta | Cuándo |
| --- | --- |
| **200** `ClientDetailDto` | El cliente existe y no está borrado. |
| **404** | No existe o está borrado. |

```json
{
  "id": 42, "companyName": "NOBEX FOODS", "legalName": "AGROINDUSTRIAS NOBEX S.A.",
  "taxId": "20342015108", "industry": "ELAB. DE VINOS.",
  "email": null, "phoneNumber": null, "website": "http://www.agronobex.com",
  "address": "AV. LOS FAISANES NRO. 148", "district": null, "province": "CHINCHA",
  "firstName": "VANESA", "lastName": "BELLIDO", "jobTitle": "AREA COMPRAS",
  "createdAt": "2026-07-29T14:18:17Z", "updatedAt": null
}
```

---

### `POST /api/clients`

Cuerpo: `CreateClientDto`. `companyName` es obligatorio; el resto es opcional.

```json
{
  "companyName": "Comercial Lima S.A.C.",
  "taxId": "20512345678",
  "industry": "Comercio",
  "email": "ventas@comercialima.pe",
  "phoneNumber": "+51 987 654 321",
  "website": "https://comercialima.pe",
  "firstName": "María", "lastName": "Quispe", "jobTitle": "Compras"
}
```

| Respuesta | Cuándo |
| --- | --- |
| **201 Created** | Creado. `Location` apunta a `GET /api/clients/{id}`; cuerpo = `ClientDetailDto`. |
| **400 Bad Request** | Falla `ClientEditModelValidator` (p. ej. `companyName` vacío, RUC ≠ 11 dígitos, email o URL inválidos). Cuerpo = `ValidationProblemDetails` con errores por campo. |

---

### `PUT /api/clients/{id}`

Reemplaza todos los campos editables. Cuerpo: `UpdateClientDto` (misma forma que
`CreateClientDto`). Fija `updatedAt = UtcNow`.

| Respuesta | Cuándo |
| --- | --- |
| **204 No Content** | Actualizado. |
| **400 Bad Request** | Falla la validación. |
| **404 Not Found** | El cliente no existe o está borrado. |

---

### `DELETE /api/clients/{id}`

Borrado **lógico**: marca `isDeleted = true` y `deletedAt = UtcNow`. La fila
permanece en la tabla.

| Respuesta | Cuándo |
| --- | --- |
| **204 No Content** | Borrado. |
| **404 Not Found** | No existe o ya estaba borrado. |

---

## Errores de validación (400)

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "CompanyName": ["La razón comercial es obligatoria."],
    "TaxId": ["El RUC debe tener 11 dígitos."]
  }
}
```

---

## Ejemplos con `curl`

```bash
# Requiere cookie de sesión: primero inicia sesión y guarda la cookie.
curl -s -c cookies.txt -b cookies.txt \
  -d 'Email=admin@openclient.local' -d 'Password=...' \
  -H "X-CSRF-TOKEN: ..." http://localhost:5000/auth/log-in

# Listado
curl -s -b cookies.txt \
  'http://localhost:5000/api/clients?search=lima&sortBy=name&page=1&pageSize=25'

# Detalle
curl -s -b cookies.txt http://localhost:5000/api/clients/42

# Alta
curl -s -b cookies.txt -X POST http://localhost:5000/api/clients \
  -H 'Content-Type: application/json' \
  -d '{"companyName":"Comercial Lima S.A.C.","taxId":"20512345678"}'

# Borrado lógico
curl -s -b cookies.txt -X DELETE http://localhost:5000/api/clients/42 -i
```

> El formulario de login usa antiforgery; para automatizar llamadas conviene
> exponer un login de servicio o usar el panel directamente. La API está pensada
> para consumo desde el mismo dominio autenticado.

---

## 2. API de integración (`/api/v1`)

`core/Controllers/ApiController.cs` (namespace `OpenClient.Controllers.Api.V1`).
API REST **de solo lectura**, versionada e independiente del CRUD administrativo:
servicio propio (`IApiClientService`), DTO propio (`ApiClientDto`, contrato
público), envoltura `{ data, pagination }` y errores
`{ "error": { "code", "message" } }`. `ApiErrorMiddleware` da ese formato también
a los 401/403/404/405 vacíos bajo `/api/v1`.

- **Autorización**: política `ApiV1.Read` → autenticado + rol `Admin` o
  `Integrations`. Preparada para sumar API Keys como requisito alternativo.
- **Paginación**: `page` (≥ 1, por defecto 1), `pageSize` (1..100, por defecto
  25). Fuera de rango → **400** `invalid_pagination`.
- **OpenAPI**: `GET /openapi/v1.json` (parámetros, DTOs, respuestas, códigos).

| Método | Ruta | Respuestas |
|--------|------|------------|
| `GET` | `/api/v1/clients?page=&pageSize=` | **200** `{ data: ApiClientDto[], pagination: { page, pageSize, totalItems, totalPages } }` · **400** |
| `GET` | `/api/v1/clients/{id}` | **200** `ApiClientDto` · **404** `client_not_found` |
| `GET` | `/api/v1/clients/search?search=&companyName=&legalName=&industry=&province=&district=&jobTitle=&taxId=&page=&pageSize=` | **200** (misma envoltura, filtros combinables, resueltos en SQL) · **400** |

```json
{
  "data": [ { "id": 42, "companyName": "NOBEX FOODS", "industry": "ELAB. DE VINOS.", "province": "CHINCHA", "createdAt": "2026-07-29T14:18:17Z" } ],
  "pagination": { "page": 1, "pageSize": 25, "totalItems": 4018, "totalPages": 161 }
}
```

---

## 3. Analíticas (`/api/analytics`)

`core/Controllers/AnalyticsController.cs`. Métricas agregadas sobre `Clients`,
**calculadas en SQL Server** (`GROUP BY`, `COUNT`, histograma diario). `[Authorize]`.

- **Filtro temporal**: `from` / `to` (`yyyy-MM-dd`). Sin período → últimos 365
  días. `from > to` → **400** `invalid_period`. El período acota las métricas
  temporales y las distribuciones; `totalClients` es global.
- **Top N**: `top` (1..50, por defecto 10) en las distribuciones.
- **Bucket**: `bucket` = `day` \| `week` \| `month` (por defecto) \| `year` en `growth`.

| Método | Ruta | Devuelve |
|--------|------|----------|
| `GET` | `/api/analytics` | Resumen completo: `overview` (con `newClients` como `MetricDto { value, percentageChange }`), `completeness`, `industries[]`, `provinces[]`, `districts[]`, `jobTitles[]`, `growth[]`. |
| `GET` | `/api/analytics/industries` · `/provinces` · `/job-titles` | `DistributionDto[]` (`label`, `value`, `percentage`), orden descendente, `Unknown` para nulos. |
| `GET` | `/api/analytics/districts?province=` | Igual; opcionalmente acotado a una provincia. |
| `GET` | `/api/analytics/growth` | `ChartDataDto` (`bucket`, `from`, `to`, `points: { period, value }[]`), con relleno de huecos a cero. |
| `GET` | `/api/analytics/completeness` | Cobertura de `phone` / `email` / `website` / `address` / `taxId` (`{ count, percentage }`). |

---

## 4. Usuarios (`/api/users`)

`core/Controllers/UsersController.cs`. Administración de las cuentas del panel.
**Política `Users.Admin`** (rol `Admin`); además `UserService` revalida el
principal, así que ni la UI Blazor ni la API pueden saltarse el control.

- Errores: `{ "error": { "code", "message" } }`; los de validación añaden
  `details: string[]`. Códigos: `user_not_found` (404), `duplicate_email` /
  `duplicate_username` / `concurrency` (409), `last_admin` /
  `forbidden_self_deactivate` / `forbidden_self_delete` (409),
  `validation_failed` (400).
- **Concurrencia**: la edición envía el `concurrencyStamp` leído en el detalle;
  si otro admin modificó el usuario entre medias → **409** `concurrency`.
- **Protecciones**: no puedes desactivarte/eliminarte a ti mismo; ninguna
  operación puede dejar el sistema sin un administrador activo.
- **Secretos**: nunca se devuelven `passwordHash` ni security stamps.

| Método | Ruta | Respuestas |
|--------|------|------------|
| `GET` | `/api/users?search=&status=&role=&sortBy=&sortDir=&page=&pageSize=` | **200** `PagedResult<UserListItemDto>` (`status` = `All`/`Active`/`Inactive`; `sortBy` = `name`/`username`/`email`/`created`/`status`) |
| `GET` | `/api/users/roles` | **200** `string[]` (`["Admin","Manager","User"]`) |
| `GET` | `/api/users/{id}` | **200** `UserDetailDto` (incl. `concurrencyStamp`) · **404** |
| `POST` | `/api/users` | **201** `UserDetailDto` · **400** · **409** |
| `PUT` | `/api/users/{id}` | **200** `UserDetailDto` · **400** · **404** · **409** |
| `POST` | `/api/users/{id}/activate` · `/deactivate` | **204** · **409** (self / último admin) |
| `PUT` | `/api/users/{id}/role` — body `{ "role": "Manager" }` | **204** · **400** (rol desconocido) · **409** |
| `DELETE` | `/api/users/{id}/role` — body `{ "role": "Admin" }` | **204** · **409** |
| `POST` | `/api/users/{id}/password` — body `{ "newPassword", "confirmPassword" }` | **204** · **400** · **404** |
| `DELETE` | `/api/users/{id}` | **204** · **404** · **409** |

Toda operación administrativa importante se audita en el log estructurado
(`USER_AUDIT action= actor= actorId= targetUserId= timestamp=`); nunca se
registran contraseñas ni tokens.
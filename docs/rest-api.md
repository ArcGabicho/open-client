# API REST

`ClientsController` (`core/Controllers/ClientsController.cs`) expone el catálogo
de clientes como JSON. Es la misma capa de servicio (`IClientService`) que
consume el panel Blazor; el panel la usa **en proceso**, no por HTTP.

- **Base**: `/api/clients`
- **Formato**: `application/json`
- **Autenticación**: cookie de sesión (`.OpenClient.Auth`). Todos los endpoints
  llevan `[Authorize]`; sin cookie válida → **302** a `/log-in` (navegador) o
  **401/403** según el cliente.
- **CancellationToken**: todas las acciones lo propagan.

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

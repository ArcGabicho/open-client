# Panel de Clientes

Cómo funciona `core/Components/Pages/Clients.razor` (ruta `/dashboard/clients`):
el módulo de gestión de la cartera comercial. Recorre la cadena **componente
Blazor → servicio → repositorio → EF Core → SQL Server**, con alta, edición y
borrado lógico reales, y está pensada para operar sobre la tabla de ~4018
clientes sin cargarla en memoria.

> Es uno de los cuatro módulos del panel `/dashboard`. Los otros son
> **Integraciones** (`/dashboard/integrations` + API `/api/v1`, ver
> `rest-api.md`), **Analíticas** (`/dashboard/analytics`) y **Usuarios**
> (`/dashboard/users`). `/dashboard` (`Dashboard.razor`) es la portada del panel.
> El sitio público (`MainLayout` + Index / About / Docs / Contact) y el inicio de
> sesión completan la superficie visible.

---

## 1. Identidad de la página

| Aspecto | Valor |
| --- | --- |
| Componente | `core/Components/Pages/Clients.razor` |
| Ruta | `/dashboard/clients` (`@page`) |
| Layout | `DashboardLayout` (barra lateral con los cuatro módulos + perfil real del usuario autenticado + cerrar sesión) |
| Autorización | `@attribute [Authorize]` — requiere sesión con cookie |
| Render mode | `@rendermode InteractiveServer` (interactividad opt-in) |
| Ciclo de vida | `@implements IDisposable` (libera el debounce de búsqueda) |
| Dependencia | `@inject IClientService ClientService` |

El componente no habla con EF Core: todo pasa por `IClientService`
(`OpenClient.Interfaces`), implementado por `ClientService`
(`OpenClient.Services`) y registrado en `ServiceExtensions.AddApplicationServices`
como `AddScoped<IClientService, ClientService>()`.

---

## 2. Arquitectura de datos

```
Clients.razor ─► IClientService ─► IClientRepository ─► IDbContextFactory<OpenClientDbContext> ─► SQL Server
   (UI)          (caso de uso)      (acceso a datos)      (un contexto por operación)

ClientsController (api/clients) ─► IClientService   (misma capa; el panel Blazor NO pasa por HTTP)
```

- **`ClientService`** orquesta: valida con FluentValidation, mapea entidad↔DTO y
  registra eventos con Serilog. No conoce EF Core.
- **`ClientRepository`** traduce a SQL. Cada método abre su propio
  `OpenClientDbContext` con `IDbContextFactory.CreateDbContextAsync(ct)` y lo
  libera (`await using`), por lo que es seguro frente a la concurrencia de un
  circuito Blazor. Todas las lecturas filtran `!IsDeleted`.
- **`ClientsController`** (`[Authorize]`) expone la misma lógica como REST. El
  panel inyecta el servicio en proceso y evita el salto HTTP.

### DTOs y modelos (`core/Models/DTO/`)

| Tipo | Rol |
| --- | --- |
| `ClientSearchFilter` | Entrada de consulta: `Search`, `Industry`, `SortBy` (`recent`/`name`/`oldest`), `Page`, `PageSize`. Setters autocorrectores: `Page` ≥ 1; `PageSize` ∈ {10,25,50,100} (por defecto **10**). |
| `ClientListItemDto` | Proyección de lectura. Campos crudos + `UpdatedAt` + `FromEntity(Client)` + cadenas derivadas: `DisplayName`, `ContactName`, `Location`, `Initials`. |
| `ClientEditModel` | Datos editables del formulario Blazor. `FromDto(ClientListItemDto)` lo rellena a partir de una fila. |
| `CreateClientDto` / `UpdateClientDto` | Cuerpos JSON del controlador REST (`ToEditModel()` los adapta al servicio). |
| `ClientDetailDto` | Respuesta de `GET /api/clients/{id}` (`FromListItem(...)`). |
| `PagedResult<T>` | `Items`, `Page`, `PageSize`, `TotalCount` + calculados `TotalPages`, `FirstItemIndex`, `LastItemIndex`, `HasPrevious`, `HasNext`. |

### Contrato `IClientService`

| Método | Qué hace |
| --- | --- |
| `GetClientsAsync(filter, ct)` | Pide `repository.GetPagedAsync(filter)` (`(IReadOnlyList<Client>, int)`) y proyecta a `PagedResult<ClientListItemDto>` con `ClientListItemDto.FromEntity`. |
| `GetByIdAsync(id, ct)` | `repository.GetByIdAsync` → `ClientListItemDto?` (excluye borrados). |
| `GetIndustriesAsync(ct)` | `repository.GetRawIndustriesAsync()` (SELECT DISTINCT en SQL) y **normaliza en memoria**: recorta, descarta vacíos, colapsa duplicados por mayúsculas/espacios (`Distinct(OrdinalIgnoreCase)`), ordena. |
| `CreateAsync(model, ct)` | `_validator.ValidateAndThrowAsync(model)` → `new Client { CreatedAt = UtcNow }` + `Apply` + `repository.AddAsync`. Lanza `ValidationException` si el modelo no es válido. |
| `UpdateAsync(id, model, ct)` | `ValidateAndThrowAsync` → `repository.UpdateAsync(id, apply)` donde `apply` ejecuta `Apply(model, entity)` y fija `entity.UpdatedAt = UtcNow`. Devuelve `false` si no existe. |
| `DeleteAsync(id, ct)` | `repository.SoftDeleteAsync(id)` → marca `IsDeleted = true`, `DeletedAt = UtcNow`. Devuelve `false` si no existe. |

`Apply` copia los 13 campos de `ClientEditModel` a la entidad pasándolos por
`Clean` (`null` si vacío, `Trim()` si no). El filtro de industria del repositorio
compara `client.Industry.Trim() == value` para tolerar espacios en datos
importados.

### Validación (`core/Models/Validators/`)

`ClientEditModelValidator` (FluentValidation): `CompanyName` obligatorio y ≤ 100;
`Email` con formato; `TaxId` = 11 dígitos; `Website` URL http/https absoluta;
longitudes máximas alineadas con `ClientConfiguration`. Se registra con
`AddValidatorsFromAssemblyContaining<ClientEditModelValidator>()` y `ClientService`
lo inyecta como `IValidator<ClientEditModel>`. Un fallo lanza
`FluentValidation.ValidationException`; el controlador lo convierte en **400** con
los errores por campo, y `SaveFormAsync` del panel lo captura y lo muestra en
`formError`. (`ClientSearchFilterValidator` existe para el controlador pero no se
engancha automáticamente: los setters del filtro ya autocorrigen.)

---

## 3. Estado del componente (`@code`)

```csharp
// Consulta
private readonly ClientSearchFilter filter = new() { PageSize = 10 };
private PagedResult<ClientListItemDto>? result;   // null = aún cargando la 1.ª vez
private IReadOnlyList<string> industries = [];
private string viewMode = "list";                 // "list" | "grid"
private bool loading;                              // atenúa la tarjeta durante la recarga
private int? expandedId;                           // acordeón abierto (uno a la vez)
private CancellationTokenSource? searchDebounce;   // antirrebote del buscador

// Modales
private bool showForm;                             // modal crear/editar
private int? editingId;                            // null = crear; con valor = editar
private ClientEditModel form = new();
private string? formError;
private bool saving;
private ClientListItemDto? detailClient;           // modal "Ver ficha"
private ClientListItemDto? deleteTarget;           // modal de confirmación de borrado
private bool deleting;
```

---

## 4. Ciclo de vida y recarga

- **`OnInitializedAsync`** → `industries = GetIndustriesAsync()` y luego
  `LoadAsync()`. Con prerenderizado se ejecuta dos veces (prerender + conexión
  interactiva); es aceptable y consistente con el resto del panel.
- **`LoadAsync`** → `loading = true`, cierra el acordeón (`expandedId = null`),
  `StateHasChanged()`, y `result = await ClientService.GetClientsAsync(filter)`.
  `loading` vuelve a `false` en el `finally`.

Toda interacción que cambie el conjunto de datos termina llamando a `LoadAsync()`.

### Estructura de la página

1. **Cabecera** (`.clients-head`): título, contador (`CountLabel`) y botón
   *Nuevo cliente*.
2. **Franja de resumen** (`.stat-row`): tres tarjetas — *Total de clientes*
   (`result.TotalCount`), *Industrias* (`industries.Count`) y *En esta página*
   (`result.Items.Count` de `result.TotalCount`). Datos ya cargados, sin
   consultas extra.
3. **Barra de controles** (`.clients-controls`): buscador, orden, filtro de
   industria y conmutador lista/cuadrícula.
4. **Tarjeta principal** (`.clients-card`): acordeón o cuadrícula + paginador.
5. **Modales**: crear/editar, ver ficha y confirmar borrado.

---

## 5. Interacciones

### Búsqueda (antirrebote)

`<input @oninput="OnSearchInput">` → escribe `filter.Search`, `filter.Page = 1`,
cancela el `CancellationTokenSource` anterior, crea uno nuevo y espera
`Task.Delay(350, token)`. Si el usuario sigue tecleando, el `Delay` lanza
`TaskCanceledException` (se ignora); si no, dispara `LoadAsync()`. El token se
libera en `Dispose()`.

### Orden e industria

`<select @bind="filter.SortBy" @bind:after="ApplyFilterAsync">` y el equivalente
para `filter.Industry`. `ApplyFilterAsync` fija `filter.Page = 1` y recarga. El
desplegable de industria tiene `<option value="">Industria</option>` como "todas".

### Cambio de vista

`.view-toggle` alterna `viewMode` entre `"list"` (acordeón) y `"grid"`
(tarjetas). No recarga: solo cambia el render de `result.Items`.

### Paginación

Tamaño fijo en **10**. `PageItems()` produce la lista de botones: ≤ 7 páginas →
todas; si hay más → `1 … n-1 n n+1 … último` (con `null` = elipsis). `GoTo(page)`
acota con `Math.Clamp(1, TotalPages)` y recarga. Las flechas ‹ › usan
`result.HasPrevious`/`HasNext`; la leyenda usa `FirstItemIndex`, `LastItemIndex`,
`TotalCount`.

### Acordeón (vista lista)

Cada fila es un `<button class="acc-summary">` con cuatro columnas siempre
visibles — **Cliente** (avatar + `DisplayName`), **Contacto**, **Cargo**,
**Alta** — y un chevron. `Toggle(id)` abre/cierra; abrir una fila cierra la
anterior (`expandedId` es un único `int?`).

El panel desplegado (`.acc-panel-inner`) muestra los campos extendidos en rejilla
etiqueta/valor: Razón social, RUC (monoespaciado), Industria (chip de color),
Correo (`mailto:`), Teléfono (`tel:`), Sitio web (enlace externo), Ubicación,
Dirección. Los valores ausentes se muestran como *"Sin correo / Sin teléfono / …"*.
Debajo, tres acciones: **Ver ficha**, **Editar**, **Eliminar**.

### Acciones y modales

Todos los modales comparten patrón: `.modal-overlay` con `@onclick="CloseModals"`
y un `.modal` interior con `@onclick:stopPropagation="true"` (clic fuera cierra,
clic dentro no). `CloseModals()` limpia `showForm`, `detailClient` y
`deleteTarget`.

| Acción | Método | Comportamiento |
| --- | --- | --- |
| **Nuevo cliente** | `OpenCreate` | `form = new()`, `editingId = null`, abre el modal de formulario (13 campos, Industria con `<datalist>`). |
| **Editar** (acordeón o modal de ficha) | `OpenEdit(c)` | `form = ClientEditModel.FromDto(c)`, `editingId = c.Id`. |
| Guardar formulario | `SaveFormAsync` | `CompanyName` no vacío (chequeo rápido; la validación completa la hace `ClientService`); `editingId` → `UpdateAsync`, si no → `CreateAsync`; cierra y `LoadAsync()`. `ValidationException`/errores → `formError`. `saving` desactiva los botones. |
| **Ver ficha** | `OpenDetail(c)` | Modal de solo lectura con el registro completo (incluye Contacto/Cargo/Alta). Atajos a **Editar** y **Eliminar**. |
| **Eliminar** | `OpenDelete(c)` → `ConfirmDeleteAsync` | `OpenDelete` fija `deleteTarget` (y cierra el modal de ficha). El modal de confirmación (`role="alertdialog"`) llama a `ClientService.DeleteAsync(id)` (borrado lógico), limpia `deleteTarget` y `LoadAsync()`. `deleting` desactiva los botones. |

La vista de cuadrícula reutiliza `OpenDetail` y `OpenDelete` en sus botones
"Ver" / "Eliminar".

---

## 6. Helpers de presentación

| Helper | Uso |
| --- | --- |
| `Dash(value)` | `"—"` si el string está vacío. |
| `WebDisplay(url)` | Quita `http(s)://` y la barra final. |
| `TelHref(phone)` | `tel:` conservando solo dígitos y `+`. |
| `ChipClass(seed)` | Clase de color determinista (`chip-a…chip-f`) por suma de caracteres → mismo color para la misma industria entre recargas. |
| `AvatarColor(seed)` | Igual, sobre una paleta de 6 hex, aplicado inline al avatar. |
| `Initials` (en el DTO) | Iniciales de `DisplayName` (1–2 letras). |
| `CountLabel` | `"{TotalCount:N0} clientes en total"` o `"Cargando…"`. |

---

## 7. Estilos (`Clients.razor.css`, CSS aislado)

- Paleta y tokens sobre `.clients` (clase raíz), no en `:root` — ver la nota de
  aislamiento en `CONTRIBUTING.md`.
- **Franja de resumen**: `.stat-row` es una rejilla de 3 → 2 → 1 columnas; cada
  `.stat-card` es icono + etiqueta + valor.
- La fila abierta del acordeón (`.acc-item.is-open`) lleva una barra de acento
  a la izquierda (`box-shadow: inset 3px 0 0 var(--accent)`).
- El estado "Sin resultados" (`.empty-state`) incluye un icono.
- **Acordeón**: `.acc-head` y `.acc-summary` comparten `grid-template-columns`;
  toda pista flexible es `minmax(0, …)`. Bajo 820 px el resumen se apila y cada
  celda muestra su etiqueta (`data-label` + `::before`).
- **Sin scroll horizontal**: `min-width: 0`, `overflow-wrap: anywhere`,
  `minmax(min(…, 100%), 1fr)` en las rejillas, `.clients-card` con `overflow: hidden`.
- El `<select>` de industria está acotado a `max-width: 14rem` con elipsis.
- **Modales**: `.mbtn-danger` para acciones destructivas; el modal de
  confirmación usa `.modal-sm` y `.modal-confirm`.
- **Breakpoints**: paginador en columna ≤ 900 px; controles ≤ 720 px;
  cabecera/filtros a ancho completo y rejilla a 1 columna ≤ 560 px; formulario
  del modal a 1 columna ≤ 560 px.

---

## 8. Limitaciones conocidas

- **Validación de búsqueda**: `ClientSearchFilterValidator` no se ejecuta
  automáticamente en el controlador (no se añadió `FluentValidation.AspNetCore`).
  Los setters de `ClientSearchFilter` ya autocorrigen valores fuera de rango.
- **Borrado**: solo lógico (`IsDeleted`). No hay purga física ni pantalla para
  restaurar clientes borrados (se hace en base de datos).
- **Concurrencia de edición**: sin control optimista (`RowVersion`); la última
  escritura gana.

---

## 9. Cómo extender

**Añadir un campo visible/editable:**

1. Propiedad en `Models/Domain/Client.cs` + regla en `Data/Configurations/ClientConfiguration.cs`.
2. Migración: `dotnet ef migrations add NombreDescriptivo --project core/openclient.csproj --output-dir Data/Migrations`.
3. `init` en `ClientListItemDto` + línea en `ClientListItemDto.FromEntity`.
4. Si es editable: campo en `ClientEditModel`, línea en `ClientService.Apply`,
   regla en `ClientEditModelValidator`, campo en `CreateClientDto`/`UpdateClientDto`
   (+ `ToEditModel`), y `<label class="mfield">` en el formulario.
5. Render en el panel del acordeón y en el modal de ficha.

**Añadir un filtro:** propiedad en `ClientSearchFilter`, cláusula en
`ClientRepository.GetPagedAsync` (helper `ApplyXxx`), y control `@bind` +
`@bind:after="ApplyFilterAsync"` en `.clients-filters`.
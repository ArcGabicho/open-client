# Guía de Contribución

Gracias por tu interés en **Open Client**, un CRM y motor de datos comercial
100 % open source y self-hosted construido con .NET 10, Blazor Web App, EF Core
y SQL Server. Este documento resume cómo proponer cambios de forma que sean
fáciles de revisar y de integrar.

El proyecto se publica bajo licencia **MIT** (ver `LICENSE.md`). Al enviar una
contribución aceptas que se distribuya bajo esa misma licencia.

---

## 1. Antes de empezar

- **Alcance del producto**: la superficie visible se limita a tres cosas — el
  **sitio público** (`MainLayout` + Index / About / Docs / Contact), el
  **inicio de sesión** (`/log-in`) y el **panel de clientes** (`/dashboard`,
  `Dashboard.razor`). No añadas nuevas páginas de aplicación sin acordarlo antes
  en una Issue.
- **Idioma**: el código, la interfaz, los comentarios, la documentación y los
  mensajes de commit están en **español**. Mantén ese idioma en tus aportes.
- **Rama principal**: `master`. Todo el trabajo parte de `master` y vuelve a
  `master` mediante Pull Request.
- **Documentación de referencia** (carpeta `docs/`):
  - `docs/app-overview.md` — perfil del proyecto, stack y estructura.
  - `docs/architecture.md` — capas, `IDbContextFactory`, repositorio, validación, logging.
  - `docs/development-guide.md` — entorno local, estructura y verificación.
  - `docs/database-guide.md` / `docs/database-initialization.md` — esquema, seed y `DbInitializer`.
  - `docs/authentication.md` — modelo de autenticación (cookies + BCrypt).
  - `docs/clients-panel.md` — funcionamiento del panel de clientes (`Dashboard.razor`).
  - `docs/rest-api.md` — endpoints REST de `/api/clients`.
  - `docs/docker-guide.md` / `docs/bash-scripts-guide.md` — contenedores y scripts.

Lee `docs/development-guide.md` antes de tu primer cambio.

---

## 2. Reportar bugs y proponer mejoras

Usa las **Issues** de GitHub: <https://github.com/ArcGabicho/open-client/issues>

Para un bug incluye:

- Pasos exactos para reproducirlo.
- Comportamiento esperado vs. observado.
- Entorno: SO, versión de .NET (`dotnet --list-sdks`), forma de arranque
  (`./scripts/run.sh`, `--full`, etc.).
- Logs relevantes (sin contraseñas, hashes, cookies ni cadenas de conexión).

Para una mejora, describe el caso de uso y el problema que resuelve antes de
entrar en la solución técnica. Para cambios grandes, abre primero una Issue de
discusión: evita trabajo que luego no encaje con la dirección del proyecto.

---

## 3. Entorno de desarrollo

Requisitos: **.NET SDK 10**, **Docker + Docker Compose v2**, **Bash**.

```bash
git clone https://github.com/ArcGabicho/open-client.git
cd open-client
cp .env.example .env          # primera vez; ajusta los valores
./scripts/run.sh              # SQL Server en Docker + init + app en http://localhost:5000
```

Otros modos útiles:

```bash
./scripts/run.sh --full      # stack completo en Docker (app en :8080)
./scripts/run.sh --stop      # detener contenedores
./scripts/run.sh --logs      # logs de SQL Server
./scripts/clear.sh           # limpiar artefactos y contenedores
```

Al iniciar, la app ejecuta `DbInitializer`: aplica migraciones EF Core, crea el
usuario administrador (BCrypt) y hace el seed de clientes de muestra si la tabla
está vacía. **La segunda ejecución debe ser idempotente** (1 admin, sin
clientes duplicados).

---

## 4. Estructura del código

```
core/
├── Components/
│   ├── Layout/      # Layouts (MainLayout público, DashboardLayout, LoginLayout)
│   └── Pages/       # Páginas .razor (+ su .razor.css con CSS aislado)
├── Controllers/     # API REST JSON (ControllerBase, [Route("api/...")])
├── Data/
│   ├── Context/           # OpenClientDbContext + IDesignTimeDbContextFactory
│   ├── Configurations/    # IEntityTypeConfiguration<T> (Fluent API)
│   ├── Migrations/        # Migraciones EF Core (nunca se editan a mano)
│   ├── Repositories/      # IClientRepository + ClientRepository (acceso a datos)
│   ├── Seeds/             # DbInitializer, DbSeeder, ClientSeedData
│   └── DbHealthCheck.cs   # Comprobación de salud de la BD
├── Extensions/     # ServiceExtensions (composición de la DI)
├── Interfaces/      # Contratos (IClientService, IAuthService, IDbInitializer)
├── Models/
│   ├── Domain/      # Entidades EF (Client, User)
│   ├── DTO/         # DTOs de entrada/salida y modelos de vista
│   └── Validators/  # Validadores de FluentValidation
├── Services/        # Casos de uso (ClientService, AuthService)
└── Program.cs       # Entrypoint: Serilog + ServiceExtensions + pipeline
```

Convenciones de organización:

- **Namespaces**: `OpenClient.<Carpeta>` (p. ej. `OpenClient.Services`,
  `OpenClient.Models.DTO`). Usa `namespace` con ámbito de archivo (file-scoped).
- Un servicio nuevo se declara con **interfaz** en `Interfaces/`, se implementa
  en `Services/` (clase `sealed`) y se registra en
  `Extensions/ServiceExtensions.cs` (`AddScoped<IFoo, Foo>()`), no en `Program.cs`.
- **Capas**: la UI (Blazor y controladores) depende solo de interfaces de
  servicio; los servicios dependen del repositorio (interfaz) y de
  `IValidator<T>`, y **no** referencian EF Core; solo el repositorio
  (`Data/Repositories/`) usa `IDbContextFactory<OpenClientDbContext>`.
- Los DTO viven en `Models/DTO/`; las entidades de dominio no salen de la capa
  de servicios sin proyectarse a un DTO.

Ver `docs/architecture.md` para el detalle de las capas.

---

## 5. Estilo de código

### C#

- `Nullable` e `ImplicitUsings` están **activados**; no añadas `using`
  redundantes ni ignores advertencias de nulabilidad.
- Métodos asíncronos con sufijo `Async` y `CancellationToken` como último
  parámetro cuando aplique.
- Sigue el formato existente (4 espacios, llaves en línea nueva). Antes de
  commitear: `dotnet format core/openclient.csproj` si tienes la herramienta,
  o al menos respeta el estilo de los archivos vecinos.

### Acceso a datos (EF Core)

- **`IDbContextFactory<OpenClientDbContext>`**, no `DbContext` scoped. Cada
  operación abre y libera su propio contexto
  (`await using var db = await _factory.CreateDbContextAsync(ct);`). Motivo: un
  circuito Blazor es de larga duración y no es seguro compartir un contexto.
- El acceso a datos vive en `Data/Repositories/`. Los servicios no consultan EF
  Core directamente.
- Lecturas siempre con `AsNoTracking()`; proyecta a DTO (columnas escalares) o
  devuelve entidades desde el repositorio y proyecta en el servicio.
- Paginación, filtrado y orden se resuelven **en la base de datos**
  (`Where` + `Skip`/`Take` + `OrderBy`), nunca cargando todo en memoria.
- Toda lectura de clientes excluye el borrado lógico (`!IsDeleted`),
  **explícitamente** en cada consulta (no hay `HasQueryFilter` global).
- Para actualizar, carga la entidad rastreada y muta solo los campos editables;
  no uses `SetValues` con un objeto nuevo (pisaría `Id`/`CreatedAt`/`IsDeleted`).
- **Nunca** `EnsureCreated()`: todo el esquema se gestiona con migraciones.

### Validación (FluentValidation)

- Reglas de servidor en `Models/Validators/` (`AbstractValidator<T>`),
  registradas con `AddValidatorsFromAssemblyContaining<...>()`.
- Los servicios llaman a `ValidateAndThrowAsync` en las operaciones de escritura.
- El controlador traduce `ValidationException` a **HTTP 400** con errores por
  campo; el panel Blazor lo captura y lo muestra en el formulario.
- No confíes en la validación del cliente: la de servidor es la que manda.

### Blazor / UI

- **Render modes**: SSR estático por defecto; la interactividad es opt-in por
  página (`@rendermode InteractiveServer`). No reintroducir un modo interactivo
  global.
- **CSS aislado**: cada componente lleva su `*.razor.css`. El selector `:root`
  **no sobrevive** a la reescritura de aislamiento de Blazor, así que la paleta
  y los tokens se declaran sobre la **clase raíz del componente**
  (`.dashboard-shell`, `.clients`, `.welcome`, …), no en `:root`.
- Reutiliza la paleta y los tokens compartidos (tema claro):

  ```css
  --bg: #ffffff;          --bg-soft: #f5f6fa;      --bg-softer: #fafbfd;
  --ink-panel: #14151b;   --text: #14151a;        --text-soft: #565b67;
  --text-mute: #8b909c;   --border: #ebecf1;      --border-strong: #dcdee6;
  --accent: #635bff;      --accent-hover: #4b43e6; --accent-soft: #f0efff;
  --radius-sm: 10px;      --radius: 16px;          --radius-lg: 26px;
  --ring: 0 0 0 4px rgba(99, 91, 255, 0.16);
  --shadow-1: 0 1px 2px rgba(20,21,26,.04), 0 1px 3px rgba(20,21,26,.06);
  --shadow-2: 0 18px 44px -28px rgba(20,21,26,.22);
  ```

- Diseño responsive: nada debe generar scroll horizontal; usa `min-width: 0`,
  `overflow-wrap: anywhere` y `minmax(min(…, 100%), 1fr)` en las rejillas.
- Para llegar a los elementos que renderiza `<NavLink>` u otros componentes
  hijos desde CSS aislado, usa `::deep` desde un wrapper propio.

### Seguridad y operación

- **Secretos** solo en `.env` (fuera de Git). Las cadenas de conexión llegan
  por variable de entorno; no se hardcodean en `appsettings*.json`.
- **Autenticación**: un único mecanismo (formularios HTTP + cookies). No usar
  JS interop ni `fetch` para login/logout.
- **Logging** con `ILogger<T>` (el host usa **Serilog**: consola + fichero
  rotado en `logs/`). Registra altas, ediciones, borrados y eventos de
  autenticación; nunca contraseñas, hashes, cookies ni secretos. Captura y
  registra la excepción real (`LogError`) y muestra al usuario mensajes
  genéricos.
- **Health checks**: `GET /health` y `GET /health/ready` (este último solo las
  comprobaciones etiquetadas `ready`, hoy la conectividad con SQL Server).

---

## 6. Migraciones EF Core

Si tu cambio toca el esquema (`Models/Domain`, `Data/Configurations`):

```bash
export PATH="$PATH:$HOME/.dotnet/tools"
dotnet ef migrations add NombreDescriptivo \
  --project core/openclient.csproj --output-dir Data/Migrations
```

- Incluye el archivo de migración generado en el mismo commit que el cambio de
  modelo.
- No edites migraciones ya publicadas; crea una nueva.
- La app aplica `Database.MigrateAsync()` al arrancar (vía `DbInitializer`), así
  que normalmente no hace falta `dotnet ef database update` a mano.
- `dotnet ef` usa `Data/Context/OpenClientDbContextFactory.cs`
  (`IDesignTimeDbContextFactory`), así que las herramientas funcionan aunque en
  runtime solo se registre `AddDbContextFactory`.
- Si añades un campo a una entidad que se muestra en el panel, actualiza también
  su DTO (`Models/DTO/*`), el mapeo (`FromEntity`/`ToEditModel`), el validador y
  la proyección del repositorio.

---

## 7. Flujo de trabajo con Git

1. Haz fork del repositorio (o crea una rama si tienes acceso de escritura).
2. Crea una rama descriptiva desde `master`:
   `feat/exportar-clientes`, `fix/select-industria-desbordado`, `docs/contribuir`.
3. Haz commits pequeños y enfocados.
4. Sincroniza con `master` antes de abrir el PR (`git pull --rebase origin master`).

### Mensajes de commit

Formato del proyecto: **`tipo: descripción en minúsculas`**, en español,
normalmente con gerundio o infinitivo.

```
feat: agregando exportación de clientes a CSV
fix: corrigiendo desbordamiento del select de industria
migrate: añadiendo campo notas a la entidad Client
docs: documentando el flujo de contribución
```

Tipos habituales: `init`, `add`, `feat`, `fix`, `delete`, `migrate`,
`design`, `docs`, `refactor`, `chore`. Un tipo por commit; si un cambio mezcla
varios propósitos, divídelo.

---

## 8. Pull Requests

Antes de abrir el PR, verifica en local:

```bash
dotnet build core/openclient.csproj      # 0 advertencias, 0 errores
./scripts/run.sh                         # la app arranca; 2ª ejecución idempotente
```

Checklist funcional mínimo (según `docs/development-guide.md`):

- [ ] `/log-in` carga sin errores de consola.
- [ ] Login incorrecto muestra un mensaje genérico; login correcto llega a `/dashboard`.
- [ ] `/dashboard` anónimo redirige a `/log-in?ReturnUrl=%2Fdashboard`.
- [ ] Logout lleva a `/log-in` y luego `/dashboard` vuelve a exigir login.
- [ ] La segunda ejecución de `run.sh` no duplica datos (1 admin, ~4018 clientes).
- [ ] Si tocaste una vista, no aparece scroll horizontal en desktop, tablet ni móvil.

En la descripción del PR:

- Enlaza la Issue relacionada (`Closes #123`).
- Explica **qué** cambia y **por qué**; incluye capturas si el cambio es visual.
- Lista cualquier migración nueva o variable de entorno añadida.
- Mantén el PR acotado a un solo tema. Los cambios de formato masivos van en su
  propio PR.
- Actualiza la documentación de `docs/` y el `README.md` si el comportamiento
  observable cambia.

---

## 9. Tests

Todavía no hay proyecto de pruebas automatizadas; la verificación es manual con
el checklist anterior. Las contribuciones que añadan un proyecto de tests
(`core.Tests/` con xUnit) para los servicios (`ClientService`, `AuthService`)
son bienvenidas y se revisarán con prioridad.

---

## 10. ¿Dudas?

Abre una Issue con la etiqueta `question` o comenta en la Issue relacionada.
Toda contribución de buena fe es bienvenida: código, documentación, reportes de
bugs reproducibles o mejoras de la experiencia de despliegue.
# Autenticación — Open Client

Este documento describe el comportamiento **real** del sistema de autenticación
después del rediseño de agosto de 2026: arquitectura, flujos de login/logout,
cookies, claims, autorización, antiforgery y troubleshooting.

---

## 1. Arquitectura

```
Login.razor (SSR estático, sin circuito)
   |
   | <form method="post" action="/auth/log-in">   <-- HTML nativo, data-enhance="false"
   |      + <AntiforgeryToken />                   <-- token antiforgery en campo oculto
   v
AuthController [HttpPost("log-in")]               <-- HTTP POST tradicional (PRG)
   |-- IAntiforgery.ValidateRequestAsync()        <-- valida token+cookie
   |-- AuthService.ValidateCredentialsAsync()     <-- busca usuario activo + BCrypt.Verify
   |-- ClaimsIdentity + ClaimsPrincipal           <-- claims: Id, Name, Email, GivenName, Surname, Role
   |-- HttpContext.SignInAsync(Cookie)            <-- Set-Cookie .OpenClient.Auth
   |-- AuthService.RecordSuccessfulLoginAsync()   <-- sella Users.LastLoginAt
   v
302 -> /dashboard  (o returnUrl si es local)
```

Principios aplicados:

* **Un solo mecanismo de autenticación**: formularios HTTP tradicionales +
  Cookie Authentication de ASP.NET Core. No hay `fetch`, ni JS interop, ni
  `HttpClient` manual para login/logout.
* **El login no participa en un circuito Blazor.** La página `/log-in` se
  renderiza como SSR estático; `SignInAsync` escribe la cookie sobre una
  respuesta HTTP real.
* **El logout no pasa por el circuito**: es un enlace HTML nativo
  (`data-enhance-nav="false"`); la navegación la ejecuta el navegador y el
  circuito se destruye limpiamente en el unload.

### ¿Por qué se eliminó el flujo anterior?

| Problema anterior | Causa raíz | Solución actual |
|---|---|---|
| `POST /api/auth/log-in → 400` / "A valid antiforgery token was not provided" | `Login.razor` llamaba a `auth.js` (JS interop) que hacía `fetch` sin token antiforgery | Formulario HTML nativo con `<AntiforgeryToken />`; validación explícita con `IAntiforgery` |
| `BCrypt.Net.SaltParseException: Invalid salt version` | Se verificaban hashes que no eran BCrypt | `DbInitializer` genera hashes con `BCrypt.Net.BCrypt.HashPassword(password, 12)`; `AuthService` captura la excepcion y rechaza el acceso sin tumbar el endpoint |
| `WebSocket is not in the OPEN state` al hacer logout | Logout dentro del circuito (fetch + `window.location`) mataba el WebSocket mientras el circuito intentaba seguir ejecutando JS | Logout por navegación HTTP forzada fuera del circuito |
| Dos sistemas de autenticación simultáneos (`auth.js` + API) | Diseño híbrido innecesario | `auth.js` eliminado; un solo flujo nativo |

---

## 2. Flujo de login

1. El usuario visita `/dashboard` sin sesión → el middleware de cookies
   redirige a `/log-in?ReturnUrl=%2Fdashboard`.
2. `Login.razor` (**SSR estático**) renderiza el formulario con:
   * campo oculto `__RequestVerificationToken` (`<AntiforgeryToken />`),
   * campo oculto `returnUrl` (para conservar el destino),
   * validaciones HTML5 (`required`, `type=email`) y validación de modelo
     server-side (`DataAnnotations` en `LoginModel`).
3. El navegador hace `POST /auth/log-in` (`application/x-www-form-urlencoded`,
   `data-enhance="false"` para que Blazor no lo intercepte).
4. `AuthController.Login`:
   1. valida el token antiforgery con `IAntiforgery.ValidateRequestAsync`;
      si falla → redirect a `/log-in?error=form_expired`.
   2. valida el modelo; inválido → `/log-in?error=invalid_input`.
   3. `AuthService.ValidateCredentialsAsync`: busca usuario **activo** por
      email y verifica la contraseña con `BCrypt.Verify(password, hash)`.
      Falla → `/log-in?error=invalid_credentials` (mensaje genérico, no
      revela si existió el usuario).
   4. crea claims (`NameIdentifier=Id`, `Name/Email`, `Role`),
      `ClaimsIdentity` con esquema `Cookies`, y ejecuta
      `HttpContext.SignInAsync(...)` → cookie emitida.
   5. responde `302 LocalRedirect(returnUrl)` si `returnUrl` es local
      (`Url.IsLocalUrl`, bloquea open redirect), o `/dashboard`.
5. El navegador carga `/dashboard` ya autenticado.

### Mensajes de error

La página muestra mensajes fijos según el código recibido
(`error=invalid_credentials|invalid_input|form_expired`). Nunca se refleja
texto libre del query string.

## 3. Flujo de logout

1. `DashboardLayout.razor` renderiza un **enlace nativo**:
   `<a href="/auth/log-out" data-enhance-nav="false">Cerrar sesión</a>`.
   El click lo gestiona el navegador directamente: no hay `@onclick`, ni
   JS interop, ni ningún mensaje enviado por el WebSocket del circuito.
2. La navegación HTTP completa (`GET /auth/log-out`) destruye el circuito
   de forma limpia durante el unload — blazor.web.js solo ejecuta su cierre
   normal (`close()`), sin intentos de envío sobre un socket en cierre.
3. `AuthController.Logout` ejecuta `SignOutAsync` (la cookie se emite expirada)
   y responde `302 /log-in`.

> Nota: una versión anterior usaba `Navigation.NavigateTo(..., forceLoad: true)`
> dentro de un `@onclick`. Funciona, pero la orden de navegación viaja por JS
> interop sobre el propio WebSocket que se está cerrando, y puede producir
> `Uncaught (in promise) WebSocket is not in the OPEN state` en consola.
> Con el enlace nativo el error desaparece por completo.

### ¿Por qué `data-enhance-nav="false"`?

blazor.web.js intercepta los clics en enlaces same-origin para hacer
navegación mejorada (fetch + swap del DOM). Sin ese atributo, al pulsar el
enlace Blazor mantendría vivo el circuito del dashboard mientras muestra
`/log-in` — exactamente lo que un logout no debe hacer.

### ¿Por qué GET y no POST?

* `<AntiforgeryToken />` solo es fiable durante SSR con `HttpContext` real;
  dentro de un circuito interactivo el token puede no estar disponible tras
  navegaciones client-side y el POST fallaría con 400.
* El riesgo residual de logout-CSRF se acepta documentadamente: el peor caso
  es cerrar la sesión de otro usuario (molestia, sin escalación de
  privilegios). El destino del redirect está fijado a `/log-in` y el endpoint
  es idempotente.

## 4. Cookies

Configuradas en `Program.cs`:

| Propiedad | Valor | Motivo |
|---|---|---|
| Nombre | `.OpenClient.Auth` | Identificable en herramientas de desarrollo |
| `HttpOnly` | `true` | Inaccesible desde JavaScript |
| `SameSite` | `Lax` | Se envía en navegaciones top-level (necesario para redirects de login); bloquea envío cross-site en la mayoría de casos CSRF |
| `SecurePolicy` | `SameAsRequest` | Funciona en `http://localhost:5000` (Development) y exige `Secure` automáticamente cuando el tráfico llega por HTTPS (producción) |
| `ExpireTimeSpan` | 8 horas | Ventana razonable para un panel administrativo |
| `SlidingExpiration` | `true` | La sesión se renueva mientras haya actividad |
| `IsPersistent` (en `SignInAsync`) | `false` | Cookie de sesión: muere al cerrar el navegador |

## 5. Claims y autorización

Claims emitidos por `AuthService.CreateClaims`: `ClaimTypes.NameIdentifier` (Id
numérico), `ClaimTypes.Name` y `ClaimTypes.Email` (correo),
`ClaimTypes.GivenName` / `ClaimTypes.Surname` (nombre y apellido, usados por
`DashboardLayout` para el pie del panel) y `ClaimTypes.Role`.

**Roles**: el sistema aprovisiona `Admin`. El módulo de Usuarios reconoce el
conjunto cerrado `{Admin, Manager, User}` (`UserRoles.All`) y valida contra él
toda asignación. `Program.cs` registra dos políticas:

| Política | Requisito | Uso |
|---|---|---|
| `ApiV1.Read` | autenticado + rol en `{Admin, Integrations}` | endpoints `/api/v1/*` |
| `Users.Admin` | autenticado + rol `Admin` | `UsersController` y la página `/dashboard/users` |

Las páginas protegidas usan `@attribute [Authorize]` (enforced por el middleware
de autorización en la navegación inicial del componente). Un anónimo que visita
`/dashboard` recibe `302 → /log-in?ReturnUrl=%2Fdashboard`; tras loguearse, el
`returnUrl` local lo devuelve.

`AccessDeniedPath = "/access-denied"` está configurado y la página existe
(`Pages/AccessDenied.razor`).

### Peticiones a `/api/*`

Los eventos de la cookie distinguen las rutas de API: bajo `/api` una petición
no autenticada recibe **401** y una autenticada sin permiso **403**, en ambos
casos sin redirigir al HTML de login (`OnRedirectToLogin` /
`OnRedirectToAccessDenied`). Bajo `/api/v1`, `ApiErrorMiddleware` además rellena
el cuerpo con `{ "error": { "code", "message" } }`.

### Cuenta desactivada a mitad de sesión

`AuthService.ValidateCredentialsAsync` ya rechaza el login de un usuario
inactivo. Para expulsar a quien fue **desactivado o eliminado con la sesión
abierta**, `OnValidatePrincipal` revalida contra la base de datos —como máximo
una vez cada 3 minutos por circuito— que el usuario siga existiendo y activo; si
no, `RejectPrincipal()` + `SignOutAsync()`.

### Último acceso

Tras un `SignInAsync` correcto, `AuthController` llama a
`AuthService.RecordSuccessfulLoginAsync(userId)`, que sella `Users.LastLoginAt`
con `ExecuteUpdateAsync` (sin cargar la entidad ni tocar el `ConcurrencyStamp`).
Es la única fuente de verdad del "último acceso"; el módulo de Usuarios solo lo
lee.

## 6. Antiforgery

* `app.UseAntiforgery()` permanece activo globalmente (no se desactivó nada).
* El formulario de login lleva `<AntiforgeryToken />` y el endpoint valida
  explícitamente con `IAntiforgery.ValidateRequestAsync(HttpContext)`.
* No se usa `[ValidateAntiForgeryToken]`: ese filtro requiere los servicios de
  MVC ViewFeatures (`AddControllersWithViews`), que una Blazor Web App no
  registra (provoca `InvalidOperationException`). `IAntiforgery` es el mismo
  mecanismo subyacente y ya está registrado por el framework.
* Los endpoints de Blazor Server (`/_blazor`) gestionan su propia protección;
  el logout GET no requiere token (ver §3).

## 7. Endpoints

| Método | Ruta | Protección | Respuesta |
|---|---|---|---|
| `GET` | `/log-in` | pública | página SSR con formulario |
| `POST` | `/auth/log-in` | token antiforgery | `302 /dashboard` o `302 /log-in?error=…` |
| `GET` | `/auth/log-out` | ninguna (idempotente) | `302 /log-in`, cookie eliminada |
| `GET` | `/access-denied` | pública | página informativa |

> Nota: la ruta real del endpoint es `/auth/log-in` (no `/api/auth/log-in`).

## 8. Logging

Eventos registrados por `AuthService` y `AuthController`:

* credenciales válidas / sesión iniciada (con `UserId`),
* correo inexistente,
* usuario inactivo,
* contraseña incorrecta,
* hash almacenado corrupto (con excepción),
* token antiforgery inválido (con excepción),
* cierre de sesión.

Nunca se registran: contraseñas, hashes, cookies ni secretos del `.env`.
Los mensajes al usuario son genéricos y no exponen detalles internos.

## 9. Troubleshooting

| Síntoma | Causa probable | Solución |
|---|---|---|
| `form_expired` al enviar el login | Token antiforgery viejo (página abierta mucho tiempo, cookie de antiforgery eliminada, o token emitido a otra identidad) | Recargar `/log-in` |
| Login correcto pero `/dashboard` vuelve a `/log-in` | Cookie no persistida o reloj del sistema desfasado | Verificar cookies del navegador y hora del equipo |
| `Invalid salt version` (ya no debe ocurrir) | Hash no-BCrypt en `dbo.Users` | Verificar que el hash fue generado por `DbInitializer` con BCrypt. Recrear el admin si es necesario. |
| `WebSocket is not in the OPEN state` (ya no debe ocurrir) | Interop JS tras cerrar el circuito | El logout usa navegación forzada; no reintroducir llamadas JS post-navegación |
| `500 ValidateAntiforgeryTokenAuthorizationFilter` | Uso de `[ValidateAntiForgeryToken]` sin MVC ViewFeatures | Usar `IAntiforgery` (implementación actual) |

## 10. Cómo probar manualmente

```bash
./scripts/run.sh          # SQL Server + init + app en http://localhost:5000
# 1. Visitar http://localhost:5000/log-in
# 2. Credenciales incorrectas  -> mensaje "Correo o contraseña incorrectos."
# 3. Credenciales correctas    -> /dashboard
# 4. Visitar /dashboard en ventana privada -> redirect a /log-in?ReturnUrl=%2Fdashboard
# 5. "Cerrar sesión"           -> /log-in
# 6. Volver a /dashboard       -> exige login nuevamente
```

Pruebas HTTP con curl (extraer el token del campo oculto de `/log-in`):

```bash
curl -s -c jar.txt http://localhost:5000/log-in > login.html
TOKEN=$(grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' login.html | sed 's/.*value="//;s/"$//')
curl -s -i -b jar.txt -X POST http://localhost:5000/auth/log-in \
  --data-urlencode "Email=tu-admin@openclient.local" \
  --data-urlencode "Password=TU_PASSWORD" \
  --data-urlencode "__RequestVerificationToken=$TOKEN" \
  --data-urlencode "returnUrl=/dashboard"
# Esperado: HTTP/1.1 302 Found ; Location: /dashboard ; Set-Cookie: .OpenClient.Auth=...
```
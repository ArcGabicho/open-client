# Guía de Desarrollo

Entorno local de Open Client: requisitos, arranque, estructura y verificación
de cambios. Para detalles de autenticación e inicialización de BD, ver
`authentication.md` y `database-initialization.md`.

---

## 1. Requisitos

* .NET SDK 10 (`dotnet --list-sdks`)
* Docker + Docker Compose v2
* Bash

## 2. Arranque

```bash
cp .env.example .env        # primera vez; luego edita los valores reales
./scripts/run.sh            # SQL Server (Docker) + init/seed + app en :5000
```

`run.sh` hace, en orden:

1. verifica que `.env` existe y lo exporta al entorno,
2. levanta `sqlserver` (healthcheck `SELECT 1`),
3. comprueba que el puerto 5000 está libre,
4. publica `PasswordHasher` (single-file self-contained linux-x64),
5. construye la imagen `db-init` y ejecuta la inicialización
   (idempotente — ver `database-initialization.md`),
6. restaura paquetes y lanza la app con
   `ASPNETCORE_ENVIRONMENT=Development`, `ASPNETCORE_URLS=http://localhost:5000`
   y la cadena de conexión **por variable de entorno**.

Otros modos:

```bash
./scripts/run.sh --full     # stack completo en Docker (app en :8080)
./scripts/run.sh --stop     # detener contenedores
./scripts/run.sh --logs     # logs de SQL Server
```

La app queda disponible en <http://localhost:5000>. Login con las credenciales
del `.env` (`OPENCLIENT_ADMIN_EMAIL` / `OPENCLIENT_ADMIN_PASSWORD`).

## 3. Estructura relevante a autenticación

| Archivo | Rol |
|---|---|
| `core/Program.cs` | Cookie Authentication, authorization, antiforgery, pipeline |
| `core/Controllers/AuthController.cs` | `POST /auth/log-in`, `GET /auth/log-out` |
| `core/Services/AuthService.cs` | búsqueda de usuario + BCrypt + claims + logging |
| `core/Components/Pages/Login.razor` | formulario SSR estático con antiforgery |
| `core/Components/Pages/Dashboard.razor` | `[Authorize]` + logout por navegación forzada |
| `core/Components/Pages/AccessDenied.razor` | página de acceso denegado |
| `docker/database/init.sh` | validación de credenciales admin + hash BCrypt |
| `docker/database/PasswordHasher/` | generador de hashes BCrypt |

## 4. Reglas del proyecto

* **Secretos**: solo en `.env` (fuera de Git). Las cadenas de conexión llegan
  por variables de entorno — no hardcodearlas en `appsettings*.json`.
* **Render modes**: SSR estático por defecto; interactividad opt-in por página
  (`@rendermode InteractiveServer`). No volver a un modo global interactivo.
* **Autenticación**: un único mecanismo (formularios HTTP + cookies). No usar
  JS interop ni `fetch` para login/logout.
* **Logging**: eventos de autenticación vía `ILogger`; nunca registrar
  contraseñas, hashes, cookies ni secretos.
* **Errores**: capturar y registrar la excepción real (`ILogger.LogError`);
  mostrar al usuario mensajes genéricos.

## 5. Verificación antes de commit

```bash
dotnet build core/openclient.csproj
dotnet build docker/database/PasswordHasher/PasswordHasher.csproj
./scripts/run.sh          # segunda ejecución debe ser idempotente
```

Checklist funcional mínimo:

- [ ] `/log-in` carga sin errores de consola (sin circuito Blazor)
- [ ] login incorrecto muestra mensaje genérico
- [ ] login correcto llega a `/dashboard`
- [ ] `/dashboard` anónimo redirige a `/log-in?ReturnUrl=%2Fdashboard`
- [ ] logout lleva a `/log-in` sin errores de WebSocket
- [ ] tras logout, `/dashboard` vuelve a exigir login
- [ ] segunda ejecución de `run.sh`: 1 admin, 4040 clientes, sin duplicados

## 6. Problemas conocidos resueltos (referencia)

Ver la tabla de causas raíz en `docs/authentication.md` §1: errores de
antiforgery en el login (400), `Invalid salt version` de BCrypt, y
`WebSocket is not in the OPEN state` en logout. Ninguno debe reaparecer; si
lo hacen, revisar primero que no se reintrodujo JS interop o render global
interactivo.

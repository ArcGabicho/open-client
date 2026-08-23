# Inicialización de la Base de Datos

Comportamiento real del pipeline de inicialización (agosto de 2026): desde el
`.env` hasta `dbo.Users` y el seed de 4040 clientes. Complementa a
`database-guide.md` con las decisiones de idempotencia y seguridad vigentes.

---

## 1. Pipeline

```
.env                          (fuera de Git; ver .env.example)
  OPENCLIENT_ADMIN_EMAIL
  OPENCLIENT_ADMIN_PASSWORD
        |   docker compose --env-file .env pasa las variables al contenedor db-init
        v
init.sh
  1. espera a que SQL Server responda
  2. valida OPENCLIENT_ADMIN_EMAIL (no vacío, formato email básico)
  3. valida OPENCLIENT_ADMIN_PASSWORD (no vacío)
  4. ejecuta /PasswordHasher  ->  hash BCrypt por stdout
  5. valida formato BCrypt: ^\$2[aby]\$[0-9]{2}\$[./A-Za-z0-9]{53}$  (60 chars)
  6. escapa ADMIN_EMAIL (' -> '') y sustituye placeholders en /admin.sql
  7. sqlcmd -d OpenClientDb -i admin.sql
  8. si dbo.Clients está vacía: seed transaccional (SET XACT_ABORT ON)
        v
SQL Server (volumen openclient_data)
  dbo.Users   -> 1 fila admin (Role='Admin', IsActive=1)
  dbo.Clients -> 4040 filas
```

## 2. PasswordHasher

* Ubicación: `docker/database/PasswordHasher/`.
* Lee **solo** la variable de entorno `OPENCLIENT_ADMIN_PASSWORD` (nunca argv,
  nunca archivos); imprime únicamente el hash por stdout; errores por stderr;
  sale con código 1 si falta la variable.
* Genera `BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12)` →
  hashes `$2a$12$…`.
* Publicado por `scripts/run.sh` como **single-file self-contained linux-x64**
  en `PasswordHasher/publish/`; el Dockerfile copia el binario como
  `/PasswordHasher` y `init.sh` lo ejecuta directamente. Runtime y ruta
  coinciden exactamente (no existe `/PasswordHasher.dll`, no se usa
  `dotnet /PasswordHasher.dll`).

## 3. admin.sql e inyección SQL

`admin.sql` no contiene contraseñas ni hashes fijos: recibe dos placeholders
(`__ADMIN_EMAIL__`, `__ADMIN_PASSWORD_HASH__`) sustituidos por `init.sh`.

* La sustitución es literal byte-a-byte (bucle bash, sin `sed` ni regex), inmune
  a caracteres especiales (`& \ | $ . *`) en los valores del `.env`.
* Los valores se insertan dentro de literales `N'…'` tras escapar comillas
  simples (`'` → `''`) — mitigación estándar frente a inyección en T-SQL.
* El hash proviene exclusivamente de `PasswordHasher` (salida validada contra
  la regex BCrypt antes de tocar la BD), no de entrada humana directa.

## 4. Idempotencia (política oficial)

Ejecutar `./scripts/run.sh` repetidamente es seguro:

| Elemento | Primera ejecución | Ejecuciones siguientes |
|---|---|---|
| `OpenClientDb` | se crea | se detecta y se omite |
| login/usuario `openclient_user`, rol | se crean | se detectan y se omiten |
| tabla `dbo.Users` | se crea | "ya existe" |
| admin (`OPENCLIENT_ADMIN_EMAIL`) | INSERT | **UPDATE solo de `PasswordHash`** |
| seed `dbo.Clients` (4040) | INSERT transaccional | **se omite** si ya hay registros |

### Política de contraseña del administrador

**`.env` es la fuente de verdad del administrador inicial.** Si cambia
`OPENCLIENT_ADMIN_PASSWORD` en `.env`, la siguiente ejecución actualiza el hash
del admin existente. Nunca se modifican correo, rol ni estado (`IsActive`) —
una desactivación manual del admin no se revierte.

Justificación: quien controla el `.env` ya controla todo el pipeline de
inicialización (incluida la cuenta `sa` vía `MSSQL_PASSWORD`); sincronizar el
hash permite recuperar acceso sin recrear la base de datos.

## 5. Verificación rápida

```bash
docker exec openclient-database /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$MSSQL_PASSWORD" -C -d OpenClientDb \
  -Q "SELECT COUNT(*) FROM dbo.Users; SELECT COUNT(*) FROM dbo.Clients;"
# Esperado: 1 y 4040
```

## 6. Secretos

* `.env` está fuera de Git (`.gitignore` lo cubre; plantilla en `.env.example`).
* `appsettings*.json` ya no contienen cadenas de conexión: `scripts/run.sh`
  inyecta `ConnectionStrings__DefaultConnection` como variable de entorno.
* ⚠️ Histórico: versiones anteriores de este repositorio incluyeron una
  contraseña real en `appsettings.Development.json`. Rota esa credencial
  (`MSSQL_APP_PASSWORD`) si tu instalación hereda ese valor.

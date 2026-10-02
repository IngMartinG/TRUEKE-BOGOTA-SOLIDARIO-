# Estado del backend — Trueke Bogotá Solidario (2026-10-02)

## Hecho — backend completo para conectar el front
**Base:**
- arquitectura por capas;
- catálogo con geolocalización y comentarios;
- SignalR + Redis;
- migraciones;
- Docker;
- CI;
- contrato OpenAPI y ProblemDetails.

**Seguridad y cuentas:**
- refresh token rotativo en cookie HttpOnly;
- verificación de correo y recuperación de clave;
- login con Google;
- **2FA TOTP** obligatoria para administración;
- **reCAPTCHA v3**;
- Habeas Data (Ley 1581).

**Funcionalidad:**
- perfil público;
- editar publicaciones y hasta 5 fotos (Azure Blob con SAS);
- chat interno;
- **confirmación de entrega por ambas partes** con cierre automático (7 y 30 días);
- **calificaciones**;
- **favoritos**;
- orden y filtros;
- notificaciones persistentes;
- denuncias.

**Administración:**
- suspender o reactivar cuentas con jerarquía de roles;
- búsqueda de usuarios;
- pagos en revisión y registro de reembolsos;
- verificaciones y roles.

**Operación:**
- Application Insights (OpenTelemetry);
- correos HTML;
- tarea horaria de cierre y purga;
- pruebas contra SQL Server real (Testcontainers) obligatorias en el CI.

**Estado de verificación:** 175 pruebas aprobadas y 4 de SQL Server que corren en el CI; `-warnaserror` limpio; sin paquetes vulnerables.

## Pendiente (fuera del código)
- **Subir la rama:** GitHub Desktop → Publish branch → Pull Request → CI en verde → Merge.
- **Instalar Docker en local:** activar la virtualización en la BIOS, `wsl --install` y Docker Desktop.
- **Configuración externa:** Google Client ID, reCAPTCHA, SMTP, Azure (SQL, Redis, Storage, App Service, Key Vault, Application Insights) y Wompi. Ver la sección 7 del README; se guía en el momento de cada etapa.
- ~~Construir el front Angular~~ → hecho en `frontend/` (ver `frontend/README.md`). El compose ahora incluye el servicio `web` en http://localhost:8081.

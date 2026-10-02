# Estado del backend — Trueke Bogotá Solidario (2026-10-02)

## Hecho
**Base (10 pendientes originales de CLAUDE.md):**
- solución por capas;
- configuración sin secretos;
- geolocalización (`/publicaciones/cercanas`, coordenadas aproximadas);
- comentarios con moderación;
- SignalR + Redis;
- migración EF Core;
- pruebas;
- Docker y CI;
- contrato para Angular (ProblemDetails, fechas UTC, enums como texto, `docs/openapi.json`);
- README.

**Cierre de brechas antes de conectar el front:**
1. **Refresh token seguro:** acceso de 15 min en memoria; refresco rotativo en una cookie HttpOnly con detección de reuso y anti-CSRF.
2. **Verificación de correo y recuperación de contraseña:** enlaces de un solo uso y SMTP (MailKit). Sin correo verificado no se puede publicar, solicitar, comentar, chatear, denunciar ni pagar.
3. **Login con Google:** ID token validado en el servidor y vinculación segura de cuentas.
4. **Chat interno por solicitud**, en tiempo real. Se eliminó `correoContacto`: la API ya no comparte correos entre usuarios.
5. **Subida de imágenes a Azure Blob:** SAS de 5 min y validación posterior de dueño, tamaño y magic bytes. Documentos de identidad privados, borrados al resolver la verificación.
6. **Notificaciones persistentes:** bandeja más tiempo real.
7. **Denuncias** con cola de moderación agrupada.
8. **Habeas Data (Ley 1581):** consentimiento, exportar mis datos y eliminar la cuenta (anonimización).
9. **Mantenimiento periódico:** purga de tokens vencidos y notificaciones viejas.

137 pruebas en verde; `dotnet build -warnaserror` y `dotnet list package --vulnerable` limpios.

## Pendiente
- Ejecutar `docker build` / `docker compose up` (Docker no estaba instalado en la máquina de desarrollo; los valida el CI).
- Infraestructura Azure:
  - contenedores `imagenes` (lectura pública de blobs) y `documentos` (privado);
  - CORS de Storage;
  - regla de *lifecycle* para archivos huérfanos;
  - Client ID de Google y SMTP.
- Construir el front Angular siguiendo la sección 10 del README.

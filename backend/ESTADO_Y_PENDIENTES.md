# Estado del backend — Trueke Bogotá Solidario (2026-10-02)

## Hecho (los 10 pendientes de CLAUDE.md)
1. Solución `TruekeBogotaSolidario.sln` (4 proyectos), `nuget.config`, `global.json`, `appsettings*.json` sin secretos, `.gitignore`, `.env.example`, `launchSettings.json`.
2. `dotnet build -warnaserror` en verde. El backend anterior (`TruekeBogotaSolidario-Backend-Etapa4/`) se eliminó: estaba superado por completo.
3. Geolocalización: `GET /api/v1/publicaciones/cercanas` (Haversine con prefiltro rectangular). Las coordenadas públicas se redondean a 2 decimales; las exactas solo llegan al dueño, a los moderadores o con una solicitud aceptada. La distancia se mide contra la posición visible, para impedir la triangulación.
4. Comentarios: entidad, repositorio, servicio y endpoints (crear, listar, ocultar y mostrar con auditoría). Los ocultos nunca son públicos.
5. SignalR `/hubs/notificaciones` (JWT; token por query solo en `/hubs`), `INotificador` en Negocio y backplane Redis configurable.
6. Migración `Inicial` en `Datos/Migraciones`, más una prueba que detecta cambios de modelo sin migrar.
7. 74 pruebas xUnit: unitarias, integración, arquitectura y contrato.
8. Dockerfile multi-stage sin root, docker-compose (API + SQL Server + Redis) y GitHub Actions (build, test, auditoría de vulnerables, docker build).
9. Contrato para Angular:
   - errores como ProblemDetails en español;
   - fechas UTC con Z y enums como texto;
   - CORS por configuración;
   - `docs/openapi.json` versionado y verificado por prueba.
10. README completo (`backend/README.md`) y revisión de seguridad.

## Pendiente / decisiones abiertas
- `docker build` y `docker compose up` no se ejecutaron en local (Docker no estaba instalado); los valida el CI.
- **Decidir** si se mantiene `correoContacto` en las solicitudes aceptadas. CLAUDE.md prohíbe exponer correos de terceros; hoy se comparte el correo de la contraparte solo tras aceptar.
- Refresh tokens: no implementados (sesión de 60 minutos).

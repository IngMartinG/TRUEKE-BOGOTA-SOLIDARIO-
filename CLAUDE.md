# CLAUDE.md — Trueke Bogotá Solidario (backend)

Responde siempre en español. Entrega código completo y listo para usar. Objetivo: **producción real**, no solo entrega académica.

## Contexto
Plataforma comunitaria de economía circular en Bogotá con 3 modos: **Trueke, Compra y Donación**, más moneda interna **Eco-Puntos**.
Repo: https://github.com/IngMartinG/TRUEKE-BOGOTA-SOLIDARIO- · Flujo Git: rama feature → PR → merge a `main`.
El modelo C4 en `docs/c4/` es la fuente de verdad de la arquitectura.

## Stack obligatorio (no cambiar)
ASP.NET Core 8 (C#) + SignalR · SQL Server (PostgreSQL rechazado) · Redis (backplane de SignalR / caché) · Wompi (solo recargas entrantes) · Docker / Compose · GitHub Actions · Azure App Service + VNet + Blob Storage · Front-end: Angular.

## Arquitectura (exigencia del profesor: un proyecto por capa)
```
TruekeBogotaSolidario.sln
├── TruekeBogotaSolidario.Datos         (EF Core, entidades, repositorios, PoliticaEcoPuntos, PasswordHasher)
├── TruekeBogotaSolidario.Negocio       (servicios, DTOs; referencia SOLO Datos con PrivateAssets="compile")
├── TruekeBogotaSolidario.Presentacion  (Web API; referencia SOLO Negocio)
└── TruekeBogotaSolidario.Pruebas       (xUnit)
```
Un controller nunca puede tocar un repositorio ni el DbContext.

## Estado al 2026-10-02
- Los 10 pendientes de abajo están **hechos**. Además, se cerraron las brechas previas al front:
  - refresh token en cookie HttpOnly;
  - verificación de correo y recuperación de clave;
  - Google;
  - chat interno;
  - imágenes en Azure Blob;
  - notificaciones persistentes;
  - denuncias;
  - Habeas Data.

  Build `-warnaserror` y 137 pruebas en verde. Ver `backend/ESTADO_Y_PENDIENTES.md` y `backend/README.md` (sección 10: contrato para Angular).
- La solución y los proyectos viven en `backend/`. El backend anterior (Etapa4) se eliminó. Estructura del repo: `backend/`, `frontend/`, `prototipo/`, `docs/`.
- **Reglas añadidas:**
  - publicar, solicitar, comentar, chatear, denunciar y pagar exigen correo verificado (`Guardas.ExigirCorreoVerificado`);
  - la API nunca comparte correos entre usuarios (se usa el chat);
  - los archivos se suben directo a Blob con SAS y se validan al usarlos.
- La clave JWT se configura como `Jwt:Key` (variable `Jwt__Key`).

## Pendientes originales (completados)
1. `.sln`, `appsettings.json` sin secretos, `appsettings.Development.json` (Pagos:Proveedor=Simulado, BD InMemory), `.gitignore`, `.env.example`.
2. `dotnet build` en verde (TreatWarningsAsErrors) y corregir Datos/Contexto, Datos/Repositorios/Implementaciones y Presentacion.
3. **Geolocalización**: Latitud/Longitud en Publicacion, `GET /api/v1/publicaciones/cercanas` (Haversine). En listados públicos, redondear a 2 decimales o mostrar solo la Localidad. Coordenadas exactas solo para el dueño o con una Solicitud aceptada.
4. **Comentarios**: entidad + endpoints. Cliente comenta; Administrador/SuperUsuario oculta. Los comentarios ocultos no se muestran en público.
5. **SignalR + Redis**: hub autenticado con JWT (token por query `access_token` solo en la ruta del hub) para notificar solicitudes nuevas/aceptadas/rechazadas, pagos aprobados y moderación. Backplane Redis configurable (desactivado en Development).
6. Migración inicial EF Core (`dotnet ef migrations add Inicial`). En producción usar `Database:Inicializacion=Migrate`.
7. Pruebas xUnit: bloqueo de login, anti-farmeo, idempotencia de pagos, firma del webhook, JWT/roles/revocación, ocultamiento de coordenadas, autorización por rol.
8. Dockerfile multi-stage (usuario no root), docker-compose (API + SQL Server + Redis), GitHub Actions (build + test + `dotnet list package --vulnerable --include-transitive`).
9. Contrato para Angular: OpenAPI exportado en `docs/openapi.json`, CORS por configuración (dev: `http://localhost:4200`), errores con formato único (ProblemDetails), enums como string, fechas UTC ISO-8601.
10. README final y revisión de seguridad completa.

## Roles
Cliente ⊂ Administrador ⊂ SuperUsuario. **Invitado** = visitante anónimo, nunca se guarda como rol. Solo puede leer el catálogo, las categorías y la política de Eco-Puntos.

## Reglas de seguridad (no negociables)
- El usuario que actúa SIEMPRE sale del JWT, nunca del body ni del query.
- `[Authorize]` en cada controller; `[AllowAnonymous]` solo en: login, registro, GET del catálogo y de categorías, GET `/eco-puntos/politica`, health y webhook de Wompi (este con firma verificada).
- Un error 500 nunca expone `ex.Message` ni el stack trace; los 400, 404 y 409 de dominio sí pueden mostrar su mensaje.
- Las respuestas JSON solo llevan lo necesario: nunca ClaveHash, RowVersion internos, correos de terceros, coordenadas exactas ni datos de otro usuario.
- Swagger solo en Development. HSTS + HTTPS + cabeceras de seguridad. No enviar la cabecera `Server`.
- Rate limiting en auth y webhook. Validación con DataAnnotations en todos los DTOs. Límite de tamaño de los requests.
- Secretos (Jwt:Clave, Wompi, cadena de conexión) solo por variables de entorno o user-secrets. Si faltan en producción, la app no arranca.
- Concurrencia optimista (RowVersion) y respuesta 409 si hay conflicto. Validar antes de cobrar; los pagos son transaccionales.
- Sobre "F12": el código del front siempre es descargable. La protección real es que el front no tenga secretos, el build de producción sin source maps y que el backend valide todo.

## Economía Eco-Puntos
Compra 5 · Trueke 10 · Donación 20 · Registro 10. Reputación +0.05 / +0.10 / +0.20, tope 5.0. Máximo 5 transacciones con puntos por día.
- Destacar publicación: $6.000 (25 % de descuento con ≥200 pts, 40 % con ≥500 pts).
- Verificar cuenta: $20.000 (20 % / 35 %).
- Premium: $15.000/mes (3 destacados gratis + 15 % de descuento).
- Empresa: $50.000/mes.

Recarga: 100 COP = 1 Eco-Punto. Los Eco-Puntos nunca se convierten de vuelta a pesos. Wompi solo recibe dinero hacia la plataforma, nunca entre usuarios.
`GET /eco-puntos/politica` se lee en vivo de PoliticaEcoPuntos.cs.

## Comandos
Desde `backend/`:
```
dotnet tool restore
dotnet build TruekeBogotaSolidario.sln -warnaserror
dotnet test
dotnet tool run dotnet-ef migrations add <Nombre> -p TruekeBogotaSolidario.Datos -s TruekeBogotaSolidario.Datos -o Migraciones
dotnet run --project TruekeBogotaSolidario.Presentacion   # Swagger en /swagger (Development)
ACTUALIZAR_OPENAPI=1 dotnet test --filter OpenApiTests    # regenera docs/openapi.json tras cambiar la API
```

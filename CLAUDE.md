# CLAUDE.md — Trueke Bogotá Solidario (backend)

Responde siempre en español. Entrega código completo y listo para usar. Objetivo: **producción real**, no solo entrega académica.

## Contexto
Plataforma comunitaria de economía circular en Bogotá con 3 modos: **Trueke, Compra y Donación**, más moneda interna **Eco-Puntos**.
Repo: https://github.com/IngMartinG/TRUEKE-BOGOTA-SOLIDARIO- · Flujo Git: rama feature → PR → merge a `main`.
El modelo C4 en `docs/c4/` es la fuente de verdad de la arquitectura.

## Stack obligatorio (no cambiar)
ASP.NET Core 8 (C#) + SignalR · SQL Server (PostgreSQL rechazado) · Redis (backplane de SignalR / caché) · Wompi (solo recargas entrantes) · Docker / Compose · GitHub Actions · Azure App Service + VNet + Blob Storage · Front-end: Angular.

## Arquitectura (un proyecto por capa, obligatorio)
```
TruekeBogotaSolidario.sln
├── TruekeBogotaSolidario.Datos         (EF Core, entidades, repositorios, PoliticaEcoPuntos, PasswordHasher)
├── TruekeBogotaSolidario.Negocio       (servicios, DTOs; referencia SOLO Datos con PrivateAssets="compile")
├── TruekeBogotaSolidario.Presentacion  (Web API; referencia SOLO Negocio)
└── TruekeBogotaSolidario.Pruebas       (xUnit)
```
Un controller nunca puede tocar un repositorio ni el DbContext.

## Estado actual
El detalle de lo hecho y lo pendiente está en `docs/ESTADO_Y_PENDIENTES.md` (fuente única; no repetirlo aquí). Resumen al 2026-10-04:
backend y front completos (234 pruebas + build `-warnaserror`; front: lint, 15 pruebas y build de producción), 6 migraciones,
diseño adaptable a celular/tablet/PC, chat en tiempo real con respuestas, entregado/leído, escribiendo y en línea.
Estructura: `backend/`, `frontend/`, `docs/` (c4, openapi.json, migraciones, diseno, historico). Lo académico (prototipo, C4 viejo) está archivado en `docs/historico/`.
SuperUsuario: martincolombia15@gmail.com (con 2FA).

## Decisiones y reglas técnicas vigentes
- Los Eco-Puntos se otorgan solo al completarse el intercambio (ambas partes confirman la entrega).
- Administración exige sesión con 2FA (`amr=mfa`). Comentar y denunciar exigen correo verificado (`Guardas.ExigirCorreoVerificado`); publicar, editar, impulsar, solicitar, aceptar, chatear y pagar exigen además foto de perfil (`Guardas.ExigirCuentaCompleta`, 403 `foto_requerida`). Con Google, la foto se importa sola. Sin correo verificado el front solo deja explorar.
- La API nunca comparte correos entre usuarios (se usa el chat). Los archivos se suben directo a Blob con SAS y se validan al usarlos; `ProcesadorImagenes` (SkiaSharp + `NativeAssets.Linux.NoDependencies`; ImageSharp descartado: la v3 tiene avisos sin parche y la v4 exige llave) re-codifica la foto, aplica la orientación y quita EXIF/GPS. Solo acepta JPEG, PNG y WEBP.
- La clave JWT es `Jwt:Key` (variable `Jwt__Key`). La sesión solo se cierra con 401/403 al refrescar (candado entre pestañas en el front).
- Escala Colombia (DANE-DIVIPOLA, Bogotá = 11001); la marca sigue siendo "Trueke Bogotá Solidario" (decisión del dueño). Logo oficial en `frontend/public/logo-emblema.png` y `logo-completo.png`.
- Anti-farmeo: correo canónico único, correos desechables bloqueados, misma pareja suma 1 vez cada 30 días.
- Facturación: `Factura` por pago aprobado (IVA incluido), modo `Manual` en producción, `Simulado` solo en dev. Un reembolso revierte el beneficio.
- Denuncias con debido proceso (aviso al denunciado sin revelar al denunciante, apelación en 15 días resuelta por otro moderador). Bloqueos en ambas direcciones; avisos por correo opcionales (`IAvisosCorreo`) solo si la persona no está en línea (`IPresencia`).
- Endpoints anónimos permitidos: ver "Reglas de seguridad".
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
- `[Authorize]` en cada controller; `[AllowAnonymous]` solo en: login, registro, GET del catálogo (incluye destacadas, sugerencias y la vista previa `/compartir/publicaciones/{id}`), de categorías y de ubicaciones, GET `/eco-puntos/politica`, health y webhook de Wompi (este con firma verificada).
- Un error 500 nunca expone `ex.Message` ni el stack trace; los 400, 404 y 409 de dominio sí pueden mostrar su mensaje.
- Las respuestas JSON solo llevan lo necesario: nunca ClaveHash, RowVersion internos, correos de terceros, coordenadas exactas ni datos de otro usuario.
- Swagger solo en Development. HSTS + HTTPS + cabeceras de seguridad. No enviar la cabecera `Server`.
- Rate limiting en auth y webhook. Validación con DataAnnotations en todos los DTOs. Límite de tamaño de los requests.
- Secretos (Jwt:Clave, Wompi, cadena de conexión) solo por variables de entorno o user-secrets. Si faltan en producción, la app no arranca.
- Concurrencia optimista (RowVersion) y respuesta 409 si hay conflicto. Validar antes de cobrar; los pagos son transaccionales.
- Sobre "F12": el código del front siempre es descargable. La protección real es que el front no tenga secretos, el build de producción sin source maps y que el backend valide todo.

## Economía Eco-Puntos
Compra 5 · Trueke 10 · Donación 20 · Bienvenida 10 (al verificar el correo). Reputación +0.05 / +0.10 / +0.20, tope 5.0. Máximo 5 transacciones con puntos por día y 1 cada 30 días con la misma persona.
- Destacar publicación: $6.000 (25 % de descuento con ≥200 pts, 40 % con ≥500 pts).
- Verificar cuenta: $20.000 (20 % / 35 %).
- Premium: $15.000/mes (3 destacados gratis + 15 % de descuento, 150 publicaciones, estadísticas diarias).
- Empresa: $50.000/mes (10 destacados gratis + 20 % de descuento, 1.000 publicaciones, nombre comercial + NIT, estadísticas diarias).
- Impulsar: 20 Eco-Puntos (cada 24 h por publicación). Es el único beneficio pagado 100 % con puntos (no tiene precio en pesos).
- Precios con IVA incluido; cada pago aprobado genera una `Factura`.

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
Desde `frontend/` (Node 24 LTS):
```
npm ci
npm start            # http://localhost:4200 con proxy de /api y /hubs a https://localhost:7180
npm run verificar    # lint + pruebas + build de producción
npm run api          # regenera los tipos desde docs/openapi.json (tras cambiar la API)
```

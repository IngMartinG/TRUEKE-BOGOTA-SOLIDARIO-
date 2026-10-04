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

  Segunda tanda, también hecha:
  - perfil público;
  - editar publicaciones y varias fotos;
  - confirmación de entrega por ambas partes;
  - calificaciones;
  - favoritos y filtros;
  - suspensión de cuentas;
  - pagos en revisión;
  - correos HTML;
  - reCAPTCHA v3;
  - 2FA TOTP para administradores;
  - Application Insights;
  - pruebas contra SQL Server real (Testcontainers).

  Build `-warnaserror` y 175 pruebas en verde (más 4 de SQL Server que corren en el CI). Ver `backend/ESTADO_Y_PENDIENTES.md` y `backend/README.md` (sección 10: contrato para Angular).
- **Front-end Angular hecho** en `frontend/` (rama `feature/frontend-angular`): Angular 22 con signals y sin Zone.js, Tailwind v4 con sistema de diseño ecológico, todas las pantallas del contrato (catálogo con mapa, publicar, intercambios, chat SignalR, notificaciones, Eco-Puntos/pagos, cuenta con 2FA y Habeas Data, moderación). Lint, 13 pruebas y build de producción en verde; imagen nginx sin root con CSP; CI en `.github/workflows/frontend-ci.yml`. Ver `frontend/README.md`.
- La solución y los proyectos viven en `backend/`. El backend anterior (Etapa4) se eliminó. Estructura del repo: `backend/`, `frontend/`, `prototipo/`, `docs/`.
- **Reglas añadidas:**
  - los Eco-Puntos se otorgan solo al completarse el intercambio (ambas partes confirman la entrega);
  - las funciones de administración exigen una sesión con 2FA (`amr=mfa`);
  - publicar, solicitar, comentar, chatear, denunciar y pagar exigen correo verificado (`Guardas.ExigirCorreoVerificado`);
  - la API nunca comparte correos entre usuarios (se usa el chat);
  - los archivos se suben directo a Blob con SAS y se validan al usarlos.
- La clave JWT se configura como `Jwt:Key` (variable `Jwt__Key`).

## Estado al 2026-10-03 (escala nacional y monetización)
- **Escala Colombia:** catálogo DANE-DIVIPOLA embebido (`Datos/Recursos/divipola.json`, clase `Divipola`); `MunicipioCodigo`/`DepartamentoCodigo` en publicaciones y usuarios (Bogotá = 11001); `GET /ubicaciones/...`; filtros por departamento/municipio. La marca sigue siendo "Trueke Bogotá Solidario" (decisión del dueño).
- **Estado del producto** obligatorio al publicar (`CondicionProducto`: Nuevo, ComoNuevo, Usado, UsadoConDetalles, Reparado, ParaRepuestos; las tres últimas exigen `DetalleCondicion`).
- **Fotos limpias:** `ProcesadorImagenes` (ImageSharp 3.1, NO subir a 4: exige llave) re-codifica y quita EXIF/GPS; la copia limpia se guarda con nombre nuevo.
- **Anti-farmeo:** correo canónico único (`CorreoCanonico`), correos desechables bloqueados, bono al verificar el correo, misma pareja suma puntos/reputación 1 vez cada 30 días, 1 calificación por pareja/30 días cuenta en el promedio.
- **Monetización:** Impulsar (20 Eco-Puntos, cada 24 h, único uso 100 % con puntos); Empresa = 10 destacados gratis, 20 % de descuento, 1.000 publicaciones, nombre comercial + NIT; Premium = 3 destacados, 15 %, 150 publicaciones; estadísticas por publicación (serie diaria solo planes pagos); vitrina de destacadas; recordatorio de vencimiento de planes; art. 53 (más de 5 ventas exige identidad verificada o Empresa).
- **Facturación:** `Factura` por pago aprobado (misma transacción), IVA incluido, modo `Manual` en producción (equipo registra número y CUFE), `Simulado` solo dev. **PQR** con retracto y reversión. **Ingresos** y CSV solo SuperUsuario. Un reembolso revierte el beneficio.
- **Infra:** límite de peticiones distribuido en Redis, proxies confiables configurables, CodeQL, Dependabot, ZAP (manual contra staging).
- Migración `EscalaNacionalYMonetizacion` (con valores por defecto para datos existentes). 216 pruebas + 6 de SQL Server (CI).
- **Endpoints anónimos añadidos** a la lista blanca: `GET /ubicaciones/...` y `GET /publicaciones/destacadas` (lectura del catálogo).
- **Pendiente (requiere cuentas externas):** cobro recurrente con fuentes de pago de Wompi, integración API con proveedor de factura electrónica, festivos en plazos de PQR.

## Estado al 2026-10-04
- Chat: responder un mensaje en particular (`respuestaAId`; deslizar en celular, botón con mouse).
- Denuncias con debido proceso: si es procedente se avisa al denunciado (`DenunciaRecibida`, sin revelar al denunciante) y puede apelar una vez en 15 días; la apelación la resuelve otro moderador o un SuperUsuario y, si se acepta, se revierte la medida. Migración `ChatRespuestasYApelaciones`. 227 pruebas.

- Segunda tanda del 2026-10-04: sesión estable (solo 401/403 al refrescar cierran sesión; candado entre pestañas), chat con entregado/leído/escribiendo/en línea (`IPresencia`), foto de perfil (`PUT /usuarios/yo/foto`), diseño adaptable (barra inferior < 1024 px, `app-volver`), "Pagos y facturas", bloqueo de usuarios, preferencias de avisos por correo (`IAvisosCorreo`) y `/ayuda`. 234 pruebas.

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
- `[Authorize]` en cada controller; `[AllowAnonymous]` solo en: login, registro, GET del catálogo (incluye destacadas), de categorías y de ubicaciones, GET `/eco-puntos/politica`, health y webhook de Wompi (este con firma verificada).
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

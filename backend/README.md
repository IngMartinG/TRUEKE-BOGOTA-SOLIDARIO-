# Trueke Bogotá Solidario — Backend

API REST + tiempo real para la plataforma comunitaria de economía circular de Bogotá: **Trueke, Compra y Donación**, con moneda interna **Eco-Puntos**.

ASP.NET Core 8 · EF Core 8 · SQL Server · SignalR (+ Redis) · Wompi · Docker · GitHub Actions · Azure App Service.
La arquitectura sigue el modelo C4 de [`docs/c4`](../docs/c4) y el contrato para el front está en [`docs/openapi.json`](../docs/openapi.json).

---

## 1. Arquitectura (un proyecto por capa)

```
TruekeBogotaSolidario.sln
├── TruekeBogotaSolidario.Datos          Entidades, DbContext, repositorios, migraciones, PoliticaEcoPuntos, PasswordHasher
├── TruekeBogotaSolidario.Negocio        Servicios, DTOs, reglas, pagos Wompi, notificaciones (sin ASP.NET Core)
├── TruekeBogotaSolidario.Presentacion   Web API: controllers, JWT, SignalR, CORS, rate limiting, ProblemDetails
└── TruekeBogotaSolidario.Pruebas        xUnit: unitarias, integración (WebApplicationFactory) y arquitectura
```

- **Presentacion → Negocio → Datos.** Negocio referencia Datos con `PrivateAssets="compile"`, así que Presentacion **no puede ver** repositorios ni el DbContext. Las pruebas de arquitectura lo verifican.
- Un request: `Controller` (HTTP ↔ DTO, el usuario sale del JWT) → `Servicio` (reglas, autorización de dominio) → `Repositorio` → `DbContext`. Todos los cambios de una operación se confirman en un solo `SaveChanges`, mediante `IUnidadDeTrabajo`.
- Concurrencia optimista con `rowversion` en Usuario, Publicacion, Solicitud, Pago y Comentario. Un conflicto se devuelve como **409**.

## 2. Roles

| Rol | Puede |
|---|---|
| **Invitado** (anónimo, nunca se guarda) | Leer el catálogo, el detalle y los comentarios visibles, ver publicaciones cercanas, categorías y la política de Eco-Puntos; registrarse e iniciar sesión |
| **Cliente** | Publicar, solicitar, aceptar o rechazar, comentar, pagar beneficios y recargar Eco-Puntos |
| **Administrador** | Todo lo del Cliente, más moderar publicaciones y comentarios y resolver verificaciones |
| **SuperUsuario** | Todo lo del Administrador, más cambiar roles (con auditoría; siempre queda al menos un SuperUsuario) |

El primer SuperUsuario se crea al arrancar si existen `Bootstrap:SuperUsuarioCorreo` y `Bootstrap:SuperUsuarioClave`. Después del primer despliegue, quita esas variables.

## 3. Economía Eco-Puntos

La fuente única es `Datos/Common/PoliticaEcoPuntos.cs`, y `GET /api/v1/eco-puntos/politica` la lee en vivo.

| Acción | Eco-Puntos | Reputación |
|---|---|---|
| Registro | 10 | — |
| Compra | 5 | +0,05 |
| Trueke | 10 | +0,10 |
| Donación | 20 | +0,20 |

- La reputación tiene un tope de 5,0. **Anti-farmeo:** solo las primeras 5 transacciones de un usuario en 24 h dan puntos y reputación; las demás cuentan en el historial pero no suman.
- **Beneficios:**

  | Beneficio | Precio | Descuento con Eco-Puntos |
  |---|---|---|
  | Destacar (7 días) | $6.000 | 25 % con ≥200 pts, 40 % con ≥500 pts |
  | Verificar cuenta | $20.000 | 20 % con ≥200 pts, 35 % con ≥500 pts |
  | Premium | $15.000/mes | 3 destacados gratis + 15 % extra en los descuentos |
  | Empresa | $50.000/mes | — |

- **Recarga:** 100 COP = 1 Eco-Punto, entre $2.000 y $500.000. Los Eco-Puntos **nunca** se convierten a pesos. Wompi solo recibe dinero hacia la plataforma, nunca entre usuarios.
- **Flujo de pago:**
  1. `POST /pagos/iniciar` valida todo **antes** de cobrar, reserva los puntos y devuelve la referencia y la firma de integridad para el widget de Wompi.
  2. Wompi llama a `POST /pagos/wompi/eventos`. El endpoint verifica la firma y luego **consulta la transacción a Wompi**, que es la fuente de verdad. Es idempotente: un webhook repetido no acredita dos veces.
  3. Un reconciliador en segundo plano resuelve o expira los pagos pendientes y devuelve los puntos reservados.

## 4. Ejecutar en local

Requisitos: SDK de .NET 8 (`global.json` fija la línea 8.0.4xx).

```bash
cd backend
dotnet tool restore
dotnet build TruekeBogotaSolidario.sln -warnaserror
dotnet test
dotnet run --project TruekeBogotaSolidario.Presentacion
```

En **Development**:
- La base es InMemory y los pagos están en modo `Simulado`: `POST /pagos/{ref}/simular?aprobado=true` aprueba un pago.
- CORS permite `http://localhost:4200`, y Swagger está en `https://localhost:7180/swagger`.
- Si no defines `Jwt:Key`, se genera una clave efímera, y los tokens dejan de valer al reiniciar. Para fijarla:

```bash
dotnet user-secrets set "Jwt:Key" "<al menos 32 caracteres aleatorios>" --project TruekeBogotaSolidario.Presentacion
```

### Con Docker (API + SQL Server + Redis)

```bash
cp .env.example .env
docker compose up --build
```

Antes de levantarlo, completa los valores de `.env`. La API queda en `http://localhost:8080`, con `/health/live` y `/health/ready`. SQL Server y Redis no se exponen al host.

## 5. Configuración y secretos

Todo se configura por variables de entorno (en Azure: *App Settings* o Key Vault references) con `__` como separador. **Ningún secreto se versiona**: `appsettings.json` solo trae valores no sensibles.

| Variable | Obligatoria en producción | Descripción |
|---|---|---|
| `ConnectionStrings__TruekeDb` | ✅ | SQL Server / Azure SQL |
| `Jwt__Key` | ✅ | ≥ 32 caracteres aleatorios (`openssl rand -base64 48`) |
| `Pagos__Wompi__LlavePublica`, `__LlavePrivada`, `__SecretoIntegridad`, `__SecretoEventos` | ✅ | Credenciales de Wompi |
| `Pagos__Wompi__BaseUrl` | — | `https://production.wompi.co/v1` (por defecto) o sandbox |
| `Cors__Origenes__0..n` | ✅ | Origen(es) del front Angular. Nunca `*` |
| `Urls__HostsPermitidosImagenes__0`, `Urls__HostsPermitidosDocumentos__0` | Recomendado | Host de Blob Storage. Si está vacío se acepta cualquier https, y se registra un aviso |
| `Redis__Habilitado`, `Redis__Conexion` | Con más de una instancia | Backplane de SignalR |
| `Proxy__Confiar` | En App Service | `true` **solo** si la app es inalcanzable salvo a través del proxy (VNet o restricciones de acceso). Sirve para obtener la IP real del cliente en el rate limiting |
| `Database__Inicializacion` | — | `Migrate` (por defecto), `EnsureCreated` o `None` |
| `AllowedHosts` | Recomendado | Dominio(s) de la API en lugar de `*` |
| `RateLimiting__AuthPorMinuto`, `__WebhookPorMinuto`, `__EscrituraPorMinuto`, `__GlobalPorMinuto` | — | Por defecto 10, 120, 20 y 300 por minuto |

**Fail-fast:** en `Production` la app **no arranca** si falta `Jwt:Key` (o es un placeholder), si falta la cadena de conexión o los secretos de Wompi, si el proveedor es `Simulado` o si la base es `InMemory`.

## 6. Base de datos y migraciones

```bash
dotnet tool run dotnet-ef migrations add <Nombre> -p TruekeBogotaSolidario.Datos -s TruekeBogotaSolidario.Datos -o Migraciones
dotnet tool run dotnet-ef migrations script --idempotent -p TruekeBogotaSolidario.Datos -s TruekeBogotaSolidario.Datos -o migracion.sql
```

- Datos se usa como proyecto de inicio porque contiene `TruekeDbContextFactory`; Presentacion no referencia EF.
- La prueba `MigracionesTests` falla si el modelo cambió sin migración.
- En producción, `Database:Inicializacion=Migrate` aplica las migraciones al arrancar. Con **varias instancias**, aplica el script idempotente como paso del despliegue y usa `None` en la app, para evitar migraciones concurrentes.

## 7. Despliegue en Azure

1. **Imagen:** `docker build -t <registro>.azurecr.io/trueke-api:<versión> backend` y `docker push`.
2. **App Service (Linux, contenedor):**
   - Puerto `WEBSITES_PORT=8080`.
   - *Health check* en `/health/ready`.
   - HTTPS Only, TLS 1.2 o superior.
   - Integración con la **VNet**.
   - Identidad administrada para ACR y Key Vault.
3. **Azure SQL** con endpoint privado en la VNet. **Azure Cache for Redis** con `Redis__Habilitado=true` y conexión TLS (`:6380,ssl=true,password=...`).
4. **Blob Storage** para imágenes y documentos. El front sube directamente con SAS de corta duración y la API solo recibe la URL. Configura `Urls__HostsPermitidos*`.
5. **Wompi:** registra la URL de eventos `https://<api>/api/v1/pagos/wompi/eventos`.
6. Define todos los secretos de la sección 5. Activa *WebSockets* en App Service para SignalR.

## 8. Contrato para el front Angular

- **Base:** `/api/v1`. Autenticación con `Authorization: Bearer <token>`. El token sale de `POST /auth/login` o `POST /auth/registrar` (`token`, `expiraUtc`, `usuario`). Expira a los 60 minutos y no hay refresh: al recibir un 401, vuelve a pedir login.
- Si cambias la clave, cierras sesiones o te cambian el rol, **todos** los tokens anteriores quedan revocados.
- **Errores:** siempre `application/problem+json`:
  ```json
  { "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1", "title": "Hay datos no válidos en la solicitud.",
    "status": 400, "traceId": "…", "errors": { "titulo": ["El campo Titulo debe tener entre 3 y 120 caracteres."] } }
  ```
  `title` es el mensaje para mostrar. `errors` aparece solo en las validaciones, con claves en camelCase. Un 500 nunca incluye detalles.
- **JSON:** camelCase y enums como texto (`"modo": "Trueke"`; los números no se aceptan). Las fechas van en **UTC ISO-8601 con `Z`** (`2026-10-02T15:04:05.123Z`).
- **Cabeceras:** `Location` en los 201 y `Retry-After` en los 429; ambas están expuestas por CORS.
- **Paginación:** `?pagina=1&tamano=20` (máximo 50) → `{ items, total, pagina, tamano }`.
- **Coordenadas:**
  - En las vistas públicas llegan redondeadas a 2 decimales (~1 km), con `coordenadasAproximadas: true`.
  - Las exactas solo llegan al dueño, a los moderadores o a quien tenga una solicitud aceptada.
  - `GET /publicaciones/cercanas?lat&lon&radioKm` (radio de 0,1 a 50 km) mide la distancia contra la posición que el usuario puede ver.
- **Tiempo real (SignalR):**
  ```ts
  new HubConnectionBuilder()
    .withUrl(`${api}/hubs/notificaciones`, { accessTokenFactory: () => token })
    .withAutomaticReconnect().build()
    .on('notificacion', (n: { tipo: string; mensaje: string; recursoId?: string; fechaUtc: string }) => …);
  ```
  - Tipos de notificación: `SolicitudNueva`, `SolicitudAceptada`, `SolicitudRechazada`, `SolicitudCancelada`, `PagoAprobado`, `PagoRechazado`, `PagoEnRevision`, `PublicacionOcultada`, `PublicacionRestaurada`, `ComentarioOcultado`, `VerificacionAprobada` y `VerificacionRechazada`.
  - La conexión se cierra cuando expira el token: reconecta con uno nuevo.
- **Generar el cliente TypeScript:** `npx @openapitools/openapi-generator-cli generate -i docs/openapi.json -g typescript-angular -o src/app/api`.
- **Regenerar `docs/openapi.json`** cuando cambie la API (una prueba lo exige): `ACTUALIZAR_OPENAPI=1 dotnet test --filter OpenApiTests`.
- **Seguridad del front:** el código de Angular siempre es descargable (F12). Por eso el front no guarda secretos, el build de producción va sin source maps y **el backend valida todo**. Muestra los textos de usuarios (comentarios, títulos) con interpolación de Angular, nunca con `innerHTML`.

## 9. Seguridad (resumen)

- **JWT:**
  - HS256 con algoritmo fijo (se rechaza `alg:none`), validación de issuer, audience, expiración y firma, y `ClockSkew` de 30 s.
  - El token lleva solo `sub`, `role` y `sv` (versión de seguridad, que permite revocarlo) y ningún dato personal.
- **Contraseñas:**
  - PBKDF2-SHA256 con 600.000 iteraciones y política de mayúscula, minúscula y número.
  - Bloqueo tras 5 intentos durante 15 minutos.
  - El mensaje de error es idéntico para un correo inexistente, una clave incorrecta o una cuenta bloqueada, con tiempos equivalentes.
- **Autorización:** `FallbackPolicy` exige sesión en todo. Lo anónimo es una lista blanca verificada por prueba. El rol de administración se vuelve a comprobar contra la base de datos, y el usuario actuante sale **siempre** del JWT.
- **Respuestas:** sin `ClaveHash`, `RowVersion`, correos de terceros ni ids internos de otros usuarios. Los nombres públicos se abrevian ("María F.").
- **Endurecimiento HTTP:**
  - HSTS y redirección a HTTPS fuera de Development; sin cabecera `Server`.
  - CSP `default-src 'none'`, `X-Frame-Options: DENY`, `nosniff`, `Referrer-Policy: no-referrer` y `Cache-Control: no-store`.
  - Cuerpo máximo de 1 MB.
  - Rate limiting por IP en auth (10/min), webhook (120/min) y global (300/min), y por usuario en escritura (20/min).
- **Pagos:**
  - Webhook con firma SHA-256 comparada en tiempo constante.
  - Se verifican monto, moneda y referencia contra la pasarela; el procesamiento es idempotente y transaccional.
  - El modo simulado está prohibido en producción.
- **Notificaciones:** SignalR solo envía al propio usuario; el hub no expone métodos al cliente.
- **Cadena de suministro:**
  - Solo nuget.org (`packageSourceMapping`).
  - El CI falla si `dotnet list package --vulnerable --include-transitive` encuentra algo.
  - Imagen Docker sin SDK y sin root.

## 10. Limitaciones conocidas

- **Sin refresh tokens:** la sesión dura 60 minutos (`Jwt:Minutos`).
- **Revocación con retraso:** con varias instancias, una revocación tarda hasta `Seguridad:SegundosCacheSesion` (30 s) en propagarse, porque la caché es local. Una conexión SignalR abierta sigue viva hasta que expira su token.
- **Token en la URL del hub:** para WebSockets, el token viaja en `?access_token=` (solo se acepta en `/hubs/*`). Desactiva el registro de *query strings* en los logs HTTP de App Service o del proxy.
- **Reembolsos manuales:** un pago aprobado cuyo beneficio ya no aplica queda en `RequiereRevision`.
- **Enumeración de cuentas:** el registro indica si un correo ya existe. Está mitigado con rate limiting.
- **Contacto entre usuarios:** el correo de la contraparte se comparte con ambos usuarios solo cuando la solicitud se acepta (`correoContacto`).
- **Sin subida de archivos en la API:** imágenes y documentos se suben a Blob Storage desde el front y aquí llega su URL https.

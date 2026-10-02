# Trueke Bogotá Solidario — Backend

API REST + tiempo real para la plataforma comunitaria de economía circular de Bogotá: **Trueke, Compra y Donación**, con moneda interna **Eco-Puntos**.

ASP.NET Core 8 · EF Core 8 · SQL Server · SignalR (+ Redis) · Azure Blob Storage · Wompi · SMTP · Google Identity · Docker · GitHub Actions · Azure App Service.
La arquitectura sigue el modelo C4 de [`docs/c4`](../docs/c4) y el contrato para el front está en [`docs/openapi.json`](../docs/openapi.json).

---

## 1. Arquitectura (un proyecto por capa)

```
TruekeBogotaSolidario.sln
├── TruekeBogotaSolidario.Datos          Entidades, DbContext, repositorios, migraciones, PoliticaEcoPuntos, PasswordHasher
├── TruekeBogotaSolidario.Negocio        Servicios, DTOs, reglas; pagos Wompi, correo, Google, Azure Blob, notificaciones (sin ASP.NET Core)
├── TruekeBogotaSolidario.Presentacion   Web API: controllers, JWT + cookie de refresco, SignalR, CORS, rate limiting, ProblemDetails
└── TruekeBogotaSolidario.Pruebas        xUnit: unitarias, integración (WebApplicationFactory), arquitectura y contrato
```

- **Presentacion → Negocio → Datos.** Negocio referencia Datos con `PrivateAssets="compile"`, así que Presentacion **no puede ver** repositorios ni el DbContext. Las pruebas de arquitectura lo verifican.
- **Flujo de un request:** `Controller` (HTTP ↔ DTO; el usuario sale del JWT) → `Servicio` (reglas y autorización de dominio) → `Repositorio` → `DbContext`. Cada operación confirma todos sus cambios en un solo `SaveChanges`.
- **Concurrencia optimista:** `rowversion` en Usuario, Publicacion, Solicitud, Pago, Comentario, Denuncia y SesionRefresh. Un conflicto se devuelve como **409**.
- **Notificaciones, correos y archivos:** se procesan **después** de confirmar la operación y nunca la hacen fallar.

## 2. Funcionalidades

| Módulo | Qué hace |
|---|---|
| Cuentas | Registro con consentimiento de datos, login, **Google**, verificación de correo, recuperación de contraseña, refresco de sesión, cerrar sesión (este dispositivo / todos) |
| Catálogo | Publicaciones (Trueke/Compra/Donación), categorías, búsqueda, **cercanas** (Haversine), imágenes en Azure Blob |
| Solicitudes | Solicitar, aceptar, rechazar o cancelar. Al aceptar se otorgan Eco-Puntos con tope anti-farmeo |
| **Chat** | Conversación privada por solicitud, en tiempo real. Reemplaza el intercambio de correos |
| Comentarios | Públicos por publicación. La moderación puede ocultarlos |
| **Notificaciones** | Bandeja persistente más envío en tiempo real (SignalR) |
| Eco-Puntos y pagos | Política pública, cotización con descuentos, pagos Wompi (destacar, verificar, Premium, Empresa, recarga) |
| **Moderación** | Denuncias de usuarios con cola agrupada; ocultar publicaciones, comentarios y mensajes; verificación de identidad; roles |
| **Habeas Data** | Exportar todos mis datos y eliminar mi cuenta (Ley 1581 de 2012) |

## 3. Roles

| Rol | Puede |
|---|---|
| **Invitado** (anónimo, nunca se guarda) | Leer el catálogo, el detalle, los comentarios, las publicaciones cercanas, las categorías y la política de Eco-Puntos; registrarse e iniciar sesión |
| **Cliente** | Todo lo anterior. Con **correo verificado**, además: publicar, solicitar, comentar, chatear, denunciar y pagar |
| **Administrador** | Todo lo del Cliente, más resolver denuncias, moderar contenido y verificaciones |
| **SuperUsuario** | Todo lo del Administrador, más cambiar roles (con auditoría; siempre queda al menos uno) |

El primer SuperUsuario se crea al arrancar con `Bootstrap:SuperUsuarioCorreo` y `Bootstrap:SuperUsuarioClave`. Después, quita esas variables.

## 4. Autenticación (cómo funciona)

```
Login / registro / Google ──► { token (JWT, 15 min), expiraUtc, usuario }  +  cookie __Secure-trueke_rt (HttpOnly, 14 días)
Petición normal          ──► Authorization: Bearer <token>
401 (token vencido)      ──► POST /auth/refrescar   (cookie + cabecera X-Trueke-Csrf: 1)  ──► token nuevo + cookie rotada
Cerrar sesión            ──► POST /auth/salir        (revoca el refresco de este dispositivo)
```

- **Token de acceso:** dura 15 minutos y solo lleva `sub`, `role` y `sv`. Angular lo guarda **solo en memoria**, nunca en `localStorage`.
- **Refresco:** viaja en una cookie que JavaScript no puede leer, limitada a `/api/v1/auth`. En la BD solo se guarda su hash SHA-256.
  - **Rotación:** cada uso entrega uno nuevo. Reusar uno viejo se interpreta como robo y corta toda la sesión. Si dos pestañas refrescan a la vez (ventana de 30 s), la segunda recibe 409 y solo tiene que reintentar.
  - **Vida máxima:** 30 días desde el login.
- **Revocación inmediata:** cambiar o restablecer la clave, "cerrar todas las sesiones", cambiar el rol o eliminar la cuenta invalida todos los tokens de acceso (`sv`) y de refresco.
- **Correo verificado:** sin verificar se puede navegar y editar el perfil, pero publicar, solicitar, comentar, chatear, denunciar y pagar devuelven **403**.
- **Recuperación de contraseña:** `olvide-clave` responde siempre 202 (no revela si la cuenta existe). El enlace dura 30 minutos, es de un solo uso, desbloquea la cuenta y cierra todas las sesiones.
- **Google:** el backend valida el *ID token* (firma de Google, emisor, audiencia = nuestro Client ID, vigencia y `email_verified`) y emite **su propio** JWT.
  - Si ya existía una cuenta con ese correo, se vincula.
  - Si esa cuenta tenía clave pero nunca verificó el correo, la clave se elimina. Así nadie puede "pre-registrar" el correo de otra persona.
- **Contraseñas:** PBKDF2-SHA256 con 600.000 iteraciones; mayúscula, minúscula y número. Bloqueo de 15 minutos tras 5 intentos, con un mensaje idéntico para cualquier fallo.

## 5. Economía Eco-Puntos

La fuente única es `Datos/Common/PoliticaEcoPuntos.cs`, y `GET /api/v1/eco-puntos/politica` la lee en vivo.

| Acción | Eco-Puntos | Reputación |
|---|---|---|
| Registro | 10 | — |
| Compra | 5 | +0,05 |
| Trueke | 10 | +0,10 |
| Donación | 20 | +0,20 |

- **Tope de reputación:** 5,0.
- **Anti-farmeo:** solo las primeras 5 transacciones de un usuario en 24 h dan puntos y reputación.
- **Beneficios:**

  | Beneficio | Precio | Descuento con Eco-Puntos |
  |---|---|---|
  | Destacar (7 días) | $6.000 | 25 % con ≥200 pts, 40 % con ≥500 pts |
  | Verificar cuenta | $20.000 | 20 % con ≥200 pts, 35 % con ≥500 pts |
  | Premium | $15.000/mes | 3 destacados gratis + 15 % extra en los descuentos |
  | Empresa | $50.000/mes | — |

- **Recarga:** 100 COP = 1 Eco-Punto. Los puntos **nunca** vuelven a pesos. Wompi solo recibe dinero hacia la plataforma.
- **Flujo de pago:**
  1. `POST /pagos/iniciar` valida todo **antes** de cobrar.
  2. El webhook de Wompi llega firmado. La API consulta la transacción a Wompi, que es la fuente de verdad, y procesa de forma idempotente.
  3. Un reconciliador resuelve o expira los pagos pendientes.

## 6. Ejecutar en local

Requisitos: SDK de .NET 8 (`global.json` fija la línea 8.0.4xx).

```bash
cd backend
dotnet tool restore
dotnet build TruekeBogotaSolidario.sln -warnaserror
dotnet test
dotnet run --project TruekeBogotaSolidario.Presentacion
```

En **Development**:
- La base es InMemory; pagos y correo son **simulados**. Los correos se escriben en la consola, desde donde se copia el enlace de verificación. Para aprobar un pago: `POST /pagos/{ref}/simular?aprobado=true`.
- CORS permite `http://localhost:4200`, y Swagger está en `https://localhost:7180/swagger`.
- **Imágenes:** levanta Azurite y activa el almacenamiento:
  ```bash
  docker run -d -p 10000:10000 mcr.microsoft.com/azure-storage/azurite azurite-blob --blobHost 0.0.0.0 --skipApiVersionCheck
  dotnet user-secrets set "Almacenamiento:CadenaConexion" "UseDevelopmentStorage=true" --project TruekeBogotaSolidario.Presentacion
  ```
- **Google:** `dotnet user-secrets set "Google:ClientId" "<client-id>.apps.googleusercontent.com" --project TruekeBogotaSolidario.Presentacion`.
- Sin `Jwt:Key` se genera una clave efímera, y las sesiones se pierden al reiniciar.

### Con Docker (API + SQL Server + Redis)

```bash
cp .env.example .env
docker compose up --build
```

Antes de levantarlo, completa los valores de `.env`. La API queda en `http://localhost:8080`, con `/health/live` y `/health/ready`. Arranca en `Staging` (permite correo y pagos simulados). SQL Server y Redis no se exponen al host.

## 7. Configuración y secretos

Todo se configura por variables de entorno (en Azure: *App Settings* o referencias a Key Vault) con `__` como separador. **Ningún secreto se versiona.**

| Variable | Obligatoria en producción | Descripción |
|---|---|---|
| `ConnectionStrings__TruekeDb` | ✅ | Azure SQL |
| `Jwt__Key` | ✅ | ≥ 32 caracteres aleatorios (`openssl rand -base64 48`) |
| `Pagos__Wompi__LlavePublica`, `__LlavePrivada`, `__SecretoIntegridad`, `__SecretoEventos` | ✅ | Credenciales de Wompi |
| `Correo__Smtp__Host`, `__Puerto`, `__Usuario`, `__Clave`, `Correo__Remitente` | ✅ | SMTP con TLS (Azure Communication Services, SendGrid…) |
| `Urls__Frontend` | ✅ | URL **https** del front. Los enlaces de los correos apuntan ahí |
| `Almacenamiento__ServicioUrl` | ✅ | `https://<cuenta>.blob.core.windows.net` (Managed Identity con rol *Storage Blob Data Contributor* y *Delegator*) |
| `Cors__Origenes__0..n` | ✅ | Origen(es) del front. Nunca `*` |
| `Google__ClientId` | Si se usa Google | OAuth Client ID tipo "Aplicación web" |
| `Auth__CookieSameSite` | — | `Strict` (por defecto; front y API en el mismo sitio) o `None` (sitios distintos) |
| `Redis__Habilitado`, `Redis__Conexion` | Con más de una instancia | Backplane de SignalR |
| `Proxy__Confiar` | En App Service | `true` **solo** si la app es inalcanzable salvo a través del proxy (VNet o restricciones de acceso) |
| `Legal__VersionPoliticaDatos` | — | Versión de la política que acepta el usuario al registrarse (`2026-10`) |
| `Database__Inicializacion` | — | `Migrate` (por defecto), `EnsureCreated` o `None` |
| `AllowedHosts` | Recomendado | Dominio(s) de la API en lugar de `*` |
| `RateLimiting__*PorMinuto` | — | Auth 10, Refresco 60, Escritura 20, Webhook 120, Global 300 |

**Fail-fast:** en `Production` la app **no arranca** si falta cualquiera de las obligatorias, o si el correo o los pagos están en `Simulado`, la base en `InMemory` o `Urls:Frontend` no es https.

## 8. Base de datos y migraciones

```bash
dotnet tool run dotnet-ef migrations add <Nombre> -p TruekeBogotaSolidario.Datos -s TruekeBogotaSolidario.Datos -o Migraciones
dotnet tool run dotnet-ef migrations script --idempotent -p TruekeBogotaSolidario.Datos -s TruekeBogotaSolidario.Datos -o migracion.sql
```

- `MigracionesTests` falla si el modelo cambió sin migración.
- Con varias instancias, aplica el script idempotente como paso del despliegue y usa `Database__Inicializacion=None`.
- Una tarea en segundo plano purga cada hora los refrescos y enlaces vencidos, y las notificaciones leídas de más de 90 días.

## 9. Despliegue en Azure

1. **Imagen:** `docker build -t <registro>.azurecr.io/trueke-api:<versión> backend` y `docker push`.
2. **App Service (Linux, contenedor):**
   - `WEBSITES_PORT=8080`;
   - *Health check* en `/health/ready`;
   - HTTPS Only y TLS ≥ 1.2;
   - **WebSockets activados** (para SignalR);
   - integración con la VNet;
   - identidad administrada para ACR, Key Vault y Storage.
3. **Datos y caché:** Azure SQL con endpoint privado; Azure Cache for Redis (`Redis__Conexion=<host>:6380,ssl=true,password=...`).
4. **Storage:**
   - contenedor `imagenes` con acceso anónimo **solo de lectura de blobs**;
   - contenedor `documentos` **privado**;
   - CORS de la cuenta: `PUT` desde el origen del front, con las cabeceras `x-ms-blob-type` y `content-type`.
5. **Dominio:** usa el mismo para front y API (`app.` y `api.`) para que la cookie `SameSite=Strict` funcione.
6. **Wompi:** registra el webhook `https://<api>/api/v1/pagos/wompi/eventos`.
7. **Google Cloud:** agrega el origen del front a los *Authorized JavaScript origins*.

## 10. Contrato para el front Angular

- **Base:** `/api/v1`. Errores siempre en `application/problem+json`:
  ```json
  { "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1", "title": "Hay datos no válidos en la solicitud.",
    "status": 400, "traceId": "…", "errors": { "titulo": ["El campo Titulo debe tener entre 3 y 120 caracteres."] } }
  ```
  `title` es el mensaje para mostrar; un 500 nunca trae detalles.
- **JSON:** camelCase y enums como texto. Las fechas van en **UTC ISO-8601 con `Z`**.
- **Cabeceras:** `Location` en los 201 y `Retry-After` en los 429.
- **Sesión:**
  - El token se guarda en memoria y el interceptor añade `Authorization`.
  - Al recibir un 401, el front llama **una vez** a `POST /auth/refrescar` con `withCredentials: true` y la cabecera `X-Trueke-Csrf: 1`, y reintenta la petición original.
  - Al recargar la página, el front llama a `refrescar` para recuperar la sesión.
  - En desarrollo, usa el **proxy** de Angular (`proxy.conf.json` hacia `https://localhost:7180`, con `secure: false`) para que front y API compartan origen.
- **Google:** con Google Identity Services se obtiene el `credential` (ID token) y se envía a `POST /auth/google { idToken, aceptoPoliticaDatos }`. Responde **201** si creó la cuenta y 200 si ya existía.
- **Enlaces de correo** (`/verificar-correo?token=…` y `/restablecer-clave?token=…` en el front):
  - la página lee el token, llama a `POST /auth/verificar-correo` o `POST /auth/restablecer-clave` y **borra el token de la URL** (`history.replaceState`);
  - el front debe enviar `Referrer-Policy: no-referrer`.
- **Registro:** casilla obligatoria de autorización de tratamiento de datos (`aceptoPoliticaDatos: true`), con un enlace a la política.
- **Imágenes:**
  1. `POST /archivos/subidas { tipo: "Imagen", contentType: "image/jpeg", tamanoBytes }`.
  2. `PUT` del archivo a `urlSubida` con las `cabeceras` indicadas, directo a Azure, antes de 5 minutos.
  3. Usar `urlArchivo` como `imagenUrl` al crear la publicación.

  Formatos: JPG, PNG o WEBP de hasta 5 MB (los documentos también admiten PDF).
- **Coordenadas:** en público llegan redondeadas a 2 decimales (`coordenadasAproximadas: true`). Las exactas solo llegan al dueño, a los moderadores o con una solicitud aceptada.
- **Tiempo real (SignalR):**
  ```ts
  const hub = new HubConnectionBuilder()
    .withUrl(`${api}/hubs/notificaciones`, { accessTokenFactory: () => auth.tokenActual() })
    .withAutomaticReconnect().build();
  hub.on('notificacion', (n: { id: string; tipo: string; mensaje: string; recursoId?: string; fechaUtc: string; leida: boolean }) => …);
  hub.on('mensaje', (m: { id: string; conversacionId: string; esMio: boolean; texto: string; fechaUtc: string; leido: boolean; oculto: boolean }) => …);
  ```
  - La conexión se cierra cuando expira el token; reconecta después de refrescar.
  - Al reconectar, el front recupera lo pendiente con `GET /notificaciones?soloNoLeidas=true` y `GET /conversaciones`.
- **Chat:** `GET /conversaciones`; `GET /conversaciones/{id}/mensajes?antesDe=&tamano=` (paginado con cursor); `POST /conversaciones/{id}/mensajes`; `POST /conversaciones/{id}/leer`. Cada solicitud trae su `conversacionId`.
- **Denuncias:** `POST /denuncias { tipo, objetivoId, motivo, detalle }`. Un segundo intento sobre el mismo objetivo devuelve **409** ("ya denunciaste").
- **Mis datos:** `GET /usuarios/yo/datos` (descargar JSON) y `POST /usuarios/yo/eliminar { confirmacion: "ELIMINAR", clave | googleIdToken }`.
- **Seguridad del front:**
  - Los textos de usuarios (títulos, comentarios, mensajes) se muestran con interpolación de Angular, **nunca** con `innerHTML`.
  - Sin secretos en el código y sin source maps en producción.
  - El backend valida todo.
- **Cliente TypeScript:** `npx @openapitools/openapi-generator-cli generate -i docs/openapi.json -g typescript-angular -o src/app/api`. Si cambia la API, se regenera el contrato con `ACTUALIZAR_OPENAPI=1 dotnet test --filter OpenApiTests`.

## 11. Seguridad (resumen)

- **Autenticación:**
  - JWT HS256 de 15 minutos con algoritmo fijo (se rechaza `alg:none`);
  - refresco rotativo en una cookie HttpOnly/Secure/SameSite con detección de reuso y anti-CSRF (cabecera más Origin);
  - revocación por versión de seguridad;
  - Google validado en el servidor;
  - vinculación de cuentas segura frente al pre-registro.
- **Autorización:**
  - `FallbackPolicy` exige sesión en todo y lo anónimo es una lista blanca verificada por prueba;
  - los roles se vuelven a comprobar contra la base de datos;
  - el usuario actuante sale **siempre** del JWT;
  - los recursos ajenos (chats, notificaciones, solicitudes) responden 404.
- **Privacidad:**
  - las respuestas no incluyen hashes, `rowversion`, correos de terceros ni ids de otros usuarios;
  - los nombres públicos se abrevian y las coordenadas se aproximan;
  - los usuarios hablan por un **chat interno**, nunca por correo;
  - los documentos de identidad se guardan en un contenedor privado, los moderadores los ven con un enlace de 5 minutos y **se borran al resolver la verificación**;
  - Habeas Data: consentimiento, exportación y supresión;
  - purga periódica de datos vencidos.
- **Archivos:**
  - las subidas usan SAS de 5 minutos, solo crear/escribir, sobre un blob con nombre elegido por el servidor;
  - después se verifica dueño, tamaño (≤ 5 MB), tipo y *magic bytes*; el contenido disfrazado se borra;
  - SVG y HTML están prohibidos.
- **Endurecimiento HTTP:**
  - HSTS y HTTPS, sin cabecera `Server`;
  - CSP `default-src 'none'`, `X-Frame-Options: DENY`, `nosniff`, `no-referrer`, `no-store`;
  - cuerpo máximo de 1 MB;
  - rate limiting por IP y por usuario;
  - errores ProblemDetails sin detalles internos.
- **Pagos:** webhook con firma en tiempo constante, verificación contra la pasarela, idempotencia y transacciones; modo simulado prohibido en producción.
- **Abuso:**
  - topes de comentarios (20/h), mensajes (120/h), denuncias (10/día), enlaces de correo (3/h), pagos pendientes (5/h) y solicitudes pendientes (10);
  - las denuncias se agrupan para moderación.
- **Cadena de suministro:**
  - solo nuget.org (`packageSourceMapping`);
  - el CI falla con paquetes vulnerables;
  - dependencias Azure fijadas a la línea 8.x;
  - imagen Docker sin SDK y sin root.

## 12. Limitaciones conocidas

- **Propagación de la revocación:** con varias instancias, revocar un token de acceso tarda hasta `Seguridad:SegundosCacheSesion` (30 s), porque la caché es local. Una conexión SignalR abierta sigue viva hasta que expira su token (≤ 15 min).
- **Token en la URL del hub:** para WebSockets, el token viaja en `?access_token=` (solo se acepta en `/hubs/*`). Desactiva el registro de *query strings* en los logs HTTP del proxy o App Service.
- **"Aceptar" completa el intercambio:** no hay una confirmación de entrega por ambas partes. El tope diario de puntos limita el abuso entre cuentas coludidas.
- **Enumeración de cuentas:**
  - el registro indica si el correo ya existe (mitigado con rate limiting);
  - `olvide-clave` puede tardar unos milisegundos más con un correo existente.
- **Pagos cobrados sin beneficio:** si el beneficio ya no aplica, el pago queda en `RequiereRevision` y se reembolsa manualmente.
- **Archivos huérfanos:** los archivos subidos que nunca se usan quedan en Blob. Configura una regla de *lifecycle management* (por ejemplo, borrar los blobs de más de 30 días sin uso en `documentos`).
- **Correo en memoria:** la cola de correo vive en memoria; un reinicio puede perder un correo en tránsito, y el usuario puede pedir "reenviar".

# Trueke Bogotá Solidario — Backend

API REST + tiempo real para la plataforma comunitaria de economía circular de **Colombia** (nació en Bogotá y opera en los 1.122 municipios del país): **Trueke, Compra y Donación**, con moneda interna **Eco-Puntos**.

ASP.NET Core 8 · EF Core 8 · SQL Server · SignalR (+ Redis) · Azure Blob Storage · Wompi · SMTP · Google Identity · reCAPTCHA v3 · TOTP (2FA) · Application Insights · Docker · GitHub Actions · Azure App Service.
La arquitectura sigue el modelo C4 de [`docs/c4`](../docs/c4) y el contrato para el front está en [`docs/openapi.json`](../docs/openapi.json).

---

## 1. Arquitectura (un proyecto por capa)

```
TruekeBogotaSolidario.sln
├── TruekeBogotaSolidario.Datos          Entidades, DbContext, repositorios, migraciones, PoliticaEcoPuntos, PasswordHasher
├── TruekeBogotaSolidario.Negocio        Servicios, DTOs y reglas; integraciones (Wompi, SMTP, Google, reCAPTCHA, Azure Blob)
├── TruekeBogotaSolidario.Presentacion   Web API: controllers, JWT + cookie de refresco, SignalR, CORS, rate limiting, ProblemDetails
└── TruekeBogotaSolidario.Pruebas        xUnit: unitarias, integración (WebApplicationFactory), arquitectura, contrato y SQL Server real
```

- **Dependencias entre capas:** Presentacion → Negocio → Datos. Negocio referencia Datos con `PrivateAssets="compile"`, así que un controller **no puede** tocar repositorios ni el DbContext. Las pruebas de arquitectura lo verifican.
- **Concurrencia optimista:** `rowversion` en las entidades críticas; un conflicto se devuelve como **409**. Cada operación se confirma en un solo `SaveChanges`.
- **Efectos secundarios:** notificaciones, correos y archivos se procesan **después** de confirmar la operación y nunca la hacen fallar.

## 2. Funcionalidades

| Módulo | Qué hace |
|---|---|
| Cuentas | Registro con consentimiento de datos, login, **Google**, **2FA** con app autenticadora, verificación de correo, recuperación de contraseña, refresco de sesión, **reCAPTCHA v3** |
| Perfil público | `GET /usuarios/{id}/perfil`, `/publicaciones` y `/calificaciones` (sin correo ni nombre completo) |
| Catálogo | Publicar, **editar**, **hasta 5 fotos (sin GPS ni metadatos)**, **estado del producto** (nuevo, como nuevo, usado, usado con detalles, reparado, para repuestos), búsqueda, filtros (categoría, modo, estado, **departamento/municipio**, localidad, rango de precio, solo verificados), **orden** (recientes/precio), **cercanas** (Haversine), **favoritos**, **vitrina de destacadas** |
| Escala nacional | Catálogo oficial DANE-DIVIPOLA (33 departamentos, 1.122 municipios) embebido; `GET /ubicaciones/...`; usuarios y publicaciones con municipio |
| Monetización | Destacar, verificar, **Premium** y **Empresa** (destacados gratis, descuentos, más publicaciones, perfil comercial con NIT, estadísticas diarias), **impulsar con Eco-Puntos**, recargas; **estadísticas por publicación** (vistas, favoritos, solicitudes) |
| Facturación | Factura por cada pago aprobado (misma transacción), IVA discriminado, datos del comprador o consumidor final, cola de emisión y CSV para el equipo |
| PQR | Peticiones, quejas, reclamos, sugerencias, **retracto** (5 días hábiles) y **reversión del pago**, con radicado y plazo legal de 15 días hábiles |
| Finanzas | Tablero de ingresos (cobrado, reembolsado, neto, ingreso recurrente mensual) y exportación contable CSV (solo SuperUsuario) |
| Intercambio | Solicitar → aceptar → coordinar por **chat** → **ambos confirman la entrega** → Completada (Eco-Puntos) → **calificar**; o "no concretada" |
| Comentarios | Públicos por publicación, moderables |
| Notificaciones | Bandeja persistente más tiempo real (SignalR) |
| Eco-Puntos y pagos | Política pública, cotización con descuentos, pagos Wompi (destacar, verificar, Premium, Empresa, recarga) |
| Moderación | Denuncias (publicaciones, comentarios, mensajes, calificaciones, usuarios), ocultar contenido, **suspender/reactivar cuentas**, **buscar usuarios**, verificación de identidad, **pagos en revisión y reembolsos**, roles |
| Habeas Data | Exportar todos mis datos y eliminar mi cuenta (Ley 1581 de 2012) |
| Operación | Health checks, Application Insights, purga y cierre automático periódicos |

## 3. Roles

| Rol | Puede |
|---|---|
| **Invitado** (anónimo, nunca se guarda) | Catálogo, detalle, comentarios, cercanas, perfiles públicos, categorías, política de Eco-Puntos y configuración pública; registrarse e iniciar sesión |
| **Cliente** | Con **correo verificado**: publicar, editar, solicitar, chatear, confirmar entregas, calificar, comentar, denunciar, favoritos y pagar |
| **Administrador** | Lo del Cliente más moderación: denuncias, ocultar contenido, suspender Clientes, verificaciones y pagos. **Requiere sesión con 2FA** |
| **SuperUsuario** | Lo del Administrador más cambiar roles, suspender Administradores y registrar reembolsos. **Requiere sesión con 2FA** |

Nadie puede suspender a un SuperUsuario, y siempre queda al menos uno. El primero se crea con `Bootstrap:SuperUsuarioCorreo` y `Bootstrap:SuperUsuarioClave`; quita esas variables después del primer arranque.

## 4. Autenticación

```
Login / registro / Google ─► { token (JWT 15 min), expiraUtc, usuario }  +  cookie __Secure-trueke_rt (HttpOnly, 14 días)
Con 2FA activo            ─► 1.º intento sin código → 401 { codigo: "2fa_requerido" } → reenviar con codigoDosFactores
Petición normal           ─► Authorization: Bearer <token>
401 (token vencido)       ─► POST /auth/refrescar (cookie + X-Trueke-Csrf: 1) → token nuevo + cookie rotada
```

- **Token de acceso:** dura 15 minutos y lleva solo `sub`, `role`, `sv` (revocación) y `amr` (`pwd` o `mfa`). Angular lo guarda **en memoria**.
- **Refresco:** viaja en una cookie HttpOnly, Secure y SameSite, limitada a `/api/v1/auth`. En la BD solo se guarda su hash.
  - **Rotación:** cada uso entrega uno nuevo; el reuso de uno viejo corta toda la sesión.
  - **Dos pestañas a la vez:** ventana de 30 s que responde 409, para reintentar.
  - **Vida máxima:** 30 días. La marca de 2FA se conserva al refrescar.
- **Verificación en dos pasos (TOTP):**
  - **Activación:** `configurar` devuelve el QR (`uriOtpauth`) → `activar` con el primer código. La respuesta trae 10 códigos de recuperación de un solo uso y una sesión nueva; las demás se cierran.
  - **Secreto:** cifrado con AES-256-GCM, con la llave `Seguridad:ClaveCifrado` fuera de la BD.
  - **Códigos:** no se aceptan repetidos, y un código erróneo cuenta como intento fallido (bloqueo tras 5).
  - **Administración:** sus funciones exigen una sesión con `amr=mfa`. Sin ella responden 403 `{ codigo: "2fa_requerido_admin" }`.
- **reCAPTCHA v3:**
  - aplica en registro (`registro`), login (`login`) y olvidé mi clave (`olvide_clave`);
  - el backend verifica el token con Google, la acción y un puntaje ≥ 0,5;
  - si Google no responde, rechaza la petición (falla cerrada);
  - es obligatorio en producción.
- **Revocación inmediata:** cambiar o restablecer la clave, cerrar sesiones, cambiar el rol, activar o desactivar 2FA, suspender o eliminar la cuenta invalida todos los tokens.
- **Correo verificado:** sin él solo se puede navegar y editar el perfil.
- **Recuperación de contraseña:** `olvide-clave` responde siempre 202. El enlace dura 30 minutos, es de un solo uso, desbloquea la cuenta y cierra las sesiones.
- **Google:**
  - el *ID token* se valida en el servidor y luego se emite nuestro JWT;
  - una cuenta existente con el mismo correo se vincula; si nunca verificó el correo, su clave se elimina;
  - si la cuenta tiene 2FA, también se exige el código.
- **Contraseñas:** PBKDF2-SHA256 con 600.000 iteraciones; mayúscula, minúscula y número. Bloqueo de 15 minutos tras 5 fallos, con un mensaje idéntico para cualquier fallo.
- **Suspensión:** solo tras una clave correcta, el login responde 403 con el motivo y la fecha de fin.

## 5. Intercambio y Eco-Puntos

```
Pendiente ──aceptar──► Aceptada (coordinan por chat) ──ambos confirman──► Completada ──► calificar (30 días)
    │                      │ └─ una confirmación + 7 días ─► Completada (automático)
    │                      ├─ nadie confirma en 30 días ──► NoConcretada (automático)
    │                      └─ "no-concretada" (si nadie confirmó) ─► NoConcretada
    └─ rechazar / cancelar
```

Los **Eco-Puntos y la reputación se otorgan solo al completarse**. La fuente única de la política es `Datos/Common/PoliticaEcoPuntos.cs`, y `GET /eco-puntos/politica` la lee en vivo.

| Acción | Eco-Puntos | Reputación |
|---|---|---|
| Confirmar el correo (cuenta nueva, una vez) | 10 | — |
| Compra | 5 | +0,05 |
| Trueke | 10 | +0,10 |
| Donación | 20 | +0,20 |

- **Topes:** reputación máxima 5,0.
- **Anti-farmeo:**
  - solo las primeras 5 transacciones en 24 h dan puntos;
  - **la misma pareja** suma puntos y reputación una sola vez cada 30 días (en cualquier sentido);
  - solo la primera calificación de una persona a otra en 30 días cuenta en el promedio;
  - **correo canónico único**: `ana.perez+x@gmail.com` y `anaperez@gmail.com` son la misma cuenta;
  - correos desechables bloqueados (lista + `Seguridad:DominiosCorreoBloqueados`);
  - el bono de bienvenida llega al confirmar el correo, no al registrarse.
- **Uso directo de los puntos:** **Impulsar** (20 pts) sube la publicación al primer lugar de "Más recientes", una vez cada 24 h. Es lo único que se paga 100 % con puntos (no tiene precio en pesos).
- **Beneficios (IVA incluido):** Destacar $6.000 (25 % o 40 % con ≥200 o ≥500 pts) · Verificar $20.000 (20 % o 35 %).

| Plan | Precio | Destacados gratis/mes | Descuento extra | Publicaciones activas | Otros |
|---|---|---|---|---|---|
| Individual | Gratis | — | — | 50 | — |
| Premium | $15.000/mes | 3 | 15 % | 150 | Gráfica diaria de vistas |
| Empresa | $50.000/mes | 10 | 20 % | 1.000 | Nombre comercial y NIT públicos, ventas sin el límite del art. 53, gráfica diaria |

- **Planes:** duran 30 días y **no se renuevan solos**; 3 días antes del vencimiento se envía correo + notificación. Renovar antes suma los días al final del periodo.
- **Vendedores habituales (Ley 1480, art. 53):** más de 5 ventas activas exigen identidad verificada o plan Empresa.
- **Recarga:** 100 COP = 1 Eco-Punto. Los puntos nunca vuelven a pesos.
- **Pagos:**
  - Wompi solo recibe dinero hacia la plataforma.
  - Webhook firmado; la transacción se consulta a la pasarela y se procesa de forma idempotente.
  - Un pago cobrado cuyo beneficio ya no aplica queda en `RequiereRevision` y **se avisa a los moderadores**. El SuperUsuario reembolsa en el panel de Wompi y lo registra con `POST /admin/pagos/{ref}/reembolsado`.
  - **Registrar un reembolso revierte el beneficio**: resta el mes de plan, retira los puntos de la recarga, quita el destacado o cancela la verificación pendiente, y anula la factura (o la marca para nota crédito si ya se emitió).

### 5.1 Facturación electrónica, IVA y PQR

- **Factura por cada pago aprobado**, creada en la misma transacción que aprueba el pago (`Facturas`, una por pago). Los precios **incluyen IVA**: se guarda base, IVA y total (`Facturacion:IvaPorcentaje`, 19 % por defecto; `Facturacion:ResponsableIva=false` si la empresa no es responsable de IVA).
- **Comprador:** los datos de `PUT /cuenta/facturacion` (CC, CE, NIT con dígito de verificación validado, o pasaporte) o, si no hay, "consumidor final" (`222222222222`).
- **Modo `Manual` (producción):** la factura queda `Pendiente`. El equipo la emite en el portal gratuito de la DIAN o en su proveedor tecnológico (Alegra, Siigo, etc.) usando `GET /admin/facturas.csv`, y registra número y CUFE con `POST /admin/facturas/{id}/emitida`. El usuario recibe la notificación. *(Integrar la API de un proveedor para emitir automáticamente es el siguiente paso cuando tengan contrato con uno.)*
- **Modo `Simulado`:** solo desarrollo (la tarea horaria las marca emitidas con datos ficticios). Producción no arranca con él.
- **PQR (`POST /pqr`):** radicado `PQR-AAAAMMDD-XXXXXX`, acuse por correo, plazo de 15 días hábiles, bandeja `GET /admin/pqr` ordenada por vencimiento y respuesta por correo. El **retracto** se valida contra un pago propio aprobado dentro de los 5 días hábiles; la **reversión** contra un pago propio aprobado.

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
- La base es InMemory; pagos y correo son simulados. Los correos se escriben en la consola, donde se copia el enlace de verificación.
- Sin reCAPTCHA y sin exigir 2FA a administradores.
- `Jwt:Key` y `Seguridad:ClaveCifrado` efímeras si no se definen.
- CORS para `http://localhost:4200` y Swagger en `/swagger`.
- **Imágenes:**
  ```bash
  docker run -d -p 10000:10000 mcr.microsoft.com/azure-storage/azurite azurite-blob --blobHost 0.0.0.0 --skipApiVersionCheck
  dotnet user-secrets set "Almacenamiento:CadenaConexion" "UseDevelopmentStorage=true" --project TruekeBogotaSolidario.Presentacion
  ```
- **Google / reCAPTCHA en dev:** `dotnet user-secrets set "Google:ClientId" "..."`; `Captcha:ClaveSitio` y `Captcha:ClaveSecreta` (las claves de prueba de Google sirven).
- **Pruebas contra SQL Server real:** con Docker instalado, `dotnet test` también las ejecuta. Sin Docker se marcan como *omitidas*; el CI las exige.

### Con un SQL Server instalado en el PC (datos persistentes)

```bash
dotnet tool run dotnet-ef database update -p TruekeBogotaSolidario.Datos -s TruekeBogotaSolidario.Datos --connection "Server=.\<INSTANCIA>;Database=TruekeBogotaSolidario;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True"
dotnet user-secrets set "ConnectionStrings:TruekeDb" "<la misma cadena>" --project TruekeBogotaSolidario.Presentacion
dotnet run --project TruekeBogotaSolidario.Presentacion --launch-profile SqlServerLocal
```

El perfil `SqlServerLocal` usa SQL Server con pagos y correo simulados. Opcional: `Bootstrap:SuperUsuarioCorreo` y `Bootstrap:SuperUsuarioClave` en user-secrets para tener un administrador. En VS Code: *Terminal → Ejecutar tarea → "Trueke: todo (SQL Server local)"*.

### Con Docker (API + SQL Server + Redis)

```bash
cp .env.example .env
docker compose up --build
```

Antes de levantarlo, completa los valores de `.env`. La API queda en `http://localhost:8080` y arranca en `Staging`: correo y pagos simulados permitidos, base y Redis reales.

## 7. Configuración y secretos

Todo se configura por variables de entorno (Azure: *App Settings* o referencias a Key Vault) con `__` como separador. **Ningún secreto se versiona.**

| Variable | Obligatoria en producción | Descripción |
|---|---|---|
| `ConnectionStrings__TruekeDb` | ✅ | Azure SQL |
| `Jwt__Key` | ✅ | ≥ 32 caracteres aleatorios (`openssl rand -base64 48`) |
| `Seguridad__ClaveCifrado` | ✅ | 32 bytes en base64 (`openssl rand -base64 32`). **No la cambies**: cifra los secretos de 2FA |
| `Captcha__ClaveSitio`, `Captcha__ClaveSecreta` | ✅ | reCAPTCHA v3 (google.com/recaptcha/admin) |
| `Pagos__Wompi__LlavePublica`, `__LlavePrivada`, `__SecretoIntegridad`, `__SecretoEventos` | ✅ | Wompi |
| `Correo__Smtp__Host`, `__Puerto`, `__Usuario`, `__Clave`, `Correo__Remitente` | ✅ | SMTP con TLS |
| `Urls__Frontend` | ✅ | URL **https** del front (enlaces de los correos) |
| `Almacenamiento__ServicioUrl` | ✅ | `https://<cuenta>.blob.core.windows.net` (Managed Identity) |
| `Cors__Origenes__0..n` | ✅ | Origen(es) del front |
| `Google__ClientId` | Si se usa Google | OAuth Client ID tipo "Aplicación web" |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Recomendado | Monitoreo (se registra un aviso si falta) |
| `Seguridad__ExigirDosFactoresModeradores` | — | `true` por defecto |
| `Auth__CookieSameSite` | — | `Strict` (mismo sitio) o `None` (sitios distintos) |
| `Redis__Habilitado`, `Redis__Conexion` | Con más de una instancia | Backplane de SignalR |
| `Proxy__Confiar` | En App Service | `true` **solo** si la app es inalcanzable salvo por el proxy |
| `Proxy__RedesConfiables__0..n`, `Proxy__ProxiesConfiables__0..n` | Recomendado | CIDR/IP del proxy o Front Door: solo de ahí se acepta `X-Forwarded-For` |
| `Facturacion__Modo` | ✅ (`Manual`) | `Manual` en producción; `Simulado` está prohibido |
| `Facturacion__ResponsableIva`, `Facturacion__IvaPorcentaje` | — | `true` / `19` por defecto (confírmalo con tu contador) |
| `Legal__CorreoContacto` | Recomendado | Correo de atención (PQR) que se muestra a los usuarios |
| `Seguridad__DominiosCorreoBloqueados__0..n` | — | Dominios extra que no pueden registrarse |
| `Legal__VersionPoliticaDatos` | — | Versión de la política aceptada al registrarse |
| `Database__Inicializacion` | — | `Migrate` (por defecto), `EnsureCreated` o `None` |
| `RateLimiting__*PorMinuto` | — | Auth 10 · Refresco 60 · Escritura 20 · Webhook 120 · Global 300 |

**Fail-fast:** en `Production` la app **no arranca** si falta cualquiera de las obligatorias, si el correo, los pagos o la facturación están en `Simulado`, si la base es `InMemory` o si `Urls:Frontend` no es https.

### 7.1 Dónde se obtiene cada llave

Ninguna llave se escribe en el código ni en `appsettings.json`. En local: `dotnet user-secrets set "<Clave>" "<valor>" --project TruekeBogotaSolidario.Presentacion`. En Azure: *App Service → Configuración → Variables de entorno* (mejor como referencia a Key Vault: `@Microsoft.KeyVault(SecretUri=...)`).

| Servicio | Dónde | Qué copiar → variable |
|---|---|---|
| **Wompi** | [comercios.wompi.co](https://comercios.wompi.co) → inicia sesión → **Desarrolladores** (o *Configuración avanzada → Llaves*). Primero usa el modo **Sandbox** (llaves `pub_test_…`, `prv_test_…`) y luego activa **Producción** (`pub_prod_…`, `prv_prod_…`) cuando Wompi apruebe el comercio (piden RUT, cámara de comercio o cédula y cuenta bancaria). | Llave pública → `Pagos__Wompi__LlavePublica` · Llave privada → `Pagos__Wompi__LlavePrivada` · Secreto de **integridad** → `Pagos__Wompi__SecretoIntegridad` · Secreto de **eventos** → `Pagos__Wompi__SecretoEventos` · En sandbox además `Pagos__Wompi__BaseUrl=https://sandbox.wompi.co/v1` |
| Wompi (webhook) | Misma sección → **URL de eventos** | `https://<tu-api>/api/v1/pagos/wompi/eventos` |
| **Google (inicio de sesión)** | [console.cloud.google.com](https://console.cloud.google.com) → *APIs y servicios* → *Pantalla de consentimiento OAuth* (tipo Externo, nombre y correo de soporte) → *Credenciales* → *Crear credenciales* → **ID de cliente OAuth** → tipo **Aplicación web** → *Orígenes de JavaScript autorizados*: `http://localhost:4200` y `https://<tu-front>` | ID de cliente (`…apps.googleusercontent.com`) → `Google__ClientId`. **No** se necesita el "secreto de cliente": el backend valida el ID token. |
| **reCAPTCHA v3** | [google.com/recaptcha/admin](https://www.google.com/recaptcha/admin) → nuevo sitio → **reCAPTCHA v3** → dominios `localhost` y el del front | Clave del sitio → `Captcha__ClaveSitio` · Clave secreta → `Captcha__ClaveSecreta` |
| **SQL Server (producción)** | Azure Portal → *Azure SQL Database* (servidor + base `TruekeDb`, nivel Basic/S0 para empezar, endpoint privado en la VNet, *Microsoft Entra* como administrador). En *Cadenas de conexión* → ADO.NET. | `ConnectionStrings__TruekeDb=Server=tcp:<servidor>.database.windows.net,1433;Database=TruekeDb;Authentication=Active Directory Default;Encrypt=True;` (con identidad administrada, sin contraseña) o con usuario SQL `User ID=…;Password=…` guardada en Key Vault. Deja `Database__Inicializacion=Migrate` (aplica las migraciones al arrancar) o `None` + script idempotente en el despliegue si hay varias instancias. |
| SMTP | Azure Communication Services → *Email* (dominio verificado) o SendGrid | `Correo__Smtp__Host`, `__Usuario`, `__Clave`, `Correo__Remitente` |
| Azure Blob | Cuenta de Storage | `Almacenamiento__ServicioUrl=https://<cuenta>.blob.core.windows.net` (con identidad administrada) |

## 8. Base de datos y migraciones

```bash
dotnet tool run dotnet-ef migrations add <Nombre> -p TruekeBogotaSolidario.Datos -s TruekeBogotaSolidario.Datos -o Migraciones
dotnet tool run dotnet-ef migrations script --idempotent -p TruekeBogotaSolidario.Datos -s TruekeBogotaSolidario.Datos -o migracion.sql
```

- `MigracionesTests` falla si el modelo cambió sin migración.
- Las pruebas de `SqlServer/` aplican las migraciones en SQL Server 2022 real.
- **Con varias instancias:** aplica el script idempotente en el despliegue y usa `Database__Inicializacion=None`.
- **Tarea horaria:**
  - cierra automáticamente los intercambios vencidos;
  - purga refrescos y enlaces vencidos y las notificaciones leídas de más de 90 días.

## 9. Despliegue en Azure

1. **Imagen:** `docker build -t <registro>.azurecr.io/trueke-api:<versión> backend` y `docker push`.
2. **App Service (Linux, contenedor):**
   - `WEBSITES_PORT=8080`;
   - *Health check* en `/health/ready`;
   - HTTPS Only, TLS ≥ 1.2 y **WebSockets** activados;
   - VNet;
   - identidad administrada con acceso a ACR, Key Vault y Storage (*Storage Blob Data Contributor* + *Storage Blob Delegator*).
3. **Datos y caché:** Azure SQL con endpoint privado; Azure Cache for Redis (`:6380,ssl=true,password=...`).
4. **Storage:**
   - contenedor `imagenes` con acceso anónimo **solo de lectura de blobs**;
   - contenedor `documentos` **privado**;
   - CORS de la cuenta: `PUT` desde el origen del front, con las cabeceras `x-ms-blob-type` y `content-type`;
   - regla de *lifecycle* para borrar blobs huérfanos.
5. **Monitoreo:** recurso de Application Insights → `APPLICATIONINSIGHTS_CONNECTION_STRING`.
6. **Dominio:** el mismo para front y API (`app.` y `api.`), para usar la cookie con `SameSite=Strict`.
7. **Externos:**
   - Wompi: webhook `https://<api>/api/v1/pagos/wompi/eventos`;
   - Google Cloud: el front en *Authorized JavaScript origins*;
   - reCAPTCHA: el dominio del front registrado.
8. **Endurecimiento de la infraestructura (configuración de Azure y GitHub, no código):**
   - **Entrada única:** Azure Front Door (o Application Gateway) con **WAF** (reglas OWASP + bots) delante del front y la API; en App Service, *Restricciones de acceso* para aceptar solo el tráfico de Front Door (etiqueta de servicio `AzureFrontDoor.Backend` + cabecera `X-Azure-FDID`). Así `Proxy__Confiar=true` es seguro.
   - **Redis** habilitado cuando haya más de una instancia: además del backplane de SignalR, activa el **límite de peticiones compartido** entre instancias.
   - **Microsoft Defender for Storage** con *malware scanning* en la cuenta de Blob (escanea las fotos y los documentos al subirse).
   - **Azure SQL:** copias automáticas con restauración a un punto en el tiempo (PITR, 7–35 días) y, si es posible, retención a largo plazo; *Microsoft Defender for SQL*; auditoría hacia Log Analytics.
   - **Key Vault** para todos los secretos, con rotación anual de `Jwt__Key` y de las llaves de Wompi.
   - **Alertas** de Application Insights: errores 5xx, latencia y la notificación `AlertaPagoEnRevision`.
   - **GitHub:** activar *Secret scanning*, *Push protection*, *Dependabot alerts* y proteger `main` exigiendo los checks de CI y CodeQL.
   - **OWASP ZAP:** `Actions → OWASP ZAP → Run workflow` contra la URL de **staging** antes de cada lanzamiento.

## 10. Contrato para el front Angular

- **Base:** `/api/v1`. Errores siempre en `application/problem+json` `{ type, title, status, traceId, errors?, codigo? }`. `title` es el mensaje para mostrar; `codigo` es estable (`2fa_requerido`, `2fa_requerido_admin`).
- **JSON:** camelCase y enums como texto. Fechas en **UTC ISO-8601 con `Z`**. `Location` en los 201 y `Retry-After` en los 429.
- **Al arrancar:** `GET /configuracion` devuelve `captchaClaveSitio`, `googleClientId`, `versionPoliticaDatos`, `subidaArchivosHabilitada`, `maxImagenesPorPublicacion`, `tamanoMaximoArchivoBytes` y los días de cierre automático. Una función con valor null está deshabilitada.
- **Sesión:**
  - token en memoria;
  - ante un 401, llamar una vez a `POST /auth/refrescar` con `withCredentials` y la cabecera `X-Trueke-Csrf: 1`, y reintentar;
  - al recargar la página, refrescar;
  - en desarrollo, usar el proxy de Angular hacia la API.
- **reCAPTCHA:** cargar `https://www.google.com/recaptcha/api.js?render=<captchaClaveSitio>` y enviar `captchaToken = await grecaptcha.execute(clave, { action: 'registro' | 'login' | 'olvide_clave' })` en esos tres formularios.
- **Login con 2FA:** si la respuesta trae `codigo === "2fa_requerido"`, mostrar el campo y reenviar con `codigoDosFactores`; vale lo mismo para `POST /auth/google`.
- **Configurar 2FA:** `POST /auth/2fa/configurar` (dibujar el QR de `uriOtpauth`) → `POST /auth/2fa/activar { codigo }`. Mostrar **una sola vez** los `codigosRecuperacion` y usar la `sesion` nueva.
- **Google:** el `credential` de Google Identity Services se envía a `POST /auth/google { idToken, aceptoPoliticaDatos }`. Responde 201 si creó la cuenta y 200 si ya existía.
- **Enlaces de correo** (`/verificar-correo?token=…` y `/restablecer-clave?token=…`): la página hace POST del token y lo **borra de la URL** con `history.replaceState`.
- **Registro:** casilla obligatoria de autorización de tratamiento de datos (`aceptoPoliticaDatos: true`).
- **Fotos:**
  1. `POST /archivos/subidas { tipo: "Imagen", contentType, tamanoBytes }`;
  2. `PUT` directo a `urlSubida` con las `cabeceras` indicadas;
  3. enviar `imagenes: [urlArchivo, …]` (máximo 5; la primera es la principal) al crear o editar (`PUT /publicaciones/{id}`, que reemplaza la lista).
- **Perfil público:** `publicacion.propietario.id` → `/usuarios/{id}/perfil`, `/publicaciones` y `/calificaciones`.
- **Intercambio:**
  - el dueño acepta y ambos escriben por `/conversaciones/{id}/mensajes`; para responder un mensaje en particular se envía `respuestaAId` (de la misma conversación) y cada mensaje trae `respuestaA { id, esMio, texto, oculto }`;
  - cada uno pulsa `POST /solicitudes/{id}/confirmar-entrega`; mostrar `cierreAutomaticoUtc` mientras siga `Aceptada`;
  - si no se dio, `POST /solicitudes/{id}/no-concretada { motivo }`;
  - con `puedoCalificar`, enviar `POST /solicitudes/{id}/calificar { estrellas, comentario }`.
- **Catálogo:** `GET /publicaciones?texto&categoriaId&modo&condicion&departamentoCodigo&municipioCodigo&localidad&precioMin&precioMax&soloVerificados&orden=Recientes|PrecioAsc|PrecioDesc&pagina&tamano`. Cada publicación trae `esFavorita`, `condicion`, `detalleCondicion`, `municipio`; para guardarla, `POST` o `DELETE /publicaciones/{id}/favorito`; la lista está en `GET /favoritos`. Vitrina: `GET /publicaciones/destacadas?departamentoCodigo&municipioCodigo&categoriaId&max`.
- **Publicar:** `condicion` es obligatoria (`Nuevo`, `ComoNuevo`, `Usado`, `UsadoConDetalles`, `Reparado`, `ParaRepuestos`); las tres últimas exigen `detalleCondicion`. `municipioCodigo` (5 dígitos DIVIPOLA) es opcional: por defecto, el del perfil.
- **Ubicaciones (públicas, cacheables):** `GET /ubicaciones/departamentos`, `/ubicaciones/departamentos/{dd}/municipios`, `/ubicaciones/municipios?texto=` y `/ubicaciones/municipios/{ddmmm}`.
- **Dueño:** `PublicacionDto.vistas` y `proximoImpulsoUtc` solo llegan al dueño; `POST /publicaciones/{id}/impulsar` y `GET /publicaciones/{id}/estadisticas`.
- **Cuenta:** `PUT /usuarios/yo/empresa { nombreComercial, nit }`, `GET|PUT|DELETE /cuenta/facturacion`, `GET /cuenta/facturas`, `POST /pqr`, `GET /pqr/mias`.
- **Denuncias con debido proceso:** si el moderador considera procedente una denuncia, la persona denunciada recibe `DenunciaRecibida` (`recursoId` = `resolucionId`, nunca se dice quién denunció). En `GET /denuncias/recibidas` ve la decisión y, mientras `apelableHastaUtc` no sea null (15 días), puede apelar una vez con `POST /denuncias/recibidas/{resolucionId}/apelacion { texto }`. Si se descarta, no se le avisa.
- **Coordenadas:** en público llegan redondeadas a 2 decimales; las exactas, solo al dueño, a los moderadores o con una solicitud aceptada.
- **Tiempo real:**
  - `new HubConnectionBuilder().withUrl(api + '/hubs/notificaciones', { accessTokenFactory })`, con los eventos `notificacion` y `mensaje`;
  - al reconectar, recuperar lo pendiente con `GET /notificaciones?soloNoLeidas=true` y `GET /conversaciones`.
- **Mis datos:** `GET /usuarios/yo/datos` y `POST /usuarios/yo/eliminar { confirmacion: "ELIMINAR", clave | googleIdToken }`.
- **Administración** (solo con sesión 2FA):
  - `GET /admin/usuarios?texto&soloSuspendidos`, `POST /admin/usuarios/{id}/suspender { motivo, dias? }` y `/reactivar`;
  - `GET /admin/denuncias` y `POST /admin/denuncias/{id}/resolver`;
  - `GET /admin/apelaciones?estado` y `POST /admin/apelaciones/{id}/resolver { aceptar, nota }` (aceptar revierte la medida; la resuelve otro moderador o un SuperUsuario);
  - `GET /admin/pagos?estado=RequiereRevision` y `POST /admin/pagos/{ref}/reembolsado`;
  - `/admin/verificaciones` y `PATCH /admin/usuarios/{id}/rol`;
  - `GET /admin/facturas?estado`, `GET /admin/facturas.csv`, `POST /admin/facturas/{id}/emitida { numeroDian, cufe }`;
  - `GET /admin/pqr?estado`, `POST /admin/pqr/{id}/responder { respuesta }`;
  - solo SuperUsuario: `GET /admin/ingresos?desde&hasta` y `GET /admin/ingresos.csv`.
- **Seguridad del front:**
  - textos de usuarios siempre con interpolación de Angular (nunca `innerHTML`);
  - sin secretos en el código y sin source maps en producción;
  - CSP que permita solo la API, Google (gsi y recaptcha) y el Blob.
- **Cliente TypeScript:** `npx @openapitools/openapi-generator-cli generate -i docs/openapi.json -g typescript-angular -o src/app/api`. Si cambia la API, se regenera el contrato con `ACTUALIZAR_OPENAPI=1 dotnet test --filter OpenApiTests`.

## 11. Seguridad (resumen)

- **Autenticación:**
  - JWT HS256 de 15 minutos con algoritmo fijo;
  - refresco rotativo en una cookie HttpOnly, con detección de reuso y anti-CSRF;
  - revocación por versión de seguridad;
  - Google validado en el servidor, con vinculación segura;
  - **2FA TOTP** cifrado con AES-GCM, sin repetición de códigos y obligatorio para administrar;
  - **reCAPTCHA v3** con falla cerrada.
- **Autorización:**
  - `FallbackPolicy` exige sesión en todo; lo anónimo es una lista blanca verificada por prueba;
  - los roles se recomprueban contra la BD, con jerarquía para suspender;
  - el usuario actuante sale **siempre** del JWT;
  - los recursos ajenos responden 404.
- **Privacidad:**
  - sin hashes, `rowversion` ni correos de terceros;
  - nombres públicos abreviados y coordenadas aproximadas;
  - chat interno en lugar de correos;
  - documentos de identidad privados y borrados al resolverse;
  - Habeas Data: consentimiento, exportación y supresión;
  - purga de datos vencidos;
  - el monitoreo redacta la query string.
- **Archivos:** SAS de 5 minutos (crear/escribir) sobre un blob con nombre elegido por el servidor; luego se valida dueño, tamaño ≤ 5 MB y *magic bytes*.
- **Fotos sin ubicación oculta:** al publicar, cada foto se **re-codifica** (ImageSharp): se aplica la orientación, se reduce a 1.600 px y se eliminan **todos los metadatos** (EXIF con GPS, modelo del celular, IPTC, XMP). La copia limpia se guarda con un **nombre nuevo** y se borra el original, para que la SAS aún vigente no pueda reemplazarla. Protege contra "bombas de descompresión" (máx. 12.000 px por lado).
- **Límite de peticiones distribuido:** con Redis, los contadores se comparten entre instancias (el límite de login no se multiplica al escalar). Solo se acepta `X-Forwarded-For` de proxies configurados.
- **CSV seguros:** las exportaciones neutralizan fórmulas (`=`, `+`, `-`, `@`) para evitar inyección en Excel.
- **Endurecimiento HTTP:**
  - HSTS, sin cabecera `Server`, CSP `default-src 'none'`, `nosniff`, `DENY`, `no-store`;
  - cuerpo máximo de 1 MB;
  - rate limiting por IP y por usuario;
  - errores ProblemDetails sin detalles internos.
- **Correos:** plantilla HTML con todo el texto variable escapado, más la versión de texto plano.
- **Pagos:** firma en tiempo constante, verificación contra la pasarela, idempotencia, transacciones y auditoría de los reembolsos.
- **Abuso:**
  - topes de comentarios (20/h), mensajes (120/h), denuncias (10/día), PQR (5/día), enlaces de correo (3/h), favoritos (500), pagos pendientes y solicitudes;
  - anti-farmeo por pareja, correo canónico, correos desechables bloqueados y bono al verificar (sección 5);
  - suspensión de cuentas.
- **Cadena de suministro:**
  - solo nuget.org;
  - el CI falla con paquetes vulnerables o con pruebas de SQL Server omitidas;
  - dependencias Azure y OpenTelemetry fijadas a versiones compatibles con .NET 8;
  - imagen sin SDK y sin root.

## 12. Limitaciones conocidas

- **Propagación de la revocación:** entre instancias tarda hasta 30 s (caché local). Una conexión SignalR abierta vive hasta que expira su token (≤ 15 min).
- **Token en la URL del hub:** para WebSockets va en `?access_token=` (solo se acepta en `/hubs/*`). Desactiva el registro de *query strings* en los logs del proxy.
- **Enumeración de cuentas:** el registro indica si el correo ya existe (mitigado con rate limiting y reCAPTCHA).
- **Archivos huérfanos:** las fotos quitadas al editar y las subidas que nunca se usan quedan en Blob. Usa una regla de *lifecycle*.
- **Correo en memoria:** la cola vive en memoria; un reinicio puede perder un correo en tránsito, y el usuario puede pedir "reenviar".
- **Reembolsos manuales:** se hacen en el panel de Wompi; la API solo los registra (y revierte el beneficio).
- **Sin cobro recurrente automático:** los planes se renuevan pagando de nuevo (con recordatorio). El débito automático requiere habilitar *fuentes de pago* (tokenización) con Wompi; se implementará cuando el comercio esté aprobado.
- **Facturación manual:** la emisión ante la DIAN la registra el equipo; la integración con la API de un proveedor tecnológico queda para cuando haya contrato.
- **Plazos de PQR:** los días hábiles no descuentan festivos colombianos (el plazo calculado es igual o menor al legal, nunca mayor).
- **Vistas:** se acumulan en memoria y se guardan cada minuto; un reinicio abrupto puede perder el último minuto.
- **ImageSharp:** licencia Six Labors Split (Apache 2.0 gratis para empresas con ingresos < 1 M USD/año); por encima, requiere licencia comercial. Se usa la rama 3.1 (la 4 exige llave al compilar).

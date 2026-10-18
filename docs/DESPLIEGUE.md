# Despliegue de Trueke Bogotá Solidario

Guía para publicar la plataforma completa. Hay dos fases: **gratis** (para la clase y las primeras pruebas) y **de pago** (usuarios reales). El código es el mismo en ambas; solo cambia dónde corre.

| Pieza | Fase gratis | Fase de pago |
|---|---|---|
| Front (Angular) | GitHub Pages (ya publicado) | GitHub Pages o Azure Static Web Apps, con dominio propio |
| API (.NET 10, Docker) | Render Free | Render Starter (~US$7/mes) o Azure App Service B1 |
| Base de datos (PostgreSQL) | Supabase Free (500 MB) | Supabase Pro (US$25/mes: sin pausa, backups diarios) |
| Fotos | Supabase Storage Free (1 GB), mismo proyecto que la base | Supabase Pro (100 GB incluidos) |
| Tiempo real entre instancias | No hace falta (1 instancia) | Azure Cache for Redis si hay más de una instancia |

**Modelo de nube:** todo es **PaaS**. Nosotros ponemos el contenedor y la configuración; el proveedor administra servidores, sistema operativo y parches.

> **Seguridad:** el código es igual de seguro en las dos fases. Lo que cambia es el servicio: en la fase gratis la API se duerme a los 15 minutos sin tráfico (el primer usuario espera unos 30-60 s), no hay garantía de disponibilidad y el front y la API quedan en dominios distintos (ver §7).

---

## 1. Antes de empezar
- Cuenta de **Supabase** para la base de datos (§2): gratis y sin tarjeta.
- Cuenta de **Supabase** también para las fotos (§3): es el mismo proyecto de la base.
- Cuenta de **Brevo** para el correo (§5.1): gratis y sin tarjeta. Ya no hace falta cuenta de Azure.
- Cuenta de **Render**: [render.com](https://render.com) → *Get started* → **con GitHub** (así ve el repositorio).
- Para generar claves aleatorias sirve cualquier terminal con Git Bash: `openssl rand -base64 32`.

## 2. Base de datos: PostgreSQL en Supabase (gratis)
El plan Free de Supabase no vence: da 500 MB de base, no pide tarjeta y no mide horas de cómputo. Su única condición: **pausa el proyecto si pasa 7 días sin actividad**. Los datos no se borran; se reactiva con *Restore* en el panel (ver §7).

1. [supabase.com](https://supabase.com) → *Start your project* → entrar **con GitHub**.
2. *New project*:
   - Organización: la personal (plan Free).
   - Nombre: `trueke`.
   - **Database Password**: *Generate a password*. Guardarla en un gestor de contraseñas, porque no se vuelve a mostrar.
   - Región: **East US (North Virginia)**, la más cercana a Render Ohio.
3. En *Security options* (o después en *Project Settings → Data API*), **desmarcar "Enable Data API"**. Trueke no la usa: toda la lectura y escritura pasa por nuestra API. Si quedara encendida, la llave pública `anon` podría consultar las tablas. Por si acaso, la migración `CerrarDataApiSupabase` les quita los permisos a `anon` y `authenticated` y activa RLS en todas las tablas.
4. *Create new project* y esperar 1-2 minutos.
5. Botón **Connect** (arriba) → pestaña **Session pooler**. Es la opción con **IPv4**: la conexión directa `db.<ref>.supabase.co` solo tiene IPv6 y Render no la alcanza. Supabase la muestra como URI:
   ```
   postgresql://postgres.<ref>:[YOUR-PASSWORD]@aws-N-us-east-1.pooler.supabase.com:5432/postgres
   ```
   Npgsql (el conector de .NET) no lee ese formato. Hay que pasarla a pares `clave=valor` con la clave del paso 2:
   ```
   Host=aws-N-us-east-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<ref>;Password=<clave>;SSL Mode=Require;Maximum Pool Size=10
   ```
   - Este valor va en `ConnectionStrings__TruekeDb`. Las tablas las crea la API sola al arrancar (migraciones).
   - `Maximum Pool Size=10`: en modo sesión cada conexión de la API ocupa una del pooler, y el plan gratis permite pocas.
   - **No usar el Transaction pooler (puerto 6543):** no conserva la sesión entre consultas y obligaría a desactivar las sentencias preparadas (`No Reset On Close=true`).
6. Si la clave tiene `;` o comillas, generar otra (*Project Settings → Database → Reset database password*). Esos caracteres rompen la cadena.

## 3. Fotos: Supabase Storage (gratis)
Las fotos y los documentos de verificación van en **Supabase Storage**, dentro del mismo proyecto de la base (§2). El plan Free incluye 1 GB de archivos y algo de transferencia mensual (ver los límites vigentes en *Usage*); cada foto pesa unos cientos de KB y su miniatura unas decenas.

1. No hay que crear nada a mano: **la API crea sola los tres buckets** al arrancar y les fija la configuración:
   - `imagenes`: **público, de solo lectura** (cualquiera ve las fotos; solo la API escribe).
   - `documentos` y `subidas`: **privados**. Los moderadores ven un documento con un enlace firmado que dura 5 minutos.
   - Los tres rechazan por sí mismos archivos de más de 5 MB y tipos que no sean JPG, PNG, WEBP (o PDF en documentos y subidas).
2. *Project Settings* → **API Keys** → **Secret keys** → copiar la llave secreta (`sb_secret_…`; la `service_role` antigua también sirve) → `Almacenamiento__LlaveServicio`. **Es la llave maestra del proyecto:** solo va en Render, nunca en el front ni en el repositorio.
3. `Almacenamiento__Url` = `https://<ref>.supabase.co/storage/v1` (el `<ref>` aparece en *Project Settings → General → Project ID*, y también en la URL del panel).
4. El host `<ref>.supabase.co` va en `Urls__HostsPermitidosImagenes__0` y `Urls__HostsPermitidosDocumentos__0`.
5. **No crear políticas en *Storage → Policies*.** Sin políticas, la llave pública no puede subir ni borrar nada: todo pasa por la API. Tampoco hace falta CORS: Supabase ya lo responde para las subidas desde el navegador.
6. (Opcional) En GitHub → *Settings → Secrets and variables → Actions → Variables*, crear `STORAGE_ORIGEN` = `https://<ref>.supabase.co`: la CSP de GitHub Pages permite entonces solo ese proyecto en lugar de cualquier `*.supabase.co`.

**Cómo funciona la subida:** la API pide a Supabase una URL firmada (`createSignedUploadUrl`) para **un** archivo nuevo, con nombre elegido por el servidor, en el bucket privado `subidas`. El navegador sube el archivo directo con esa URL (no pasa por Render) y no puede reemplazarlo después. Al usarlo, la API lo revisa (dueño, tamaño, tipo real), quita GPS y metadatos, publica una copia limpia con otro nombre en `imagenes` y borra la subida. Lo que nadie usa se borra pasada una hora. La URL firmada de Supabase vence a las 2 horas (no se puede acortar); como sirve para un solo archivo privado que nunca se publica tal cual, eso no abre ningún riesgo.

**Desarrollo y pruebas:** `backend/docker-compose.storage.yml` levanta el mismo servidor de Storage que usa Supabase (`supabase/storage-api`) en `http://127.0.0.1:8082/storage/v1`, con llaves de demostración. Las pruebas de .NET y las de punta a punta lo usan.

### 3.1 Miniaturas y CDN
- **Miniaturas (sin costo extra):** cada foto se publica con una miniatura WEBP de 640 px (`…/abc.jpg` → `…/abc.min.webp`) que usan el catálogo, las listas y los avatares. Pesa unas decenas de KB, contra cientos de KB de la foto grande. Al arrancar, la API crea sola las miniaturas que falten. No hay que configurar nada.
- **CDN:** Supabase ya sirve los buckets públicos a través de su CDN. Las fotos y miniaturas salen con `Cache-Control: public, max-age=31536000, immutable` (el nombre nunca se reutiliza), así que cualquier CDN las guarda un año.
- **Dominio propio para las fotos (opcional, apagado):** con `Almacenamiento__CdnUrl` (https, p. ej. `https://fotos.midominio.co`, que reenvía a `https://<ref>.supabase.co/storage/v1/object/public/`), el front descarga las fotos desde ese dominio. La base y la API siguen usando las URLs de Supabase, así que se puede apagar en cualquier momento quitando la variable. Si el front corre en el contenedor nginx, agregar ese host a `CSP_IMG_EXTRA`. Gratis con Cloudflare (plan Free) delante del dominio propio.

## 4. API en Render
1. Render → **New** → **Blueprint** → elegir el repositorio `TRUEKE-BOGOTA-SOLIDARIO-`. Render lee [`render.yaml`](../render.yaml) y muestra el servicio `trueke-api`.
2. Render pide los valores marcados como secretos. Llenarlos con lo de los pasos 2 (Supabase), 3 y 5 (reCAPTCHA, correo, Wompi). `Jwt__Key` y `Seguridad__ClaveCifrado` los genera Render solo. → **Apply**.
3. No hace falta abrir firewall: Supabase acepta conexiones desde cualquier IP con usuario y clave, siempre cifradas (`SSL Mode=Require`).
4. Cuando termine, la URL queda como `https://trueke-api.onrender.com` (o parecida). Probar:
   - `https://<api>/health/live` → responde OK (la API está viva).
   - `https://<api>/health/ready` → `{"estado":"ok"}` (la API llega a la base).
   - Si falla, revisar **Logs**: la API dice exactamente qué variable falta.

Después de esto, **cada merge a `main` se despliega solo** cuando pasan los checks de GitHub (`autoDeployTrigger: checksPass`).

## 5. Llaves externas
| Servicio | Dónde | Variables |
|---|---|---|
| reCAPTCHA v3 | [google.com/recaptcha/admin](https://www.google.com/recaptcha/admin) → nuevo sitio → **v3** → dominios `ingmarting.github.io` y `localhost` | `Captcha__ClaveSitio`, `Captcha__ClaveSecreta` |
| Correo (Brevo) | Ver §5.1 | `Correo__Brevo__ClaveApi`, `Correo__Remitente` |
| Wompi (sandbox) | [comercios.wompi.co](https://comercios.wompi.co) → *Desarrolladores* → llaves de **prueba** (`pub_test_…`, `prv_test_…`, integridad, eventos) | `Pagos__Wompi__*` · URL de eventos: `https://<api>/api/v1/pagos/wompi/eventos` |
| Google (opcional) | Ver `backend/README.md` §7.1; origen autorizado `https://ingmarting.github.io` | `Google__ClientId` |

Más detalle de cada llave: [`backend/README.md` §7.1](../backend/README.md).

### 5.1 Correo con Brevo (gratis: 300 correos al día)
Render gratis **bloquea el SMTP** (puertos 25, 465 y 587), así que Gmail por SMTP no funciona ahí. La API envía por la **API HTTPS de Brevo** (`Correo__Proveedor=Brevo`, ya fijado en `render.yaml`). El plan gratis da 300 correos al día, sin tarjeta y sin dominio propio.
1. **Cuenta:** [brevo.com](https://www.brevo.com) → *Sign up free* → confirmar el correo y completar el perfil (nombre de la organización, país). Elegir el plan **Free**.
2. **Remitente verificado:** menú del perfil → **Senders, Domains & Dedicated IPs** → **Senders** → *Add a sender*: nombre `Trueke Bogotá Solidario` y la dirección desde la que saldrán los correos (puede ser un Gmail del equipo). Brevo manda un código a esa dirección; escribirlo para verificarla. Esa dirección es `Correo__Remitente`. **Si el remitente no está verificado, Brevo rechaza cada envío.**
3. **Clave API:** menú del perfil → **SMTP & API** → pestaña **API Keys** (no la pestaña *SMTP*) → *Generate a new API key*:
   - nombre `trueke-api`; vencimiento *Sin vencimiento* (igual caduca tras 90 días seguidos sin uso);
   - acceso **Restringido**, con un solo permiso: **Emails transaccionales → Todos** (con *Leer* no puede enviar) y todo lo demás en *Ninguno*;
   - copiar la clave: empieza por **`xkeysib-`** y solo se muestra una vez. Es secreta: no va al repositorio ni a chats.

   **No sirve la clave SMTP** (`xsmtpsib-…`, pestaña *SMTP*): con ella Brevo responde 401.
4. **IPs autorizadas:** las cuentas nuevas de Brevo bloquean las llamadas desde IPs desconocidas, y Render gratis cambia de IP. Menú del perfil → **Security** → **Authorised IPs** → desactivar el bloqueo (la clave sigue protegiendo la cuenta). Alternativa más estricta: autorizar los rangos de salida que Render muestra en *trueke-api → Connect → Outbound*.
5. **Render** → *trueke-api* → *Environment*: `Correo__Brevo__ClaveApi` = la clave del paso 3 y `Correo__Remitente` = la dirección del paso 2 → *Save, rebuild, and deploy*. **Si falta la clave, la API no arranca** (avisa en los logs cuál falta).
6. **Probar:** registrar una cuenta o pedir "olvidé mi contraseña". El envío se ve en Brevo → **Transactional** → *Logs*. Si en los logs de Render sale `Brevo rechazó el correo (HTTP 401)`, revisar que la clave sea la API key `xkeysib-` con el permiso del paso 3, y el paso 4; con `HTTP 400`, el remitente del paso 2.

**Con un Gmail como remitente**, Brevo puede cambiar el dominio del remitente por `brevosend.com` para cumplir las reglas de Gmail y Yahoo (DMARC), y algunos correos pueden caer en spam. Con **dominio propio** (§8): Brevo → *Domains* → *Add a domain* → publicar los registros DNS (DKIM y DMARC) que pide → remitente `no-responder@tudominio`. Si 300 al día no alcanzan, el plan Starter de Brevo sube el límite sin cambiar el código.

En un plan pago de Render se puede usar también cualquier SMTP: `Correo__Proveedor=Smtp` en `render.yaml` y `Correo__Smtp__Host`, `Correo__Smtp__Usuario`, `Correo__Smtp__Clave` (por ejemplo Gmail con contraseña de aplicación) y `Correo__Remitente` = esa cuenta.

## 6. Conectar el front con la API
1. GitHub → repositorio → **Settings** → **Secrets and variables** → **Actions** → pestaña **Variables** → *New repository variable*: `API_URL` = la URL de Render (sin `/` al final). No es secreta: el navegador la ve igual.
2. **Actions** → *Front en GitHub Pages* → **Run workflow** (rama `main`).
3. Abrir https://ingmarting.github.io/TRUEKE-BOGOTA-SOLIDARIO-/: deben cargar las categorías y el catálogo. Si es la primera visita del día, esperar a que la API despierte.

### 6.1 Vista previa al compartir (WhatsApp, Facebook, X)
- **Inicio y catálogo:** el workflow de Pages escribe `og:image` y `og:url` con URL absoluta (WhatsApp no resuelve rutas relativas). Por defecto usa la URL de GitHub Pages; con dominio propio, crear la variable del repositorio `FRONT_URL` (p. ej. `https://trueke.co/`) y volver a ejecutar el workflow.
- **Cada publicación:** el botón *Compartir* usa `https://<API>/compartir/publicaciones/{id}`. Esa página trae título, modo, precio, lugar, descripción corta y la foto (JPEG de 1200×630), y redirige a la publicación en el front. Si la publicación no es pública (oculta, reservada, cancelada), muestra la vista general sin revelar nada.
- `Urls__Api` (en `render.yaml`) es la URL pública de la API para armar la imagen absoluta. Si cambia la URL de Render o se usa dominio propio, actualizarla.
- **Probar:** pegar el enlace en el [depurador de Facebook](https://developers.facebook.com/tools/debug/) (*Scrape Again* para refrescar). WhatsApp guarda la vista previa de cada enlace un tiempo; para ver un cambio, compartir otro enlace.
- **Limitación de la fase gratis:** si la API está dormida, WhatsApp puede rendirse antes de que despierte (sale el enlace sin foto). El enlace sigue funcionando. Se resuelve con el plan pago o con el chequeo de disponibilidad (mejora 4).

### 6.2 App instalable y notificaciones push (gratis)
- **App instalable (PWA):** el build de producción incluye el service worker de Angular. En Android y PC (Chrome/Edge) aparece "Instalar la app" en *Cuenta → Avisos*; en iPhone se instala desde Safari → *Compartir* → *Agregar a pantalla de inicio*. Guarda la app (no los datos: la API siempre se consulta en vivo) y las versiones nuevas se usan la próxima vez que se abre.
- **Notificaciones push** (estándar Web Push con llaves VAPID; los servicios de push de Google, Mozilla, Apple y Microsoft son gratis). Mientras no haya llaves, quedan apagadas y la app no las ofrece. Para activarlas:
  1. Generar las llaves UNA vez, en una terminal con Node (por ejemplo dentro de `frontend/`):
     ```bash
     node -e "const c=require('crypto');const k=c.generateKeyPairSync('ec',{namedCurve:'prime256v1'});const p=k.publicKey.export({format:'jwk'});console.log('Push__ClavePublica='+Buffer.concat([Buffer.from([4]),Buffer.from(p.x,'base64url'),Buffer.from(p.y,'base64url')]).toString('base64url'));console.log('Push__ClavePrivada='+k.privateKey.export({format:'jwk'}).d)"
     ```
  2. Render → *trueke-api* → *Environment*: pegar `Push__ClavePublica` y `Push__ClavePrivada` → *Save, rebuild, and deploy*. La privada es secreta: no va al repositorio ni a chats.
  3. **No cambiarlas después:** con llaves nuevas, todos los dispositivos dejan de recibir avisos hasta que cada persona las vuelva a activar.
- Cada persona las activa por dispositivo en *Cuenta → Avisos* (el permiso del navegador es el consentimiento). Llegan solo si no tiene la app abierta, con el mismo criterio que los correos: solicitudes, entregas, calificaciones y mensajes (nunca el texto del mensaje). Al cerrar sesión, cerrar todas las sesiones o eliminar la cuenta, los dispositivos se dan de baja.
- **iPhone/iPad:** Apple solo permite notificaciones con la app instalada en la pantalla de inicio (iOS 16.4 o superior); la pantalla lo explica.
- El servidor solo envía a los servicios de push reconocidos (`fcm.googleapis.com`, `*.push.services.mozilla.com`, `*.notify.windows.com`, `*.push.apple.com`): nunca a una URL arbitraria.

## 7. Limitaciones de la fase gratis
- **Arranque en frío:** tras 15 min sin uso, la primera petición tarda 30-60 s.
- **iPhone/Safari:** el front (`github.io`) y la API (`onrender.com`) son sitios distintos, y Safari bloquea esa cookie de sesión "de terceros". En Safari la sesión se cierra cuando vence el token (15 min). En Chrome, Edge y Firefox funciona bien. **Se resuelve con dominio propio** (§8).
- **Pausa de Supabase por inactividad:** si el proyecto pasa 7 días sin actividad, Supabase lo pausa y avisa por correo. La API sigue viva, pero `/health/ready` y todo lo que use la base fallan. Para reactivarlo: panel de Supabase → proyecto → **Restore project** (1-2 minutos). Los datos no se pierden. Con usuarios entrando cada semana no pasa. Además: 500 MB de base (las fotos van aparte) y 5 GB de salida al mes; el consumo se ve en *Project Settings → Usage*.
- Sin Redis: el límite de intentos es por instancia (correcto mientras haya una sola).
- **Sin chequeo de disponibilidad por ahora:** un monitor cada 5 min mantendría despierta la API de Render (750 horas gratis al mes alcanzan para una sola instancia encendida siempre) y, de paso, evitaría la pausa de Supabase. Se puede activar ya con UptimeRobot (§7.2).

### 7.1 Monitoreo
Application Insights se quitó al salir de Azure. Los errores quedan en los **logs de Render** (*trueke-api → Logs*; el plan gratis guarda los últimos días). El chequeo de disponibilidad (§7.2) avisa si la API se cae.

### 7.2 Chequeo de disponibilidad (cuando haya plan pago)
- **Gratis:** [UptimeRobot](https://uptimerobot.com) (cuenta propia) → monitor HTTP(s) a `https://<API>/health/live` cada 5 min, con aviso por correo. `/health/live` no toca la base; `/health/ready` sí.
- Con Supabase ya no hay cuota de cómputo que cuidar: se puede activar en la fase gratis. Usar `/health/ready` si además se quiere mantener activa la base.

## 8. Pasar a pago (producción real)
1. **Dominio propio** (p. ej. `trueke.co`, ~US$15-30/año en un registrador o en Cloudflare): `trueke.co` → front y `api.trueke.co` → API. Al quedar en el mismo sitio, cambiar `Auth__CookieSameSite` a `Strict`, y `Cors__Origenes__0` / `Urls__Frontend` al dominio nuevo. Agregar el dominio en reCAPTCHA y en Google.
2. **API sin arranque en frío:** en `render.yaml` cambiar `plan: free` → `plan: starter`, o mover la misma imagen a **Azure App Service B1** (Linux, contenedor) con Key Vault para los secretos. Ver `backend/README.md` §9.
3. **Base de datos:** Supabase **Pro** (US$25/mes): sin pausa por inactividad, backups diarios y más conexiones. Alternativa: cualquier PostgreSQL administrado (Azure Database for PostgreSQL, Neon, Render Postgres) con la misma cadena de conexión.
4. **Pagos reales:** cuando Wompi apruebe el comercio, usar las llaves `pub_prod_…` y cambiar `Pagos__Wompi__BaseUrl` a `https://production.wompi.co/v1` **en `render.yaml`** (si se cambia solo en el panel, la sincronización del Blueprint lo devuelve a sandbox). Con eso desaparece sola la franja "Sitio de demostración". **Antes**, borrar los datos de prueba (la app promete a los testers que los beneficios de prueba se borran): lo más limpio es una base nueva vacía (`Database__Inicializacion=Migrate` crea las tablas).
5. **Monitoreo:** activar el chequeo de disponibilidad (§7.2) y, si hace falta más detalle que los logs de Render, un servicio de errores con plan gratis.
6. **Más de una instancia:** Azure Cache for Redis (`Redis__Habilitado=true`, `Redis__Conexion`).
7. **Ambiente de staging** antes de los pagos reales: guía y costos en `docs/STAGING.md` (gratis si va en otro workspace de Render).
8. **Analítica de visitas** sin cookies y métricas del negocio desde la base: `docs/ANALITICA.md`.

## 9. Kubernetes, ¿hace falta?
No. Kubernetes administra **muchos** contenedores (los reparte entre servidores, los reinicia y los escala) y conviene cuando hay decenas de servicios y mucho tráfico. Trueke es **una API y un front**: un PaaS que corre el contenedor (Render o App Service) hace lo mismo con mucho menos costo y trabajo. Si algún día hiciera falta, la misma imagen Docker sirve en Azure Kubernetes Service sin cambios.

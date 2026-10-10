# Despliegue de Trueke Bogotá Solidario

Guía para publicar la plataforma completa. Hay dos fases: **gratis** (para la clase y las primeras pruebas) y **de pago** (usuarios reales). El código es el mismo en ambas; solo cambia dónde corre.

| Pieza | Fase gratis | Fase de pago |
|---|---|---|
| Front (Angular) | GitHub Pages (ya publicado) | GitHub Pages o Azure Static Web Apps, con dominio propio |
| API (.NET 8, Docker) | Render Free | Render Starter (~US$7/mes) o Azure App Service B1 |
| Base de datos (SQL Server) | Azure SQL Database, oferta gratuita | Azure SQL Database (Basic/S0) con backups |
| Fotos | Azure Blob Storage (centavos al mes) | Igual |
| Tiempo real entre instancias | No hace falta (1 instancia) | Azure Cache for Redis si hay más de una instancia |

**Modelo de nube:** todo es **PaaS**. Nosotros ponemos el contenedor y la configuración; el proveedor administra servidores, sistema operativo y parches.

> **Seguridad:** el código es igual de seguro en las dos fases. Lo que cambia es el servicio: en la fase gratis la API se duerme a los 15 minutos sin tráfico (el primer usuario espera unos 30-60 s), no hay garantía de disponibilidad y el front y la API quedan en dominios distintos (ver §7).

---

## 1. Antes de empezar
- Cuenta de **Azure**, una de estas dos:
  - **Azure for Students** ([azure.microsoft.com/free/students](https://azure.microsoft.com/free/students)) con el correo de la universidad: sin tarjeta, US$100 de crédito. Si dice *"No se puede renovar Azure for Students"*, no hay forma de reactivarla desde la web; usar la otra opción.
  - **Cuenta gratuita** ([azure.microsoft.com/free](https://azure.microsoft.com/free)): pide una tarjeta solo para verificar identidad (puede aparecer un cobro de verificación que se devuelve) y da US$200 por 30 días. Al terminar, se pasa a *pago por uso* para que los recursos sigan encendidos: la base con la oferta gratuita sigue sin costo y las fotos cuestan centavos.
- **Alerta de presupuesto (hacerla primero):** portal → **Administración de costos** → **Presupuestos** → *Agregar*: monto mensual **US$1**, alerta al 100 % al correo del equipo. Así nada se cobra sin que se enteren.
- Cuenta de **Render**: [render.com](https://render.com) → *Get started* → **con GitHub** (así ve el repositorio).
- Para generar claves aleatorias sirve cualquier terminal con Git Bash: `openssl rand -base64 32`.

## 2. Base de datos: Azure SQL (gratis)
1. Portal de Azure → buscar **Azure SQL** → *Crear* → **Base de datos SQL**. Arriba aparece el aviso de la **oferta gratuita** → *Aplicar oferta*.
2. Grupo de recursos: `trueke`. Nombre de la base: `TruekeDb`.
3. Servidor → *Crear nuevo*: nombre único (p. ej. `trueke-sql-<algo>`), región **Central US** (las cuentas de prueba no permiten East US; Render queda en Ohio, al lado). Si una región sale en rojo, probar otra de EE. UU. y poner la misma en Storage y la más cercana en `render.yaml`, autenticación **SQL** con un usuario administrador y una clave larga. Guardar ambos en un gestor de contraseñas.
4. En *Comportamiento al alcanzar el límite gratis* elegir **Pausar la base hasta el próximo mes**. Así nunca hay cobros.
5. *Redes*: acceso **público**, y por ahora **no** marcar "Agregar la IP actual". Las reglas se agregan en el paso 4.3.
6. Crear. Al terminar: base → *Cadenas de conexión* → **ADO.NET**. Copiarla, reemplazar `{your_password}` y agregar al final `Connection Timeout=60;` (la base gratis puede tardar en despertar):
   ```
   Server=tcp:trueke-sql-xxx.database.windows.net,1433;Initial Catalog=TruekeDb;User ID=...;Password=...;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;
   ```
   Este valor va en `ConnectionStrings__TruekeDb`. Las tablas las crea la API sola al arrancar (migraciones).

## 3. Fotos: Azure Blob Storage
1. Portal → **Cuentas de almacenamiento** → *Crear*: grupo `trueke`, nombre único en minúsculas (p. ej. `truekefotos<algo>`), región **la misma de la base** (Central US), rendimiento **Estándar**, redundancia **LRS** (la más barata).
2. *Configuración* → **Permitir el acceso anónimo de blobs: Habilitado**. Solo el contenedor `imagenes` es público (de solo lectura); `documentos` queda privado. La API crea ambos contenedores sola.
3. *Uso compartido de recursos (CORS)* → pestaña **Blob service** → agregar una regla:
   - Orígenes permitidos: `https://ingmarting.github.io`
   - Métodos: `PUT`, `GET`, `HEAD`, `OPTIONS`
   - Encabezados permitidos: `content-type,x-ms-blob-type`
   - Encabezados expuestos: `*` · Antigüedad máxima: `3600`
4. *Seguridad y redes* → **Claves de acceso** → *Mostrar* → copiar la **cadena de conexión** → `Almacenamiento__CadenaConexion`.
5. El host `truekefotos<algo>.blob.core.windows.net` va en `Urls__HostsPermitidosImagenes__0` y `Urls__HostsPermitidosDocumentos__0`.

### 3.1 Miniaturas y CDN
- **Miniaturas (sin costo extra):** cada foto se publica con una miniatura WEBP de 640 px (`…/abc.jpg` → `…/abc.min.webp`) que usan el catálogo, las listas y los avatares. Pesa unas decenas de KB, contra cientos de KB de la foto grande. Al arrancar, la API crea sola las miniaturas que falten (fotos anteriores). No hay que configurar nada.
- **CDN (opcional, apagada):** con `Almacenamiento__CdnUrl` (https, p. ej. `https://fotos.midominio.co`) y `Almacenamiento__ServicioUrl` (la URL de la cuenta), el front descarga las fotos desde la CDN. La base y la API siguen usando las URLs de Blob, así que se puede apagar en cualquier momento quitando la variable. Si el front corre en el contenedor nginx, agregar el host de la CDN a `CSP_IMG_EXTRA`.
  - **Gratis:** Cloudflare (plan Free) delante del dominio propio: un subdominio `fotos` en modo proxy apuntando a `truekefotos<algo>.blob.core.windows.net` (con regla de *Host header* hacia la cuenta). Requiere el dominio propio.
  - **De pago:** Azure Front Door Standard (~US$35/mes + tráfico). La CDN "clásica" de Azure ya no admite perfiles nuevos.
  - Las fotos y miniaturas ya salen con `Cache-Control: public, max-age=31536000, immutable`, así que cualquier CDN las guarda un año sin configuración extra.

## 4. API en Render
1. Render → **New** → **Blueprint** → elegir el repositorio `TRUEKE-BOGOTA-SOLIDARIO-`. Render lee [`render.yaml`](../render.yaml) y muestra el servicio `trueke-api`.
2. Render pide los valores marcados como secretos. Llenarlos con lo de los pasos 2, 3 y 5 (reCAPTCHA, correo, Wompi). `Jwt__Key` y `Seguridad__ClaveCifrado` los genera Render solo. → **Apply**.
3. Mientras construye: servicio → **Connect** → **Outbound** → copiar las IP de salida. En Azure → servidor SQL → *Redes* → **Reglas de firewall** → agregar cada IP (inicio = fin). Sin esto, la API no llega a la base.
4. Cuando termine, la URL queda como `https://trueke-api.onrender.com` (o parecida). Probar:
   - `https://<api>/health/live` → responde OK (la API está viva).
   - `https://<api>/health/ready` → `{"estado":"ok"}` (la API llega a la base).
   - Si falla, revisar **Logs**: la API dice exactamente qué variable falta.

Después de esto, **cada merge a `main` se despliega solo** cuando pasan los checks de GitHub (`autoDeployTrigger: checksPass`).

## 5. Llaves externas
| Servicio | Dónde | Variables |
|---|---|---|
| reCAPTCHA v3 | [google.com/recaptcha/admin](https://www.google.com/recaptcha/admin) → nuevo sitio → **v3** → dominios `ingmarting.github.io` y `localhost` | `Captcha__ClaveSitio`, `Captcha__ClaveSecreta` |
| Correo (Azure) | Ver §5.1 | `Correo__Azure__CadenaConexion`, `Correo__Remitente` |
| Wompi (sandbox) | [comercios.wompi.co](https://comercios.wompi.co) → *Desarrolladores* → llaves de **prueba** (`pub_test_…`, `prv_test_…`, integridad, eventos) | `Pagos__Wompi__*` · URL de eventos: `https://<api>/api/v1/pagos/wompi/eventos` |
| Google (opcional) | Ver `backend/README.md` §7.1; origen autorizado `https://ingmarting.github.io` | `Google__ClientId` |

Más detalle de cada llave: [`backend/README.md` §7.1](../backend/README.md).

### 5.1 Correo con Azure Communication Services
Render gratis **bloquea el SMTP** (puertos 25, 465 y 587), así que Gmail por SMTP no funciona ahí. La API envía por la API HTTPS de Azure (`Correo__Proveedor=AzureCommunication`, ya fijado en `render.yaml`). Cuesta unos US$0,00025 por correo.
1. Portal → buscar **Email Communication Services** → *Crear*: grupo `trueke`, nombre (p. ej. `trueke-correo`), **ubicación de datos: United States**.
2. Al terminar: el recurso → **Aprovisionar dominios** → **Agregar dominio gratuito de Azure** (Azure subdomain). Tarda 1-2 minutos. Queda un dominio como `xxxx.azurecomm.net`.
3. Abrir ese dominio → **Direcciones MailFrom**: viene `DoNotReply@xxxx.azurecomm.net`. Ese es el **remitente** → `Correo__Remitente`. Ahí mismo se puede poner el nombre visible: *Trueke Bogotá Solidario*.
4. Portal → buscar **Communication Services** (otro recurso, sin "Email") → *Crear*: grupo `trueke`, nombre (p. ej. `trueke-comunicaciones`), ubicación de datos **United States**.
5. Ese recurso → **Correo electrónico → Dominios** → **Conectar dominio** → elegir el de los pasos 1-2.
6. Ese recurso → **Configuración → Claves** → copiar la **Cadena de conexión** principal (`endpoint=https://…;accesskey=…`) → `Correo__Azure__CadenaConexion`.
7. En Render → *Environment*, poner `Correo__Azure__CadenaConexion` y cambiar `Correo__Remitente` por la dirección del paso 3. **Si falta la cadena, la API no arranca** (avisa en los logs cuál falta).

**Límite del dominio gratuito de Azure: 10 correos por hora** (5 por minuto) y no se puede subir. Alcanza para la demostración, no para usuarios reales. Con **dominio propio** verificado en el mismo recurso (paso 2 → *Agregar dominio personalizado*, con los registros DNS que pide Azure) el límite pasa a 100 por hora y se puede ampliar, y los correos salen desde `no-responder@tudominio`. Fuente: [límites de Azure Communication Services](https://learn.microsoft.com/azure/communication-services/concepts/service-limits).

En un plan pago de Render se puede volver a Gmail u otro SMTP: `Correo__Proveedor=Smtp` en `render.yaml` y `Correo__Smtp__Host=smtp.gmail.com`, `Correo__Smtp__Usuario`, `Correo__Smtp__Clave` (contraseña de aplicación de Google) y `Correo__Remitente` = ese Gmail.

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

## 7. Limitaciones de la fase gratis
- **Arranque en frío:** tras 15 min sin uso, la primera petición tarda 30-60 s.
- **iPhone/Safari:** el front (`github.io`) y la API (`onrender.com`) son sitios distintos, y Safari bloquea esa cookie de sesión "de terceros". En Safari la sesión se cierra cuando vence el token (15 min). En Chrome, Edge y Firefox funciona bien. **Se resuelve con dominio propio** (§8).
- **Cuota de la base gratis:** cada mes trae 32 GB y 100.000 vCore-segundos de cómputo, que son unas **28-55 horas de base activa** (la base se duerme sola cuando nadie la usa; como la API de Render también se duerme, solo gasta mientras alguien usa la app). Alcanza para la clase y las pruebas, no para usuarios todo el día. Si se agota, la base se pausa hasta el mes siguiente; para tráfico real, pasarla a un plan de pago (§8). El consumo se ve en la base → *Información general* → "Cantidad mensual gratuita de vCore". Fuente: [oferta gratuita de Azure SQL](https://learn.microsoft.com/azure/azure-sql/database/free-offer).
- Sin Redis: el límite de intentos es por instancia (correcto mientras haya una sola).
- **Sin chequeo de disponibilidad (a propósito):** un monitor cada 5 min mantendría despierta la API. Las tareas de fondo (mantenimiento cada hora, reconciliación de pagos) despertarían también la base, y la cuota gratis de Azure SQL se agotaría en 1-2 días: **la base quedaría pausada hasta el mes siguiente**. Activarlo solo con plan pago (§7.2).

### 7.1 Monitoreo con Application Insights (gratis hasta 5 GB/mes)
La API envía errores, peticiones lentas, consultas a SQL (sin el texto ni los parámetros) y logs solo si existe la variable `APPLICATIONINSIGHTS_CONNECTION_STRING`. Nunca envía cuerpos de peticiones, contraseñas ni la query string (allí viaja el token del chat en tiempo real). Los `/health` no se registran.
1. Portal → **Application Insights** → *Crear*: grupo `trueke`, nombre `trueke-monitoreo`, región **Central US**. Se crea también un *área de trabajo de Log Analytics*, que es donde se cobra por datos.
2. **Tope para no pagar nunca:** área de trabajo de Log Analytics → *Uso y costos estimados* → **Límite diario** = `0.15` GB/día (≈4,5 GB/mes, por debajo de los 5 GB gratis). Si se llega al tope, ese día deja de llegar telemetría, pero la app sigue funcionando.
3. Application Insights → *Información general* → copiar la **Cadena de conexión** → en Render → *Environment* → `APPLICATIONINSIGHTS_CONNECTION_STRING` → *Save* (Render redespliega). Al arrancar ya no sale el aviso "no habrá monitoreo" en los logs.
4. **Alertas:**
   - *Detección inteligente → Anomalías de errores* viene activa y es gratis: avisa por correo a los dueños de la suscripción cuando sube el porcentaje de peticiones fallidas.
   - Opcional (≈US$0,10/mes): *Alertas → Crear regla de alerta* → señal **Excepciones** → mayor que 5 en 15 min → *Grupo de acciones* con tu correo.
5. Dónde mirar: **Errores** (excepciones con su traza), **Rendimiento** (endpoints lentos y consultas a SQL), **Mapa de aplicación** (`trueke-api` → SQL, Blob, Wompi) y **Métricas en vivo**.
6. **Costo:** con el tráfico actual, muy por debajo de los 5 GB. Si un mes se acerca al tope, bajar el muestreo con la variable `Monitoreo__Muestreo=0.25` (envía 1 de cada 4 peticiones; los errores siguen visibles en proporción).

### 7.2 Chequeo de disponibilidad (cuando haya plan pago)
- **Gratis:** [UptimeRobot](https://uptimerobot.com) (cuenta propia) → monitor HTTP(s) a `https://<API>/health/live` cada 5 min, con aviso por correo. `/health/live` no toca la base; `/health/ready` sí (úsalo solo con la base en plan pago).
- **En Azure:** Application Insights → *Disponibilidad* → *Prueba estándar* a la misma URL, con alerta. Cobra por ejecución: revisar la calculadora de precios de Azure antes de activarla.
- Se activa junto con: API en plan pago (no se duerme) y Azure SQL en plan pago (la base gratis no aguanta estar siempre activa).

## 8. Pasar a pago (producción real)
1. **Dominio propio** (p. ej. `trueke.co`, ~US$15-30/año en un registrador o en Cloudflare): `trueke.co` → front y `api.trueke.co` → API. Al quedar en el mismo sitio, cambiar `Auth__CookieSameSite` a `Strict`, y `Cors__Origenes__0` / `Urls__Frontend` al dominio nuevo. Agregar el dominio en reCAPTCHA, en el CORS del Storage y en Google.
2. **API sin arranque en frío:** en `render.yaml` cambiar `plan: free` → `plan: starter`, o mover la misma imagen a **Azure App Service B1** (Linux, contenedor) con Managed Identity (`Almacenamiento__ServicioUrl` en lugar de la cadena de conexión) y Key Vault. Ver `backend/README.md` §9.
3. **Base de datos:** pasar Azure SQL a Basic/S0 (sin pausa) y revisar la retención de backups.
4. **Pagos reales:** cuando Wompi apruebe el comercio, usar las llaves `pub_prod_…` y cambiar `Pagos__Wompi__BaseUrl` a `https://production.wompi.co/v1` **en `render.yaml`** (si se cambia solo en el panel, la sincronización del Blueprint lo devuelve a sandbox). Con eso desaparece sola la franja "Sitio de demostración". **Antes**, borrar los datos de prueba (la app promete a los testers que los beneficios de prueba se borran): lo más limpio es una base nueva vacía (`Database__Inicializacion=Migrate` crea las tablas).
5. **Monitoreo:** Application Insights ya funciona en la fase gratis (§7.1); con plan pago, activar el chequeo de disponibilidad (§7.2).
6. **Más de una instancia:** Azure Cache for Redis (`Redis__Habilitado=true`, `Redis__Conexion`).

## 9. Kubernetes, ¿hace falta?
No. Kubernetes administra **muchos** contenedores (los reparte entre servidores, los reinicia y los escala) y conviene cuando hay decenas de servicios y mucho tráfico. Trueke es **una API y un front**: un PaaS que corre el contenedor (Render o App Service) hace lo mismo con mucho menos costo y trabajo. Si algún día hiciera falta, la misma imagen Docker sirve en Azure Kubernetes Service sin cambios.

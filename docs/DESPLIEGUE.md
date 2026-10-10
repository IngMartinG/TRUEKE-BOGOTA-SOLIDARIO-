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
| Correo (Gmail) | Cuenta de Google → *Seguridad* → verificación en 2 pasos → **Contraseñas de aplicaciones** | `Correo__Smtp__Host=smtp.gmail.com`, `Correo__Smtp__Usuario` y `Correo__Remitente` = el correo, `Correo__Smtp__Clave` = la clave de aplicación |
| Wompi (sandbox) | [comercios.wompi.co](https://comercios.wompi.co) → *Desarrolladores* → llaves de **prueba** (`pub_test_…`, `prv_test_…`, integridad, eventos) | `Pagos__Wompi__*` · URL de eventos: `https://<api>/api/v1/pagos/wompi/eventos` |
| Google (opcional) | Ver `backend/README.md` §7.1; origen autorizado `https://ingmarting.github.io` | `Google__ClientId` |

Más detalle de cada llave: [`backend/README.md` §7.1](../backend/README.md).

## 6. Conectar el front con la API
1. GitHub → repositorio → **Settings** → **Secrets and variables** → **Actions** → pestaña **Variables** → *New repository variable*: `API_URL` = la URL de Render (sin `/` al final). No es secreta: el navegador la ve igual.
2. **Actions** → *Front en GitHub Pages* → **Run workflow** (rama `main`).
3. Abrir https://ingmarting.github.io/TRUEKE-BOGOTA-SOLIDARIO-/: deben cargar las categorías y el catálogo. Si es la primera visita del día, esperar a que la API despierte.

## 7. Limitaciones de la fase gratis
- **Arranque en frío:** tras 15 min sin uso, la primera petición tarda 30-60 s.
- **iPhone/Safari:** el front (`github.io`) y la API (`onrender.com`) son sitios distintos, y Safari bloquea esa cookie de sesión "de terceros". En Safari la sesión se cierra cuando vence el token (15 min). En Chrome, Edge y Firefox funciona bien. **Se resuelve con dominio propio** (§8).
- **Cuota de la base gratis:** cada mes trae 32 GB y 100.000 vCore-segundos de cómputo, que son unas **28-55 horas de base activa** (la base se duerme sola cuando nadie la usa; como la API de Render también se duerme, solo gasta mientras alguien usa la app). Alcanza para la clase y las pruebas, no para usuarios todo el día. Si se agota, la base se pausa hasta el mes siguiente; para tráfico real, pasarla a un plan de pago (§8). El consumo se ve en la base → *Información general* → "Cantidad mensual gratuita de vCore". Fuente: [oferta gratuita de Azure SQL](https://learn.microsoft.com/azure/azure-sql/database/free-offer).
- Sin Redis: el límite de intentos es por instancia (correcto mientras haya una sola).

## 8. Pasar a pago (producción real)
1. **Dominio propio** (p. ej. `trueke.co`, ~US$15-30/año en un registrador o en Cloudflare): `trueke.co` → front y `api.trueke.co` → API. Al quedar en el mismo sitio, cambiar `Auth__CookieSameSite` a `Strict`, y `Cors__Origenes__0` / `Urls__Frontend` al dominio nuevo. Agregar el dominio en reCAPTCHA, en el CORS del Storage y en Google.
2. **API sin arranque en frío:** en `render.yaml` cambiar `plan: free` → `plan: starter`, o mover la misma imagen a **Azure App Service B1** (Linux, contenedor) con Managed Identity (`Almacenamiento__ServicioUrl` en lugar de la cadena de conexión) y Key Vault. Ver `backend/README.md` §9.
3. **Base de datos:** pasar Azure SQL a Basic/S0 (sin pausa) y revisar la retención de backups.
4. **Pagos reales:** cuando Wompi apruebe el comercio, usar las llaves `pub_prod_…` y cambiar `Pagos__Wompi__BaseUrl` a `https://production.wompi.co/v1` **en `render.yaml`** (si se cambia solo en el panel, la sincronización del Blueprint lo devuelve a sandbox). Con eso desaparece sola la franja "Sitio de demostración". **Antes**, borrar los datos de prueba (la app promete a los testers que los beneficios de prueba se borran): lo más limpio es una base nueva vacía (`Database__Inicializacion=Migrate` crea las tablas).
5. **Monitoreo:** Application Insights (`APPLICATIONINSIGHTS_CONNECTION_STRING`).
6. **Más de una instancia:** Azure Cache for Redis (`Redis__Habilitado=true`, `Redis__Conexion`).

## 9. Kubernetes, ¿hace falta?
No. Kubernetes administra **muchos** contenedores (los reparte entre servidores, los reinicia y los escala) y conviene cuando hay decenas de servicios y mucho tráfico. Trueke es **una API y un front**: un PaaS que corre el contenedor (Render o App Service) hace lo mismo con mucho menos costo y trabajo. Si algún día hiciera falta, la misma imagen Docker sirve en Azure Kubernetes Service sin cambios.

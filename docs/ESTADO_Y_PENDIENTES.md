# Estado del proyecto — Trueke Bogotá Solidario (actualizado el 2026-10-18)

## Hecho — backend completo para conectar el front
**Base:**
- arquitectura por capas;
- catálogo con geolocalización y comentarios;
- SignalR + Redis;
- migraciones;
- Docker;
- CI;
- contrato OpenAPI y ProblemDetails.

**Seguridad y cuentas:**
- refresh token rotativo en cookie HttpOnly;
- verificación de correo y recuperación de clave;
- login con Google;
- **2FA TOTP** obligatoria para administración;
- **reCAPTCHA v3**;
- Habeas Data (Ley 1581).

**Funcionalidad:**
- perfil público;
- editar publicaciones y hasta 5 fotos (subida directa con URL firmada);
- chat interno;
- **confirmación de entrega por ambas partes** con cierre automático (7 y 30 días);
- **calificaciones**;
- **favoritos**;
- orden y filtros;
- notificaciones persistentes;
- denuncias.

**Administración:**
- suspender o reactivar cuentas con jerarquía de roles;
- búsqueda de usuarios;
- pagos en revisión y registro de reembolsos;
- verificaciones y roles.

**Operación:**
- correo por la API HTTPS de Brevo (o SMTP con TLS);
- correos HTML;
- tarea horaria de cierre y purga;
- pruebas contra la base real (Testcontainers; hoy PostgreSQL) obligatorias en el CI.

## Hecho el 2026-10-03 — escala nacional, monetización y seguridad
- **Colombia completa:** 33 departamentos y 1.122 municipios (DANE-DIVIPOLA), filtros por departamento/municipio, registro y perfil con municipio.
- **Estado del producto** obligatorio al publicar (nuevo, como nuevo, usado, usado con detalles, reparado, para repuestos) y filtro en el catálogo.
- **Privacidad de fotos:** se eliminan EXIF/GPS y metadatos al publicar.
- **Anti-farmeo:** correo canónico, correos desechables, bono al verificar, límite por pareja y calificaciones que no inflan el promedio.
- **Monetización:** impulsar con Eco-Puntos, plan Empresa con beneficios reales, estadísticas por publicación, vitrina de destacadas, recordatorio de vencimiento, límite de ventas sin identidad (Ley 1480 art. 53).
- **Facturación electrónica** (registro de cada venta, IVA, cola de emisión y CSV), **PQR** (retracto y reversión), **tablero de ingresos** y CSV contable.
- **Reembolsos que revierten el beneficio.**
- **Infra:** límite de peticiones distribuido (Redis), proxies confiables, CodeQL, Dependabot y ZAP.

**Estado de verificación:** 216 pruebas aprobadas y 6 de SQL Server que corren en el CI (incluye aplicar la migración nueva sobre datos existentes); `-warnaserror` limpio; sin paquetes vulnerables. Front: lint, 13 pruebas y build de producción en verde.

## Hecho el 2026-10-04 — respuestas en el chat y denuncias con debido proceso
- **Responder un mensaje en particular** (como en WhatsApp): deslizar a la derecha en el celular o botón "Responder" con el mouse; la respuesta muestra la cita y al tocarla lleva al original.
- **Denuncias escuchando a ambas partes:** si el moderador la considera procedente, la persona denunciada recibe el aviso (sin saber quién la denunció) y tiene 15 días para contar su versión una vez. Otro moderador (o un SuperUsuario) revisa la apelación; si la acepta, el contenido vuelve a mostrarse y también se avisa a quienes denunciaron. Pantallas: Cuenta → "Reportes sobre ti" y Moderación → "Apelaciones".
- Migración `ChatRespuestasYApelaciones` (solo columnas que admiten nulos y una tabla nueva). 227 pruebas en verde (incluye SQL Server real).

## Hecho el 2026-10-04 (segunda tanda)
- **Sesión estable:** ya no se cierra por un corte de red, un servidor reiniciándose o varias pestañas renovando a la vez; se renueva al volver a la pestaña o despertar el equipo.
- **Chat estilo WhatsApp:** ✓ enviado, ✓✓ entregado, ✓✓ celeste leído, "escribiendo…" y "en línea" (presencia en memoria o en Redis).
- **Foto de perfil** (sin GPS ni metadatos).
- **Diseño adaptable** a celular, tablet y PC (sin desbordes de 360 a 1440 px) y botón **Volver** en las pantallas secundarias.
- **Pagos y facturas:** explica para qué sirve; los datos fiscales son opcionales.
- **Bloquear usuarios**, **preferencias de avisos por correo** y **Centro de ayuda**.
- Migraciones `ChatEstadosYPresencia`, `FotoPerfil`, `BloqueosYPreferenciasAvisos`. 234 pruebas en verde (incluye SQL Server real); front: 15 pruebas y build de producción.

## Hecho el 2026-10-04 (tercera tanda)
- **Logo oficial** en la app, favicon e íconos.
- **Repositorio ordenado:** C4 reescrito con la arquitectura real, lo académico archivado en `docs/historico/`, script de migraciones al día, READMEs actualizados.

## Hecho el 2026-10-09 — front en GitHub Pages
- Workflow `.github/workflows/frontend-pages.yml`: compila el front con la subcarpeta del repo y lo publica en https://ingmarting.github.io/TRUEKE-BOGOTA-SOLIDARIO-/ (con `404.html` para que las rutas profundas carguen).
- El front ya no asume la raíz del dominio (`config.json`, manifiesto e imagen para redes son relativos).
- **Falta:** publicar el backend. Ya está todo preparado (`render.yaml` + `API_URL` en el workflow de Pages); solo faltan las cuentas y llaves: ver [`docs/DESPLIEGUE.md`](DESPLIEGUE.md).

- **ImageSharp 3.1.12 tuvo 5 avisos nuevos (2026-10-07)**, corregidos solo en la v4 (exige licencia). Primero se mitigó y se suprimieron con justificación; ese mismo día se resolvió del todo (ver la sección siguiente).

## Hecho el 2026-10-09 — fotos con SkiaSharp
- `ProcesadorImagenes` pasa de ImageSharp a **SkiaSharp 4.153** (MIT, sin llave) con binarios nativos de Linux sin dependencias del sistema. Mismo comportamiento: orientación del EXIF, máximo 1.600 px, sin metadatos, protección contra bombas de descompresión. Ahora además decodifica las fotos grandes ya reducidas (menos memoria) y rechaza explícitamente GIF/BMP/ICO aunque vengan con extensión de foto.
- Sin avisos suprimidos: `backend/Directory.Build.props` queda vacío (el mecanismo sigue disponible) y la auditoría de paquetes está limpia.
- 238 pruebas en verde (incluye SQL Server real); las del procesador también se corrieron en Linux (contenedor del SDK).

## Hecho el 2026-10-10 — varias personas interesadas por publicación
- Antes, la primera solicitud sacaba la publicación del catálogo y bloqueaba a los demás aunque no se concretara. Ahora varias personas pueden solicitarla y conversar; la publicación sigue visible con *"N personas interesadas"* hasta que el dueño **acepta** a una. Ahí queda **reservada** y las demás pasan a **lista de espera**. Si no se concreta, vuelve al catálogo y se avisa a la lista; si se completa, se otorgan los Eco-Puntos y la lista se cierra con aviso.
- Con interesados no se puede editar la publicación (nadie recibe algo distinto de lo que pidió). Cancelarla avisa a todas las personas interesadas.
- Migración de datos `LiberarPublicacionesSinAceptada`: las publicaciones que el sistema viejo dejó "en negociación" sin nadie aceptado vuelven a estar disponibles. 252 pruebas en verde, 6 nuevas en `ListaDeEsperaTests`.

## Hecho el 2026-10-10 — confianza: fotos y perfil
- **Foto de perfil obligatoria** (con el correo verificado) para publicar, editar, impulsar, solicitar, aceptar, chatear y pagar; la API responde 403 con `codigo: "foto_requerida"` y el front lleva a subirla. Comentar y denunciar no la exigen.
- **Foto de Google automática:** al entrar con Google sin foto propia se copia la de Google a 512 px (solo `*.googleusercontent.com`, sin redirecciones, con límite de tamaño y tiempo; si falla, el login sigue).
- **Visor de fotos** a pantalla completa (flechas, teclado, deslizar, zoom) en las publicaciones y en la foto de perfil.
- **Perfil público con señales de confianza:** correo e identidad verificados, 2FA, Google, en línea, intercambios completados, % concretados y tiempo típico de respuesta (último año), y distribución de estrellas. Sin datos privados.
- 259 pruebas del backend y 25 del front.

## Hecho el 2026-10-10 — miniaturas de fotos y CDN opcional
- Cada foto limpia se publica con una **miniatura WEBP de 640 px** (`.min.webp`, decodificada una sola vez con SkiaSharp). Tarjetas del catálogo, mis publicaciones, tiras de fotos y avatares usan la miniatura; el detalle y el visor, la foto grande. Si una miniatura no carga, el front usa la original.
- `MiniaturasHostedService`: al arrancar crea las miniaturas faltantes (fotos anteriores). Al borrar la foto de perfil se borra también su miniatura.
- **CDN opcional** (`Almacenamiento:CdnUrl`, apagada): `GET /configuracion` publica `imagenesOrigenUrl`/`imagenesCdnUrl` y el front reescribe solo para mostrar. Opciones y costos en `docs/DESPLIEGUE.md` §3.1.
- 267 pruebas del backend y 28 del front.

## Hecho el 2026-10-10 — búsqueda mejorada
- Cada publicación guarda su texto normalizado (`TituloBusqueda`, `TextoBusqueda`: minúsculas, sin tildes, con título, descripción, estado y categoría). Migración `BusquedaNormalizada`; las publicaciones existentes se rellenan al arrancar.
- La consulta se parte en palabras (sin "de", "la", "para"…, plurales simples al singular, máximo 6) y TODAS deben estar como inicio de palabra: "camara" encuentra "Cámara", "bicicletas rojas" encuentra "Bicicleta roja de montaña" y "sol" no encuentra "consola".
- Con texto, primero salen las publicaciones con más palabras en el título; después, destacadas y recientes.
- `GET /publicaciones/sugerencias` (anónimo): autocompletar con títulos de publicaciones visibles, sin repetidos. Buscador accesible (flechas, Enter, Escape) en el catálogo y el encabezado.
- Costo $0 (misma Azure SQL). Si el catálogo llega a decenas de miles de publicaciones, evaluar Full-Text Search de Azure SQL (incluido) o Azure AI Search (~US$75/mes en el plan básico).
- 280 pruebas del backend y 30 del front.

## Hecho el 2026-10-10 — vista previa al compartir
- `index.html`: `og:url`, `og:site_name`, `og:image:alt` y `twitter:card`; el workflow de Pages los deja con URL absoluta (variable opcional `FRONT_URL`).
- `GET /compartir/publicaciones/{id}` (anónimo, fuera de `/api/v1` y del OpenAPI): HTML con Open Graph (título, "Compra · $50.000 · Kennedy, Bogotá", descripción en una línea de máx. 200 caracteres) que redirige a la publicación. `…/imagen.jpg`: JPEG de 1200×630 recortado al centro desde la foto ya limpia (solo se leen fotos del contenedor propio: sin SSRF). Lo no público muestra la vista general.
- El botón *Compartir* del detalle usa ese enlace. `Urls__Api` en `render.yaml`. Costo $0.
- 286 pruebas del backend y 30 del front.

## Hecho el 2026-10-10 — monitoreo (quitado el 2026-10-18 al salir de Azure)
- `Presentacion/Monitoreo/Telemetria.cs`: Application Insights (OpenTelemetry, `Azure.Monitor.OpenTelemetry.AspNetCore` 1.6.0) con rol `trueke-api`, sin `/health`, logs del framework desde Warning, muestreo configurable (`Monitoreo:Muestreo`) y un procesador que quita la query string de todas las URLs (token del hub, llaves de terceros).
- `APPLICATIONINSIGHTS_CONNECTION_STRING` como secreto en `render.yaml`. Guía del portal con tope diario de 0,15 GB (nunca pasa los 5 GB gratis) y alertas en `docs/DESPLIEGUE.md` §7.1.
- Decisión: **sin chequeo de disponibilidad en la fase gratis** (mantendría despierta la API y agotaría la cuota de Azure SQL en 1-2 días). Listo para el plan pago en §7.2.
- 288 pruebas del backend.

## Hecho el 2026-10-10 — pruebas de punta a punta (Playwright)
- `frontend/e2e/`: 6 pruebas en PC y celular (12 corridas) contra las imágenes Docker de producción (API, front con nginx, SQL Server con migraciones, Redis). Correo por SMTP con STARTTLS real a Mailpit (CA de un solo uso con CRL), fotos en Azurite y pagos simulados. **La app no tiene ningún modo especial para pruebas.**
- Flujos: visitante (búsqueda con sugerencias, detalle, compartir), cuenta nueva (registro, verificación por correo, foto obligatoria, publicar con foto) y trueke completo entre dos personas (chat en tiempo real, confirmación y Eco-Puntos).
- Workflow `e2e.yml` en cada PR que toque `frontend/` o `backend/`; el CI del front revisa los tipos de las pruebas.
- **Errores reales que encontraron (corregidos):**
  - nginx reenviaba `Host` sin el puerto. La protección CSRF rechazaba el refresco de sesión y cerraba la sesión al recargar si el front corría en un puerto no estándar (p. ej. el `docker-compose` local en :8081).
  - El front no resincronizaba después de la PRIMERA conexión de tiempo real. Lo que llegaba entre la carga de la pantalla y la conexión (un mensaje del chat, una solicitud) no aparecía hasta recargar.

## Hecho el 2026-10-10 — PWA instalable y notificaciones push
- Service worker de Angular (`ngsw-config.json`): app instalable, abre sin conexión una vez cargada, nunca guarda respuestas de la API ni `config.json`. nginx sirve el service worker sin caché. Botón "Instalar la app" e instrucciones para iPhone en *Cuenta → Avisos*.
- Push con Web Push + VAPID (`Lib.Net.Http.WebPush` 3.3.1): tabla `SuscripcionesPush` (migración `SuscripcionesPush`, endpoint único `varchar(1000)`), `PUT /api/v1/push/suscripciones` y `POST …/baja`, cola + worker como el correo. Solo servicios de push reconocidos (sin SSRF). Mismo criterio que los correos (solo si no está en línea, nunca el texto del mensaje). Baja al salir, al cerrar todas las sesiones y al eliminar la cuenta; si otra cuenta usa el navegador, la suscripción pasa a ella.
- Llaves en Render (`Push__ClavePublica`/`Push__ClavePrivada`, §6.2 de DESPLIEGUE). Costo $0.
- **Encontrado por las E2E y corregido:** el service worker de Angular hace esperar TODAS las peticiones mientras se instala. Una petición a la API en ese momento (p. ej. crear la cuenta) quedaba colgada. Ahora:
  - Toda llamada a la API y al hub lleva `?ngsw-bypass` (en la query para no tocar CORS) y nunca pasa por el service worker.
  - De entrada solo se guarda lo básico (grupo `base`); el resto se guarda a medida que se usa.
- 306 pruebas del backend y 31 del front; E2E con Chromium completo (el "headless shell" reporta mal el permiso de notificaciones) y pruebas de PWA (service worker, sin conexión, sección de avisos).

## Hecho el 2026-10-11 — ofrecer un objeto propio en un trueke
- `Solicitud.PublicacionOfrecidaId` (migración `TruekeConObjeto`): al proponer un trueke se puede ofrecer una publicación propia, disponible y visible (opcional; el mismo objeto puede ofrecerse en varios truekes).
- **Aceptar** reserva los dos objetos. Quienes pidieron el objeto ofrecido quedan en lista de espera, y las otras propuestas donde se ofrecía cualquiera de los dos se cancelan con aviso.
- **Completar** intercambia los dos y cierra las dos listas de espera (un solo intercambio: +10 Eco-Puntos a cada uno). **No concretado** (manual o automático) devuelve los dos al catálogo y avisa a sus listas de espera.
- Si quien ofrece **retira** su objeto, se cierran sus propuestas (y un trueke ya aceptado no se concreta: lo pedido vuelve al catálogo). Si **moderación lo oculta**, se cierran las propuestas pendientes. Mientras esté ofrecido en un trueke en curso **no se puede editar**.
- Quien recibe la oferta puede ver el objeto aunque ya esté reservado (nadie más).
- Front: elección del objeto en "Proponer trueke" y tarjeta "Te ofrece / Ofreces a cambio" en *Mis intercambios*.
- 310 pruebas del backend (nuevas: `TruekeConObjetoTests`) y una E2E del trueke con objeto en pantalla.

## Hecho el 2026-10-11 — búsquedas guardadas con alertas
- Tabla `BusquedasGuardadas` (migración `BusquedasGuardadas`): los mismos filtros del catálogo, máximo 10 por persona, sin duplicados (texto normalizado), con nombre legible ("bicicleta · Trueke · Bogotá, D.C. · hasta $200.000").
- `GET/POST /api/v1/busquedas-guardadas`, `PUT …/{id}/alertas`, `DELETE …/{id}` (solo su dueño; exige correo verificado).
- **Alertas** en la tarea de mantenimiento de cada hora (no despierta la base más de lo que ya lo hace). Cuenta las publicaciones NUEVAS desde la última revisión (sin contar las propias) y envía como mucho un aviso por búsqueda por hora, en la app y por push. Al reactivar una alerta no avisa de lo publicado mientras estuvo apagada.
- Front: botón "Guardar búsqueda" en el catálogo, pantalla *Cuenta → Búsquedas guardadas* (ver resultados, alerta on/off, borrar), y la notificación abre los resultados.
- Se incluyen en "Exportar mis datos" y se borran con la cuenta.
- **Encontrado por las E2E y corregido:** la subida directa de fotos al almacenamiento (PUT con SAS) también pasaba por el service worker. Al reenviarla, podía fallar (504) y la foto no subía. Ahora:
  - Lleva `ngsw-bypass` como parámetro.
  - Si el almacenamiento rechazara ese parámetro (400/403), reintenta una vez sin él. **Verificar en producción** subiendo una foto después del despliegue.
- Las E2E locales usan 1 navegador a la vez: con Docker en el mismo equipo, 2 navegadores agotaban la memoria (`ERR_BLOB_OUT_OF_MEMORY`). En GitHub siguen siendo 2.

## Hecho el 2026-10-11 — documentación de staging y analítica (sin código)
- `docs/STAGING.md`:
  - Para qué sirve y cuándo vale la pena.
  - Arquitectura: front en Render Static Site, API en otro workspace de Render, base `TruekeDbStaging` (otra base gratis de Azure SQL), Storage propio y Wompi siempre en sandbox.
  - Costos: $0 si va en otro workspace, porque las 750 h gratis de Render son por workspace y al agotarse suspenden todo.
  - Reglas: nunca datos reales, secretos propios, `noindex`. Pasos para crearlo y retirarlo.
- `docs/ANALITICA.md`:
  - Medir sin rastrear personas: los indicadores del negocio salen de la base. Las consultas SQL se validaron contra SQL Server con el esquema real.
  - Comparación de herramientas para visitas: Cloudflare Web Analytics recomendada; GA4 y grabación de sesiones descartadas.
  - Pasos para activarla (CSP y política de datos) y qué no hacer.
- **Con esto se completan las 9 mejoras de la hoja de ruta del 2026-10-10.**

## Hecho el 2026-10-11 — auditoría de seguridad (PR 60 a 69)
Cada hallazgo de la auditoría se corrigió en su propio PR, con pruebas.
- **Archivos (#60, alto):** las subidas van a un contenedor privado con SAS de solo crear. Al usarlas, el servidor descarga el archivo una vez (condicionado al ETag) y publica **su propia copia**: las fotos siempre se re-codifican. Ya no sirve el metadato que el cliente podía poner para saltarse la limpieza, y nadie puede reemplazar un archivo aceptado.
- **Google + 2FA (#61):** el login con Google aplica el mismo bloqueo de 5 intentos y exige el segundo factor **antes** de vincular. Al vincular una cuenta con el correo sin verificar se quitan la clave y el 2FA que hubiera puesto un impostor.
- **Reautenticación (#62):** activar 2FA, crear o cambiar la clave exige confirmar la identidad (`ConfirmadorIdentidad`): la clave actual, que cuenta para el bloqueo, o un ID token de Google de la misma cuenta emitido hace menos de 10 minutos.
- **Moderación propia (#63):** nadie resuelve denuncias ni apelaciones sobre su propio contenido ni restaura lo suyo, ni siquiera un SuperUsuario.
- **Disputas (#64):** quien no confirmó la entrega puede marcar "no se concretó" aunque la otra parte ya confirmara. Así una sola persona no puede forzar el intercambio ni los puntos.
- **Moderar exige 2FA también en las rutas públicas (#65):** lo oculto, los motivos y las coordenadas exactas solo se ven con 2FA. Las validaciones de arranque se aplican en todo entorno publicado, no solo en `Production`: sin pagos ni correo simulados, y `Wompi:LlavePrivada` obligatoria.
- **Pagos (#66):** una aprobación de Wompi que llega después de un rechazo queda en `RequiereRevision`, con aviso a la persona y a moderación para reembolsarla.
- **Endurecimientos menores (#67):**
  - la tarjeta para compartir queda en caché y se generan como máximo 2 a la vez;
  - `Urls:Api` es obligatoria en producción;
  - "en línea" solo se muestra a quien tiene sesión;
  - cerrar sesión en otra pestaña limpia los datos de la página abierta.
- **CI (#68):**
  - acciones fijadas por SHA;
  - ZAP rechaza los hosts de producción;
  - CSP por `<meta>` en GitHub Pages;
  - la app no arranca dentro de un iframe ajeno (`core/sin-marcos.ts`).
- **Docker local (#69):**
  - puertos solo en `127.0.0.1`;
  - la API entra a la base con el usuario `trueke_app`, dueño solo de su base (hoy PostgreSQL, variable `DB_APP_PASSWORD`);
  - la clave de Redis ya no aparece en los argumentos del proceso;
  - imágenes fijadas por digest.

## Hecho el 2026-10-11 — .NET 10 y dependencias (PR 82 a 84)
- **.NET 10 LTS (#82):** los 4 proyectos en `net10.0`, con EF Core y ASP.NET Core 10. Las imágenes del SDK y del runtime van por digest y la app sigue con usuario sin privilegios. El contrato OpenAPI no cambió. .NET 8 dejaba de recibir parches el 10 de noviembre de 2026.
- **Dependabot revisado en bloque (#83):**
  - GitHub Actions, Redis 8 sin root, imágenes de las E2E y paquetes NuGet probados juntos;
  - Azure.Storage.Blobs exigía subir también Azure.Identity;
  - 337 pruebas del backend y 20 E2E correctas.
  - Los PR sueltos de Dependabot (70 a 81) se cerraron.
- **Docs (#84):** se quitaron referencias a archivos de herramientas locales.

## Hecho el 2026-10-11 — revisión del frontend (PR 85 a 95)
- **Contraste y foco (#85):** los tonos de Eco-Puntos, error y peligro pasan AA. Hay un token nuevo `borde-campo` para que se vean los campos, y el foco es visible en todo.
- **Aviso de comentarios (#86):** el dueño recibe la notificación `ComentarioNuevo` en la bandeja, en tiempo real y por push. No se avisa cuando comenta en su propia publicación, y el aviso no lleva datos de quien comentó.
- **Chat en celular (#87):**
  - la caja para escribir siempre se ve;
  - el nombre aparece en la tablet;
  - en pantallas táctiles, tocar un mensaje muestra sus acciones;
  - los avisos ya no tapan la caja.
- **Intercambios (#88):**
  - "Confirmar entrega" y "Aceptar y reservar" piden confirmación;
  - la foto de perfil se pide a tiempo, con `?volver=`;
  - los errores de red son claros.
- **Lectores de pantalla (#89):**
  - la directiva `CampoAccesible` marca `aria-invalid` y asocia el campo con su error;
  - el error aparece al salir del campo;
  - al cambiar de página, el foco va a `#contenido` y se anuncia el título nuevo.
- **Detalle (#90):** en celular el orden es foto → título, precio y botón → descripción. La foto principal carga con prioridad.
- **Inicio (#91):** solo cifras reales y la marca "Nacida en Bogotá, abierta a toda Colombia". La pantalla de carga pasa de 251 KB a 30 KB con `logo-carga.webp`: el mismo logo, solo reescalado.
- **Táctil y tipografía (#92):** botones de 44 px al tacto, nada legible por debajo de 12 px y mejor navegación en celular.
- **Modo oscuro y movimiento (#93):** no hay destello claro al cargar (el script en línea va con hash en la CSP), las pastillas tienen variante oscura y toda animación respeta "reducir movimiento".
- **Logro (#94):** `app-logro-intercambio` celebra el intercambio completado con los Eco-Puntos ganados y es accesible.
- **Borrador al publicar (#95):** lo escrito se guarda en el navegador, con una clave por cuenta, y se ofrece "Retomar" o "Empezar de cero". El foco va a cada paso.

## Hecho el 2026-10-12 — páginas nuevas (PR 96 a 101)
- **Historial de Eco-Puntos (#96):**
  - entidad inmutable `MovimientoEcoPuntos` (migración `HistorialEcoPuntos`), con el motivo, la referencia y el saldo resultante;
  - pantalla con el historial;
  - el bloque nuevo también está en `docs/migraciones/script-idempotente.sql`.
- **Cerca de ti (#97):** una sección en el inicio pide la ubicación solo al tocar el botón y no la guarda. Muestra hasta 8 publicaciones a menos de 5 km, con la distancia, y un enlace directo al mapa del catálogo.
- **Bienvenida guiada (#98):** `/bienvenida`, al confirmar el correo, muestra el regalo de Eco-Puntos y el progreso (foto de perfil, barrio) y propone qué hacer primero.
- **Rendimiento (#99):** `@angular/forms` sale del paquete inicial, que vuelve a quedar dentro del presupuesto.
- **Accesibilidad (#100):** declaración pública `/accesibilidad` (WCAG 2.2 AA), enlazada en el pie.
- **Impacto (#101):** `GET /api/v1/impacto` anónimo, autorizado por el dueño, y la página pública con totales de la comunidad.
  - Datos: objetos con nuevo hogar por modo, personas que han intercambiado, intercambios del mes y las localidades más activas.
  - Privacidad: una localidad aparece solo desde 3 intercambios y nunca hay ids, nombres ni correos.
  - Caché de 10 minutos.
  - 341 pruebas del backend.

## Hecho el 2026-10-16 — puntos seguros de encuentro (PR 109)
- Se recomiendan **tipos de lugar, sin nombres de sedes**: estación de TransMilenio (o de transporte público), centro comercial, biblioteca pública y CAI de la Policía.
- **Detalle de la publicación:** cada lugar aparece con su consejo.
- **Chat:** botones para proponer el lugar, en la conversación vacía y con el botón del escudo. Solo escriben la propuesta: la persona la edita y la envía.
- La ayuda usa los mismos lugares. Hay una prueba del componente y un paso E2E en el chat.

## Hecho el 2026-10-17 — lista de deseos ("Lo que busco")
- **Lista:** hasta 10 deseos por persona (texto corto y categoría opcional), con dos interruptores: "En mi perfil" y "Avisarme". Pantalla `/lo-que-busco`.
- **Coincidencias** (`GET /api/v1/deseos/coincidencias`):
  - publicaciones de otras personas que coinciden, con la misma búsqueda del catálogo;
  - primero los **truekes mutuos**: la otra persona busca, en público, algo que tú tienes disponible;
  - "Proponer trueke" abre la propuesta con tu objeto ya elegido (`/publicacion/{id}?ofrecer=…`).
- **Perfil público:** sección "Lo que busca" con la marca "Lo tienes" para quien mira con sesión. Solo se muestran los deseos públicos.
- **Avisos:** la tarea horaria avisa de publicaciones nuevas que coinciden, sin repetir (notificación `DeseoCoincidencia`).
- **Moderación y datos:** los deseos se denuncian y se ocultan con apelación, se exportan con los datos personales y se borran al eliminar la cuenta. Se omiten las personas bloqueadas.
- Migración `ListaDeDeseos` (solo una tabla nueva), también en `docs/migraciones/script-idempotente.sql`. Pruebas de integración, del componente y un E2E.

## Hecho el 2026-10-17 — base de datos en PostgreSQL (Supabase)
Azure SQL gratis agotó su cupo del mes y se pausó (error 42119): la API de Render no arrancaba porque migra al iniciar. Además, la cuenta gratuita de Azure vence a los 30 días. Por eso se sale de Azure en tres pasos; este es el primero:
- **Motor:** SQL Server se reemplazó del todo por PostgreSQL con el proveedor Npgsql de EF Core. Producción usa Supabase Free por el Session pooler (IPv4). `Database:Provider` admite `InMemory` (desarrollo) o `PostgreSQL`.
- **Concurrencia:** el token de concurrencia es la columna de sistema `xmin` de PostgreSQL. Dos cambios simultáneos siguen dando 409.
- **Fechas:** todas las fechas son `timestamptz` (UTC); las estadísticas diarias van en `date`.
- **Migraciones:**
  - Se empezó con la base vacía: una sola migración `Inicial`, más `CerrarDataApiSupabase`. Esta última les quita a `anon` y `authenticated` los permisos de la Data API de Supabase y activa RLS en todas las tablas.
  - Las 17 migraciones de SQL Server y su script quedaron archivados en `docs/historico/migraciones-sqlserver/`.
- **Pruebas:**
  - Las de `TruekeBogotaSolidario.Pruebas/PostgreSql` corren contra PostgreSQL 17 real (Testcontainers): migraciones, flujo completo de la API, monetización, 409, fechas UTC, índices únicos y la Data API cerrada.
  - El CI las exige (no pueden quedar omitidas).
- **Docker local y E2E:** `postgres:17-alpine` con el usuario `trueke_app`, dueño solo de la base `trueke`. Variables `POSTGRES_PASSWORD` y `DB_APP_PASSWORD`, que reemplazan a `SQL_SA_PASSWORD` y `SQL_APP_PASSWORD`.
- **Guía:** `docs/DESPLIEGUE.md` §2 explica cómo crear el proyecto en Supabase y configurar Render.

## Hecho el 2026-10-17 — fotos y documentos en Supabase Storage
Segundo paso para salir de Azure: Azure Blob se reemplazó por **Supabase Storage**, en el mismo proyecto de la base.
- **Almacén:** `AlmacenSupabase` usa la API REST de Storage con la llave secreta del proyecto (solo en el servidor). Se quitaron los paquetes `Azure.Storage.Blobs` y `Azure.Identity`.
- **Subida directa:** la API pide una URL firmada (`createSignedUploadUrl`) para **un** archivo nuevo en el bucket privado `subidas`. El navegador sube con un `PUT` y no puede sobrescribir. Ya no hacen falta las cabeceras `x-ms-blob-type` ni una regla CORS.
- **Igual que antes:** al usarla, el servidor descarga el archivo una vez (≤ 5 MB), revisa los *magic bytes*, re-codifica la foto (sin EXIF/GPS), publica la copia limpia y su miniatura `.min.webp` con un nombre nuevo y `Cache-Control` inmutable, y borra la subida. Las subidas sin usar se borran a la hora.
- **Buckets:** la API los crea y les fija la configuración al arrancar. `imagenes` es público y de solo lectura; `documentos` y `subidas` son privados, y los documentos se ven con un enlace firmado de 5 minutos. Además, cada bucket rechaza por sí mismo más de 5 MB y los tipos no permitidos.
- **Diferencia con Azure:** la URL firmada de subida vence a las 2 horas (Supabase no permite acortarla; la SAS duraba 5 minutos). Sirve para un solo archivo privado que nunca se publica tal cual.
- **Pruebas:** Azurite se reemplazó por `supabase/storage-api`, el mismo servidor que corre Supabase:
  - en .NET, con Testcontainers: subida firmada real, no sobrescribe, rechazo de tipos y tamaño, la llave pública no escribe, limpieza y miniaturas;
  - en las E2E, detrás de una puerta nginx con CORS y el prefijo `/storage/v1`, como el gateway de Supabase.

  Se descartó MinIO: por la API S3 no existe la URL firmada de subida de Supabase, así que no se probaría el camino real. Para desarrollo: `backend/docker-compose.storage.yml` (puerto 8082).
- **CSP:** GitHub Pages permite `https://*.supabase.co` o, con la variable del repo `STORAGE_ORIGEN`, solo el proyecto. En nginx se usa `CSP_IMG_EXTRA`/`CSP_CONNECT_EXTRA` con el origen del proyecto.
- **Guía:** `docs/DESPLIEGUE.md` §3.

## Hecho el 2026-10-18 — correo con Brevo y fin de Azure
Tercer y último paso para salir de Azure: ya no queda nada en Azure.
- **Correo:** Azure Communication Services se reemplazó por **Brevo** (`TransporteCorreoBrevo`): `POST https://api.brevo.com/v3/smtp/email` con la cabecera `api-key`. El plan gratis da 300 correos al día y no exige dominio propio; Render gratis bloquea el SMTP, por eso va por HTTPS. Se quitó el paquete `Azure.Communication.Email`.
  - Configuración: `Correo__Proveedor=Brevo`, `Correo__Brevo__ClaveApi` (secreto) y `Correo__Remitente` (remitente verificado en Brevo). Sin la clave, la API no arranca.
  - Cliente HTTP con nombre, timeout de 15 s y sin seguir redirecciones (la clave solo viaja a Brevo). Si Brevo responde un error, se lanza solo con el código HTTP (la respuesta puede traer el destinatario) y el worker reintenta 3 veces, como con SMTP.
  - Se conservan `Smtp` (E2E con Mailpit, plan pago) y `Simulado` (desarrollo).
  - Pruebas: `CorreoBrevoTests` con un `HttpMessageHandler` falso (URL, cabecera, JSON, errores 4xx/5xx y arranque sin clave).
- **Monitoreo:** se quitaron Application Insights y OpenTelemetry (`Azure.Monitor.OpenTelemetry.AspNetCore`, `Telemetria.cs` y `APPLICATIONINSIGHTS_CONNECTION_STRING`). Los errores quedan en los logs de Render.
- **Guía:** `docs/DESPLIEGUE.md` §5.1 (cuenta, remitente verificado, clave API e IPs autorizadas de Brevo).

## Hecho el 2026-10-18 — solo para mayores de 18 años
- El registro (correo y Google) pide la fecha de nacimiento y rechaza a menores sin crear la cuenta ni guardar la fecha.
- Las cuentas anteriores a la regla quedan bloqueadas hasta registrarla en `/completar-perfil`: el token lleva el claim
  `edad`, la política de autorización por defecto lo exige y la API responde 403 `fecha_nacimiento_requerida`. Siguen
  abiertos la sesión, la seguridad de la cuenta, los avisos, la PQR y exportar o eliminar los datos.
- La fecha se registra una sola vez, sale en la exportación de datos y nunca en el perfil público.

## Hecho el 2026-10-18 — cada campo acepta solo lo que le corresponde
- Catálogo único de formatos en `Datos/Common/FormatosTexto.cs`, usado por los DTO (`[RegularExpression]`, error por campo)
  y por las entidades (nada entra por otra vía). El front repite las reglas en `shared/validadores.ts`:
  - nombre de persona: solo letras (con tildes, ñ y diéresis), espacio, apóstrofo, guion y punto; en registro y perfil, nombre y apellido;
  - nombre comercial o razón social: letras, dígitos, espacio y `& . , - ' ( )` ("3M");
  - localidad o barrio: letras, dígitos, espacio y `# - . °`, con al menos una letra ("20 de Julio");
  - NIT: dígitos y puntos con el DV opcional tras un guion (el DV se comprueba con el módulo 11 en el front y en la API);
  - documento de facturación según el tipo (CC solo dígitos; CE y Pasaporte letras y dígitos) y nombre de la factura
    como persona o, si es NIT, como razón social;
  - dirección: letras, dígitos, espacio y `# - . , ° /`.
- Se toleran espacios al inicio, al final y repetidos: se guardan recortados y con un solo espacio.
- Los datos ya guardados no se rompen: la regla se aplica al guardar. Los datos de facturación guardados se leen con las
  reglas de antes (`DatosComprador.Restaurar`), y el perfil muestra el error cuando la persona lo edita.
- Google: la cuenta se crea con el nombre real, bien escrito: se quitan dígitos y emojis, y cada palabra empieza con
  mayúscula, salvo las partículas ("MARÍA DE LA TORRE" → "María de la Torre"). Solo si no queda nada usable se usa
  "Usuario Trueke".
- **Sin nombre y apellido reales solo se explora:** si falta (nombre provisional, una sola palabra o un nombre guardado
  antes de la regla), publicar, solicitar, comentar, chatear, denunciar, subir archivos y pagar responden 403
  `nombre_requerido`. El front lleva a Mi cuenta → Perfil con un aviso y, al guardar, vuelve a donde estaba
  (`UsuarioDto.nombreRealPendiente`).
- `TextoSeguro` común para todos los textos libres (títulos, descripciones, comentarios, chat, denuncias, PQRS, deseos…):
  rechaza los caracteres de control (salvo saltos de línea donde se admiten) y los invisibles de engaño (inversión de
  sentido U+202A–202E y U+2066–2069, ancho cero U+200B, U+FEFF, U+2060 y U+180E). Los emojis siguen funcionando.
- En el front: teclado numérico y filtro al escribir o pegar en la CC y el NIT, y mensajes claros por campo.

## Hecho el 2026-10-18 — legal a fondo y aplicado en la app
- **Responsable del tratamiento configurable** (el repositorio es público): `Legal__ResponsableNombre`, `Legal__Domicilio`
  (dirección de notificación y ciudad), `Legal__CorreoContacto` y `Legal__Telefono` son obligatorios en producción (la API
  no arranca sin ellos); `Legal__ResponsableDocumento` es opcional. Decisión de los dueños (2026-10-18): por ahora figura una
  persona natural; cuando constituyan la SAS se cambian las variables por la razón social, el NIT y su dirección. Se publican en `GET /configuracion` (`legal`) junto con las versiones vigentes.
- **Política de tratamiento de datos** reescrita (Ley 1581 y Decreto 1377, compilado en el 1074 de 2015): responsable,
  datos por categoría, finalidades necesarias y opcionales, datos sensibles, menores, encargados y transferencias
  internacionales (Supabase, Render, GitHub Pages, Brevo, Wompi y Google), derechos, consultas (10 días hábiles + 5) y
  reclamos (15 + 8), conservación, cookies, seguridad, vigencia y nota del RNBD.
- **Términos de uso** reescritos: prestador, portal de contacto (Ley 1480, art. 53), mayoría de edad, lista de
  publicaciones prohibidas, Eco-Puntos sin valor monetario, pagos, retracto y reversión, licencia sobre el contenido,
  moderación con debido proceso, responsabilidad, Ley 527, cambios con aviso y ley colombiana.
- **Declaración de accesibilidad** con estado (cumplimiento parcial de WCAG 2.2 AA), método y fecha de evaluación,
  limitaciones reales y canal con plazo. Plantilla legal con versión, vigencia, índice con anclas e impresión.
- **Dos casillas separadas** al registrarse y al crear la cuenta con Google: autorización de datos y aceptación de los
  términos con la declaración de mayoría de edad. Se guardan `TerminosVersion` y `FechaAceptacionTerminos`
  (migración `AceptacionTerminos`).
- **Cambio de versión:** si cambia `Legal__VersionPoliticaDatos` o `Legal__VersionTerminos`, el token lleva el claim
  `legal=pendiente` y la API responde 403 `terminos_desactualizados` (salvo lo exento: sesión, perfil, habeas data y PQRS).
  El front lleva a `/completar-perfil`, que muestra las dos casillas (`PUT /usuarios/yo/aceptar-legal`).
- **Avisos contextuales** con enlace: dato sensible al subir la foto y el documento, prohibidos al publicar, retracto al
  pagar y no compartir datos en el chat. El pie muestra el responsable y el enlace a PQRS.
- **PQRS pública** (autorizada por el dueño el 2026-10-18): `/pqrs` sin cuenta, con captcha, límite por IP y por correo
  (`POST /pqr/publica`) y consulta con radicado + correo (`POST /pqr/consultar`, 404 genérico si no coinciden). Tipo
  nuevo `Consulta` (10 días hábiles) y estado `EnTramite` (el equipo lo marca y se le avisa a quien la radicó). Los plazos
  se cuentan en **días hábiles colombianos**, sin festivos (fijos, ley Emiliani y los de Pascua). Migración `PqrsPublica`.

## Hecho el 2026-10-18 — asistente de ayuda sin IA (preparado para IA)
- **Base de conocimiento única** en `frontend/src/app/features/ayuda/conocimiento.ts`: cada entrada tiene id, pregunta,
  variantes, palabras clave, respuesta, acción y tema. La usan el Centro de ayuda, las preguntas de Planes y de Cómo
  funciona, y el asistente; ya no hay preguntas repetidas en tres archivos. Las cifras (puntos, precios, recargas,
  invitaciones, prueba Premium…) salen en vivo de `/eco-puntos/politica`; si no cargó, la respuesta va sin números.
- **Motor de búsqueda** (`motor.ts`, sin red): sin tildes ni mayúsculas, plurales, sinónimos ("clave" = "contraseña",
  "cobro" = "pago"…) y un error de tecleo en palabras largas. El buscador del Centro de ayuda también lo usa.
- **Asistente** (`asistente-soporte.ts`): botón "Ayuda" flotante en todas las páginas (salvo el chat abierto y el
  formulario de publicar, donde taparía la barra), cargado con `@defer (on idle)` fuera del paquete inicial. Es un
  `<dialog>` modal (foco atrapado, Esc lo cierra y el foco vuelve al botón), hoja inferior en el celular, modo oscuro y
  movimiento reducido. Ofrece temas según la sección, responde con un botón de acción ("Ir a mis pagos", "Subir foto";
  sin sesión pasa por ingresar) y pregunta "¿Te sirvió?".
- **Escalado a PQRS:** si no sirvió o no encontró nada, "Crear PQRS" con el tipo sugerido. Con sesión y correo
  confirmado va a `/cuenta/soporte?tipo=…`; si no, a `/pqrs?tipo=…` (sin sesión, retracto y reversión pasan a reclamo).
  El asunto y la conversación se precargan **en memoria** (`BorradorPqrs`), no en la URL, para no dejar datos personales
  en el historial ni en registros.
- **`ProveedorRespuestas`:** interfaz con la implementación local. Una futura `ProveedorIA` llamaría a un endpoint propio
  del backend con límite de uso (la llave nunca en el front); esta fase no llama a ninguna IA ni cambia la API.
- La medición de "preguntas sin respuesta" **no se hizo**: hoy no hay analítica en el front (`docs/ANALITICA.md` solo la
  documenta) y exigiría un endpoint nuevo. Queda como mejora cuando se active la analítica.
- Pruebas: motor (coincidencias, sin coincidencia, cifras en vivo y escalado) en vitest, y E2E `e2e/asistente.spec.ts`
  (visitante hasta la PQRS pública; con sesión hasta radicar en Soporte).

## Hecho el 2026-10-18 — auditoría integral
Informe completo, con los hallazgos por severidad, en `docs/AUDITORIA_2026-10-18.md`.
- **#155:** un fragmento JS que no carga ya no hace fallar el ingreso. Era el "Ocurrió un error inesperado" intermitente del E2E en celular. Ahora se carga la página destino completa una vez.
- **#156:** el acuse de la PQRS pública no repite el texto de quien radica, y cambiar un rol no devuelve la fecha de nacimiento.
- **#157:** el foco va al primer error al enviar (`EnfocarError`), con ajustes de accesibilidad en los formularios.
- Se actualizaron las menciones viejas a Application Insights y Azure en la documentación.

## Hecho el 2026-10-18 — pendientes de la auditoría integral
- **#159, PQRS pública con límite propio:** `POST /pqr/publica` y `/pqr/consultar` usan la política `pqr-publica`, solo por IP,
  con 3 por minuto y 10 por hora (`RateLimiting__PqrPublicaPorMinuto` y `RateLimiting__PqrPublicaPorHora`), también en el
  limitador de Redis. Ya no comparten el límite del ingreso. Las PQRS públicas avisan a la moderación en un resumen cada
  10 minutos ("N PQRS públicas nuevas"); las de usuarios con cuenta siguen avisando una por una.
- **#160, cuenta existente que declara ser menor** (decisión del dueño del 2026-10-18: marcar y revisar): el primer intento con
  una fecha de menor en `PUT /usuarios/yo/fecha-nacimiento` marca `Usuario.MenorDetectadoUtc` (migración `MenorDetectado`),
  no guarda la fecha, oculta sus publicaciones activas y avisa a la moderación. Los reintentos responden 403
  `menor_de_edad_detectado` y el token lleva `edad=menor`. Conserva la sesión, exportar y eliminar sus datos y la PQRS; el
  saldo de Eco-Puntos queda congelado porque no puede operar. La moderación la encuentra con el filtro "En revisión por
  edad" y, si comprueba que fue un error de digitación, la descarta (`POST /admin/usuarios/{id}/descartar-menor`, con
  motivo en la auditoría); si es menor, la suspende. En el front, la fecha de menor se envía solo después de confirmarla.
- **#161, registro de oferentes** (Ley 1480, art. 53; decisión del dueño del 2026-10-18: solo para vender en modo Compra):
  publicar o editar en Compra sin el registro responde 403 `datos_oferente_requeridos` y el front lleva a
  `/cuenta/vendedor` (el borrador de la publicación se conserva). `PUT /usuarios/yo/oferente` guarda nombre, documento
  (validado con `DocumentosFiscales`), dirección de notificaciones, municipio y teléfono de Colombia; documento, dirección y
  teléfono van cifrados con `Seguridad:ClaveCifrado` (migración `RegistroOferentes`). Nunca se devuelve a terceros: el
  titular lo ve en `/cuenta/vendedor` y en la exportación; la moderación (con 2FA) lo consulta solo desde una PQRS de
  alguien que completó una compra con esa persona (`GET /admin/pqr/{id}/vendedores` y `.../{usuarioId}/oferente`), y cada
  consulta queda en la auditoría (`OFERENTE_CONSULTADO`). Se borra al eliminar la cuenta. Política de datos, términos y
  asistente actualizados; **versión legal `2026-10-19`**, que pide reaceptar a todos.

## Hecho el 2026-10-19 — auditoría externa (sin credenciales)
- **SEO:** el build de Pages genera `robots.txt`, `sitemap.xml` y una copia de `index.html` por ruta pública y por
  publicación (`scripts/generar-seo.mjs`). Antes, Pages servía todas las rutas desde `404.html` con HTTP 404 y Google no
  las indexaba. Se regenera cada 6 h. Falta registrar el sitemap en Google Search Console (Google no lee un `robots.txt`
  dentro de la subcarpeta del repositorio; con dominio propio sí).
- **Arranque en frío:** workflow *Mantener la API despierta* (`/health/ready` cada 10 min) y aviso en la pantalla de carga.
- **404 real:** una ruta inexistente responde 404/405 también sin sesión (antes, 401 "sesión expirada").
- **Ya estaban bien:** cabeceras de seguridad, CORS con lista de orígenes, límites de peticiones, Swagger cerrado, Storage
  sin listado y Data API cerrada. Lo público por diseño quedó documentado en `backend/README.md` §11.1.

## Pendiente
**De la auditoría del 2026-10-18 (ver `docs/AUDITORIA_2026-10-18.md`):**
- **Registro de oferentes:** que el abogado revise el texto de la política y los términos (versión `2026-10-19`), si el
  registro debe conservarse un tiempo tras eliminar la cuenta, y si las ventas publicadas antes del registro deben pedirlo
  (hoy se pide al crear o editar una venta).
- **Producto:** registrar el sitemap en Google Search Console y, como respaldo del workflow, un monitor externo de `/health/ready`.

**Despliegue y operación:**
- **Versión legal en Render:** si `Legal__VersionPoliticaDatos`, `Legal__VersionTerminos` o `Legal__FechaVigencia` están
  definidas allí, cambiarlas a `2026-10-19` al desplegar el registro de oferentes (si no, se usa la de `appsettings.json`).
- **Configurar en Render** `Legal__ResponsableNombre`, `Legal__Domicilio`, `Legal__CorreoContacto` y `Legal__Telefono` (y, si
  se quiere, `Legal__ResponsableDocumento`) **antes** de desplegar la fase legal: sin ellos la API no arranca.
- **Cuando exista la SAS** (Cámara de Comercio y RUT): cambiar las variables `Legal__*` a la razón social, el NIT y la dirección
  de la sociedad; subir la versión de la política y los términos si el abogado lo indica; y replantear la Fase 4 (aportes),
  que hoy supone que el dinero lo recibe una persona natural.
- **Brevo con un Gmail como remitente:** algunos correos pueden caer en spam. Con dominio propio, autenticarlo en Brevo (DKIM y DMARC) y usar `no-responder@tudominio`.
- **Monitoreo de errores:** solo hay logs de Render. Si hace falta más, un servicio de errores con plan gratis.
- **Supabase Free pausa el proyecto tras 7 días sin actividad:** se reactiva con *Restore project* (los datos no se pierden). Un monitor de `/health/ready` lo evita (`docs/DESPLIEGUE.md` §7.2).
- **Dominio propio:**
  - resuelve la cookie de sesión entre sitios distintos (Safari/iPhone);
  - permite cambiar la cookie a `SameSite=Strict`.
- **Servidor:**
  - hoy la plataforma corre en la fase gratis (front en GitHub Pages, API en Render Free, que se duerme a los 15 minutos);
  - se evalúa un VPS o un plan pago para el tráfico real, y sacar el front de GitHub Pages;
  - guía en `docs/DESPLIEGUE.md`.
- **Wompi en producción:**
  - cambiar `BaseUrl` en `render.yaml` y poner las llaves de producción;
  - **antes**, borrar los datos de prueba, como promete la interfaz.
- **Después de cada despliegue:** verificar en producción la subida de fotos, por el `ngsw-bypass`.
- **Dependabot:** revisar y probar juntos los PR abiertos (102 a 108), como se hizo en #83.

**Con terceros:**
- **Contador / abogado:**
  - confirmar el régimen de IVA (`Facturacion:ResponsableIva`);
  - el tratamiento tributario de las recargas de Eco-Puntos;
  - la resolución de facturación de la DIAN;
  - el texto final de los términos, la política de datos y la declaración de accesibilidad (versión 2026-10-18);
  - si el responsable debe inscribir las bases de datos en el RNBD (hoy no, por ser persona natural).
- **Cuando haya contratos:** cobro recurrente con fuentes de pago de Wompi y factura electrónica automática con un proveedor.

**Producto (por decidir):**
- Revisión de la monetización: planes, precios y beneficios.
- Funciones grandes: logros, fotos en el chat.
- Opcional: una cifra de impacto en el inicio.

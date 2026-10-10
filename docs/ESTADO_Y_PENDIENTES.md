# Estado del backend — Trueke Bogotá Solidario (2026-10-03)

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
- editar publicaciones y hasta 5 fotos (Azure Blob con SAS);
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
- Application Insights (OpenTelemetry);
- correos HTML;
- tarea horaria de cierre y purga;
- pruebas contra SQL Server real (Testcontainers) obligatorias en el CI.

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

## Pendiente (fuera del código)
- **Subir la rama:** GitHub Desktop → Publish branch → Pull Request → CI en verde → Merge.
- **Dependabot:** `gh auth login` y cerrar los PR de .NET 10, EF Core 9, TypeScript 7 y Node 26 (los demás, fusionar si el CI pasa).
- **Ya listo en local:** Docker, Google (inicio de sesión) y correo Gmail.
- **Llaves externas pendientes:** reCAPTCHA v3, Wompi y, para producción, Azure (SQL, Redis, Storage, App Service, Key Vault, Application Insights), un correo transaccional y el dominio. Ver la sección 7.1 de `backend/README.md`: dónde se obtiene cada una.
- **Logo en alta resolución** (1024 px o SVG) para el ícono grande del celular.
- **Contador / abogado:** confirmar el régimen de IVA (`Facturacion:ResponsableIva`), el tratamiento tributario de las recargas de Eco-Puntos, la resolución de facturación de la DIAN y el texto final de términos y política de datos.
- **Cuando haya contratos:** cobro recurrente con fuentes de pago de Wompi y emisión automática con la API de un proveedor de factura electrónica.

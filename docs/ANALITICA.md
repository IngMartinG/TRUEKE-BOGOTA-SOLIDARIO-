# Analítica: qué medir y con qué

> **Estado:** solo documentado. No hay ningún script de analítica en el front todavía. Esta guía dice qué medir, con qué herramienta y los pasos para activarla cuando se decida.

## Principio: medir sin rastrear personas
Trueke maneja datos personales (Ley 1581 de 2012) y la confianza es el producto. Por eso:
1. **Las métricas del negocio salen de la propia base de datos:** registros, publicaciones, intercambios y tiempos de respuesta. No se agrega ningún rastreador, y los datos ya existen y son exactos.
2. **Para las visitas** (cuántas personas entran, desde dónde llegan, qué páginas ven y qué tan rápido carga) se usa una herramienta **sin cookies y sin datos personales**.
3. **Nunca** se envía a terceros: correos, nombres, textos de mensajes, ubicaciones exactas ni identificadores de usuario.
4. **Nunca** se graban sesiones (mapas de calor, grabación de pantalla): capturarían chats, formularios y fotos.

## Qué medir (indicadores)
| Indicador | Qué dice | De dónde sale |
|---|---|---|
| Cuentas nuevas por semana y % que confirma el correo | Si la gente llega y termina el registro | Base (`Usuarios`) |
| Publicaciones nuevas por semana, por modo | Si hay oferta (sin oferta no hay plataforma) | Base (`Publicaciones`) |
| Embudo: solicitudes → aceptadas → completadas | Dónde se caen los intercambios | Base (`Solicitudes`) |
| Horas hasta la primera respuesta del dueño | Si la comunidad responde a tiempo | Base (`Solicitudes`) |
| Intercambios completados por modo | El resultado real (y el impacto ambiental) | Base (`Transacciones`) |
| Personas activas en 30 días | Si vuelven (retención) | Base (`Publicaciones` + `Solicitudes`) |
| Visitas, páginas más vistas, de dónde llegan, dispositivo | Si la difusión funciona (WhatsApp, redes) | Herramienta de visitas |
| Velocidad de carga real (Core Web Vitals) | Si el celular promedio la siente rápida | Herramienta de visitas |
| Errores y lentitud de la API | Salud técnica | Application Insights (ya activo) |

## Consultas para la base (Azure SQL)
Se corren en Azure → base `TruekeDb` → **Editor de consultas**.
- Cada consulta **despierta la base** y gasta un poco de la cuota gratis: correrlas como mucho una vez por semana, todas juntas.
- Son solo de lectura y no muestran datos personales: solo conteos.

```sql
-- Cuentas nuevas por semana (últimas 8) y % que confirmó el correo
SELECT DATEPART(iso_week, FechaRegistro) AS Semana, COUNT(*) AS Nuevas,
       CAST(100.0 * SUM(CASE WHEN CorreoVerificado = 1 THEN 1 ELSE 0 END) / COUNT(*) AS DECIMAL(5,1)) AS PctVerificado
FROM Usuarios
WHERE FechaRegistro >= DATEADD(week, -8, SYSUTCDATETIME()) AND EstaEliminado = 0
GROUP BY DATEPART(iso_week, FechaRegistro) ORDER BY Semana;

-- Publicaciones nuevas por semana y modo
SELECT DATEPART(iso_week, FechaPublicacion) AS Semana, Modo, COUNT(*) AS Publicaciones
FROM Publicaciones
WHERE FechaPublicacion >= DATEADD(week, -8, SYSUTCDATETIME())
GROUP BY DATEPART(iso_week, FechaPublicacion), Modo ORDER BY Semana, Modo;

-- Embudo de los últimos 30 días
SELECT COUNT(*) AS Solicitudes,
       SUM(CASE WHEN FechaAceptacionUtc IS NOT NULL THEN 1 ELSE 0 END) AS Aceptadas,
       SUM(CASE WHEN Estado = 'Completada' THEN 1 ELSE 0 END) AS Completadas,
       SUM(CASE WHEN Estado = 'NoConcretada' THEN 1 ELSE 0 END) AS NoConcretadas
FROM Solicitudes
WHERE FechaSolicitud >= DATEADD(day, -30, SYSUTCDATETIME());

-- Horas hasta que el dueño acepta (mediana aproximada y promedio, últimos 30 días)
SELECT AVG(CAST(DATEDIFF(minute, FechaSolicitud, FechaAceptacionUtc) AS FLOAT)) / 60 AS HorasPromedio,
       (SELECT TOP 1 PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY DATEDIFF(minute, FechaSolicitud, FechaAceptacionUtc)) OVER () / 60.0
        FROM Solicitudes WHERE FechaAceptacionUtc IS NOT NULL AND FechaSolicitud >= DATEADD(day, -30, SYSUTCDATETIME())) AS HorasMediana
FROM Solicitudes
WHERE FechaAceptacionUtc IS NOT NULL AND FechaSolicitud >= DATEADD(day, -30, SYSUTCDATETIME());

-- Intercambios completados por modo (últimos 30 días)
SELECT Modo, COUNT(*) AS Completados FROM Transacciones
WHERE FechaUtc >= DATEADD(day, -30, SYSUTCDATETIME()) GROUP BY Modo;

-- Personas activas en 30 días (publicaron o pidieron algo)
SELECT COUNT(DISTINCT Id) AS Activas FROM (
  SELECT PropietarioId AS Id FROM Publicaciones WHERE FechaPublicacion >= DATEADD(day, -30, SYSUTCDATETIME())
  UNION SELECT SolicitanteId FROM Solicitudes WHERE FechaSolicitud >= DATEADD(day, -30, SYSUTCDATETIME())
) a;
```
Si un día se quiere verlas en la app, el paso natural es una pantalla de **Administración → Indicadores** (solo SuperUsuario y con 2FA), que haga estos mismos conteos.

## Herramienta para las visitas: comparación
| Herramienta | Costo | Cookies / consentimiento | Qué da | Veredicto |
|---|---|---|---|---|
| **Cloudflare Web Analytics** | Gratis | Según Cloudflare, sin cookies ni localStorage y sin huella por IP o navegador ([fuente](https://www.cloudflare.com/web-analytics/)); hay revisiones independientes que discuten el detalle ([ejemplo](https://www.ctrl.blog/entry/review-cloudflare-analytics/)) | Visitas, páginas, procedencia, países, dispositivos, Core Web Vitals | **Recomendada para empezar.** No exige cambiar el DNS ni usar el proxy de Cloudflare. |
| Plausible | De pago (revisar el precio vigente) | Sin cookies | Lo mismo, con mejor detalle y eventos | Buena opción si se quiere pagar |
| Application Insights (navegador) | Dentro de los 5 GB gratis | Usa cookies (`ai_user`, `ai_session`) salvo que se desactiven | Rendimiento y errores del front | Útil para errores del front, no para visitas |
| Google Analytics 4 | Gratis | Cookies y envío de datos a Google: exige **banner de consentimiento** | Muy completo | **No recomendada:** pone en riesgo la promesa de privacidad |
| Microsoft Clarity / grabación de sesiones | Gratis | Graba lo que ve la persona | Mapas de calor y grabaciones | **No usar:** capturaría chats, formularios y fotos |

## Pasos para activar Cloudflare Web Analytics (cuando se decida)
1. Crear una cuenta gratis en Cloudflare → **Web Analytics** → *Add a site* → escribir el dominio (`ingmarting.github.io` o el dominio propio) → **sin** cambiar el DNS (opción de "JS snippet").
2. Cloudflare entrega un **token público** (no es secreto). Agregarlo al front **por configuración**, como `apiUrl`:
   - Una variable del repositorio (p. ej. `ANALITICA_TOKEN`) que el workflow de Pages escriba en `config.json`.
   - Que el front cargue el script solo si existe el token, y solo en producción.
   - Así staging y desarrollo nunca cuentan visitas falsas.
3. Revisar que cuente la **navegación interna de la SPA** (cambios de ruta sin recargar la página). Si no, activar su opción para SPA.
4. **CSP** del front en Docker/nginx (`frontend/docker/seguridad.inc.template`):
   - `script-src`: agregar `https://static.cloudflareinsights.com`;
   - `connect-src`: agregar `https://cloudflareinsights.com` (variable `CSP_CONNECT_EXTRA`).
   - GitHub Pages hoy no envía CSP, pero el dominio propio con nginx sí lo hará.
5. **Política de tratamiento de datos** (`/privacidad`): agregar un párrafo que diga qué se mide (visitas anónimas, sin cookies), con qué proveedor y para qué. Subir la versión de la política (`Legal:VersionPoliticaDatos`) si el abogado lo indica.
6. Revisarla cada mes junto con las consultas de la base. No hace falta mirarla todos los días.

## Qué NO hacer
- No agregar píxeles de Facebook/Meta, TikTok ni de anuncios: rastrean a las personas entre sitios.
- No enviar eventos con el id del usuario, su correo o el texto de una publicación o de un mensaje.
- No activar ninguna herramienta en staging ni en desarrollo.

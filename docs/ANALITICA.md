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
| Errores y lentitud de la API | Salud técnica | Logs de Render (Application Insights se quitó el 2026-10-18 al salir de Azure) |

## Consultas para la base (PostgreSQL en Supabase)
Se corren en Supabase → proyecto → **SQL Editor**.
- Son solo de lectura y no muestran datos personales: solo conteos.
- Los nombres van entre comillas dobles porque EF Core crea las tablas y columnas con mayúsculas.

```sql
-- Cuentas nuevas por semana (últimas 8) y % que confirmó el correo
SELECT date_trunc('week', "FechaRegistro")::date AS "Semana", COUNT(*) AS "Nuevas",
       ROUND(100.0 * COUNT(*) FILTER (WHERE "CorreoVerificado") / COUNT(*), 1) AS "PctVerificado"
FROM "Usuarios"
WHERE "FechaRegistro" >= now() - interval '8 weeks' AND NOT "EstaEliminado"
GROUP BY 1 ORDER BY 1;

-- Publicaciones nuevas por semana y modo
SELECT date_trunc('week', "FechaPublicacion")::date AS "Semana", "Modo", COUNT(*) AS "Publicaciones"
FROM "Publicaciones"
WHERE "FechaPublicacion" >= now() - interval '8 weeks'
GROUP BY 1, 2 ORDER BY 1, 2;

-- Embudo de los últimos 30 días
SELECT COUNT(*) AS "Solicitudes",
       COUNT(*) FILTER (WHERE "FechaAceptacionUtc" IS NOT NULL) AS "Aceptadas",
       COUNT(*) FILTER (WHERE "Estado" = 'Completada') AS "Completadas",
       COUNT(*) FILTER (WHERE "Estado" = 'NoConcretada') AS "NoConcretadas"
FROM "Solicitudes"
WHERE "FechaSolicitud" >= now() - interval '30 days';

-- Horas hasta que el dueño acepta (mediana y promedio, últimos 30 días)
SELECT ROUND((AVG(EXTRACT(EPOCH FROM "FechaAceptacionUtc" - "FechaSolicitud")) / 3600)::numeric, 1) AS "HorasPromedio",
       ROUND((PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM "FechaAceptacionUtc" - "FechaSolicitud")) / 3600)::numeric, 1) AS "HorasMediana"
FROM "Solicitudes"
WHERE "FechaAceptacionUtc" IS NOT NULL AND "FechaSolicitud" >= now() - interval '30 days';

-- Intercambios completados por modo (últimos 30 días)
SELECT "Modo", COUNT(*) AS "Completados" FROM "Transacciones"
WHERE "FechaUtc" >= now() - interval '30 days' GROUP BY "Modo";

-- Personas activas en 30 días (publicaron o pidieron algo)
SELECT COUNT(DISTINCT "Id") AS "Activas" FROM (
  SELECT "PropietarioId" AS "Id" FROM "Publicaciones" WHERE "FechaPublicacion" >= now() - interval '30 days'
  UNION SELECT "SolicitanteId" FROM "Solicitudes" WHERE "FechaSolicitud" >= now() - interval '30 days'
) a;
```
Si un día se quiere verlas en la app, el paso natural es una pantalla de **Administración → Indicadores** (solo SuperUsuario y con 2FA), que haga estos mismos conteos.

## Herramienta para las visitas: comparación
| Herramienta | Costo | Cookies / consentimiento | Qué da | Veredicto |
|---|---|---|---|---|
| **Cloudflare Web Analytics** | Gratis | Según Cloudflare, sin cookies ni localStorage y sin huella por IP o navegador ([fuente](https://www.cloudflare.com/web-analytics/)); hay revisiones independientes que discuten el detalle ([ejemplo](https://www.ctrl.blog/entry/review-cloudflare-analytics/)) | Visitas, páginas, procedencia, países, dispositivos, Core Web Vitals | **Recomendada para empezar.** No exige cambiar el DNS ni usar el proxy de Cloudflare. |
| Plausible | De pago (revisar el precio vigente) | Sin cookies | Lo mismo, con mejor detalle y eventos | Buena opción si se quiere pagar |
| Application Insights (navegador) | Descartada: se salió de Azure el 2026-10-18 | Usa cookies (`ai_user`, `ai_session`) | Rendimiento y errores del front | No aplica |
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

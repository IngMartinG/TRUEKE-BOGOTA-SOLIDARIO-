# Ambiente de staging (pruebas antes de producción)

> **Estado:** solo documentado. Nada de esto está creado todavía. Cuando lo actives, sigue esta guía en orden.

## Para qué sirve
Staging es una **copia separada de producción**: mismo código, mismas imágenes Docker y misma configuración de seguridad, pero con **su propia base, sus propias fotos y sus propios secretos**. Sirve para:
- probar un cambio grande (una migración, pagos, correo, notificaciones push) **con servicios reales** antes de que lo vean las personas;
- mostrar algo a alguien sin tocar los datos reales;
- correr el escaneo de seguridad **OWASP ZAP** (`.github/workflows/zap-staging.yml`), que está pensado para correr contra staging y **nunca** contra producción.

**No reemplaza** a las pruebas automáticas: las E2E (`npm run e2e`) ya prueban cada PR con las imágenes de producción. Staging agrega lo que las E2E no pueden: Azure real (SQL, Blob, Communication Services), Wompi sandbox real, Google y el servicio de push real.

**Cuándo vale la pena:**
- antes de pasar Wompi a producción (dinero real);
- antes de migraciones que tocan datos existentes;
- cuando haya más de una persona desarrollando;
- cuando haya usuarios reales a los que no se les puede romper nada.

Mientras el equipo sea una persona y el tráfico sea de amistades, **las E2E en cada PR son suficientes**.

## Cómo se ve
| Pieza | Producción (hoy) | Staging |
|---|---|---|
| Front | GitHub Pages (`ingmarting.github.io/TRUEKE-BOGOTA-SOLIDARIO-/`) | **Render Static Site** (`trueke-staging.onrender.com`). Pages solo permite un sitio por repositorio. |
| API | Render Free, servicio `trueke-api` | Render Free, servicio `trueke-api-staging` en **otro workspace** (ver costos) |
| Base | Azure SQL `TruekeDb` (oferta gratis) | Azure SQL **`TruekeDbStaging`** en el mismo servidor (otra base gratis) |
| Fotos | La cuenta de Storage de producción | **Otra cuenta de Storage** (p. ej. `truekefotosstaging…`), para que nunca se mezclen ni se borren fotos reales |
| Correo | Azure Communication Services | El mismo recurso (10 correos/hora compartidos) o uno propio |
| Pagos | Wompi **sandbox** (por ahora) | Wompi **sandbox, siempre**. Nunca llaves de producción. |
| Monitoreo | App Insights `trueke-monitoreo` | Opcional: otro recurso o el mismo con `Monitoreo` separado por rol |
| Rama | `main` | `staging` (o despliegue manual de un PR) |

## Costos
**Todo cabe en lo gratis**, con estas condiciones:
- **Render:** las 750 horas gratis al mes son **por workspace** y se reparten entre todos sus servicios gratis. Al agotarse, Render **suspende todos** los servicios gratis del workspace hasta el mes siguiente ([fuente](https://render.com/docs/free)). Por eso staging debe ir en **otro workspace** (otra cuenta o equipo de Render): así nunca le gasta las horas a producción. Los *Static Sites* son gratis y no usan horas.
- **Azure SQL:** la oferta gratis permite **hasta 10 bases por suscripción**, cada una con sus 100.000 vCore-segundos al mes ([fuente](https://learn.microsoft.com/azure/azure-sql/database/free-offer)). La base de staging tiene su propia cuota y no le resta a producción.
- **Storage:** centavos de dólar al mes con pocas fotos de prueba.
- **Si producción pasa a plan pago**, staging puede quedarse en gratis: solo se usa de vez en cuando y se duerme sola.

## Reglas
1. **Nunca copiar datos de producción a staging** (Ley 1581: los datos se usan solo para lo que la persona autorizó). Staging se llena con cuentas y publicaciones de prueba.
2. **Secretos propios:** `Jwt__Key`, `Seguridad__ClaveCifrado`, llaves VAPID y cadenas de conexión **distintos** de producción. Un token de staging nunca debe servir en producción.
3. **Wompi solo sandbox.**
4. Que nadie la confunda con la real: el aviso "Sitio de demostración" ya aparece solo con pagos de prueba. Además, no publicar el enlace de staging.
5. Los buscadores no deben indexarla: el front de staging debe enviar `X-Robots-Tag: noindex` (en Render Static Site → *Headers*).

## Pasos para crearlo (cuando se decida)
1. **Base:** Azure → el servidor SQL de producción (grupo `trueke`) → *Crear base de datos* `TruekeDbStaging` → **Aplicar oferta gratis** → al agotar la cuota: **auto-pausa** (nunca cobrar).
2. **Storage:** crear `truekefotosstaging<algo>` (mismo grupo, Central US, LRS). Activar acceso anónimo de blobs y una regla CORS para el dominio del front de staging (igual que `docs/DESPLIEGUE.md` §3).
3. **API en Render** (en el workspace nuevo): *New → Web Service* desde este repositorio, **Docker**, `backend/Dockerfile`, rama `staging`.
   - Variables: las mismas de `render.yaml`, con los valores de staging.
   - `ASPNETCORE_ENVIRONMENT=Production`, para probar exactamente lo mismo que producción, con todas sus validaciones de arranque.
   - `Urls__Frontend` y `Cors__Origenes__0` apuntando al front de staging.
   - `Urls__Api` con la URL de la API de staging.
4. **Front en Render:** *New → Static Site* desde este repositorio.
   - Raíz: `frontend`.
   - Build:
     ```bash
     npm ci && npx ng build && printf '{"apiUrl":"https://trueke-api-staging.onrender.com"}' > dist/trueke-web/browser/config.json && cp dist/trueke-web/browser/index.html dist/trueke-web/browser/404.html
     ```
   - Publicar: `dist/trueke-web/browser`.
   - *Redirects/Rewrites:* `/*` → `/index.html` (rewrite), para que funcionen las rutas de la SPA.
5. **Google y reCAPTCHA:** agregar el dominio de staging como origen autorizado (OAuth) y como dominio permitido (reCAPTCHA). Sin esto, el login con Google y el captcha fallan solo en staging.
6. **Rama `staging`:** para probar un PR antes de fusionarlo, `git push origin <rama>:staging --force`. Render despliega solo, y la base se migra sola al arrancar (`Database__Inicializacion=Migrate`).
7. **Seguridad:** *Actions → OWASP ZAP → Run workflow* con la URL del front de staging. Revisar el issue que abre con los hallazgos.

## Cuándo retirarlo
Si no se usa en un mes: borrar los servicios de Render del workspace de staging y pausar o borrar `TruekeDbStaging` y la cuenta de Storage de staging. No afecta a producción.

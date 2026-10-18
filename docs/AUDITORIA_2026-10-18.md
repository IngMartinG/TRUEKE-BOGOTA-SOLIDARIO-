# Auditoría integral (2026-10-18)

Se revisó la plataforma después de las fases de mayoría de edad, validación de campos, legal y asistente de ayuda (PR #150 a #154). La fase de aportes sigue en pausa y no entró.

Frentes revisados:
- **Seguridad**, mirando como un atacante: OWASP Top 10, IDOR, bypass del claim de edad, abuso de la PQRS pública, límites de peticiones, dependencias y secretos.
- **Accesibilidad**: WCAG 2.2 AA.
- **Interfaz y formularios**: Web Interface Guidelines.
- **Código**.
- **Legal**.
- **Producto**.

Además se investigó un error intermitente del E2E en celular.

Cada corrección va en su propio PR. Lo que necesita una decisión del dueño, de un abogado o de un contador queda como **pendiente**, con su propuesta.

## Resumen por severidad

| # | Severidad | Hallazgo | Estado |
|---|---|---|---|
| 1 | **Alta** (legal) | No existe el registro de oferentes que exige la Ley 1480 a un portal de contacto (art. 53) | **Corregido** en #161 (texto legal: revisión del abogado) |
| 2 | Media | Un fragmento JS que no carga hacía fallar el ingreso ("Ocurrió un error inesperado") | **Corregido** en #155 |
| 3 | Media | La PQRS pública reenviaba a cualquier correo el texto escrito por quien la radicaba | **Corregido** en #156 |
| 4 | Media | Al enviar un formulario con errores, el foco no iba al primer campo inválido (WCAG 3.3.1 y 2.4.3) | **Corregido** en #157 |
| 5 | Media | Una cuenta antigua que declara una fecha de menor de edad puede reintentar con otra fecha | **Corregido** en #160 |
| 6 | Media-baja | La PQRS pública comparte el límite por IP con el ingreso y avisa a la moderación sin tope global | **Corregido** en #159 |
| 7 | Baja | Cambiar el rol de una cuenta devolvía su fecha de nacimiento | **Corregido** en #156 |
| 8 | Baja | Facturas de administración sin `aria-invalid`; correctores ortográficos en correos y códigos; el indicador "buscando" del asistente no se anunciaba | **Corregido** en #157 |
| 9 | Baja | La documentación seguía mencionando Application Insights y el despliegue en Azure | **Corregido** en este PR |
| 10 | Informativa | Un menor rechazado en el registro puede intentarlo de nuevo con otra fecha (autodeclaración) | Riesgo residual aceptado |
| 11 | Informativa | El radicado usa 6 caracteres de un GUID con índice único | Sin acción |

## Detalle

### 1. Registro de oferentes, Ley 1480, art. 53 (alta, legal)
Los términos se apoyan en la figura de **portal de contacto** (Ley 1480, art. 53) para no responder como vendedor. Esa misma norma exige al portal un registro de **todos los oferentes**, con estos datos como mínimo:
- nombre;
- documento de identificación;
- dirección física de notificaciones;
- teléfono.

Ese registro debe estar disponible para quien compró y quiere reclamar, y para la autoridad.

Hoy el registro de una cuenta pide solo nombre, correo y ubicación. El documento existe únicamente en la verificación opcional y en los datos de facturación, que se usan para comprar beneficios, no para vender.

**Propuesta** (no se implementó porque implica pedir más datos personales):
- Exigir esos cuatro datos solo a quien publique en modo **Compra**, una sola vez y antes de la primera venta.
- Guardarlos cifrados, como ya se hace con los secretos de 2FA.
- No mostrarlos a nadie. Solo se entregan a quien completó una compra con esa persona y radica una PQRS, o a la autoridad.
- Anunciarlo en la política de datos.

**Corrección (decisión del dueño: solo para vender en modo Compra):** se pide una sola vez, antes de la primera publicación de venta (403 `datos_oferente_requeridos`). Documento, dirección y teléfono van cifrados con la clave de los secretos de 2FA. Nunca se devuelven a terceros: el titular los ve en su cuenta y en la exportación; la moderación con 2FA los consulta solo desde la PQRS de alguien que completó una compra con esa persona, y cada consulta queda en la auditoría. La política, los términos y el asistente lo explican, y la versión legal sube a `2026-10-19`, lo que pide reaceptarlos. Queda para el abogado: el texto final y la conservación del registro después de eliminar la cuenta.

El abogado debe confirmar si aplica también al **trueke** y a las **donaciones**, donde no hay precio.

### 2. "Ocurrió un error inesperado" al ingresar (media) — #155
La API nunca devuelve ese texto: el front lo muestra cuando el error **no es HTTP**.

Se reprodujo de forma determinista cortando la descarga del fragmento JS de `/explorar` justo después de un login exitoso. El router rechazaba la navegación, `ingresar.ts` lo trataba como si el login hubiera fallado, y la persona quedaba en `/ingresar` **con la sesión ya abierta**.

En aislamiento, 40 ingresos seguidos en celular pasaron todos. En las corridas completas, la traza mostró la causa: `net::ERR_BLOB_OUT_OF_MEMORY`. Al instalarse, el service worker descarga todos los fragmentos para su caché y, con Docker en el mismo equipo, Chromium se queda sin memoria para blobs, así que fallan las descargas de la prueba en curso.

En producción pasa también:
- con red inestable en el celular;
- **tras cada despliegue**, porque los fragmentos de la versión anterior dejan de existir en GitHub Pages.

Afecta igual al registro, a completar el perfil y a cualquier acción que navega después de guardar.

**Corrección:**
- Un manejador global de errores de navegación. Ante un fragmento que no cargó, carga la página destino completa una vez; la sesión vuelve con la cookie de refresco. Si falla de nuevo, avisa que se revise la conexión, sin recargar en bucle.
- El borrador que el asistente pasa a la PQRS sobrevive a esa recarga: queda en el `sessionStorage` de la pestaña hasta que se usa, como máximo 10 minutos.
- El E2E corre sin service worker, salvo la prueba de PWA.
- Nueva prueba E2E `resiliencia.spec.ts` en PC y celular.

Antes de este cambio, cada corrida completa del E2E tenía un fallo. Después, dos corridas seguidas dieron 38 de 38.

### 3. PQRS pública como relevo de correo (media) — #156
El canal es anónimo, así que cualquiera puede escribir el correo de otra persona. El acuse repetía el nombre y el asunto que escribió quien radicaba. Con eso se podían enviar textos arbitrarios desde el dominio de la plataforma, lo que también daña la reputación del remitente en Brevo.

**Corrección:** en el canal público, el acuse solo lleva el radicado, el tipo y la fecha límite, más "si no enviaste esta solicitud, ignora este mensaje".

### 4. Foco en el primer error (media) — #157
Solo la pantalla de publicar llevaba el foco al campo con error.

**Corrección:** la directiva `EnfocarError`, que se aplica sola a cada `form[formGroup]`, en los nueve formularios con validación. Se verificó en el navegador con la imagen de producción.

### 5. Cuenta antigua que declara ser menor (media, pendiente)
`PUT /auth/yo/fecha-nacimiento` rechaza una fecha de menor y no la guarda, pero la persona puede volver a intentarlo con otra fecha. La política dice que si se detecta un menor, la cuenta se suprime.

**Propuesta:** al primer intento con una fecha de menor en una cuenta **existente**, marcar la cuenta para revisión y no aceptar más intentos. La persona conserva la exportación y la eliminación de sus datos. Necesita una migración y la decisión del dueño sobre qué pasa con sus publicaciones y su saldo.

**Corrección (decisión del dueño: marcar y revisar):** el primer intento marca la cuenta (`MenorDetectadoUtc`), no guarda la fecha, oculta sus publicaciones activas y avisa a la moderación. Los reintentos responden 403 `menor_de_edad_detectado`. Conserva la sesión, la exportación y la eliminación de sus datos y la PQRS. El saldo queda congelado. La moderación descarta la marca si comprueba un error de digitación, con el motivo en la auditoría, o suspende la cuenta.

### 6. Límites de la PQRS pública (media-baja, pendiente)
`POST /pqr/publica` usa la misma política de límite por IP que el ingreso (10 por minuto). Desde una IP compartida, como una universidad o una red móvil con NAT, el abuso de un tercero puede dejar sin ingreso a los demás. Además, cada radicación avisa a todos los moderadores.

La exposición es baja porque hay reCAPTCHA obligatorio en producción y un tope de 5 por día por correo.

**Propuesta:** una política de límite propia para la PQRS pública, por ejemplo 5 por hora por IP, también en el limitador de Redis, y agrupar los avisos a la moderación en un resumen.

**Corrección:** política `pqr-publica` solo por IP, con 3 por minuto y 10 por hora (configurables), en memoria y en Redis. El ingreso desde la misma IP no se ve afectado. Las PQRS públicas entran a un resumen cada 10 minutos: un solo aviso por moderador con la cantidad, sin el texto de quien radicó. El contador vive en la memoria de cada instancia; si una instancia se reinicia antes de enviarlo, las PQRS siguen en la cola de moderación.

### 7 y 8. Hallazgos menores — #156 y #157
- La respuesta del cambio de rol traía la fecha de nacimiento de la otra cuenta. Por minimización (Ley 1581), ahora va en `null`.
- Facturas de administración no tenía `CampoAccesible`.
- Se puso `spellcheck="false"` en correos, códigos 2FA y radicado.
- El indicador "buscando" del asistente ahora tiene `role="status"`.

### 9. Documentación desactualizada — este PR
`backend/README.md` y `docs/ANALITICA.md` seguían diciendo que Application Insights estaba activo y describían el despliegue en Azure como vigente. Quedaron como referencia y apuntan a `docs/DESPLIEGUE.md`.

### 10 y 11. Informativos
- La mayoría de edad es **autodeclarada**: quien miente sobre su fecha no se detecta. La mitigación real es la verificación documental, que ya existe como opcional. Los términos trasladan la responsabilidad a quien declara.
- El radicado tiene la forma `PQR-AAAAMMDD-XXXXXX`, con 16,7 millones de combinaciones por día y un índice único. Una colisión respondería 500, pero es improbable con el volumen actual. Para consultar se exige además el correo.

## Lo que se revisó y está bien
- **Claims de edad y de términos.** Se firman en el servidor y se recalculan desde la base en cada emisión.
  - La política por defecto, la de respaldo y las de moderación los exigen.
  - El hub de SignalR también: un token sin el claim no se conecta.
  - Los endpoints exentos son los previstos: sesión, 2FA, avisos, exportar y eliminar datos, y PQRS.
- **IDOR.**
  - `pqr/consultar` exige radicado y correo, y responde el mismo 404 si no existe o si no coincide.
  - `pqr/mias` y los datos personales salen del JWT.
  - El panel de moderación exige rol y sesión con 2FA.
- **Datos personales.** La fecha de nacimiento solo aparece en la sesión propia y en la exportación, nunca en el perfil público.
- **Correo.** La plantilla codifica en HTML todo el contenido variable.
- **Captcha.** Es obligatorio en producción: la API no arranca sin las claves.
- **Dependencias.**
  - `dotnet list package --vulnerable --include-transitive`: ninguna.
  - `npm audit --omit=dev`: 0 vulnerabilidades.
- **Secretos.** No hay ninguno en el repositorio. Las únicas llaves JWT versionadas son las de demostración de Supabase local (`supabase-demo`), en los archivos de Docker para pruebas.

## Para el abogado y el contador
**Ley 1581 de 2012 y Decreto 1377 de 2013 (datos personales)**
- Contratos o cláusulas de transmisión con los encargados: Supabase, Render, Brevo, Google y Wompi (Decreto 1377, art. 25). Basta con aceptar el DPA de cada proveedor y guardar la evidencia.
- Confirmar el país de alojamiento de cada uno para la sección de transferencias internacionales.
- RNBD: hoy no aplica a una persona natural. Revisarlo cuando exista la SAS.

**Ley 1480 de 2011 (consumidor)**
- Registro de oferentes del art. 53 (hallazgo 1).
- Si el retracto del art. 47 aplica a las recargas de Eco-Puntos y a los planes una vez usados.

**Ley 527 de 1999 (comercio electrónico)**
- Hoy se guardan la versión y la fecha de aceptación de la política y de los términos.
- Confirmar si basta como prueba o si conviene guardar también la IP y el navegador.

**Ley 1755 de 2015 (peticiones)**
- Plazos: 15 días hábiles para peticiones, quejas y reclamos, y 10 para consultas de habeas data (Ley 1581).
- Los festivos colombianos ya se descuentan.
- Confirmar que la PQRS pública cubre el canal exigido.

**Ley 2300 de 2023 (contacto con consumidores)**
- Si los correos de "novedades" o los avisos push con fines comerciales deben respetar horarios y frecuencia máxima.
- Hoy son opcionales y con consentimiento separado.

**Ley 1618 de 2013 y Resolución MinTIC 1519 de 2020 (accesibilidad)**
- Obligan a entidades públicas. Aquí se toman como referencia voluntaria; confirmar el texto de la declaración.

**Estatuto Tributario**
- Régimen de IVA del responsable (no responsable si los ingresos son menores de 3.500 UVT).
- Tratamiento de las recargas de Eco-Puntos: ingreso anticipado o venta.
- Si se necesita facturación electrónica con resolución de la DIAN.
- Tratamiento de los aportes voluntarios cuando se retome la fase 4 (art. 125: no se puede certificar como donación).

## Producto: funciones que faltan
| Función | Valor | Esfuerzo | Nota |
|---|---|---|---|
| Registro de oferentes en modo Compra | Alto (legal) | Medio | Hallazgo 1 |
| `robots.txt` y `sitemap.xml` del catálogo público | Medio (SEO) | Bajo | Hoy no existen; la vista previa al compartir ya está |
| Monitor externo de `/health/ready` | Alto | Bajo | Evita que Supabase Free pause el proyecto y avisa caídas |
| Panel interno de métricas (las consultas de `docs/ANALITICA.md`) | Medio | Bajo | Solo conteos, sin datos personales |
| Analítica de visitas sin cookies (Cloudflare Web Analytics) | Medio | Bajo | Ya documentada; falta activarla |
| Seguir a una persona o tienda (aviso al publicar) | Medio | Medio | Complementa las búsquedas guardadas |
| Fotos en el chat | Medio | Alto | Requiere moderación y el mismo proceso de imágenes (EXIF/GPS) |
| Verificación de identidad automatizada | Alto | Alto | Mitiga el hallazgo 10; tiene costo por verificación |
| Asistente con IA (`ProveedorIA`) | Medio | Medio | El punto de conexión ya existe; la llave va solo en el backend, con límite de peticiones |

## Verificación de esta auditoría
- **Backend:** `dotnet build -warnaserror` y `dotnet test`, 507 de 507 en verde en #156.
- **Front:** `npm run verificar` (lint, pruebas y build de producción) en #155 y #157.
- **E2E:** dos corridas completas seguidas sobre #155, 38 de 38 en verde (PC y celular).
- **Navegador:** se comprobó el foco en el primer error de la PQRS pública (#157) con la imagen de producción.
- **Orden de fusión:** los cuatro PR son independientes y no tocan los mismos archivos.

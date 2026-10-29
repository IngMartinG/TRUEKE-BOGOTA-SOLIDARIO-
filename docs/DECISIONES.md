# Decisiones

Decisiones de producto vigentes y por qué. Las técnicas de detalle están en `docs/ESTADO_Y_PENDIENTES.md`.

## D-001 — Dos capas
Gratis para personas, juntas de acción comunal, fundaciones, colegios y bibliotecas; pago para negocios y empresas. Ninguna
función esencial para una persona puede volverse de pago.

## D-002 — Sin comisión al trueque
El ingreso viene de la visibilidad (destacar, planes) y de los servicios a empresas, nunca de cobrar por intercambiar.

## D-003 — Doble confirmación obligatoria
Los Eco-Puntos y el impacto solo cuentan cuando las dos partes confirman la entrega. Sin esto los puntos se inflan y las cifras
dejan de ser creíbles.

## D-004 — Kilos primero, CO₂ estimado
Los kilos son la métrica principal y el CO₂ se presenta como estimación. Toda cifra pública cita la versión de la
metodología (`docs/METODOLOGIA-IMPACTO.md`).

## D-005 — Factores versionados
Cambiar un factor de impacto sube la versión de la metodología y no reescribe lo ya calculado.

## D-006 — El primer informe para una empresa no se regala
Se cobra, aunque sea a precio de costo: regalarlo crea expectativa de gratuidad.

## D-007 — Explorar sin registro
El catálogo, las categorías, las fundaciones y el impacto se ven sin cuenta. El registro se pide al publicar o contactar.

## D-008 — Español en el código
Entidades, servicios, rutas y componentes se nombran en español, como lo ya construido.

## D-009 — Lanzamiento sin cobro (2026-10-20)
Los cobros en dinero están apagados (`Pagos:VentasHabilitadas=false`). Toda función de pago nueva queda detrás de ese
interruptor.

## D-010 — Contacto solo por el chat
La API nunca comparte correos ni teléfonos entre usuarios; todo se coordina por el chat interno.

## D-011 — Foto de perfil real (2026-10-29)
La foto de perfil debe mostrar el rostro de la persona. Un detector de rostros que corre en el propio servidor (sin enviar
fotos a terceros) aprueba las claras, rechaza las que no tienen rostro y manda las dudosas a revisión humana. Quien ya tiene
una foto sin rostro recibe un aviso y 7 días para cambiarla.

## D-012 — Moderación por riesgo (2026-10-29)
Las publicaciones de bajo riesgo salen al instante; las de riesgo (cuenta nueva, contacto en el texto, artículos prohibidos,
denuncias previas…) esperan en una cola ordenada por prioridad. Así un equipo pequeño puede moderar mucho volumen.
Son reglas con pesos y motivos visibles para quien revisa, no un clasificador opaco; sin datos que salgan a terceros.

## D-013 — Negocio, Fundación y Empresa no son roles de seguridad
Los roles siguen siendo Cliente, Administrador y SuperUsuario. Ser negocio, fundación o empresa es una capacidad de la cuenta
(plan, verificación u organización), no un permiso.

## Pendientes de decidir
- Precio de "Destacar" (hoy 6.000 COP por 7 días).
- Punto físico o 100 % digital.
- Dominio propio.

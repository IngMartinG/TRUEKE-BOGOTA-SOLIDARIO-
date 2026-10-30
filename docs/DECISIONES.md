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

## D-018 — Retención sin presión: preguntar antes de pausar y avisos opcionales (2026-10-30)
- Una publicación sin actividad en 30 días no se borra ni se esconde de golpe: primero se le pregunta al dueño y solo si no
  responde en 7 días se pausa. Pausada no es cancelada: se reactiva con un clic y sigue contando para el límite del plan, para
  que pausar y reactivar no sirva para saltarse ese límite.
- Cuenta como actividad todo lo que muestra que el dueño o la comunidad siguen ahí: publicarla, editarla, impulsarla, confirmarla
  o que alguien la solicite.
- El resumen semanal es opcional y viene apagado (como las novedades, por la Ley 1581): solo se envía con algo que contar.
- Los logros siguen siendo solo un reconocimiento (sin puntos ni descuentos) y salen de los registros de impacto, así que solo
  cuentan entregas con doble confirmación (D-014).
- Las metas comunitarias las fija el SuperUsuario por localidad y respetan el mínimo de 3 intercambios: con menos, la meta se
  muestra pero su avance no, para que no se pueda adivinar quién participó.

## D-017 — Empresas por invitación e informes como foto inmutable (2026-10-30)
- La empresa es una organización a la que pertenecen cuentas normales (D-013), verificada por el SuperUsuario con el
  certificado de la Cámara de Comercio. No tiene cobro en la plataforma: el servicio se acuerda por cotización.
- Los miembros entran con un enlace de un solo uso que el Admin comparte por su cuenta. No se busca a nadie por correo, así la
  API no revela qué cuentas existen ni comparte correos entre usuarios. Una cuenta pertenece a una sola empresa.
- El impacto de una empresa sale de las publicaciones que sus miembros ligan a una campaña activa; el registro inmutable guarda
  la campaña. Solo cuentan las entregas con doble confirmación (D-014).
- Un informe lo emite un Administrador de la plataforma, no la empresa, y guarda una foto de los datos con su huella SHA-256.
  El PDF y el CSV se generan siempre desde esa foto: dan las mismas cifras y no cambian si después cambian los factores.
- La trazabilidad es anónima: un código por intercambio, sin personas, títulos ni coordenadas. La foto de la entrega es
  opcional, privada para las dos partes y la moderación, y el informe solo dice si existe.
- Los PDF se hacen con QuestPDF (licencia Community, gratis por debajo de USD 1 M de ingresos brutos anuales); al acercarse a
  ese umbral hay que comprar la licencia o cambiar de librería.

## D-016 — Plan Negocio con vitrina pública y sello revisado por personas (2026-10-29)
El plan Empresa se llama Negocio (solo en lo visible; los pagos no cambian). Su directorio y su vitrina son públicos, con solo
datos del negocio. El sello "Negocio verificado" lo da un moderador tras revisar el NIT en el RUES; tener el NIT escrito no basta.

## D-015 — Régimen Tributario Especial sin promesas fiscales (2026-10-29)
El equipo marca a mano a las fundaciones calificadas en el Régimen Tributario Especial, con la evidencia que revisó. En público
solo se dice que pertenecen al régimen: que una donación sea deducible depende de cada caso y del certificado que emita la
fundación.

## D-014 — Impacto por objeto y solo con doble confirmación (2026-10-29)
El impacto se registra por objeto entregado (con su peso y material, o los típicos de su categoría) y de forma inmutable,
con el factor y la versión con que se calculó. Un intercambio que se cierra solo porque una parte no confirmó a tiempo no
suma impacto, Eco-Puntos ni reputación: quien confirmó tiene el incentivo de pedirle a la otra parte que confirme.

## Pendientes de decidir
- Precio de "Destacar": 5.000 COP por 7 días desde el 2026-10-30 (antes 6.000). Revisar con datos reales cuando se encienda el cobro.
- Punto físico o 100 % digital.
- Dominio propio.

import type { PoliticaEcoPuntosDto, TipoPqrDto } from '../../api/tipos';

/**
 * Base de conocimiento única de la ayuda: la usan el Centro de ayuda, las preguntas de Planes y de Cómo funciona, y
 * el asistente de soporte. Las cifras de Eco-Puntos y precios salen en vivo de `/eco-puntos/politica`: si la política
 * todavía no cargó, la respuesta se redacta sin números en lugar de repetir valores que pueden cambiar.
 */
export type Politica = PoliticaEcoPuntosDto | null | undefined;

export type IdTema = 'empezar' | 'intercambios' | 'chat' | 'puntos' | 'planes' | 'seguridad';
export type Pagina = 'ayuda' | 'planes' | 'como-funciona';

export interface TemaAyuda {
  id: IdTema;
  titulo: string;
  icono: string;
}

export interface AccionAyuda {
  texto: string;
  ruta: string;
  fragmento?: string;
  /** Si exige sesión, a quien no la tiene se le lleva primero a ingresar y luego vuelve aquí. */
  conSesion?: boolean;
}

export interface EntradaConocimiento {
  id: string;
  tema: IdTema;
  pregunta: string;
  /** Otras formas de preguntar lo mismo (pesan casi como la pregunta en la búsqueda). */
  variantes: readonly string[];
  palabrasClave: readonly string[];
  respuesta: string | ((p: Politica) => string);
  accion?: AccionAyuda;
  /** Tipo de PQRS que se sugiere si la respuesta no le sirvió a la persona. */
  tipoPqr?: TipoPqrDto;
  /** Páginas donde aparece como pregunta frecuente (todas aparecen en el Centro de ayuda). */
  paginas?: readonly Exclude<Pagina, 'ayuda'>[];
}

export const TEMAS: readonly TemaAyuda[] = [
  { id: 'empezar', titulo: 'Empezar', icono: 'brote' },
  { id: 'intercambios', titulo: 'Intercambios y entregas', icono: 'apreton' },
  { id: 'chat', titulo: 'Chat', icono: 'mensaje' },
  { id: 'puntos', titulo: 'Eco-Puntos, pagos y facturas', icono: 'moneda' },
  { id: 'planes', titulo: 'Planes Premium y Empresa', icono: 'corona' },
  { id: 'seguridad', titulo: 'Reportes, datos y seguridad de tu cuenta', icono: 'bandera' },
];

const cop = (v: number) => `$${v.toLocaleString('es-CO')}`;
/** "a, b y c" */
const lista = (xs: string[]) => (xs.length > 1 ? `${xs.slice(0, -1).join(', ')} y ${xs.at(-1)}` : (xs[0] ?? ''));

export const CONOCIMIENTO: readonly EntradaConocimiento[] = [
  // ── Empezar ─────────────────────────────────────────────────────────────────────────────────────────────────────
  {
    id: 'que-es',
    tema: 'empezar',
    pregunta: '¿Qué puedo hacer en Trueke?',
    variantes: ['para qué sirve trueke', 'cómo funciona la plataforma', 'qué es trueke'],
    palabrasClave: ['trueke', 'intercambiar', 'vender', 'donar', 'plataforma'],
    respuesta: 'Intercambiar objetos sin dinero (trueke), venderlos o comprarlos de segunda mano, o donarlos a quien los necesita. Cada intercambio completado te da Eco-Puntos y reputación.',
    accion: { texto: 'Ver cómo funciona', ruta: '/como-funciona' },
  },
  {
    id: 'costo',
    tema: 'empezar',
    pregunta: '¿Cuesta algo usar Trueke?',
    variantes: ['es gratis', 'cuánto cuesta', 'hay que pagar para publicar', 'intercambiar o donar cuesta algo'],
    palabrasClave: ['gratis', 'costo', 'cobran', 'precio', 'pagar'],
    respuesta: 'No. Publicar, intercambiar, vender, comprar y donar es gratis. Solo pagas si quieres un beneficio opcional: destacar una publicación, verificar tu cuenta o un plan Premium o Empresa (mensual o anual, con 2 meses gratis).',
    accion: { texto: 'Ver los planes', ruta: '/planes' },
    paginas: ['como-funciona'],
  },
  {
    id: 'confirmar-correo',
    tema: 'empezar',
    pregunta: '¿Por qué debo confirmar mi correo?',
    variantes: ['no me llegó el correo de confirmación', 'cómo verifico mi correo', 'no puedo publicar sin correo'],
    palabrasClave: ['correo', 'confirmar', 'verificar', 'activar', 'enlace'],
    respuesta: 'Para publicar, solicitar, comentar, chatear y pagar necesitamos saber que hay una persona real detrás de la cuenta. Así evitamos cuentas falsas y estafas. Si no te llegó el correo, revisa la carpeta de spam o pide otro enlace.',
    accion: { texto: 'Reenviar el correo', ruta: '/verifica-tu-correo', conSesion: true },
  },
  {
    id: 'foto-perfil',
    tema: 'empezar',
    pregunta: '¿Por qué me piden una foto de perfil?',
    variantes: ['no puedo publicar me pide foto', 'cómo cambio mi foto', 'cómo subo mi foto', 'subir foto de perfil'],
    palabrasClave: ['foto', 'perfil', 'cara', 'imagen', 'avatar'],
    respuesta: 'Para publicar, solicitar, chatear y pagar necesitas una foto donde se vea tu cara: así la otra persona sabe con quién se va a encontrar. Si entraste con Google, la foto se toma de tu cuenta de Google.',
    accion: { texto: 'Subir foto', ruta: '/cuenta/perfil', fragmento: 'foto', conSesion: true },
  },
  {
    id: 'publicar',
    tema: 'empezar',
    pregunta: '¿Cómo publico algo?',
    variantes: ['cómo vendo algo', 'cómo subo un objeto', 'quiero donar algo', 'crear publicación'],
    palabrasClave: ['publicar', 'publicacion', 'subir', 'anuncio', 'vender', 'donar'],
    respuesta: 'Toca el botón "+" (Publicar), elige si es trueke, venta o donación, cuenta en qué estado está, sube fotos y marca la zona aproximada. Tus fotos se publican sin la ubicación GPS que guarda el celular.',
    accion: { texto: 'Publicar ahora', ruta: '/publicar', conSesion: true },
  },
  {
    id: 'registro-vendedor',
    tema: 'empezar',
    pregunta: '¿Por qué me piden documento, dirección y teléfono para vender?',
    variantes: ['registro de vendedor', 'por qué piden mi cédula para vender', 'quién ve mi teléfono', 'datos para vender'],
    palabrasClave: ['vendedor', 'vender', 'documento', 'cedula', 'telefono', 'direccion', 'registro', 'oferente'],
    respuesta: 'El Estatuto del Consumidor (Ley 1480, art. 53) nos exige llevar un registro de quienes venden en la plataforma. Lo pedimos una sola vez, antes de tu primera venta; truekes y donaciones no lo necesitan. Guardamos tu documento, tu dirección y tu teléfono cifrados: nadie de la comunidad los ve. Solo los entregamos a quien te compró y presenta un reclamo, o a la autoridad que los pida.',
    accion: { texto: 'Ir al registro de vendedor', ruta: '/cuenta/vendedor', conSesion: true },
    tipoPqr: 'Consulta',
  },
  {
    id: 'fundaciones',
    tema: 'empezar',
    pregunta: '¿Cómo verifico una fundación o dono a una?',
    variantes: ['soy una fundación', 'verificar mi fundación', 'insignia de fundación', 'donar a una fundación', 'qué necesitan las fundaciones'],
    palabrasClave: ['fundacion', 'fundaciones', 'esal', 'ong', 'nit', 'camara', 'comercio', 'necesitamos', 'donar', 'insignia'],
    respuesta: 'En Fundaciones ves las entidades sin ánimo de lucro verificadas y lo que necesitan: en cada necesidad, «Tengo esto» abre Publicar como donación; al publicarla le avisamos a la fundación y ella te la pide. Donarle a una fundación da los mismos Eco-Puntos que cualquier donación. Si tu cuenta representa a una fundación, pide la verificación gratis en Cuenta → Fundación con la razón social, el NIT y el certificado de existencia de la Cámara de Comercio; el equipo lo revisa y el certificado se borra al decidir.',
    accion: { texto: 'Ver las fundaciones', ruta: '/fundaciones' },
  },
  // ── Intercambios ────────────────────────────────────────────────────────────────────────────────────────────────
  {
    id: 'solicitud',
    tema: 'intercambios',
    pregunta: '¿Cómo funciona una solicitud?',
    variantes: ['cómo pido un objeto', 'quiero algo que vi', 'cómo propongo un trueke', 'lista de espera'],
    palabrasClave: ['solicitud', 'solicitar', 'pedir', 'proponer', 'reservada', 'espera'],
    respuesta: 'Cuando te interesa una publicación envías una solicitud y se abre un chat con quien la publicó. Varias personas pueden estar interesadas a la vez: la publicación sigue en el catálogo hasta que el dueño elige a una. Ahí queda reservada y las demás pasan a lista de espera; si el intercambio no se concreta, vuelve al catálogo y el dueño puede elegir a otra persona.',
    accion: { texto: 'Ir a mis intercambios', ruta: '/intercambios', conSesion: true },
  },
  {
    id: 'contraoferta',
    tema: 'intercambios',
    pregunta: '¿Puedo negociar el precio o los objetos?',
    variantes: ['hacer una contraoferta', 'ofrecer menos dinero', 'ofrecer varios objetos', 'regatear', 'pedir otro objeto a cambio'],
    palabrasClave: ['contraoferta', 'negociar', 'regatear', 'precio', 'varios', 'objetos', 'descuento'],
    respuesta: 'Sí. En una compra puedes proponer otro precio al pedirla, y en un trueke ofrecer hasta 3 de tus objetos a cambio de uno. Quien recibe la propuesta la acepta, se retira o hace una contraoferta: otro precio, o en un trueke otros objetos de quien pidió. Se responden por turnos, con un máximo de 5 contraofertas; después solo queda aceptar o retirarse. En las donaciones no hay contraofertas. El precio acordado se paga en persona al entregar.',
    accion: { texto: 'Ir a mis intercambios', ruta: '/intercambios', conSesion: true },
  },
  {
    id: 'venta-acepta-trueke',
    tema: 'intercambios',
    pregunta: '¿Puedo vender algo y también aceptar trueke?',
    variantes: ['vender o cambiar', 'acepto cambios', 'recibo trueke por lo que vendo', 'pagar con objetos'],
    palabrasClave: ['vender', 'venta', 'trueke', 'cambio', 'cambios', 'acepta', 'también'],
    respuesta: 'Sí. Al publicar en Compra y venta marca "También acepto trueke". Quien se interese podrá pagar el precio o proponerte hasta 3 de sus objetos, y durante la negociación pueden pasar de precio a objetos o al revés. Si se cierra como trueke suma los Eco-Puntos de un trueke; si se paga, los de una compra. Tu publicación también aparece cuando alguien filtra por Trueke. Si ya tenías ventas publicadas, edítalas desde Mis publicaciones y marca la casilla.',
    accion: { texto: 'Ir a mis publicaciones', ruta: '/mis-publicaciones', conSesion: true },
  },
  {
    id: 'completar',
    tema: 'intercambios',
    pregunta: '¿Cuándo se completa un intercambio?',
    variantes: ['cómo confirmo la entrega', 'no me dieron los puntos', 'cuándo me dan los eco-puntos'],
    palabrasClave: ['completar', 'confirmar', 'entrega', 'recibido', 'calificar'],
    respuesta: 'Cuando las dos partes confirman la entrega en la app. En ese momento se suman los Eco-Puntos y la reputación, y pueden calificarse. Si alguien olvida confirmar, te lo recordamos a los 3 días y otra vez un día antes de que el intercambio se cierre solo: a los 7 días si una parte ya confirmó, o a los 30 si ninguna lo hizo.',
    accion: { texto: 'Ir a mis intercambios', ruta: '/intercambios', conSesion: true },
  },
  {
    id: 'no-concretado',
    tema: 'intercambios',
    pregunta: '¿Qué pasa si el intercambio no se da?',
    variantes: ['la otra persona no aparece', 'me dejaron plantado', 'cancelar un intercambio', 'no llegó a la cita'],
    palabrasClave: ['cancelar', 'concreto', 'aparecio', 'plantado', 'cita', 'incumplio'],
    respuesta: 'Desde "Mis intercambios" cualquiera de las dos partes puede marcarlo como no concretado, explicando el motivo. La publicación vuelve a estar disponible.',
    accion: { texto: 'Ir a mis intercambios', ruta: '/intercambios', conSesion: true },
    tipoPqr: 'Queja',
    paginas: ['como-funciona'],
  },
  {
    id: 'entregar-seguro',
    tema: 'intercambios',
    pregunta: '¿Dónde y cómo entrego con seguridad?',
    variantes: ['lugar seguro para encontrarnos', 'punto de encuentro', 'me da miedo encontrarme con alguien'],
    palabrasClave: ['seguro', 'seguridad', 'encuentro', 'lugar', 'transmilenio', 'cai'],
    respuesta: 'Coordina todo por el chat y entrega de día en un lugar público y concurrido: una estación de TransMilenio, un centro comercial, una biblioteca pública o frente a un CAI. Revisa el objeto antes de confirmar y nunca pagues por adelantado a alguien que no conoces.',
  },
  // ── Chat ────────────────────────────────────────────────────────────────────────────────────────────────────────
  {
    id: 'chulitos',
    tema: 'chat',
    pregunta: '¿Qué significan los chulitos?',
    variantes: ['leyó mi mensaje', 'visto en el chat', 'mensaje entregado'],
    palabrasClave: ['chulitos', 'leido', 'visto', 'entregado', 'mensaje'],
    respuesta: 'Un ✓ es enviado, dos ✓✓ grises es que llegó al dispositivo de la otra persona, y ✓✓ de color es que ya lo leyó.',
    accion: { texto: 'Ir a mensajes', ruta: '/mensajes', conSesion: true },
  },
  {
    id: 'responder-mensaje',
    tema: 'chat',
    pregunta: '¿Cómo respondo un mensaje en particular?',
    variantes: ['citar un mensaje', 'contestar un mensaje específico'],
    palabrasClave: ['responder', 'citar', 'contestar', 'deslizar'],
    respuesta: 'En el celular desliza el mensaje hacia la derecha; en el computador pasa el mouse sobre él y toca la flecha de responder.',
  },
  {
    id: 'bloquear',
    tema: 'chat',
    pregunta: '¿Cómo bloqueo a alguien?',
    variantes: ['me está molestando', 'no quiero que me escriba', 'desbloquear a alguien'],
    palabrasClave: ['bloquear', 'desbloquear', 'molesta', 'acoso', 'insiste'],
    respuesta: 'Desde el chat o su perfil, toca "Bloquear". Ninguno de los dos podrá escribirse ni solicitar las publicaciones del otro, y no se le avisa. Puedes desbloquear en Cuenta → Privacidad y datos.',
    accion: { texto: 'Ver mis bloqueos', ruta: '/cuenta/privacidad', conSesion: true },
    tipoPqr: 'Queja',
  },
  // ── Eco-Puntos, pagos y facturas ────────────────────────────────────────────────────────────────────────────────
  {
    id: 'eco-puntos',
    tema: 'puntos',
    pregunta: '¿Qué son los Eco-Puntos?',
    variantes: ['para qué sirven los puntos', 'cómo gano puntos', 'cuánto vale un punto', 'recargar puntos'],
    palabrasClave: ['eco-puntos', 'puntos', 'ganar', 'recargar', 'recarga', 'valen'],
    respuesta: (p) => {
      const ganancias = (p?.ganancias ?? []).filter((g) => g.modo && g.puntos);
      const gana = ganancias.length
        ? ` Por cada intercambio completado ganas ${lista(ganancias.map((g) => `${g.puntos} (${g.modo === 'Compra' ? 'compra o venta' : g.modo === 'Donacion' ? 'donación' : 'trueke'})`))}.`
        : ' Los ganas al completar intercambios (más por donar).';
      const otras = [
        p?.puntosPerfilCompleto ? `${p.puntosPerfilCompleto} al completar tu perfil` : '',
        p?.puntosPrimeraPublicacion ? `${p.puntosPrimeraPublicacion} por tu primera publicación` : '',
        p?.puntosCalificacionCincoEstrellas ? `${p.puntosCalificacionCincoEstrellas} cuando te califican con 5 estrellas` : '',
        p?.puntosDenunciaConfirmada ? `${p.puntosDenunciaConfirmada} si un moderador confirma una denuncia tuya` : '',
      ].filter(Boolean);
      const extra = otras.length ? ` También ganas ${lista(otras)}.` : '';
      const valor = p?.copPorEcoPunto ? `cada punto vale ${cop(p.copPorEcoPunto)}` : 'cada punto tiene un valor fijo en pesos';
      const paquetes = (p?.paquetesRecarga ?? []).filter((x) => x.bono).map((x) => cop(x.montoCop ?? 0));
      const recarga = p?.recargaMinimaCop
        ? ` También puedes recargar desde ${cop(p.recargaMinimaCop)}${paquetes.length ? `; los paquetes de ${lista(paquetes)} traen Eco-Puntos de bono` : ''}.`
        : ' También puedes recargar.';
      return `La moneda interna de la comunidad.${gana}${extra} Sirven para impulsar publicaciones y para pagar destacar o verificar tu cuenta: ${valor} y tú eliges cuántos usar.${recarga} No se convierten en dinero.`;
    },
    accion: { texto: 'Ver mis Eco-Puntos', ruta: '/eco-puntos' },
    paginas: ['como-funciona'],
  },
  {
    id: 'invitar',
    tema: 'puntos',
    pregunta: '¿Cómo funciona «Invita y ganan los dos»?',
    variantes: ['invitar amigos', 'enlace de invitación', 'referidos', 'no me dieron los puntos de la invitación'],
    palabrasClave: ['invitar', 'invitacion', 'referido', 'amigo', 'enlace'],
    respuesta: (p) => {
      const puntos = p?.puntosInvitacion ? `cada una gana ${p.puntosInvitacion} Eco-Puntos` : 'las dos ganan Eco-Puntos';
      const tope = p?.maxInvitacionesRecompensadasPorCiclo
        ? `un máximo de ${p.maxInvitacionesRecompensadasPorCiclo} invitaciones recompensadas cada ${p.diasCicloInvitaciones ?? 30} días`
        : 'un número máximo de invitaciones recompensadas por ciclo';
      return `En Eco-Puntos encuentras tu enlace personal. Cuando la persona que invitaste complete su primer intercambio, ${puntos}. No cuenta si ese primer intercambio es contigo, y puedes ganar por ${tope}: pasado el tope, tus invitados siguen ganando y tú vuelves a ganar cuando termina el ciclo.`;
    },
    accion: { texto: 'Ver mi enlace', ruta: '/eco-puntos' },
  },
  {
    id: 'impulsar-destacar',
    tema: 'puntos',
    pregunta: '¿Qué es impulsar y qué es destacar?',
    variantes: ['cómo hago que mi publicación se vea más', 'subir mi publicación', 'destacar publicación'],
    palabrasClave: ['impulsar', 'destacar', 'destacada', 'visibilidad', 'arriba'],
    respuesta: (p) => {
      const impulsar = p?.puntosImpulsar ? `cuesta ${p.puntosImpulsar} Eco-Puntos y se puede cada ${p.horasEntreImpulsos ?? 24} horas` : 'se paga solo con Eco-Puntos y tiene un tiempo de espera';
      const destacar = p?.precioDestacarCop
        ? `cuesta ${cop(p.precioDestacarCop)}${p.duracionDestacadoDias ? ` por ${p.duracionDestacadoDias} días` : ''}`
        : 'tiene un precio en pesos';
      return `Impulsar devuelve tu publicación al primer lugar de «Más recientes», después de las destacadas: ${impulsar}. Destacar la deja primera en el catálogo con cualquier orden: ${destacar}, y puedes pagarlo con Eco-Puntos.`;
    },
    accion: { texto: 'Ir a mis publicaciones', ruta: '/mis-publicaciones', conSesion: true },
  },
  {
    id: 'pago-no-aplicado',
    tema: 'puntos',
    pregunta: 'Pagué y no veo mis Eco-Puntos o mi beneficio',
    variantes: ['me cobraron y no me llegó', 'pago pendiente', 'la recarga no aparece', 'cobro doble', 'me cobraron dos veces'],
    palabrasClave: ['pago', 'pague', 'cobro', 'cobraron', 'recarga', 'pendiente', 'wompi', 'tarjeta', 'nequi', 'pse'],
    respuesta: 'Algunos pagos tardan unos minutos en confirmarse. En Cuenta → Pagos y facturas ves el estado de cada pago con su referencia (TRK-…). Si dice aprobado y no ves el beneficio, o te cobraron algo que no reconoces, crea una PQRS con esa referencia y lo revisamos.',
    accion: { texto: 'Ir a mis pagos', ruta: '/cuenta/facturacion', conSesion: true },
    tipoPqr: 'ReversionPago',
  },
  {
    id: 'factura',
    tema: 'puntos',
    pregunta: '¿Por qué me llega una factura?',
    variantes: ['factura a mi nombre', 'factura con nit', 'factura electrónica'],
    palabrasClave: ['factura', 'facturacion', 'nit', 'cedula', 'electronica'],
    respuesta: 'La ley colombiana obliga a facturar electrónicamente cada pago a la plataforma. Sin datos, la factura sale a "consumidor final"; si la necesitas a tu nombre o al de tu empresa, agrega tu cédula o NIT en Cuenta → Pagos y facturas.',
    accion: { texto: 'Ir a mis pagos', ruta: '/cuenta/facturacion', conSesion: true },
    tipoPqr: 'Peticion',
    paginas: ['planes'],
  },
  {
    id: 'retracto',
    tema: 'puntos',
    pregunta: '¿Puedo arrepentirme de una compra?',
    variantes: ['quiero devolver un pago', 'reembolso', 'devolución del dinero', 'retracto'],
    palabrasClave: ['retracto', 'arrepentir', 'reembolso', 'devolucion', 'devolver', 'dinero'],
    respuesta: 'Sí: tienes derecho de retracto dentro de los 5 días hábiles siguientes a la compra (Ley 1480). Se solicita desde Soporte con la referencia del pago.',
    accion: { texto: 'Ir a mis pagos', ruta: '/cuenta/facturacion', conSesion: true },
    tipoPqr: 'Retracto',
    paginas: ['planes'],
  },
  // ── Planes ──────────────────────────────────────────────────────────────────────────────────────────────────────
  {
    id: 'prueba-premium',
    tema: 'planes',
    pregunta: '¿Cómo funciona la prueba gratis?',
    variantes: ['probar premium gratis', 'prueba de premium'],
    palabrasClave: ['prueba', 'premium', 'gratis', 'probar'],
    respuesta: (p) => {
      const dias = p?.diasPruebaPremium ? `${p.diasPruebaPremium} días` : 'unos días';
      const destacados = p?.destacadosGratisPruebaPremium && p.destacadosGratisPremium
        ? `, con ${p.destacadosGratisPruebaPremium} destacado gratis en vez de ${p.destacadosGratisPremium}`
        : '';
      return `Tienes Premium ${dias} sin pagar nada y sin tarjeta. Incluye todo Premium${destacados}. Al terminar vuelves al plan Gratis: no se cobra nada. Es una vez por cuenta, para quien nunca ha tenido un plan.`;
    },
    accion: { texto: 'Ver los planes', ruta: '/planes' },
    paginas: ['planes'],
  },
  {
    id: 'renovacion',
    tema: 'planes',
    pregunta: '¿Se renueva solo?',
    variantes: ['cobro automático', 'débito automático del plan', 'cancelar suscripción'],
    palabrasClave: ['renueva', 'renovar', 'automatico', 'suscripcion', 'cancelar'],
    respuesta: (p) =>
      `No. Ningún plan se cobra automáticamente. Te avisamos ${p?.diasRecordatorioVencimiento ? `${p.diasRecordatorioVencimiento} días` : 'unos días'} antes de que venza y, si renuevas antes, los días se suman sin perder tus destacados gratis.`,
    accion: { texto: 'Ver los planes', ruta: '/planes' },
    paginas: ['planes'],
  },
  {
    id: 'vencimiento',
    tema: 'planes',
    pregunta: '¿Qué pasa si se vence mi plan?',
    variantes: ['se me venció premium', 'plan vencido'],
    palabrasClave: ['vence', 'vencio', 'vencido', 'plan', 'premium', 'empresa'],
    respuesta: 'Tus publicaciones siguen activas, pero no puedes crear nuevas por encima del límite del plan Gratis. Dejas de tener el descuento, la gráfica diaria, el nombre comercial y los destacados gratis.',
    accion: { texto: 'Renovar mi plan', ruta: '/planes' },
    paginas: ['planes'],
  },
  {
    id: 'plan-anual',
    tema: 'planes',
    pregunta: '¿Cómo funciona el plan anual?',
    variantes: ['pagar un año', 'premium anual', 'empresa anual'],
    palabrasClave: ['anual', 'año', 'meses'],
    respuesta: (p) =>
      `Pagas una sola vez y tienes ${p?.duracionSuscripcionAnualDias ? `${p.duracionSuscripcionAnualDias} días` : 'un año'} de Premium o Empresa: son 12 meses por el precio de 10. Los destacados gratis se reponen igual, cada ${p?.diasCicloDestacadosGratis ? `${p.diasCicloDestacadosGratis} días` : 'ciclo'}, y si ya tienes el plan mensual los días del anual se suman a los que te quedan.`,
    accion: { texto: 'Ver los planes', ruta: '/planes' },
    paginas: ['planes'],
  },
  {
    id: 'cambiar-plan',
    tema: 'planes',
    pregunta: '¿Puedo pasar de Premium a Empresa?',
    variantes: ['cambiar de plan', 'subir a empresa'],
    palabrasClave: ['cambiar', 'empresa', 'premium', 'plan'],
    respuesta: 'Sí, cuando venza tu Premium. Durante la prueba gratis puedes pasar a Empresa cuando quieras.',
    accion: { texto: 'Ver los planes', ruta: '/planes' },
    paginas: ['planes'],
  },
  // ── Reportes, datos y seguridad ─────────────────────────────────────────────────────────────────────────────────
  {
    id: 'reportar',
    tema: 'seguridad',
    pregunta: '¿Qué hago si veo algo sospechoso?',
    variantes: ['me quieren estafar', 'estafa', 'cómo denuncio a alguien', 'qué pasa cuando reporto algo', 'publicación falsa'],
    palabrasClave: ['reportar', 'denunciar', 'sospechoso', 'estafa', 'fraude', 'falso', 'moderador'],
    respuesta: 'Usa el botón "Reportar" en la publicación, comentario o perfil. Un moderador lo revisa y, si confirma el problema, puede ocultar el contenido o suspender la cuenta; la otra persona recibe el aviso sin saber quién la reportó.',
    tipoPqr: 'Queja',
    paginas: ['como-funciona'],
  },
  {
    id: 'apelar',
    tema: 'seguridad',
    pregunta: 'Me reportaron y no estoy de acuerdo',
    variantes: ['me ocultaron una publicación', 'me suspendieron', 'apelar una sanción'],
    palabrasClave: ['apelar', 'apelacion', 'reportaron', 'suspendieron', 'ocultaron', 'sancion'],
    respuesta: 'En Cuenta → Reportes sobre ti puedes contar tu versión una vez, dentro de los 15 días siguientes. La revisa otra persona del equipo y, si te da la razón, se revierte la medida.',
    accion: { texto: 'Ver reportes sobre mí', ruta: '/cuenta/reportes', conSesion: true },
    tipoPqr: 'Reclamo',
  },
  {
    id: 'proteger-cuenta',
    tema: 'seguridad',
    pregunta: '¿Cómo protejo mi cuenta?',
    variantes: ['olvidé mi contraseña', 'cambiar contraseña', 'alguien entró a mi cuenta', 'me hackearon'],
    palabrasClave: ['contrasena', 'clave', 'hackearon', 'sesion', 'seguridad', 'olvide'],
    respuesta: 'Usa una contraseña única. Si la olvidaste, pide una nueva desde "Ingresar". Si sospechas que alguien entró, cámbiala y usa "Cerrar todas las sesiones" en Cuenta → Seguridad.',
    accion: { texto: 'Ir a seguridad', ruta: '/cuenta/seguridad', conSesion: true },
    tipoPqr: 'Peticion',
  },
  {
    id: 'mis-datos',
    tema: 'seguridad',
    pregunta: '¿Cómo protegen, descargo o elimino mis datos?',
    variantes: ['borrar mi cuenta', 'eliminar mi cuenta', 'qué datos tienen de mí', 'cómo protegen mis datos'],
    palabrasClave: ['datos', 'eliminar', 'borrar', 'descargar', 'privacidad', '1581', 'personales'],
    respuesta: 'Nunca mostramos tu correo ni tu ubicación exacta a otras personas. En Cuenta → Privacidad y datos puedes descargar todo lo que tenemos sobre ti o eliminar tu cuenta (Ley 1581 de 2012).',
    accion: { texto: 'Ir a privacidad', ruta: '/cuenta/privacidad', conSesion: true },
    tipoPqr: 'Consulta',
    paginas: ['como-funciona'],
  },
];

export const entradaPorId = (id: string) => CONOCIMIENTO.find((e) => e.id === id);

export function respuestaDe(e: EntradaConocimiento, p: Politica): string {
  return typeof e.respuesta === 'string' ? e.respuesta : e.respuesta(p);
}

/** Preguntas frecuentes de una página: primero las de su propio tema y luego las demás, en el orden de la base. */
export function preguntasDe(pagina: Exclude<Pagina, 'ayuda'>, p: Politica): { id: string; p: string; r: string }[] {
  const propias = (e: EntradaConocimiento) => (e.tema === pagina ? 0 : 1);
  return CONOCIMIENTO.filter((e) => e.paginas?.includes(pagina))
    .sort((a, b) => propias(a) - propias(b))
    .map((e) => ({ id: e.id, p: e.pregunta, r: respuestaDe(e, p) }));
}

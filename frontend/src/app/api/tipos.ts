// Tipos del contrato con la API. `schema.d.ts` se genera desde docs/openapi.json (npm run api):
// nunca se edita a mano. Aquí solo se re-exportan y se añaden etiquetas para la interfaz.
export type * from './schema';
import type { CondicionDto, ModoDto, OrdenPublicacionesDto, TipoDocumentoFiscalDto, TipoPqrDto } from './schema';

/** Estado físico del objeto. `requiereDetalle`: el backend exige describir qué tiene, qué se reparó o qué sirve. */
export const CONDICIONES: readonly {
  valor: CondicionDto;
  etiqueta: string;
  descripcion: string;
  requiereDetalle: boolean;
  clase: string;
}[] = [
  { valor: 'Nuevo', etiqueta: 'Nuevo', descripcion: 'Sin usar, con etiquetas o en su empaque.', requiereDetalle: false, clase: 'insignia-trueke' },
  { valor: 'ComoNuevo', etiqueta: 'Como nuevo', descripcion: 'Usado muy poco, sin marcas visibles.', requiereDetalle: false, clase: 'insignia-agua' },
  { valor: 'Usado', etiqueta: 'Usado', descripcion: 'Uso normal, funciona perfectamente.', requiereDetalle: false, clase: 'insignia-neutra' },
  { valor: 'UsadoConDetalles', etiqueta: 'Usado con detalles', descripcion: 'Funciona, pero tiene rayones, golpes o piezas gastadas.', requiereDetalle: true, clase: 'insignia-sol' },
  { valor: 'Reparado', etiqueta: 'Reparado', descripcion: 'Tuvo un daño y fue reparado o reacondicionado.', requiereDetalle: true, clase: 'insignia-sol' },
  { valor: 'ParaRepuestos', etiqueta: 'Para repuestos', descripcion: 'No funciona completo; sirve por sus piezas.', requiereDetalle: true, clase: 'insignia-donacion' },
];
export const infoCondicion = (c: string | null | undefined) => CONDICIONES.find((x) => x.valor === c) ?? CONDICIONES[2]!;

export const TIPOS_DOCUMENTO: readonly { valor: TipoDocumentoFiscalDto; etiqueta: string }[] = [
  { valor: 'CC', etiqueta: 'Cédula de ciudadanía' },
  { valor: 'CE', etiqueta: 'Cédula de extranjería' },
  { valor: 'NIT', etiqueta: 'NIT (empresa)' },
  { valor: 'Pasaporte', etiqueta: 'Pasaporte' },
];

export const TIPOS_PQR: readonly { valor: TipoPqrDto; etiqueta: string; descripcion: string; conPago: boolean }[] = [
  { valor: 'Peticion', etiqueta: 'Petición', descripcion: 'Solicitar información o un trámite.', conPago: false },
  { valor: 'Queja', etiqueta: 'Queja', descripcion: 'Inconformidad con la atención o el comportamiento de alguien.', conPago: false },
  { valor: 'Reclamo', etiqueta: 'Reclamo', descripcion: 'Un servicio pagado no funcionó como se ofreció.', conPago: false },
  { valor: 'Sugerencia', etiqueta: 'Sugerencia', descripcion: 'Una idea para mejorar la plataforma.', conPago: false },
  { valor: 'Retracto', etiqueta: 'Retracto', descripcion: 'Desistir de una compra dentro de los 5 días hábiles siguientes (Ley 1480, art. 47).', conPago: true },
  { valor: 'ReversionPago', etiqueta: 'Reversión del pago', descripcion: 'Pago no autorizado, fraude o servicio no prestado (Ley 1480, art. 51).', conPago: true },
];
export const etiquetaPqr = (t: string | null | undefined) => TIPOS_PQR.find((x) => x.valor === t)?.etiqueta ?? t ?? '';

export const ETIQUETA_CONCEPTO: Record<string, string> = {
  Destacar: 'Destacar publicación',
  Verificar: 'Verificar cuenta',
  Premium: 'Plan Premium',
  Empresa: 'Plan Empresa',
  Recarga: 'Recarga de Eco-Puntos',
};

export const CODIGO_BOGOTA = '11001';

/** Cuerpo de error estándar de la API (RFC 7807). `title` es el mensaje mostrable. */
export interface Problema {
  type?: string;
  title?: string;
  status?: number;
  traceId?: string;
  codigo?: string;
  errors?: Record<string, string[]>;
}

export type EstadoPublicacion = 'Disponible' | 'EnNegociacion' | 'Intercambiada' | 'Cancelada';
export type EstadoSolicitud =
  | 'Pendiente'
  | 'Aceptada'
  | 'Rechazada'
  | 'Cancelada'
  | 'Completada'
  | 'NoConcretada';
export type Rol = 'Cliente' | 'Administrador' | 'SuperUsuario';

export const MODOS: readonly ModoDto[] = ['Trueke', 'Compra', 'Donacion'];

export const INFO_MODO: Record<
  ModoDto,
  { etiqueta: string; verbo: string; descripcion: string; clase: string; icono: string; puntos: number }
> = {
  Trueke: {
    etiqueta: 'Trueke',
    verbo: 'Proponer trueke',
    descripcion: 'Cambia lo que ya no usas por algo que necesitas, sin dinero de por medio.',
    clase: 'insignia-trueke',
    icono: 'repeat',
    puntos: 10,
  },
  Compra: {
    etiqueta: 'Compra',
    verbo: 'Quiero comprarlo',
    descripcion: 'Compra de segunda mano a un precio justo y dale una nueva vida al objeto.',
    clase: 'insignia-compra',
    icono: 'bag',
    puntos: 5,
  },
  Donacion: {
    etiqueta: 'Donación',
    verbo: 'Solicitar donación',
    descripcion: 'Regala lo que ya no necesitas a quien sí lo necesita cerca de ti.',
    clase: 'insignia-donacion',
    icono: 'heart',
    puntos: 20,
  },
};

export const ETIQUETA_ESTADO_PUBLICACION: Record<string, string> = {
  Disponible: 'Disponible',
  EnNegociacion: 'Reservada',
  Intercambiada: 'Intercambiada',
  Cancelada: 'Cancelada',
};

export const ETIQUETA_ESTADO_SOLICITUD: Record<string, { texto: string; clase: string }> = {
  Pendiente: { texto: 'Esperando respuesta', clase: 'insignia-sol' },
  Aceptada: { texto: 'Aceptada · coordinando entrega', clase: 'insignia-agua' },
  Rechazada: { texto: 'Rechazada', clase: 'insignia-neutra' },
  Cancelada: { texto: 'Cancelada', clase: 'insignia-neutra' },
  Completada: { texto: 'Completada', clase: 'insignia-trueke' },
  NoConcretada: { texto: 'No se concretó', clase: 'insignia-donacion' },
};

export const ORDENES: { valor: OrdenPublicacionesDto; etiqueta: string }[] = [
  { valor: 'Recientes', etiqueta: 'Más recientes' },
  { valor: 'PrecioAsc', etiqueta: 'Precio: menor a mayor' },
  { valor: 'PrecioDesc', etiqueta: 'Precio: mayor a menor' },
];

/** Las 20 localidades de Bogotá D.C. (en otros municipios la localidad, barrio o sector es texto libre). */
export const LOCALIDADES: readonly string[] = [
  'Usaquén',
  'Chapinero',
  'Santa Fe',
  'San Cristóbal',
  'Usme',
  'Tunjuelito',
  'Bosa',
  'Kennedy',
  'Fontibón',
  'Engativá',
  'Suba',
  'Barrios Unidos',
  'Teusaquillo',
  'Los Mártires',
  'Antonio Nariño',
  'Puente Aranda',
  'La Candelaria',
  'Rafael Uribe Uribe',
  'Ciudad Bolívar',
  'Sumapaz',
];

/** Centro aproximado de cada localidad: sirve para sugerir la ubicación al publicar. */
export const CENTRO_LOCALIDAD: Record<string, [number, number]> = {
  Usaquén: [4.7031, -74.0306],
  Chapinero: [4.6486, -74.0628],
  'Santa Fe': [4.6097, -74.0697],
  'San Cristóbal': [4.5636, -74.0833],
  Usme: [4.4767, -74.1172],
  Tunjuelito: [4.5761, -74.1339],
  Bosa: [4.6181, -74.1903],
  Kennedy: [4.6286, -74.1525],
  Fontibón: [4.6783, -74.1428],
  Engativá: [4.7072, -74.1103],
  Suba: [4.7411, -74.0836],
  'Barrios Unidos': [4.6672, -74.0753],
  Teusaquillo: [4.6403, -74.0889],
  'Los Mártires': [4.6036, -74.0889],
  'Antonio Nariño': [4.5867, -74.1006],
  'Puente Aranda': [4.6192, -74.1153],
  'La Candelaria': [4.5964, -74.0733],
  'Rafael Uribe Uribe': [4.5711, -74.1147],
  'Ciudad Bolívar': [4.5089, -74.1528],
  Sumapaz: [4.0317, -74.2678],
};

export const CENTRO_BOGOTA: [number, number] = [4.6533, -74.0836];

/** Ícono por categoría (ids sembrados por el backend en TruekeDbContext). */
export const ICONO_CATEGORIA: Record<number, string> = {
  1: 'bag',
  2: 'paquete',
  3: 'rayo',
  4: 'inicio',
  5: 'cuadricula',
  6: 'regalo',
  7: 'hoja',
};
export const iconoCategoria = (id: number | null | undefined): string => ICONO_CATEGORIA[id ?? 7] ?? 'hoja';

export const MOTIVOS_DENUNCIA = [
  { valor: 'Spam', etiqueta: 'Spam o publicidad' },
  { valor: 'Fraude', etiqueta: 'Posible fraude o estafa' },
  { valor: 'ContenidoInapropiado', etiqueta: 'Contenido inapropiado' },
  { valor: 'ArticuloProhibido', etiqueta: 'Artículo prohibido' },
  { valor: 'Acoso', etiqueta: 'Acoso o lenguaje ofensivo' },
  { valor: 'Otro', etiqueta: 'Otro motivo' },
] as const;

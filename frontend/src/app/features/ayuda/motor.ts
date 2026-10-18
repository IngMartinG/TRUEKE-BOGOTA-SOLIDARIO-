import { TIPOS_PQR, type TipoPqrDto } from '../../api/tipos';
import { CONOCIMIENTO, respuestaDe, type EntradaConocimiento, type Politica } from './conocimiento';

/**
 * Motor de búsqueda de la ayuda, sin dependencias ni red: normaliza (sin tildes ni mayúsculas), reduce plurales,
 * entiende sinónimos ("clave" = "contraseña") y tolera un error de tecleo en palabras largas.
 */

/** Búsqueda sin tildes ni mayúsculas: "facturacion" encuentra "facturación". */
export function normalizar(t: string): string {
  return t.normalize('NFD').replace(/[\u0300-\u036F]/g, '').toLowerCase();
}

const VACIAS = new Set(
  ('a al algo como con cual cuales cuando de del el en es esta este esto ha hay la las le les lo los me mi mis muy ' +
    'no o para pero por que se si sin su sus te tengo tu tus un una uno unos y ya yo puedo puede hago hacer quiero ' +
    'quisiera necesito porque donde ese esa eso estoy fue era son ser sobre hola gracias favor alguien algun alguna').split(' '),
);

/** Reduce el plural más común para comparar "puntos" con "punto" y "publicaciones" con "publicacion". */
function raiz(p: string): string {
  if (p.length > 5 && p.endsWith('ones')) return p.slice(0, -2);
  if (p.length > 4 && /[^aeiou]es$/.test(p) && !p.endsWith('ses')) return p.slice(0, -2);
  if (p.length > 3 && p.endsWith('s') && !p.endsWith('ss')) return p.slice(0, -1);
  return p;
}

export function palabras(t: string): string[] {
  const limpio = normalizar(t).replace(/eco[\s-]?puntos?/g, 'puntos').replace(/[^a-z0-9ñ]+/g, ' ');
  return [...new Set(limpio.split(' ').filter((p) => p.length > 1 && !VACIAS.has(p)).map(raiz))];
}

/** Grupos de palabras que la gente usa para lo mismo. */
const GRUPOS_SINONIMOS: readonly string[][] = [
  ['contrasena', 'clave', 'password', 'pin'],
  ['foto', 'imagen', 'avatar', 'selfie', 'retrato'],
  ['correo', 'email', 'mail', 'gmail', 'hotmail'],
  ['punto', 'ecopunto', 'moneda', 'saldo'],
  ['pago', 'pagar', 'pague', 'cobro', 'cobrar', 'cobraron', 'cobran', 'cargo', 'descontaron'],
  ['eliminar', 'borrar', 'suprimir', 'baja', 'quitar'],
  ['estafa', 'fraude', 'estafaron', 'enganaron', 'robo', 'robaron', 'timo', 'sospechoso'],
  ['reembolso', 'devolucion', 'devolver', 'retracto', 'arrepenti', 'arrepentir'],
  ['bloquear', 'bloqueo', 'acoso', 'acosa', 'acosando', 'acosan', 'molesta', 'molestando'],
  ['publicar', 'publicacion', 'anuncio', 'aviso', 'ofrecer'],
  ['factura', 'facturacion', 'recibo', 'comprobante'],
  ['intercambio', 'trueke', 'trueque', 'cambio', 'canje'],
  ['chat', 'mensaje', 'escribir', 'conversacion'],
  ['premium', 'plan', 'suscripcion', 'membresia'],
  ['gratis', 'gratuito', 'costo', 'cuesta', 'precio'],
  ['hackearon', 'hackeo', 'hackeada', 'entraron'],
  ['denunciar', 'reportar', 'reporte', 'denuncia', 'queja'],
  ['entrega', 'entregar', 'encuentro', 'cita', 'recoger'],
  ['cancelar', 'anular', 'concreto', 'plantado'],
];
const SINONIMOS = new Map<string, number>();
GRUPOS_SINONIMOS.forEach((g, i) => g.flatMap(palabras).forEach((p) => SINONIMOS.set(p, i)));

/** Distancia de edición ≤ 1 (una letra de más, de menos o cambiada). */
function casiIgual(a: string, b: string): boolean {
  if (Math.abs(a.length - b.length) > 1) return false;
  let i = 0;
  let j = 0;
  let diferencias = 0;
  while (i < a.length && j < b.length) {
    if (a[i] === b[j]) {
      i++;
      j++;
      continue;
    }
    if (++diferencias > 1) return false;
    if (a.length > b.length) i++;
    else if (b.length > a.length) j++;
    else {
      i++;
      j++;
    }
  }
  return diferencias + (a.length - i) + (b.length - j) <= 1;
}

/** Qué tanto se parece una palabra de la pregunta a una del índice (0 a 1). */
function parecido(q: string, w: string): number {
  if (q === w) return 1;
  const gq = SINONIMOS.get(q);
  if (gq !== undefined && gq === SINONIMOS.get(w)) return 0.85;
  if (q.length >= 4 && w.length >= 4 && (w.startsWith(q) || q.startsWith(w))) return 0.7;
  if (q.length >= 5 && w.length >= 5 && casiIgual(q, w)) return 0.6;
  return 0;
}

interface Indice {
  entrada: EntradaConocimiento;
  fuertes: string[];
  debiles: string[];
  frases: string[];
}
const indices = new Map<Politica, Indice[]>();

function indexar(p: Politica): Indice[] {
  let idx = indices.get(p);
  if (!idx) {
    idx = CONOCIMIENTO.map((e) => ({
      entrada: e,
      fuertes: palabras([e.pregunta, ...e.variantes, ...e.palabrasClave].join(' ')),
      debiles: palabras(respuestaDe(e, p)),
      frases: [e.pregunta, ...e.variantes].map((f) => normalizar(f).replace(/[¿?¡!.,]/g, '').trim()),
    }));
    indices.clear(); // solo se guarda la política vigente
    indices.set(p, idx);
  }
  return idx;
}

export interface Resultado {
  entrada: EntradaConocimiento;
  /** 0 a 1: cuánto de lo que preguntó la persona encontró eco en la entrada. */
  puntaje: number;
}

/** Desde aquí una entrada cuenta como respuesta; por debajo solo se sugiere como "quizá buscabas". */
export const UMBRAL_RESPUESTA = 0.45;
export const UMBRAL_SUGERENCIA = 0.2;

/** Entradas ordenadas por parecido con la pregunta (solo las que pasan el umbral de sugerencia). */
export function buscar(pregunta: string, p?: Politica): Resultado[] {
  const q = palabras(pregunta);
  if (!q.length) return [];
  const frase = normalizar(pregunta).replace(/[¿?¡!.,]/g, '').trim();
  return indexar(p)
    .map(({ entrada, fuertes, debiles, frases }) => {
      let suma = 0;
      let fuertesEncontradas = 0;
      for (const palabra of q) {
        const f = Math.max(0, ...fuertes.map((w) => parecido(palabra, w)));
        const d = Math.max(0, ...debiles.map((w) => parecido(palabra, w))) * 0.45;
        if (f > 0) fuertesEncontradas++;
        suma += Math.max(f, d);
      }
      let puntaje = fuertesEncontradas ? suma / q.length : (suma / q.length) * 0.5;
      if (frase.length >= 6 && frases.some((x) => x.includes(frase) || frase.includes(x))) puntaje += 0.35;
      return { entrada, puntaje: Math.min(1, puntaje) };
    })
    .filter((r) => r.puntaje >= UMBRAL_SUGERENCIA)
    .sort((a, b) => b.puntaje - a.puntaje);
}

/** Filtro del Centro de ayuda: texto literal (como antes) o parecido suficiente. */
export function coincide(e: EntradaConocimiento, consulta: string, p?: Politica): boolean {
  const q = normalizar(consulta.trim());
  if (!q) return true;
  if (normalizar(`${e.pregunta} ${respuestaDe(e, p)}`).includes(q)) return true;
  return buscar(consulta, p).some((r) => r.entrada.id === e.id && r.puntaje >= UMBRAL_RESPUESTA);
}

const PISTAS_TIPO: readonly [TipoPqrDto, RegExp][] = [
  ['Consulta', /\b(mis datos|datos personales|habeas|1581|que datos)\b/],
  ['Retracto', /\b(retracto|arrepenti\w*)\b/],
  ['ReversionPago', /\b(no autorice|no reconozco|cobro doble|cobraron dos|fraude con mi tarjeta)\b/],
  ['Reclamo', /\b(cobr\w*|pag\w*|reembols\w*|devolu\w*|no funciono|no sirvio el|plata|dinero)\b/],
  ['Queja', /\b(estaf\w*|grosero\w*|acos\w*|maltrat\w*|insult\w*|amenaz\w*|robo|robaron|enga\w*|mala atencion)\b/],
  ['Sugerencia', /\b(sugeren\w*|sugiero|idea|seria bueno|deberian|mejorar|agreguen|propongo)\b/],
];

/** Tipo de PQRS que mejor encaja con lo que escribió la persona, si no hay una entrada que lo diga. */
export function tipoSugerido(texto: string, entrada?: EntradaConocimiento | null): TipoPqrDto {
  if (entrada?.tipoPqr) return entrada.tipoPqr;
  const t = normalizar(texto);
  return PISTAS_TIPO.find(([, re]) => re.test(t))?.[0] ?? 'Peticion';
}

/** Sin sesión no se pueden pedir retracto ni reversión (van sobre un pago propio): se radican como reclamo. */
export function tipoParaEscalar(tipo: TipoPqrDto, conSesion: boolean): TipoPqrDto {
  return !conSesion && TIPOS_PQR.find((t) => t.valor === tipo)?.conPago ? 'Reclamo' : tipo;
}

export function esTipoPqr(v: string | null | undefined): v is TipoPqrDto {
  return TIPOS_PQR.some((t) => t.valor === v);
}

import type { PoliticaEcoPuntosDto } from '../../api/tipos';
import { escalarAPqrs } from './borrador-pqrs';
import { CONOCIMIENTO, entradaPorId, preguntasDe, respuestaDe } from './conocimiento';
import { buscar, coincide, palabras, tipoParaEscalar, tipoSugerido } from './motor';
import { responderLocal } from './proveedor-respuestas';

describe('motor de búsqueda de la ayuda', () => {
  it.each([
    ['olvidé mi clave', 'proteger-cuenta'],
    ['me cobraron dos veces', 'pago-no-aplicado'],
    ['cómo subo mi foto?', 'foto-perfil'],
    ['necesito la factura con mi NIT', 'factura'],
    ['cuanto valen los ecopuntos', 'eco-puntos'],
    ['quiero borrar mi cuenta', 'mis-datos'],
    ['me estafaron', 'reportar'],
    ['FACTURASION', 'factura'], // escrita sin tilde, en mayúsculas y con un error de tecleo
    ['la persona no llegó a la cita', 'no-concretado'],
    ['alguien me está acosando por el chat', 'bloquear'],
    ['se renueva solo el premium?', 'renovacion'],
  ])('«%s» responde con %s', (pregunta, id) => {
    expect(responderLocal(pregunta).entrada?.id).toBe(id);
  });

  it('sin coincidencia no inventa una respuesta y propone una petición', () => {
    const r = responderLocal('cuál es el clima en Medellín mañana');
    expect(r.entrada).toBeNull();
    expect(r.texto).toBe('');
    expect(r.tipoPqr).toBe('Peticion');
  });

  it('una pregunta vacía o solo de palabras vacías no encuentra nada', () => {
    expect(buscar('')).toEqual([]);
    expect(buscar('¿y que?')).toEqual([]);
  });

  it('reduce plurales y entiende sinónimos', () => {
    expect(palabras('Las publicaciones y los Eco-Puntos')).toEqual(['publicacion', 'punto']);
    expect(buscar('contraseña')[0]?.entrada.id).toBe(buscar('clave')[0]?.entrada.id);
  });

  it('el Centro de ayuda filtra por texto literal y por parecido', () => {
    const factura = entradaPorId('factura')!;
    expect(coincide(factura, 'consumidor final')).toBe(true);
    expect(coincide(factura, 'recibo')).toBe(true);
    expect(coincide(factura, 'bloquear')).toBe(false);
    expect(coincide(factura, '   ')).toBe(true);
  });

  it('las cifras salen de la política en vivo y sin ella no se inventan', () => {
    const eco = entradaPorId('eco-puntos')!;
    const politica = { copPorEcoPunto: 250, recargaMinimaCop: 7000, ganancias: [{ modo: 'Donacion', puntos: 33 }] } as PoliticaEcoPuntosDto;
    expect(respuestaDe(eco, politica)).toContain('$250');
    expect(respuestaDe(eco, politica)).toContain('33 (donación)');
    expect(respuestaDe(eco, null)).not.toMatch(/\$\d/);
  });

  it('cada entrada tiene lo necesario y los ids no se repiten', () => {
    expect(new Set(CONOCIMIENTO.map((e) => e.id)).size).toBe(CONOCIMIENTO.length);
    for (const e of CONOCIMIENTO) {
      expect(e.pregunta.length).toBeGreaterThan(5);
      expect(e.palabrasClave.length).toBeGreaterThan(0);
      expect(respuestaDe(e, null).length).toBeGreaterThan(20);
    }
    expect(preguntasDe('planes', null).length).toBeGreaterThanOrEqual(5);
    expect(preguntasDe('como-funciona', null).length).toBeGreaterThanOrEqual(4);
  });
});

describe('escalado a PQRS', () => {
  it('sugiere el tipo según lo que escribió la persona', () => {
    expect(tipoSugerido('tengo una idea para mejorar la app')).toBe('Sugerencia');
    expect(tipoSugerido('qué datos personales tienen de mí')).toBe('Consulta');
    expect(tipoSugerido('el vendedor fue grosero')).toBe('Queja');
    expect(tipoSugerido('me cobraron algo raro')).toBe('Reclamo');
    expect(tipoSugerido('algo', entradaPorId('retracto'))).toBe('Retracto');
  });

  it('sin sesión el retracto y la reversión se radican como reclamo', () => {
    expect(tipoParaEscalar('ReversionPago', false)).toBe('Reclamo');
    expect(tipoParaEscalar('ReversionPago', true)).toBe('ReversionPago');
    expect(tipoParaEscalar('Queja', false)).toBe('Queja');
  });

  it('con sesión lleva a la cuenta; sin sesión, a la PQRS pública, con la conversación precargada', () => {
    const turnos = [
      { pregunta: 'Pagué una recarga y no aparece', respondio: 'Pagué y no veo mis Eco-Puntos o mi beneficio', sirvio: false },
    ];
    const conSesion = escalarAPqrs(turnos, 'ReversionPago', true);
    expect(conSesion.ruta).toBe('/cuenta/soporte');
    expect(conSesion.tipo).toBe('ReversionPago');
    expect(conSesion.borrador.asunto).toBe('Pagué una recarga y no aparece');
    expect(conSesion.borrador.descripcion).toContain('pero no me sirvió');

    const publica = escalarAPqrs([{ pregunta: 'hola\u0007', respondio: null, sirvio: null }], 'ReversionPago', false);
    expect(publica.ruta).toBe('/pqrs');
    expect(publica.tipo).toBe('Reclamo');
    expect(publica.borrador.asunto).toBe('Reclamo desde el asistente de ayuda'); // "hola" es muy corto para asunto
    expect(publica.borrador.descripcion).toContain('no encontró una respuesta');
    expect(publica.borrador.descripcion).not.toContain('\u0007');
  });
});

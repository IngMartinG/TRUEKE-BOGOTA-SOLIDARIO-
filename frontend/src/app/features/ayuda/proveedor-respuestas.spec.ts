import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ConfigService } from '../../core/config.service';
import { SesionService } from '../../core/sesion.service';
import { historialDe } from './asistente-soporte';
import { ProveedorIA, ProveedorLocal, ProveedorRespuestas, type RespuestaAsistente } from './proveedor-respuestas';

const GUIA: RespuestaAsistente = { entrada: null, texto: 'respuesta de la guía', relacionadas: [], tipoPqr: 'Peticion' };

function preparar({ ia = true, autenticado = true, verificado = true } = {}) {
  const local = { responder: vi.fn(async () => GUIA), responderEntrada: vi.fn(async () => GUIA) };
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: ConfigService, useValue: { asistenteIa: signal(ia) } },
      { provide: SesionService, useValue: { autenticado: signal(autenticado), correoVerificado: signal(verificado) } },
      { provide: ProveedorLocal, useValue: local },
    ],
  });
  return { proveedor: TestBed.inject(ProveedorRespuestas), control: TestBed.inject(HttpTestingController), local };
}

const URL_PREGUNTAR = (u: string) => u.includes('/asistente/preguntar');

describe('ProveedorIA', () => {
  it('es el proveedor del asistente', () => {
    expect(preparar().proveedor).toBeInstanceOf(ProveedorIA);
  });

  it.each([
    ['la IA está apagada en el servidor', { ia: false }],
    ['no hay sesión', { autenticado: false }],
    ['el correo no está verificado', { verificado: false }],
  ])('responde con la guía local si %s', async (_, opciones) => {
    const { proveedor, control, local } = preparar(opciones);
    expect(await proveedor.responder('hola')).toBe(GUIA);
    expect(local.responder).toHaveBeenCalledWith('hola');
    control.expectNone((r) => URL_PREGUNTAR(r.url));
  });

  it('pregunta a la IA con el historial y devuelve su texto y sus botones', async () => {
    const { proveedor, control } = preparar();
    const historial = [{ rol: 'Usuario' as const, texto: 'hola' }, { rol: 'Asistente' as const, texto: '¡Hola!' }];
    const respuesta = proveedor.responder('¿cuántos puntos tengo?', historial);

    const req = control.expectOne((r) => URL_PREGUNTAR(r.url));
    expect(req.request.body).toEqual({ pregunta: '¿cuántos puntos tengo?', historial });
    req.flush({ texto: 'Tienes 30 Eco-Puntos.', enlaces: [{ texto: 'Ver mis Eco-Puntos', ruta: '/eco-puntos' }] });

    const r = await respuesta;
    expect(r.ia).toBe(true);
    expect(r.texto).toBe('Tienes 30 Eco-Puntos.');
    expect(r.enlaces).toEqual([{ texto: 'Ver mis Eco-Puntos', ruta: '/eco-puntos', conSesion: true }]);
    control.verify();
  });

  it.each([
    [429, 'se acabaron'],
    [503, 'no está disponible'],
  ])('si la API responde %i contesta la guía local con un aviso', async (estado, aviso) => {
    const { proveedor, control } = preparar();
    const respuesta = proveedor.responder('hola');
    control.expectOne((r) => URL_PREGUNTAR(r.url)).flush({}, { status: estado, statusText: 'x' });

    const r = await respuesta;
    expect(r.texto).toBe(GUIA.texto);
    expect(r.ia).toBeUndefined();
    expect(r.nota).toContain(aviso);
  });

  it('los temas frecuentes siempre salen de la guía local', async () => {
    const { proveedor, control, local } = preparar();
    await proveedor.responderEntrada({ id: 'x', tema: 'empezar', pregunta: '¿?', variantes: [], palabrasClave: [], respuesta: '' });
    expect(local.responderEntrada).toHaveBeenCalled();
    control.expectNone((r) => URL_PREGUNTAR(r.url));
  });
});

describe('historialDe', () => {
  it('envía solo los últimos tres turnos con respuesta y recorta el texto', () => {
    const turnos = [
      { pregunta: 'uno', texto: 'r1', cargando: false },
      { pregunta: 'dos', texto: 'r2', cargando: false },
      { pregunta: 'sin respuesta', texto: '', cargando: false },
      { pregunta: 'tres', texto: 'x'.repeat(1500), cargando: false },
      { pregunta: 'cuatro', texto: 'r4', cargando: false },
      { pregunta: 'pendiente', texto: '', cargando: true },
    ];
    const h = historialDe(turnos);
    expect(h.map((t) => t.texto.slice(0, 4))).toEqual(['dos', 'r2', 'tres', 'xxxx', 'cuat', 'r4']);
    expect(h[3].texto.length).toBe(1000);
    expect(h.map((t) => t.rol)).toEqual(['Usuario', 'Asistente', 'Usuario', 'Asistente', 'Usuario', 'Asistente']);
  });
});

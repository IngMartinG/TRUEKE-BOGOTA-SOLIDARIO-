import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import type { ContextoChatDto, MensajeMarcadoDto } from '../../api/tipos';
import { AdminApi } from '../../core/api/admin.api';
import { AvisosService } from '../../core/avisos.service';
import Chat from './chat';

describe('Revisión del chat', () => {
  const marcado: MensajeMarcadoDto = {
    mensajeId: 'm2', conversacionId: 'c1', autorId: 'u2', autor: 'Beto Pérez', texto: 'Escríbeme al 310 555 1234',
    tieneFoto: false, motivo: 'Comparte un teléfono', marcadoUtc: new Date().toISOString(),
  };
  const contexto: ContextoChatDto = {
    mensajeId: 'm2', conversacionId: 'c1', publicacion: 'Bicicleta', marcaPendiente: true, denuncias: 0,
    participantes: [{ id: 'u1', nombreCompleto: 'Ana Gómez' }, { id: 'u2', nombreCompleto: 'Beto Pérez' }],
    mensajes: [
      { id: 'm1', autorId: 'u1', texto: '¿El sábado te sirve?', fechaUtc: new Date().toISOString(), oculto: false, esElRevisado: false },
      { id: 'm2', autorId: 'u2', texto: 'Escríbeme al 310 555 1234', fechaUtc: new Date().toISOString(), oculto: false,
        motivoRevision: 'Comparte un teléfono', esElRevisado: true },
    ],
  };

  async function crear(mensaje: string | null) {
    const query = new BehaviorSubject(convertToParamMap(mensaje ? { mensaje } : {}));
    const api = {
      mensajesMarcados: vi.fn().mockReturnValue(of([marcado])),
      contextoChat: vi.fn().mockReturnValue(of(contexto)),
      resolverMensajeMarcado: vi.fn().mockReturnValue(of(undefined)),
    };
    TestBed.configureTestingModule({
      imports: [Chat],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { queryParamMap: query } },
        { provide: AdminApi, useValue: api },
        { provide: AvisosService, useValue: { exito: vi.fn(), error: vi.fn() } },
      ],
    });
    const f = TestBed.createComponent(Chat);
    f.detectChanges();
    await f.whenStable();
    f.detectChanges();
    return { el: f.nativeElement as HTMLElement, api };
  }

  it('lista los mensajes marcados sin abrir ninguna conversación', async () => {
    const { el, api } = await crear(null);
    expect(el.textContent).toContain('Comparte un teléfono');
    expect(el.textContent).toContain('Beto Pérez');
    expect(api.contextoChat).not.toHaveBeenCalled();
  });

  it('abre el contexto solo del mensaje pedido, resaltado y con las acciones', async () => {
    const { el, api } = await crear('m2');
    expect(api.contextoChat).toHaveBeenCalledWith('m2');
    expect(el.textContent).toContain('Conversación sobre «Bicicleta»');
    const resaltado = [...el.querySelectorAll('li')].find((li) => li.querySelector('.ring-2'));
    expect(resaltado?.textContent).toContain('310 555 1234');
    expect(el.textContent).toContain('Ocultar el mensaje');
    expect(el.textContent).toContain('No es un problema');
  });
});

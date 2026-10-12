import { AvisosService } from './avisos.service';

describe('AvisosService', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('se cierra solo, pero no mientras está en pausa', () => {
    const avisos = new AvisosService();
    avisos.exito('Guardado');
    const id = avisos.avisos()[0]!.id;

    vi.advanceTimersByTime(3000);
    avisos.pausar(id);
    vi.advanceTimersByTime(60_000);
    expect(avisos.avisos()).toHaveLength(1);

    avisos.reanudar(id);
    vi.advanceTimersByTime(1999);
    expect(avisos.avisos()).toHaveLength(1);
    vi.advanceTimersByTime(1);
    expect(avisos.avisos()).toHaveLength(0);
  });

  it('no repite el mismo aviso y guarda como máximo cuatro', () => {
    const avisos = new AvisosService();
    avisos.error('Sin conexión');
    avisos.error('Sin conexión');
    expect(avisos.avisos()).toHaveLength(1);
    for (const t of ['a', 'b', 'c', 'd']) avisos.info(t);
    expect(avisos.avisos().map((a) => a.titulo)).toEqual(['a', 'b', 'c', 'd']);
  });
});

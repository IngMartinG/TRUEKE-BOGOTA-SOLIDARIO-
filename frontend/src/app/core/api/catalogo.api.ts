import { inject, Injectable } from '@angular/core';
import { shareReplay } from 'rxjs';
import type {
  ImpactoDto,
  BusquedaGuardadaDto,
  CoincidenciaDeseoDto,
  DeseoDto,
  GuardarDeseoRequest,
  CategoriaDto,
  ComentarioDto,
  ComentarioDtoPaginaDto,
  CondicionDto,
  CrearPublicacionRequest,
  EstadisticasPublicacionDto,
  GuardarBusquedaRequest,
  ModoDto,
  ObjetoTipicoDto,
  PeriodoImpactoDto,
  OrdenPublicacionesDto,
  PublicacionCercanaDto,
  PublicacionDto,
  PublicacionDtoPaginaDto,
} from '../../api/tipos';
import { HttpApi, seg } from './http-api';

export interface FiltrosCatalogo {
  texto?: string;
  categoriaId?: number | null;
  modo?: ModoDto | null;
  condicion?: CondicionDto | null;
  departamentoCodigo?: string | null;
  municipioCodigo?: string | null;
  localidad?: string | null;
  precioMin?: number | null;
  precioMax?: number | null;
  soloVerificados?: boolean;
  /** Solo las de esta cuenta (vitrina de un negocio). */
  propietarioId?: string;
  orden?: OrdenPublicacionesDto;
  pagina?: number;
  tamano?: number;
}

@Injectable({ providedIn: 'root' })
export class CatalogoApi {
  private readonly api = inject(HttpApi);

  /** Las categorías casi nunca cambian: se piden una vez por sesión de navegador. */
  readonly categorias$ = this.api.get<CategoriaDto[]>('/categorias').pipe(shareReplay(1));

  listar = (f: FiltrosCatalogo) =>
    this.api.get<PublicacionDtoPaginaDto>('/publicaciones', {
      params: { ...f, soloVerificados: f.soloVerificados || undefined },
    });
  cercanas = (lat: number, lon: number, radioKm = 5, modo?: ModoDto | null, categoriaId?: number | null, condicion?: CondicionDto | null,
    soloVerificados = false) =>
    this.api.get<PublicacionCercanaDto[]>('/publicaciones/cercanas', {
      params: { lat, lon, radioKm, modo, categoriaId, condicion, soloVerificados: soloVerificados || undefined, max: 50 },
    });
  /** Público: totales de intercambios completados (el backend los guarda 10 min); una petición por sesión. */
  impacto$ = this.api.get<ImpactoDto>('/impacto', { silencioso: true }).pipe(shareReplay(1));
  /** Con filtros (la localidad debe ser una de `localidadesDisponibles`). */
  impactoFiltrado = (f: { periodo?: PeriodoImpactoDto; modo?: ModoDto | null; municipioCodigo?: string | null; localidad?: string | null }) =>
    this.api.get<ImpactoDto>('/impacto', {
      params: { periodo: f.periodo && f.periodo !== 'Todo' ? f.periodo : undefined, modo: f.modo ?? undefined,
        municipioCodigo: f.localidad ? (f.municipioCodigo ?? undefined) : undefined, localidad: f.localidad ?? undefined },
      silencioso: true,
    });
  /** Ayuda al publicar: objetos comunes con su peso y material (no cambian: una petición por sesión). */
  objetosTipicos$ = this.api.get<ObjetoTipicoDto[]>('/impacto/objetos-tipicos', { silencioso: true }).pipe(shareReplay(1));
  sugerencias = (texto: string, max = 8) =>
    this.api.get<string[]>('/publicaciones/sugerencias', { params: { texto, max }, silencioso: true });
  obtener = (id: string) => this.api.get<PublicacionDto>(`/publicaciones/${seg(id)}`, { silencioso: true });
  crear = (r: CrearPublicacionRequest) => this.api.post<PublicacionDto>('/publicaciones', r, { silencioso: true });
  editar = (id: string, r: CrearPublicacionRequest) =>
    this.api.put<PublicacionDto>(`/publicaciones/${seg(id)}`, r, { silencioso: true });
  mias = () => this.api.get<PublicacionDto[]>('/publicaciones/mias');
  cancelar = (id: string, motivo: string | null) =>
    this.api.post<void>(`/publicaciones/${seg(id)}/cancelar`, { motivo });
  destacarGratis = (id: string) => this.api.post<void>(`/publicaciones/${seg(id)}/destacar-gratis`);
  impulsar = (id: string) => this.api.post<PublicacionDto>(`/publicaciones/${seg(id)}/impulsar`, {}, { silencioso: true });
  /** "¿Sigue disponible?": confirma que sí, o reactiva la publicación si ya se pausó. */
  sigueDisponible = (id: string) => this.api.post<PublicacionDto>(`/publicaciones/${seg(id)}/sigue-disponible`, {}, { silencioso: true });
  estadisticas = (id: string) => this.api.get<EstadisticasPublicacionDto>(`/publicaciones/${seg(id)}/estadisticas`);
  destacadas = (f: { departamentoCodigo?: string | null; municipioCodigo?: string | null; categoriaId?: number | null }, max = 8) =>
    this.api.get<PublicacionDto[]>('/publicaciones/destacadas', { params: { ...f, max }, silencioso: true });

  busquedasGuardadas = () => this.api.get<BusquedaGuardadaDto[]>('/busquedas-guardadas');
  guardarBusqueda = (r: GuardarBusquedaRequest) => this.api.post<BusquedaGuardadaDto>('/busquedas-guardadas', r, { silencioso: true });
  alertasBusqueda = (id: string, activas: boolean) =>
    this.api.put<BusquedaGuardadaDto>(`/busquedas-guardadas/${seg(id)}/alertas`, { activas });
  eliminarBusqueda = (id: string) => this.api.delete<void>(`/busquedas-guardadas/${seg(id)}`);

  deseos = () => this.api.get<DeseoDto[]>('/deseos');
  crearDeseo = (r: GuardarDeseoRequest) => this.api.post<DeseoDto>('/deseos', r, { silencioso: true });
  editarDeseo = (id: string, r: GuardarDeseoRequest) => this.api.put<DeseoDto>(`/deseos/${seg(id)}`, r, { silencioso: true });
  eliminarDeseo = (id: string) => this.api.delete<void>(`/deseos/${seg(id)}`);
  coincidencias = () => this.api.get<CoincidenciaDeseoDto[]>('/deseos/coincidencias');

  marcarFavorito = (id: string) => this.api.post<void>(`/publicaciones/${seg(id)}/favorito`);
  quitarFavorito = (id: string) => this.api.delete<void>(`/publicaciones/${seg(id)}/favorito`);
  favoritos = (pagina = 1, tamano = 24) =>
    this.api.get<PublicacionDtoPaginaDto>('/favoritos', { params: { pagina, tamano } });

  comentarios = (id: string, pagina = 1, tamano = 20) =>
    this.api.get<ComentarioDtoPaginaDto>(`/publicaciones/${seg(id)}/comentarios`, { params: { pagina, tamano } });
  comentar = (id: string, texto: string) =>
    this.api.post<ComentarioDto>(`/publicaciones/${seg(id)}/comentarios`, { texto });
}

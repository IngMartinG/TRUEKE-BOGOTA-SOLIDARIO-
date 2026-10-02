import { inject, Injectable } from '@angular/core';
import { shareReplay } from 'rxjs';
import type {
  CategoriaDto,
  ComentarioDto,
  ComentarioDtoPaginaDto,
  CrearPublicacionRequest,
  ModoDto,
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
  localidad?: string | null;
  precioMin?: number | null;
  precioMax?: number | null;
  soloVerificados?: boolean;
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
  cercanas = (lat: number, lon: number, radioKm = 5, modo?: ModoDto | null, categoriaId?: number | null) =>
    this.api.get<PublicacionCercanaDto[]>('/publicaciones/cercanas', {
      params: { lat, lon, radioKm, modo, categoriaId, max: 60 },
    });
  obtener = (id: string) => this.api.get<PublicacionDto>(`/publicaciones/${seg(id)}`, { silencioso: true });
  crear = (r: CrearPublicacionRequest) => this.api.post<PublicacionDto>('/publicaciones', r, { silencioso: true });
  editar = (id: string, r: CrearPublicacionRequest) =>
    this.api.put<PublicacionDto>(`/publicaciones/${seg(id)}`, r, { silencioso: true });
  mias = () => this.api.get<PublicacionDto[]>('/publicaciones/mias');
  cancelar = (id: string, motivo: string | null) =>
    this.api.post<void>(`/publicaciones/${seg(id)}/cancelar`, { motivo });
  destacarGratis = (id: string) => this.api.post<void>(`/publicaciones/${seg(id)}/destacar-gratis`);

  marcarFavorito = (id: string) => this.api.post<void>(`/publicaciones/${seg(id)}/favorito`);
  quitarFavorito = (id: string) => this.api.delete<void>(`/publicaciones/${seg(id)}/favorito`);
  favoritos = (pagina = 1, tamano = 24) =>
    this.api.get<PublicacionDtoPaginaDto>('/favoritos', { params: { pagina, tamano } });

  comentarios = (id: string, pagina = 1, tamano = 20) =>
    this.api.get<ComentarioDtoPaginaDto>(`/publicaciones/${seg(id)}/comentarios`, { params: { pagina, tamano } });
  comentar = (id: string, texto: string) =>
    this.api.post<ComentarioDto>(`/publicaciones/${seg(id)}/comentarios`, { texto });
}

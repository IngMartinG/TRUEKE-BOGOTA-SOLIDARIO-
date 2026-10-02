import { inject, Injectable } from '@angular/core';
import { shareReplay } from 'rxjs';
import type {
  ActualizarPerfilRequest,
  CalificacionDtoPaginaDto,
  ConceptoPagoDto,
  ConfiguracionPublicaDto,
  CotizacionDto,
  CrearDenunciaRequest,
  DatosPersonalesDto,
  DenunciaCreadaDto,
  EcoPuntosResumenDto,
  EliminarCuentaRequest,
  IniciarPagoRequest,
  NotificacionDtoPaginaDto,
  PagoEstadoDto,
  PagoIniciadoDto,
  PerfilUsuarioDto,
  PoliticaEcoPuntosDto,
  PublicacionDtoPaginaDto,
  SolicitarSubidaRequest,
  SubidaArchivoDto,
  UsuarioDto,
} from '../../api/tipos';
import { HttpApi, seg } from './http-api';

@Injectable({ providedIn: 'root' })
export class CuentaApi {
  private readonly api = inject(HttpApi);

  configuracion = () => this.api.get<ConfiguracionPublicaDto>('/configuracion', { silencioso: true });

  yo = () => this.api.get<UsuarioDto>('/usuarios/yo');
  actualizarPerfil = (r: ActualizarPerfilRequest) => this.api.put<UsuarioDto>('/usuarios/yo', r, { silencioso: true });
  misDatos = () => this.api.get<DatosPersonalesDto>('/usuarios/yo/datos');
  eliminarCuenta = (r: EliminarCuentaRequest) => this.api.post<void>('/usuarios/yo/eliminar', r, { silencioso: true });

  perfil = (id: string) => this.api.get<PerfilUsuarioDto>(`/usuarios/${seg(id)}/perfil`, { silencioso: true });
  publicacionesDe = (id: string, pagina = 1, tamano = 12) =>
    this.api.get<PublicacionDtoPaginaDto>(`/usuarios/${seg(id)}/publicaciones`, { params: { pagina, tamano } });
  calificacionesDe = (id: string, pagina = 1, tamano = 10) =>
    this.api.get<CalificacionDtoPaginaDto>(`/usuarios/${seg(id)}/calificaciones`, { params: { pagina, tamano } });

  readonly politica$ = this.api.get<PoliticaEcoPuntosDto>('/eco-puntos/politica').pipe(shareReplay(1));
  resumenPuntos = () => this.api.get<EcoPuntosResumenDto>('/eco-puntos/resumen');
  cotizacion = (concepto: ConceptoPagoDto) =>
    this.api.get<CotizacionDto>('/eco-puntos/cotizacion', { params: { concepto } });

  iniciarPago = (r: IniciarPagoRequest) => this.api.post<PagoIniciadoDto>('/pagos/iniciar', r, { silencioso: true });
  estadoPago = (ref: string) => this.api.get<PagoEstadoDto>(`/pagos/${seg(ref)}`, { silencioso: true });
  simularPago = (ref: string, aprobado: boolean) =>
    this.api.post<PagoEstadoDto>(`/pagos/${seg(ref)}/simular`, {}, { params: { aprobado } });

  notificaciones = (soloNoLeidas = false, pagina = 1, tamano = 20) =>
    this.api.get<NotificacionDtoPaginaDto>('/notificaciones', { params: { soloNoLeidas, pagina, tamano } });
  totalNoLeidas = () => this.api.get<number>('/notificaciones/no-leidas/total', { silencioso: true });
  leerNotificacion = (id: string) => this.api.post<void>(`/notificaciones/${seg(id)}/leer`, {}, { silencioso: true });
  leerTodas = () => this.api.post<void>('/notificaciones/leer-todas');

  solicitarSubida = (r: SolicitarSubidaRequest) =>
    this.api.post<SubidaArchivoDto>('/archivos/subidas', r, { silencioso: true });
  denunciar = (r: CrearDenunciaRequest) => this.api.post<DenunciaCreadaDto>('/denuncias', r, { silencioso: true });
}

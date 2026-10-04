import { inject, Injectable } from '@angular/core';
import type {
  CalificacionDto,
  ConversacionDto,
  CrearSolicitudRequest,
  MensajeChatDto,
  SolicitudDto,
} from '../../api/tipos';
import { HttpApi, seg } from './http-api';

@Injectable({ providedIn: 'root' })
export class IntercambiosApi {
  private readonly api = inject(HttpApi);

  solicitar = (r: CrearSolicitudRequest) => this.api.post<SolicitudDto>('/solicitudes', r, { silencioso: true });
  enviadas = () => this.api.get<SolicitudDto[]>('/solicitudes/enviadas');
  recibidas = () => this.api.get<SolicitudDto[]>('/solicitudes/recibidas');
  aceptar = (id: string) => this.api.post<SolicitudDto>(`/solicitudes/${seg(id)}/aceptar`);
  rechazar = (id: string, motivo: string | null) =>
    this.api.post<void>(`/solicitudes/${seg(id)}/rechazar`, { motivo });
  cancelar = (id: string) => this.api.post<void>(`/solicitudes/${seg(id)}/cancelar`);
  confirmarEntrega = (id: string) => this.api.post<SolicitudDto>(`/solicitudes/${seg(id)}/confirmar-entrega`);
  noConcretada = (id: string, motivo: string) =>
    this.api.post<SolicitudDto>(`/solicitudes/${seg(id)}/no-concretada`, { motivo });
  calificar = (id: string, estrellas: number, comentario: string | null) =>
    this.api.post<CalificacionDto>(`/solicitudes/${seg(id)}/calificar`, { estrellas, comentario });

  conversaciones = () => this.api.get<ConversacionDto[]>('/conversaciones');
  mensajes = (id: string, antesDe?: string, tamano = 40) =>
    this.api.get<MensajeChatDto[]>(`/conversaciones/${seg(id)}/mensajes`, { params: { antesDe, tamano } });
  enviarMensaje = (id: string, texto: string, respuestaAId: string | null = null) =>
    this.api.post<MensajeChatDto>(`/conversaciones/${seg(id)}/mensajes`, { texto, respuestaAId });
  marcarLeida = (id: string) => this.api.post<void>(`/conversaciones/${seg(id)}/leer`, {}, { silencioso: true });
}

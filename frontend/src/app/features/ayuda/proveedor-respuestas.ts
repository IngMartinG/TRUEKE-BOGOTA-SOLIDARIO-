import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { catchError, firstValueFrom, of, timeout } from 'rxjs';
import type { RespuestaAsistenteDto, TipoPqrDto, TurnoAsistenteRequest } from '../../api/tipos';
import { AsistenteApi } from '../../core/api/asistente.api';
import { CuentaApi } from '../../core/api/cuenta.api';
import { ConfigService } from '../../core/config.service';
import { SesionService } from '../../core/sesion.service';
import { respuestaDe, type AccionAyuda, type EntradaConocimiento, type Politica } from './conocimiento';
import { buscar, tipoSugerido, UMBRAL_RESPUESTA } from './motor';

export interface RespuestaAsistente {
  /** La entrada de la base que responde, o null si nada coincidió (o si respondió la IA). */
  entrada: EntradaConocimiento | null;
  texto: string;
  /** Otras entradas parecidas para ofrecer como "quizá buscabas". */
  relacionadas: EntradaConocimiento[];
  /** Tipo de PQRS que se propone si la persona dice que no le sirvió. */
  tipoPqr: TipoPqrDto;
  /** Respondió el asistente con IA: el texto es suyo y los botones vienen en `enlaces`. */
  ia?: boolean;
  enlaces?: AccionAyuda[];
  /** Aviso corto cuando la IA no pudo responder y contestó la guía local. */
  nota?: string;
}

/** Turnos anteriores que se envían como contexto a la IA (la conversación no se guarda en el servidor). */
export type TurnoHistorial = TurnoAsistenteRequest;

/**
 * Quien contesta las preguntas del asistente: la guía local o, con la IA activa y el correo verificado, el endpoint
 * propio del backend (la llave y el límite de uso viven en el servidor, nunca en el front).
 */
@Injectable({ providedIn: 'root', useFactory: () => inject(ProveedorIA) })
export abstract class ProveedorRespuestas {
  abstract responder(pregunta: string, historial?: readonly TurnoHistorial[]): Promise<RespuestaAsistente>;
  abstract responderEntrada(entrada: EntradaConocimiento): Promise<RespuestaAsistente>;
}

/** Responde con la base de conocimiento local (función pura, para probarla sin Angular). */
export function responderLocal(pregunta: string, p?: Politica): RespuestaAsistente {
  const resultados = buscar(pregunta, p);
  const mejor = resultados[0]?.puntaje >= UMBRAL_RESPUESTA ? resultados[0].entrada : null;
  return {
    entrada: mejor,
    texto: mejor ? respuestaDe(mejor, p) : '',
    relacionadas: resultados
      .map((r) => r.entrada)
      .filter((e) => e !== mejor)
      .slice(0, 3),
    tipoPqr: tipoSugerido(pregunta, mejor),
  };
}

/** Convierte la respuesta de la API: los botones de páginas de la cuenta llevan a ingresar si la sesión se cerró. */
export function desdeIA(pregunta: string, r: RespuestaAsistenteDto): RespuestaAsistente {
  return {
    entrada: null,
    texto: r.texto ?? '',
    relacionadas: [],
    tipoPqr: tipoSugerido(pregunta, null),
    ia: true,
    enlaces: (r.enlaces ?? []).map((e) => ({ texto: e.texto ?? '', ruta: e.ruta ?? '/', conSesion: true })),
  };
}

@Injectable({ providedIn: 'root' })
export class ProveedorLocal extends ProveedorRespuestas {
  private readonly api = inject(CuentaApi);

  /** Las cifras salen de la política en vivo; si la API tarda o falla, se responde sin números. */
  private politica(): Promise<Politica> {
    return firstValueFrom(this.api.politica$.pipe(timeout(3000), catchError(() => of(null))));
  }

  async responder(pregunta: string): Promise<RespuestaAsistente> {
    return responderLocal(pregunta, await this.politica());
  }

  async responderEntrada(entrada: EntradaConocimiento): Promise<RespuestaAsistente> {
    const p = await this.politica();
    return { entrada, texto: respuestaDe(entrada, p), relacionadas: [], tipoPqr: tipoSugerido('', entrada) };
  }
}

/**
 * Pregunta a la IA solo si el servidor la tiene activa y la cuenta tiene el correo verificado. Si se acabó el cupo, la IA
 * no está disponible o no hay red, contesta la guía local con un aviso: el asistente nunca queda sin respuesta.
 * Los temas frecuentes (botones) siempre salen de la guía local: son instantáneos y no gastan el cupo.
 */
@Injectable({ providedIn: 'root' })
export class ProveedorIA extends ProveedorRespuestas {
  private readonly local = inject(ProveedorLocal);
  private readonly api = inject(AsistenteApi);
  private readonly config = inject(ConfigService);
  private readonly sesion = inject(SesionService);

  /** El servidor corta a los 15 s con Groq; un poco más de margen para la red del celular. */
  static readonly ESPERA_MS = 25_000;

  private disponible(): boolean {
    return this.config.asistenteIa() && this.sesion.autenticado() && this.sesion.correoVerificado();
  }

  async responder(pregunta: string, historial: readonly TurnoHistorial[] = []): Promise<RespuestaAsistente> {
    if (!this.disponible()) return this.local.responder(pregunta);
    try {
      const r = await firstValueFrom(this.api.preguntar({ pregunta, historial: historial.slice(-6) }).pipe(timeout(ProveedorIA.ESPERA_MS)));
      return desdeIA(pregunta, r);
    } catch (e) {
      const nota =
        e instanceof HttpErrorResponse && e.status === 429
          ? 'Por hoy se acabaron tus preguntas al asistente inteligente; te respondo con la guía.'
          : 'El asistente inteligente no está disponible ahora; te respondo con la guía.';
      return { ...(await this.local.responder(pregunta)), nota };
    }
  }

  responderEntrada(entrada: EntradaConocimiento): Promise<RespuestaAsistente> {
    return this.local.responderEntrada(entrada);
  }
}

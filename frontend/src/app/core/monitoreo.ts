import { HttpErrorResponse } from '@angular/common/http';
import { ErrorHandler, Injectable } from '@angular/core';
import type { Breadcrumb, ErrorEvent } from '@sentry/browser';
import { monitoreo } from './entorno';
import { esFalloDeCarga } from './navegacion';

/**
 * Monitoreo de errores del navegador con Sentry (plan gratis), opcional: solo con `monitoreoDsn` en config.json. El SDK
 * se descarga aparte y después de arrancar, así que sin DSN no pesa nada.
 * Ley 1581: no viaja ningún dato personal. Sin usuario, IP, cookies ni cabeceras (`dataCollection`), sin consola, sin query ni
 * fragmento en las URL (tokens de verificación, enlaces firmados) y los correos de los mensajes se reemplazan.
 */

/** Errores que llegan antes de que cargue el SDK (como mucho unos pocos: el resto no aporta). */
const pendientes: unknown[] = [];
const MAX_PENDIENTES = 5;
let reportar: ((error: unknown) => void) | null = null;

/**
 * Lo que no es un fallo del código: la API ya registra sus errores y un fragmento sin red se reintenta solo. Un
 * `resource()` en error envuelve el de la API en `cause`.
 */
export function debeReportarse(error: unknown): boolean {
  for (let e = error, i = 0; e && i < 5; e = (e as { cause?: unknown }).cause, i++) {
    if (e instanceof HttpErrorResponse || esFalloDeCarga(e)) return false;
  }
  return true;
}

export function reportarError(error: unknown): void {
  if (!debeReportarse(error)) return;
  if (reportar) reportar(error);
  else if (pendientes.length < MAX_PENDIENTES) pendientes.push(error);
}

export function sinQuery(url: string): string;
export function sinQuery(url: string | undefined): string | undefined;
export function sinQuery(url: string | undefined): string | undefined {
  return url?.replace(/[?#].*$/s, '');
}

const CORREO = /[^\s@<>"'(),;:]+@[^\s@<>"'(),;:]+\.[a-zA-Z]{2,}/g;
/** La query de una URL dentro de un texto (mensajes de error que la citan). */
const QUERY_EN_URL = /(\bhttps?:\/\/[^\s?#"'<>]*)[?#][^\s"'<>]*/g;
/** Correos y query de las URL citadas en un mensaje. */
export const limpiarTexto = (texto: string | undefined): string | undefined =>
  texto?.replace(QUERY_EN_URL, '$1').replace(CORREO, '[correo]');

export function limpiarEvento(evento: ErrorEvent): ErrorEvent {
  delete evento.user;
  if (evento.request) {
    evento.request.url = sinQuery(evento.request.url);
    delete evento.request.query_string;
    delete evento.request.cookies;
    // Solo el navegador: la cabecera Referer trae la URL completa de la página anterior.
    const agente = evento.request.headers?.['User-Agent'];
    evento.request.headers = agente ? { 'User-Agent': agente } : {};
  }
  evento.message = limpiarTexto(evento.message);
  for (const excepcion of evento.exception?.values ?? []) excepcion.value = limpiarTexto(excepcion.value);
  return evento;
}

export function limpiarMiga(miga: Breadcrumb): Breadcrumb {
  miga.message = limpiarTexto(miga.message);
  if (miga.data) {
    for (const clave of ['url', 'from', 'to']) {
      if (typeof miga.data[clave] === 'string') miga.data[clave] = sinQuery(miga.data[clave] as string);
    }
  }
  return miga;
}

export async function iniciarMonitoreo(): Promise<void> {
  const { dsn, version } = monitoreo();
  if (!dsn || reportar) return;
  const Sentry = await import('@sentry/browser');
  Sentry.init({
    dsn,
    release: version ? `trueke-web@${version}` : undefined,
    dataCollection: { userInfo: false, cookies: false, httpHeaders: false, httpBodies: [], urlQueryParams: false },
    // Sin el manejo global del SDK: Angular ya entrega a ManejadorErrores los errores de la ventana y las promesas.
    defaultIntegrations: false,
    integrations: [
      Sentry.eventFiltersIntegration(),
      Sentry.functionToStringIntegration(),
      Sentry.breadcrumbsIntegration(), // sin consola: en esta versión es otra integración, que no se agrega
      Sentry.linkedErrorsIntegration(),
      Sentry.dedupeIntegration(),
      Sentry.httpContextIntegration(),
    ],
    tracesSampleRate: 0,
    beforeSend: limpiarEvento,
    beforeBreadcrumb: limpiarMiga,
  });
  reportar = (error) => Sentry.captureException(error);
  for (const error of pendientes.splice(0)) reportar(error);
}

/** Igual que el de Angular (al registro de la consola) y, si está activo, a Sentry. */
@Injectable()
export class ManejadorErrores extends ErrorHandler {
  override handleError(error: unknown): void {
    super.handleError(error);
    reportarError(error);
  }
}

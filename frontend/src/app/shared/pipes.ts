import { Pipe, PipeTransform } from '@angular/core';

const formatoCop = new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 });
const formatoNumero = new Intl.NumberFormat('es-CO');
const formatoFecha = new Intl.DateTimeFormat('es-CO', { day: 'numeric', month: 'short', year: 'numeric' });
const formatoFechaHora = new Intl.DateTimeFormat('es-CO', {
  day: 'numeric',
  month: 'short',
  hour: 'numeric',
  minute: '2-digit',
});
const formatoHora = new Intl.DateTimeFormat('es-CO', { hour: 'numeric', minute: '2-digit' });
const relativo = new Intl.RelativeTimeFormat('es-CO', { numeric: 'auto' });

export const cop = (valor: number | null | undefined): string =>
  valor === null || valor === undefined ? '' : formatoCop.format(valor);

/** $ 25.000 */
@Pipe({ name: 'cop' })
export class CopPipe implements PipeTransform {
  transform = cop;
}

/** 1.250 */
@Pipe({ name: 'numero' })
export class NumeroPipe implements PipeTransform {
  transform(valor: number | null | undefined): string {
    return valor === null || valor === undefined ? '0' : formatoNumero.format(valor);
  }
}

/** "hace 5 minutos", "ayer", "12 sept 2026". Las fechas de la API vienen en UTC con Z. */
@Pipe({ name: 'hace' })
export class HacePipe implements PipeTransform {
  transform(fecha: string | null | undefined): string {
    if (!fecha) return '';
    const t = Date.parse(fecha);
    const seg = Math.round((t - Date.now()) / 1000);
    const abs = Math.abs(seg);
    if (abs < 45) return seg <= 0 ? 'justo ahora' : 'en unos segundos';
    if (abs < 3600) return relativo.format(Math.round(seg / 60), 'minute');
    if (abs < 86400) return relativo.format(Math.round(seg / 3600), 'hour');
    if (abs < 86400 * 7) return relativo.format(Math.round(seg / 86400), 'day');
    return formatoFecha.format(t);
  }
}

@Pipe({ name: 'fecha' })
export class FechaPipe implements PipeTransform {
  transform(fecha: string | null | undefined, conHora = false): string {
    if (!fecha) return '';
    return (conHora ? formatoFechaHora : formatoFecha).format(Date.parse(fecha));
  }
}

@Pipe({ name: 'hora' })
export class HoraPipe implements PipeTransform {
  transform(fecha: string | null | undefined): string {
    return fecha ? formatoHora.format(Date.parse(fecha)) : '';
  }
}

/** "María Fernanda Ruiz" → "MR" */
export const iniciales = (nombre: string | null | undefined): string =>
  (nombre ?? '?')
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((p) => p[0]!.toUpperCase())
    .join('') || '?';

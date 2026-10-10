import { ChangeDetectionStrategy, Component, computed, effect, inject, input, linkedSignal, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { CuentaApi } from '../../core/api/cuenta.api';
import { SesionService } from '../../core/sesion.service';
import { FechaPipe, HacePipe } from '../../shared/pipes';
import { Avatar } from '../../shared/ui/avatar';
import { Bloquear } from '../../shared/ui/bloquear';
import { Denunciar } from '../../shared/ui/denunciar';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Estrellas } from '../../shared/ui/estrellas';
import { Icono } from '../../shared/ui/icono';
import { Paginador } from '../../shared/ui/paginador';
import { TarjetaEsqueleto, TarjetaPublicacion } from '../../shared/ui/tarjeta-publicacion';
import { VisorImagenes } from '../../shared/ui/visor-imagenes';
import { Volver } from '../../shared/ui/volver';

@Component({
  imports: [Volver, RouterLink, Avatar, Estrellas, Icono, EstadoVacio, Paginador, TarjetaPublicacion, TarjetaEsqueleto, Denunciar, Bloquear, VisorImagenes, FechaPipe, HacePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (perfil.error()) {
      <app-estado-vacio icono="usuario" titulo="Este perfil no está disponible" descripcion="Puede que la cuenta ya no exista.">
        <a routerLink="/explorar" class="btn btn-primario">Ir al catálogo</a>
      </app-estado-vacio>
    } @else {
      <section class="bg-gradient-to-b from-bosque-100 to-fondo dark:from-bosque-950/70">
        <div class="contenedor pt-4"><app-volver respaldo="/explorar" /></div>
        <div class="contenedor flex flex-col items-center gap-6 pt-4 pb-10 text-center sm:flex-row sm:text-left">
          @if (perfil.value(); as p) {
            @if (p.fotoUrl) {
              <button type="button" class="shrink-0 cursor-zoom-in rounded-full focus-visible:outline-offset-4" (click)="fotoAbierta.set(true)"
                [attr.aria-label]="'Ver la foto de ' + p.nombre + ' en grande'">
                <app-avatar [nombre]="p.nombre" [foto]="p.fotoUrl" [tamano]="104" [verificado]="!!p.verificado" />
              </button>
              <app-visor-imagenes [imagenes]="[p.fotoUrl]" [titulo]="p.nombre ?? ''" [(abierto)]="fotoAbierta" />
            } @else {
              <app-avatar [nombre]="p.nombre" [foto]="p.fotoUrl" [tamano]="104" [verificado]="!!p.verificado" />
            }
            <div class="flex-1">
              <div class="flex flex-wrap items-center justify-center gap-2 sm:justify-start">
                <h1 class="text-3xl font-extrabold">{{ p.nombreComercial || p.nombre }}</h1>
                @if (p.nombreComercial) {
                  <span class="text-sm text-tenue">({{ p.nombre }})</span>
                }
                @if (p.verificado) {
                  <span class="insignia-agua"><app-icono nombre="verificado" [tamano]="12" />Verificada</span>
                }
                @if (p.tipoCuenta && p.tipoCuenta !== 'Individual') {
                  <span class="insignia-sol"><app-icono nombre="corona" [tamano]="12" />{{ p.tipoCuenta }}</span>
                }
              </div>
              <p class="mt-1 flex flex-wrap items-center justify-center gap-x-3 gap-y-1 text-sm text-tenue sm:justify-start">
                <span class="flex items-center gap-1"><app-icono nombre="pin" [tamano]="14" />{{ p.localidad }}@if (p.municipio) {<span>, {{ p.municipio }}</span>}</span>
                <span>Miembro desde {{ p.miembroDesde | fecha }}</span>
              </p>
              @if (p.calificacionPromedio) {
                <p class="mt-2 flex items-center justify-center gap-2 sm:justify-start">
                  <app-estrellas [valor]="p.calificacionPromedio" [tamano]="18" />
                  <span class="text-sm font-semibold">{{ p.calificacionPromedio.toFixed(1) }}</span>
                  <span class="text-sm text-tenue">({{ p.totalCalificaciones }} calificaciones)</span>
                </p>
              }
            </div>
            <dl class="grid grid-cols-2 gap-2 text-center sm:grid-cols-4">
              @for (d of datos(); track d.texto) {
                <div class="min-w-0 rounded-2xl bg-superficie p-3 shadow-suave sm:min-w-20">
                  <dd class="font-display text-2xl font-extrabold">{{ d.valor }}</dd>
                  <dt class="text-[11px] leading-tight text-tenue">{{ d.texto }}</dt>
                </div>
              }
            </dl>
          } @else {
            <div class="esqueleto size-26 rounded-full"></div>
            <div class="flex-1 space-y-3"><div class="esqueleto h-8 w-60"></div><div class="esqueleto h-4 w-40"></div></div>
          }
        </div>
      </section>

      <div class="contenedor grid grid-cols-1 gap-10 py-10 lg:grid-cols-[1fr_22rem]">
        <section aria-labelledby="titulo-publicaciones">
          <h2 id="titulo-publicaciones" class="text-xl font-bold">Publicaciones activas</h2>
          <div class="mt-5 grid grid-cols-2 gap-3 sm:gap-5 xl:grid-cols-3">
            @if (publicaciones.isLoading()) {
              @for (i of [1, 2, 3]; track i) {
                <app-tarjeta-esqueleto />
              }
            } @else {
              @for (pub of publicaciones.value()?.items ?? []; track pub.id) {
                <app-tarjeta-publicacion [publicacion]="pub" />
              }
            }
          </div>
          @if (!publicaciones.isLoading() && !publicaciones.value()?.items?.length) {
            <p class="mt-4 rounded-tarjeta border-2 border-dashed border-borde p-8 text-center text-sm text-tenue">No tiene publicaciones activas en este momento.</p>
          }
          <app-paginador [pagina]="paginaPub()" [total]="publicaciones.value()?.total ?? 0" [tamano]="12" (cambiar)="paginaPub.set($event)" />
        </section>

        <aside aria-labelledby="titulo-calificaciones">
          @if (perfil.value()?.confianza; as c) {
            <section class="tarjeta mb-8 p-5" aria-labelledby="titulo-confianza">
              <h2 id="titulo-confianza" class="flex items-center gap-2 text-lg font-bold"><app-icono nombre="escudo" [tamano]="20" class="text-bosque-600" />Señales de confianza</h2>
              <ul class="mt-4 space-y-2.5 text-sm">
                @for (s of senales(); track s.texto) {
                  <li class="flex items-start gap-2.5">
                    <span class="mt-0.5 grid size-5 shrink-0 place-items-center rounded-full"
                      [class]="s.ok ? 'bg-bosque-100 text-bosque-700 dark:bg-bosque-900 dark:text-bosque-200' : 'bg-superficie-2 text-tenue'">
                      <app-icono [nombre]="s.ok ? 'check' : 'x'" [tamano]="12" [grosor]="3" />
                    </span>
                    <span [class.text-tenue]="!s.ok">{{ s.texto }}</span>
                  </li>
                }
              </ul>
              <dl class="mt-4 grid grid-cols-2 gap-2 text-center">
                <div class="rounded-xl bg-superficie-2 p-3">
                  <dd class="font-display text-xl font-extrabold">{{ c.tasaConcrecion !== null && c.tasaConcrecion !== undefined ? c.tasaConcrecion + ' %' : '—' }}</dd>
                  <dt class="text-[11px] leading-tight text-tenue">Intercambios que concretó (último año)</dt>
                </div>
                <div class="rounded-xl bg-superficie-2 p-3">
                  <dd class="font-display text-xl font-extrabold">{{ respuesta(c.respuestaHoras) }}</dd>
                  <dt class="text-[11px] leading-tight text-tenue">Tiempo típico en responder</dt>
                </div>
              </dl>
              @if ((perfil.value()?.totalCalificaciones ?? 0) > 0) {
                <div class="mt-4 space-y-1" aria-label="Distribución de calificaciones">
                  @for (e of distribucion(); track e.estrellas) {
                    <div class="flex items-center gap-2 text-xs">
                      <span class="w-7 shrink-0 text-right tabular-nums">{{ e.estrellas }} ★</span>
                      <span class="h-2 flex-1 overflow-hidden rounded-full bg-superficie-2">
                        <span class="block h-full rounded-full bg-sol-400" [style.width.%]="e.porcentaje"></span>
                      </span>
                      <span class="w-6 shrink-0 tabular-nums text-tenue">{{ e.total }}</span>
                    </div>
                  }
                </div>
              }
              <p class="mt-4 text-xs text-tenue">Consejo: conversen por el chat de Trueke y encuéntrense en un lugar público y concurrido.</p>
            </section>
          }
          <h2 id="titulo-calificaciones" class="text-xl font-bold">Lo que dice la comunidad</h2>
          <ul class="mt-5 space-y-3">
            @for (c of calificaciones.value()?.items ?? []; track c.id) {
              <li class="tarjeta p-4">
                <div class="flex items-center gap-3">
                  <app-avatar [nombre]="c.autor?.nombre" [foto]="c.autor?.fotoUrl" [tamano]="32" />
                  <div class="min-w-0 flex-1">
                    <p class="truncate text-sm font-semibold">{{ c.autor?.nombre }}</p>
                    <app-estrellas [valor]="c.estrellas ?? 0" [tamano]="12" />
                  </div>
                  <span class="text-xs text-tenue">{{ c.fechaUtc | hace }}</span>
                </div>
                @if (c.comentario) {
                  <p class="mt-2 text-sm break-words text-tinta/85">{{ c.comentario }}</p>
                }
              </li>
            } @empty {
              @if (!calificaciones.isLoading()) {
                <li class="rounded-tarjeta border-2 border-dashed border-borde p-6 text-center text-sm text-tenue">Aún no tiene calificaciones.</li>
              }
            }
          </ul>
          @if ((calificaciones.value()?.total ?? 0) > (calificaciones.value()?.items?.length ?? 0)) {
            <button type="button" class="btn btn-secundario btn-sm mt-4 w-full" (click)="paginaCal.set(paginaCal() + 1)">Ver más</button>
          }
          @if (sesion.autenticado() && sesion.usuario()?.id !== id()) {
            <div class="mt-6 flex flex-wrap items-center gap-x-4 gap-y-2">
              <button type="button" class="flex items-center gap-1 text-xs text-tenue hover:text-tierra-600" (click)="denunciaAbierta.set(true)">
                <app-icono nombre="bandera" [tamano]="14" />Reportar este perfil
              </button>
              <app-bloquear [usuarioId]="id()" [nombre]="perfil.value()?.nombre ?? 'esta persona'" [(bloqueado)]="bloqueado"
                clase="flex items-center gap-1 text-xs text-tenue hover:text-tierra-600" />
            </div>
            <app-denunciar [(abierto)]="denunciaAbierta" tipo="Usuario" [objetivoId]="id()" />
          }
        </aside>
      </div>
    }
  `,
})
export default class PerfilPublico {
  private readonly api = inject(CuentaApi);
  private readonly titulo = inject(Title);
  protected readonly sesion = inject(SesionService);
  readonly id = input.required<string>();
  protected readonly paginaPub = signal(1);
  protected readonly paginaCal = signal(1);
  protected readonly denunciaAbierta = signal(false);
  /** Si yo bloqueé a esta persona (sale de mi lista de bloqueados; solo con sesión). */
  private readonly misBloqueados = rxResource({
    params: () => (this.sesion.autenticado() ? true : undefined),
    stream: () => this.api.bloqueados(),
  });
  protected readonly bloqueado = linkedSignal(() => !!this.misBloqueados.value()?.some((b) => b.perfil?.id === this.id()));

  protected readonly perfil = rxResource({ params: () => this.id(), stream: ({ params }) => this.api.perfil(params) });
  protected readonly publicaciones = rxResource({
    params: () => ({ id: this.id(), pagina: this.paginaPub() }),
    stream: ({ params }) => this.api.publicacionesDe(params.id, params.pagina, 12),
  });
  protected readonly calificaciones = rxResource({
    params: () => ({ id: this.id(), tamano: 5 * this.paginaCal() }),
    stream: ({ params }) => this.api.calificacionesDe(params.id, 1, params.tamano),
  });

  protected readonly datos = computed(() => {
    const p = this.perfil.value();
    return [
      { texto: 'Truekes', valor: p?.truekesCompletados ?? 0 },
      { texto: 'Ventas y compras', valor: p?.comprasRealizadas ?? 0 },
      { texto: 'Donaciones', valor: p?.donacionesRealizadas ?? 0 },
      { texto: 'Reputación', valor: (p?.reputacion ?? 0).toFixed(1) },
    ];
  });

  protected readonly fotoAbierta = signal(false);

  /** Hechos verificables (sin datos privados) que ayudan a decidir si intercambiar con esta persona. */
  protected readonly senales = computed(() => {
    const p = this.perfil.value();
    const c = p?.confianza;
    if (!p || !c) return [];
    const total = c.intercambiosCompletados ?? 0;
    return [
      { ok: !!p.fotoUrl, texto: p.fotoUrl ? 'Tiene foto de perfil' : 'Aún no tiene foto de perfil' },
      { ok: !!c.correoVerificado, texto: 'Correo verificado' },
      { ok: !!c.identidadVerificada, texto: c.identidadVerificada ? 'Identidad verificada con documento' : 'Identidad aún no verificada con documento' },
      { ok: !!c.dosFactores, texto: c.dosFactores ? 'Protege su cuenta con verificación en dos pasos' : 'Sin verificación en dos pasos' },
      ...(c.conGoogle ? [{ ok: true, texto: 'Ingresa con su cuenta de Google' }] : []),
      { ok: total > 0, texto: total > 0 ? `${total} ${total === 1 ? 'intercambio completado' : 'intercambios completados'}` : 'Aún no completa intercambios' },
      ...(c.enLinea ? [{ ok: true, texto: 'En línea ahora' }] : []),
    ];
  });

  protected readonly distribucion = computed(() => {
    const e = this.perfil.value()?.confianza?.estrellas ?? [];
    const total = e.reduce((a, b) => a + b, 0) || 1;
    return [5, 4, 3, 2, 1].map((n) => ({ estrellas: n, total: e[n - 1] ?? 0, porcentaje: Math.round((100 * (e[n - 1] ?? 0)) / total) }));
  });

  protected respuesta(horas: number | null | undefined): string {
    if (horas === null || horas === undefined) return '—';
    if (horas < 1) return 'Menos de 1 h';
    if (horas < 24) return `${Math.round(horas)} h`;
    const dias = Math.round(horas / 24);
    return `${dias} ${dias === 1 ? 'día' : 'días'}`;
  }

  constructor() {
    effect(() => {
      const n = this.perfil.value()?.nombre;
      if (n) this.titulo.setTitle(`${n} · Trueke Bogotá Solidario`);
    });
  }
}

import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  input,
  NgZone,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { Router } from '@angular/router';
import type * as L from 'leaflet';
import { CENTRO_BOGOTA } from '../../api/tipos';

export interface PuntoMapa {
  id: string;
  lat: number;
  lon: number;
  titulo: string;
  modo?: string | null;
  detalle?: string;
}

const COLOR_MODO: Record<string, string> = { Trueke: '#1f7a4d', Compra: '#2f6fa3', Donacion: '#c25a2e' };
const TESELAS = 'https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png';
const ATRIBUCION =
  '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> &copy; <a href="https://carto.com/attributions">CARTO</a>';

/**
 * Mapa con Leaflet (se descarga solo cuando se usa). Modos:
 * - puntos: marcadores de publicaciones; al tocar uno se abre su ficha.
 * - seleccion: la persona toca el mapa para elegir una ubicación (`ubicacion` bidireccional).
 * - area: círculo de ubicación aproximada (privacidad: nunca se muestra el punto exacto a terceros).
 */
@Component({
  selector: 'app-mapa',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'relative block overflow-hidden' },
  template: `
    <div #contenedor class="size-full min-h-64 bg-superficie-2" role="application" [attr.aria-label]="etiqueta()"></div>
    @if (!listo()) {
      <div class="absolute inset-0 esqueleto rounded-none"></div>
    }
  `,
})
export class Mapa {
  readonly puntos = input<PuntoMapa[]>([]);
  readonly centro = input<[number, number] | null>(null);
  readonly zoom = input(12);
  readonly seleccion = input(false);
  readonly ubicacion = input<[number, number] | null>(null);
  readonly area = input<{ lat: number; lon: number; radioM: number } | null>(null);
  readonly etiqueta = input('Mapa');
  readonly ubicacionCambiada = output<[number, number]>();

  protected readonly listo = signal(false);
  private readonly contenedor = viewChild.required<ElementRef<HTMLElement>>('contenedor');
  private readonly router = inject(Router);
  private readonly zona = inject(NgZone);
  private leaflet: typeof L | null = null;
  private mapa: L.Map | null = null;
  private capaPuntos: L.LayerGroup | null = null;
  private marcadorSeleccion: L.Marker | null = null;
  private circulo: L.Circle | null = null;

  constructor() {
    afterNextRender(() => void this.iniciar());
    inject(DestroyRef).onDestroy(() => this.mapa?.remove());

    effect(() => {
      const puntos = this.puntos();
      if (this.listo()) this.dibujarPuntos(puntos);
    });
    effect(() => {
      const u = this.ubicacion();
      if (this.listo()) this.dibujarSeleccion(u);
    });
    effect(() => {
      const a = this.area();
      if (this.listo()) this.dibujarArea(a);
    });
    effect(() => {
      const c = this.centro();
      if (this.listo() && c) this.mapa?.setView(c, this.zoom(), { animate: true });
    });
  }

  private async iniciar(): Promise<void> {
    const modulo = (await import('leaflet')) as unknown as { default?: typeof L } & typeof L;
    const lf = modulo.default ?? modulo;
    this.leaflet = lf;
    this.zona.runOutsideAngular(() => {
      const mapa = lf.map(this.contenedor().nativeElement, {
        center: this.centro() ?? this.ubicacion() ?? CENTRO_BOGOTA,
        zoom: this.zoom(),
        scrollWheelZoom: false,
        zoomControl: true,
        attributionControl: true,
      });
      lf.tileLayer(TESELAS, { attribution: ATRIBUCION, maxZoom: 19, subdomains: 'abcd', crossOrigin: true }).addTo(mapa);
      mapa.on('focus', () => mapa.scrollWheelZoom.enable());
      mapa.on('blur', () => mapa.scrollWheelZoom.disable());
      if (this.seleccion()) {
        mapa.on('click', (e: L.LeafletMouseEvent) => this.elegir(e.latlng.lat, e.latlng.lng));
      }
      this.mapa = mapa;
      this.capaPuntos = lf.layerGroup().addTo(mapa);
      // El contenedor puede cambiar de tamaño al terminar de pintarse el layout.
      setTimeout(() => mapa.invalidateSize(), 150);
    });
    this.listo.set(true);
  }

  private elegir(lat: number, lon: number): void {
    const punto: [number, number] = [Number(lat.toFixed(6)), Number(lon.toFixed(6))];
    this.zona.run(() => this.ubicacionCambiada.emit(punto));
  }

  private icono(color: string, grande = false): L.DivIcon {
    const t = grande ? 44 : 34;
    return this.leaflet!.divIcon({
      className: '',
      iconSize: [t, t],
      iconAnchor: [t / 2, t],
      popupAnchor: [0, -t + 4],
      html: `<svg width="${t}" height="${t}" viewBox="0 0 24 24" style="filter:drop-shadow(0 3px 4px rgb(0 0 0 / .3))"><path d="M12 22s7-6.2 7-12a7 7 0 1 0-14 0c0 5.8 7 12 7 12Z" fill="${color}" stroke="#fff" stroke-width="1.5"/><circle cx="12" cy="10" r="2.6" fill="#fff"/></svg>`,
    });
  }

  private dibujarPuntos(puntos: PuntoMapa[]): void {
    const L = this.leaflet!;
    this.capaPuntos?.clearLayers();
    const limites: L.LatLngExpression[] = [];
    for (const p of puntos) {
      const marcador = L.marker([p.lat, p.lon], {
        icon: this.icono(COLOR_MODO[p.modo ?? ''] ?? '#1f7a4d'),
        title: p.titulo,
        keyboard: true,
      });
      // Contenido construido con textContent: los títulos son texto de usuarios (nunca innerHTML).
      const ficha = document.createElement('button');
      ficha.type = 'button';
      ficha.className = 'text-left';
      const titulo = document.createElement('strong');
      titulo.textContent = p.titulo;
      titulo.style.display = 'block';
      ficha.appendChild(titulo);
      if (p.detalle) {
        const detalle = document.createElement('span');
        detalle.textContent = p.detalle;
        detalle.style.cssText = 'font-size:12px;color:#56675d';
        ficha.appendChild(detalle);
      }
      const ver = document.createElement('span');
      ver.textContent = 'Ver publicación →';
      ver.style.cssText = 'display:block;margin-top:4px;font-size:12px;font-weight:600;color:#1f7a4d';
      ficha.appendChild(ver);
      ficha.addEventListener('click', () => this.zona.run(() => void this.router.navigate(['/publicacion', p.id])));
      marcador.bindPopup(ficha);
      marcador.addTo(this.capaPuntos!);
      limites.push([p.lat, p.lon]);
    }
    if (limites.length > 1 && !this.centro()) {
      this.mapa?.fitBounds(L.latLngBounds(limites), { padding: [40, 40], maxZoom: 15 });
    }
  }

  private dibujarSeleccion(u: [number, number] | null): void {
    const L = this.leaflet!;
    if (!u) {
      this.marcadorSeleccion?.remove();
      this.marcadorSeleccion = null;
      return;
    }
    if (!this.marcadorSeleccion) {
      this.marcadorSeleccion = L.marker(u, { icon: this.icono('#1f7a4d', true), draggable: true }).addTo(this.mapa!);
      this.marcadorSeleccion.on('dragend', () => {
        const ll = this.marcadorSeleccion!.getLatLng();
        this.elegir(ll.lat, ll.lng);
      });
    } else {
      this.marcadorSeleccion.setLatLng(u);
    }
    this.mapa?.panTo(u);
  }

  private dibujarArea(a: { lat: number; lon: number; radioM: number } | null): void {
    const L = this.leaflet!;
    this.circulo?.remove();
    this.circulo = null;
    if (!a) return;
    this.circulo = L.circle([a.lat, a.lon], {
      radius: a.radioM,
      color: '#1f7a4d',
      weight: 2,
      fillColor: '#47b37d',
      fillOpacity: 0.18,
    }).addTo(this.mapa!);
    this.mapa?.setView([a.lat, a.lon], 14);
  }
}

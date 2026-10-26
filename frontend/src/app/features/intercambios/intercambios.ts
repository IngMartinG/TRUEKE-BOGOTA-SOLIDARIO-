import { ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, input, signal, untracked } from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { filter, firstValueFrom, type Observable } from 'rxjs';
import type { PublicacionDto, SolicitudDto } from '../../api/tipos';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { CuentaApi } from '../../core/api/cuenta.api';
import { IntercambiosApi } from '../../core/api/intercambios.api';
import { AvisosService } from '../../core/avisos.service';
import { mensajeDe } from '../../core/http/problema';
import { SesionService } from '../../core/sesion.service';
import { TiempoRealService } from '../../core/tiempo-real.service';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Estrellas } from '../../shared/ui/estrellas';
import { Modal } from '../../shared/ui/modal';
import { Icono } from '../../shared/ui/icono';
import { SelectorObjetos } from '../../shared/ui/selector-objetos';
import { cop } from '../../shared/pipes';
import { TarjetaSolicitud, type AccionSolicitud } from './tarjeta-solicitud';
import { Volver } from '../../shared/ui/volver';

type Pestana = 'recibidas' | 'enviadas';
const TIPOS_SOLICITUD = /^(Solicitud|Entrega|Intercambio|Calificacion)/;

@Component({
  imports: [Volver, FormsModule, RouterLink, TarjetaSolicitud, EstadoVacio, Modal, Estrellas, Icono, SelectorObjetos],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="contenedor max-w-4xl py-8 sm:py-10">
      <app-volver respaldo="/" class="mb-1" />
      <h1 class="text-3xl font-extrabold">Mis intercambios</h1>
      <p class="mt-1 text-tenue">Responde solicitudes, coordina entregas y califica a tu comunidad.</p>

      <div class="mt-6 flex flex-wrap items-center justify-between gap-3">
        <div class="pestanas" role="group" aria-label="Solicitudes">
          <button type="button" class="pestana" [class.pestana-activa]="pestana() === 'recibidas'" [attr.aria-pressed]="pestana() === 'recibidas'" (click)="cambiar('recibidas')">
            Recibidas
            @if (pendientesRecibidas() > 0) {
              <span class="ml-1 rounded-full bg-tierra-600 px-1.5 text-[10px] text-white">{{ pendientesRecibidas() }}</span>
            }
          </button>
          <button type="button" class="pestana" [class.pestana-activa]="pestana() === 'enviadas'" [attr.aria-pressed]="pestana() === 'enviadas'" (click)="cambiar('enviadas')">
            Enviadas
          </button>
        </div>
        <div class="flex gap-1 text-sm" role="group" aria-label="Filtrar por estado">
          @for (f of filtros; track f.valor) {
            <button type="button" class="rounded-full px-3 py-1 font-medium transition" [class]="filtroEstado() === f.valor ? 'bg-superficie-2 text-tinta' : 'text-tenue hover:text-tinta'" [attr.aria-pressed]="filtroEstado() === f.valor" (click)="filtroEstado.set(f.valor)">
              {{ f.texto }}
            </button>
          }
        </div>
      </div>

      <div class="mt-6 space-y-4">
        @if (lista.isLoading() && !lista.value()) {
          @for (i of [1, 2]; track i) {
            <div class="esqueleto h-56 rounded-tarjeta"></div>
          }
        } @else if (lista.error()) {
          <app-estado-vacio icono="alerta" titulo="No pudimos cargar tus intercambios" descripcion="Revisa tu conexión e inténtalo de nuevo. Tus solicitudes siguen guardadas.">
            <button type="button" class="btn btn-primario" (click)="lista.reload()">Reintentar</button>
          </app-estado-vacio>
        } @else {
          @for (s of visibles(); track s.id) {
            <app-tarjeta-solicitud class="block animate-aparecer" [solicitud]="s" [ocupado]="ocupado() === s.id" (accion)="ejecutar(s, $event)" />
          } @empty {
            @if (lista.value()?.length) {
              <app-estado-vacio icono="checkCirculo" titulo="Nada pendiente por aquí" descripcion="No tienes intercambios en este estado.">
                <button type="button" class="btn btn-secundario" (click)="filtroEstado.set('todas')">Ver todos</button>
              </app-estado-vacio>
            } @else if (pestana() === 'recibidas') {
              <app-estado-vacio icono="apreton" titulo="Aún no recibes solicitudes" descripcion="Cuando alguien quiera tu objeto, lo verás aquí. Las publicaciones con buenas fotos reciben más solicitudes.">
                <a routerLink="/publicar" class="btn btn-primario">Publicar algo</a>
              </app-estado-vacio>
            } @else {
              <app-estado-vacio icono="brujula" titulo="No has enviado solicitudes" descripcion="Explora el catálogo y propone un trueke, compra o pide una donación.">
                <a routerLink="/explorar" class="btn btn-primario">Explorar el catálogo</a>
              </app-estado-vacio>
            }
          }
        }
      </div>
    </div>

    <!-- Diálogo con motivo (rechazar / no concretada) -->
    <app-modal [(abierto)]="motivoAbierto" [titulo]="accionMotivo() === 'rechazar' ? 'Rechazar solicitud' : 'El intercambio no se concretó'"
      [subtitulo]="accionMotivo() === 'rechazar' ? 'La publicación seguirá disponible para otras personas.' : 'La publicación volverá a estar disponible.'">
      <form id="form-motivo" (ngSubmit)="confirmarMotivo()" class="campo">
        <label for="motivo" class="etiqueta">Motivo {{ accionMotivo() === 'rechazar' ? '(opcional)' : '' }}</label>
        <textarea id="motivo" name="motivo" class="entrada" maxlength="300" [(ngModel)]="motivo"
          [placeholder]="accionMotivo() === 'rechazar' ? 'Ej: ya acordé con otra persona' : 'Ej: no llegamos a un acuerdo sobre el lugar'"></textarea>
      </form>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="motivoAbierto.set(false)">Volver</button>
        <button type="submit" form="form-motivo" class="btn btn-peligro" [disabled]="accionMotivo() === 'noConcretada' && motivo().trim().length < 3">Confirmar</button>
      </div>
    </app-modal>

    <!-- Confirmar antes de lo que no se puede deshacer o afecta a otras personas -->
    <app-modal [(abierto)]="confirmarAbierto" [titulo]="textoConfirmacion().titulo" [subtitulo]="textoConfirmacion().subtitulo">
      <p class="text-sm">{{ textoConfirmacion().cuerpo }}</p>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="confirmarAbierto.set(false)">Todavía no</button>
        <button type="button" class="btn btn-primario" (click)="confirmarAccion()">{{ textoConfirmacion().boton }}</button>
      </div>
    </app-modal>

    <!-- Contraofertar: otro precio (Compra) u otros objetos de quien solicita (Trueke); en una venta que acepta trueke, cualquiera -->
    <app-modal [(abierto)]="contraofertaAbierta" titulo="Hacer una contraoferta" [subtitulo]="subtituloContraoferta()">
      <form id="form-contraoferta" (ngSubmit)="enviarContraoferta()" class="space-y-4">
        @if (contraoferta()?.publicacionAceptaTrueke) {
          <fieldset>
            <legend class="etiqueta mb-2">¿Qué propones?</legend>
            <div class="grid grid-cols-1 gap-2 sm:grid-cols-2">
              @for (f of formasContraoferta; track f.modo) {
                <label class="flex cursor-pointer items-center gap-3 rounded-2xl border-2 p-3 transition has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-bosque-500"
                  [class]="modoContraoferta() === f.modo ? 'border-bosque-500 bg-bosque-50 dark:bg-bosque-900/50' : 'border-borde hover:border-bosque-200'">
                  <input type="radio" class="sr-only" name="forma-contraoferta" [value]="f.modo" [ngModel]="modoContraoferta()" (ngModelChange)="elegirModoContraoferta($event)" />
                  <app-icono [nombre]="f.icono" [tamano]="20" class="shrink-0 text-bosque-700 dark:text-bosque-300" />
                  <span class="text-sm font-semibold">{{ f.etiqueta }}</span>
                </label>
              }
            </div>
          </fieldset>
        }
        @if (modoContraoferta() === 'Compra') {
          <div class="campo">
            <label for="precio-contraoferta" class="etiqueta">Precio que propones</label>
            <div class="relative">
              <span class="pointer-events-none absolute inset-y-0 left-3 grid place-items-center text-tenue" aria-hidden="true">$</span>
              <input id="precio-contraoferta" name="precio" type="number" inputmode="numeric" min="1" step="1" required class="entrada pl-7"
                [(ngModel)]="precioContraoferta" aria-describedby="ayuda-precio-contraoferta" />
            </div>
            <p id="ayuda-precio-contraoferta" class="ayuda">{{ ayudaPrecio() }}</p>
          </div>
        } @else {
          <fieldset>
            <legend class="etiqueta">{{ contraoferta()?.soyDuenio ? '¿Qué objetos de ' + (contraoferta()?.solicitante?.nombre ?? 'esta persona') + ' prefieres?' : '¿Qué objetos ofreces ahora?' }}</legend>
            <p class="ayuda mb-2">Elige de 1 a 3. {{ contraoferta()?.soyDuenio ? 'Solo aparecen sus publicaciones disponibles.' : 'Si aceptan, quedan reservados.' }}</p>
            <app-selector-objetos [objetos]="objetosContraoferta()" [(seleccion)]="seleccionContraoferta"
              [prefijo]="contraoferta()?.soyDuenio ? 'Pedir' : 'Ofrecer'">
              <p class="text-sm text-tenue">{{ contraoferta()?.soyDuenio ? 'No tiene otros objetos disponibles. Puedes aceptar, rechazar o conversar por el chat.' : 'No tienes objetos disponibles para ofrecer.' }}</p>
            </app-selector-objetos>
          </fieldset>
        }
        <p class="text-xs text-tenue">
          La propuesta anterior se reemplaza y le toca responder a la otra persona.
          {{ (contraoferta()?.contraofertasRestantes ?? 0) === 1 ? 'Es la última contraoferta posible: después solo queda aceptar o retirarse.' : 'Quedan ' + contraoferta()?.contraofertasRestantes + ' contraofertas entre los dos.' }}
        </p>
        @if (errorContraoferta()) {
          <p class="error-campo" role="alert">{{ errorContraoferta() }}</p>
        }
      </form>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="contraofertaAbierta.set(false)">Volver</button>
        <button type="submit" form="form-contraoferta" class="btn btn-primario" [disabled]="!contraofertaValida() || ocupado()">
          <app-icono nombre="repeat" [tamano]="16" />Enviar contraoferta
        </button>
      </div>
    </app-modal>

    <!-- Calificar -->
    <app-modal [(abierto)]="calificarAbierto" titulo="¿Cómo te fue?" subtitulo="Tu calificación ayuda a que la comunidad sea más confiable.">
      <form id="form-calificar" (ngSubmit)="enviarCalificacion()" class="space-y-5">
        <div class="flex flex-col items-center gap-2">
          <app-estrellas [(valor)]="estrellas" [editable]="true" [tamano]="36" />
          <p class="text-sm font-semibold text-tenue">{{ textoEstrellas[estrellas()] }}</p>
        </div>
        <div class="campo">
          <label for="comentario-calificacion" class="etiqueta">Comentario (opcional)</label>
          <textarea id="comentario-calificacion" name="comentario" class="entrada" maxlength="500" [(ngModel)]="comentario" placeholder="¿Fue puntual? ¿El objeto era como en la publicación?"></textarea>
        </div>
      </form>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="calificarAbierto.set(false)">Después</button>
        <button type="submit" form="form-calificar" class="btn btn-sol" [disabled]="estrellas() === 0">Enviar calificación</button>
      </div>
    </app-modal>
  `,
})
export default class Intercambios {
  private readonly api = inject(IntercambiosApi);
  private readonly avisos = inject(AvisosService);
  private readonly router = inject(Router);
  private readonly sesion = inject(SesionService);

  readonly tab = input<string>();
  readonly calificar = input<string>();
  /** ?ver=<id> (aviso de contraoferta): el aviso no sabe si fue recibida o enviada; se busca en la otra pestaña si hace falta. */
  readonly ver = input<string>();
  protected readonly pestana = computed<Pestana>(() => (this.tab() === 'enviadas' ? 'enviadas' : 'recibidas'));
  protected readonly filtroEstado = signal<'activas' | 'todas' | 'cerradas'>('activas');
  protected readonly filtros = [
    { valor: 'activas' as const, texto: 'En curso' },
    { valor: 'cerradas' as const, texto: 'Cerradas' },
    { valor: 'todas' as const, texto: 'Todas' },
  ];
  protected readonly ocupado = signal<string | null>(null);

  protected readonly lista = rxResource({
    params: () => this.pestana(),
    stream: ({ params }) => (params === 'enviadas' ? this.api.enviadas() : this.api.recibidas()),
  });
  /** Solo hace falta pedirlas aparte en la pestaña "Enviadas" (para el contador de pendientes). */
  private readonly recibidas = rxResource({
    params: () => (this.pestana() === 'enviadas' ? true : undefined),
    stream: () => this.api.recibidas(),
  });
  protected readonly pendientesRecibidas = computed(() =>
    ((this.pestana() === 'recibidas' ? this.lista.value() : this.recibidas.value()) ?? []).filter((s) => s.estado === 'Pendiente').length,
  );

  protected readonly visibles = computed(() => {
    const todas = [...(this.lista.value() ?? [])].sort(
      (a, b) => Date.parse(b.fechaSolicitud ?? '') - Date.parse(a.fechaSolicitud ?? ''),
    );
    const activa = (s: SolicitudDto) => s.estado === 'Pendiente' || s.estado === 'Aceptada' || !!s.puedoCalificar;
    switch (this.filtroEstado()) {
      case 'activas':
        return todas.filter(activa);
      case 'cerradas':
        return todas.filter((s) => !activa(s));
      default:
        return todas;
    }
  });

  // Diálogos
  protected readonly motivoAbierto = signal(false);
  protected readonly accionMotivo = signal<'rechazar' | 'noConcretada'>('rechazar');
  protected readonly motivo = signal('');
  protected readonly calificarAbierto = signal(false);
  protected readonly estrellas = signal(0);
  protected readonly comentario = signal('');
  protected readonly textoEstrellas = ['Toca una estrella', 'Mala experiencia', 'Regular', 'Buena', 'Muy buena', '¡Excelente!'];
  protected readonly confirmarAbierto = signal(false);
  protected readonly accionConfirmar = signal<'aceptar' | 'confirmar'>('confirmar');
  private readonly solicitudConfirmar = signal<SolicitudDto | null>(null);
  protected readonly textoConfirmacion = computed(() => {
    const s = this.solicitudConfirmar();
    const objeto = s?.publicacionTitulo ?? 'el objeto';
    const otra = (s?.soyDuenio ? s.solicitante?.nombre : s?.propietario?.nombre) ?? 'la otra persona';
    if (this.accionConfirmar() === 'aceptar' && !s?.soyDuenio)
      return {
        titulo: '¿Aceptar la contraoferta?',
        subtitulo: `${objeto} · con ${otra}`,
        cuerpo: 'El acuerdo queda cerrado con esta propuesta y la publicación se reserva para ti. Coordinen la entrega por el chat.',
        boton: 'Aceptar contraoferta',
      };
    if (this.accionConfirmar() === 'aceptar')
      return {
        titulo: `¿Aceptar a ${otra}?`,
        subtitulo: objeto,
        cuerpo: 'La publicación se reserva para esta persona y sale del catálogo. Si hay más interesados, quedan en lista de espera por si no se concreta.',
        boton: 'Aceptar y reservar',
      };
    return {
      titulo: s?.soyDuenio ? '¿Ya entregaste el objeto?' : '¿Ya tienes el objeto en tus manos?',
      subtitulo: `${objeto} · con ${otra}`,
      cuerpo: 'Confirma solo cuando el intercambio ya haya ocurrido: esto no se puede deshacer. Cuando ambas partes confirmen, los dos reciben sus Eco-Puntos.',
      boton: 'Sí, confirmar entrega',
    };
  });
  private seleccionada: SolicitudDto | null = null;

  // Contraoferta
  private readonly catalogo = inject(CatalogoApi);
  private readonly cuenta = inject(CuentaApi);
  protected readonly contraofertaAbierta = signal(false);
  protected readonly contraoferta = signal<SolicitudDto | null>(null);
  protected readonly precioContraoferta = signal<number | null>(null);
  /** Publicaciones disponibles de quien solicita (null = cargando). */
  protected readonly objetosContraoferta = signal<PublicacionDto[] | null>(null);
  protected readonly seleccionContraoferta = signal<string[]>([]);
  protected readonly errorContraoferta = signal<string | null>(null);
  protected readonly subtituloContraoferta = computed(() => {
    const s = this.contraoferta();
    const otra = (s?.soyDuenio ? s.solicitante?.nombre : s?.propietario?.nombre) ?? 'la otra persona';
    return `${s?.publicacionTitulo ?? ''} · con ${otra}`;
  });
  /** Venta que acepta trueke: la contraoferta puede ser un precio o un trueke (empieza en el modo de la propuesta vigente). */
  protected readonly formasContraoferta = [
    { modo: 'Compra', etiqueta: 'Un precio', icono: 'bag' },
    { modo: 'Trueke', etiqueta: 'Objetos a cambio', icono: 'repeat' },
  ] as const;
  protected readonly formaContraoferta = signal<'Compra' | 'Trueke'>('Compra');
  protected readonly modoContraoferta = computed(() => {
    const s = this.contraoferta();
    return s?.publicacionAceptaTrueke ? this.formaContraoferta() : s?.modo;
  });
  protected readonly ayudaPrecio = computed(() => {
    const s = this.contraoferta();
    if (s?.modo !== 'Compra') return `Publicado en ${cop(s?.precioPublicadoCop)}. Se paga en persona al entregar.`;
    const actual = cop(s?.precioPropuestoCop ?? s?.precioPublicadoCop);
    const publicado = s?.precioPropuestoCop ? `; publicado en ${cop(s.precioPublicadoCop)}` : '';
    return `Propuesta actual: ${actual}${publicado}. Se paga en persona al entregar.`;
  });
  /** Repetir la propuesta vigente no vale; cambiar de precio a objetos (o al revés) sí. */
  protected readonly contraofertaValida = computed(() => {
    const s = this.contraoferta();
    if (this.modoContraoferta() === 'Compra') {
      const p = this.precioContraoferta();
      return !!p && p > 0 && (s?.modo !== 'Compra' || Math.round(p) !== (s.precioPropuestoCop ?? s.precioPublicadoCop));
    }
    const sel = this.seleccionContraoferta();
    const actual = s?.modo === 'Trueke' ? (s.ofrecidas ?? []).map((o) => o.id) : [];
    return sel.length > 0 && !(sel.length === actual.length && sel.every((id) => actual.includes(id)));
  });

  constructor() {
    const tiempoReal = inject(TiempoRealService);
    tiempoReal.notificacion$
      .pipe(
        filter((n) => TIPOS_SOLICITUD.test(n.tipo ?? '')),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe(() => this.recargar());
    tiempoReal.resincronizar$.pipe(takeUntilDestroyed()).subscribe(() => this.recargar());

    // ?calificar=<id> (desde la pantalla de logro): abre la calificación de ese intercambio. Si no está en esta
    // pestaña (la persona era quien solicitó o quien publicó), se busca una vez en la otra.
    let buscadoEnOtra = false;
    effect(() => {
      const id = this.calificar();
      const lista = this.lista.value();
      if (!id || !lista || this.lista.isLoading()) return;
      untracked(() => {
        const s = lista.find((x) => x.id === id);
        if (s?.puedoCalificar) {
          this.filtroEstado.set('todas');
          void this.router.navigate([], { queryParams: { calificar: null }, queryParamsHandling: 'merge', replaceUrl: true });
          this.ejecutar(s, 'calificar');
        } else if (!s && !buscadoEnOtra) {
          buscadoEnOtra = true;
          void this.router.navigate([], { queryParams: { tab: this.pestana() === 'enviadas' ? 'recibidas' : 'enviadas' }, queryParamsHandling: 'merge', replaceUrl: true });
        }
      });
    });
  }

  private verBuscadoEnOtra = false;
  private readonly irASolicitud = effect(() => {
    const id = this.ver();
    const lista = this.lista.value();
    if (!id || !lista || this.lista.isLoading()) return;
    untracked(() => {
      if (lista.some((x) => x.id === id)) {
        void this.router.navigate([], { queryParams: { ver: null }, queryParamsHandling: 'merge', replaceUrl: true });
      } else if (!this.verBuscadoEnOtra) {
        this.verBuscadoEnOtra = true;
        void this.router.navigate([], { queryParams: { tab: this.pestana() === 'enviadas' ? 'recibidas' : 'enviadas' }, queryParamsHandling: 'merge', replaceUrl: true });
      }
    });
  });

  protected cambiar(p: Pestana): void {
    void this.router.navigate([], { queryParams: { tab: p }, replaceUrl: true });
  }

  protected ejecutar(s: SolicitudDto, accion: AccionSolicitud): void {
    this.seleccionada = s;
    this.solicitudConfirmar.set(s);
    switch (accion) {
      case 'aceptar':
      case 'confirmar':
        this.accionConfirmar.set(accion);
        this.confirmarAbierto.set(true);
        break;
      case 'cancelar':
        void this.correr(s, () => this.api.cancelar(s.id!), 'Solicitud retirada');
        break;
      case 'contraofertar':
        this.abrirContraoferta(s);
        break;
      case 'rechazar':
      case 'noConcretada':
        this.accionMotivo.set(accion);
        this.motivo.set('');
        this.motivoAbierto.set(true);
        break;
      case 'calificar':
        this.estrellas.set(0);
        this.comentario.set('');
        this.calificarAbierto.set(true);
        break;
    }
  }

  private abrirContraoferta(s: SolicitudDto): void {
    this.contraoferta.set(s);
    this.errorContraoferta.set(null);
    this.precioContraoferta.set(null);
    this.seleccionContraoferta.set((s.ofrecidas ?? []).map((o) => o.id!));
    this.objetosContraoferta.set(null);
    this.formaContraoferta.set(s.modo === 'Trueke' ? 'Trueke' : 'Compra');
    this.contraofertaAbierta.set(true);
    if (s.modo === 'Trueke') void this.cargarObjetosContraoferta(s);
  }

  protected elegirModoContraoferta(modo: 'Compra' | 'Trueke'): void {
    this.formaContraoferta.set(modo);
    const s = this.contraoferta();
    if (modo === 'Trueke' && s && this.objetosContraoferta() === null) void this.cargarObjetosContraoferta(s);
  }

  /** Siempre objetos de quien solicita: el dueño elige cuáles prefiere; quien solicita, cuáles ofrece ahora. */
  private async cargarObjetosContraoferta(s: SolicitudDto): Promise<void> {
    try {
      const objetos = s.soyDuenio
        ? ((await firstValueFrom(this.cuenta.publicacionesDe(s.solicitante!.id!, 1, 50))).items ?? [])
        : await firstValueFrom(this.catalogo.mias());
      if (this.contraoferta() !== s) return; // se cerró o se abrió otra mientras cargaba
      this.objetosContraoferta.set(objetos.filter((o) => o.estado === 'Disponible' && !o.oculta && o.id !== s.publicacionId));
    } catch {
      this.objetosContraoferta.set([]);
    }
  }

  protected async enviarContraoferta(): Promise<void> {
    const s = this.contraoferta();
    if (!s?.id || !this.contraofertaValida()) return;
    const cuerpo = this.modoContraoferta() === 'Compra'
      ? { precioPropuestoCop: Math.round(this.precioContraoferta()!) }
      : { publicacionesOfrecidasIds: this.seleccionContraoferta() };
    this.ocupado.set(s.id);
    this.errorContraoferta.set(null);
    try {
      await firstValueFrom(this.api.contraofertar(s.id, cuerpo));
      this.contraofertaAbierta.set(false);
      const otra = (s.soyDuenio ? s.solicitante?.nombre : s.propietario?.nombre) ?? 'la otra persona';
      this.avisos.exito('Contraoferta enviada', `Ahora le toca responder a ${otra}. Te avisaremos.`);
      this.recargar();
    } catch (e) {
      this.errorContraoferta.set(mensajeDe(e));
    } finally {
      this.ocupado.set(null);
    }
  }

  protected async confirmarAccion(): Promise<void> {
    const s = this.seleccionada;
    this.confirmarAbierto.set(false);
    if (!s?.id) return;
    if (this.accionConfirmar() === 'aceptar' && !s.soyDuenio)
      await this.correr(s, () => this.api.aceptar(s.id!), '¡Contraoferta aceptada!', 'La publicación quedó reservada para ti. Coordinen la entrega por el chat.');
    else if (this.accionConfirmar() === 'aceptar')
      await this.correr(s, () => this.api.aceptar(s.id!), '¡Solicitud aceptada!', 'La publicación quedó reservada. Coordinen la entrega por el chat; si hay más interesados, quedan en lista de espera.');
    else await this.correr(s, () => this.api.confirmarEntrega(s.id!), 'Entrega confirmada', 'Cuando la otra persona también confirme, ambos recibirán Eco-Puntos.');
  }

  protected async confirmarMotivo(): Promise<void> {
    const s = this.seleccionada;
    if (!s?.id) return;
    const motivo = this.motivo().trim();
    this.motivoAbierto.set(false);
    if (this.accionMotivo() === 'rechazar') {
      await this.correr(s, () => this.api.rechazar(s.id!, motivo || null), 'Solicitud rechazada');
    } else {
      await this.correr(s, () => this.api.noConcretada(s.id!, motivo), 'Intercambio marcado como no concretado');
    }
  }

  protected async enviarCalificacion(): Promise<void> {
    const s = this.seleccionada;
    if (!s?.id || this.estrellas() === 0) return;
    this.calificarAbierto.set(false);
    await this.correr(s, () => this.api.calificar(s.id!, this.estrellas(), this.comentario().trim() || null), '¡Gracias por calificar!');
  }

  private async correr(
    s: SolicitudDto,
    llamada: () => Observable<unknown>,
    titulo: string,
    detalle?: string,
  ): Promise<void> {
    this.ocupado.set(s.id ?? null);
    try {
      await firstValueFrom(llamada());
      this.avisos.exito(titulo, detalle);
      this.recargar();
      this.sesion.recargarUsuario();
    } catch {
      // El interceptor ya mostró el error.
    } finally {
      this.ocupado.set(null);
    }
  }

  private recargar(): void {
    this.lista.reload();
    if (this.pestana() === 'enviadas') this.recibidas.reload();
  }
}

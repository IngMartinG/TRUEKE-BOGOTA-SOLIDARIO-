import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { catchError, firstValueFrom, of } from 'rxjs';
import type { MiFundacionDto, NecesidadDto } from '../../api/tipos';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { FundacionesApi } from '../../core/api/fundaciones.api';
import { AvisosService } from '../../core/avisos.service';
import { ConfigService } from '../../core/config.service';
import { mensajeDe } from '../../core/http/problema';
import { SesionService } from '../../core/sesion.service';
import { SubidasService } from '../../core/subidas.service';
import { FechaPipe } from '../../shared/pipes';
import { Icono } from '../../shared/ui/icono';
import { InsigniaFundacion } from '../../shared/ui/insignia-fundacion';
import { Modal } from '../../shared/ui/modal';

const MAX_NECESIDADES = 10;

/**
 * Perfil de fundación de la propia cuenta: pedir la verificación (NIT + certificado de la Cámara de Comercio, que solo ve un
 * SuperUsuario y se borra al decidir), ver en qué va y, ya verificada, contar "Lo que necesitamos".
 */
@Component({
  imports: [FormsModule, NgTemplateOutlet, RouterLink, Icono, InsigniaFundacion, Modal],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="text-2xl font-extrabold">Fundación</h1>
    <p class="mt-1 max-w-2xl text-tenue">
      Si tu cuenta representa a una entidad sin ánimo de lucro, verifícala gratis: tendrá la insignia
      <app-insignia-fundacion class="align-middle" />, una página pública y una lista de lo que necesitan.
    </p>

    @if (recurso.isLoading() && !recurso.value()) {
      <div class="esqueleto mt-6 h-40 w-full rounded-tarjeta"></div>
    } @else {
      @if (mia(); as f) {
        <!-- Estado de la solicitud -->
        <div class="mt-6 flex gap-3 rounded-2xl border p-4 text-sm" [class]="estado().clase" role="status">
          <app-icono [nombre]="estado().icono" [tamano]="20" class="mt-0.5 shrink-0" />
          <div class="min-w-0 space-y-1">
            <p class="font-semibold">{{ estado().titulo }}</p>
            <p class="text-tenue">{{ estado().texto }}</p>
            @if (f.motivo) {
              <p><span class="font-semibold">Motivo:</span> {{ f.motivo }}</p>
            }
            @if (f.estado === 'Aprobada') {
              <a [routerLink]="['/fundaciones', sesion.usuario()?.id]" class="enlace font-semibold">Ver la página de la fundación</a>
            }
          </div>
        </div>
      }

      @if (puedeSolicitar()) {
        <form class="tarjeta mt-6 space-y-5 p-5" (ngSubmit)="solicitar()" novalidate>
          <h2 class="text-lg font-bold">{{ mia() ? 'Enviar una solicitud nueva' : 'Pedir la verificación' }}</h2>
          <div class="grid grid-cols-1 gap-4 sm:grid-cols-[1fr_14rem]">
            <div class="campo">
              <label for="fun-razon" class="etiqueta">Razón social</label>
              <input id="fun-razon" name="razonSocial" class="entrada" maxlength="150" autocomplete="organization" required
                [(ngModel)]="razonSocial" placeholder="Como aparece en el certificado" />
            </div>
            <div class="campo">
              <label for="fun-nit" class="etiqueta">NIT</label>
              <input id="fun-nit" name="nit" class="entrada" maxlength="20" inputmode="numeric" autocomplete="off" spellcheck="false" required
                [(ngModel)]="nit" placeholder="900123456-8" aria-describedby="fun-nit-ayuda" />
              <p id="fun-nit-ayuda" class="ayuda">El de la entidad (empieza por 8 o 9).</p>
            </div>
          </div>
          <ng-container *ngTemplateOutlet="campoDescripcion" />

          <div class="campo">
            <span class="etiqueta" id="fun-cert-etiqueta">Certificado de existencia y representación legal</span>
            @if (config.subidasHabilitadas()) {
              <label class="flex cursor-pointer items-center gap-3 rounded-2xl border-2 border-dashed border-borde p-4 hover:border-bosque-300 focus-within:border-bosque-500">
                <app-icono [nombre]="certificadoUrl() ? 'check' : 'subir'" [class]="certificadoUrl() ? 'text-bosque-600' : 'text-tenue'" />
                <span class="text-sm">{{ subiendo() ? 'Subiendo…' : certificadoUrl() ? 'Certificado cargado. Toca para cambiarlo.' : 'Sube el PDF o una foto (máximo 5 MB)' }}</span>
                <input type="file" class="sr-only" accept="application/pdf,image/jpeg,image/png,image/webp" aria-labelledby="fun-cert-etiqueta"
                  (change)="subirCertificado($event)" />
              </label>
              <p class="ayuda">
                Expedido por la Cámara de Comercio hace menos de 30 días. Solo lo ve quien revisa la solicitud y se borra al decidir.
                <a routerLink="/privacidad" fragment="seccion-4" class="enlace" target="_blank">Cómo lo tratamos<span class="sr-only"> (abre otra pestaña)</span></a>
              </p>
            } @else {
              <p class="rounded-xl bg-sol-50 p-3 text-sm dark:bg-sol-500/10">La carga de documentos no está disponible en este momento.</p>
            }
          </div>

          <label class="flex items-start gap-3 text-sm">
            <input type="checkbox" name="declaro" class="mt-0.5 size-4 shrink-0 accent-bosque-600" [(ngModel)]="declaro" />
            <span>Represento legalmente a esta entidad o su representante legal me autorizó a crear su perfil, y los datos son ciertos.</span>
          </label>

          @if (error()) {
            <p class="rounded-xl bg-tierra-50 p-3 text-sm text-tierra-700 dark:bg-tierra-700/20 dark:text-tierra-100" role="alert">{{ error() }}</p>
          }
          <div class="flex justify-end">
            <button type="submit" class="btn btn-primario" [disabled]="enviando() || subiendo() || !certificadoUrl() || !declaro()">
              {{ enviando() ? 'Enviando…' : 'Enviar solicitud' }}
            </button>
          </div>
        </form>
      } @else if (mia(); as f) {
        <form class="tarjeta mt-6 space-y-5 p-5" (ngSubmit)="guardarPerfil()" novalidate>
          <div class="flex flex-wrap items-baseline justify-between gap-2">
            <h2 class="text-lg font-bold">{{ f.razonSocial }}</h2>
            <span class="text-sm text-tenue">NIT {{ f.nit }}</span>
          </div>
          <ng-container *ngTemplateOutlet="campoDescripcion" />
          @if (error()) {
            <p class="rounded-xl bg-tierra-50 p-3 text-sm text-tierra-700 dark:bg-tierra-700/20 dark:text-tierra-100" role="alert">{{ error() }}</p>
          }
          <div class="flex justify-end">
            <button type="submit" class="btn btn-secundario" [disabled]="enviando()">{{ enviando() ? 'Guardando…' : 'Guardar cambios' }}</button>
          </div>
        </form>
      }

      <ng-template #campoDescripcion>
        <div class="campo">
          <label for="fun-descripcion" class="etiqueta">¿Qué hacen y a quién atienden?</label>
          <textarea id="fun-descripcion" name="descripcion" class="entrada min-h-28" maxlength="600" required [(ngModel)]="descripcion"
            aria-describedby="fun-descripcion-ayuda" placeholder="Por ejemplo: acompañamos a 60 niños de Bosa con refuerzo escolar y almuerzo los sábados."></textarea>
          <p id="fun-descripcion-ayuda" class="ayuda flex justify-between gap-2">
            <span>Se muestra en la página pública. Entre 20 y 600 caracteres.</span><span class="tabular-nums">{{ descripcion().length }}/600</span>
          </p>
        </div>
        <label class="flex items-start gap-3 text-sm">
          <input type="checkbox" name="mostrarNit" class="mt-0.5 size-4 shrink-0 accent-bosque-600" [(ngModel)]="mostrarNit" />
          <span>Mostrar el NIT en la página pública (ayuda a que los donantes confíen).</span>
        </label>
      </ng-template>

      @if (mia()?.estado === 'Aprobada') {
        <section class="mt-10" aria-labelledby="titulo-necesito">
          <div class="flex items-baseline justify-between gap-3">
            <h2 id="titulo-necesito" class="text-xl font-bold">Lo que necesitamos</h2>
            <span class="text-sm text-tenue tabular-nums">{{ necesidades().length }} de {{ maximo }}</span>
          </div>
          <p class="mt-1 text-sm text-tenue">Bórrala cuando ya se cubra. Las urgentes salen primero.</p>

          @if (necesidades().length < maximo || editando()) {
            <form class="tarjeta mt-4 grid grid-cols-1 gap-4 p-4 sm:grid-cols-[1fr_12rem]" (ngSubmit)="guardarNecesidad()">
              <div class="campo sm:col-span-2">
                <label for="nec-texto" class="etiqueta">{{ editando() ? 'Editar necesidad' : '¿Qué necesitan?' }}</label>
                <input id="nec-texto" name="texto" class="entrada" maxlength="100" autocomplete="off" required [(ngModel)]="texto"
                  placeholder="Por ejemplo: cuadernos cuadriculados" />
              </div>
              <div class="campo">
                <label for="nec-categoria" class="etiqueta">Categoría (opcional)</label>
                <select id="nec-categoria" name="categoria" class="entrada" [(ngModel)]="categoriaId">
                  <option [ngValue]="null">Cualquiera</option>
                  @for (c of categorias(); track c.id) {
                    <option [ngValue]="c.id">{{ c.nombre }}</option>
                  }
                </select>
              </div>
              <div class="campo">
                <label for="nec-cantidad" class="etiqueta">Cantidad (opcional)</label>
                <input id="nec-cantidad" name="cantidad" type="number" inputmode="numeric" min="1" max="100000" class="entrada" [(ngModel)]="cantidad" />
              </div>
              <label class="flex items-center gap-3 text-sm sm:col-span-2">
                <input type="checkbox" name="urgente" class="size-4 accent-bosque-600" [(ngModel)]="urgente" />
                <span>Es urgente</span>
              </label>
              @if (errorNecesidad()) {
                <p class="text-sm text-tierra-700 sm:col-span-2 dark:text-tierra-400" role="alert">{{ errorNecesidad() }}</p>
              }
              <div class="flex justify-end gap-2 sm:col-span-2">
                @if (editando()) {
                  <button type="button" class="btn btn-fantasma btn-sm" (click)="limpiarNecesidad()">Cancelar</button>
                }
                <button type="submit" class="btn btn-primario btn-sm" [disabled]="guardandoNecesidad() || texto().trim().length < 3">
                  {{ editando() ? 'Guardar' : 'Agregar' }}
                </button>
              </div>
            </form>
          }

          <ul class="mt-4 space-y-2">
            @for (n of necesidades(); track n.id) {
              <li class="tarjeta flex flex-wrap items-center gap-3 p-4">
                <div class="min-w-0 flex-1">
                  <p class="font-semibold break-words">
                    @if (n.cantidad) {
                      <span class="tabular-nums">{{ n.cantidad }} ·</span>
                    }
                    {{ n.texto }}
                  </p>
                  <p class="mt-0.5 flex flex-wrap items-center gap-2 text-xs text-tenue">
                    @if (n.urgente) {
                      <span class="insignia-sol">Urgente</span>
                    }
                    {{ n.categoria ?? 'Cualquier categoría' }}
                  </p>
                </div>
                <button type="button" class="btn btn-fantasma btn-sm" (click)="editar(n)" [attr.aria-label]="'Editar ' + n.texto">
                  <app-icono nombre="editar" [tamano]="14" />Editar
                </button>
                <button type="button" class="btn btn-secundario btn-sm" (click)="cubierta(n)" [attr.aria-label]="'Ya se cubrió: ' + n.texto">
                  <app-icono nombre="check" [tamano]="14" />Ya se cubrió
                </button>
              </li>
            } @empty {
              <li class="rounded-tarjeta border-2 border-dashed border-borde p-6 text-center text-sm text-tenue">
                Aún no hay nada en la lista. Lo que agregues aparece en la página de la fundación.
              </li>
            }
          </ul>
        </section>
      }

      @if (mia(); as f) {
        <section class="mt-12 border-t border-borde pt-6">
          <h2 class="font-bold">{{ f.estado === 'Pendiente' ? 'Retirar la solicitud' : 'Dejar de ser fundación' }}</h2>
          <p class="mt-1 text-sm text-tenue">Se borran el perfil de fundación, la insignia y la lista de necesidades. Tu cuenta y tus publicaciones siguen igual.</p>
          <button type="button" class="btn btn-fantasma btn-sm mt-3 text-tierra-700 dark:text-tierra-400" (click)="confirmarAbierto.set(true)">
            {{ f.estado === 'Pendiente' ? 'Retirar la solicitud' : 'Dejar de ser fundación' }}
          </button>
        </section>
      }
    }

    <app-modal [(abierto)]="confirmarAbierto" titulo="¿Borrar el perfil de fundación?" subtitulo="Para volver a tenerlo hay que pedir otra verificación.">
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="confirmarAbierto.set(false)">Cancelar</button>
        <button type="button" class="btn btn-peligro" (click)="eliminar()">Borrar perfil de fundación</button>
      </div>
    </app-modal>
  `,
})
export default class FundacionCuenta {
  private readonly api = inject(FundacionesApi);
  private readonly avisos = inject(AvisosService);
  private readonly subidas = inject(SubidasService);
  protected readonly config = inject(ConfigService);
  protected readonly sesion = inject(SesionService);
  protected readonly maximo = MAX_NECESIDADES;

  protected readonly recurso = rxResource({ stream: () => this.api.mia() });
  protected readonly mia = computed<MiFundacionDto | null>(() => this.recurso.value() ?? null);
  protected readonly necesidades = computed(() => this.mia()?.necesidades ?? []);
  protected readonly categorias = toSignal(inject(CatalogoApi).categorias$.pipe(catchError(() => of([]))), { initialValue: [] });
  protected readonly puedeSolicitar = computed(() => {
    const e = this.mia()?.estado;
    return !e || e === 'Rechazada' || e === 'Retirada';
  });

  // Solicitud y perfil
  protected readonly razonSocial = signal('');
  protected readonly nit = signal('');
  protected readonly descripcion = signal('');
  protected readonly mostrarNit = signal(true);
  protected readonly declaro = signal(false);
  protected readonly certificadoUrl = signal<string | null>(null);
  protected readonly subiendo = signal(false);
  protected readonly enviando = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly confirmarAbierto = signal(false);

  // Necesidad en edición
  protected readonly editando = signal<string | null>(null);
  protected readonly texto = signal('');
  protected readonly categoriaId = signal<number | null>(null);
  protected readonly cantidad = signal<number | null>(null);
  protected readonly urgente = signal(false);
  protected readonly guardandoNecesidad = signal(false);
  protected readonly errorNecesidad = signal<string | null>(null);

  protected readonly estado = computed(() => {
    switch (this.mia()?.estado) {
      case 'Pendiente':
        return { titulo: 'Solicitud en revisión', texto: `La enviaste el ${this.fecha(this.mia()?.fechaSolicitudUtc)}. Te avisaremos cuando la revisemos.`,
          icono: 'reloj', clase: 'border-sol-300 bg-sol-50 dark:border-sol-500/40 dark:bg-sol-500/10' };
      case 'Aprobada':
        return { titulo: 'Fundación verificada', texto: 'Tu cuenta muestra la insignia y tiene página pública.',
          icono: 'heart', clase: 'border-bosque-200 bg-bosque-50 dark:border-bosque-800 dark:bg-bosque-950/50' };
      case 'Rechazada':
        return { titulo: 'No pudimos verificarla', texto: 'Corrige lo que indica el motivo y envía una solicitud nueva.',
          icono: 'alerta', clase: 'border-tierra-500/30 bg-tierra-50 dark:bg-tierra-700/15' };
      default:
        return { titulo: 'Verificación retirada', texto: 'La moderación retiró la insignia. Si ya se resolvió, envía una solicitud nueva.',
          icono: 'alerta', clase: 'border-tierra-500/30 bg-tierra-50 dark:bg-tierra-700/15' };
    }
  });

  private readonly fechaPipe = new FechaPipe();
  private fecha(f: string | null | undefined): string {
    return this.fechaPipe.transform(f);
  }

  constructor() {
    // Rellena el formulario con lo guardado (o lo de la solicitud anterior, para corregirla)
    effect(() => {
      const f = this.mia();
      untracked(() => {
        this.razonSocial.set(f?.razonSocial ?? this.sesion.usuario()?.nombreComercial ?? '');
        this.nit.set(f?.nit ?? '');
        this.descripcion.set(f?.descripcion ?? '');
        this.mostrarNit.set(f?.mostrarNit ?? true);
      });
    });
  }

  protected async subirCertificado(e: Event): Promise<void> {
    const input = e.target as HTMLInputElement;
    const archivo = input.files?.[0];
    input.value = '';
    if (!archivo) return;
    const invalido = this.subidas.validar(archivo, 'Documento');
    if (invalido) {
      this.error.set(invalido);
      return;
    }
    this.subiendo.set(true);
    this.error.set(null);
    try {
      this.certificadoUrl.set(await this.subidas.subirTodo(archivo, 'Documento'));
    } catch (err) {
      this.error.set(mensajeDe(err));
    } finally {
      this.subiendo.set(false);
    }
  }

  protected async solicitar(): Promise<void> {
    const certificadoUrl = this.certificadoUrl();
    if (!certificadoUrl || !this.declaro()) return;
    this.enviando.set(true);
    this.error.set(null);
    try {
      const f = await firstValueFrom(this.api.solicitar({
        razonSocial: this.razonSocial().trim(), nit: this.nit().trim(), descripcion: this.descripcion().trim(),
        mostrarNit: this.mostrarNit(), certificadoUrl, declaroRepresentacion: true,
      }));
      this.recurso.set(f);
      this.certificadoUrl.set(null);
      this.declaro.set(false);
      this.avisos.exito('Solicitud enviada', 'Te avisaremos cuando la revisemos.');
    } catch (err) {
      this.error.set(mensajeDe(err));
    } finally {
      this.enviando.set(false);
    }
  }

  protected async guardarPerfil(): Promise<void> {
    this.enviando.set(true);
    this.error.set(null);
    try {
      this.recurso.set(await firstValueFrom(this.api.editar({ descripcion: this.descripcion().trim(), mostrarNit: this.mostrarNit() })));
      this.avisos.exito('Cambios guardados');
    } catch (err) {
      this.error.set(mensajeDe(err));
    } finally {
      this.enviando.set(false);
    }
  }

  protected editar(n: NecesidadDto): void {
    this.editando.set(n.id ?? null);
    this.texto.set(n.texto ?? '');
    this.categoriaId.set(n.categoriaId ?? null);
    this.cantidad.set(n.cantidad ?? null);
    this.urgente.set(!!n.urgente);
    this.errorNecesidad.set(null);
    document.getElementById('nec-texto')?.focus();
  }

  protected limpiarNecesidad(): void {
    this.editando.set(null);
    this.texto.set('');
    this.categoriaId.set(null);
    this.cantidad.set(null);
    this.urgente.set(false);
    this.errorNecesidad.set(null);
  }

  protected async guardarNecesidad(): Promise<void> {
    const r = { texto: this.texto().trim(), categoriaId: this.categoriaId(), cantidad: this.cantidad() || null, urgente: this.urgente() };
    this.guardandoNecesidad.set(true);
    this.errorNecesidad.set(null);
    try {
      const id = this.editando();
      await firstValueFrom(id ? this.api.actualizarNecesidad(id, r) : this.api.crearNecesidad(r));
      this.limpiarNecesidad();
      this.recurso.reload();
    } catch (err) {
      this.errorNecesidad.set(mensajeDe(err));
    } finally {
      this.guardandoNecesidad.set(false);
    }
  }

  protected async cubierta(n: NecesidadDto): Promise<void> {
    if (!n.id) return;
    try {
      await firstValueFrom(this.api.eliminarNecesidad(n.id));
      if (this.editando() === n.id) this.limpiarNecesidad();
      this.avisos.exito('Quitada de la lista', `«${n.texto}» ya no aparece en la página.`);
      this.recurso.reload();
    } catch {
      // El interceptor ya mostró el error.
    }
  }

  protected async eliminar(): Promise<void> {
    try {
      await firstValueFrom(this.api.eliminar());
      this.confirmarAbierto.set(false);
      this.avisos.exito('Perfil de fundación borrado');
      this.recurso.reload();
    } catch {
      // El interceptor ya mostró el error.
    }
  }
}

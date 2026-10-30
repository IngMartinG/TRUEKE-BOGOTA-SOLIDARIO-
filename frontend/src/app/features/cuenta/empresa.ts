import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom, map } from 'rxjs';
import type { CampanaDto, InformeImpactoDto, InvitacionCreadaDto, MiEmpresaDto, MiembroEmpresaDto, RolMiembroEmpresaDto } from '../../api/tipos';
import { EmpresasApi } from '../../core/api/empresas.api';
import { AvisosService } from '../../core/avisos.service';
import { ConfigService } from '../../core/config.service';
import { mensajeDe } from '../../core/http/problema';
import { SubidasService } from '../../core/subidas.service';
import { descargar } from '../../shared/descargar';
import { FechaPipe, KilosPipe, NumeroPipe } from '../../shared/pipes';
import { Avatar } from '../../shared/ui/avatar';
import { Icono } from '../../shared/ui/icono';
import { Modal } from '../../shared/ui/modal';
import { dia, estadoCampana, hoyEnColombia } from '../../shared/dias';

/**
 * La empresa de la cuenta: registrarla (NIT + certificado de la Cámara de Comercio, que solo ve un SuperUsuario y se borra al
 * decidir), y ya verificada, el equipo con sus invitaciones, las campañas y los informes de impacto. Las invitaciones son un
 * enlace de un solo uso que el Admin comparte por su cuenta: la plataforma nunca busca personas por correo.
 */
@Component({
  imports: [FormsModule, RouterLink, Avatar, Icono, Modal, FechaPipe, KilosPipe, NumeroPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="text-2xl font-extrabold">Empresa</h1>
    <p class="mt-1 max-w-2xl text-tenue">
      Mide el impacto de tu empresa: tu equipo liga sus publicaciones a campañas y recibes informes con cifras trazables y
      la versión de la metodología. El servicio se acuerda por cotización: escríbenos desde
      <a routerLink="/cuenta/soporte" class="enlace">Soporte y PQR</a>.
    </p>

    @if (recurso.isLoading() && !recurso.value()) {
      <div class="esqueleto mt-6 h-40 w-full rounded-tarjeta"></div>
    } @else {
      @if (codigo() && !mia()) {
        <div class="tarjeta mt-6 flex flex-col gap-4 p-5 sm:flex-row sm:items-center" role="region" aria-labelledby="titulo-invitacion">
          <span class="grid size-11 shrink-0 place-items-center rounded-2xl bg-bosque-50 text-bosque-700 dark:bg-bosque-950 dark:text-bosque-300">
            <app-icono nombre="edificio" [tamano]="22" />
          </span>
          <div class="min-w-0 sm:flex-1">
            <h2 id="titulo-invitacion" class="font-bold">Te invitaron a una empresa</h2>
            <p class="text-sm text-tenue">Al unirte, tus compañeros verán tu nombre público y podrás ligar tus publicaciones a sus campañas.</p>
            @if (errorUnirse()) {
              <p class="mt-2 text-sm text-tierra-700 dark:text-tierra-400" role="alert">{{ errorUnirse() }}</p>
            }
          </div>
          <button type="button" class="btn btn-primario" [disabled]="uniendo()" (click)="unirse()">
            {{ uniendo() ? 'Uniéndote…' : 'Unirme a la empresa' }}
          </button>
        </div>
      } @else if (codigo() && mia()) {
        <p class="mt-6 rounded-xl bg-sol-50 p-3 text-sm dark:bg-sol-500/10" role="status">
          Abriste una invitación, pero ya perteneces a {{ mia()!.razonSocial }}. Sal de ella si quieres unirte a otra.
        </p>
      }

      @if (mia(); as e) {
        <div class="mt-6 flex gap-3 rounded-2xl border p-4 text-sm" [class]="estado().clase" role="status">
          <app-icono [nombre]="estado().icono" [tamano]="20" class="mt-0.5 shrink-0" />
          <div class="min-w-0 space-y-1">
            <p class="font-semibold">{{ e.razonSocial }} · {{ estado().titulo }}</p>
            <p class="text-tenue">{{ estado().texto }}</p>
            @if (e.motivo) {
              <p><span class="font-semibold">Motivo:</span> {{ e.motivo }}</p>
            }
            <p class="text-xs text-tenue">NIT <span class="tabular-nums">{{ e.nit }}</span> · tu rol: {{ e.miRol === 'Admin' ? 'Admin' : 'Miembro' }}</p>
          </div>
        </div>
      }

      @if (puedeSolicitar()) {
        <form class="tarjeta mt-6 space-y-5 p-5" (ngSubmit)="solicitar()" novalidate>
          <h2 class="text-lg font-bold">{{ mia() ? 'Enviar el registro de nuevo' : 'Registrar la empresa' }}</h2>
          <div class="grid grid-cols-1 gap-4 sm:grid-cols-[1fr_14rem]">
            <div class="campo">
              <label for="emp-razon" class="etiqueta">Razón social</label>
              <input id="emp-razon" name="razonSocial" class="entrada" maxlength="150" autocomplete="organization" required
                [(ngModel)]="razonSocial" placeholder="Como aparece en el certificado" />
            </div>
            <div class="campo">
              <label for="emp-nit" class="etiqueta">NIT</label>
              <input id="emp-nit" name="nit" class="entrada" maxlength="20" inputmode="numeric" autocomplete="off" spellcheck="false" required
                [(ngModel)]="nit" placeholder="900123456-8" aria-describedby="emp-nit-ayuda" />
              <p id="emp-nit-ayuda" class="ayuda">De la persona jurídica (empieza por 8 o 9).</p>
            </div>
          </div>
          <div class="campo">
            <span class="etiqueta" id="emp-cert-etiqueta">Certificado de existencia y representación legal</span>
            @if (config.subidasHabilitadas()) {
              <label class="flex cursor-pointer items-center gap-3 rounded-2xl border-2 border-dashed border-borde p-4 hover:border-bosque-300 focus-within:border-bosque-500">
                <app-icono [nombre]="certificadoUrl() ? 'check' : 'subir'" [class]="certificadoUrl() ? 'text-bosque-600' : 'text-tenue'" />
                <span class="text-sm">{{ subiendo() ? 'Subiendo…' : certificadoUrl() ? 'Certificado cargado. Toca para cambiarlo.' : 'Sube el PDF o una foto (máximo 5 MB)' }}</span>
                <input type="file" class="sr-only" accept="application/pdf,image/jpeg,image/png,image/webp" aria-labelledby="emp-cert-etiqueta"
                  (change)="subirCertificado($event)" />
              </label>
              <p class="ayuda">
                Expedido por la Cámara de Comercio hace menos de 30 días. Solo lo ve quien revisa el registro y se borra al decidir.
                <a routerLink="/privacidad" fragment="seccion-4" class="enlace" target="_blank">Cómo lo tratamos<span class="sr-only"> (abre otra pestaña)</span></a>
              </p>
            } @else {
              <p class="rounded-xl bg-sol-50 p-3 text-sm dark:bg-sol-500/10">La carga de documentos no está disponible en este momento.</p>
            }
          </div>
          <label class="flex items-start gap-3 text-sm">
            <input type="checkbox" name="declaro" class="mt-0.5 size-4 shrink-0 accent-bosque-600" [(ngModel)]="declaro" />
            <span>Represento legalmente a esta empresa o su representante legal me autorizó a registrarla, y los datos son ciertos.</span>
          </label>
          @if (error()) {
            <p class="rounded-xl bg-tierra-50 p-3 text-sm text-tierra-700 dark:bg-tierra-700/20 dark:text-tierra-100" role="alert">{{ error() }}</p>
          }
          <div class="flex justify-end">
            <button type="submit" class="btn btn-primario" [disabled]="enviando() || subiendo() || !certificadoUrl() || !declaro()">
              {{ enviando() ? 'Enviando…' : 'Enviar registro' }}
            </button>
          </div>
        </form>
      }

      @if (mia()?.estado === 'Aprobada') {
        <!-- Campañas -->
        <section class="mt-10" aria-labelledby="titulo-campanas">
          <h2 id="titulo-campanas" class="text-xl font-bold">Campañas</h2>
          <p class="mt-1 max-w-2xl text-sm text-tenue">
            Mientras una campaña está activa, cualquier miembro puede elegirla al publicar. Solo cuentan los objetos cuya entrega
            confirmaron las dos partes.
          </p>

          @if (esAdmin()) {
            <form class="tarjeta mt-4 grid grid-cols-1 gap-4 p-4 sm:grid-cols-[1fr_10rem_10rem]" (ngSubmit)="guardarCampana()">
              <div class="campo">
                <label for="camp-nombre" class="etiqueta">{{ editandoCampana() ? 'Editar campaña' : 'Nueva campaña' }}</label>
                <input id="camp-nombre" name="nombre" class="entrada" maxlength="100" autocomplete="off" required [(ngModel)]="nombreCampana"
                  placeholder="Por ejemplo: Donatón de uniformes" />
              </div>
              <div class="campo">
                <label for="camp-inicio" class="etiqueta">Empieza</label>
                <input id="camp-inicio" name="inicio" type="date" class="entrada" required [(ngModel)]="inicioCampana" />
              </div>
              <div class="campo">
                <label for="camp-fin" class="etiqueta">Termina</label>
                <input id="camp-fin" name="fin" type="date" class="entrada" required [min]="inicioCampana()" [(ngModel)]="finCampana" />
              </div>
              @if (errorCampana()) {
                <p class="text-sm text-tierra-700 sm:col-span-3 dark:text-tierra-400" role="alert">{{ errorCampana() }}</p>
              }
              <div class="flex justify-end gap-2 sm:col-span-3">
                @if (editandoCampana()) {
                  <button type="button" class="btn btn-fantasma btn-sm" (click)="limpiarCampana()">Cancelar</button>
                }
                <button type="submit" class="btn btn-primario btn-sm"
                  [disabled]="guardandoCampana() || nombreCampana().trim().length < 3 || !inicioCampana() || !finCampana()">
                  {{ editandoCampana() ? 'Guardar' : 'Crear campaña' }}
                </button>
              </div>
            </form>
          }

          <ul class="mt-4 space-y-2">
            @for (c of mia()!.campanas ?? []; track c.id) {
              <li class="tarjeta flex flex-wrap items-center gap-x-6 gap-y-3 p-4">
                <div class="min-w-0 flex-1">
                  <p class="flex flex-wrap items-center gap-2 font-semibold">
                    <span class="break-words">{{ c.nombre }}</span>
                    <span [class]="estadoCampana(c).clase">{{ estadoCampana(c).texto }}</span>
                  </p>
                  <p class="mt-0.5 text-xs text-tenue">Del {{ dia(c.inicio) }} al {{ dia(c.fin) }}</p>
                </div>
                <dl class="flex gap-5 text-sm">
                  <div><dt class="text-xs text-tenue">Publicaciones</dt><dd class="font-semibold tabular-nums">{{ c.publicaciones | numero }}</dd></div>
                  <div><dt class="text-xs text-tenue">Entregados</dt><dd class="font-semibold tabular-nums">{{ c.objetos | numero }}</dd></div>
                  <div><dt class="text-xs text-tenue">Reutilizado</dt><dd class="font-semibold tabular-nums">{{ c.kg | kilos }}</dd></div>
                  <div><dt class="text-xs text-tenue">CO₂e evitado</dt><dd class="font-semibold tabular-nums">≈ {{ c.kgCo2 | kilos }}</dd></div>
                </dl>
                @if (esAdmin()) {
                  <button type="button" class="btn btn-fantasma btn-sm" (click)="editarCampana(c)" [attr.aria-label]="'Editar ' + c.nombre">
                    <app-icono nombre="editar" [tamano]="14" />Editar
                  </button>
                }
              </li>
            } @empty {
              <li class="rounded-tarjeta border-2 border-dashed border-borde p-6 text-center text-sm text-tenue">
                {{ esAdmin() ? 'Crea la primera campaña para que tu equipo empiece a ligar sus publicaciones.' : 'Aún no hay campañas. Las crean los Admin de la empresa.' }}
              </li>
            }
          </ul>
        </section>

        <!-- Informes -->
        <section class="mt-10" aria-labelledby="titulo-informes">
          <h2 id="titulo-informes" class="text-xl font-bold">Informes de impacto</h2>
          <p class="mt-1 max-w-2xl text-sm text-tenue">
            Los emite el equipo de Trueke Bogotá Solidario. Cada informe guarda sus datos al emitirse: no cambia aunque después se
            actualice la metodología. El CSV trae las mismas entregas que el PDF.
          </p>
          <ul class="mt-4 space-y-2">
            @for (i of mia()!.informes ?? []; track i.id) {
              <li class="tarjeta flex flex-wrap items-center gap-3 p-4">
                <div class="min-w-0 flex-1">
                  <p class="font-semibold">Del {{ dia(i.desde) }} al {{ dia(i.hasta) }}</p>
                  <p class="mt-0.5 text-xs text-tenue">
                    {{ i.campana ?? 'Todas las campañas' }} · emitido el {{ i.emitidoUtc | fecha }} · metodología {{ i.versiones }}
                  </p>
                </div>
                <button type="button" class="btn btn-secundario btn-sm" [disabled]="descargando() === i.id + 'pdf'" (click)="bajar(i, 'pdf')">
                  <app-icono nombre="descargar" [tamano]="14" />PDF
                </button>
                <button type="button" class="btn btn-fantasma btn-sm" [disabled]="descargando() === i.id + 'csv'" (click)="bajar(i, 'csv')">
                  <app-icono nombre="descargar" [tamano]="14" />CSV
                </button>
              </li>
            } @empty {
              <li class="rounded-tarjeta border-2 border-dashed border-borde p-6 text-center text-sm text-tenue">
                Cuando el equipo de Trueke emita un informe, te avisaremos y aparecerá aquí.
              </li>
            }
          </ul>
        </section>

        <!-- Equipo -->
        <section class="mt-10" aria-labelledby="titulo-equipo">
          <div class="flex flex-wrap items-baseline justify-between gap-3">
            <h2 id="titulo-equipo" class="text-xl font-bold">Equipo</h2>
            <span class="text-sm text-tenue tabular-nums">{{ mia()!.miembros?.length ?? 0 }} de 50 personas</span>
          </div>
          <ul class="tarjeta mt-4 divide-y divide-borde">
            @for (m of mia()!.miembros ?? []; track m.id) {
              <li class="flex flex-wrap items-center gap-3 p-4">
                <app-avatar [nombre]="m.nombre" [foto]="m.fotoUrl" [tamano]="40" />
                <div class="min-w-0 flex-1">
                  <p class="truncate font-semibold">{{ m.nombre }}@if (m.soyYo) { <span class="font-normal text-tenue"> (tú)</span> }</p>
                  <p class="text-xs text-tenue">Desde el {{ m.fechaIngresoUtc | fecha }}</p>
                </div>
                @if (esAdmin() && !m.soyYo) {
                  <label class="sr-only" [attr.for]="'rol-' + m.id">Rol de {{ m.nombre }}</label>
                  <select [id]="'rol-' + m.id" class="entrada w-auto py-1.5 text-sm" [ngModel]="m.rol" (ngModelChange)="cambiarRol(m, $event)">
                    <option value="Miembro">Miembro</option>
                    <option value="Admin">Admin</option>
                  </select>
                  <button type="button" class="btn btn-fantasma btn-sm text-tierra-700 dark:text-tierra-400" (click)="quitar(m)"
                    [attr.aria-label]="'Quitar a ' + m.nombre + ' de la empresa'">Quitar</button>
                } @else {
                  <span [class]="m.rol === 'Admin' ? 'insignia-agua' : 'insignia-neutra'">{{ m.rol === 'Admin' ? 'Admin' : 'Miembro' }}</span>
                }
              </li>
            }
          </ul>

          @if (esAdmin()) {
            <div class="tarjeta mt-4 space-y-4 p-4">
              <div class="flex flex-wrap items-end gap-3">
                <div class="campo">
                  <label for="inv-rol" class="etiqueta">Invitar como</label>
                  <select id="inv-rol" name="rolInvitacion" class="entrada w-auto" [(ngModel)]="rolInvitacion">
                    <option value="Miembro">Miembro (liga sus publicaciones)</option>
                    <option value="Admin">Admin (gestiona equipo y campañas)</option>
                  </select>
                </div>
                <button type="button" class="btn btn-primario" [disabled]="invitando()" (click)="invitar()">
                  <app-icono nombre="mas" [tamano]="16" />Crear enlace de invitación
                </button>
              </div>
              @if (enlace(); as l) {
                <div class="rounded-2xl bg-bosque-50 p-4 dark:bg-bosque-950/50" role="status">
                  <p class="text-sm font-semibold">Comparte este enlace por tu cuenta (correo o chat interno). Sirve una vez y vence el {{ l.expira | fecha }}.</p>
                  <div class="mt-2 flex flex-wrap gap-2">
                    <input class="entrada min-w-0 flex-1 font-mono text-xs" readonly [value]="l.url" aria-label="Enlace de invitación" (focus)="$any($event.target).select()" />
                    <button type="button" class="btn btn-secundario btn-sm" (click)="copiar(l.url)"><app-icono nombre="copiar" [tamano]="14" />Copiar</button>
                  </div>
                  <p class="ayuda mt-2">Por seguridad no lo volveremos a mostrar: si se pierde, revócalo y crea otro.</p>
                </div>
              }
              @if (mia()!.invitaciones?.length) {
                <ul class="space-y-1 text-sm">
                  @for (i of mia()!.invitaciones; track i.id) {
                    <li class="flex flex-wrap items-center gap-2">
                      <app-icono nombre="correo" [tamano]="14" class="text-tenue" />
                      <span class="flex-1">
                        Invitación como {{ i.rol === 'Admin' ? 'Admin' : 'Miembro' }} ·
                        {{ i.vencida ? 'venció' : 'vence el ' + (i.expiraUtc | fecha) }}
                      </span>
                      <button type="button" class="btn btn-fantasma btn-sm" (click)="revocar(i.id!)">{{ i.vencida ? 'Borrar' : 'Revocar' }}</button>
                    </li>
                  }
                </ul>
              }
            </div>
          }
        </section>
      }

      @if (mia(); as e) {
        <section class="mt-12 border-t border-borde pt-6">
          <h2 class="font-bold">Salir de la empresa</h2>
          <p class="mt-1 text-sm text-tenue">
            Tu cuenta sigue igual. Lo que ya entregaste en sus campañas sigue en sus informes, sin tus datos.
            @if (e.miRol === 'Admin') { Si eres el único Admin, nombra a otra persona antes de salir. }
          </p>
          <button type="button" class="btn btn-fantasma btn-sm mt-3 text-tierra-700 dark:text-tierra-400" (click)="salirAbierto.set(true)">Salir de la empresa</button>
        </section>
      }
    }

    <app-modal [(abierto)]="salirAbierto" titulo="¿Salir de la empresa?" subtitulo="Para volver necesitarás una invitación nueva.">
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="salirAbierto.set(false)">Cancelar</button>
        <button type="button" class="btn btn-peligro" (click)="salir()">Salir de la empresa</button>
      </div>
    </app-modal>
  `,
})
export default class EmpresaCuenta {
  private readonly api = inject(EmpresasApi);
  private readonly avisos = inject(AvisosService);
  private readonly subidas = inject(SubidasService);
  private readonly router = inject(Router);
  protected readonly config = inject(ConfigService);
  protected readonly dia = dia;
  protected readonly estadoCampana = estadoCampana;

  protected readonly codigo = toSignal(inject(ActivatedRoute).queryParamMap.pipe(map((q) => q.get('codigo'))), { initialValue: null });
  protected readonly recurso = rxResource({ stream: () => this.api.mia() });
  protected readonly mia = computed<MiEmpresaDto | null>(() => this.recurso.value() ?? null);
  protected readonly esAdmin = computed(() => this.mia()?.miRol === 'Admin');
  protected readonly puedeSolicitar = computed(() => {
    const e = this.mia();
    if (!e) return !this.codigo();
    return e.miRol === 'Admin' && (e.estado === 'Rechazada' || e.estado === 'Retirada');
  });
  protected readonly estado = computed(() => {
    switch (this.mia()?.estado) {
      case 'Pendiente':
        return { titulo: 'en revisión', texto: 'Revisaremos el certificado y te avisaremos. Mientras tanto no se pueden crear campañas.',
          icono: 'reloj', clase: 'border-sol-300 bg-sol-50 dark:border-sol-500/40 dark:bg-sol-500/10' };
      case 'Aprobada':
        return { titulo: 'verificada', texto: 'Tu equipo ya puede ligar publicaciones a las campañas activas.',
          icono: 'edificio', clase: 'border-bosque-200 bg-bosque-50 dark:border-bosque-800 dark:bg-bosque-950/50' };
      case 'Rechazada':
        return { titulo: 'no pudimos verificarla', texto: 'Corrige lo que indica el motivo y envía el registro de nuevo.',
          icono: 'alerta', clase: 'border-tierra-500/30 bg-tierra-50 dark:bg-tierra-700/15' };
      default:
        return { titulo: 'verificación retirada', texto: 'Sus informes se conservan, pero no se pueden crear campañas ni invitar personas.',
          icono: 'alerta', clase: 'border-tierra-500/30 bg-tierra-50 dark:bg-tierra-700/15' };
    }
  });

  // Registro
  protected readonly razonSocial = signal('');
  protected readonly nit = signal('');
  protected readonly declaro = signal(false);
  protected readonly certificadoUrl = signal<string | null>(null);
  protected readonly subiendo = signal(false);
  protected readonly enviando = signal(false);
  protected readonly error = signal<string | null>(null);

  // Invitaciones
  protected readonly uniendo = signal(false);
  protected readonly errorUnirse = signal<string | null>(null);
  protected readonly rolInvitacion = signal<RolMiembroEmpresaDto>('Miembro');
  protected readonly invitando = signal(false);
  protected readonly enlace = signal<{ url: string; expira: string } | null>(null);

  // Campañas
  protected readonly editandoCampana = signal<string | null>(null);
  protected readonly nombreCampana = signal('');
  protected readonly inicioCampana = signal(hoyEnColombia());
  protected readonly finCampana = signal('');
  protected readonly guardandoCampana = signal(false);
  protected readonly errorCampana = signal<string | null>(null);

  protected readonly descargando = signal<string | null>(null);
  protected readonly salirAbierto = signal(false);

  constructor() {
    // Para corregir un registro rechazado se parte de lo enviado
    effect(() => {
      const e = this.mia();
      untracked(() => {
        this.razonSocial.set(e?.razonSocial ?? '');
        this.nit.set(e?.nit ?? '');
      });
    });
  }

  protected async subirCertificado(ev: Event): Promise<void> {
    const input = ev.target as HTMLInputElement;
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
      this.recurso.set(await firstValueFrom(this.api.solicitar({
        razonSocial: this.razonSocial().trim(), nit: this.nit().trim(), certificadoUrl, declaroRepresentacion: true,
      })));
      this.certificadoUrl.set(null);
      this.declaro.set(false);
      this.avisos.exito('Registro enviado', 'Te avisaremos cuando revisemos el certificado.');
    } catch (err) {
      this.error.set(mensajeDe(err));
    } finally {
      this.enviando.set(false);
    }
  }

  protected async unirse(): Promise<void> {
    const codigo = this.codigo();
    if (!codigo) return;
    this.uniendo.set(true);
    this.errorUnirse.set(null);
    try {
      const e = await firstValueFrom(this.api.unirse(codigo));
      this.recurso.set(e);
      void this.router.navigate([], { queryParams: { codigo: null }, replaceUrl: true });
      this.avisos.exito('¡Ya eres parte del equipo!', `Ya perteneces a ${e.razonSocial}.`);
    } catch (err) {
      this.errorUnirse.set(mensajeDe(err));
    } finally {
      this.uniendo.set(false);
    }
  }

  protected async invitar(): Promise<void> {
    this.invitando.set(true);
    try {
      const inv: InvitacionCreadaDto = await firstValueFrom(this.api.invitar(this.rolInvitacion()));
      const url = new URL(`cuenta/empresa?codigo=${encodeURIComponent(inv.codigo ?? '')}`, document.baseURI).toString();
      this.enlace.set({ url, expira: inv.expiraUtc ?? '' });
      this.recurso.reload();
    } catch {
      // El interceptor ya mostró el error.
    } finally {
      this.invitando.set(false);
    }
  }

  protected async copiar(texto: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(texto);
      this.avisos.exito('Enlace copiado');
    } catch {
      this.avisos.error('No se pudo copiar', 'Selecciona el enlace y cópialo a mano.');
    }
  }

  protected async revocar(id: string): Promise<void> {
    try {
      await firstValueFrom(this.api.revocarInvitacion(id));
      this.enlace.set(null);
      this.recurso.reload();
    } catch {
      // El interceptor ya mostró el error.
    }
  }

  protected async cambiarRol(m: MiembroEmpresaDto, rol: RolMiembroEmpresaDto): Promise<void> {
    try {
      this.recurso.set(await firstValueFrom(this.api.cambiarRol(m.id!, rol)));
      this.avisos.exito('Rol actualizado', `${m.nombre} ahora es ${rol === 'Admin' ? 'Admin' : 'Miembro'}.`);
    } catch {
      this.recurso.reload(); // vuelve al rol que tenía
    }
  }

  protected async quitar(m: MiembroEmpresaDto): Promise<void> {
    try {
      await firstValueFrom(this.api.quitarMiembro(m.id!));
      this.avisos.exito('Persona quitada', `${m.nombre} ya no pertenece a la empresa.`);
      this.recurso.reload();
    } catch {
      // El interceptor ya mostró el error.
    }
  }

  protected editarCampana(c: CampanaDto): void {
    this.editandoCampana.set(c.id ?? null);
    this.nombreCampana.set(c.nombre ?? '');
    this.inicioCampana.set(c.inicio ?? '');
    this.finCampana.set(c.fin ?? '');
    this.errorCampana.set(null);
    document.getElementById('camp-nombre')?.focus();
  }

  protected limpiarCampana(): void {
    this.editandoCampana.set(null);
    this.nombreCampana.set('');
    this.inicioCampana.set(hoyEnColombia());
    this.finCampana.set('');
    this.errorCampana.set(null);
  }

  protected async guardarCampana(): Promise<void> {
    const r = { nombre: this.nombreCampana().trim(), inicio: this.inicioCampana(), fin: this.finCampana() };
    this.guardandoCampana.set(true);
    this.errorCampana.set(null);
    try {
      const id = this.editandoCampana();
      await firstValueFrom(id ? this.api.editarCampana(id, r) : this.api.crearCampana(r));
      this.avisos.exito(id ? 'Campaña guardada' : 'Campaña creada', id ? undefined : 'Tu equipo ya puede elegirla al publicar.');
      this.limpiarCampana();
      this.recurso.reload();
    } catch (err) {
      this.errorCampana.set(mensajeDe(err));
    } finally {
      this.guardandoCampana.set(false);
    }
  }

  protected async bajar(i: InformeImpactoDto, tipo: 'pdf' | 'csv'): Promise<void> {
    if (!i.id) return;
    this.descargando.set(i.id + tipo);
    try {
      const blob = await firstValueFrom(tipo === 'pdf' ? this.api.informePdf(i.id) : this.api.informeCsv(i.id));
      descargar(blob, `informe-impacto-${i.desde}-a-${i.hasta}.${tipo}`);
    } catch {
      // El interceptor ya mostró el error.
    } finally {
      this.descargando.set(null);
    }
  }

  protected async salir(): Promise<void> {
    this.salirAbierto.set(false);
    try {
      await firstValueFrom(this.api.salir());
      this.avisos.exito('Saliste de la empresa');
      this.enlace.set(null);
      this.recurso.reload();
    } catch {
      // El interceptor ya mostró el error.
    }
  }
}

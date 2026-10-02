import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthApi } from '../../core/api/auth.api';
import { AvisosService } from '../../core/avisos.service';
import { erroresDeCampos, mensajeDe } from '../../core/http/problema';
import { SesionService } from '../../core/sesion.service';
import { descargar } from '../../shared/descargar';
import { CampoClave } from '../../shared/ui/campo-clave';
import { aplicarErroresServidor } from '../../shared/ui/error-campo';
import { Icono } from '../../shared/ui/icono';
import { Modal } from '../../shared/ui/modal';
import { claveSegura, coinciden } from '../../shared/validadores';

type Paso2fa = 'inicio' | 'escanear' | 'codigos';

@Component({
  imports: [ReactiveFormsModule, FormsModule, CampoClave, Icono, Modal],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="text-2xl font-extrabold">Seguridad</h1>
    <p class="mt-1 text-tenue">Protege tu cuenta y controla dónde tienes sesión abierta.</p>

    <!-- Contraseña -->
    <form [formGroup]="formClave" (ngSubmit)="cambiarClave()" class="tarjeta mt-6 space-y-5 p-6" novalidate>
      <div>
        <h2 class="text-lg font-bold">{{ tieneClave() ? 'Cambiar contraseña' : 'Crear una contraseña' }}</h2>
        @if (!tieneClave()) {
          <p class="mt-1 text-sm text-tenue">Ingresas con Google. Crea una contraseña si también quieres entrar con tu correo.</p>
        }
      </div>
      @if (tieneClave()) {
        <app-campo-clave [control]="formClave.controls.claveActual" etiqueta="Contraseña actual" idCampo="clave-actual" />
      }
      <div class="grid gap-5 sm:grid-cols-2">
        <app-campo-clave [control]="formClave.controls.claveNueva" etiqueta="Nueva contraseña" idCampo="clave-nueva" autocompletar="new-password" [mostrarRequisitos]="true" />
        <app-campo-clave [control]="formClave.controls.confirmacion" etiqueta="Repite la nueva" idCampo="clave-confirmacion" autocompletar="new-password" />
      </div>
      <div class="flex justify-end">
        <button type="submit" class="btn btn-primario" [disabled]="guardandoClave()">{{ guardandoClave() ? 'Guardando…' : 'Guardar contraseña' }}</button>
      </div>
    </form>

    <!-- 2FA -->
    <section class="tarjeta mt-6 p-6" aria-labelledby="titulo-2fa">
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div class="flex gap-4">
          <span class="grid size-12 shrink-0 place-items-center rounded-2xl" [class]="dosFactores() ? 'bg-bosque-600 text-white' : 'bg-superficie-2 text-tenue'">
            <app-icono nombre="escudo" [tamano]="24" />
          </span>
          <div>
            <h2 id="titulo-2fa" class="text-lg font-bold">Verificación en dos pasos</h2>
            <p class="mt-1 max-w-lg text-sm text-tenue">
              Además de tu contraseña, pediremos un código de tu app autenticadora (Google Authenticator, Microsoft Authenticator, Authy…).
              @if (sesion.esModerador()) {
                <strong class="text-tinta">Es obligatoria para usar las funciones de moderación.</strong>
              }
            </p>
            @if (dosFactores()) {
              <p class="mt-2 text-sm"><span class="insignia-trueke">Activa</span> · Te quedan {{ sesion.usuario()?.codigosRecuperacionRestantes ?? 0 }} códigos de recuperación.</p>
            }
          </div>
        </div>
        @if (!dosFactores() && paso2fa() === 'inicio') {
          <button type="button" class="btn btn-primario" (click)="configurar2fa()" [disabled]="trabajando()">Activar</button>
        }
      </div>

      @if (paso2fa() === 'escanear') {
        <div class="mt-6 grid gap-6 border-t border-borde pt-6 sm:grid-cols-[auto_1fr]">
          <div class="mx-auto rounded-2xl bg-white p-3 shadow-suave">
            @if (qr()) {
              <img [src]="qr()" alt="Código QR para tu app autenticadora" width="200" height="200" />
            } @else {
              <div class="esqueleto size-[200px]"></div>
            }
          </div>
          <form (ngSubmit)="activar2fa()" class="min-w-0 space-y-4">
            <ol class="list-decimal space-y-1 pl-5 text-sm">
              <li>Abre tu app autenticadora y escanea el código.</li>
              <li>Si no puedes escanear, escribe esta clave: <code class="rounded bg-superficie-2 px-1.5 py-0.5 font-mono text-xs break-all select-all">{{ secreto() }}</code></li>
              <li>Escribe el código de 6 dígitos que muestra la app.</li>
            </ol>
            <input name="codigo" class="entrada max-w-48 text-center font-mono text-xl tracking-[0.3em]" inputmode="numeric" maxlength="6" autocomplete="one-time-code"
              placeholder="000000" aria-label="Código de 6 dígitos" [(ngModel)]="codigo" />
            @if (error2fa()) {
              <p class="error-campo" role="alert">{{ error2fa() }}</p>
            }
            <div class="flex gap-2">
              <button type="submit" class="btn btn-primario" [disabled]="codigo().length < 6 || trabajando()">Confirmar y activar</button>
              <button type="button" class="btn btn-fantasma" (click)="paso2fa.set('inicio')">Cancelar</button>
            </div>
          </form>
        </div>
      }

      @if (dosFactores() && paso2fa() === 'inicio') {
        <div class="mt-5 flex flex-wrap gap-2 border-t border-borde pt-5">
          <button type="button" class="btn btn-secundario btn-sm" (click)="abrirCodigo('regenerar')"><app-icono nombre="llave" [tamano]="14" />Nuevos códigos de recuperación</button>
          <button type="button" class="btn btn-fantasma btn-sm text-tierra-600" (click)="abrirCodigo('desactivar')">Desactivar</button>
        </div>
      }
    </section>

    <!-- Sesiones -->
    <section class="tarjeta mt-6 flex flex-wrap items-center justify-between gap-4 p-6">
      <div>
        <h2 class="text-lg font-bold">Sesiones abiertas</h2>
        <p class="mt-1 text-sm text-tenue">¿Perdiste un celular o usaste un computador público? Cierra la sesión en todos los dispositivos.</p>
      </div>
      <button type="button" class="btn btn-peligro" (click)="cerrarAbierto.set(true)"><app-icono nombre="salir" [tamano]="16" />Cerrar todas</button>
    </section>

    <!-- Códigos de recuperación (una sola vez) -->
    <app-modal [(abierto)]="codigosAbierto" titulo="Guarda tus códigos de recuperación" subtitulo="Te sirven si pierdes el celular. Solo los mostraremos esta vez.">
      <ul class="grid grid-cols-2 gap-2 rounded-2xl bg-superficie-2 p-4 font-mono text-sm">
        @for (c of codigos(); track c) {
          <li class="rounded-lg bg-superficie px-3 py-2 text-center select-all">{{ c }}</li>
        }
      </ul>
      <p class="mt-3 flex items-start gap-2 text-sm text-tenue"><app-icono nombre="alerta" [tamano]="16" class="mt-0.5 shrink-0 text-sol-500" />Cada código sirve una sola vez. Guárdalos en un lugar seguro, fuera de este dispositivo.</p>
      <div pie class="flex flex-wrap justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-secundario" (click)="copiarCodigos()"><app-icono nombre="copiar" [tamano]="16" />Copiar</button>
        <button type="button" class="btn btn-secundario" (click)="descargarCodigos()"><app-icono nombre="descargar" [tamano]="16" />Descargar</button>
        <button type="button" class="btn btn-primario" (click)="codigosAbierto.set(false)">Ya los guardé</button>
      </div>
    </app-modal>

    <!-- Pedir código (desactivar / regenerar) -->
    <app-modal [(abierto)]="pedirCodigoAbierto" [titulo]="accionCodigo() === 'desactivar' ? 'Desactivar verificación en dos pasos' : 'Generar nuevos códigos'"
      subtitulo="Confirma con un código de tu app autenticadora.">
      <form id="form-codigo" (ngSubmit)="confirmarCodigo()" class="campo">
        <label for="codigo-confirmar" class="etiqueta">Código</label>
        <input id="codigo-confirmar" name="codigo" class="entrada text-center font-mono text-xl tracking-[0.3em]" maxlength="20" autocomplete="one-time-code" [(ngModel)]="codigo" />
        @if (error2fa()) {
          <p class="error-campo" role="alert">{{ error2fa() }}</p>
        }
      </form>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="pedirCodigoAbierto.set(false)">Cancelar</button>
        <button type="submit" form="form-codigo" class="btn" [class]="accionCodigo() === 'desactivar' ? 'btn-peligro' : 'btn-primario'" [disabled]="codigo().length < 6 || trabajando()">Confirmar</button>
      </div>
    </app-modal>

    <app-modal [(abierto)]="cerrarAbierto" titulo="¿Cerrar sesión en todos los dispositivos?" subtitulo="Tendrás que volver a ingresar, también en este.">
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="cerrarAbierto.set(false)">Cancelar</button>
        <button type="button" class="btn btn-peligro" (click)="cerrarTodas()" [disabled]="trabajando()">Cerrar todas</button>
      </div>
    </app-modal>
  `,
})
export default class Seguridad {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(AuthApi);
  private readonly avisos = inject(AvisosService);
  private readonly router = inject(Router);
  protected readonly sesion = inject(SesionService);

  protected readonly tieneClave = computed(() => this.sesion.usuario()?.tieneClave !== false);
  protected readonly dosFactores = computed(() => !!this.sesion.usuario()?.dosFactoresActivo);
  protected readonly guardandoClave = signal(false);
  protected readonly trabajando = signal(false);

  protected readonly formClave = this.fb.group(
    { claveActual: [''], claveNueva: ['', [Validators.required, claveSegura]], confirmacion: ['', Validators.required] },
    { validators: coinciden('claveNueva', 'confirmacion') },
  );

  protected readonly paso2fa = signal<Paso2fa>('inicio');
  protected readonly qr = signal<string | null>(null);
  protected readonly secreto = signal('');
  protected readonly codigo = signal('');
  protected readonly error2fa = signal<string | null>(null);
  protected readonly codigos = signal<string[]>([]);
  protected readonly codigosAbierto = signal(false);
  protected readonly pedirCodigoAbierto = signal(false);
  protected readonly accionCodigo = signal<'desactivar' | 'regenerar'>('desactivar');
  protected readonly cerrarAbierto = signal(false);

  protected async cambiarClave(): Promise<void> {
    const c = this.formClave.controls;
    if (this.tieneClave()) c.claveActual.setValidators(Validators.required);
    c.claveActual.updateValueAndValidity();
    this.formClave.markAllAsTouched();
    if (this.formClave.invalid) return;
    this.guardandoClave.set(true);
    try {
      const v = this.formClave.getRawValue();
      const sesion = await firstValueFrom(this.api.cambiarClave({ claveActual: v.claveActual || null, claveNueva: v.claveNueva }));
      this.sesion.establecer(sesion);
      this.formClave.reset();
      this.avisos.exito('Contraseña actualizada', 'Cerramos tus sesiones en otros dispositivos.');
    } catch (e) {
      if (!aplicarErroresServidor(c, erroresDeCampos(e))) this.avisos.error(mensajeDe(e));
    } finally {
      this.guardandoClave.set(false);
    }
  }

  protected async configurar2fa(): Promise<void> {
    this.trabajando.set(true);
    this.error2fa.set(null);
    this.codigo.set('');
    try {
      const conf = await firstValueFrom(this.api.configurar2fa());
      this.secreto.set(conf.secretoBase32 ?? '');
      this.paso2fa.set('escanear');
      const QRCode = (await import('qrcode')).default;
      this.qr.set(await QRCode.toDataURL(conf.uriOtpauth ?? '', { width: 200, margin: 1, color: { dark: '#123d27', light: '#ffffff' } }));
    } catch {
      // El interceptor ya mostró el error.
    } finally {
      this.trabajando.set(false);
    }
  }

  protected async activar2fa(): Promise<void> {
    this.trabajando.set(true);
    this.error2fa.set(null);
    try {
      const r = await firstValueFrom(this.api.activar2fa(this.codigo().trim()));
      if (r.sesion) this.sesion.establecer(r.sesion);
      this.codigos.set(r.codigosRecuperacion ?? []);
      this.paso2fa.set('inicio');
      this.qr.set(null);
      this.codigosAbierto.set(true);
      this.avisos.exito('Verificación en dos pasos activada');
    } catch (e) {
      this.error2fa.set(mensajeDe(e));
    } finally {
      this.trabajando.set(false);
    }
  }

  protected abrirCodigo(accion: 'desactivar' | 'regenerar'): void {
    this.accionCodigo.set(accion);
    this.codigo.set('');
    this.error2fa.set(null);
    this.pedirCodigoAbierto.set(true);
  }

  protected async confirmarCodigo(): Promise<void> {
    this.trabajando.set(true);
    this.error2fa.set(null);
    try {
      if (this.accionCodigo() === 'desactivar') {
        this.sesion.establecer(await firstValueFrom(this.api.desactivar2fa(this.codigo().trim())));
        this.avisos.exito('Verificación en dos pasos desactivada');
      } else {
        const r = await firstValueFrom(this.api.regenerarCodigos(this.codigo().trim()));
        this.codigos.set(r.codigosRecuperacion ?? []);
        this.codigosAbierto.set(true);
        this.sesion.recargarUsuario();
      }
      this.pedirCodigoAbierto.set(false);
    } catch (e) {
      this.error2fa.set(mensajeDe(e));
    } finally {
      this.trabajando.set(false);
    }
  }

  protected async copiarCodigos(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.codigos().join('\n'));
      this.avisos.exito('Códigos copiados');
    } catch {
      this.avisos.error('No se pudieron copiar. Selecciónalos manualmente.');
    }
  }

  protected descargarCodigos(): void {
    const texto = `Trueke Bogotá Solidario — códigos de recuperación\nCada código sirve una sola vez.\n\n${this.codigos().join('\n')}\n`;
    descargar(new Blob([texto], { type: 'text/plain;charset=utf-8' }), 'trueke-codigos-recuperacion.txt');
  }

  protected async cerrarTodas(): Promise<void> {
    this.trabajando.set(true);
    try {
      await firstValueFrom(this.api.cerrarSesiones());
      this.sesion.limpiar();
      this.avisos.exito('Cerramos todas tus sesiones');
      await this.router.navigateByUrl('/ingresar');
    } catch {
      // El interceptor ya mostró el error.
    } finally {
      this.trabajando.set(false);
    }
  }
}

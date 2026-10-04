import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { CODIGO_BOGOTA, LOCALIDADES } from '../../api/tipos';
import { CuentaApi } from '../../core/api/cuenta.api';
import { AvisosService } from '../../core/avisos.service';
import { erroresDeCampos, mensajeDe } from '../../core/http/problema';
import { SesionService } from '../../core/sesion.service';
import { SubidasService } from '../../core/subidas.service';
import { comprimirImagen } from '../../shared/imagenes';
import { FechaPipe, NumeroPipe } from '../../shared/pipes';
import { Avatar } from '../../shared/ui/avatar';
import { aplicarErroresServidor, ErrorCampo } from '../../shared/ui/error-campo';
import { Icono } from '../../shared/ui/icono';
import { SelectorMunicipio } from '../../shared/ui/selector-municipio';

@Component({
  imports: [ReactiveFormsModule, RouterLink, ErrorCampo, Icono, NumeroPipe, FechaPipe, SelectorMunicipio, Avatar],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="text-2xl font-extrabold">Mi cuenta</h1>
    <p class="mt-1 text-tenue">Tu información básica y el estado de tu cuenta.</p>

    @if (sesion.usuario(); as u) {
      <section class="tarjeta mt-6 flex flex-col items-center gap-4 p-5 text-center sm:flex-row sm:text-left" aria-labelledby="titulo-foto">
        <div class="relative">
          <app-avatar [nombre]="u.nombreCompleto" [foto]="u.fotoUrl" [tamano]="96" />
          @if (subiendoFoto()) {
            <span class="absolute inset-0 grid place-items-center rounded-full bg-black/45 text-xs font-semibold text-white">Subiendo…</span>
          }
        </div>
        <div class="min-w-0 flex-1">
          <h2 id="titulo-foto" class="font-bold">Foto de perfil</h2>
          <p class="text-sm text-tenue">Ayuda a que la comunidad confíe en ti. Le quitamos la ubicación y los datos ocultos de la foto antes de publicarla.</p>
          <div class="mt-3 flex flex-wrap justify-center gap-2 sm:justify-start">
            <label class="btn btn-primario btn-sm cursor-pointer" [class.pointer-events-none]="subiendoFoto()" [class.opacity-60]="subiendoFoto()">
              <app-icono nombre="camara" [tamano]="16" />{{ u.fotoUrl ? 'Cambiar foto' : 'Subir foto' }}
              <input type="file" class="sr-only" accept="image/jpeg,image/png,image/webp" (change)="elegirFoto($event)" [disabled]="subiendoFoto()" />
            </label>
            @if (u.fotoUrl) {
              <button type="button" class="btn btn-fantasma btn-sm" (click)="quitarFoto()" [disabled]="subiendoFoto()">Quitar foto</button>
            }
          </div>
        </div>
      </section>

      <div class="mt-6 grid grid-cols-2 gap-3 sm:grid-cols-4">
        @for (d of [
          { t: 'Eco-Puntos', v: (u.saldoEcoPuntos | numero), i: 'moneda' },
          { t: 'Reputación', v: (u.reputacion ?? 0).toFixed(1), i: 'hoja' },
          { t: 'Truekes', v: u.truekesCompletados ?? 0, i: 'repeat' },
          { t: 'Donaciones', v: u.donacionesRealizadas ?? 0, i: 'heart' }
        ]; track d.t) {
          <div class="tarjeta p-4">
            <app-icono [nombre]="d.i" [tamano]="20" class="text-bosque-600 dark:text-bosque-300" />
            <p class="mt-2 font-display text-2xl font-extrabold">{{ d.v }}</p>
            <p class="text-xs text-tenue">{{ d.t }}</p>
          </div>
        }
      </div>

      <div class="mt-6 grid gap-6 xl:grid-cols-[1fr_20rem]">
        <div class="space-y-6">
          <form [formGroup]="form" (ngSubmit)="guardar()" class="tarjeta space-y-5 p-6" novalidate>
            <h2 class="text-lg font-bold">Datos personales</h2>
            <div class="campo">
              <label for="nombre" class="etiqueta">Nombre completo</label>
              <input id="nombre" class="entrada" formControlName="nombreCompleto" maxlength="120" autocomplete="name" aria-describedby="nombre-error" />
              <app-error-campo [control]="form.controls.nombreCompleto" etiqueta="El nombre" idError="nombre-error" />
            </div>
            <div>
              <app-selector-municipio id="perfil-ubicacion" formControlName="municipioCodigo" />
              <app-error-campo [control]="form.controls.municipioCodigo" etiqueta="El municipio" />
            </div>
            <div class="campo">
              @if (esBogota()) {
                <label for="localidad" class="etiqueta">Localidad</label>
                <select id="localidad" class="entrada" formControlName="localidad">
                  <option value="" disabled>Elige tu localidad</option>
                  @for (l of localidades; track l) {
                    <option [value]="l">{{ l }}</option>
                  }
                </select>
              } @else {
                <label for="localidad" class="etiqueta">Barrio o sector</label>
                <input id="localidad" class="entrada" formControlName="localidad" maxlength="60" />
              }
              <app-error-campo [control]="form.controls.localidad" etiqueta="La localidad" />
              <p class="ayuda">Tus publicaciones nuevas usan esta ubicación por defecto.</p>
            </div>
            <div class="campo">
              <span class="etiqueta">Correo</span>
              <p class="flex items-center gap-2 rounded-xl bg-superficie-2 px-4 py-2.5 text-sm">
                {{ u.correo }}
                @if (u.correoVerificado) {
                  <span class="insignia-trueke ml-auto"><app-icono nombre="check" [tamano]="12" />Verificado</span>
                } @else {
                  <a routerLink="/verifica-tu-correo" class="insignia-sol ml-auto">Sin verificar</a>
                }
              </p>
              <p class="ayuda">Nunca se muestra a otras personas.</p>
            </div>
            <div class="flex justify-end">
              <button type="submit" class="btn btn-primario" [disabled]="form.pristine || guardando()">{{ guardando() ? 'Guardando…' : 'Guardar cambios' }}</button>
            </div>
          </form>

          @if (u.tipoCuenta === 'Empresa') {
            <form [formGroup]="empresa" (ngSubmit)="guardarEmpresa()" class="tarjeta space-y-5 p-6" novalidate>
              <div>
                <h2 class="flex items-center gap-2 text-lg font-bold"><app-icono nombre="edificio" [tamano]="20" />Perfil de empresa</h2>
                <p class="mt-1 text-sm text-tenue">Tu nombre comercial aparece en tus publicaciones y en tu perfil público mientras el plan Empresa esté vigente.</p>
              </div>
              <div class="campo">
                <label for="nombre-comercial" class="etiqueta">Nombre comercial</label>
                <input id="nombre-comercial" class="entrada" formControlName="nombreComercial" maxlength="120" autocomplete="organization" />
                <app-error-campo [control]="empresa.controls.nombreComercial" etiqueta="El nombre comercial" />
              </div>
              <div class="campo">
                <label for="nit" class="etiqueta">NIT</label>
                <input id="nit" class="entrada" formControlName="nit" maxlength="20" inputmode="numeric" placeholder="900123456 o 900.123.456-8" />
                <p class="ayuda">Si no escribes el dígito de verificación, lo calculamos.</p>
                <app-error-campo [control]="empresa.controls.nit" etiqueta="El NIT" />
              </div>
              <div class="flex justify-end">
                <button type="submit" class="btn btn-primario" [disabled]="empresa.pristine || guardando()">Guardar perfil de empresa</button>
              </div>
            </form>
          }
        </div>

        <div class="space-y-4">
          <div class="tarjeta p-5">
            <h2 class="font-bold">Tipo de cuenta</h2>
            <p class="mt-2 flex items-center gap-2 font-display text-xl font-extrabold">
              <app-icono [nombre]="u.tipoCuenta === 'Individual' ? 'usuario' : u.tipoCuenta === 'Empresa' ? 'edificio' : 'corona'" [tamano]="20" class="text-sol-500" />{{ u.tipoCuenta }}
            </p>
            @if (u.planVigenteHasta) {
              <p class="text-sm text-tenue">Vigente hasta el {{ u.planVigenteHasta | fecha }}</p>
            }
            <a routerLink="/eco-puntos" class="btn btn-secundario btn-sm mt-4 w-full">{{ u.planVigenteHasta ? 'Renovar o ver planes' : 'Ver planes' }}</a>
          </div>
          <div class="tarjeta p-5">
            <h2 class="font-bold">Verificación de identidad</h2>
            <p class="mt-2 text-sm text-tenue">
              @switch (u.estadoVerificacion) {
                @case ('Aprobada') { Tu cuenta está verificada. ¡Gracias por generar confianza! }
                @case ('Pendiente') { Tu documento está en revisión. Te avisaremos pronto. }
                @case ('Rechazada') { La verificación fue rechazada. Puedes intentarlo de nuevo con un documento legible. }
                @default { Verifica tu identidad para mostrar la insignia, aparecer en el filtro de cuentas verificadas y vender más de 5 artículos a la vez. }
              }
            </p>
            @if (u.estadoVerificacion !== 'Aprobada' && u.estadoVerificacion !== 'Pendiente') {
              <a routerLink="/eco-puntos" class="btn btn-primario btn-sm mt-4 w-full"><app-icono nombre="verificado" [tamano]="14" />Verificar mi cuenta</a>
            }
          </div>
        </div>
      </div>
    }
  `,
})
export default class Perfil {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(CuentaApi);
  private readonly avisos = inject(AvisosService);
  protected readonly sesion = inject(SesionService);
  protected readonly localidades = LOCALIDADES;
  protected readonly guardando = signal(false);
  protected readonly subiendoFoto = signal(false);
  private readonly subidas = inject(SubidasService);
  protected readonly form = this.fb.group({
    nombreCompleto: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(120)]],
    municipioCodigo: [CODIGO_BOGOTA, [Validators.required, Validators.pattern(/^\d{5}$/)]],
    localidad: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(60)]],
  });
  protected readonly empresa = this.fb.group({
    nombreComercial: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(120)]],
    nit: ['', [Validators.required, Validators.minLength(6), Validators.maxLength(20)]],
  });
  private readonly municipio = toSignal(this.form.controls.municipioCodigo.valueChanges, { initialValue: CODIGO_BOGOTA });
  protected readonly esBogota = computed(() => this.municipio() === CODIGO_BOGOTA);
  private municipioCargado = '';

  constructor() {
    // Refresca los datos al entrar (saldo y verificación pueden haber cambiado).
    this.sesion.recargarUsuario();
    effect(() => {
      const u = this.sesion.usuario();
      untracked(() => {
        if (u && this.form.pristine) {
          this.municipioCargado = u.municipioCodigo ?? CODIGO_BOGOTA;
          this.form.reset({ nombreCompleto: u.nombreCompleto ?? '', municipioCodigo: this.municipioCargado, localidad: u.localidad ?? '' });
        }
        if (u && this.empresa.pristine) this.empresa.reset({ nombreComercial: u.nombreComercial ?? '', nit: u.nit ?? '' });
      });
    });
    // Al elegir otro municipio, la localidad anterior deja de aplicar.
    effect(() => {
      const m = this.municipio();
      untracked(() => {
        if (this.municipioCargado && m !== this.municipioCargado) {
          this.form.controls.localidad.setValue('');
          this.municipioCargado = m;
        }
      });
    });
  }

  protected async guardar(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.guardando.set(true);
    try {
      const v = this.form.getRawValue();
      const u = await firstValueFrom(
        this.api.actualizarPerfil({ nombreCompleto: v.nombreCompleto.trim(), localidad: v.localidad.trim(), municipioCodigo: v.municipioCodigo }),
      );
      this.sesion.actualizarUsuario(u);
      this.form.markAsPristine();
      this.avisos.exito('Perfil actualizado');
    } catch (e) {
      if (!aplicarErroresServidor(this.form.controls, erroresDeCampos(e))) this.avisos.error(mensajeDe(e));
    } finally {
      this.guardando.set(false);
    }
  }

  protected async elegirFoto(e: Event): Promise<void> {
    const entrada = e.target as HTMLInputElement;
    const archivo = entrada.files?.[0];
    entrada.value = ''; // permite volver a elegir el mismo archivo
    if (!archivo) return;
    if (!/^image\/(jpeg|png|webp)$/.test(archivo.type)) {
      this.avisos.error('Formato no admitido', 'Usa una foto JPG, PNG o WebP.');
      return;
    }
    this.subiendoFoto.set(true);
    try {
      // Una foto de perfil se ve pequeña: 800 px bastan y la subida es rápida incluso con datos móviles.
      const url = await this.subidas.subirTodo(await comprimirImagen(archivo, 800), 'Imagen');
      this.sesion.actualizarUsuario(await firstValueFrom(this.api.cambiarFoto(url)));
      this.avisos.exito('Foto actualizada');
    } catch (err) {
      this.avisos.error(mensajeDe(err));
    } finally {
      this.subiendoFoto.set(false);
    }
  }

  protected async quitarFoto(): Promise<void> {
    this.subiendoFoto.set(true);
    try {
      this.sesion.actualizarUsuario(await firstValueFrom(this.api.quitarFoto()));
      this.avisos.exito('Foto eliminada');
    } catch {
      // El interceptor ya mostró el error.
    } finally {
      this.subiendoFoto.set(false);
    }
  }

  protected async guardarEmpresa(): Promise<void> {
    this.empresa.markAllAsTouched();
    if (this.empresa.invalid) return;
    this.guardando.set(true);
    try {
      const v = this.empresa.getRawValue();
      const u = await firstValueFrom(this.api.actualizarEmpresa({ nombreComercial: v.nombreComercial.trim(), nit: v.nit.trim() }));
      this.sesion.actualizarUsuario(u);
      this.empresa.reset({ nombreComercial: u.nombreComercial ?? '', nit: u.nit ?? '' });
      this.avisos.exito('Perfil de empresa actualizado', `NIT registrado: ${u.nit}`);
    } catch (e) {
      if (!aplicarErroresServidor(this.empresa.controls, erroresDeCampos(e))) this.avisos.error(mensajeDe(e));
    } finally {
      this.guardando.set(false);
    }
  }
}

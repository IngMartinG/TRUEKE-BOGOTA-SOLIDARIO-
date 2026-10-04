import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Icono } from '../shared/ui/icono';
import { Logo } from '../shared/ui/logo';

@Component({
  selector: 'app-pie',
  imports: [RouterLink, Logo, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <footer class="relative mt-24 overflow-hidden bg-bosque-950 text-bosque-100">
      <svg class="absolute -top-px left-0 w-full text-fondo" viewBox="0 0 1440 48" preserveAspectRatio="none" aria-hidden="true">
        <path d="M0 0h1440v16c-240 32-480 32-720 16S240 0 0 24Z" fill="currentColor" />
      </svg>
      <div class="contenedor grid grid-cols-1 gap-10 pt-20 pb-12 sm:grid-cols-2 lg:grid-cols-[1.4fr_1fr_1fr_1fr]">
        <div>
          <app-logo [claro]="true" />
          <p class="mt-4 max-w-sm text-sm text-bosque-200/80">
            La plataforma comunitaria de economía circular de Colombia, nacida en Bogotá. Cada objeto que circula es un residuo menos en el relleno.
          </p>
          <p class="mt-4 inline-flex items-center gap-2 rounded-full bg-white/5 px-3 py-1.5 text-xs text-bosque-200">
            <app-icono nombre="hoja" [tamano]="14" class="text-bosque-300" /> Hecho en Bogotá con energía verde
          </p>
        </div>
        @for (col of columnas; track col.titulo) {
          <nav [attr.aria-label]="col.titulo">
            <h2 class="mb-4 text-sm font-bold tracking-wide text-white">{{ col.titulo }}</h2>
            <ul class="space-y-2.5 text-sm">
              @for (e of col.enlaces; track e.ruta) {
                <li><a [routerLink]="e.ruta" class="text-bosque-200/80 transition hover:text-white">{{ e.texto }}</a></li>
              }
            </ul>
          </nav>
        }
      </div>
      <div class="border-t border-white/10">
        <!-- pb-24: espacio para la barra inferior de celular y tablet -->
        <div class="contenedor flex flex-col gap-2 py-6 pb-24 text-xs text-bosque-200/60 sm:flex-row sm:justify-between lg:pb-6">
          <p>© {{ anio }} Trueke Bogotá Solidario · Hecho en Colombia</p>
          <p>Tus datos se tratan conforme a la Ley 1581 de 2012.</p>
        </div>
      </div>
    </footer>
  `,
})
export class Pie {
  protected readonly anio = new Date().getFullYear();
  protected readonly columnas = [
    {
      titulo: 'Plataforma',
      enlaces: [
        { ruta: '/explorar', texto: 'Explorar el catálogo' },
        { ruta: '/publicar', texto: 'Publicar un objeto' },
        { ruta: '/como-funciona', texto: 'Cómo funciona' },
        { ruta: '/ayuda', texto: 'Centro de ayuda' },
        { ruta: '/eco-puntos', texto: 'Eco-Puntos y planes' },
      ],
    },
    {
      titulo: 'Tu cuenta',
      enlaces: [
        { ruta: '/intercambios', texto: 'Mis intercambios' },
        { ruta: '/mensajes', texto: 'Mensajes' },
        { ruta: '/favoritos', texto: 'Favoritos' },
        { ruta: '/cuenta/perfil', texto: 'Configuración' },
      ],
    },
    {
      titulo: 'Legal',
      enlaces: [
        { ruta: '/privacidad', texto: 'Tratamiento de datos' },
        { ruta: '/terminos', texto: 'Términos de uso' },
        { ruta: '/cuenta/privacidad', texto: 'Exportar o eliminar mis datos' },
      ],
    },
  ];
}

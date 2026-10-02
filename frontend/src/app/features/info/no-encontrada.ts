import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Icono } from '../../shared/ui/icono';

@Component({
  imports: [RouterLink, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="contenedor flex min-h-[60vh] flex-col items-center justify-center py-16 text-center">
      <p class="font-display text-8xl font-extrabold text-bosque-200 dark:text-bosque-800">404</p>
      <h1 class="mt-2 text-2xl font-bold">Esta página se fue a reciclar</h1>
      <p class="mt-2 max-w-md text-tenue">No encontramos lo que buscabas. Quizás el enlace cambió o la publicación ya no existe.</p>
      <div class="mt-8 flex flex-wrap justify-center gap-3">
        <a routerLink="/" class="btn btn-secundario"><app-icono nombre="inicio" [tamano]="16" />Inicio</a>
        <a routerLink="/explorar" class="btn btn-primario"><app-icono nombre="brujula" [tamano]="16" />Explorar el catálogo</a>
      </div>
    </div>
  `,
})
export default class NoEncontrada {}

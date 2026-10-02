import { inject, Injectable } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';

/** "Explorar · Trueke Bogotá Solidario" */
@Injectable()
export class TituloEstrategia extends TitleStrategy {
  private readonly title = inject(Title);

  override updateTitle(estado: RouterStateSnapshot): void {
    const titulo = this.buildTitle(estado);
    this.title.setTitle(titulo ? `${titulo} · Trueke Bogotá Solidario` : 'Trueke Bogotá Solidario · Intercambia, compra y dona');
  }
}

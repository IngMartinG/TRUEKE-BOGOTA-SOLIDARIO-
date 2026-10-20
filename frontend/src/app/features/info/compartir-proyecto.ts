import { inject, Injectable } from '@angular/core';
import { AvisosService } from '../../core/avisos.service';

/** Comparte el enlace de la plataforma: hoja de compartir del sistema o, si no existe o falla (no por cancelar), copia. */
@Injectable({ providedIn: 'root' })
export class CompartirProyecto {
  private readonly avisos = inject(AvisosService);

  async compartir(texto: string): Promise<void> {
    // Contra el <base href>: en GitHub Pages la app vive en una subcarpeta del dominio.
    const url = new URL('.', document.baseURI).href;
    if (typeof navigator.share === 'function') {
      try {
        await navigator.share({ title: 'Trueke Bogotá Solidario', text: texto, url });
        return;
      } catch (e) {
        if (e instanceof DOMException && e.name === 'AbortError') return;
      }
    }
    try {
      await navigator.clipboard.writeText(url);
      this.avisos.exito('¡Enlace copiado!', 'Pégalo en un chat o una red social.');
    } catch {
      this.avisos.info('No pudimos copiarlo solo', `Comparte esta dirección: ${url}`);
    }
  }
}

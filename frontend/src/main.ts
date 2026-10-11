import { bootstrapApplication } from '@angular/platform-browser';
import { App } from './app/app';
import { appConfig } from './app/app.config';
import { cargarEntorno } from './app/core/entorno';
import { cargadaEnMarcoAjeno, mostrarAvisoDeMarco } from './app/core/sin-marcos';

if (cargadaEnMarcoAjeno()) {
  // Dentro de un iframe de otro sitio (clickjacking): la app no arranca.
  mostrarAvisoDeMarco();
} else {
  // La URL de la API se lee antes de arrancar para que cualquier petición inicial ya la use.
  cargarEntorno()
    .then(() => bootstrapApplication(App, appConfig))
    .catch((err) => console.error(err));
}

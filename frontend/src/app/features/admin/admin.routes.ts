import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { SesionService } from '../../core/sesion.service';

/** Las finanzas son solo para el SuperUsuario (la API lo vuelve a exigir). */
const exigirSuperUsuario: CanActivateFn = () =>
  inject(SesionService).esSuperUsuario() || inject(Router).createUrlTree(['/admin']);

export default [
  {
    path: '',
    loadComponent: () => import('./panel'),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'denuncias' },
      { path: 'denuncias', title: 'Moderación · Denuncias', loadComponent: () => import('./denuncias') },
      { path: 'usuarios', title: 'Moderación · Usuarios', loadComponent: () => import('./usuarios') },
      { path: 'verificaciones', title: 'Moderación · Verificaciones', loadComponent: () => import('./verificaciones') },
      { path: 'pagos', title: 'Moderación · Pagos', loadComponent: () => import('./pagos') },
      { path: 'facturas', title: 'Administración · Facturas', loadComponent: () => import('./facturas') },
      { path: 'pqr', title: 'Administración · PQR', loadComponent: () => import('./pqr') },
      { path: 'ingresos', title: 'Administración · Ingresos', canActivate: [exigirSuperUsuario], loadComponent: () => import('./ingresos') },
    ],
  },
] satisfies Routes;

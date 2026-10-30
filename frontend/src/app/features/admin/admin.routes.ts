import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { SesionService } from '../../core/sesion.service';

/** Las finanzas y la revisión de fundaciones son solo para el SuperUsuario (la API lo vuelve a exigir). */
const exigirSuperUsuario: CanActivateFn = () =>
  inject(SesionService).esSuperUsuario() || inject(Router).createUrlTree(['/admin']);

export default [
  {
    path: '',
    loadComponent: () => import('./panel'),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'publicaciones' },
      { path: 'denuncias', title: 'Moderación · Denuncias', loadComponent: () => import('./denuncias') },
      { path: 'apelaciones', title: 'Moderación · Apelaciones', loadComponent: () => import('./apelaciones') },
      { path: 'usuarios', title: 'Moderación · Usuarios', loadComponent: () => import('./usuarios') },
      { path: 'publicaciones', title: 'Moderación · Publicaciones en revisión', loadComponent: () => import('./publicaciones-pendientes') },
      { path: 'fotos', title: 'Moderación · Fotos de perfil', loadComponent: () => import('./fotos') },
      { path: 'negocios', title: 'Moderación · Negocios', loadComponent: () => import('./negocios') },
      { path: 'empresas', title: 'Administración · Empresas e informes', loadComponent: () => import('./empresas') },
      { path: 'verificaciones', title: 'Moderación · Verificaciones', loadComponent: () => import('./verificaciones') },
      { path: 'pagos', title: 'Moderación · Pagos', loadComponent: () => import('./pagos') },
      { path: 'facturas', title: 'Administración · Facturas', loadComponent: () => import('./facturas') },
      { path: 'pqr', title: 'Administración · PQR', loadComponent: () => import('./pqr') },
      { path: 'fundaciones', title: 'Administración · Fundaciones', canActivate: [exigirSuperUsuario], loadComponent: () => import('./fundaciones') },
      { path: 'impacto', title: 'Administración · Factores de impacto', canActivate: [exigirSuperUsuario], loadComponent: () => import('./factores-impacto') },
      { path: 'ingresos', title: 'Administración · Ingresos', canActivate: [exigirSuperUsuario], loadComponent: () => import('./ingresos') },
    ],
  },
] satisfies Routes;

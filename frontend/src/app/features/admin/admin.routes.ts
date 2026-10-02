import { Routes } from '@angular/router';

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
    ],
  },
] satisfies Routes;

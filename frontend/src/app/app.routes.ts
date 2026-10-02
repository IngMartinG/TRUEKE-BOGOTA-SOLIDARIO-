import { Routes } from '@angular/router';
import { exigirCorreoVerificado, exigirModerador, exigirSesion, soloInvitado } from './core/guardas';

export const routes: Routes = [
  { path: '', title: '', loadComponent: () => import('./features/inicio/inicio') },
  { path: 'explorar', title: 'Explorar', loadComponent: () => import('./features/catalogo/catalogo') },
  { path: 'como-funciona', title: 'Cómo funciona', loadComponent: () => import('./features/info/como-funciona') },
  { path: 'publicacion/:id', title: 'Publicación', loadComponent: () => import('./features/publicacion/detalle') },
  {
    path: 'publicar',
    title: 'Publicar',
    canActivate: [exigirCorreoVerificado],
    loadComponent: () => import('./features/publicacion/publicar'),
  },
  {
    path: 'publicacion/:id/editar',
    title: 'Editar publicación',
    canActivate: [exigirCorreoVerificado],
    loadComponent: () => import('./features/publicacion/publicar'),
  },
  {
    path: 'mis-publicaciones',
    title: 'Mis publicaciones',
    canActivate: [exigirSesion],
    loadComponent: () => import('./features/publicacion/mis-publicaciones'),
  },
  {
    path: 'favoritos',
    title: 'Favoritos',
    canActivate: [exigirSesion],
    loadComponent: () => import('./features/catalogo/favoritos'),
  },
  {
    path: 'intercambios',
    title: 'Mis intercambios',
    canActivate: [exigirSesion],
    loadComponent: () => import('./features/intercambios/intercambios'),
  },
  {
    path: 'mensajes',
    title: 'Mensajes',
    canActivate: [exigirSesion],
    loadComponent: () => import('./features/mensajes/mensajes'),
  },
  {
    path: 'mensajes/:id',
    title: 'Mensajes',
    canActivate: [exigirSesion],
    loadComponent: () => import('./features/mensajes/mensajes'),
  },
  {
    path: 'notificaciones',
    title: 'Notificaciones',
    canActivate: [exigirSesion],
    loadComponent: () => import('./features/notificaciones/notificaciones'),
  },
  { path: 'eco-puntos', title: 'Eco-Puntos', loadComponent: () => import('./features/eco-puntos/eco-puntos') },
  {
    path: 'pagos/:referencia',
    title: 'Estado del pago',
    canActivate: [exigirSesion],
    loadComponent: () => import('./features/eco-puntos/estado-pago'),
  },
  { path: 'usuarios/:id', title: 'Perfil', loadComponent: () => import('./features/perfil/perfil-publico') },
  {
    path: 'cuenta',
    canActivate: [exigirSesion],
    loadComponent: () => import('./features/cuenta/cuenta'),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'perfil' },
      { path: 'perfil', title: 'Mi cuenta', loadComponent: () => import('./features/cuenta/perfil') },
      { path: 'seguridad', title: 'Seguridad', loadComponent: () => import('./features/cuenta/seguridad') },
      { path: 'privacidad', title: 'Privacidad y datos', loadComponent: () => import('./features/cuenta/privacidad') },
    ],
  },
  {
    path: 'admin',
    canActivate: [exigirModerador],
    loadChildren: () => import('./features/admin/admin.routes'),
  },

  // Autenticación
  { path: 'ingresar', title: 'Ingresar', canActivate: [soloInvitado], loadComponent: () => import('./features/auth/ingresar') },
  { path: 'registro', title: 'Crear cuenta', canActivate: [soloInvitado], loadComponent: () => import('./features/auth/registro') },
  { path: 'olvide-clave', title: 'Recuperar contraseña', loadComponent: () => import('./features/auth/olvide-clave') },
  { path: 'restablecer-clave', title: 'Nueva contraseña', loadComponent: () => import('./features/auth/restablecer-clave') },
  { path: 'verificar-correo', title: 'Verificar correo', loadComponent: () => import('./features/auth/verificar-correo') },
  {
    path: 'verifica-tu-correo',
    title: 'Verifica tu correo',
    canActivate: [exigirSesion],
    loadComponent: () => import('./features/auth/verifica-tu-correo'),
  },

  // Legales
  { path: 'privacidad', title: 'Política de tratamiento de datos', loadComponent: () => import('./features/info/privacidad') },
  { path: 'terminos', title: 'Términos de uso', loadComponent: () => import('./features/info/terminos') },

  { path: '**', title: 'Página no encontrada', loadComponent: () => import('./features/info/no-encontrada') },
];

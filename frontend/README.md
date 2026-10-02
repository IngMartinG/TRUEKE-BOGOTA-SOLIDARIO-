# Trueke Bogotá Solidario — Front-end (Angular)

Aplicación web de la plataforma comunitaria de economía circular de Bogotá: **Trueke, Compra y Donación**, con **Eco-Puntos**, chat en tiempo real y moderación. Consume la API de [`backend/`](../backend) según el contrato [`docs/openapi.json`](../docs/openapi.json).

## Stack

| Tema | Elección |
|---|---|
| Framework | Angular 22 (componentes standalone, **signals**, sin Zone.js, rutas diferidas) |
| Estilos | Tailwind CSS v4 + sistema de diseño propio en tonos ecológicos (`src/styles.css`), modo claro/oscuro |
| Accesibilidad | Angular CDK (menús), `<dialog>` nativo, foco visible, `aria-live`, reglas de accesibilidad de angular-eslint |
| Tiempo real | `@microsoft/signalr` (se descarga solo cuando hay sesión) |
| Mapas | Leaflet + OpenStreetMap (proveedor configurable) |
| Contrato | Tipos generados desde `docs/openapi.json` con `openapi-typescript` |
| Calidad | ESLint (angular-eslint), Vitest, `strictTemplates`, CI en GitHub Actions |
| Producción | nginx sin privilegios, CSP estricta, sin source maps, configuración en tiempo de ejecución |

## Desarrollo local

Requisitos: **Node 24 LTS** y **.NET 8 SDK** (para la API).

```bash
# 1) API (Swagger en https://localhost:7180/swagger, base en memoria, pagos y correos simulados)
dotnet run --project backend/TruekeBogotaSolidario.Presentacion

# 2) Front (http://localhost:4200, con proxy de /api y /hubs hacia la API)
cd frontend
npm ci
npm start
```

- Los **correos** (verificación, recuperación) se imprimen en la consola de la API: copia el enlace en el navegador.
- Los **pagos** usan la pasarela simulada: la página del pago muestra los botones "Aprobar / Rechazar".
- Las **fotos** requieren Azure Blob (o Azurite); sin él, la interfaz lo indica y permite publicar sin fotos.
- **Google** y **reCAPTCHA** se activan solos cuando la API publica `googleClientId` / `captchaClaveSitio` en `GET /configuracion`.
- Para probar la moderación en local, la configuración `api` de `.claude/launch.json` crea un SuperUsuario de prueba (`admin@trueke.test`, solo en la base en memoria de desarrollo).

### Scripts

| Comando | Qué hace |
|---|---|
| `npm start` | Servidor de desarrollo con proxy a la API |
| `npm run build` | Build de producción en `dist/trueke-web/browser` |
| `npm run lint` | ESLint (TypeScript + plantillas + accesibilidad) |
| `npm run test:ci` | Pruebas unitarias (Vitest) una sola vez |
| `npm run api` | Regenera `src/app/api/schema.d.ts` desde `docs/openapi.json` |
| `npm run verificar` | Lint + pruebas + build (lo mismo que el CI) |

Si la API cambia: en `backend/` ejecuta `ACTUALIZAR_OPENAPI=1 dotnet test --filter OpenApiTests` y luego `npm run api`. El CI falla si los tipos no coinciden con el contrato.

## Estructura

```
src/app/
├── api/            tipos generados (schema.d.ts) + etiquetas de la interfaz (tipos.ts)
├── core/           sesión, interceptores, configuración, tiempo real, subidas, guardas, terceros (Google, reCAPTCHA)
│   └── api/        un servicio por área: auth, catálogo, intercambios, cuenta, admin
├── layout/         encabezado, barra inferior móvil, pie
├── shared/         componentes de UI (tarjetas, modal, mapa, estrellas…), pipes, validadores
└── features/       páginas (una carpeta por área, todas con carga diferida)
    ├── inicio, catalogo, publicacion, intercambios, mensajes, notificaciones
    ├── eco-puntos, perfil, cuenta, auth, admin, info
```

## Cómo funciona por dentro

- **Sesión:** el JWT vive **solo en memoria**. El refresco va en una cookie HttpOnly (`Path=/api/v1/auth`) con la cabecera anti-CSRF `X-Trueke-Csrf: 1`. Ante un 401 se hace **un único** refresco compartido entre todas las peticiones (el backend detecta reuso de la cookie) y se reintenta. El token se renueva solo un minuto antes de vencer.
- **Errores:** todas las respuestas de error son ProblemDetails; `title` se muestra tal cual y `errors` se marca en cada campo del formulario. `codigo: "2fa_requerido"` pide el código en el login y `"2fa_requerido_admin"` lleva a activar la verificación en dos pasos.
- **Tiempo real:** SignalR en `/hubs/notificaciones` (eventos `notificacion` y `mensaje`). Al reconectar se recuperan los contadores y las pantallas abiertas recargan sus datos.
- **Fotos:** se reducen en el navegador (≤1600 px, WebP) y se **elimina el EXIF** (ubicación GPS) antes de subirlas directo a Blob con la URL SAS que entrega la API.
- **Privacidad:** el mapa público muestra solo una zona aproximada; los textos de usuarios se pintan siempre con interpolación de Angular (nunca `innerHTML`) y los enlaces de correo borran el token de la URL con `history.replaceState`.

## Producción

### Docker

```bash
docker build -t trueke-web frontend
docker run -p 8081:8080 -e API_UPSTREAM=http://api:8080 trueke-web
```

O todo el entorno (front + API + SQL Server + Redis): `cd backend && docker compose up --build` → http://localhost:8081.

La imagen sirve el build con **nginx sin root** (puerto 8080), fallback de SPA, caché de un año para los archivos con hash, `index.html` y `config.json` sin caché, y cabeceras de seguridad (CSP, HSTS, `nosniff`, `DENY`, `Permissions-Policy`). Por defecto hace de **proxy inverso** de `/api` y `/hubs`, así el front y la API comparten origen: sin CORS y con la cookie `SameSite=Strict` funcionando.

| Variable | Por defecto | Uso |
|---|---|---|
| `API_UPSTREAM` | `http://api:8080` | Dirección interna de la API para el proxy |
| `API_URL` | *(vacío)* | Solo si la API está en otro dominio (p. ej. `https://api.trueke.co`); se añade a la CSP con su variante `wss://` |
| `NGINX_RESOLVER` | `127.0.0.11` | DNS para resolver `API_UPSTREAM` (el de Docker); en Azure, `168.63.129.16` |
| `CSP_IMG_EXTRA` | *(vacío)* | Hosts extra para imágenes, p. ej. `https://tucuenta.blob.core.windows.net` |
| `CSP_CONNECT_EXTRA` | *(vacío)* | Hosts extra para conexiones, p. ej. el mismo Blob (subidas con SAS) |
| `MAPA_TESELAS` / `MAPA_ATRIBUCION` | OpenStreetMap | Proveedor de mapas para tráfico alto (OSM pide no abusar de sus servidores) |

### Lista de verificación antes de salir a producción

1. API con `Urls__Frontend` = URL https del front y, si van en dominios distintos, `Cors__Origenes__0` igual a esa URL.
2. `CSP_IMG_EXTRA` y `CSP_CONNECT_EXTRA` con el host de Azure Blob; regla CORS del Blob que permita `PUT` desde el dominio del front.
3. Google Client ID y claves de reCAPTCHA v3 cargados en la API (los lee el front desde `/configuracion`); autorizar el dominio del front en ambos paneles.
4. Wompi en producción: el front redirige al Web Checkout y vuelve a `/pagos/{referencia}`; el beneficio se aplica por el webhook del backend.
5. Revisar los textos legales (`features/info/privacidad.ts` y `terminos.ts`) con el responsable del tratamiento de datos.

## Accesibilidad y experiencia

- Diseño pensado primero para celular (barra inferior con botón de publicar) y luego escritorio.
- Contraste AA, foco visible, navegación completa con teclado, "Saltar al contenido", avisos anunciados a lectores de pantalla, `prefers-reduced-motion` respetado.
- Estados vacíos y de error que explican qué hacer, esqueletos de carga y actualizaciones optimistas (favoritos).

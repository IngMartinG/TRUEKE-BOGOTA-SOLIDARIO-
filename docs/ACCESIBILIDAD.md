# Accesibilidad

Reglas obligatorias para toda pantalla nueva o cambio de interfaz. La declaración pública está en `/accesibilidad`
(`frontend/src/app/features/info/accesibilidad.ts`).

## Base
- Contraste AA: 4,5:1 en texto normal y 3:1 en texto grande e íconos, en modo claro y oscuro.
- Todo se usa con teclado y el foco siempre se ve (`:focus-visible` en `frontend/src/styles.css`).
- Se mantiene el enlace "Saltar al contenido" (`app.ts`).
- HTML semántico: un `h1` por página, encabezados en orden, `button` para acciones y `a` para navegar.
- Botones de solo ícono con `aria-label`.

## Formularios
- Todo campo tiene `label`; el placeholder nunca es la única etiqueta.
- Los errores se anuncian con `aria-live` y dicen qué hacer ("Escribe al menos 3 letras"), no solo qué falló.
- Los campos obligatorios se marcan con texto, no solo con color.

## Imágenes
- `alt` descriptivo en las fotos de contenido; `alt=""` en las decorativas.
- Fotos en WebP comprimido, `loading="lazy"` y dimensiones explícitas.

## Movimiento
- Toda animación respeta `prefers-reduced-motion`.
- Nada parpadea más de 3 veces por segundo.

## Táctil
- Objetivos de al menos 44 × 44 px (`min-h-11`).

## Pruebas antes de cerrar un cambio de interfaz
- [ ] Recorrido solo con teclado.
- [ ] Zoom al 200 % sin perder contenido ni desplazamiento horizontal.
- [ ] Modo claro y modo oscuro.
- [ ] Ancho de celular (375 px).
- [ ] Lector de pantalla en los cambios grandes.

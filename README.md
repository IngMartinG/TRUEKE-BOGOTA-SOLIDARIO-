# Etapa 3 — UI/UX & Prototipado Generativo con IA
**Proyecto:** Trueke Bogotá Solidario

## 1. Prompt utilizado en Figma AI

> "Genera un UI/UX y especificación de diseño para una plataforma de intercambio
> solidario de bienes en Bogotá con tres modos: Trueke, Compra y Donación. Incluye
> pantalla de catálogo con hero verde ecológico, badges de certificación (100% Sin
> Plástico, Eco-Circular, Eco-Puntos), tarjetas de producto con precio en Eco-Puntos,
> y una pantalla de detalle de producto con métricas de impacto ambiental (plástico
> evitado, CO₂ reducido, vida útil extendida)."

## 2. Resultado en ejecución

El prototipo generado (4 pantallas: catálogo en modo Trueke, catálogo en modo Compra,
catálogo en modo Donación y detalle de producto) fue exportado como PDF de
especificación oficial:

`Especificaciones_UIUX_Proyecto_pdf.pdf`

## 3. Tokens de diseño extraídos

A partir de las pantallas del PDF se abstrajeron las variables visuales a
`styles.css` (`:root`), evitando "hardcodear" colores dentro de los componentes:

| Token | Valor | Uso en el PDF |
|---|---|---|
| `--color-primary` | `#1f7a4d` | Botón "Solicitar Trueke", precios en Eco-Pts |
| `--color-primary-dark` | `#123d27` | Fondo del hero "Bogotá Circular" y navbar |
| `--color-accent` | `#0f8a86` | Badge "100% Sin Plástico" |
| `--color-info` | `#2f6fa3` | Badge "Eco-Puntos" |
| `--color-disabled` | `#9aa39c` | Estado "No disponible en este modo" |
| `--radius-card` | `14px` | Esquinas de las tarjetas de producto |
| `--radius-badge` | `999px` | Forma de píldora de los badges |

## 4. Componentes implementados

- `.navbar` — franja superior verde con buscador y contador de Eco-Puntos.
- `.hero` — sección "Bogotá Circular" con estadísticas de impacto.
- `.badge` (+ variantes) — certificaciones del producto.
- `.card-producto` — tarjeta reutilizable del catálogo, con estado `:hover` y
  botón `disabled` para "No disponible en este modo".
- `.btn-primary` / `.btn-secondary` — con estado `:focus-visible` accesible.

## 5. Archivos de esta entrega

```
/
├── Especificaciones_UIUX_Proyecto_pdf.pdf   ← spec oficial de diseño
├── styles.css                                ← tokens + componentes
├── index.html                                ← implementación semántica
└── README.md                                 ← este archivo
```

## 6. Pull Request

Enlace al PR: `[pegar aquí la URL una vez creado el PR — ver Paso 3]`

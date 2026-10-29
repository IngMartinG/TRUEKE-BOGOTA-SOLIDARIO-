# Metodología de cálculo de impacto

**Versión vigente: 2.0.0** (la que usa hoy `GET /api/v1/impacto`). Todo informe o cifra pública cita la versión con la que
se calculó; cada registro guarda la suya.

## Principios
1. **Solo cuenta lo confirmado.** Un objeto suma cuando el intercambio se completa (las dos partes confirman la entrega).
   Una solicitud aceptada, una entrega confirmada por una sola parte o un intercambio "no concretado" no suman. Un
   intercambio que se cierra solo porque una parte no confirmó a tiempo tampoco suma impacto, Eco-Puntos ni reputación.
2. **Kilos primero, CO₂ después.** Los kilos reutilizados son la métrica principal; el CO₂ evitado es una estimación
   derivada y siempre se presenta como tal ("≈").
3. **Conservador.** Ante la duda, el factor más bajo: es mejor quedarse corto que presumir.
4. **Versionado.** Cambiar un factor o la fórmula sube la versión y se anota en el historial de este documento.
5. **Auditable.** La tabla de factores se publica junto con las cifras (`factores` y `pesosPorCategoria` en la respuesta y
   la sección "Cómo lo calculamos" de `/impacto`).

## Versión 2.0.0 — por peso y material (vigente desde el 2026-10-29)
- Cuenta **cada objeto entregado**: lo publicado y, en un trueke con objetos, también lo que se ofreció a cambio.
- **Kilos:** el peso que declaró quien publicó (opcional, entre 0,05 y 500 kg; al publicar se puede elegir un objeto típico
  que lo llena) o, si no lo declaró, el típico de su categoría. El registro dice cuál fue (`Declarado` o
  `EstimadoPorCategoria`), y `/impacto` informa qué parte de los kilos fue declarada.
- **Material:** el declarado o, si falta, el típico de la categoría.
- **CO₂ evitado** = kilos × factor del material. Los factores viven en la tabla `FactoresImpacto` con su fuente y versión;
  solo el SuperUsuario los cambia (Administración → Impacto), con una fuente y una versión nueva, y queda en la auditoría.
- Cada objeto deja un **registro inmutable** (`RegistrosImpacto`): kg, origen del peso, material, factor, versión, modo,
  municipio, localidad y fecha. Cambiar un factor no reescribe la historia: el resultado es reproducible.
- `/impacto` filtra por periodo (mes, trimestre o año en curso, en hora de Colombia), modo y localidad. Solo se puede filtrar
  por una localidad con al menos 3 intercambios, y si con los filtros quedan menos de 3, las cifras se reservan.
- Los intercambios anteriores que ambas partes confirmaron se registraron al migrar, con el peso y el material típicos de su
  categoría (las publicaciones viejas no los declaraban).

Factores por material (kg CO₂e por kg reutilizado):

| Material | Factor |
|---|---|
| Textil | 8,0 |
| Electrónica | 12,0 |
| Electrodoméstico | 5,0 |
| Madera | 1,2 |
| Papel y cartón | 0,9 |
| Plástico | 3,0 |
| Vidrio | 0,7 |
| Metal | 4,0 |
| Varios materiales | 1,5 |

Peso y material típicos por categoría (si no se declaran): Ropa y calzado 0,5 kg textil · Libros y papelería 0,4 kg papel ·
Electrodomésticos 6 kg electrodoméstico · Hogar y muebles 8 kg varios · Tecnología 1 kg electrónica · Juguetes y niños 0,8 kg
plástico · Otros 1 kg varios. Están en `backend/TruekeBogotaSolidario.Negocio/Comun/ImpactoAmbiental.cs`, junto con los
objetos típicos de ayuda al publicar (kg): prenda 0,4, par de zapatos 0,8, libro 0,5, caja de 20 libros 10, nevera 55,
lavadora 60, microondas 12, ventilador 4, licuadora 3, plancha 1,5, colchón sencillo 18, silla de oficina 8, silla plástica 3,
mesa pequeña 12, sofá de dos puestos 35, juego de ollas 5, televisor de 32" 6, monitor 3,5, portátil 2,2, teclado 0,8, mouse
0,1, impresora 6, coche de bebé 9, bolsa de juguetes 4, bicicleta 14, bicicleta estática 25.

## Versión 1.0.0 — por categoría (reemplazada)
Cada intercambio completado cuenta **un objeto** (lo publicado) con el peso y el CO₂ de fabricación de un objeto típico de su
categoría. Una categoría sin factor usa la de "Otros". Los valores están en
`backend/TruekeBogotaSolidario.Negocio/Comun/ImpactoAmbiental.cs`.

| Categoría | kg por objeto | kg CO₂e por objeto |
|---|---|---|
| Ropa y calzado | 0,5 | 5 |
| Libros y papelería | 0,4 | 1 |
| Electrodomésticos | 6 | 30 |
| Hogar y muebles | 8 | 20 |
| Tecnología | 1 | 40 |
| Juguetes y niños | 0,8 | 3 |
| Otros | 1 | 2 |

Los factores son promedios recortados a la baja, del orden de los estudios de ciclo de vida de WRAP y la EPA.

**Redondeo:** kilos y CO₂ con un decimal en la API; en pantalla, "8,5 kg", "350 kg" o "1,2 t".

**Privacidad:** una localidad solo aparece en "Localidades más activas" con al menos 3 intercambios.

## Cómo se cita en un informe
> Los kilogramos reutilizados corresponden a objetos cuya entrega confirmaron ambas partes en Trueke Bogotá Solidario. La
> estimación de CO₂ usa la metodología interna versión X.Y.Z, con factores conservadores. Los valores son estimaciones y no
> constituyen una verificación de tercera parte.

## Qué no se afirma
- No se dice "certificado" ni "verificado" sin una verificación externa.
- No se cuentan objetos sin la doble confirmación.
- No se presentan estimaciones como mediciones.

## Referencias de alineación
Jerarquía de residuos (reutilizar antes que reciclar), Protocolo GHG y el estándar GRI 306 (residuos). Siguiente paso: buscar
la validación de un tercero (universidad o verificadora).

## Historial
- **1.0.0** (2026-10-24) — Inicial: un objeto por intercambio completado, factores por categoría.
- **2.0.0** (2026-10-29) — Peso y material por objeto, factores por material en la base de datos, registro inmutable por
  objeto (también los ofrecidos en un trueke), filtros en `/impacto`, y el cierre automático con una sola confirmación deja
  de contar.

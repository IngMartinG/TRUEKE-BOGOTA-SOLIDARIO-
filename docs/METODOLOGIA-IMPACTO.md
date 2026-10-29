# Metodología de cálculo de impacto

**Versión vigente: 1.0.0** (la que usa hoy `GET /api/v1/impacto`). Todo informe o cifra pública cita la versión con la que
se calculó.

## Principios
1. **Solo cuenta lo confirmado.** Un objeto suma cuando el intercambio se completa (las dos partes confirman la entrega).
   Una solicitud aceptada, una entrega confirmada por una sola parte o un intercambio "no concretado" no suman.
2. **Kilos primero, CO₂ después.** Los kilos reutilizados son la métrica principal; el CO₂ evitado es una estimación
   derivada y siempre se presenta como tal ("≈").
3. **Conservador.** Ante la duda, el factor más bajo: es mejor quedarse corto que presumir.
4. **Versionado.** Cambiar un factor o la fórmula sube la versión y se anota en el historial de este documento.
5. **Auditable.** La tabla de factores se publica junto con las cifras (`factores` en la respuesta y la sección
   "Cómo lo calculamos" de `/impacto`).

## Versión 1.0.0 — por categoría
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

## Versión 2.0.0 — por peso y material (en construcción)
Se publicará cuando esté en producción. Cambios previstos:
- Cada publicación puede declarar su **peso estimado** y su **material**. Si no lo declara, se usa el peso típico de su
  categoría y el registro queda marcado como "estimado por categoría".
- CO₂ = kg × factor del material. Los factores se guardan en la base de datos con su fuente y versión.
- Cada intercambio completado deja un **registro de impacto inmutable** (kg, origen del peso, material, factor, versión,
  localidad). Cambiar un factor no reescribe la historia.
- Filtros por localidad, periodo y modo en `/impacto`.

Factores por material propuestos (kg CO₂e por kg), a validar antes de publicarse:

| Material | Factor |
|---|---|
| Textil | 8,0 |
| Electrónica | 12,0 |
| Electrodoméstico | 5,0 |
| Madera | 1,2 |
| Papel | 0,9 |
| Plástico | 3,0 |
| Vidrio | 0,7 |
| Metal | 4,0 |
| Mixto | 1,5 |

Pesos típicos de ayuda al publicar (kg): bicicleta 14, bicicleta estática 25, colchón sencillo 18, silla de oficina 8, silla
plástica 3, mesa pequeña 12, sofá de dos puestos 35, nevera 55, lavadora 60, microondas 12, ventilador 4, licuadora 3,
plancha 1,5, juego de ollas 5, televisor de 32" 6, monitor 3,5, portátil 2,2, teclado 0,8, mouse 0,1, impresora 6, coche de
bebé 9, bulto de juguetes 4, libro 0,5, caja de 20 libros 10, prenda 0,4, par de zapatos 0,8.

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

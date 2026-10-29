# Glosario

Vocabulario del dominio. Entre paréntesis, el nombre en el código cuando es distinto.

## Plataforma
- **Trueke** — la marca y el modo de intercambio sin dinero. "Trueque" es la palabra común; en los textos de búsqueda se usan
  las dos.
- **Compra y venta** — modo con precio en pesos entre personas. La plataforma no recibe ese dinero.
- **Donación** — entrega gratuita.
- **Venta que también acepta trueke** — venta con la casilla "También acepto trueke" (`Publicacion.AceptaTrueke`).
- **Publicación** — objeto ofrecido, con modo, categoría, condición, localidad y hasta 5 fotos.
- **Condición** — estado del objeto: nuevo, como nuevo, usado, usado con detalles, reparado o para repuestos.
- **Solicitud** — interés de una persona en una publicación. Estados: pendiente, aceptada, rechazada, cancelada,
  completada o no concretada.
- **Contraoferta** — cambio de precio u objetos dentro de una solicitud, por turnos.
- **Entrega confirmada** — cada parte confirma por separado en la solicitud; con las dos, la solicitud se completa.
- **Transacción** — registro del intercambio completado (`Transaccion`).
- **Destacada** — publicación pagada que sale primero hasta `DestacadaHasta`.
- **Impulsar** — subir una publicación con 20 Eco-Puntos, una vez cada 24 horas.
- **Lo que busco** — lista de deseos con avisos de coincidencias (`Deseo`).

## Economía
- **Eco-Puntos** — moneda interna. Se ganan al completar intercambios y nunca se convierten a pesos.
- **Nivel** — Semilla, Brote, Árbol, Bosque y Guardián, según los Eco-Puntos acumulados.
- **Logro** — medalla de reconocimiento en el perfil.
- **Plan** — Individual (gratis), Premium o Empresa (`TipoCuenta`).

## Impacto
- **Impacto** — kilos reutilizados y CO₂ evitado por los intercambios completados.
- **Kg reutilizados** — la métrica principal.
- **Factor de impacto** — kilos y CO₂ por objeto o por kilo de material. Ver `docs/METODOLOGIA-IMPACTO.md`.
- **Versión de la metodología** — se cita en toda cifra pública o informe.

## Organizaciones
- **Fundación verificada** — entidad sin ánimo de lucro (NIT de ESAL) aprobada por el SuperUsuario. Usa la plataforma gratis.
- **Necesidad** — lo que pide una fundación en "Lo que necesitamos" (`NecesidadFundacion`).
- **Régimen Tributario Especial (RTE)** — calificación de la DIAN para ciertas ESAL. No equivale a que toda donación sea
  deducible.
- **Negocio** — tienda o persona con NIT que vende con un plan pago.
- **Empresa** — organización que contrata campañas e informes de impacto.
- **Campaña** — jornada de una empresa con fechas de inicio y fin.
- **Informe** — documento de impacto de un periodo o una campaña. No es un certificado.
- **JAC** — Junta de Acción Comunal.

## Roles y cuentas
- **Invitado** — visitante sin cuenta; nunca se guarda como rol.
- **Cliente** — cuenta normal.
- **Administrador** — modera y verifica (exige 2FA).
- **SuperUsuario** — la cuenta del dueño; además aprueba fundaciones y maneja pagos.
- **Cuenta completa** — correo verificado, localidad y foto de perfil.
- **Cuenta verificada** — revisión de identidad pagada (`EstadoVerificacion`).

## Geografía
- **Localidad** — división de Bogotá (o municipio, según DANE-DIVIPOLA).
- **Coordenadas aproximadas** — en público se redondean; las exactas solo las ve el dueño o quien tiene una solicitud
  aceptada.

## Confusiones frecuentes
- Trueke (marca) frente a trueque (palabra común).
- Informe frente a certificado: solo hay certificado con verificación externa.
- Verificar la cuenta (identidad) frente a verificar el correo.

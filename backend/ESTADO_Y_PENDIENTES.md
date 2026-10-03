# Estado del backend — Trueke Bogotá Solidario (2026-10-03)

## Hecho — backend completo para conectar el front
**Base:**
- arquitectura por capas;
- catálogo con geolocalización y comentarios;
- SignalR + Redis;
- migraciones;
- Docker;
- CI;
- contrato OpenAPI y ProblemDetails.

**Seguridad y cuentas:**
- refresh token rotativo en cookie HttpOnly;
- verificación de correo y recuperación de clave;
- login con Google;
- **2FA TOTP** obligatoria para administración;
- **reCAPTCHA v3**;
- Habeas Data (Ley 1581).

**Funcionalidad:**
- perfil público;
- editar publicaciones y hasta 5 fotos (Azure Blob con SAS);
- chat interno;
- **confirmación de entrega por ambas partes** con cierre automático (7 y 30 días);
- **calificaciones**;
- **favoritos**;
- orden y filtros;
- notificaciones persistentes;
- denuncias.

**Administración:**
- suspender o reactivar cuentas con jerarquía de roles;
- búsqueda de usuarios;
- pagos en revisión y registro de reembolsos;
- verificaciones y roles.

**Operación:**
- Application Insights (OpenTelemetry);
- correos HTML;
- tarea horaria de cierre y purga;
- pruebas contra SQL Server real (Testcontainers) obligatorias en el CI.

## Hecho el 2026-10-03 — escala nacional, monetización y seguridad
- **Colombia completa:** 33 departamentos y 1.122 municipios (DANE-DIVIPOLA), filtros por departamento/municipio, registro y perfil con municipio.
- **Estado del producto** obligatorio al publicar (nuevo, como nuevo, usado, usado con detalles, reparado, para repuestos) y filtro en el catálogo.
- **Privacidad de fotos:** se eliminan EXIF/GPS y metadatos al publicar.
- **Anti-farmeo:** correo canónico, correos desechables, bono al verificar, límite por pareja y calificaciones que no inflan el promedio.
- **Monetización:** impulsar con Eco-Puntos, plan Empresa con beneficios reales, estadísticas por publicación, vitrina de destacadas, recordatorio de vencimiento, límite de ventas sin identidad (Ley 1480 art. 53).
- **Facturación electrónica** (registro de cada venta, IVA, cola de emisión y CSV), **PQR** (retracto y reversión), **tablero de ingresos** y CSV contable.
- **Reembolsos que revierten el beneficio.**
- **Infra:** límite de peticiones distribuido (Redis), proxies confiables, CodeQL, Dependabot y ZAP.

**Estado de verificación:** 216 pruebas aprobadas y 6 de SQL Server que corren en el CI (incluye aplicar la migración nueva sobre datos existentes); `-warnaserror` limpio; sin paquetes vulnerables. Front: lint, 13 pruebas y build de producción en verde.

## Pendiente (fuera del código)
- **Subir la rama:** GitHub Desktop → Publish branch → Pull Request → CI en verde → Merge.
- **Instalar Docker en local:** activar la virtualización en la BIOS, `wsl --install` y Docker Desktop (para correr también las pruebas de SQL Server en tu PC).
- **Llaves externas:** Wompi, Google Client ID, reCAPTCHA, SMTP y Azure (SQL, Redis, Storage, App Service, Key Vault, Application Insights). Ver la sección 7.1 del README: dónde se obtiene cada una.
- **Contador / abogado:** confirmar el régimen de IVA (`Facturacion:ResponsableIva`), el tratamiento tributario de las recargas de Eco-Puntos, la resolución de facturación de la DIAN y el texto final de términos y política de datos.
- **Cuando haya contratos:** cobro recurrente con fuentes de pago de Wompi y emisión automática con la API de un proveedor de factura electrónica.

import type { LegalPublicoDto } from '../../api/tipos';

/** Texto que se muestra si la API aún no respondió o el dato no está configurado (fuera de producción). */
const PENDIENTE = '[dato pendiente de configurar]';

/** Datos del responsable del tratamiento y de las versiones vigentes, listos para insertar en los textos legales. */
export function datosResponsable(legal: LegalPublicoDto | null | undefined) {
  const nombre = legal?.responsableNombre ?? PENDIENTE;
  return {
    nombre,
    /** "Nombre, identificado con 123" o solo el nombre si el documento no se configuró. */
    identificacion: legal?.responsableDocumento ? `${nombre}, identificado(a) con ${legal.responsableDocumento}` : nombre,
    domicilio: legal?.domicilio ?? PENDIENTE,
    correo: legal?.correoContacto ?? PENDIENTE,
    telefono: legal?.telefono ?? null,
    vigencia: legal?.fechaVigencia ?? PENDIENTE,
    versionPolitica: legal?.versionPoliticaDatos ?? PENDIENTE,
    versionTerminos: legal?.versionTerminos ?? PENDIENTE,
  };
}

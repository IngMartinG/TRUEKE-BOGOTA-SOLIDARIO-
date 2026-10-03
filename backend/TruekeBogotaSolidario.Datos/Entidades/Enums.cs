namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>
/// Invitado = visitante anónimo SIN cuenta. Nunca se persiste ni se asigna a un usuario real.
/// Jerarquía: SuperUsuario ⊃ Administrador ⊃ Cliente ⊃ Invitado.
/// </summary>
public enum RolUsuarioEnum { Invitado = 0, Cliente = 1, Administrador = 2, SuperUsuario = 3 }

public enum ModoTransaccion { Trueke = 1, Compra = 2, Donacion = 3 }

public enum EstadoPublicacionEnum { Disponible = 1, EnNegociacion = 2, Intercambiada = 3, Cancelada = 4 }

public enum TipoCuenta { Individual = 1, Premium = 2, Empresa = 3 }

public enum EstadoVerificacion { NoVerificado = 0, Pendiente = 1, Aprobada = 2, Rechazada = 3 }

/// <summary>Aceptada = acordado, en coordinación de la entrega. Completada = ambas partes confirmaron (o venció la gracia con una confirmación).</summary>
public enum EstadoSolicitud { Pendiente = 1, Aceptada = 2, Rechazada = 3, Cancelada = 4, Completada = 5, NoConcretada = 6 }

public enum ConceptoPago { Destacar = 1, Verificar = 2, Premium = 3, Empresa = 4, Recarga = 5 }

/// <summary>Pendiente → Aprobado | Rechazado | Expirado. RequiereRevision = se cobró pero el beneficio no pudo aplicarse (reembolso manual).</summary>
public enum EstadoPago { Pendiente = 1, Aprobado = 2, Rechazado = 3, Expirado = 4, RequiereRevision = 5, Reembolsado = 6 }

/// <summary>Estado físico del objeto publicado (lo declara quien publica).</summary>
public enum CondicionProducto { Nuevo = 1, ComoNuevo = 2, Usado = 3, UsadoConDetalles = 4, Reparado = 5, ParaRepuestos = 6 }

/// <summary>Documento del comprador para la factura electrónica (DIAN).</summary>
public enum TipoDocumentoFiscal { CC = 1, CE = 2, NIT = 3, Pasaporte = 4 }

/// <summary>Pendiente = falta emitirla ante la DIAN. Emitida = tiene número y CUFE. Anulada = no se emitirá (pago revertido antes de emitir).</summary>
public enum EstadoFactura { Pendiente = 1, Emitida = 2, Anulada = 3 }

/// <summary>PQR (Ley 1480 de 2011 y Ley 1755 de 2015). Retracto y ReversionPago se asocian a un pago propio.</summary>
public enum TipoPqr { Peticion = 1, Queja = 2, Reclamo = 3, Sugerencia = 4, Retracto = 5, ReversionPago = 6 }

public enum EstadoPqr { Abierta = 1, Respondida = 2 }

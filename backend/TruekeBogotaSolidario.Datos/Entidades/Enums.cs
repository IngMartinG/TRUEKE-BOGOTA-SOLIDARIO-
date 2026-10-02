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

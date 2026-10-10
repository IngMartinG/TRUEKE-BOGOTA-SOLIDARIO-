using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Negocio.Comun;

/// <summary>Recurso inexistente O que el usuario no debe saber que existe (HTTP 404).</summary>
public sealed class NoEncontradoException : Exception
{
    public NoEncontradoException(string mensaje) : base(mensaje) { }
}

/// <summary>Autenticado pero sin permiso para la operación (HTTP 403).</summary>
public sealed class AccesoDenegadoException : Exception
{
    public AccesoDenegadoException(string mensaje = "No tienes permiso para realizar esta acción.") : base(mensaje) { }
}

/// <summary>Credenciales inválidas, cuenta bloqueada o firma inválida (HTTP 401). El mensaje es siempre genérico.</summary>
public sealed class AutenticacionException : Exception
{
    public AutenticacionException(string mensaje = "Correo o contraseña incorrectos.") : base(mensaje) { }
}

/// <summary>La clave es correcta pero la cuenta tiene 2FA: el front debe pedir el código de 6 dígitos (HTTP 401, codigo "2fa_requerido").</summary>
public sealed class DosFactoresRequeridoException : Exception
{
    public const string Codigo = "2fa_requerido";
    public DosFactoresRequeridoException() : base("Ingresa el código de 6 dígitos de tu app autenticadora (o un código de recuperación).") { }
}

/// <summary>Falta la foto de perfil (HTTP 403, codigo "foto_requerida"): el front lleva a subirla.</summary>
public sealed class FotoPerfilRequeridaException : Exception
{
    public const string Codigo = "foto_requerida";
    public FotoPerfilRequeridaException()
        : base("Agrega una foto de perfil donde se vea tu cara: así la comunidad sabe con quién intercambia. Hazlo en Mi cuenta → Perfil.") { }
}

/// <param name="Codigo">Identificador estable para que el front reaccione sin depender del texto.</param>
public sealed record ErrorTraducido(int Estado, string Titulo, bool EsInesperado, string? Codigo = null);

/// <summary>
/// Única puerta de traducción excepción → respuesta HTTP. Solo las excepciones de negocio conocidas exponen su
/// mensaje; cualquier otra devuelve un mensaje genérico (el detalle solo va al log, nunca al cliente).
/// </summary>
public static class TraductorErrores
{
    public static ErrorTraducido Traducir(Exception ex) => ex switch
    {
        ReglaDeNegocioException e => new(400, e.Message, false),
        NoEncontradoException e => new(404, e.Message, false),
        AccesoDenegadoException e => new(403, e.Message, false),
        DosFactoresRequeridoException e => new(401, e.Message, false, DosFactoresRequeridoException.Codigo),
        FotoPerfilRequeridaException e => new(403, e.Message, false, FotoPerfilRequeridaException.Codigo),
        AutenticacionException e => new(401, e.Message, false),
        ConflictoDeConcurrenciaException e => new(409, e.Message, false),
        _ => new(500, "Error interno del servidor.", true)
    };
}

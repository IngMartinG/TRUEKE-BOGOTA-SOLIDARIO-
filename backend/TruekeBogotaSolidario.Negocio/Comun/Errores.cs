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

public sealed record ErrorTraducido(int Estado, string Titulo, bool EsInesperado);

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
        AutenticacionException e => new(401, e.Message, false),
        ConflictoDeConcurrenciaException e => new(409, e.Message, false),
        _ => new(500, "Error interno del servidor.", true)
    };
}

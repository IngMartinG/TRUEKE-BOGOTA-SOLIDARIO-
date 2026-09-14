namespace TruekeBogotaSolidario.Api.Common.Exceptions;

// Excepcion de dominio: se lanza cuando un recurso solicitado no existe.
// El middleware global la traduce a HTTP 404 + ProblemDetails (RFC 7807).
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

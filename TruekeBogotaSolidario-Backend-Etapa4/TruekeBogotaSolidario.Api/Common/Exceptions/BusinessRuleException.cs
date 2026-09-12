namespace TruekeBogotaSolidario.Api.Common.Exceptions;

// Excepcion de dominio: se lanza cuando se viola una regla de negocio
// (ej. transicion de estado invalida, campo obligatorio faltante).
// El middleware global la traduce a HTTP 400 + ProblemDetails (RFC 7807).
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message) { }
}

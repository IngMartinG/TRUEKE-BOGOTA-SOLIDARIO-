namespace TruekeBogotaSolidario.Datos.Common;

/// <summary>Violación de una regla de dominio. Su mensaje es seguro de mostrar al usuario (HTTP 400).</summary>
public class ReglaDeNegocioException : Exception
{
    public ReglaDeNegocioException(string mensaje) : base(mensaje) { }
}

/// <summary>Dos operaciones modificaron el mismo registro a la vez (HTTP 409).</summary>
public class ConflictoDeConcurrenciaException : Exception
{
    public ConflictoDeConcurrenciaException(string mensaje, Exception? interna = null) : base(mensaje, interna) { }
}

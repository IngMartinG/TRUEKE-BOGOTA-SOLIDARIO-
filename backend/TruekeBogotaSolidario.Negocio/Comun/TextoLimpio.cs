using System.ComponentModel.DataAnnotations;
using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Negocio.Comun;

/// <summary>
/// Error por campo (400) si el texto trae caracteres de control o invisibles de engaño; la regla es la de
/// <see cref="TextoSeguro"/>, que las entidades vuelven a aplicar al guardar.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class TextoLimpioAttribute : ValidationAttribute
{
    public TextoLimpioAttribute() : base(TextoSeguro.Mensaje) { }

    /// <summary>Permite saltos de línea (\n y \r).</summary>
    public bool Multilinea { get; init; }

    public override bool IsValid(object? value) => value is not string texto || TextoSeguro.EsSeguro(texto, Multilinea);
}

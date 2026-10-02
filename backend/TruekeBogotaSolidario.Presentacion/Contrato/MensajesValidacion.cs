using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace TruekeBogotaSolidario.Presentacion.Contrato;

/// <summary>
/// Mensajes de validación en español para las DataAnnotations que no traen ErrorMessage propio
/// (los de .NET vienen en inglés). {0} es el nombre del campo.
/// </summary>
public sealed class MensajesValidacionEspanol : IValidationMetadataProvider
{
    public void CreateValidationMetadata(ValidationMetadataProviderContext context)
    {
        foreach (var a in context.ValidationMetadata.ValidatorMetadata.OfType<ValidationAttribute>())
        {
            if (!string.IsNullOrEmpty(a.ErrorMessage) || a.ErrorMessageResourceType is not null) continue;
            a.ErrorMessage = a switch
            {
                RequiredAttribute => "El campo {0} es obligatorio.",
                StringLengthAttribute s when s.MinimumLength > 0 => "El campo {0} debe tener entre {2} y {1} caracteres.",
                StringLengthAttribute => "El campo {0} admite como máximo {1} caracteres.",
                MaxLengthAttribute => "El campo {0} admite como máximo {1} caracteres.",
                MinLengthAttribute => "El campo {0} debe tener al menos {1} caracteres.",
                RangeAttribute => "El campo {0} debe estar entre {1} y {2}.",
                EmailAddressAttribute => "El campo {0} no es un correo válido.",
                EnumDataTypeAttribute => "El valor del campo {0} no es válido.",
                _ => "El campo {0} no es válido."
            };
        }
    }

    /// <summary>Errores de conversión del binder (p. ej. texto donde se espera un número), también en español.</summary>
    public static void Configurar(MvcOptions o)
    {
        o.ModelMetadataDetailsProviders.Add(new MensajesValidacionEspanol());
        var m = o.ModelBindingMessageProvider;
        m.SetValueMustNotBeNullAccessor(_ => "El valor no puede ser nulo.");
        m.SetAttemptedValueIsInvalidAccessor((v, c) => $"El valor '{v}' no es válido para {c}.");
        m.SetMissingBindRequiredValueAccessor(c => $"Falta el valor de {c}.");
        m.SetMissingKeyOrValueAccessor(() => "Falta un valor obligatorio.");
        m.SetMissingRequestBodyRequiredValueAccessor(() => "El cuerpo de la solicitud es obligatorio.");
        m.SetNonPropertyAttemptedValueIsInvalidAccessor(v => $"El valor '{v}' no es válido.");
        m.SetNonPropertyUnknownValueIsInvalidAccessor(() => "El valor no es válido.");
        m.SetNonPropertyValueMustBeANumberAccessor(() => "El valor debe ser un número.");
        m.SetUnknownValueIsInvalidAccessor(c => $"El valor de {c} no es válido.");
        m.SetValueIsInvalidAccessor(v => $"El valor '{v}' no es válido.");
        m.SetValueMustBeANumberAccessor(c => $"El campo {c} debe ser un número.");
    }
}

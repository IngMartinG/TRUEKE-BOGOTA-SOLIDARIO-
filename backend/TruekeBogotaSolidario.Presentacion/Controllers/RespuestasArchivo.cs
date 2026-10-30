using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

/// <summary>Descargas privadas (nunca en caché): CSV para Excel en español y PDF.</summary>
internal static class RespuestasArchivo
{
    /// <summary>UTF-8 con BOM para que Excel en español muestre bien las tildes.</summary>
    public static FileContentResult ArchivoCsv(this ControllerBase c, string contenido, string nombre)
    {
        c.Response.Headers.CacheControl = "no-store";
        return c.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(contenido)).ToArray(), "text/csv; charset=utf-8", nombre);
    }

    public static FileContentResult ArchivoPdf(this ControllerBase c, byte[] contenido, string nombre)
    {
        c.Response.Headers.CacheControl = "no-store";
        return c.File(contenido, "application/pdf", nombre);
    }
}

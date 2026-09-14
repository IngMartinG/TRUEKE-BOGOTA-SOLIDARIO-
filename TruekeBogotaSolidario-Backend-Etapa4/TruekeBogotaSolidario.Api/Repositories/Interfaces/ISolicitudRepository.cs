using TruekeBogotaSolidario.Api.Domain.Entities;

namespace TruekeBogotaSolidario.Api.Repositories.Interfaces;

public interface ISolicitudRepository
{
    Task<List<Solicitud>> ObtenerPorPublicacionAsync(Guid publicacionId);
    Task<Solicitud?> ObtenerPorIdAsync(Guid id);
    Task AgregarAsync(Solicitud solicitud);
    Task GuardarCambiosAsync();
}

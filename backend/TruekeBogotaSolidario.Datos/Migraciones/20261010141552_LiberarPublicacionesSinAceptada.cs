using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <summary>
    /// Migración de datos (sin cambios de esquema). Antes, la primera solicitud ponía la publicación "EnNegociacion" y la
    /// sacaba del catálogo. Ahora solo pasa a ese estado cuando el dueño ACEPTA a alguien. Las publicaciones que quedaron
    /// "EnNegociacion" sin ninguna solicitud aceptada vuelven a "Disponible"; sus solicitudes pendientes siguen en pie
    /// (ahora son personas interesadas que el dueño puede aceptar).
    /// </summary>
    public partial class LiberarPublicacionesSinAceptada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE p SET p.Estado = 'Disponible'
                FROM Publicaciones p
                WHERE p.Estado = 'EnNegociacion'
                  AND NOT EXISTS (SELECT 1 FROM Solicitudes s WHERE s.PublicacionId = p.Id AND s.Estado = 'Aceptada');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin vuelta atrás: con las reglas nuevas, "EnNegociacion" sin solicitud aceptada es un estado inválido.
        }
    }
}

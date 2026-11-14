using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class HigieneSoloNueva : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Higiene solo nueva: la ropa interior, las medias y los trajes de baño ya publicados que no son nuevos salen del
            // catálogo y esperan a un moderador (no se borran). Se mira el título normalizado (minúsculas y sin tildes); la lista
            // es la de ArticulosDeHigiene. A cada dueño se le avisa en la bandeja.
            migrationBuilder.Sql("""
                INSERT INTO "Notificaciones" ("Id", "UsuarioId", "Tipo", "Mensaje", "RecursoId", "FechaUtc", "LeidaUtc")
                SELECT gen_random_uuid(), p."PropietarioId", 'PublicacionEnRevision',
                       '"' || p."Titulo" || '" quedó en revisión: por higiene, la ropa interior, las medias y los trajes de baño solo se publican como nuevos. Un moderador la revisa antes de mostrarla.',
                       p."Id", now(), NULL
                FROM "Publicaciones" p
                WHERE p."Estado" = 'Disponible' AND NOT p."EstaOculta" AND p."EstadoModeracion" = 'Aprobada' AND p."Condicion" <> 'Nuevo'
                  AND p."TituloBusqueda" ~ ' (ropa interior|calzoncillos?|calzon(?:es)?|panty|pantys|panties|pantaletas?|brasier(?:es)?|sosten(?:es)?|boxers?|tangas?|lenceria|lingerie|medias|calcetin(?:es)?|calcetas?|trajes? de bano|vestidos? de bano|ropa de bano|bikinis?|banadores?) ';

                UPDATE "Publicaciones" p
                SET "EstadoModeracion" = 'Pendiente', "PuntajeRiesgo" = 100,
                    "MotivosRiesgo" = 'Artículo de higiene que debe ser nuevo', "MotivoRechazo" = NULL, "EnRevisionDesdeUtc" = now()
                WHERE p."Estado" = 'Disponible' AND NOT p."EstaOculta" AND p."EstadoModeracion" = 'Aprobada' AND p."Condicion" <> 'Nuevo'
                  AND p."TituloBusqueda" ~ ' (ropa interior|calzoncillos?|calzon(?:es)?|panty|pantys|panties|pantaletas?|brasier(?:es)?|sosten(?:es)?|boxers?|tangas?|lenceria|lingerie|medias|calcetin(?:es)?|calcetas?|trajes? de bano|vestidos? de bano|ropa de bano|bikinis?|banadores?) ';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nada que deshacer: lo que quedó en revisión lo resuelve un moderador (aprobar o rechazar).
        }
    }
}

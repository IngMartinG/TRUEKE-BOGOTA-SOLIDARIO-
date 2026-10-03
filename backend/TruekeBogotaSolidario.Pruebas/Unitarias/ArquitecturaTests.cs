using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>Reglas de arquitectura y seguridad verificadas por reflexión (fallan en CI si alguien las rompe).</summary>
public class ArquitecturaTests
{
    private static readonly Assembly Presentacion = typeof(Program).Assembly;

    private static IEnumerable<Type> Controllers()
        => Presentacion.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

    [Fact]
    public void Presentacion_no_referencia_la_capa_de_Datos()
        => Assert.DoesNotContain(Presentacion.GetReferencedAssemblies(), a => a.Name == "TruekeBogotaSolidario.Datos");

    [Fact]
    public void Negocio_no_depende_de_AspNetCore()
        => Assert.DoesNotContain(typeof(Negocio.NegocioServiceCollectionExtensions).Assembly.GetReferencedAssemblies(),
            a => a.Name!.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));

    [Fact]
    public void Ningun_controller_recibe_repositorios_ni_DbContext()
    {
        foreach (var c in Controllers())
            foreach (var p in c.GetConstructors().SelectMany(k => k.GetParameters()))
                Assert.False(p.ParameterType.Namespace?.StartsWith("TruekeBogotaSolidario.Datos", StringComparison.Ordinal) == true,
                    $"{c.Name} recibe {p.ParameterType.Name} de la capa de Datos.");
    }

    [Fact]
    public void Todo_controller_tiene_Authorize_a_nivel_de_clase()
    {
        foreach (var c in Controllers())
            Assert.True(c.GetCustomAttribute<AuthorizeAttribute>() is not null, $"{c.Name} no tiene [Authorize].");
    }

    /// <summary>
    /// SqlClient lanza CultureNotFoundException al abrir la conexión si la API corre con InvariantGlobalization=true.
    /// Las pruebas de integración no lo detectan (usan la configuración de este proyecto), por eso se revisa el csproj.
    /// </summary>
    [Fact]
    public void La_API_no_usa_globalizacion_invariante()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TruekeBogotaSolidario.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var csproj = File.ReadAllText(Path.Combine(dir!.FullName, "TruekeBogotaSolidario.Presentacion", "TruekeBogotaSolidario.Presentacion.csproj"));
        Assert.DoesNotContain("<InvariantGlobalization>true</InvariantGlobalization>", csproj);
    }

    /// <summary>Lista blanca de CLAUDE.md: login, registro, GET de catálogo y categorías, política de Eco-Puntos y webhook de Wompi.</summary>
    [Fact]
    public void Solo_los_endpoints_permitidos_son_anonimos()
    {
        var permitidos = new HashSet<string>
        {
            "POST api/v1/auth/registrar",
            "POST api/v1/auth/login",
            "POST api/v1/auth/refrescar",
            "POST api/v1/auth/salir",
            "POST api/v1/auth/google",
            "POST api/v1/auth/verificar-correo",
            "POST api/v1/auth/olvide-clave",
            "POST api/v1/auth/restablecer-clave",
            "GET api/v1/categorias",
            "GET api/v1/publicaciones",
            "GET api/v1/publicaciones/cercanas",
            "GET api/v1/publicaciones/destacadas",
            "GET api/v1/ubicaciones/departamentos",
            "GET api/v1/ubicaciones/departamentos/{codigo:regex(^\\d{{2}}$)}/municipios",
            "GET api/v1/ubicaciones/municipios",
            "GET api/v1/ubicaciones/municipios/{codigo:regex(^\\d{{5}}$)}",
            "GET api/v1/publicaciones/{id:guid}",
            "GET api/v1/publicaciones/{id:guid}/comentarios",
            "GET api/v1/eco-puntos/politica",
            "GET api/v1/configuracion",
            "GET api/v1/usuarios/{id:guid}/perfil",
            "GET api/v1/usuarios/{id:guid}/publicaciones",
            "GET api/v1/usuarios/{id:guid}/calificaciones",
            "POST api/v1/pagos/wompi/eventos",
        };

        var anonimos = new List<string>();
        foreach (var c in Controllers())
        {
            var rutaBase = c.GetCustomAttribute<RouteAttribute>()?.Template ?? "";
            var claseAnonima = c.GetCustomAttribute<AllowAnonymousAttribute>() is not null;
            foreach (var m in c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var http = m.GetCustomAttribute<HttpMethodAttribute>();
                if (http is null || (!claseAnonima && m.GetCustomAttribute<AllowAnonymousAttribute>() is null)) continue;
                var ruta = string.IsNullOrEmpty(http.Template) ? rutaBase : $"{rutaBase}/{http.Template}";
                anonimos.AddRange(http.HttpMethods.Select(v => $"{v} {ruta}"));
            }
        }

        var noPermitidos = anonimos.Where(a => !permitidos.Contains(a)).ToList();
        Assert.True(noPermitidos.Count == 0, "Endpoints anónimos no permitidos: " + string.Join(", ", noPermitidos));
    }
}

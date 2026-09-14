using Microsoft.EntityFrameworkCore;
using TruekeBogotaSolidario.Api.Data;
using TruekeBogotaSolidario.Api.Middleware;
using TruekeBogotaSolidario.Api.Repositories;
using TruekeBogotaSolidario.Api.Repositories.Interfaces;
using TruekeBogotaSolidario.Api.Services;
using TruekeBogotaSolidario.Api.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// ==========================================================================
// 1) CAPA DE DATOS - Registro del DbContext (persistencia)
// ==========================================================================
// El proyecto academico define SQL Server como motor (Modelo C4 - Nivel 2).
// Para poder ejecutar y sustentar el proyecto sin depender de tener una
// instancia real de SQL Server instalada, se deja un interruptor en
// appsettings.json ("UsarBaseDeDatosEnMemoria"). En producción se apaga
// ese flag y EF Core persiste de verdad en SQL Server.
var usarBaseEnMemoria = builder.Configuration.GetValue<bool>("UsarBaseDeDatosEnMemoria");

builder.Services.AddDbContext<TruekeDbContext>(options =>
{
    if (usarBaseEnMemoria)
    {
        options.UseInMemoryDatabase("TruekeBogotaSolidarioDb");
    }
    else
    {
        var connectionString = builder.Configuration.GetConnectionString("TruekeDb");
        options.UseSqlServer(connectionString);
    }
});

// ==========================================================================
// 2) INYECCION DE DEPENDENCIAS - Contenedor IoC de .NET
// ==========================================================================
// Repositorios y Servicios se registran como Scoped: viven durante el ciclo
// de vida de cada peticion HTTP, igual que el DbContext (buena practica con EF Core).
builder.Services.AddScoped<IPublicacionRepository, PublicacionRepository>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<ISolicitudRepository, SolicitudRepository>();

builder.Services.AddScoped<IPublicacionService, PublicacionService>();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<ITruequeService, TruequeService>();

// ==========================================================================
// 3) CAPA DE CONTROLADOR - Controllers tradicionales [ApiController] + Swagger
// ==========================================================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Trueke Bogota Solidario - API",
        Version = "v1",
        Description = "API REST del backend academico de Trueke Bogota Solidario " +
                      "(Ingenieria Web - CUN). Arquitectura en capas: " +
                      "Controller -> Service -> Repository -> SQL Server (EF Core)."
    });
});

var app = builder.Build();

// ==========================================================================
// 4) SEED / MIGRACION AUTOMATICA EN DESARROLLO
// ==========================================================================
// Crea la base (o el esquema en memoria) y aplica los datos de prueba
// definidos en TruekeDbContext.OnModelCreating (HasData).
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TruekeDbContext>();
    context.Database.EnsureCreated();
}

// ==========================================================================
// 5) MIDDLEWARE PIPELINE
// ==========================================================================
// Manejo global de excepciones -> ProblemDetails (RFC 7807). Va primero en
// el pipeline para poder capturar errores de cualquier capa posterior.
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Trueke Bogota Solidario v1");
        options.RoutePrefix = string.Empty; // Swagger UI queda en la raiz: http://localhost:5000/
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

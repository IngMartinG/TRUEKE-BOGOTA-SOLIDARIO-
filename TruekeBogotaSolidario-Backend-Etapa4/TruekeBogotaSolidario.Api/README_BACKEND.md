# Trueke Bogotá Solidario — Back-end (Etapa 4: Desarrollo/Codificación)

Backend académico en **ASP.NET Core 8 (C#)**, alineado 1 a 1 con el Modelo C4
del proyecto (Nivel 2, 3 y 4) versionado en `/docs/c4` del repositorio.

## Arquitectura en capas (flujo de una petición)

```
Cliente (Postman / Swagger / futuro Front-end Angular)
        │
        ▼
Controller (PublicacionesController, UsuariosController, SolicitudesController)
        │  — solo enruta HTTP <-> DTOs, valida ModelState
        ▼
Service (PublicacionService, UsuarioService, TruequeService)
        │  — reglas de negocio: existencia de FKs, transiciones de estado válidas,
        │     "no puedes solicitar tu propia publicación", campos obligatorios, etc.
        ▼
Repository (PublicacionRepository, UsuarioRepository, SolicitudRepository)
        │  — único punto que habla con la base de datos (sin lógica de negocio)
        ▼
TruekeDbContext (Entity Framework Core)
        │
        ▼
SQL Server  (o InMemory para desarrollo/sustentación, ver appsettings.json)
```

Esto reproduce exactamente el Nivel 3 del C4 (Controladores → Servicios de
Dominio → Repositorios → PostgreSQL/SQL Server), y las entidades del Nivel 4
(`Publicacion`, `Categoria`, `Usuario`, `Solicitud`, `EstadoPublicacionEnum`)
con sus mismos métodos de dominio (`Publicar`, `MarcarEnNegociacion`,
`ConfirmarIntercambio`, `Cancelar`, `Aceptar`, `Rechazar`).

## Cómo ejecutar

Requiere [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
cd TruekeBogotaSolidario.Api
dotnet restore
dotnet run
```

Al iniciar, se abre Swagger UI en `http://localhost:5000/` con los 3
grupos de endpoints y datos de prueba precargados (1 usuario, 3 categorías,
1 publicación).

Por defecto el proyecto corre con **`UsarBaseDeDatosEnMemoria: true`** en
`appsettings.json`, para poder ejecutarlo y sustentarlo sin instalar SQL
Server. Para usar la base de datos relacional real (el motor definido en el
Modelo C4), cambia esa bandera a `false` y ajusta la cadena de conexión
`ConnectionStrings:TruekeDb`; EF Core crea el esquema automáticamente
(`Database.EnsureCreated()` en `Program.cs`).

## Endpoints principales (`/api/v1/...`)

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/v1/publicaciones` | Lista el catálogo de trueke |
| GET | `/api/v1/publicaciones/{id}` | Detalle de una publicación |
| POST | `/api/v1/publicaciones` | Publica un nuevo bien (201 + Location) |
| PATCH | `/api/v1/publicaciones/{id}/estado` | `en-negociacion` / `confirmar` / `cancelar` |
| GET | `/api/v1/usuarios` | Lista usuarios |
| POST | `/api/v1/usuarios` | Registra un usuario |
| GET | `/api/v1/solicitudes/por-publicacion/{id}` | Solicitudes recibidas por una publicación |
| POST | `/api/v1/solicitudes` | Solicita el intercambio de una publicación disponible |
| POST | `/api/v1/solicitudes/{id}/aceptar` | Acepta y confirma el intercambio |
| POST | `/api/v1/solicitudes/{id}/rechazar` | Rechaza la solicitud (con motivo) |

Cualquier error (recurso no encontrado, regla de negocio violada, dato
inválido) responde en formato `ProblemDetails` (RFC 7807), gracias al
middleware global `ExceptionHandlingMiddleware`.

## Siguientes pasos sugeridos (fuera del alcance obligatorio de esta etapa)

- `AuthController` con Google OAuth (pendiente real del proyecto).
- `ChatController` con SignalR para mensajería en tiempo real.
- Front-end en Angular consumiendo esta API (Nivel 2 del C4).

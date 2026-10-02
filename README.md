# Trueke Bogotá Solidario

Plataforma comunitaria de **economía circular** para Bogotá. Las personas intercambian, venden o donan objetos en tres modos (**Trueke, Compra y Donación**) y ganan **Eco-Puntos**, una moneda interna que premia las prácticas solidarias.

## Estructura del repositorio

```
├── backend/      API ASP.NET Core 8 en capas (Datos · Negocio · Presentación · Pruebas), SignalR, Docker
├── frontend/     Aplicación Angular (en construcción)
├── prototipo/    Prototipo estático de UI/UX (Etapa 3) y presentación del pitch
├── docs/         Modelo C4 (fuente de verdad de la arquitectura), contrato OpenAPI, especificación UI/UX y flujos
└── .github/      Integración continua (GitHub Actions)
```

## Stack

| Capa | Tecnología |
|---|---|
| Front-end | Angular |
| API | ASP.NET Core 8 (C#), SignalR |
| Datos | SQL Server (EF Core 8), Redis (backplane de SignalR) |
| Pagos | Wompi (solo dinero hacia la plataforma) |
| Infraestructura | Docker, GitHub Actions, Azure App Service + VNet + Blob Storage |

## Empezar

- **Backend:** ver [`backend/README.md`](backend/README.md). En resumen:
  ```bash
  cd backend
  dotnet test
  dotnet run --project TruekeBogotaSolidario.Presentacion
  ```
- **Contrato para el front:** [`docs/openapi.json`](docs/openapi.json).
- **Arquitectura:** [`docs/c4/C4_MODELO_TRUEKE.md`](docs/c4/C4_MODELO_TRUEKE.md).
- **Prototipo UI/UX:** abre [`prototipo/index.html`](prototipo/index.html) en el navegador.

## Flujo de trabajo

Rama `feature/*` → Pull Request → revisión y CI en verde → merge a `main`.

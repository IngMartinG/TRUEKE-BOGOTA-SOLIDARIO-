# Trueke Bogotá Solidario

Plataforma comunitaria de **economía circular** en Colombia, nacida en Bogotá. Las personas intercambian, venden o donan objetos en tres modos (**Trueke, Compra y Donación**) y ganan **Eco-Puntos**, una moneda interna que premia las prácticas solidarias.

## Estructura del repositorio

```
├── backend/        API ASP.NET Core 8 en capas (Datos · Negocio · Presentación · Pruebas), SignalR, Docker
├── frontend/       Aplicación Angular 22 (celular, tablet y PC), nginx con CSP, Docker
├── docs/
│   ├── c4/                      Modelo C4: fuente de verdad de la arquitectura
│   ├── openapi.json             Contrato de la API (lo vigilan las pruebas y el CI)
│   ├── migraciones/             Script SQL idempotente de todas las migraciones
│   ├── diseno/                  Especificación UI/UX original
│   ├── historico/               Prototipo y diagramas de la etapa académica (solo consulta)
│   └── ESTADO_Y_PENDIENTES.md   Qué está hecho y qué falta
├── .github/        CI (backend, frontend, CodeQL, ZAP) y Dependabot
└── .claude/        Servidores de desarrollo para la vista previa (api + front)
```

## Stack

| Capa | Tecnología |
|---|---|
| Front-end | Angular 22 (signals, sin Zone.js), Tailwind v4 |
| API | ASP.NET Core 8 (C#), SignalR |
| Datos | SQL Server (EF Core 8), Redis (backplane de SignalR, presencia y límite de peticiones) |
| Archivos | Azure Blob Storage (subida directa con SAS; fotos sin GPS ni metadatos) |
| Pagos | Wompi (solo dinero hacia la plataforma) y factura electrónica por cada pago |
| Infraestructura | Docker, GitHub Actions, Azure App Service + VNet + Key Vault + Application Insights |

## Empezar (desarrollo local)

1. **API** — detalles en [`backend/README.md`](backend/README.md):
   ```bash
   cd backend
   dotnet tool restore
   dotnet run --project TruekeBogotaSolidario.Presentacion --launch-profile SqlServerLocal
   ```
   Sin SQL Server instalado, el perfil por defecto usa una base en memoria (se borra al reiniciar).
2. **Front** — detalles en [`frontend/README.md`](frontend/README.md) (Node 24 LTS):
   ```bash
   cd frontend
   npm ci
   npm start
   ```
   Abre `http://localhost:4200`; `/api` y `/hubs` se reenvían a la API.
3. **Todo con Docker:** `docker compose up` desde `backend/` (API + SQL Server + Redis).

Verificaciones: `dotnet test` en `backend/` y `npm run verificar` en `frontend/`.

## Documentación

- Arquitectura: [`docs/c4/C4_MODELO_TRUEKE.md`](docs/c4/C4_MODELO_TRUEKE.md)
- Contrato de la API: [`docs/openapi.json`](docs/openapi.json) (Swagger en `/swagger` en desarrollo)
- Estado y pendientes: [`docs/ESTADO_Y_PENDIENTES.md`](docs/ESTADO_Y_PENDIENTES.md)

## Flujo de trabajo

Rama `feature/*` → Pull Request → revisión y CI en verde → merge a `main`.

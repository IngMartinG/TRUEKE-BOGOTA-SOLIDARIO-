# Modelado C4 — Trueke Bogotá Solidario

Laboratorio ACA aplicado al proyecto de cátedra "Trueke Bogotá Solidario".

---

## Nivel 1 — Contexto

Arquetipos de usuario: **Usuario Ciudadano** y **Administrador/Moderador**.
Sistemas externos: **Wompi** (pasarela de pagos) y **Google OAuth** (autenticación), más un servicio de **Email** para notificaciones.

```mermaid
flowchart TB
    U1(["👤 Usuario Ciudadano<br/>Publica, busca e<br/>intercambia bienes"]):::persona
    U2(["🛡️ Administrador / Moderador<br/>Gestiona usuarios y<br/>modera publicaciones"]):::persona

    SYS["🔄 Trueke Bogotá Solidario<br/>Plataforma web de intercambio<br/>solidario de bienes y servicios"]:::system

    EXT1["💳 Wompi<br/>Pasarela de pagos<br/>(planes premium / donaciones)"]:::external
    EXT2["🔐 Google OAuth<br/>Autenticación de usuarios"]:::external
    EXT3["✉️ Servicio de Email<br/>Notificaciones de<br/>solicitudes y mensajes"]:::external

    U1 -->|"Publica productos,<br/>busca, chatea"| SYS
    U2 -->|"Modera contenido,<br/>gestiona reportes"| SYS
    SYS -->|"Procesa pagos"| EXT1
    SYS -->|"Valida identidad"| EXT2
    SYS -->|"Envía notificaciones"| EXT3

    classDef persona fill:#1e3a8a,color:#ffffff,stroke:#3b82f6,stroke-width:1px,font-weight:bold;
    classDef system fill:#d97706,color:#ffffff,stroke:#f59e0b,stroke-width:2px,font-weight:bold;
    classDef external fill:#0f172a,color:#7dd3fc,stroke:#38bdf8,stroke-width:1px;
```

---

## Nivel 2 — Contenedores

Componentes ejecutables del sistema: Front-end, Back-end, Base de Datos y almacenamiento Cloud.

```mermaid
flowchart TB
    U1(["👤 Usuario Ciudadano"]):::persona

    subgraph SYS["Trueke Bogotá Solidario"]
        direction TB
        FE["🖥️ Front-end<br/>React (SPA)<br/>Hosting: Vercel"]:::container
        BE["⚙️ Back-end API<br/>Node.js / Express<br/>Hosting: Railway"]:::container
        DB[("🗄️ Base de Datos<br/>PostgreSQL (Supabase)<br/>usuarios, productos,<br/>chats, mensajes, solicitudes")]:::database
        STORAGE["☁️ Almacenamiento Cloud<br/>Supabase Storage<br/>(imágenes de publicaciones)"]:::container
    end

    EXT1["💳 Wompi"]:::external
    EXT2["🔐 Google OAuth"]:::external
    EXT3["✉️ Servicio de Email"]:::external

    U1 -->|"HTTPS"| FE
    FE -->|"REST API<br/>(JSON / JWT)"| BE
    BE -->|"SQL"| DB
    BE -->|"Sube/lee archivos"| STORAGE
    BE -->|"API"| EXT1
    BE -->|"OAuth 2.0"| EXT2
    BE -->|"SMTP / API"| EXT3

    classDef persona fill:#1e3a8a,color:#ffffff,stroke:#3b82f6,stroke-width:1px,font-weight:bold;
    classDef container fill:#0f172a,color:#7dd3fc,stroke:#38bdf8,stroke-width:1px;
    classDef database fill:#0f172a,color:#6ee7b7,stroke:#34d399,stroke-width:1px;
    classDef external fill:#1e293b,color:#cbd5e1,stroke:#64748b,stroke-width:1px;
```

---

## Nivel 3 — Componentes (dentro del contenedor API Backend)

```mermaid
flowchart TB
    FE["🖥️ Front-end (React)"]:::container

    subgraph API["⚙️ API Backend (Node.js / Express)"]
        direction TB
        subgraph CTRL["Controladores"]
            C1["AuthController"]:::component
            C2["ListingController"]:::component
            C3["ChatController"]:::component
        end
        subgraph SVC["Servicios de Dominio"]
            S1["UserService"]:::component
            S2["ListingService"]:::component
            S3["TruequeService"]:::component
        end
        subgraph REPO["Repositorios"]
            R1["UserRepository"]:::component
            R2["ListingRepository"]:::component
            R3["MessageRepository"]:::component
        end
    end

    DB[("🗄️ PostgreSQL (Supabase)")]:::database

    FE --> C1
    FE --> C2
    FE --> C3
    C1 --> S1
    C2 --> S2
    C3 --> S3
    S1 --> R1
    S2 --> R2
    S3 --> R3
    R1 --> DB
    R2 --> DB
    R3 --> DB

    classDef container fill:#1e3a8a,color:#ffffff,stroke:#3b82f6,stroke-width:1px,font-weight:bold;
    classDef component fill:#0f172a,color:#7dd3fc,stroke:#38bdf8,stroke-width:1px;
    classDef database fill:#0f172a,color:#6ee7b7,stroke:#34d399,stroke-width:1px;
```

---

## Nivel 4 — Código (PlantUML)

Entidad de dominio principal: **Publicacion** (equivalente a "OfertaAgricola" del ejemplo de referencia, pero aplicada al dominio de trueke).

Archivo a crear en VS Code: `Nivel4_Codigo.puml`

```plantuml
@startuml Nivel4_Codigo_TruekeBogota
skinparam classAttributeIconSize 0
top to bottom direction

package "TruekeBogota.Domain.Entities" {

    enum EstadoPublicacionEnum {
        DISPONIBLE
        EN_NEGOCIACION
        INTERCAMBIADA
        CANCELADA
    }

    class Publicacion {
        - id: Guid
        - titulo: string
        - descripcion: string
        - estado: EstadoPublicacionEnum
        - fechaPublicacion: DateTime
        + Publicacion(titulo: string, descripcion: string, categoria: Categoria)
        + Publicar(): void
        + MarcarEnNegociacion(): void
        + ConfirmarIntercambio(): void
        + Cancelar(motivo: string): void
    }

    class Categoria {
        - id: int
        - nombreCategoria: string
        - descripcion: string
        + ObtenerDetalles(): string
    }

    class Usuario {
        - id: Guid
        - nombreCompleto: string
        - localidad: string
        - correo: string
        + PublicarNuevoBien(publicacion: Publicacion): void
        + SolicitarIntercambio(publicacion: Publicacion): void
    }

    class Solicitud {
        - id: Guid
        - fechaSolicitud: DateTime
        - mensaje: string
        - aceptada: bool
        + Aceptar(): void
        + Rechazar(motivo: string): void
    }

    Publicacion "1" *-- "1" EstadoPublicacionEnum : posee
    Publicacion "*" --> "1" Categoria : pertenece a
    Usuario "1" --> "*" Publicacion : crea y gestiona
    Usuario "1" --> "*" Solicitud : envía
    Solicitud "*" --> "1" Publicacion : referencia
}
@enduml
```

### Pasos en VS Code
1. Instalar la extensión **PlantUML** (autor: jebbs).
2. Crear el archivo `Nivel4_Codigo.puml` dentro del repositorio, en una carpeta `docs/c4/`.
3. Pegar el código de arriba.
4. Presionar **Alt + D** para previsualizar en tiempo real.
5. Clic derecho sobre la previsualización → **Export Current Diagram** → `.png` o `.svg`.
6. Guardar el export dentro de `docs/c4/` junto al `.puml`.

---

## Vinculación con Trello / Azure Boards

Crear una tarjeta llamada **"Diseño de Arquitectura C4 - Hito ACA"** con:
- Enlace a este archivo en GitHub (una vez subido).
- Capturas de pantalla de los diagramas de Nivel 1, 2 y 3 (renderízalos en [mermaid.live](https://mermaid.live) y exporta como PNG).
- Captura del diagrama de Nivel 4 exportado desde VS Code.

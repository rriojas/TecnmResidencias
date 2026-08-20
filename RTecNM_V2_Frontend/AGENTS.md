# AGENTS.md - TecNM Residency System v2

## Project Overview

| Attribute | Value |
|-----------|-------|
| **System** | TecNM Professional Residency System v2 |
| **Tech Stack** | C# .NET 10, ASP.NET Core Web API, EF Core 10, PostgreSQL 18 |
| **Architecture** | Vertical Slice / Screaming Architecture |
| **Methodology** | Spec-Driven Development (SDD) + Receipt-Driven Development (RDD) |
| **Target OS** | Ubuntu 24.04 LTS + Nginx |
| **Frontend** | Server-rendered HTML + centralized CSS (no SPA framework) |

---

## Build & Test Commands

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/TecNM.Residency
```

---

## Architecture Rules

### Vertical Slice Colocation
Every domain feature lives inside `src/<DomainName>/` with ALL related files colocated:

```
src/<DomainName>/
    <Domain>Controller.cs
    <Domain>Service.cs
    <Domain>Repository.cs
    I<Domain>Repository.cs
    Create<Domain>Dto.cs
    Update<Domain>Dto.cs
    <Domain>ResponseDto.cs
    <Domain>Configuration.cs   (EF Core IEntityTypeConfiguration)
    <Domain>.cs                 (Domain Entity)
```

### Mandatory Base Fields (BaseEntity)
All entities MUST inherit from `BaseEntity` with these audit fields:

| Field | Type | DB Default |
|-------|------|------------|
| `Id` | `long` | `BIGSERIAL` PK |
| `IsActive` | `bool` | `TRUE NOT NULL` |
| `IsVisible` | `bool` | `TRUE NOT NULL` |
| `DisplayOrder` | `int` | `0 NOT NULL` |
| `CreatedBy` | `long?` | `NULL` |
| `UpdatedBy` | `long?` | `NULL` |
| `DeletedBy` | `long?` | `NULL` |
| `CreatedAt` | `DateTime` | `CURRENT_TIMESTAMP NOT NULL` |
| `UpdatedAt` | `DateTime` | `CURRENT_TIMESTAMP NOT NULL` |
| `DeletedAt` | `DateTime?` | `NULL` |

### isActive Protocol
- **NEVER** include `IsActive` in any `Update*Dto`
- **Soft Delete**: `DELETE /api/v1/{domain}/{id}` → sets `IsActive=false`, `DeletedAt`, `DeletedBy`
- **Reactivate**: `PATCH /api/v1/{domain}/{id}/activate` → sets `IsActive=true`, clears `DeletedAt`/`DeletedBy`

---

## Domain Modules

| # | Domain | Route Prefix | Description |
|---|--------|--------------|-------------|
| 01 | `src/Auth/` | `/api/v1/auth` | Authentication & JWT |
| 02 | `src/Students/` | `/api/v1/students` | Student management |
| 03 | `src/Advisors/` | `/api/v1/advisors` | Advisor management |
| 04 | `src/Projects/` | `/api/v1/projects` | Residency projects |
| 05 | `src/Activities/` | `/api/v1/activities` | Schedule & activities |
| 06 | `src/Advisories/` | `/api/v1/advisories` | Evaluations & advisories |
| 07 | `src/Reports/` | `/api/v1/reports` | Administration & reports |

---

## Naming Conventions

| Context | Convention | Example |
|---------|------------|---------|
| C# Classes | `PascalCase` | `StudentController` |
| C# Methods | `PascalCase` | `GetByIdAsync()` |
| C# Properties | `PascalCase` | `FirstName` |
| C# Interfaces | `I{PascalCase}` | `IStudentRepository` |
| C# Private Fields | `_camelCase` | `_studentRepository` |
| C# Parameters | `camelCase` | `studentId` |
| Database Tables | `snake_case` | `students`, `residency_projects` |
| Database Columns | `snake_case` | `first_name`, `is_active` |
| User Interface | **Spanish (es-MX)** | `Nombre`, `Guardar`, `Eliminar` |

EF Core must use `UseSnakeCaseNamingConvention()` for PostgreSQL mapping.

---

## RDD Rules (Receipt-Driven Development)

### The 4 Supreme Rules:

1. **NO NARRATIVE WITHOUT RECEIPT**
   - Never claim "bug fixed" or "component ready" without physical evidence
   - Valid receipts: terminal logs, `dotnet test` output, `git diff`, compiler traces

2. **PRINCIPLE OF DERIVATION**
   - "Trust what the system can derive, not what the agent narrates"
   - All decisions from file system state and artifacts, never from assumptions

3. **POST-EXECUTION CONTROL**
   - Before closing any subtask, verify with execution receipt
   - Use `dotnet test` output to confirm contracts not broken
   - Iterate based on log error evidence

4. **TOKEN EFFICIENCY**
   - No lengthy explanations or fluff
   - Identify receipt, analyze artifact, deliver solution

---

## API Conventions

| Operation | Method | Route | Body |
|-----------|--------|-------|------|
| List | `GET` | `/api/v1/{domain}` | - |
| Get by ID | `GET` | `/api/v1/{domain}/{id}` | - |
| Create | `POST` | `/api/v1/{domain}` | `Create*Dto` |
| Update | `PUT` | `/api/v1/{domain}/{id}` | `Update*Dto` |
| Soft Delete | `DELETE` | `/api/v1/{domain}/{id}` | - |
| Reactivate | `PATCH` | `/api/v1/{domain}/{id}/activate` | - |

### Pagination (List endpoints)

- All list endpoints are paginated server-side.
- Query params: `pageNumber` (default 1, clamped to >= 1), `pageSize` (default 10, max 50).
- Domain filters are server-side too (e.g. `status=all|active|inactive`, `roleFilter`, `search`).
- Response envelope: `PaginatedResult<T>`:
  ```json
  { "items": [], "totalCount": 0, "pageNumber": 1, "pageSize": 10,
    "totalPages": 0, "hasPreviousPage": false, "hasNextPage": false }
  ```
- Implemented with `QueryableExtensions.ToPaginatedAsync(query, pageNumber, pageSize)` in repositories.
- Dropdown pickers use lightweight non-paginated endpoints: `GET /api/v1/{domain}/options`.
- Frontend: shared pager via `window.renderPagination(container, meta, onPage)` (defined in `assets/js/layout.js`) and CSS classes `.tecnm-pagination*` (centralized in `main.css`).

---

## Frontend Rules

### CSS Architecture
- **100% centralized** in `src/wwwroot/assets/css/`
  - `tecnm-theme.css` → Design tokens (`:root` CSS custom properties)
  - `main.css` → Component primitives
- **Strictly prohibited**: inline styles (`style="..."`), hardcoded HEX colors, non-standard utility classes

### Frontend Structure
```
src/
  Pages/           ← Razor Pages (server-rendered HTML)
    _Layout.cshtml  ← Header institucional (logo TecNM) + navbar + footer compartidos
    Index.cshtml    ← Landing page (ruta /)
    Auth/Login.cshtml            → /auth/login
    Dashboard/Index.cshtml       → /dashboard
    Students/Index.cshtml        → /students
    Students/Profile.cshtml      → /students/profile
    Advisors/Index.cshtml        → /advisors
    Projects/Proposal.cshtml     → /projects/proposal
    Projects/Review.cshtml       → /projects/review
    Activities/Schedule.cshtml   → /activities/schedule
    Evaluations/Index.cshtml     → /evaluations
    Evaluations/Grading.cshtml   → /evaluations/grading
    Admin/Reports.cshtml         → /admin/reports
  wwwroot/
    assets/
      css/           ← Design tokens + component styles (100% centralizado)
      js/            ← JavaScript modules (layout.js = guard auth + user chip)
      images/        ← Logo institucional (tecnm-logo-white.svg)
```

### Frontend Routes & Layout Flags
- `ViewData["IsPublic"]` (`true` en landing/login): sin navbar, sin guard de sesión; requiere `@section HeaderActions`.
- `ViewData["NavActive"]`: marca la pestaña activa en el navbar compartido (`dashboard`, `students`, `advisors`, `proposal`, `review`, `schedule`, `evaluations`, `grading`, `reports`).
- `assets/js/layout.js` se carga en todas las páginas internas: redirige a `/auth/login` sin sesión, pinta el chip de usuario y gestiona logout. Los JS de dominio NO manejan logout.
- Estilos inline (`style="..."`) y bloques `<style>` **prohibidos** en `.cshtml` (Spec 08 §1).

### TecNM 2024 Graphic Identity
- Primary Blue: `#1B396A`
- Gold: `#C5A059`
- Font: Montserrat
- All UI text in **Spanish (es-MX)**
- **Date Formatting Standard**: All dates displayed in the UI MUST be formatted as `DD/NombreMes/YYYY` (e.g. `10/Agosto/2026`) via `window.formatTecNMDate(iso)`.

---

## Specs Reference

| Spec | File |
|------|------|
| Architecture & Standards | `docs/specs/00-architecture-and-standards.md` |
| Authentication | `docs/specs/01-authentication/backend.spec.md` |
| Students | `docs/specs/02-students/backend.spec.md` |
| Advisors | `docs/specs/03-advisors/backend.spec.md` |
| Residency Projects | `docs/specs/04-residency-projects/backend.spec.md` |
| Activities/Schedule | `docs/specs/05-activities-schedule/backend.spec.md` |
| Advisories/Evaluations | `docs/specs/06-advisories-evaluations/backend.spec.md` |
| Administration/Reports | `docs/specs/07-administration-reports/backend.spec.md` |
| UI Design System | `docs/specs/08-ui-design-system.md` |

### Database
- Schema: `docs/database/schema.sql`, `schema_pg18.sql`
- Seed data: `docs/database/seed_v2.sql`
- Migration: `docs/database/migrate_v1_to_v2.py`

### Legacy Reference (READ-ONLY)
- PHP source: `Legacy/` directory
- Legacy schema: `Legacy/schemaLegacy.sql`

---

## Workflow

1. **Read the relevant spec** before implementing any domain
2. **Create all domain files** following Vertical Slice structure
3. **Run `dotnet build`** to verify compilation
4. **Run `dotnet test`** to verify contracts
5. **Present receipts** (terminal output) before marking tasks complete

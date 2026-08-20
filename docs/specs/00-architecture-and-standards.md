# 00 - Architecture, SDD and RDD Coding Standards Specification

System: TecNM Professional Residency System (v2)  
Tech Stack: **C# .NET 10** (ASP.NET Core Web API), **Entity Framework Core 10** (`Npgsql.EntityFrameworkCore.PostgreSQL`), **PostgreSQL 18**, **Ubuntu Server 24.04 LTS** + **Nginx**  
Methodology: **Spec-Driven Development (SDD)** & **Receipt-Driven Development (RDD)**  
Architecture Pattern: Screaming Architecture / Vertical Slice Domain Colocation  

---

## 1. Development Methodology: SDD + RDD (Receipt-Driven Development)

This project strictly operates under **Spec-Driven Development (SDD)** paired with **Receipt-Driven Development (RDD)** to eliminate "Vibe Coding", blind trust, and narrative hallucinations. Technical work is governed by physical evidence and verifiable technical contracts.

### The 4 Supreme Rules of RDD:

1. **PROHIBIDA LA NARRACIÓN SIN RECIBO (No Narrative Without a Receipt)**:
   Never declare "The bug is fixed" or "The component is ready" based on assumption. Every task completion or suggested change MUST be accompanied by a physical **Receipt**. A receipt is: a terminal console log, execution output from a test harness (`dotnet test`), a real Git diff, or a compiler error trace.

2. **PRINCIPIO DE DERIVACIÓN (Principle of Derivation)**:
   *"Trust what the system can derive, not what the agent narrates"*. All technical decisions must be derived directly from the current file system state and generated artifacts in the environment, never from assumptions.

3. **REVISIÓN POST-EJECUCIÓN (Post-Execution Control)**:
   Before closing any subtask, enforce a control checkpoint. Present or require an execution receipt (e.g., `dotnet test` output) to verify that existing contracts are not broken. If the receipt does not match expected success, iterate strictly based on log error evidence.

4. **EFICIENCIA DE TOKENS (Token Efficiency)**:
   No lengthy explanations or fluff. Go straight to the point: identify the receipt, analyze the artifact, and deliver the exact solution.

---

## 2. Architectural Rules & Guidelines (C# .NET 10 + EF Core 10 + PostgreSQL 18)

1. **Vertical Slice Colocation**:
   - Every domain feature lives inside `src/<DomainName>/`.
   - Controllers, Services, Repositories, DTOs, EF Core Entity configurations, and Domain Entities are colocated in the same domain directory.
   - Example:
     ```
     src/Students/
     ├── StudentController.cs
     ├── StudentService.cs
     ├── StudentRepository.cs
     ├── IStudentRepository.cs
     ├── CreateStudentDto.cs
     ├── UpdateStudentDto.cs
     ├── StudentResponseDto.cs
     ├── StudentConfiguration.cs  (EF Core 10 IEntityTypeConfiguration<Student>)
     └── Student.cs
     ```

2. **State & Activation Endpoint Protocol (`isActive`)**:
   - `IsActive` / `is_active` **MUST NEVER** be part of any `Update*Dto`.
   - Disabling/Soft-deleting a record: `DELETE /api/v1/<domain>/{id}` (`IsActive = false`, `DeletedAt = DateTime.UtcNow`, `DeletedBy = userId`).
   - Reactivating a record: `PATCH /api/v1/<domain>/{id}/activate` (`IsActive = true`, `DeletedAt = null`, `DeletedBy = null`).

3. **Standard Mandatory Audit & Status Fields (PostgreSQL 18 + EF Core 10 `BaseEntity`)**:
   All database tables and domain models MUST inherit from `BaseEntity` or map standard audit properties:
   - `Id` (`long` / `bigint`, Primary Key, `BIGSERIAL` / Identity in PostgreSQL 18)
   - `IsActive` (`bool` / `boolean DEFAULT TRUE NOT NULL`)
   - `IsVisible` (`bool` / `boolean DEFAULT TRUE NOT NULL`)
   - `DisplayOrder` (`int` / `integer DEFAULT 0 NOT NULL`)
   - `CreatedBy` (`long?` / `bigint NULL`)
   - `UpdatedBy` (`long?` / `bigint NULL`)
   - `DeletedBy` (`long?` / `bigint NULL`)
   - `CreatedAt` (`DateTime` / `TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL`)
   - `UpdatedAt` (`DateTime` / `TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL`)
   - `DeletedAt` (`DateTime?` / `TIMESTAMP WITH TIME ZONE NULL`)

4. **Server-Side Pagination Convention (all list endpoints)**:
   - Query params: `pageNumber` (default `1`, clamped to `>= 1`), `pageSize` (default `10`, max `50`) — bound via `Common/PaginationQuery`.
   - Response envelope: `Common/PaginatedResult<T>` (`items`, `totalCount`, `pageNumber`, `pageSize`, `totalPages`, `hasPreviousPage`, `hasNextPage`).
   - Repositories MUST page via `QueryableExtensions.ToPaginatedAsync(query, pageNumber, pageSize)` (executes `CountAsync` + `Skip`/`Take`).
   - Domain filters (`status`, `search`, `roleFilter`) are applied **server-side** on the same query; the frontend resets to page 1 when a filter changes.
   - Dropdown pickers MUST NOT consume paginated endpoints; use lightweight `GET /api/v1/{domain}/options` endpoints instead.

5. **Language Protocol & Naming Conventions**:
   - **C# Source Code**: `PascalCase` for Classes, Methods, Properties, Interfaces (`IStudentRepository`), DTOs, Enums. `camelCase` for private fields (`_studentRepository`) and parameters.
   - **Database (PostgreSQL 18)**: 100% English `snake_case` tables and columns via EF Core `UseSnakeCaseNamingConvention()`.
   - **User Interface (GUI)**: 100% Spanish (es-MX) labels, messages, placeholders, and error alerts.

6. **Centralized Frontend UI Design System (TecNM Graphic Identity 2024)**:
   - All frontend views and templates MUST strictly conform to [08-ui-design-system.md](file:///c:/Users/rrioj/source/repos/TecNM/ResidenciasTecNM/SISTEMARESIDENCIA/docs/specs/08-ui-design-system.md), derived from the official [Manual de Identidad Gráfica TecNM 2024](https://iguala.tecnm.mx/pdf/Manual_Identidad_Grafica_TecNM_2024.pdf).
   - Styles MUST be 100% centralized via `:root` CSS Design Tokens in `public/assets/css/tecnm-theme.css` and component primitives in `public/assets/css/main.css`.
   - In-line styles (`style="..."`), hardcoded HEX colors, and non-standard utility classes are strictly prohibited.

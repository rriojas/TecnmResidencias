# 04 - Residency Projects Domain Specification (Backend C# .NET 10 + EF Core 10)

Module: `src/Projects/`  
Domain: Residency Proposals, Objectives & Department Approvals  
Tech Stack: **C# .NET 10** (ASP.NET Core Web API), **Entity Framework Core 10**, **PostgreSQL 18**

---

## 1. Colocated Files (Vertical Slice C#)

- `src/Projects/ProjectsController.cs`
- `src/Projects/ProjectService.cs`
- `src/Projects/IProjectRepository.cs`
- `src/Projects/ProjectRepository.cs`
- `src/Projects/CreateProjectDto.cs`
- `src/Projects/ProjectResponseDto.cs`
- `src/Projects/ProjectConfiguration.cs` (EF Core `IEntityTypeConfiguration<Project>`)
- `src/Projects/Project.cs`
- `src/Projects/ProjectObjective.cs`

---

## 2. API Endpoints

### 2.1 Submit Residency Proposal
- **Endpoint**: `POST /api/v1/projects`

### 2.2 Department Review & Dictamen
- **Endpoint**: `PATCH /api/v1/projects/{id}/status`
- **Request Body**:
  ```json
  {
    "status": "approved",
    "comments": "Anteproyecto cumple con los requisitos técnicos de la división."
  }
  ```

---

## 3. Business Rules & EF Core PostgreSQL 18 Mapping

1. **Status Enum**: `pending`, `under_review`, `approved`, `rejected`.
2. **Database Schema (PostgreSQL 18 `projects` table)**:
   - `id` (`BIGSERIAL`, PK)
   - `student_id` (`BIGINT`, FK `students.id`, NOT NULL)
   - `company_id` (`BIGINT`, FK `companies.id`, NOT NULL)
   - `title` (`VARCHAR(255)`, NOT NULL)
   - `problem_statement` (`TEXT`, NOT NULL)
   - `justification` (`TEXT`, NOT NULL)
   - `general_objective` (`TEXT`, NOT NULL)
   - `status` (`VARCHAR(30)`, DEFAULT 'pending', NOT NULL)
   - Audit columns (`is_active`, `is_visible`, `display_order`, `created_by`, `updated_by`, `deleted_by`, `created_at`, `updated_at`, `deleted_at`).

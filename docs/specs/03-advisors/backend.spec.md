# 03 - Advisors Domain Specification (Backend C# .NET 10 + EF Core 10)

Module: `src/Advisors/`  
Domain: Advisors Directory & Residency Assignment  
Tech Stack: **C# .NET 10** (ASP.NET Core Web API), **Entity Framework Core 10**, **PostgreSQL 18**

---

## 1. Colocated Files (Vertical Slice C#)

- `src/Advisors/AdvisorsController.cs`
- `src/Advisors/AdvisorService.cs`
- `src/Advisors/IAdvisorRepository.cs`
- `src/Advisors/AdvisorRepository.cs`
- `src/Advisors/CreateAdvisorDto.cs`
- `src/Advisors/AdvisorResponseDto.cs`
- `src/Advisors/AdvisorConfiguration.cs` (EF Core `IEntityTypeConfiguration<Advisor>`)
- `src/Advisors/Advisor.cs`

---

## 2. API Endpoints

### 2.1 List Advisors
- **Endpoint**: `GET /api/v1/advisors`
- **Controller Action**: `[HttpGet] public async Task<ActionResult<IEnumerable<AdvisorResponseDto>>> GetAll()`

### 2.2 Create Advisor
- **Endpoint**: `POST /api/v1/advisors`

### 2.3 Assign Advisor to Residency Project
- **Endpoint**: `POST /api/v1/advisors/assign`
- **Request Body**:
  ```json
  {
    "projectId": 10,
    "advisorId": 5,
    "advisorType": "internal"
  }
  ```

---

## 3. Business Rules & EF Core PostgreSQL 18 Mapping

1. **Advisor Types**: `internal` (Docentes del plantel) and `external` (Asesores de la empresa).
2. **Database Schema (PostgreSQL 18 `advisors` table)**:
   - `id` (`BIGSERIAL`, PK)
   - `user_id` (`BIGINT`, FK `users.id`, NULLABLE)
   - `full_name` (`VARCHAR(150)`, NOT NULL)
   - `advisor_type` (`VARCHAR(20)`, NOT NULL)
   - `academic_degree` (`VARCHAR(100)`, NOT NULL)
   - `department_id` (`BIGINT`, FK `academic_departments.id`, NULLABLE)
   - Audit columns (`is_active`, `is_visible`, `display_order`, `created_by`, `updated_by`, `deleted_by`, `created_at`, `updated_at`, `deleted_at`).

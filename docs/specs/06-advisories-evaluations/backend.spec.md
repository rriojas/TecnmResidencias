# 06 - Advisories and Evaluations Domain Specification (Backend C# .NET 10 + EF Core 10)

Module: `src/Evaluations/`  
Domain: Advisory Sessions & Partial/Final Grading  
Tech Stack: **C# .NET 10** (ASP.NET Core Web API), **Entity Framework Core 10**, **PostgreSQL 18**

---

## 1. Colocated Files (Vertical Slice C#)

- `src/Evaluations/EvaluationsController.cs`
- `src/Evaluations/EvaluationService.cs`
- `src/Evaluations/IEvaluationRepository.cs`
- `src/Evaluations/EvaluationRepository.cs`
- `src/Evaluations/GradeEvaluationDto.cs`
- `src/Evaluations/Evaluation.cs`
- `src/Evaluations/AdvisorySession.cs`

---

## 2. API Endpoints

### 2.1 Grade Student Evaluation (Parcial 1, Parcial 2, Final)
- **Endpoint**: `POST /api/v1/evaluations`
- **Request Body (`GradeEvaluationDto`)**:
  ```json
  {
    "projectId": 10,
    "evaluatorId": 5,
    "evaluationPeriod": "partial_1",
    "score": 95,
    "feedback": "Excelente desempeño en la entrega del primer avance."
  }
  ```

---

## 3. Business Rules & EF Core PostgreSQL 18 Mapping

1. **Score Validation**: Scores must be between `0` and `100`.
2. **Database Schema (PostgreSQL 18 `evaluations` table)**:
   - `id` (`BIGSERIAL`, PK)
   - `project_id` (`BIGINT`, FK `projects.id`, NOT NULL)
   - `evaluator_id` (`BIGINT`, FK `advisors.id`, NOT NULL)
   - `evaluation_period` (`VARCHAR(30)`, NOT NULL)
   - `score` (`NUMERIC(5,2)`, NOT NULL)
   - `feedback` (`TEXT`, NULLABLE)
   - Audit columns (`is_active`, `is_visible`, `display_order`, `created_by`, `updated_by`, `deleted_by`, `created_at`, `updated_at`, `deleted_at`).

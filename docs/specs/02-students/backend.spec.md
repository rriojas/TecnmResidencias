# 02 - Students Domain Specification (Backend C# .NET 10 + EF Core 10)

Module: `src/Students/`  
Domain: Student Registration, Profile & Academic Management  
Tech Stack: **C# .NET 10** (ASP.NET Core Web API), **Entity Framework Core 10**, **PostgreSQL 18**

---

## 1. Colocated Files (Vertical Slice C#)

- `src/Students/StudentsController.cs`
- `src/Students/StudentService.cs`
- `src/Students/IStudentRepository.cs`
- `src/Students/StudentRepository.cs`
- `src/Students/CreateStudentDto.cs`
- `src/Students/UpdateStudentDto.cs`
- `src/Students/StudentResponseDto.cs`
- `src/Students/StudentConfiguration.cs` (EF Core `IEntityTypeConfiguration<Student>`)
- `src/Students/Student.cs`

---

## 2. API Endpoints

### 2.1 List Students
- **Endpoint**: `GET /api/v1/students`
- **Controller Action**: `[HttpGet] public async Task<ActionResult<IEnumerable<StudentResponseDto>>> GetAll()`

### 2.2 Create Student
- **Endpoint**: `POST /api/v1/students`
- **Request Body (`CreateStudentDto`)**:
  ```json
  {
    "controlNumber": "20680123",
    "firstName": "Juan",
    "lastName": "Pérez López",
    "careerId": 1,
    "email": "20680123@cdserdan.tecnm.mx",
    "gpa": 92.5
  }
  ```

### 2.3 Soft-Delete Student (Deactivate)
- **Endpoint**: `DELETE /api/v1/students/{id}`
- **Behavior**: Sets `IsActive = false`, `DeletedAt = DateTime.UtcNow`, `DeletedBy = currentUserId` via EF Core.

### 2.4 Reactivate Student
- **Endpoint**: `PATCH /api/v1/students/{id}/activate`
- **Behavior**: Sets `IsActive = true`, `DeletedAt = null`, `DeletedBy = null` via EF Core.

---

## 3. Business Rules & EF Core PostgreSQL 18 Mapping

1. **Protocol (`IsActive`)**: `IsActive` must never appear in `UpdateStudentDto`. State toggling is handled exclusively via `DELETE` and `PATCH /activate`.
2. **Database Schema (PostgreSQL 18 `students` table)**:
   - `id` (`BIGSERIAL`, PK)
   - `user_id` (`BIGINT`, FK `users.id`, UNIQUE)
   - `control_number` (`VARCHAR(20)`, UNIQUE, NOT NULL)
   - `first_name` (`VARCHAR(100)`, NOT NULL)
   - `last_name` (`VARCHAR(100)`, NOT NULL)
   - `career_id` (`BIGINT`, FK `academic_careers.id`, NOT NULL)
   - `gpa` (`NUMERIC(5,2)`, NOT NULL)
   - Audit columns (`is_active`, `is_visible`, `display_order`, `created_by`, `updated_by`, `deleted_by`, `created_at`, `updated_at`, `deleted_at`).

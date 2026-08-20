# Legacy Equivalence and Parity Guide - Professional Residency System (TecNM)

This guide documents the technical equivalence mapping between the legacy system (`Legacy/`) and the new **C# .NET 10** + **EF Core 10** + **PostgreSQL 18** Spec-Driven Development architecture (v2).

---

## 1. Table Parity and Normalization Mapping Matrix (PostgreSQL 18)

| Legacy Table (Spanish) | v2 PostgreSQL 18 Table (English) | C# EF Core 10 Entity | Normalization & Design Changes |
| :--- | :--- | :--- | :--- |
| `admin`, `asesor`, `divisiones`, `gestion`, `tb_residentes` | `users`, `students`, `advisors`, `academic_departments` | `User`, `Student`, `Advisor`, `AcademicDepartment` | Consolidated authentication into `User`. Split student profile and address into `Student`. |
| `carreras` | `academic_careers` | `AcademicCareer` | Added FK `department_id`, surrogate `id` (`BIGSERIAL`), and standard EF Core audit fields. |
| `periodos` | `academic_periods` | `AcademicPeriod` | Converted strings to explicit `start_date` and `end_date` attributes (`TIMESTAMP WITH TIME ZONE`). |
| `empresa` | `companies` | `Company` | Unified RFC uniqueness, added contact details and standard audit fields. |
| `proyectos` | `projects` | `Project` | Renamed columns to English, decoupled objectives into `ProjectObjective`. |
| `objectivose` | `project_objectives` | `ProjectObjective` | Added surrogate `id`, mapped `objective_number` and status enum. |
| `actividades` (semana1A..semana26A) | `weekly_activities`, `weekly_progress` | `WeeklyActivity`, `WeeklyProgress` | **Unpivoted**: 26 individual week columns transformed into rows in `weekly_progress`. |
| `reportes`, `reportesasesor`, `reporte3` | `evaluations`, `documents` | `Evaluation`, `Document` | **Unpivoted**: Repeating `calificacionR1..RF` transformed into `Evaluation` entity records. |
| `tramitesgestion`, `solicitud`, `cartapresentacion`, `documentosinscripcion` | `documents` | `Document` | **Unpivoted**: Document blobs converted into single `Document` entity with `document_type`. |

---

## 2. API Endpoint & Controller Equivalence (C# ASP.NET Core 10)

| Legacy PHP File | New v2 C# ASP.NET Core Endpoint | HTTP Method | C# Controller / Action | GUI Component (Spanish es-MX) |
| :--- | :--- | :--- | :--- | :--- |
| `Validacion.php`, `Validacion2.php` | `/api/v1/auth/login` | `POST` | `AuthController.Login` | Pantalla de Inicio de Sesión |
| `logout.php` | `/api/v1/auth/logout` | `POST` | `AuthController.Logout` | Botón Cerrar Sesión |
| `ALUMNOS2/insertarA.php` | `/api/v1/students` | `POST` | `StudentsController.Create` | Registro de Alumno |
| `ALUMNOS2/actualizar.php` | `/api/v1/students/{id}` | `PUT` | `StudentsController.Update` | Editar Datos de Alumno |
| `ALUMNOS2/eliminar.php` | `/api/v1/students/{id}` | `DELETE` | `StudentsController.SoftDelete` | Desactivar Alumno (Soft Delete) |
| N/A (Nuevo) | `/api/v1/students/{id}/activate` | `PATCH` | `StudentsController.Activate` | Reactivar Alumno |
| `ASESORES2/index.php` | `/api/v1/advisors` | `GET` | `AdvisorsController.GetAll` | Catálogo de Asesores |
| `GESTION2/actividades.php` | `/api/v1/projects/{id}/activities` | `GET` / `POST` | `ActivitiesController.GetByProject` | Cronograma de 26 Semanas |
| `archivosgestion2/subir.php` | `/api/v1/documents` | `POST` | `DocumentsController.Upload` | Carga de Expediente y Evidencias |

---

## 3. Data Transformation & Status Rules

- **Soft Delete Rule**: In legacy, records were either physically deleted or marked with arbitrary codes (`situacion = '0'`). In v2, all deletions call `DELETE /{id}` which sets `is_active = FALSE` and records `deleted_at = NOW()` via EF Core interceptors / `StudentService`.
- **Status Value Mapping**:
  - Legacy `Aprobado` -> v2 `approved` (C# Enum `ProjectStatus.Approved`)
  - Legacy `En revisión` / `En revisió` -> v2 `under_review` (C# Enum `ProjectStatus.UnderReview`)
  - Legacy `Rechazado` -> v2 `rejected` (C# Enum `ProjectStatus.Rejected`)
  - Legacy `Pendiente` -> v2 `pending` (C# Enum `ProjectStatus.Pending`)
- **Charset Normalization**: All legacy text encoded in `latin1` (ISO-8859-1) is converted to `UTF8` in PostgreSQL 18, preserving all Spanish accents and `ñ` characters.

---

## 4. Role & Permissions Consolidation (4 Core Roles)

The legacy application roles (`tb_residentes`, `asesor`, `divisiones`, `gestion`, `subdireccion`, `admin`) are consolidated into **4 primary system roles**:

1. **`student` (Estudiantes)**: Student residents. Access to profile, proposal creation, weekly activity reporting, advisory session viewing, and document uploads.
2. **`advisor` (Académicos y Asesores)**: Academic advisors. Access to assigned projects, weekly progress validation, advisory session recording, grading (partial 1, partial 2, final), and document verification.
3. **`departmenthead` (Vinculación y Jefatura)**: Department heads and technology management/vinculación. Access to student eligibility verification, project dictamen/review, advisor assignment, document verification, presentation/release letter generation, metrics dashboard, and report exports.
4. **`admin` (Super Administrador)**: System administrators. Global system access (`IsAdmin = true`), user management, role/permission management, and catalog configuration.


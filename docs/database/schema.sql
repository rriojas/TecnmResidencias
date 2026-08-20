-- ==============================================================================
-- Database Schema v2 for Professional Residency System (TecNM)
-- Normalized to 3FN in English with Standard Audit, Order, and State Fields
-- Target RDBMS: PostgreSQL 18
-- Charset: UTF8
-- NOTA: la fuente de verdad del esquema actual es la migración EF
--       src/Migrations/*InitialSchema* (aplicada con `dotnet ef database update`).
--       Este archivo es referencia legacy (incluye tablas ya no modeladas:
--       companies, residency_applications, documents.student_id, etc.).
-- ==============================================================================

DROP TABLE IF EXISTS advisory_sessions CASCADE;
DROP TABLE IF EXISTS evaluations CASCADE;
DROP TABLE IF EXISTS documents CASCADE;
DROP TABLE IF EXISTS weekly_progress CASCADE;
DROP TABLE IF EXISTS weekly_activities CASCADE;
DROP TABLE IF EXISTS project_objectives CASCADE;
DROP TABLE IF EXISTS projects CASCADE;
DROP TABLE IF EXISTS residency_applications CASCADE;
DROP TABLE IF EXISTS advisors CASCADE;
DROP TABLE IF EXISTS students CASCADE;
DROP TABLE IF EXISTS companies CASCADE;
DROP TABLE IF EXISTS academic_periods CASCADE;
DROP TABLE IF EXISTS academic_careers CASCADE;
DROP TABLE IF EXISTS academic_departments CASCADE;
DROP TABLE IF EXISTS users CASCADE;

DROP TYPE IF EXISTS user_role CASCADE;
DROP TYPE IF EXISTS gender_type CASCADE;
DROP TYPE IF EXISTS advisor_type CASCADE;
DROP TYPE IF EXISTS application_status CASCADE;
DROP TYPE IF EXISTS project_status CASCADE;
DROP TYPE IF EXISTS objective_status CASCADE;
DROP TYPE IF EXISTS progress_status CASCADE;
DROP TYPE IF EXISTS document_type CASCADE;
DROP TYPE IF EXISTS document_status CASCADE;
DROP TYPE IF EXISTS evaluation_period CASCADE;

-- Custom Enum Types (PostgreSQL 18)
CREATE TYPE user_role AS ENUM ('student', 'advisor', 'department_head', 'admin');
CREATE TYPE gender_type AS ENUM ('male', 'female', 'other');
CREATE TYPE advisor_type AS ENUM ('internal', 'external');
CREATE TYPE application_status AS ENUM ('draft', 'submitted', 'under_review', 'approved', 'rejected');
CREATE TYPE project_status AS ENUM ('proposed', 'approved', 'in_progress', 'completed', 'cancelled');
CREATE TYPE objective_status AS ENUM ('pending', 'in_progress', 'completed');
CREATE TYPE progress_status AS ENUM ('pending', 'in_progress', 'completed');
CREATE TYPE document_type AS ENUM ('application', 'presentation_letter', 'acceptance_letter', 'report_1', 'report_2', 'report_3', 'final_report', 'release_letter', 'other');
CREATE TYPE document_status AS ENUM ('uploaded', 'under_review', 'approved', 'rejected');
CREATE TYPE evaluation_period AS ENUM ('partial_1', 'partial_2', 'final');

-- ------------------------------------------------------------------------------
-- 1. Users Table (Core Authentication)
-- ------------------------------------------------------------------------------
CREATE TABLE users (
  id BIGSERIAL PRIMARY KEY,
  email VARCHAR(150) NOT NULL,
  password_hash VARCHAR(255) NOT NULL,
  role user_role NOT NULL,
  avatar_path VARCHAR(255) NULL,
  is_active BOOLEAN DEFAULT TRUE NOT NULL,
  is_visible BOOLEAN DEFAULT TRUE NOT NULL,
  display_order INT DEFAULT 0 NOT NULL,
  created_by BIGINT NULL,
  updated_by BIGINT NULL,
  deleted_by BIGINT NULL,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  deleted_at TIMESTAMP WITH TIME ZONE NULL,
  CONSTRAINT uq_users_email UNIQUE (email)
);

-- ------------------------------------------------------------------------------
-- 2. Academic Departments Table
-- ------------------------------------------------------------------------------
CREATE TABLE academic_departments (
  id SERIAL PRIMARY KEY,
  code VARCHAR(20) NOT NULL,
  name VARCHAR(150) NOT NULL,
  department_head_id BIGINT NULL,
  is_active BOOLEAN DEFAULT TRUE NOT NULL,
  is_visible BOOLEAN DEFAULT TRUE NOT NULL,
  display_order INT DEFAULT 0 NOT NULL,
  created_by BIGINT NULL,
  updated_by BIGINT NULL,
  deleted_by BIGINT NULL,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  deleted_at TIMESTAMP WITH TIME ZONE NULL,
  CONSTRAINT uq_departments_code UNIQUE (code)
);

-- ------------------------------------------------------------------------------
-- 3. Academic Careers Table
-- ------------------------------------------------------------------------------
CREATE TABLE academic_careers (
  id SERIAL PRIMARY KEY,
  code VARCHAR(20) NOT NULL,
  name VARCHAR(150) NOT NULL,
  department_id INT NOT NULL,
  is_active BOOLEAN DEFAULT TRUE NOT NULL,
  is_visible BOOLEAN DEFAULT TRUE NOT NULL,
  display_order INT DEFAULT 0 NOT NULL,
  created_by BIGINT NULL,
  updated_by BIGINT NULL,
  deleted_by BIGINT NULL,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  deleted_at TIMESTAMP WITH TIME ZONE NULL,
  CONSTRAINT uq_careers_code UNIQUE (code),
  CONSTRAINT fk_careers_department FOREIGN KEY (department_id) REFERENCES academic_departments (id) ON DELETE CASCADE
);

-- ------------------------------------------------------------------------------
-- 4. Academic Periods Table
-- ------------------------------------------------------------------------------
CREATE TABLE academic_periods (
  id SERIAL PRIMARY KEY,
  name VARCHAR(50) NOT NULL,
  start_date DATE NOT NULL,
  end_date DATE NOT NULL,
  is_active BOOLEAN DEFAULT TRUE NOT NULL,
  is_visible BOOLEAN DEFAULT TRUE NOT NULL,
  display_order INT DEFAULT 0 NOT NULL,
  created_by BIGINT NULL,
  updated_by BIGINT NULL,
  deleted_by BIGINT NULL,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  deleted_at TIMESTAMP WITH TIME ZONE NULL,
  CONSTRAINT uq_periods_name UNIQUE (name)
);

-- ------------------------------------------------------------------------------
-- 5. Companies Table
-- ------------------------------------------------------------------------------
CREATE TABLE companies (
  id SERIAL PRIMARY KEY,
  rfc VARCHAR(20) NOT NULL,
  legal_name VARCHAR(250) NOT NULL,
  trade_name VARCHAR(250) NULL,
  industry_sector VARCHAR(100) NULL,
  company_type VARCHAR(100) NULL,
  address VARCHAR(250) NULL,
  neighborhood VARCHAR(150) NULL,
  postal_code VARCHAR(10) NULL,
  city VARCHAR(100) NULL,
  phone VARCHAR(30) NULL,
  email VARCHAR(150) NULL,
  contact_person VARCHAR(150) NULL,
  contact_position VARCHAR(150) NULL,
  external_advisor VARCHAR(150) NULL,
  external_advisor_position VARCHAR(150) NULL,
  is_active BOOLEAN DEFAULT TRUE NOT NULL,
  is_visible BOOLEAN DEFAULT TRUE NOT NULL,
  display_order INT DEFAULT 0 NOT NULL,
  created_by BIGINT NULL,
  updated_by BIGINT NULL,
  deleted_by BIGINT NULL,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  deleted_at TIMESTAMP WITH TIME ZONE NULL,
  CONSTRAINT uq_companies_rfc UNIQUE (rfc)
);

-- ------------------------------------------------------------------------------
-- 6. Students Table
-- ------------------------------------------------------------------------------
CREATE TABLE students (
  id BIGSERIAL PRIMARY KEY,
  user_id BIGINT NOT NULL,
  control_number VARCHAR(20) NOT NULL,
  first_name VARCHAR(100) NOT NULL,
  last_name_1 VARCHAR(100) NOT NULL,
  last_name_2 VARCHAR(100) NULL,
  curp VARCHAR(20) NOT NULL,
  gender gender_type NOT NULL,
  career_id INT NOT NULL,
  current_semester VARCHAR(20) NULL,
  academic_period_id INT NOT NULL,
  gpa NUMERIC(4,2) NULL,
  credits_completed INT DEFAULT 0 NOT NULL,
  total_credits INT DEFAULT 200 NOT NULL,
  social_security_number VARCHAR(50) NULL,
  phone VARCHAR(30) NULL,
  institutional_email VARCHAR(150) NULL,
  is_active BOOLEAN DEFAULT TRUE NOT NULL,
  is_visible BOOLEAN DEFAULT TRUE NOT NULL,
  display_order INT DEFAULT 0 NOT NULL,
  created_by BIGINT NULL,
  updated_by BIGINT NULL,
  deleted_by BIGINT NULL,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  deleted_at TIMESTAMP WITH TIME ZONE NULL,
  CONSTRAINT uq_students_user UNIQUE (user_id),
  CONSTRAINT uq_students_control UNIQUE (control_number),
  CONSTRAINT uq_students_curp UNIQUE (curp),
  CONSTRAINT fk_students_user FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE,
  CONSTRAINT fk_students_career FOREIGN KEY (career_id) REFERENCES academic_careers (id),
  CONSTRAINT fk_students_period FOREIGN KEY (academic_period_id) REFERENCES academic_periods (id)
);

-- ------------------------------------------------------------------------------
-- 7. Advisors Table
-- ------------------------------------------------------------------------------
CREATE TABLE advisors (
  id BIGSERIAL PRIMARY KEY,
  user_id BIGINT NOT NULL,
  department_id INT NOT NULL,
  advisor_type advisor_type DEFAULT 'internal' NOT NULL,
  full_name VARCHAR(200) NOT NULL,
  title VARCHAR(100) NULL,
  phone VARCHAR(30) NULL,
  is_active BOOLEAN DEFAULT TRUE NOT NULL,
  is_visible BOOLEAN DEFAULT TRUE NOT NULL,
  display_order INT DEFAULT 0 NOT NULL,
  created_by BIGINT NULL,
  updated_by BIGINT NULL,
  deleted_by BIGINT NULL,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  deleted_at TIMESTAMP WITH TIME ZONE NULL,
  CONSTRAINT uq_advisors_user UNIQUE (user_id),
  CONSTRAINT fk_advisors_user FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE,
  CONSTRAINT fk_advisors_department FOREIGN KEY (department_id) REFERENCES academic_departments (id)
);

-- ------------------------------------------------------------------------------
-- 8. Residency Applications Table
-- ------------------------------------------------------------------------------
CREATE TABLE residency_applications (
  id BIGSERIAL PRIMARY KEY,
  student_id BIGINT NOT NULL,
  academic_period_id INT NOT NULL,
  company_id INT NOT NULL,
  status application_status DEFAULT 'draft' NOT NULL,
  submitted_at TIMESTAMP WITH TIME ZONE NULL,
  reviewed_at TIMESTAMP WITH TIME ZONE NULL,
  review_notes TEXT NULL,
  is_active BOOLEAN DEFAULT TRUE NOT NULL,
  is_visible BOOLEAN DEFAULT TRUE NOT NULL,
  display_order INT DEFAULT 0 NOT NULL,
  created_by BIGINT NULL,
  updated_by BIGINT NULL,
  deleted_by BIGINT NULL,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  deleted_at TIMESTAMP WITH TIME ZONE NULL,
  CONSTRAINT uq_application_student_period UNIQUE (student_id, academic_period_id),
  CONSTRAINT fk_applications_student FOREIGN KEY (student_id) REFERENCES students (id) ON DELETE CASCADE,
  CONSTRAINT fk_applications_period FOREIGN KEY (academic_period_id) REFERENCES academic_periods (id),
  CONSTRAINT fk_applications_company FOREIGN KEY (company_id) REFERENCES companies (id)
);

-- ------------------------------------------------------------------------------
-- 9. Projects Table
-- ------------------------------------------------------------------------------
CREATE TABLE projects (
  id BIGSERIAL PRIMARY KEY,
  student_id BIGINT NOT NULL,
  advisor_id BIGINT NULL,
  title VARCHAR(250) NOT NULL,
  project_type VARCHAR(100) NULL,
  problem_statement TEXT NULL,
  justification TEXT NULL,
  general_objective TEXT NULL,
  status project_status DEFAULT 'proposed' NOT NULL,
  start_date DATE NULL,
  end_date DATE NULL,
  is_active BOOLEAN DEFAULT TRUE NOT NULL,
  is_visible BOOLEAN DEFAULT TRUE NOT NULL,
  display_order INT DEFAULT 0 NOT NULL,
  created_by BIGINT NULL,
  updated_by BIGINT NULL,
  deleted_by BIGINT NULL,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  deleted_at TIMESTAMP WITH TIME ZONE NULL,
  CONSTRAINT uq_projects_student UNIQUE (student_id),
  CONSTRAINT fk_projects_student FOREIGN KEY (student_id) REFERENCES students (id) ON DELETE CASCADE,
  CONSTRAINT fk_projects_advisor FOREIGN KEY (advisor_id) REFERENCES advisors (id) ON DELETE SET NULL
);

-- ------------------------------------------------------------------------------
-- 10. Project Objectives Table
-- ------------------------------------------------------------------------------
CREATE TABLE project_objectives (
  id BIGSERIAL PRIMARY KEY,
  project_id BIGINT NOT NULL,
  objective_number INT NOT NULL,
  description TEXT NOT NULL,
  status objective_status DEFAULT 'pending' NOT NULL,
  notes TEXT NULL,
  is_active BOOLEAN DEFAULT TRUE NOT NULL,
  is_visible BOOLEAN DEFAULT TRUE NOT NULL,
  display_order INT DEFAULT 0 NOT NULL,
  created_by BIGINT NULL,
  updated_by BIGINT NULL,
  deleted_by BIGINT NULL,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  deleted_at TIMESTAMP WITH TIME ZONE NULL,
  CONSTRAINT uq_project_objective_number UNIQUE (project_id, objective_number),
  CONSTRAINT fk_objectives_project FOREIGN KEY (project_id) REFERENCES projects (id) ON DELETE CASCADE
);

-- ------------------------------------------------------------------------------
-- 11. Weekly Activities Table
-- ------------------------------------------------------------------------------
CREATE TABLE weekly_activities (
  id BIGSERIAL PRIMARY KEY,
  project_id BIGINT NOT NULL,
  activity_number INT NOT NULL,
  title VARCHAR(250) NOT NULL,
  description TEXT NULL,
  planned_weeks INT DEFAULT 1 NOT NULL,
  is_active BOOLEAN DEFAULT TRUE NOT NULL,
  is_visible BOOLEAN DEFAULT TRUE NOT NULL,
  display_order INT DEFAULT 0 NOT NULL,
  created_by BIGINT NULL,
  updated_by BIGINT NULL,
  deleted_by BIGINT NULL,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  deleted_at TIMESTAMP WITH TIME ZONE NULL,
  CONSTRAINT uq_project_activity_number UNIQUE (project_id, activity_number),
  CONSTRAINT fk_activities_project FOREIGN KEY (project_id) REFERENCES projects (id) ON DELETE CASCADE
);

-- ------------------------------------------------------------------------------
-- 12. Weekly Progress Table (Unpivoted 26 weeks)
-- ------------------------------------------------------------------------------
CREATE TABLE weekly_progress (
  id BIGSERIAL PRIMARY KEY,
  activity_id BIGINT NOT NULL,
  week_number INT NOT NULL,
  status progress_status DEFAULT 'pending' NOT NULL,
  percentage NUMERIC(5,2) DEFAULT 0.00 NOT NULL,
  notes TEXT NULL,
  is_active BOOLEAN DEFAULT TRUE NOT NULL,
  is_visible BOOLEAN DEFAULT TRUE NOT NULL,
  display_order INT DEFAULT 0 NOT NULL,
  created_by BIGINT NULL,
  updated_by BIGINT NULL,
  deleted_by BIGINT NULL,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  deleted_at TIMESTAMP WITH TIME ZONE NULL,
  CONSTRAINT uq_activity_week UNIQUE (activity_id, week_number),
  CONSTRAINT fk_progress_activity FOREIGN KEY (activity_id) REFERENCES weekly_activities (id) ON DELETE CASCADE
);

-- ------------------------------------------------------------------------------
-- 13. Documents Table (Unpivoted Files)
-- ------------------------------------------------------------------------------
CREATE TABLE documents (
  id BIGSERIAL PRIMARY KEY,
  student_id BIGINT NOT NULL,
  project_id BIGINT NULL,
  document_type document_type NOT NULL,
  file_path VARCHAR(255) NOT NULL,
  file_name VARCHAR(255) NOT NULL,
  file_size INT NOT NULL,
  mime_type VARCHAR(100) NOT NULL,
  status document_status DEFAULT 'uploaded' NOT NULL,
  rejection_reason TEXT NULL,
  uploaded_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  is_active BOOLEAN DEFAULT TRUE NOT NULL,
  is_visible BOOLEAN DEFAULT TRUE NOT NULL,
  display_order INT DEFAULT 0 NOT NULL,
  created_by BIGINT NULL,
  updated_by BIGINT NULL,
  deleted_by BIGINT NULL,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  deleted_at TIMESTAMP WITH TIME ZONE NULL,
  CONSTRAINT fk_documents_student FOREIGN KEY (student_id) REFERENCES students (id) ON DELETE CASCADE,
  CONSTRAINT fk_documents_project FOREIGN KEY (project_id) REFERENCES projects (id) ON DELETE SET NULL
);

-- ------------------------------------------------------------------------------
-- 14. Evaluations Table (Unpivoted Grades)
-- ------------------------------------------------------------------------------
CREATE TABLE evaluations (
  id BIGSERIAL PRIMARY KEY,
  project_id BIGINT NOT NULL,
  advisor_id BIGINT NOT NULL,
  evaluation_type evaluation_period NOT NULL,
  score NUMERIC(5,2) NOT NULL,
  observations TEXT NULL,
  evaluated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  is_active BOOLEAN DEFAULT TRUE NOT NULL,
  is_visible BOOLEAN DEFAULT TRUE NOT NULL,
  display_order INT DEFAULT 0 NOT NULL,
  created_by BIGINT NULL,
  updated_by BIGINT NULL,
  deleted_by BIGINT NULL,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  deleted_at TIMESTAMP WITH TIME ZONE NULL,
  CONSTRAINT uq_project_evaluation_type UNIQUE (project_id, evaluation_type),
  CONSTRAINT fk_evaluations_project FOREIGN KEY (project_id) REFERENCES projects (id) ON DELETE CASCADE,
  CONSTRAINT fk_evaluations_advisor FOREIGN KEY (advisor_id) REFERENCES advisors (id)
);

-- ------------------------------------------------------------------------------
-- 15. Advisory Sessions Table
-- ------------------------------------------------------------------------------
CREATE TABLE advisory_sessions (
  id BIGSERIAL PRIMARY KEY,
  project_id BIGINT NOT NULL,
  advisor_id BIGINT NOT NULL,
  session_date DATE NOT NULL,
  topics_discussed TEXT NOT NULL,
  commitments TEXT NULL,
  proof_document_id BIGINT NULL,
  is_active BOOLEAN DEFAULT TRUE NOT NULL,
  is_visible BOOLEAN DEFAULT TRUE NOT NULL,
  display_order INT DEFAULT 0 NOT NULL,
  created_by BIGINT NULL,
  updated_by BIGINT NULL,
  deleted_by BIGINT NULL,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
  deleted_at TIMESTAMP WITH TIME ZONE NULL,
  CONSTRAINT fk_sessions_project FOREIGN KEY (project_id) REFERENCES projects (id) ON DELETE CASCADE,
  CONSTRAINT fk_sessions_advisor FOREIGN KEY (advisor_id) REFERENCES advisors (id),
  CONSTRAINT fk_sessions_document FOREIGN KEY (proof_document_id) REFERENCES documents (id) ON DELETE SET NULL
);

-- ------------------------------------------------------------------------------
-- 16. EF Core Migrations History Table
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" VARCHAR(150) NOT NULL,
    "ProductVersion" VARCHAR(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260814005732_InitialSchema', '10.0.0')
ON CONFLICT DO NOTHING;


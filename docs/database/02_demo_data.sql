-- ==============================================================================
-- Script 2: Datos de Prueba Demo (Entorno de Desarrollo)
-- Sistema de Gestión de Residencias Profesionales TecNM v2
-- RDBMS: PostgreSQL 18+
-- Charset: UTF-8
-- ==============================================================================

BEGIN;

-- ------------------------------------------------------------------------------
-- 1. Usuarios Demo (Estudiante y Asesor)
-- ------------------------------------------------------------------------------

-- Estudiante: juan.perez@monclova.tecnm.mx (Contraseña: 20680123)
-- Hash BCrypt para '20680123': $2a$11$w1pQ6b.u20kX/aC1qQWcZeB7e.B9pP9qZ0R0t0u0v0w0x0y0z0
INSERT INTO users (email, password_hash, role, is_admin, is_active)
VALUES (
    'juan.perez@monclova.tecnm.mx',
    '$2a$11$w1pQ6b.u20kX/aC1qQWcZeB7e.B9pP9qZ0R0t0u0v0w0x0y0z0',
    'Student',
    false,
    true
)
ON CONFLICT (email) DO NOTHING;

INSERT INTO user_roles (user_id, role_id, is_active)
SELECT u.id, r.id, true
FROM users u, roles r
WHERE u.email = 'juan.perez@monclova.tecnm.mx' AND r.code = 'student'
ON CONFLICT (user_id, role_id) DO NOTHING;

INSERT INTO students (user_id, control_number, first_name, last_name_1, last_name_2, curp, career_id, academic_period_id, gpa, is_active)
SELECT 
    u.id,
    '20680123',
    'Juan',
    'Pérez',
    'Gómez',
    'PEGJ020101HMCRRR01',
    1,
    1,
    92.50,
    true
FROM users u
WHERE u.email = 'juan.perez@monclova.tecnm.mx'
ON CONFLICT (control_number) DO NOTHING;

-- Asesor: fernando.rivera@monclova.tecnm.mx (Contraseña: Advisor2026!)
-- Hash BCrypt para 'Advisor2026!': $2a$11$X0y1z2a3b4c5d6e7f8g9h0i1j2k3l4m5n6o7p8q9r0s1t2u3v4w5x
INSERT INTO users (email, password_hash, role, is_admin, is_active)
VALUES (
    'fernando.rivera@monclova.tecnm.mx',
    '$2a$11$X0y1z2a3b4c5d6e7f8g9h0i1j2k3l4m5n6o7p8q9r0s1t2u3v4w5x',
    'Advisor',
    false,
    true
)
ON CONFLICT (email) DO NOTHING;

INSERT INTO user_roles (user_id, role_id, is_active)
SELECT u.id, r.id, true
FROM users u, roles r
WHERE u.email = 'fernando.rivera@monclova.tecnm.mx' AND r.code = 'advisor'
ON CONFLICT (user_id, role_id) DO NOTHING;

INSERT INTO advisors (user_id, department_id, advisor_type, full_name, title, phone, is_active)
SELECT 
    u.id,
    1,
    'Internal',
    'Fernando Rivera López',
    'M.C.',
    '8661234567',
    true
FROM users u
WHERE u.email = 'fernando.rivera@monclova.tecnm.mx'
AND NOT EXISTS (SELECT 1 FROM advisors WHERE user_id = u.id);

-- ------------------------------------------------------------------------------
-- 2. Catálogo Demo de Empresas Receptoras
-- ------------------------------------------------------------------------------
INSERT INTO companies (name, rfc, sector, address, contact_name, contact_email, contact_phone, is_active)
VALUES 
('Servicios Tecnológicos e Innovación Industrial S.A. de C.V.', 'STI150610MH2', 'Desarrollo de Software / TI', 'Av. Tecnológico #1200, Monclova, Coahuila', 'Ing. Miguel Ángel Perales', 'mperales@sti-coahuila.mx', '866-632-9900', true),
('Altos Hornos de México S.A.B. de C.V. (AHMSA)', 'AHM441231AB1', 'Siderúrgico / Metalmecánico', 'Prolongación Juárez s/n, Monclova, Coahuila', 'Ing. Carlos Mendoza Silva', 'cmendoza@ahmsa.com', '866-649-3000', true),
('Ternium México S.A. de C.V.', 'TME050209TX1', 'Siderúrgico / Industrial', 'Carretera 57 Km 12, Monclova, Coahuila', 'Lic. Elena Torres Cantú', 'etorres@ternium.com.mx', '866-649-8000', true),
('Gunderson-GIMSA S.A. de C.V.', 'GGI930815KL9', 'Ferroviario / Manufactura', 'Av. Industrial #850, Monclova, Coahuila', 'Ing. Roberto Ramírez Ramos', 'rramirez@gunderson.com.mx', '866-633-1200', true),
('Technotrim de México S. de R.L. de C.V.', 'TME981102RT4', 'Automotriz / Textil', 'Parque Industrial Monclova, Coahuila', 'Lic. Sofia Villarreal Reyes', 'svillarreal@technotrim.com', '866-641-5500', true)
ON CONFLICT DO NOTHING;

-- ------------------------------------------------------------------------------
-- 3. Anteproyecto Demo con Objetivos y Cronograma de 26 Semanas
-- ------------------------------------------------------------------------------
INSERT INTO projects (student_id, advisor_id, company_id, title, project_type, problem_statement, justification, general_objective, status, start_date, end_date, is_active)
SELECT 
    s.id,
    a.id,
    c.id,
    'Sistema Integral de Gestión de Residencias Profesionales',
    'Desarrollo Tecnológico',
    'La División carece de una herramienta unificada para el registro, dictamen y seguimiento de residencias profesionales.',
    'Automatizar el proceso de residencia profesional reduce tiempos administrativos y mejora la trazabilidad de los expedientes.',
    'Desarrollar un sistema web integral para la gestión del proceso de residencia profesional del TecNM.',
    'in_progress',
    '2026-01-15 00:00:00+00',
    '2026-06-15 00:00:00+00',
    true
FROM students s
CROSS JOIN advisors a
CROSS JOIN companies c
WHERE s.control_number = '20680123' 
  AND a.full_name = 'Fernando Rivera López' 
  AND c.rfc = 'STI150610MH2'
  AND NOT EXISTS (SELECT 1 FROM projects WHERE student_id = s.id);

-- Objetivos Específicos del Proyecto Demo
INSERT INTO project_objectives (project_id, objective_number, description, status, is_active)
SELECT p.id, 1, 'Analizar el proceso actual de gestión de residencias profesionales.', 'completed', true FROM projects p WHERE p.title = 'Sistema Integral de Gestión de Residencias Profesionales'
UNION ALL
SELECT p.id, 2, 'Diseñar la arquitectura del sistema y el modelo de datos relacional.', 'in_progress', true FROM projects p WHERE p.title = 'Sistema Integral de Gestión de Residencias Profesionales'
UNION ALL
SELECT p.id, 3, 'Implementar y desplegar los módulos del sistema en el campus.', 'pending', true FROM projects p WHERE p.title = 'Sistema Integral de Gestión de Residencias Profesionales'
ON CONFLICT (project_id, objective_number) DO NOTHING;

-- Actividades del Cronograma Demo (4 Actividades)
INSERT INTO weekly_activities (project_id, activity_number, title, is_active)
SELECT p.id, 1, 'Análisis de requerimientos del sistema de residencias', true FROM projects p WHERE p.title = 'Sistema Integral de Gestión de Residencias Profesionales'
UNION ALL
SELECT p.id, 2, 'Diseño de base de datos y arquitectura de módulos', true FROM projects p WHERE p.title = 'Sistema Integral de Gestión de Residencias Profesionales'
UNION ALL
SELECT p.id, 3, 'Implementación del registro y dictamen de anteproyectos', true FROM projects p WHERE p.title = 'Sistema Integral de Gestión de Residencias Profesionales'
UNION ALL
SELECT p.id, 4, 'Implementación del cronograma de actividades de 26 semanas', true FROM projects p WHERE p.title = 'Sistema Integral de Gestión de Residencias Profesionales'
ON CONFLICT DO NOTHING;

-- Avances Semanales para cada Actividad (Semanas 1 a 26)
INSERT INTO weekly_progress (activity_id, week_number, status, is_active)
SELECT wa.id, w.week, 'pending', true
FROM weekly_activities wa
CROSS JOIN generate_series(1, 26) AS w(week)
WHERE wa.project_id = (SELECT id FROM projects WHERE title = 'Sistema Integral de Gestión de Residencias Profesionales')
ON CONFLICT (activity_id, week_number) DO NOTHING;

COMMIT;

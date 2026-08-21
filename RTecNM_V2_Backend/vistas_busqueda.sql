-- Vistas de Búsqueda Global / Universal para TecNM Residency System v2 (PostgreSQL 18)
-- Nota: Se omiten filtros is_active estáticos para permitir filtrado dinámico (Activos / Inactivos) en runtime.

CREATE OR REPLACE VIEW vw_search_students AS
SELECT 
    s.id AS id,
    s.control_number AS control_number,
    CONCAT(s.first_name, ' ', s.last_name_1, COALESCE(' ' || s.last_name_2, '')) AS full_name,
    COALESCE(u.email, '') AS email,
    COALESCE(s.curp, '') AS curp,
    s.career_id AS career_id,
    s.is_active AS is_active
FROM students s
LEFT JOIN users u ON s.user_id = u.id;

CREATE OR REPLACE VIEW vw_search_advisors AS
SELECT 
    a.id AS id,
    a.full_name AS full_name,
    COALESCE(a.title, '') AS title,
    a.advisor_type::text AS advisor_type,
    a.department_id AS department_id,
    COALESCE(u.email, '') AS email,
    COALESCE(a.phone, '') AS phone,
    a.is_active AS is_active
FROM advisors a
LEFT JOIN users u ON a.user_id = u.id;

DROP VIEW IF EXISTS vw_search_projects CASCADE;
CREATE OR REPLACE VIEW vw_search_projects AS
SELECT 
    p.id AS id,
    p.title AS title,
    COALESCE(p.project_type, '') AS project_type,
    p.status::text AS status,
    CONCAT(s.first_name, ' ', s.last_name_1) AS student_name,
    COALESCE(a.full_name, 'Sin Asignar') AS advisor_name,
    COALESCE(c.name, 'Sin Empresa') AS company_name,
    p.advisor_id AS advisor_id,
    p.is_active AS is_active
FROM projects p
LEFT JOIN students s ON p.student_id = s.id
LEFT JOIN advisors a ON p.advisor_id = a.id
LEFT JOIN companies c ON p.company_id = c.id
WHERE p.status::text <> 'draft';

CREATE OR REPLACE VIEW vw_search_companies AS
SELECT 
    c.id AS id,
    c.name AS name,
    c.rfc AS rfc,
    COALESCE(c.sector, '') AS sector,
    COALESCE(c.contact_name, '') AS contact_name,
    COALESCE(c.contact_email, '') AS contact_email,
    c.is_active AS is_active
FROM companies c;

-- Seed script for TecNM Residency System v2
-- Credenciales de usuarios semilla:
--   admin@monclova.tecnm.mx      / Admin2026!
--   juan.perez@monclova.tecnm.mx / 20680123  (número de control)
--   fernando.rivera@monclova.tecnm.mx / Advisor2026!

INSERT INTO academic_departments (id, code, name, is_active, is_visible, display_order) VALUES (1, 'DEP-SYS', 'Division de Sistemas Computacionales', true, true, 1);
INSERT INTO academic_departments (id, code, name, is_active, is_visible, display_order) VALUES (2, 'DEP-IND', 'Division de Ingenieria Industrial', true, true, 2);
INSERT INTO academic_careers (id, code, name, department_id, is_active, is_visible) VALUES
(1, 'IIN', 'Ingeniería en Informática', 1, true, true),
(2, 'IND', 'Ingeniería Industrial', 2, true, true),
(3, 'IEE', 'Ingeniería Electrónica', 1, true, true),
(4, 'IMC', 'Ingeniería Mecatrónica', 1, true, true),
(5, 'IMA', 'Ingeniería Mecánica', 1, true, true),
(6, 'IER', 'Ingeniería en Energias Renovables', 1, true, true),
(7, 'IGE', 'Ingeniería en Gestión Empresarial', 2, true, true);
INSERT INTO academic_periods (id, name, start_date, end_date, is_active, is_visible) VALUES
(1, 'Ene-Jun 2026', '2026-01-15', '2026-06-15', true, true),
(2, 'Ago-Dic 2026', '2026-08-15', '2026-12-15', false, true);
INSERT INTO users (id, email, password_hash, role, is_active, is_visible) VALUES
(1, 'admin@monclova.tecnm.mx', '$2a$11$E.UuEFMDp09gmrvLYy80Fe0j/SvPmddfmjz0xflEWWlmVaDU.bhZm', 'admin', true, true),
(2, 'juan.perez@monclova.tecnm.mx', '$2a$11$cQonQSxf91MZ97gBGoxCE.nBjme.rphyS30C0N4mqv2Wx5brbBPo.', 'student', true, true),
(3, 'fernando.rivera@monclova.tecnm.mx', '$2a$11$yGEkyswzkwYYnkUoY/JhjehuVcWw1geHFEXY9XiUA4cdkJSey8u8.', 'advisor', true, true);

INSERT INTO students (id, user_id, control_number, first_name, last_name_1, last_name_2, curp, gender, career_id, academic_period_id, gpa, is_active, is_visible) VALUES
(1, 2, '20680123', 'Juan', 'Pérez', 'López', 'PELJ040101HPLRR01', 'male', 1, 1, 92.50, true, true);

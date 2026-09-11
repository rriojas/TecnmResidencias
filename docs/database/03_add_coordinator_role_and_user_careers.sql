-- ==============================================================================
-- 03_add_coordinator_role_and_user_careers.sql
-- Migración Idempotente: Rol 'coordinadora' y tabla 'user_careers'
-- ==============================================================================

-- 1. Crear tabla relacional user_careers para soportar múltiples carreras por usuario
CREATE TABLE IF NOT EXISTS user_careers (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    career_id BIGINT NOT NULL REFERENCES careers(id) ON DELETE CASCADE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    is_visible BOOLEAN NOT NULL DEFAULT TRUE,
    display_order INT NOT NULL DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMP WITH TIME ZONE NULL,
    created_by BIGINT NULL,
    updated_by BIGINT NULL,
    deleted_by BIGINT NULL,
    CONSTRAINT uq_user_careers UNIQUE (user_id, career_id)
);

CREATE INDEX IF NOT EXISTS ix_user_careers_user_id ON user_careers(user_id);
CREATE INDEX IF NOT EXISTS ix_user_careers_career_id ON user_careers(career_id);

-- 2. Insertar rol 'coordinadora' en el catálogo de roles
INSERT INTO roles (code, name, description, is_active, display_order)
VALUES ('coordinadora', 'Coordinadora de Carrera', 'Acceso de solo lectura restringido a sus carreras asignadas', true, 7)
ON CONFLICT (code) DO UPDATE 
SET name = EXCLUDED.name, description = EXCLUDED.description;

-- 3. Asignar al rol 'coordinadora' los permisos de todos los módulos excepto los sensibles de administración
INSERT INTO role_permissions (role_id, permission_id, is_active)
SELECT (SELECT id FROM roles WHERE code = 'coordinadora'), p.id, true
FROM permissions p
WHERE p.slug IN (
    'students.profile.view', 'advisors.manage', 'companies.view', 'projects.proposals',
    'projects.review', 'activities.schedule', 'evaluations.advisories', 'advisories.session.view',
    'evaluations.grading', 'evaluations.summary.view', 'documents.digital', 'admin.reports', 'reports.export.excel'
)
ON CONFLICT (role_id, permission_id) DO NOTHING;

-- Purgar cualquier permiso sensible asignado previamente
DELETE FROM role_permissions
WHERE role_id = (SELECT id FROM roles WHERE code = 'coordinadora')
  AND permission_id IN (
    SELECT id FROM permissions WHERE slug IN ('admin.roles', 'admin.careers', 'admin.settings', 'admin.users.manage')
);

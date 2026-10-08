-- ==============================================================================
-- Script: confirm_active_project_solicitudes.sql
-- Descripción: Confirma y registra automáticamente el documento 'solicitud'
--              (Solicitud de Residencia) para todos los anteproyectos que ya
--              hayan iniciado su proceso (enviados a revisión o aprobados)
--              y que no estén cancelados ni eliminados.
-- ==============================================================================

BEGIN;

-- 1. Insertar registro de Solicitud para anteproyectos activos que no la tengan
INSERT INTO documents (
    project_id,
    document_type,
    file_path,
    file_name,
    file_size,
    content_type,
    status,
    is_active,
    is_visible,
    display_order,
    created_at,
    updated_at,
    uploaded_at,
    created_by
)
SELECT 
    p.id,
    'solicitud',
    'uploads/generated/solicitud_' || p.id || '.pdf',
    'Solicitud_Anteproyecto_' || p.id || '.pdf',
    125000,
    'application/pdf',
    'approved',
    true,
    true,
    1,
    p.created_at,
    NOW(),
    p.created_at,
    p.student_id
FROM projects p
WHERE p.deleted_at IS NULL 
  AND LOWER(p.status) NOT IN ('cancelled', 'cancelado', 'draft', 'borrador')
  AND NOT EXISTS (
      SELECT 1 FROM documents d 
      WHERE d.project_id = p.id 
        AND d.is_active = true 
        AND LOWER(d.document_type) = 'solicitud'
  );

-- 2. Asegurar que cualquier solicitud existente de proyectos activos esté marcada como 'approved'
UPDATE documents d
SET status = 'approved',
    updated_at = NOW()
FROM projects p
WHERE d.project_id = p.id
  AND d.is_active = true
  AND LOWER(d.document_type) = 'solicitud'
  AND p.deleted_at IS NULL
  AND LOWER(p.status) NOT IN ('cancelled', 'cancelado', 'draft', 'borrador')
  AND d.status <> 'approved';

COMMIT;

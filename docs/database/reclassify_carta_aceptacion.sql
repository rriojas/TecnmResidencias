-- ==============================================================================
-- Script: reclassify_carta_aceptacion.sql
-- Descripción: Reclasifica automáticamente documentos que alumnos subieron como
--              'dictamen', 'solicitud' u 'otro' cuando su archivo es realmente una
--              Carta de Aceptación ('CARTA ACEPTACION...').
-- ==============================================================================

BEGIN;

UPDATE documents
SET document_type = 'carta_aceptacion',
    updated_at = NOW()
WHERE is_active = true
  AND document_type IN ('dictamen', 'solicitud', 'otro')
  AND (
      file_name ILIKE '%carta%aceptacion%' 
      OR file_name ILIKE '%carta_aceptacion%'
      OR file_name ILIKE '%aceptacion%'
  );

COMMIT;

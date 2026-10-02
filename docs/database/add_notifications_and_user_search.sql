-- ==============================================================================
-- TecNM Residencias v2 - Migración Idempotente para Notificaciones y Búsqueda Global
-- Garantía: No elimina datos existentes, agrega tablas/columnas e índices faltantes.
-- ==============================================================================

DO $$ 
BEGIN
    -- 1. Tabla de Notificaciones
    CREATE TABLE IF NOT EXISTS notifications (
        id BIGSERIAL PRIMARY KEY,
        title VARCHAR(200) NOT NULL,
        description TEXT NOT NULL,
        expires_at TIMESTAMP WITH TIME ZONE NOT NULL,
        target_roles VARCHAR(255) NOT NULL,
        career_id BIGINT NULL REFERENCES careers(id) ON DELETE SET NULL,
        residency_modality VARCHAR(100) NULL,
        sender_id BIGINT NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
        type VARCHAR(50) NOT NULL DEFAULT 'manual',
        target_user_id BIGINT NULL REFERENCES users(id) ON DELETE SET NULL,
        is_active BOOLEAN NOT NULL DEFAULT TRUE,
        is_visible BOOLEAN NOT NULL DEFAULT TRUE,
        display_order INT NOT NULL DEFAULT 0,
        created_by BIGINT NULL,
        updated_by BIGINT NULL,
        deleted_by BIGINT NULL,
        created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
        updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
        deleted_at TIMESTAMP WITH TIME ZONE NULL
    );

    -- Asegurar columna target_user_id si la tabla ya existía previamente
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'notifications' AND column_name = 'target_user_id'
    ) THEN
        ALTER TABLE notifications ADD COLUMN target_user_id BIGINT NULL REFERENCES users(id) ON DELETE SET NULL;
    END IF;

    CREATE INDEX IF NOT EXISTS ix_notifications_expires_at ON notifications(expires_at);
    CREATE INDEX IF NOT EXISTS ix_notifications_type ON notifications(type);
    CREATE INDEX IF NOT EXISTS ix_notifications_target_user_id ON notifications(target_user_id);

    -- 2. Tabla de lecturas de notificaciones
    CREATE TABLE IF NOT EXISTS user_notification_reads (
        id BIGSERIAL PRIMARY KEY,
        notification_id BIGINT NOT NULL REFERENCES notifications(id) ON DELETE CASCADE,
        user_id BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
        read_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
        CONSTRAINT uq_user_notification_reads UNIQUE (user_id, notification_id)
    );
    CREATE INDEX IF NOT EXISTS ix_user_notification_reads_user_id ON user_notification_reads(user_id);

    -- 3. Vistas de Búsqueda Global / Universal
    DROP VIEW IF EXISTS vw_search_students CASCADE;
    CREATE OR REPLACE VIEW vw_search_students AS
    SELECT 
        s.id AS id,
        s.user_id AS user_id,
        s.control_number AS control_number,
        CONCAT(s.first_name, ' ', s.last_name_1, COALESCE(' ' || s.last_name_2, '')) AS full_name,
        COALESCE(u.email, '') AS email,
        COALESCE(s.curp, '') AS curp,
        s.career_id AS career_id,
        s.is_active AS is_active
    FROM students s
    LEFT JOIN users u ON s.user_id = u.id;

    DROP VIEW IF EXISTS vw_search_users CASCADE;
    CREATE OR REPLACE VIEW vw_search_users AS
    SELECT 
        u.id AS id,
        u.id AS user_id,
        COALESCE(u.control_number, '') AS control_number,
        TRIM(CONCAT(COALESCE(u.first_name, ''), ' ', COALESCE(u.last_name, ''), COALESCE(' ' || u.last_name_2, ''))) AS full_name,
        COALESCE(u.email, '') AS email,
        u.role::text AS role,
        COALESCE(c.name, '') AS career_name,
        u.is_active AS is_active
    FROM users u
    LEFT JOIN careers c ON u.career_id = c.id;

    -- 4. Sembrado seguro de configuración SMTP (mantener mock en true por defecto para no enviar correos no deseados)
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'system_settings') THEN
        INSERT INTO system_settings (key, value, description, updated_at)
        VALUES 
            ('smtp.host', 'smtp.office365.com', 'Host SMTP', CURRENT_TIMESTAMP),
            ('smtp.port', '587', 'Puerto SMTP', CURRENT_TIMESTAMP),
            ('smtp.sender_name', 'TecNM Residencias', 'Nombre remitente', CURRENT_TIMESTAMP),
            ('smtp.sender_email', 'noreply@ejemplo.tecnm.mx', 'Correo remitente', CURRENT_TIMESTAMP),
            ('smtp.username', 'noreply@ejemplo.tecnm.mx', 'Usuario SMTP', CURRENT_TIMESTAMP),
            ('smtp.password', 'TU_CONTRASEÑA_AQUI', 'Contraseña SMTP', CURRENT_TIMESTAMP),
            ('smtp.enable_ssl', 'true', 'Habilitar SSL/TLS', CURRENT_TIMESTAMP),
            ('smtp.use_mock', 'true', 'Simulación de correos sin envío real', CURRENT_TIMESTAMP)
        ON CONFLICT (key) DO NOTHING;
    END IF;

END $$;

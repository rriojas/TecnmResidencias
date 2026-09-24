-- Migración segura para aplicaciones en producción: No altera datos existentes
ALTER TABLE students 
ADD COLUMN IF NOT EXISTS formato_29_deadline TIMESTAMP WITH TIME ZONE NULL,
ADD COLUMN IF NOT EXISTS formato_30_deadline TIMESTAMP WITH TIME ZONE NULL;

# Sistema de Notificaciones TecNM Residencias v2
## Arquitectura Actual (Campana Web) y Guía de Activación de Correo Electrónico

---

## 1. Visión General y Requerimientos

El sistema cuenta con dos subsistemas principales:

1. **Módulo de Emisión y Gestión (Roles: `admin` y `academic`)**:
   - Redacción de avisos con: **Tema (Título)**, **Descripción**, **Fecha de Expiración** (después de la cual deja de mostrarse).
   - **Segmentación de Destinatarios**:
     - Por **Rol** (Estudiantes, Asesores, Jefes de Carrera, etc., o Todos).
     - Por **Carrera** (opcional, aplicable a estudiantes y personal de dicha carrera).
     - Por **Modalidad de Residencia** (ej. Regular/Empresa, Acreditación InnovaTecNM, HackaTec, etc.).
   - **Historial de Emisiones**: Registro con fecha, emisor, criterios de asignación y estado (vigente/expirada).

2. **Módulo de Consumo (Campana en `AppHeader.vue`)**:
   - **Visibilidad Inteligente**: El icono de campana permanece oculto (`v-if="pendingCount > 0"`) y se vuelve visible únicamente si el usuario tiene alertas pendientes.
   - **Alertas Mixtas**:
     - *Avisos manuales*: Emitidos por el personal académico/admin dentro de su vigencia.
     - *Avisos automáticos del sistema*: Documentos faltantes, proximidad de fecha límite para entrega de formatos (ej. Formato 29 / 30).
   - **Protocolo de Lectura/Descarte**: Al hacer clic en "Marcar como leída", la notificación desaparece inmediatamente de la campana del usuario.
   - **Exclusión Innovatec**: La tarjeta o alerta informativa de InnovaTecNM permanece en el `DashboardView.vue` y no interfiere con la campana.

---

## 2. Modelo de Datos Backend (PostgreSQL / EF Core)

### Tabla `notifications` (Hereda `BaseEntity`)
```sql
CREATE TABLE IF NOT EXISTS notifications (
    id BIGSERIAL PRIMARY KEY,
    title VARCHAR(200) NOT NULL,
    description TEXT NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    target_roles VARCHAR(255) NOT NULL,           -- Formato JSON o CSV: "student,advisor" o "all"
    career_id BIGINT NULL REFERENCES careers(id), -- NULL = todas las carreras
    residency_modality VARCHAR(100) NULL,         -- NULL = todas, 'innovatec', 'regular', etc.
    sender_id BIGINT NOT NULL REFERENCES users(id),
    type VARCHAR(50) NOT NULL DEFAULT 'manual',    -- 'manual' | 'system'
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    is_visible BOOLEAN NOT NULL DEFAULT TRUE,
    display_order INT NOT NULL DEFAULT 0,
    created_by BIGINT NULL,
    updated_by BIGINT NULL,
    deleted_by BIGINT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ NULL
);
```

### Tabla `user_notification_reads`
```sql
CREATE TABLE IF NOT EXISTS user_notification_reads (
    id BIGSERIAL PRIMARY KEY,
    notification_id BIGINT NOT NULL REFERENCES notifications(id) ON DELETE CASCADE,
    user_id BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    read_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_user_notification UNIQUE (user_id, notification_id)
);
```

---

## 3. Guía de Implementación Futura: Envío por Correo Electrónico

Actualmente el sistema cuenta con la infraestructura base de correo:
- `IEmailQueue` (`RTecNM_V2_Backend/Common/Notifications/EmailQueue.cs`)
- `EmailBackgroundWorker` (`RTecNM_V2_Backend/Common/Notifications/EmailBackgroundWorker.cs`)
- Configuración SMTP en `SystemSettings` y MailKit.

Para activar el envío por correo cuando se emite una notificación, seguir estos pasos:

### Paso 1: Bandera en Configuración del Sistema
En `SystemSettingService`, registrar la clave:
- `Key`: `"Notifications.SendEmailEnabled"` (Valor booleano `"true"` o `"false"`).
- Opcionalmente en el formulario de emisión frontend, agregar el checkbox: `[ ] Enviar copia por correo electrónico`.

### Paso 2: Resolución de Destinatarios en `NotificationService`
Al crear una notificación (`CreateNotificationAsync`):
```csharp
if (dto.SendEmail && isEmailEnabled)
{
    // 1. Obtener los correos según los filtros:
    var targetEmails = await _userRepository.GetEmailsByCriteriaAsync(
        targetRoles: dto.TargetRoles,
        careerId: dto.CareerId,
        residencyModality: dto.ResidencyModality
    );

    // 2. Encolar correos mediante IEmailQueue
    foreach (var email in targetEmails)
    {
        _emailQueue.Enqueue(new EmailMessageDto
        {
            ToEmail = email,
            Subject = $"[TecNM Alerta] {notification.Title}",
            BodyHtml = $@"
                <div style='font-family: Montserrat, sans-serif; color: #1B396A;'>
                    <h2>{notification.Title}</h2>
                    <p>{notification.Description}</p>
                    <hr/>
                    <small>Este aviso tiene vigencia hasta el {notification.ExpiresAt:dd/MM/yyyy}.</small>
                </div>"
        });
    }
}
```

### Paso 3: Optimización en Lotes (Batching)
Si el número de destinatarios es grande (ej. más de 500 alumnos):
- Encolar los destinatarios en bloques de 50 utilizando copia oculta (`Bcc`) o mensajes individuales encolados de manera asíncrona mediante un `Channel` o worker específico para no saturar el servidor SMTP.

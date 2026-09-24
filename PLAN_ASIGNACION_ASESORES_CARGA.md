# Plan de Implementación: Visualización de Carga Docente y Asignación de Asesores

## 1. Resumen y Objetivo
Optimizar la asignación y supervisión de asesores académicos reduciendo la fricción de navegación. Se implementará la visualización de la carga de alumnos asignados por asesor en tres puntos estratégicos del sistema y la capacidad de asignar asesor directamente desde el Expediente Digital al validar la Carta de Aceptación/Aprobación.

---

## 2. Requerimientos Funcionales

1. **Módulo de Dictamen de División (`ReviewView.vue`)**:
   - Debajo del asesor asignado en la modal de detalle/revisión de anteproyecto, mostrar la cantidad de alumnos asignados que tiene dicho docente.
   - En el selector de cambio/asignación de asesor, visualizar la carga actual del docente seleccionado.

2. **Buscador Global (`GlobalSearchModal.vue` / Fuente `ADVISORS`)**:
   - Incorporar la columna de **Alumnos Asignados** en la vista de búsqueda de asesores (`vw_search_advisors`).
   - Permitir ordenar y filtrar por carga docente en la búsqueda rápida (Ctrl + K).

3. **Expediente Digital (`DocumentsView.vue`)**:
   - Al seleccionar un alumno y evaluar o visualizar su **Carta de Aceptación / Aprobación**, habilitar el bloque de asignación de asesor académico.
   - Mostrar el asesor actual (si existe) y su conteo de alumnos asignados.
   - Permitir asignar o cambiar el asesor directamente sin abandonar la pantalla del expediente, reutilizando el componente institucional `TecnmAutocomplete`.

4. **Integridad del Flujo de Trabajo (Workflow Safety)**:
   - Mantener las validaciones de negocio: la asignación requiere que la carta de aceptación esté cargada y activa.
   - Mantener la sincronización bidireccional entre `students.advisor_id` y `projects.advisor_id`.
   - Respetar permisos RBAC (`admin`, `jefecarrera`, `careerhead`, `departmenthead`).
   - Cero modificaciones a la carpeta legacy (`RTecNM_V2_Frontend_Legacy`).

---

## 3. Plan de Cambios Técnicos

### Fase 1: Backend (`RTecNM_V2_Backend`)
1. **Advisors DTOs & Proyecciones**:
   - `AdvisorResponseDto.cs`: Agregar campo `int AssignedStudentsCount`.
   - `AdvisorOptionDto.cs`: Agregar `int AssignedStudentsCount` para mostrar carga en dropdowns y autocompletes (`"Nombre Asesor (X alumnos)"`).
   - `AdvisorRepository.cs`: Incluir subconsulta `_context.Students.Count(s => s.AdvisorId == a.Id && s.IsActive)` en `GetPagedAsync` y `GetByIdAsync`.
   - `AdvisorService.cs`: Mapear `AssignedStudentsCount` en las respuestas de asesor.

2. **Búsqueda Global y Base de Datos**:
   - Actualizar la vista `vw_search_advisors` en PostgreSQL:
     ```sql
     CREATE OR REPLACE VIEW vw_search_advisors AS
     SELECT 
         a.id AS id,
         a.full_name AS full_name,
         COALESCE(a.title, '') AS title,
         a.advisor_type::text AS advisor_type,
         a.department_id AS department_id,
         COALESCE(u.email, '') AS email,
         COALESCE(a.phone, '') AS phone,
         (SELECT COUNT(*)::int FROM students s WHERE s.advisor_id = a.id AND s.is_active = true) AS assigned_students_count,
         a.is_active AS is_active
     FROM advisors a
     LEFT JOIN users u ON a.user_id = u.id;
     ```
   - `SearchRegistry.cs`: Agregar la columna `assigned_students_count` (DisplayName: "Alumnos Asignados", Type: "Integer") a la fuente `ADVISORS`.

### Fase 2: Frontend (`RTecNM_V2_Frontend`)
1. **Dictamen de División (`ReviewView.vue`)**:
   - En el modal de detalle del anteproyecto, agregar debajo de `selectedProject.advisorName` una etiqueta informativa reactiva con los alumnos asignados del asesor.
   - Consultar la carga del asesor mediante `/api/v1/advisors/{id}` o `/api/v1/advisors/{id}/residents` al abrir el modal, o vincularla desde el DTO.
   - En el autocomplete de asignación, formatear el subtítulo para mostrar los alumnos activos del candidato.

2. **Buscador Global (`GlobalSearchModal.vue`)**:
   - La nueva columna `assigned_students_count` se reflejará automáticamente por metadatos del backend, formateándose como badge numérico de carga.

3. **Expediente Digital (`DocumentsView.vue`)**:
   - En la vista de detalle de expediente (`viewMode === 'detail'`), agregar un widget/sección de "Asignación de Asesor Académico" vinculada a la existencia y aprobación de la Carta de Aceptación.
   - Permitir asignar el asesor vía `POST /api/v1/advisors/assign` directamente desde esa pantalla.
   - Notificación de éxito y actualización inmediata del estado sin recargar la página completa.

---

## 4. Matriz de Verificación y Recibos (RDD)
- [ ] Ejecución de script SQL de actualización de `vw_search_advisors`.
- [ ] `dotnet build` en `RTecNM_V2_Backend` con 0 errores y 0 warnings de contrato.
- [ ] `pnpm build` en `RTecNM_V2_Frontend` asegurando bundle limpio.
- [ ] Verificación funcional de asignación en Dictamen y Expediente Digital.

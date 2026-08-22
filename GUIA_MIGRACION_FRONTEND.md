# 🚀 Guía de Continuación de Migración de Frontend
## Sistema de Residencias Profesionales TecNM - Campus Monclova (`RTecNM_V2`)

Este documento sirve como hoja de ruta técnica detallada para dar continuidad al proceso de migración del frontend de la plataforma institucional.

---

## 📌 1. Objetivo General del Proyecto

Migrar la interfaz de usuario desde **ASP.NET Core Razor Pages (Monolítico Legacy)** a una arquitectura de **Single Page Application (SPA)** moderna construida con **Vite + Vue 3 (Composition API) + Pinia + Vue Router + pnpm 11**.

> [!IMPORTANT]
> **REGLA DE ORO DE FIDELIDAD VISUAL Y FUNCIONAL:**
> El nuevo frontend en Vue 3 **debe replicar al 100% el diseño, las clases CSS institucionales y el comportamiento interactivo** que ya existían en la versión legacy (`RTecNM_V2_Frontend_Legacy`). No inventar clases CSS ad-hoc; toda la estilización debe provenir estrictamente de `main.css` y `tecnm-theme.css`.

---

## 📂 2. Estructura de Directorios del Repositorio

* `c:\Dev\TecnmResidencias\RTecNM_V2_Frontend_Legacy\`
  * **Copia de Respaldo Original (Razor Pages).** Contiene todos los archivos `.cshtml`, scripts `.js` originales (`wwwroot/assets/js/`), estilos CSS e imágenes. **Usar exclusivamente como referencia de lectura y código.**
* `c:\Dev\TecnmResidencias\RTecNM_V2_Frontend\`
  * **Nuevo Frontend Oficial (Vite + Vue 3).** Aquí se encuentra todo el desarrollo en Vue 3. Se ejecuta en `http://localhost:5000` mediante `pnpm dev`.
* `c:\Dev\TecnmResidencias\RTecNM_V2_Backend\`
  * **Backend en .NET 8 / PostgreSQL.** Se ejecuta en `http://localhost:5001`. **No requiere cambios.**

---

## 🛠️ 3. Componentes Base y Arquitectura Implementada

Se han creado e integrado componentes reutilizables en `src/components/` que debes utilizar en las vistas pendientes:

| Componente | Ruta | Descripción / Clases CSS Relevantes |
|---|---|---|
| **`AppHeader.vue`** | `src/components/layout/` | Encabezado institucional con Isologo, campus, perfil y atajo `Buscar (Ctrl+K)`. |
| **`AppNavbar.vue`** | `src/components/layout/` | Navegación por rol. Los acordeones despliegan con `.open`, chevrons rotan 180° e ilumina con `.is-active`. |
| **`GlobalSearchModal.vue`** | `src/components/search/` | Modal universal de búsqueda (`Ctrl + K`). Utiliza `POST /api/v1/searches/filter-paged` con el parámetro `matchOption`. |
| **`TecnmAutocomplete.vue`** | `src/components/common/` | Campo de búsqueda reactivo con spinner, resaltado coincidente (`.tecnm-autocomplete-highlight`) y botón picker que abre la búsqueda universal. |
| **`TecnmPagination.vue`** | `src/components/common/` | Paginador oficial (`.tecnm-pagination`, `.tecnm-pagination-btn`, `.tecnm-pagination-info`). |
| **`AuditModal.vue`** | `src/components/common/` | Modal de auditoría con los **10 campos estándar** (`ID`, `Estado`, `Visible`, `Orden`, `Creado el/por`, `Actualizado el/por`, `Eliminado el/por`). |
| **`ConfirmModal.vue`** | `src/components/common/` | Diálogo de confirmación reactivo que reemplaza al `confirm()` nativo. |
| **`TecnmBadge.vue`** | `src/components/common/` | Pastillas de estado semánticas (`.tecnm-badge-approved`, `.tecnm-badge-rejected`, etc.). |

---

## 📊 4. Estado Actual del Avance (60% Completado)

### ✅ Módulos Listos y Verificados (Cero Errores):
1. **Infraestructura & Auth:** Login (`/auth/login`), Pinia Auth Store (`src/stores/auth.js`), Guards de Vue Router con RBAC.
2. **Dashboard Principal (`/dashboard`):** 6 KPIs semánticos, cola de dictamen, tabla de solicitudes recientes y avance de 26 semanas para estudiantes.
3. **Módulo de Estudiantes (`/students` y `/students/profile`):** Catálogo, paginación, filtros de inactivos, exportación PDF, modal de edición con validaciones y expediente del alumno.
4. **Módulo de Anteproyectos (`/projects/proposal`):** Solicitud con objetivos dinámicos, autocompletes (`STUDENTS`, `COMPANIES`, `ADVISORS`), envío a revisión y descarga PDF.
5. **Dictamen de División (`/projects/review`):** Filtro por estado (Pendientes, Aprobados, Rechazados), modal de dictamen técnico y observaciones.
6. **Catálogo de Asesores (`/advisors`):** Directorio docente, vinculación con cuentas `USERS`, exportación PDF y auditoría.
7. **Empresas Receptoras (`/companies`):** Directorio con RFC en mayúsculas y gestión de estatus.

---

## 📋 5. Tareas Pendientes por Realizar (40% Restante)

Te corresponde continuar con la migración de las **6 vistas restantes** divididas en el Sprint 3 y Sprint 4:

### 🔹 SPRINT 3: Evaluación y Seguimiento de Residencias

#### 1. Cronograma de Actividades (`/activities/schedule`)
* **Vista en Vue:** [`src/views/activities/ScheduleView.vue`](file:///c:/Dev/TecnmResidencias/RTecNM_V2_Frontend/src/views/activities/ScheduleView.vue)
* **Referencia Legacy:** `RTecNM_V2_Frontend_Legacy/Pages/Activities/Schedule.cshtml` y `wwwroot/assets/js/activities/schedule.js`
* **Lógica a implementar:**
  * Selector de anteproyecto del residente.
  * Tabla con la **matriz interactiva de las 26 semanas** del semestre.
  * Captura y edición de actividades del cronograma con semanas planificadas vs semanas realizadas.
  * Indicadores de estado (`pending`, `in_progress`, `completed`).

#### 2. Bitácora de Asesorías (`/evaluations`)
* **Vista en Vue:** [`src/views/evaluations/AdvisorySessionsView.vue`](file:///c:/Dev/TecnmResidencias/RTecNM_V2_Frontend/src/views/evaluations/AdvisorySessionsView.vue)
* **Referencia Legacy:** `RTecNM_V2_Frontend_Legacy/Pages/Evaluations/Index.cshtml` y `wwwroot/assets/js/evaluations/evaluations.js`
* **Lógica a implementar:**
  * Registro de sesiones de asesoría técnica entre docente y alumno.
  * Formulario de captura: Fecha, temas abordados, compromisos/acuerdos, horas acumuladas.
  * Estatus de firma de conformidad por ambas partes.

#### 3. Evaluación y Calificaciones (`/evaluations/grading`)
* **Vista en Vue:** [`src/views/evaluations/GradingView.vue`](file:///c:/Dev/TecnmResidencias/RTecNM_V2_Frontend/src/views/evaluations/GradingView.vue)
* **Referencia Legacy:** `RTecNM_V2_Frontend_Legacy/Pages/Evaluations/Grading.cshtml` y `wwwroot/assets/js/evaluations/evaluations.js`
* **Lógica a implementar:**
  * Formulario de capturas de rúbricas para el **Asesor Interno** y **Asesor Externo**.
  * Calificación de Reportes Parciales (1 y 2) y Reporte Final.
  * Cálculo ponderado automático de la calificación final del residente.

#### 4. Expediente Digital del Residente (`/documents`)
* **Vista en Vue:** [`src/views/documents/DocumentsView.vue`](file:///c:/Dev/TecnmResidencias/RTecNM_V2_Frontend/src/views/documents/DocumentsView.vue)
* **Referencia Legacy:** `RTecNM_V2_Frontend_Legacy/Pages/Documents/Index.cshtml` y `wwwroot/assets/js/documents/documents.js`
* **Lógica a implementar:**
  * Lista de documentos oficiales requeridos (Carta de Presentación, Aceptación, Dictamen, Reportes Bimestrales, Carta de Liberación).
  * Componente de carga de archivos (upload) con restricción de PDF y tamaño máximo.
  * Previsualizador modal de PDFs y estado de aprobación/rechazo por control escolar.

---

### 🔹 SPRINT 4: Reportes, Liberación y Administración

#### 5. Reportes Ejecutivo y Liberación (`/admin/reports`)
* **Vista en Vue:** [`src/views/admin/ReportsView.vue`](file:///c:/Dev/TecnmResidencias/RTecNM_V2_Frontend/src/views/admin/ReportsView.vue)
* **Referencia Legacy:** `RTecNM_V2_Frontend_Legacy/Pages/Admin/Reports.cshtml` y `wwwroot/assets/js/admin/admin.js`
* **Lógica a implementar:**
  * Filtros por periodo escolar, carrera y estatus de residencia.
  * Generación y exportación de estadísticos institucionales.
  * Emisión e impresión oficial de la **Carta de Liberación de Residencia Profesional**.

#### 6. Administración de Usuarios y Roles (`/admin/roles`)
* **Vista en Vue:** [`src/views/admin/RolesView.vue`](file:///c:/Dev/TecnmResidencias/RTecNM_V2_Frontend/src/views/admin/RolesView.vue)
* **Referencia Legacy:** `RTecNM_V2_Frontend_Legacy/Pages/Admin/Roles.cshtml` y `wwwroot/assets/js/admin/roles.js`
* **Lógica a implementar:**
  * Tabla de usuarios del sistema con su rol asignado (`student`, `advisor`, `departmenthead`, `vinculacion`, `director`, `admin`).
  * Modal para cambiar roles de usuario y restablecer accesos.

---

## ⚡ 6. Comandos Útiles para Desarrollo

```bash
# 1. Posicionarse en la carpeta del frontend oficial
cd c:\Dev\TecnmResidencias\RTecNM_V2_Frontend

# 2. Levantar servidor de desarrollo en puerto 5000
pnpm dev

# 3. Compilar para producción y verificar que no existan errores de sintaxis/imports
pnpm build
```

---

## 💡 7. Recomendaciones Finales para el Desarrollador

1. **Revisar primero el archivo `.cshtml` y `.js` equivalente en `RTecNM_V2_Frontend_Legacy`** antes de escribir código en la vista Vue.
2. **Utilizar `TecnmAutocomplete`** para cualquier campo donde se requiera seleccionar un Estudiante, Empresa o Asesor.
3. **Utilizar `useAudit()` y `<AuditModal />`** para el botón de auditoría de cada tabla.
4. **Utilizar `useConfirm()` y `<ConfirmModal />`** para diálogos de eliminación o aprobación.
5. Ejecutar `pnpm build` al finalizar cada vista para garantizar una compilación limpia.

# AGENTS.md - TecNM Residency System v2 (Frontend SPA)

## Project Overview

| Attribute | Value |
|-----------|-------|
| **System** | TecNM Professional Residency System v2 — Frontend SPA |
| **Tech Stack** | Vue 3 (v3.5+), Vite (v8+), Pinia (v4+), Vue Router (v4+), Axios (v1.19+) |
| **Architecture** | Component-Driven Single Page Application (SPA) with Composition API (`<script setup>`) |
| **Styling** | 100% Centralized Vanilla CSS with TecNM Design Tokens (Pantone 288 C & 117 C) |
| **Methodology** | Spec-Driven Development (SDD) + Receipt-Driven Development (RDD) |
| **Target Port** | `5085` (Dev Server with reverse proxy to Backend API on `5185`) |
| **Language & Locale** | Spanish (`es-MX`) |

---

## Build & Run Commands

```bash
# Dependencias
pnpm install          # O: npm install

# Servidor de Desarrollo (con HMR y proxy a backend http://localhost:5185)
pnpm dev              # O: npm run dev

# Verificación de Compilación y Bundle de Producción
pnpm build            # O: npm run build

# Previsualización del Build Local
pnpm preview          # O: npm run preview
```

---

## Architecture & Directory Structure

```text
src/
├── assets/
│   ├── css/
│   │   ├── tecnm-theme.css   ← Design tokens (:root CSS custom properties)
│   │   └── main.css          ← Component primitives & global layout classes
│   └── images/               ← Logotipos e isotipos institucionales SVG
├── components/
│   ├── common/               ← Primitivas UI reutilizables
│   │   ├── AuditModal.vue        # Modal de auditoría institucional (10 campos)
│   │   ├── ConfirmModal.vue      # Modal de confirmación asíncrono
│   │   ├── TecnmAutocomplete.vue # Selector asíncrono asistido de entidades
│   │   ├── TecnmBadge.vue        # Badges semánticos por estado
│   │   ├── TecnmKpiCard.vue      # Tarjetas KPI de métricas/dashboard
│   │   ├── TecnmModal.vue        # Modal genérico con Teleport y accesibilidad
│   │   └── TecnmPagination.vue   # Paginador institucional unificado
│   ├── layout/               ← Componentes estructurales de la aplicación
│   │   ├── AppHeader.vue         # Barra superior institucional y avatar de usuario
│   │   ├── AppNavbar.vue         # Navegación principal filtrada por rol
│   │   ├── AppFooter.vue         # Pie de página institucional
│   │   └── AppLayout.vue         # Contenedor raíz con vistas y modales globales
│   ├── search/               ← Búsqueda global multitabla
│   │   └── GlobalSearchModal.vue # Buscador universal (Ctrl + K)
│   ├── advisors/             ← Componentes especializados de dominio
│   └── evaluations/
├── composables/              ← Lógica reactiva reutilizable (Hooks)
│   ├── useAudit.js           # Apertura e inspección de auditoría de 10 campos
│   ├── useConfirm.js         # Cuadros de diálogo interactivos basados en Promesas
│   └── useGlobalSearch.js    # Control del modal universal de búsqueda
├── router/
│   └── index.js              # Enrutamiento, metadatos y Navigation Guards (RBAC)
├── services/
│   └── api.js                # Cliente Axios con interceptores JWT y captura de 401
├── stores/
│   └── auth.js               # Store Pinia: estado de autenticación, JWT y roles
├── views/                    # Vistas funcionales por módulo institucional
│   ├── activities/           # ScheduleView.vue
│   ├── admin/                # RolesView.vue, SystemSettingsView.vue, CareersView.vue, ReportsView.vue
│   ├── advisors/             # AdvisorsView.vue, AdvisorAssignmentView.vue
│   ├── auth/                 # LoginView.vue
│   ├── companies/            # CompaniesView.vue
│   ├── dashboard/            # DashboardView.vue
│   ├── documents/            # DocumentsView.vue
│   ├── evaluations/          # AdvisorySessionsView.vue, GradingView.vue
│   ├── projects/             # ProposalView.vue, ReviewView.vue
│   └── students/             # StudentsView.vue, StudentProfileView.vue
├── App.vue                   # Entrada visual montando AppLayout
└── main.js                   # Bootstrap de Vue 3, Pinia, Router y CSS global
```

---

## SFC & Vue 3 Development Standards

1. **Composition API Obrigatoria**: Usar exclusivamente `<script setup>`. Prohibido Options API (`data()`, `methods: {}`, etc.).
2. **Path Alias**: Importar siempre usando `@/` apuntando a `src/` (ej. `import apiClient from '@/services/api'`).
3. **Manejo de Formularios y Estados Reactivos**:
   - `ref()` para tipos primitivos, listas y banderas booleanas.
   - `computed()` para estados derivados, listas filtradas/ordenadas y permisos calculados.
   - Limpiar temporizadores (`setTimeout`/`setInterval`) y listeners de teclado en `onUnmounted`.
4. **Notificaciones y Feedback Visual**:
   - Usar el estándar de alerta institucional `.tecnm-alert` con autocierre a los 4.5 segundos:
   ```javascript
   const alertMessage = ref('')
   const alertType = ref('success') // 'success' | 'danger' | 'warning' | 'info'
   let alertTimer = null

   function showAlert(msg, type = 'success') {
     alertMessage.value = msg
     alertType.value = type
     clearTimeout(alertTimer)
     alertTimer = setTimeout(() => { alertMessage.value = '' }, 4500)
   }
   ```
5. **No Placeholders**: Prohibido usar datos ficticios estáticos ("Lorem ipsum", "John Doe") cuando se implementan vistas conectadas. Consumir los endpoints de backend correspondientes.

---

## CSS & Design System Architecture

### 100% Centralized CSS Rules
- **Prohibido**: Atributos `style="..."` inline, bloques `<style>` locales extensos con clases ad-hoc, frameworks tipo TailwindCSS o Bootstrap.
- Todos los estilos derivan de:
  - `src/assets/css/tecnm-theme.css`: Design tokens (`:root` CSS custom properties).
  - `src/assets/css/main.css`: Clases y componentes institucionalizados.

### Tokens de Identidad Gráfica TecNM 2024
- **Azul Primario (Pantone 288 C)**: `var(--tecnm-blue-primary)` (`#1B396A`)
- **Azul Oscuro (Footers / Activos)**: `var(--tecnm-blue-dark)` (`#0F2548`)
- **Azul Interactivo (Hover)**: `var(--tecnm-blue-hover)` (`#244B88`)
- **Oro Institucional (Pantone 117 C)**: `var(--tecnm-gold-accent)` (`#C5A059`)
- **Superficie de Fondos**: `var(--tecnm-bg-main)` (`#F4F6F9`), `var(--tecnm-surface-white)` (`#FFFFFF`)
- **Tipografía Institucional**: `'Montserrat', sans-serif`
- **Idioma de UI**: Español neutro / institucional (`es-MX`).

### Estandarización de Fechas
Todas las fechas mostradas en interfaces (tablas, detalles, auditoría, modales) DEBEN presentarse en formato `DD/NombreMes/YYYY` (ej. `10/Agosto/2026`). Utilizar la utilidad de fechas `formatAuditDate()` de `useAudit.js` o formateadores de mes institucional en español.

---

## Core UI Components & Interactive Protocols

### 1. Cuadros de Confirmación (`useConfirm` + `<ConfirmModal />`)
**NUNCA** usar `window.confirm()`. Usar el hook reactivo:
```javascript
import { useConfirm } from '@/composables/useConfirm'
const { confirm } = useConfirm()

const ok = await confirm({
  title: 'Desactivar Estudiante',
  message: '¿Está seguro de desactivar al estudiante? El expediente pasará a estado inactivo.',
  okText: 'Desactivar',
  cancelText: 'Cancelar'
})
if (!ok) return
```

### 2. Modal de Auditoría de 10 Campos (`useAudit` + `<AuditModal />`)
Para cumplir el protocolo institucional de auditoría del backend (`BaseEntity`), invocar `showAudit`:
```javascript
import { useAudit } from '@/composables/useAudit'
const { showAudit } = useAudit()

function handleAudit(item) {
  showAudit({
    title: `Auditoría — Registro #${item.id}`,
    item: item // Mapea id, title/name, isActive, isVisible, displayOrder, createdAt, createdBy, updatedAt, updatedBy, deletedAt, deletedBy
  })
}
```
*Resuelve nombres de usuario automáticamente mediante `GET /api/v1/auth/users/names?ids=...`.*

### 3. Paginación Unificada (`<TecnmPagination />`)
Componente compartido para listas paginadas del backend:
```vue
<TecnmPagination
  :current-page="pageNumber"
  :total-pages="totalPages"
  :total-count="totalCount"
  :page-size="pageSize"
  @update:current-page="pageNumber = $event"
  @page-change="loadData"
/>
```

### 4. Badges Semánticos (`<TecnmBadge />`)
Mapea estados estándar (activo, inactivo, borrador, aprobado, en progreso, correcciones, cancelado):
```vue
<TecnmBadge :status="student.isActive" />
<TecnmBadge :status="project.status" />
```

### 5. Atajos de Teclado Institucionales
- **`Ctrl + K` / `Cmd + K`**: Abre el modal de **Búsqueda Global Multitabla** desde cualquier sección del sistema para saltar rápidamente entre registros.

---

## Authentication & RBAC (Role-Based Access Control)

### Store de Autenticación (`src/stores/auth.js`)
- Gestiona sesión en `sessionStorage` (`authToken` y `authUser`).
- Expone roles del sistema:
  - `admin`: Acceso total de configuración y gestión.
  - `departmenthead`: Jefatura de División de Estudios Profesionales.
  - `jefecarrera` / `careerhead`: Jefatura de Carrera académica.
  - `advisor`: Asesor interno/externo de residencias.
  - `student`: Alumno residente.
  - `vinculacion`: Departamento de Gestión Tecnológica y Vinculación.
  - `director`: Dirección general (acceso de **Solo Lectura** `isReadOnly = true`).
  - `academic`: Personal académico / comités revisores.

### Helpers de Autorización en Componentes
```javascript
const authStore = useAuthStore()

// Verificación de Roles
authStore.hasRole('admin', 'departmenthead')

// Verificación de Permisos granulares
authStore.hasPermission('students.manage')

// Getters reactivos clave
authStore.isAdmin
authStore.isReadOnly        // Deshabilita acciones de creación/edición/eliminación
authStore.isCareerHead      // Aplica filtrado automático por carrera
authStore.userCareerId      // ID de la carrera asignada al usuario
authStore.canSeeAudit       // Permiso para ver modal de auditoría
authStore.canManageRegistry // Permiso para crear/editar registros
```

### Navigation Guards (`src/router/index.js`)
- `meta.requiresAuth`: Redirige a `/auth/login` si no hay sesión.
- `meta.isPublic`: Redirige a `/dashboard` si el usuario ya está autenticado.
- `meta.roles`: Array de roles permitidos para la ruta.
- `meta.permission`: Permiso específico requerido.
- `meta.navActive`: Identificador para marcar la pestaña activa en la barra de navegación.

---

## API & Backend Alignment

### Protocolo HTTP (Axios)
- Todas las peticiones van a través de `apiClient` (`src/services/api.js`).
- Base URL: `/api` (reenviado por el proxy de Vite a `http://localhost:5185`).
- Interceptor inyecta `Authorization: Bearer <token>` automáticamente.
- Respuestas `401 Unauthorized` limpian la sesión y redirigen a `/auth/login`.

### Protocolo de Paginación del Backend
Los endpoints de listado siguen el contrato `PaginatedResult<T>`:
```json
{
  "items": [],
  "totalCount": 120,
  "pageNumber": 1,
  "pageSize": 10,
  "totalPages": 12,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```
Parámetros de consulta estándar: `pageNumber`, `pageSize`, `search`, `includeInactive`, `sortBy`, `sortDir`.

### Protocolo isActive (Soft Delete & Reactivate)
- **Eliminación Lógica**: `DELETE /api/v1/{domain}/{id}` (establece `IsActive=false`).
- **Reactivación**: `PATCH /api/v1/{domain}/{id}/activate` (establece `IsActive=true`).
- **Update DTOs**: NUNCA enviar `isActive` en peticiones `PUT /api/v1/{domain}/{id}`.

---

## RDD Rules (Receipt-Driven Development)

1. **NO NARRATIVE WITHOUT RECEIPT**:
   - Nunca declarar una vista o corrección terminada sin evidencia concreta de ejecución.
   - Recibos válidos: salida de `pnpm build` sin errores, capturas de logs de consola/red de Vite, `git diff` verificado.
2. **PRINCIPLE OF DERIVATION**:
   - Confiar en el estado del sistema y los contratos reales del backend, no en suposiciones.
   - Inspeccionar los endpoints en `RTecNM_V2_Backend` o Swagger antes de modelar llamadas API.
3. **POST-EXECUTION CONTROL**:
   - Ejecutar `pnpm build` para asegurar que las importaciones y sintaxis SFC sean 100% válidas antes de entregar tareas.
4. **TOKEN EFFICIENCY**:
   - Respuestas concisas, enfocadas directamente en el código y en la evidencia técnica.

---

## Workflow para Agentes

1. **Revisar Especificación**: Consultar la vista existente o la especificación en `docs/specs/` antes de modificar o crear componentes.
2. **Reutilizar Primitivas**: Usar `TecnmPagination`, `TecnmBadge`, `TecnmModal`, `useConfirm`, `useAudit` y `apiClient`.
3. **Garantizar Estilos Centralizados**: Usar clases existentes de `main.css` y variables de `tecnm-theme.css`. Prohibido estilos inline.
4. **Verificar Compilación**: Ejecutar `pnpm build` para garantizar cero errores de transpilación o empaquetado.
5. **Entregar Recibo**: Presentar el resultado de la verificación técnica.

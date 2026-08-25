# 🏛️ RTecNM V2 — Frontend SPA (Vue 3 + Vite)

Cliente web institucional desarrollado como Single Page Application (SPA) moderna, desacoplada y de alto rendimiento para el **Sistema de Gestión de Residencias Profesionales del TecNM Campus Monclova**.

---

## 🛠️ Stack Tecnológico

- **Framework**: [Vue 3](https://vuejs.org/) (`v3.5+`) con Composition API y sintaxis SFC `<script setup>`.
- **Build Tool & Bundler**: [Vite](https://vite.dev/) (`v8+`) para arranque instantáneo y Hot Module Replacement (HMR).
- **Gestor de Estado**: [Pinia](https://pinia.vuejs.org/) (`v4+`) para el manejo centralizado de autenticación JWT y permisos RBAC.
- **Enrutamiento**: [Vue Router](https://router.vuejs.org/) (`v4+`) con Navigation Guards basados en roles y permisos.
- **Cliente HTTP**: [Axios](https://axios-http.com/) con interceptores para inyección de token JWT, manejo de errores y proxy a `/api`.
- **Estilos**: Vanilla CSS con Tokens y Design System oficial TecNM (Azul Institucional `#1B396A`, Oro `#C5A059`).

---

## 📂 Estructura de Directorios

```text
src/
├── assets/
│   ├── css/
│   │   └── main.css              # Tokens y clases de utilidad del Design System TecNM
│   └── tecnm-isologo.svg         # Isologotipo oficial TecNM
├── components/
│   ├── common/
│   │   ├── AuditModal.vue        # Modal de auditoría institucional (10 campos)
│   │   ├── ConfirmModal.vue      # Cuadros de diálogo de confirmación reactivos
│   │   ├── TecnmAutocomplete.vue # Buscador asistido asíncrono para entidades
│   │   ├── TecnmBadge.vue        # Badges semánticos institucionalizados
│   │   └── TecnmPagination.vue   # Componente oficial de paginación
│   ├── layout/
│   │   ├── TecnmHeader.vue       # Barra superior, datos del usuario y cierre de sesión
│   │   ├── TecnmNavbar.vue       # Barra de navegación principal filtrada por rol
│   │   └── TecnmFooter.vue       # Pie de página institucional
│   └── search/
│       └── GlobalSearchModal.vue # Modal universal de búsqueda multitabla (Ctrl + K)
├── composables/
│   ├── useAudit.js               # Hook para apertura e inspección de auditoría
│   ├── useConfirm.js             # Hook para diálogos interactivos de confirmación
│   └── useGlobalSearch.js        # Hook para control y apertura del buscador global
├── router/
│   └── index.js                  # Definición de rutas, metadatos y Navigation Guards
├── services/
│   └── api.js                    # Instancia Axios con interceptores JWT y base URL
├── stores/
│   └── auth.js                   # Store Pinia: estado de autenticación, JWT y roles
└── views/                        # Vistas funcionales por módulo institucional
    ├── activities/ScheduleView.vue           # Cronograma interactivo (26 semanas)
    ├── admin/ReportsView.vue                 # Reportes y emisión de libranzas
    ├── admin/RolesView.vue                   # Gestión de roles y permisos RBAC
    ├── admin/SystemSettingsView.vue          # Configuración institucional del sistema
    ├── advisors/AdvisorsView.vue             # Directorio de asesores académicos
    ├── advisors/AdvisorAssignmentView.vue    # Asignación y carga de asesores
    ├── auth/LoginView.vue                    # Inicio de sesión institucional
    ├── companies/CompaniesView.vue           # Directorio de empresas receptoras
    ├── dashboard/DashboardView.vue           # Panel de control y KPIs por rol
    ├── documents/DocumentsView.vue           # Expediente digital y visor de PDFs
    ├── evaluations/AdvisorySessionsView.vue  # Bitácora de sesiones de asesoría
    ├── evaluations/GradingView.vue           # Evaluación y calificaciones parciales/final
    ├── projects/ProposalView.vue             # Solicitud de anteproyectos
    ├── projects/ReviewView.vue               # Dictamen de división académica
    ├── students/StudentProfileView.vue       # Perfil y expediente del estudiante
    └── students/StudentsView.vue             # Directorio de alumnos
```

---

## 🚀 Inicio en Desarrollo

### 1. Instalar Dependencias

```bash
# Recomendado
pnpm install

# O con npm:
npm install
```

### 2. Ejecutar Servidor de Desarrollo

```bash
# Recomendado
pnpm dev

# O con npm:
npm run dev
```

El servidor Vite iniciará en **`http://localhost:5000`** y configurará automáticamente el proxy reverso para todas las peticiones `/api/*` hacia el Backend en `http://localhost:5144`.

---

## 📦 Scripts Disponibles

| Script | Comando | Descripción |
|---|---|---|
| **Desarrollo** | `pnpm dev` / `npm run dev` | Inicia servidor Vite con recarga en vivo (HMR) en el puerto `5000` |
| **Compilación** | `pnpm build` / `npm run build` | Compila y optimiza la SPA para producción en la carpeta `dist/` |
| **Vista Previa** | `pnpm preview` / `npm run preview` | Levanta un servidor local para probar los archivos de producción generados |

---

## ⌨️ Atajos de Teclado

- **`Ctrl + K`** (o **`Cmd + K`** en macOS): Abre instantáneamente el modal de **Búsqueda Global Multitabla** desde cualquier vista para localizar rápidamente alumnos, asesores, proyectos y empresas.


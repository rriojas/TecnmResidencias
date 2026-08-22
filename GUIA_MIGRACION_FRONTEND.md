# 📘 Guía Integral y Hoja de Ruta: Migración Frontend RTecNM V2

> **Documento de Control y Traspaso de Proyecto**  
> **Proyecto:** Sistema de Gestión de Residencias Profesionales — TecNM Campus Monclova  
> **Arquitectura:** Vue 3 (Composition API + `<script setup>`) + Vite + Pinia + Vue Router  
> **Estado:** **100% Migrado y Verificado (Sprints 1, 2, 3 y 4 Completados)**  

---

## 🎯 1. Objetivo General de la Migración

Reemplazar de manera íntegra y transparente el frontend legacy monolítico en ASP.NET Core Razor Pages (`RTecNM_V2_Frontend_Legacy`) por una aplicación Single Page Application (SPA) moderna, desacoplada y de alto rendimiento en Vue 3 (`RTecNM_V2_Frontend`), consumiendo la API REST institucional en ASP.NET Core / PostgreSQL (`RTecNM_V2_Backend`).

---

## 🏛️ 2. Arquitectura y Estructura del Proyecto Moderno

```
c:\Dev\TecnmResidencias\RTecNM_V2_Frontend\
├── src\
│   ├── assets\
│   │   ├── css\
│   │   │   └── main.css             <-- Design System institucional TecNM (100% fiel al original)
│   │   └── tecnm-isologo.svg        <-- Isologotipo oficial TecNM
│   ├── components\
│   │   ├── common\
│   │   │   ├── AuditModal.vue        <-- Modal de Auditoría de 10 campos institucionales
│   │   │   ├── ConfirmModal.vue      <-- Diálogos de confirmación reactivos
│   │   │   ├── TecnmAutocomplete.vue <-- Autocompletes asíncronos para Estudiantes, Asesores y Proyectos
│   │   │   ├── TecnmBadge.vue        <-- Badges homologados con getBadgeHtml()
│   │   │   └── TecnmPagination.vue   <-- Paginador oficial TecNM
│   │   ├── layout\
│   │   │   ├── TecnmFooter.vue       <-- Pie de página institucional
│   │   │   ├── TecnmHeader.vue       <-- Encabezado y perfil de usuario
│   │   │   └── TecnmNavbar.vue       <-- Barra de navegación por roles
│   │   └── search\
│   │       └── GlobalSearchModal.vue <-- Búsqueda universal global (Ctrl + K)
│   ├── composables\
│   │   ├── useAudit.js               <-- Composable para apertura y carga de datos de auditoría
│   │   ├── useConfirm.js             <-- Composable para confirmaciones
│   │   └── useGlobalSearch.js        <-- Composable del modal de búsqueda global
│   ├── router\
│   │   └── index.js                  <-- Definición de rutas y Guards de seguridad (RBAC)
│   ├── services\
│   │   └── api.js                    <-- Cliente Axios centralizado con interceptores JWT
│   ├── stores\
│   │   └── auth.js                   <-- Pinia Store de Autenticación y Permisos
│   └── views\
│       ├── activities\
│       │   └── ScheduleView.vue      <-- Matriz de 26 semanas del cronograma
│       ├── admin\
│       │   ├── ReportsView.vue       <-- Métricas y emisión de cartas de liberación
│       │   └── RolesView.vue         <-- Gestión de roles, permisos y asignación de usuarios
│       ├── advisors\
│       │   └── AdvisorsView.vue      <-- Directorio docente y vinculación de asesores
│       ├── auth\
│       │   └── LoginView.vue         <-- Inicio de sesión institucional
│       ├── companies\
│       │   └── CompaniesView.vue     <-- Directorio de empresas receptoras
│       ├── dashboard\
│       │   └── DashboardView.vue     <-- Panel principal por roles
│       ├── documents\
│       │   └── DocumentsView.vue     <-- Expediente digital y visor de documentos
│       ├── evaluations\
│       │   ├── AdvisorySessionsView.vue <-- Bitácora de asesorías técnicas
│       │   └── GradingView.vue       <-- Evaluación y calificaciones parciales/final
│       ├── projects\
│       │   ├── ProposalView.vue      <-- Registro de anteproyectos con objetivos dinámicos
│       │   └── ReviewView.vue        <-- Dictamen de división académica
│       └── students\
│           ├── StudentProfileView.vue<-- Perfil y expediente del estudiante
│           └── StudentsView.vue      <-- Directorio y catálogo de estudiantes
```

---

## 📊 3. Resumen del Avance (100% Completado)

| Módulo | Vista / Ruta | Descripción | Estado |
| :--- | :--- | :--- | :---: |
| **Auth & RBAC** | `/auth/login` | Login institucional, interceptores JWT y guardias de ruta por rol | ✅ Listo |
| **Dashboard** | `/dashboard` | 6 KPIs semánticos, cola de dictamen y avance de 26 semanas | ✅ Listo |
| **Estudiantes** | `/students` | Directorio, paginación, filtros de inactivos y exportación PDF | ✅ Listo |
| **Perfil Alumno** | `/students/profile` | Expediente y edición con validación estricta | ✅ Listo |
| **Anteproyectos** | `/projects/proposal` | Solicitud con objetivos dinámicos y autocompletes asistidos | ✅ Listo |
| **Dictamen** | `/projects/review` | Revisión académica, filtros de estado y retroalimentación | ✅ Listo |
| **Asesores** | `/advisors` | Directorio docente, vinculación USERS y auditoría | ✅ Listo |
| **Empresas** | `/companies` | Empresas receptoras, RFC en mayúsculas y gestión de estatus | ✅ Listo |
| **Cronograma** | `/activities/schedule` | Matriz interactiva de 26 semanas y alta de actividades | ✅ Listo |
| **Bitácora Asesorías** | `/evaluations` | Sesiones de asesoría técnica, compromisos y autocompletes | ✅ Listo |
| **Calificaciones** | `/evaluations/grading` | Reportes parciales 1, 2 y final con ponderación institucional | ✅ Listo |
| **Expediente Digital**| `/documents` | Subida multipart, visor de PDF/imágenes y evaluación | ✅ Listo |
| **Reportes** | `/admin/reports` | 4 KPIs, tabla de elegibilidad y emisión de libranzas | ✅ Listo |
| **Usuarios & Roles** | `/admin/roles` | Catálogo de roles con permisos por módulo y asignación de usuarios | ✅ Listo |

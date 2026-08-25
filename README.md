# Sistema de Residencias Profesionales TecNM v2

Sistema integral web para la gestión, seguimiento, evaluación, dictaminación y expediente digital de Residencias Profesionales del Tecnológico Nacional de México (TecNM).

---

## 🏛️ Descripción General

El sistema está desarrollado con una arquitectura desacoplada de alto rendimiento:
- **Backend**: API REST en **.NET 10** bajo el patrón **Vertical Slice Architecture (Screaming Architecture)** con PostgreSQL 17+, auditoría inmutable en 10 campos y autenticación JWT con control de acceso basado en roles (RBAC) y permisos granulares.
- **Frontend**: Single Page Application (SPA) moderna desarrollada con **Vue 3 (Composition API + `<script setup>`)**, **Vite**, **Pinia** (gestión de estado reactivo) y **Vue Router** (con guardias de navegación RBAC), cumpliendo con los estándares de identidad gráfica institucional TecNM (Azul `#1B396A` y Oro `#C5A059`).

---

## 👥 Roles del Sistema (6 Roles Oficiales)

El sistema cuenta con **6 roles oficiales** con permisos y ámbitos de control independientes definidos mediante RBAC:

1. **Super Administrador (`admin`)**:
   - **Ámbito**: Control total y global del sistema.
   - **Funciones**: Administración global de usuarios, configuración y asignación de roles y permisos RBAC, gestión de catálogos institucionales, parámetros del sistema y auditoría completa.

2. **Académicos y Jefatura de División (`academico` / `departmenthead` / `academic`)**:
   - **Ámbito**: Gestión académica por carrera y departamento.
   - **Funciones**: Alta y administración de estudiantes y asesores, verificación de elegibilidad académica, dictamen/revisión de anteproyectos de residencia y asignación de asesores académicos.

3. **Gestión Tecnológica y Vinculación (`vinculacion`)**:
   - **Ámbito**: Vinculación institucional y gestión de convenios.
   - **Funciones**: Alta y administración del catálogo de empresas e instituciones receptoras, emisión de cartas de presentación y liberación, y seguimiento del expediente documental de vinculación.

4. **Director / Directivos (`director`)**:
   - **Ámbito**: Supervisión institucional de solo lectura.
   - **Funciones**: Acceso de solo lectura global a todos los módulos para auditoría directiva, visualización del dashboard de indicadores clave (KPIs), métricas de desempeño y exportación de reportes.

5. **Asesores Académicos (`advisor`)**:
   - **Ámbito**: Seguimiento y evaluación académica de residentes asignados.
   - **Funciones**: Control y validación del cronograma de 26 semanas, registro de bitácoras y sesiones de asesoría técnica, revisión de avances y captura de calificaciones parciales y finales.

6. **Estudiantes (`student`)**:
   - **Ámbito**: Autogestión del proceso de residencia profesional.
   - **Funciones**: Registro y actualización de perfil/expediente, solicitud e inscripción de anteproyectos vinculados a empresas, reporte semanal de avances en la matriz de 26 semanas, carga de evidencias y seguimiento a su expediente digital.

---

## 🚀 Guía de Inicio Rápido

### Requisitos Previos

- **Node.js 20+** y gestor de paquetes (**pnpm** recomendado o **npm**) para el Frontend SPA.
- **.NET 10 SDK** para el servidor Backend Web API.
- **Docker & Docker Compose** (para PostgreSQL 17 y despliegue en contenedores).
- **PostgreSQL 17+** (si se ejecuta localmente sin Docker).

---

### ⚡ Opción A: Scripts de Inicio Rápido (Recomendado)

Se incluyen scripts automatizados para PowerShell (Windows) y Bash (Linux/macOS/Git Bash) que verifican dependencias, liberan puertos y levantan el stack:

- **Levantar todo (Base de Datos, Backend API y Frontend SPA):**
  - PowerShell: `.\start-all.ps1`
  - Bash: `./start-all.sh`

- **Levantar solo Backend (y contenedor PostgreSQL):**
  - PowerShell: `.\start-backend.ps1`
  - Bash: `./start-backend.sh`

- **Levantar solo Frontend SPA (Vite + Vue 3):**
  - PowerShell: `.\start-frontend.ps1`
  - Bash: `./start-frontend.sh`

---

### 🛠️ Opción B: Ejecución Manual Paso a Paso

#### 1. Iniciar Base de Datos PostgreSQL (vía Docker)

```bash
docker-compose up -d postgres
```
*PostgreSQL escuchará en el puerto `5432` con la base de datos `residency_v2`.*

#### 2. Iniciar Backend Web API (C# .NET 10)

```bash
cd RTecNM_V2_Backend
dotnet restore
dotnet build
dotnet run
```
*La API REST estará escuchando en `http://localhost:5144` (Swagger interactivo disponible en `http://localhost:5144/swagger`).*

#### 3. Iniciar Frontend SPA (Vite + Vue 3)

```bash
cd RTecNM_V2_Frontend
pnpm install   # O bien: npm install
pnpm dev       # O bien: npm run dev
```
*El cliente web estará disponible en **`http://localhost:5000`** (con proxy reverso automático configurado hacia `http://localhost:5144`).*

---

### 🐳 Opción C: Despliegue con Docker Compose (Servicios Backend)

Para iniciar la base de datos PostgreSQL y el contenedor Backend API:

```bash
docker-compose up -d --build
```

Esto levantará:
1. **PostgreSQL 17** (`residencia-v2-db`) en el puerto `5432` con esquemas DDL y semillas automáticas (`docs/database/01_schema_and_essential_seeds.sql`).
2. **Backend Web API** (`residencia-v2-backend`) en el puerto `5144`.

Posteriormente, ejecuta el Frontend con `pnpm dev` dentro de `RTecNM_V2_Frontend/`.

---

## 📁 Estructura del Proyecto

```text
TecnmResidencias/
├── docker-compose.yml              # Definición de contenedores Docker (Postgres y Backend)
├── README.md                       # Documentación principal del proyecto
├── GUIA_MIGRACION_FRONTEND.md      # Hoja de ruta y detalles de migración a Vue 3
├── GUIA_IMPLEMENTACION_BUSQUEDA_GLOBAL.md # Especificación del motor de búsqueda multitabla
├── start-all.ps1 / .sh             # Script para iniciar todo el stack
├── start-backend.ps1 / .sh         # Script para iniciar Backend y Postgres
├── start-frontend.ps1 / .sh        # Script para iniciar Frontend Vite
├── docs/                           # Documentación técnica y scripts SQL
│   ├── database/                   # Esquemas DDL, semillas y datos demo
│   └── specs/                      # Especificaciones de los módulos 00 al 09
├── RTecNM_V2_Backend/              # API REST en C# .NET 10 (Vertical Slice)
└── RTecNM_V2_Frontend/             # Frontend SPA en Vite + Vue 3
```

### Detalle de Módulos y Arquitectura

#### 1. `RTecNM_V2_Backend/` (Servidor API REST .NET 10)
Estructurado bajo patrón **Vertical Slice Colocation** (controlador, servicio, repositorio, DTOs y validaciones por módulo funcional):
- **`Auth/`**: Autenticación JWT, gestión de roles (6 roles institucionales), permisos RBAC y usuarios.
- **`Students/`**: Gestión de expedientes de alumnos, carreras, periodos lectivos y elegibilidad.
- **`Advisors/`**: Registro de asesores internos/externos, especialidades, asignaciones y cargas.
- **`Projects/`**: Propuestas de anteproyectos de residencia, flujo de dictamen y generación de PDF.
- **`Activities/`**: Cronograma interactivo de 26 semanas y seguimiento de avances.
- **`Evaluations/`**: Bitácoras de asesorías y evaluaciones parciales y finales con ponderación oficial.
- **`Companies/`**: Directorio de empresas receptoras, sectores, convenios y contactos.
- **`Documents/`**: Carga y validación de expediente digital (PDFs, cartas de presentación y liberación).
- **`Searches/`**: Motor de búsqueda global multitabla optimizado con vistas en PostgreSQL.
- **`Admin/`**: Dashboard con KPIs, métricas de residencias, reportes y configuración general del sistema.

#### 2. `RTecNM_V2_Frontend/` (Single Page Application — Vite + Vue 3)
Construido con **Vue 3 (`<script setup>`)**, **Vite**, **Pinia**, **Vue Router** y **Axios**:
- **`src/`**
  - **`assets/css/main.css`**: Design System institucional TecNM (paleta oficial Azul `#1B396A`, Oro `#C5A059`, tipografías y componentes UI).
  - **`components/`**
    - **`common/`**: Componentes reutilizables (`AuditModal.vue`, `ConfirmModal.vue`, `TecnmAutocomplete.vue`, `TecnmBadge.vue`, `TecnmPagination.vue`).
    - **`layout/`**: Estructura de página (`TecnmHeader.vue`, `TecnmNavbar.vue`, `TecnmFooter.vue`).
    - **`search/`**: Búsqueda global multitabla con atajo de teclado (`GlobalSearchModal.vue` vía `Ctrl + K`).
  - **`composables/`**: Lógica reactiva reutilizable (`useAudit.js`, `useConfirm.js`, `useGlobalSearch.js`).
  - **`router/index.js`**: Enrutador central con Navigation Guards y validación estricta de autenticación y permisos RBAC.
  - **`services/api.js`**: Instancia centralizada de Axios con interceptores JWT, manejo de errores y proxy a `/api`.
  - **`stores/auth.js`**: Store Pinia para persistencia de sesión, claims JWT y helpers de permisos (`hasRole`, `hasPermission`).
  - **`views/`**: Vistas funcionales por módulo institucional:
    - **`auth/LoginView.vue`**: Inicio de sesión institucional y redirección por rol.
    - **`dashboard/DashboardView.vue`**: Tablero principal con KPIs semánticos y accesos rápidos.
    - **`students/`**: Directorio de alumnos (`StudentsView.vue`) y expediente del residente (`StudentProfileView.vue`).
    - **`advisors/`**: Directorio docente (`AdvisorsView.vue`) y asignación de asesores (`AdvisorAssignmentView.vue`).
    - **`companies/CompaniesView.vue`**: Directorio de empresas receptoras, sectores y contactos.
    - **`projects/`**: Solicitud de anteproyecto (`ProposalView.vue`) y dictamen de división académica (`ReviewView.vue`).
    - **`activities/ScheduleView.vue`**: Matriz de cronograma de 26 semanas.
    - **`evaluations/`**: Bitácora de asesorías técnicas (`AdvisorySessionsView.vue`) y captura de calificaciones (`GradingView.vue`).
    - **`documents/DocumentsView.vue`**: Expediente digital, subida multipart y visor interactivo de documentos.
    - **`admin/`**: Reportes y libranzas (`ReportsView.vue`), gestión de roles y permisos (`RolesView.vue`), y configuración del sistema (`SystemSettingsView.vue`).

---

## 🔒 Credenciales de Prueba en Desarrollo

| Rol | Correo Institucional | Contraseña |
|-----|----------------------|------------|
| **Super Administrador** | `admin@monclova.tecnm.mx` | `Admin2026!` |
| **Estudiante** | `juan.perez@monclova.tecnm.mx` | `20680123` *(N° Control)* |
| **Asesor Académico** | `fernando.rivera@monclova.tecnm.mx` | `Advisor2026!` |

> **Nota**: Los correos deben pertenecer al dominio institucional `@monclova.tecnm.mx`. Al inicializarse el sistema mediante `DbSeeder.cs`, la base de datos se precarga automáticamente con los 6 roles, usuarios y permisos requeridos.

---

## 🛠️ Comandos Útiles

### Frontend (Vite + Vue 3)

| Comando | Descripción |
|---------|-------------|
| `pnpm dev` / `npm run dev` | Inicia el servidor de desarrollo Vite en `http://localhost:5000` |
| `pnpm build` / `npm run build` | Compila los assets de producción en la carpeta `dist/` |
| `pnpm preview` / `npm run preview` | Previsualiza localmente el build de producción |

### Backend (.NET 10) y Base de Datos

| Comando | Descripción |
|---------|-------------|
| `dotnet build` | Compila la solución/proyecto C# Backend |
| `dotnet run` | Ejecuta la API REST en `http://localhost:5144` |
| `dotnet test` | Ejecuta las pruebas unitarias y de integración |
| `docker-compose up -d postgres` | Inicia el contenedor PostgreSQL 17 |
| `docker-compose logs -f` | Muestra los logs en tiempo real de los contenedores Docker |
| `docker-compose down` | Detiene y remueve los contenedores en ejecución |


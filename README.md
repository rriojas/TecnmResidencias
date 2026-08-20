# Sistema de Residencias Profesionales TecNM v2

Sistema integral web para la gestión, seguimiento, evaluación, dictaminación y expediente digital de Residencias Profesionales del Tecnológico Nacional de México (TecNM).

---

## 🏛️ Descripción General

El sistema está desarrollado con una arquitectura limpia **Vertical Slice (Screaming Architecture)** en el backend (.NET 10 Web API) y **Razor Pages** en el frontend, ofreciendo alto rendimiento, trazabilidad mediante auditoría inmutable, RBAC (Control de Acceso Basado en Roles) con permisos granulares y cumplimiento estricto con los estándares de identidad gráfica institucional TecNM (Azul `#1B396A` y Oro `#C5A059`).

---

## 👥 Roles del Sistema (6 Roles Oficiales)

El sistema cuenta con **6 roles oficiales** con permisos y ámbitos de control independientes definidos mediante RBAC:

1. **Super Administrador (`admin`)**:
   - **Ámbito**: Control total y global del sistema.
   - **Funciones**: Administración global de usuarios, configuración y asignación de roles y permisos RBAC, gestión de catálogos institucionales, mantenimiento del sistema y auditoría completa.

2. **Académicos y Jefatura de División (`academico` / `departmenthead`)**:
   - **Ámbito**: Gestión académica por carrera y departamento.
   - **Funciones**: Alta y administración de estudiantes y asesores, verificación de elegibilidad académica, dictamen/revisión de anteproyectos de residencia y asignación de asesores académicos.

3. **Gestión Tecnológica y Vinculación (`vinculacion`)**:
   - **Ámbito**: Vinculación institucional y gestión de convenios.
   - **Funciones**: Alta y administración del catálogo de empresas e instituciones receptoras, solicitudes de perfiles, emisión de cartas de presentación y liberación, y seguimiento del expediente documental de vinculación.

4. **Director / Directivos (`director`)**:
   - **Ámbito**: Supervisión institucional de solo lectura.
   - **Funciones**: Acceso de solo lectura global a todos los módulos para auditoría directiva, visualización del dashboard de indicadores clave (KPIs), métricas de desempeño y exportación de reportes.

5. **Asesores Académicos (`advisor`)**:
   - **Ámbito**: Seguimiento y evaluación académica de residentes asignados.
   - **Funciones**: Control y validación del cronograma de 26 semanas, registro de bitácoras y sesiones de asesoría, revisión de avances y captura de calificaciones parciales y finales.

6. **Estudiantes (`student`)**:
   - **Ámbito**: Autogestión del proceso de residencia profesional.
   - **Funciones**: Registro y actualización de perfil, solicitud e inscripción de anteproyectos vinculados a empresas, reporte semanal de avances en la matriz de 26 semanas, carga de evidencias y seguimiento a su expediente digital.

---

## 🚀 Guía de Inicio Rápido

### Requisitos Previos

- **.NET 10 SDK** (para desarrollo local de Backend y Frontend)
- **Docker & Docker Compose** (para PostgreSQL 17/18 y despliegue en contenedores)
- **PostgreSQL 17+** (si se ejecuta localmente sin Docker)

---

### Opción A: Despliegue con Docker Compose (Recomendado)

Para iniciar la base de datos PostgreSQL y el servidor Backend API automáticamente:

```bash
docker-compose up -d --build
```

Esto ejecutará:
1. **Contenedor PostgreSQL 17** (`residencia-v2-db`) en el puerto `5432` inicializado con el esquema DDL (`docs/database/schema_pg18.sql`) y datos semilla (`docs/database/seed_v2.sql`).
2. **Contenedor Backend Web API** (`residencia-v2-backend`) en el puerto `5144`.

Para ejecutar el **Frontend (Razor Pages)**:

```bash
cd RTecNM_V2_Frontend
dotnet run
```
Accede desde tu navegador a: **`http://localhost:5000/auth/login`**

---

### Opción B: Ejecución Local en Desarrollo

#### 1. Iniciar Base de Datos PostgreSQL (vía Docker)

```bash
docker-compose up -d postgres
```

#### 2. Iniciar Backend Web API (C# .NET 10)

```bash
cd RTecNM_V2_Backend
dotnet restore
dotnet build
dotnet run
```
La API REST estará escuchando en `http://localhost:5144` (Swagger / OpenAPI interactivo disponible en desarrollo).

#### 3. Iniciar Frontend UI (C# .NET 10 Razor Pages)

```bash
cd RTecNM_V2_Frontend
dotnet restore
dotnet build
dotnet run
```
El servidor web UI estará disponible en `http://localhost:5000`.

---

## 📁 Estructura del Proyecto

```text
TecnmResidencias/
├── docker-compose.yml              # Arquitectura de contenedores Docker
├── README.md                       # Documentación principal del proyecto
├── .gitignore                      # Exclusiones globales de Git
├── GUIA_IMPLEMENTACION_BUSQUEDA_GLOBAL.md # Guía del motor de búsqueda multitabla
├── docs/                           # Documentación de arquitectura y scripts SQL
│   ├── database/                   # Esquemas SQL, semillas y migración
│   └── specs/                      # Especificaciones de los módulos 00 al 09
├── RTecNM_V2_Backend/              # API REST C# .NET 10 (Vertical Slice)
└── RTecNM_V2_Frontend/             # UI Razor Pages C# .NET 10
```

### Detalle de Módulos y Arquitectura

#### 1. `RTecNM_V2_Backend/` (Servidor API REST)
Estructurado bajo patrón **Vertical Slice Colocation** (controlador, servicio, repositorio, DTOs y mapeos en un solo lugar):
- **`Auth/`**: Autenticación JWT, gestión de roles (6 roles oficiales), permisos RBAC y usuarios.
- **`Students/`**: Gestión de expedientes de alumnos, carreras, periodos lectivos y elegibilidad.
- **`Advisors/`**: Registro de asesores internos/externos, especialidades y departamentos.
- **`Projects/`**: Propuestas de anteproyectos de residencia, flujo de dictamen y generación de PDF.
- **`Activities/`**: Cronograma interactivo de 26 semanas y seguimiento de avances.
- **`Evaluations/`**: Bitácoras de asesorías y evaluaciones parciales y finales.
- **`Companies/`**: Directorio de empresas receptoras, sectores y contactos.
- **`Documents/`**: Carga y validación de expediente digital (PDFs, cartas de presentación y liberación).
- **`Searches/`**: Motor de búsqueda global multitabla con vistas en PostgreSQL.
- **`Admin/`**: Dashboard con KPIs, métricas de residencias y exportación de reportes.

#### 2. `RTecNM_V2_Frontend/` (Servidor UI Razor Pages)
- **`Pages/`**: Razor Pages (.cshtml) por módulo funcional (`Auth`, `Dashboard`, `Students`, `Advisors`, `Projects`, `Activities`, `Evaluations`, `Companies`, `Documents`, `Admin`).
- **`Pages/Shared/_GlobalSearchModal.cshtml`**: Modal universal de búsqueda multitabla (accesible mediante `Ctrl + K`).
- **`wwwroot/assets/`**:
  - **`css/`**: Tokens de diseño (`tecnm-theme.css`) e identidad institucional TecNM.
  - **`js/`**: Módulos JS nativos (`layout.js`, `ui.js`, `global-search.js`, scripts funcionales por módulo).

---

## 🔒 Credenciales de Prueba en Desarrollo

| Rol | Correo Institucional | Contraseña |
|-----|----------------------|------------|
| **Super Administrador** | `admin@monclova.tecnm.mx` | `Admin2026!` |
| **Estudiante** | `juan.perez@monclova.tecnm.mx` | `20680123` *(N° Control)* |
| **Asesor Académico** | `fernando.rivera@monclova.tecnm.mx` | `Advisor2026!` |

> **Nota**: Los correos deben pertenecer al dominio institucional `@monclova.tecnm.mx`. En la primera ejecución con `DbSeeder.cs`, la base de datos se inicializa automáticamente con los 6 roles y permisos requeridos.

---

## 🛠️ Comandos Útiles

| Comando | Descripción |
|---------|-------------|
| `dotnet build` | Compila la solución/proyecto C# |
| `dotnet test` | Ejecuta las pruebas unitarias y de integración |
| `docker-compose logs -f` | Muestra los logs en tiempo real de los contenedores Docker |
| `docker-compose up -d --build` | Reconstruye e inicia todos los servicios Docker |

# Sistema de Residencias Profesionales TecNM v2

Sistema integral web para la gestión, seguimiento, evaluación y dictaminación de Residencias Profesionales del Tecnológico Nacional de México (TecNM).

---

## 🏛️ Descripción General

El sistema está desarrollado con arquitectura limpia **Vertical Slice (Screaming Architecture)** y **Razor Pages** para ofrecer alto rendimiento, trazabilidad mediante auditoría inmutable, RBAC (Control de Acceso Basado en Roles) y cumplimiento estricto con los estándares de identidad gráfica institucional TecNM.

### Roles del Sistema

- **Administrador / Jefe de División (`admin`, `departmenthead`)**: Gestión total de alumnos, asesores, proyectos, empresas, calificaciones y reportes.
- **Asesor (`advisor`)**: Seguimiento de alumnos asignados, registro de bitácora de asesorías y captura de calificaciones.
- **Estudiante (`student`)**: Solicitud de anteproyectos, consulta de cronograma de actividades (26 semanas) y avances.
- **Director / Consulta (`director`)**: Rol de solo lectura para auditoría y visualización de indicadores.

---

## 🚀 Guía de Inicio Rápido (Cómo Levantarlo)

### Requisitos Previos

- **.NET 10 SDK** (para desarrollo local)
- **Docker & Docker Compose** (para PostgreSQL y despliegue por contenedores)
- **PostgreSQL 17+** (si se ejecuta sin Docker)

---

### Opción A: Despliegue con Docker Compose (Recomendado)

Ejecuta el siguiente comando en la raíz del proyecto para iniciar la base de datos PostgreSQL 17 y el Backend Web API automáticamente:

```bash
docker-compose up -d --build
```

Esto iniciará:
1. **Contenedor PostgreSQL 17** (`residencia-v2-db`) en puerto `5432` inicializado con el esquema `docs/database/schema_pg18.sql` y datos semilla `docs/database/seed_v2.sql`.
2. **Contenedor Backend Web API** (`residencia-v2-backend`) en puerto `5144`.

Para iniciar el **Frontend (Razor Pages)**:

```bash
cd RTecNM_V2_Frontend
dotnet run
```
Accede en tu navegador a: `http://localhost:5000/auth/login`

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
API estará escuchando en `http://localhost:5144` (Swagger / OpenAPI habilitado en modo desarrollo).

#### 3. Iniciar Frontend UI (C# .NET 10 Razor Pages)

```bash
cd RTecNM_V2_Frontend
dotnet restore
dotnet build
dotnet run
```
Aplicación web disponible en `http://localhost:5000`.

---

## 📁 Estructura del Proyecto y Análisis de Carpetas

```text
TecnmResidencias/
├── docker-compose.yml
├── README.md
├── GUIA_IMPLEMENTACION_BUSQUEDA_GLOBAL.md
├── docs/
├── RTecNM_V2_Backend/
└── RTecNM_V2_Frontend/
```

### 1. `docker-compose.yml`
Archivo de orquestación de contenedores Docker. Define:
- Servicio **PostgreSQL 17-alpine** con volúmenes persistentes y montaje automático del esquema SQL (`schema_pg18.sql`) y semillas (`seed_v2.sql`).
- Servicio **Backend API** configurado con variables de entorno para conexión a base de datos y volumen de almacenamiento de archivos adjuntos (`uploads`).

### 2. `docs/` - Documentación y Scripts de Base de Datos
- **`database/`**:
  - `schema_pg18.sql`: Definición DDL completa de la base de datos PostgreSQL 18/17 (tablas, llaves foráneas, índices y triggers de auditoría).
  - `seed_v2.sql`: Datos iniciales de pruebas y usuarios del sistema.
  - `migrate_v1_to_v2.py`: Script de migración de datos desde versiones previas.
- **`specs/`**: Especificaciones funcionales y técnicas detalladas del módulo 00 al 09 (Autenticación, Estudiantes, Asesores, Anteproyectos, Actividades, Evaluaciones, Reportes y Sistema de Diseño UI).

### 3. `RTecNM_V2_Backend/` - Servidor Backend (API REST C# .NET 10)
Estructurado bajo **Vertical Slice Colocation** (cada módulo incluye Controlador, Servicio, Repositorio, DTOs y Configuración EF Core):

- **`Auth/`**: Autenticación JWT, RBAC, gestión de roles y permisos granulares.
- **`Students/`**: Gestión de perfiles de estudiantes, números de control, carreras y promedios.
- **`Advisors/`**: Registro de asesores internos y externos, departamentos y asignaciones.
- **`Projects/`**: Gestión de solicitudes de anteproyectos de residencia, dictaminación (Aprobado/Rechazado/Cancelado) y generación de PDF.
- **`Activities/`**: Matriz del cronograma de 26 semanas y seguimiento de avances de actividades.
- **`Evaluations/`**: Bitácora de reuniones de asesoría y captura de calificaciones parciales y finales.
- **`Companies/`**: Directorio de empresas receptoras vinculadas y contactos.
- **`Documents/`**: Carga y validación de expedientes digitales (formatos PDF, constancias, reportes).
- **`Searches/`**: Motor de búsqueda universal / global multitabla con filtrado avanzado y paginación servidor.
- **`Admin/`**: Métricas del dashboard institucional, indicadores clave (KPIs) y exportación de reportes PDF.
- **`Common/`**: Clases base (`BaseEntity`), configuración DbContext, utilidades JWT, formateador PDF y servicio de usuario actual.

### 4. `RTecNM_V2_Frontend/` - Servidor UI (Razor Pages C# .NET 10)
Aplicación web server-rendered con JavaScript modular centralizado y arquitectura CSS sin frameworks SPA:

- **`Pages/`**: Razor Pages (.cshtml) agrupadas por funcionalidad:
  - `Auth/Login.cshtml`: Pantalla de inicio de sesión institucional.
  - `Dashboard/Index.cshtml`: Tablero principal con KPIs por rol.
  - `Students/`: Administración y expediente del alumno.
  - `Advisors/`: Directorio de asesores.
  - `Projects/`: Registro de propuesta (`Proposal.cshtml`) y revisión/dictamen (`Review.cshtml`).
  - `Activities/Schedule.cshtml`: Matriz interactiva de 26 semanas.
  - `Evaluations/`: Bitácora de asesorías (`Index.cshtml`) y calificaciones (`Grading.cshtml`).
  - `Companies/`: Registro y directorio de empresas.
  - `Documents/`: Gestión de expediente digital.
  - `Shared/_GlobalSearchModal.cshtml`: Modal universal de búsqueda multitabla (Ctrl + K).
- **`wwwroot/assets/`**:
  - **`css/`**: Sistema de diseño 100% centralizado (`tecnm-theme.css` tokens de diseño, `main.css` estilos primarios e identidad gráfica TecNM Azul `#1B396A` y Oro `#C5A059`).
  - **`js/`**: Módulos JavaScript nativos (`layout.js` guardias de rol y sesión, `ui.js` sistema de alertas flotantes, `global-search.js` buscador universal, y scripts por módulo).

---

## 🔒 Credenciales de Prueba en Desarrollo

- **Administrador**: `admin@monclova.tecnm.mx`
- **Asesor**: `asesor@monclova.tecnm.mx`
- **Estudiante**: `estudiante@monclova.tecnm.mx`

---

## 🛠️ Comandos Útiles

| Comando | Descripción |
|---------|-------------|
| `dotnet build` | Compila la solución/proyecto C# |
| `dotnet test` | Ejecuta las pruebas unitarias e integración |
| `docker-compose logs -f` | Muestra los logs en tiempo real de los contenedores |

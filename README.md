# Sistema de Residencias Profesionales TecNM v2

Sistema integral web para la gestión, seguimiento, evaluación, dictaminación y expediente digital de Residencias Profesionales del Tecnológico Nacional de México (TecNM).

---

## 🏛️ Descripción General

El sistema está desarrollado con una arquitectura desacoplada de alto rendimiento y grado empresarial:

- **Backend**: API REST desarrollada en **C# (.NET 10 Web API)** bajo el patrón **Vertical Slice Architecture (Screaming Architecture)**. Utiliza **PostgreSQL 18-alpine** como motor de base de datos relacional, implementa un sistema de **auditoría inmutable de 10 campos** (`CreatedAt`, `CreatedBy`, `CreatedIp`, `UpdatedAt`, `UpdatedBy`, `UpdatedIp`, `DeletedAt`, `DeletedBy`, `DeletedIp`, `IsDeleted`) con borrado lógico (*Soft Delete*), autenticación robusta mediante tokens **JWT Bearer**, control de acceso basado en roles (**RBAC**) con 6 roles institucionales y permisos granulares por módulo.
- **Frontend**: Single Page Application (SPA) moderna desarrollada con **Vue 3 (Composition API + `<script setup>`)**, **Vite**, **Pinia** (gestión de estado reactivo) y **Vue Router** (con guardias de navegación RBAC), cumpliendo con los estándares de identidad gráfica institucional TecNM (Azul `#1B396A` y Oro `#C5A059`).
- **Infraestructura**: Despliegue empaquetado y orquestado mediante **Docker** y **Nginx** como Proxy Reverso y servidor Web estático para la SPA.

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
   - **Funciones**: Alta y administración del catálogo de empresas e instituciones receptoras, emisión de cartas de presentación y liberación, y seguimiento del expediente documental de vinculación (con restricciones de edición sobre dictámenes académicos y anteproyectos).

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

## 🐳 Estrategia de Contenedores y Modos de Ejecución

El proyecto utiliza una estrategia dual de contenedores en Docker orientada a maximizar tanto la productividad en desarrollo como la seguridad e insulación en producción.

```text
                  ┌─────────────────────────────────────────────────────────────┐
                  │                 MODO PRODUCCIÓN (3 Contenedores)            │
                  │                                                             │
Cliente Web ─────►│  [residencia-v2-frontend]                                     │
(Navegador)       │    └── Nginx (Puerto 80/5085) ──(Proxy /api/)─┐              │
                  │                                                ▼            │
                  │                                      [residencia-v2-backend]│
                  │                                        └── Web API (.NET 10)│
                  │                                                │            │
                  │                                                ▼            │
                  │                                      [residencia-v2-db]     │
                  │                                        └── PostgreSQL 18    │
                  └─────────────────────────────────────────────────────────────┘

                  ┌─────────────────────────────────────────────────────────────┐
                  │                MODO DESARROLLADOR (1 Contenedor DB)         │
                  │                                                             │
Vite Dev Server ─►│  Host Local: Frontend Vue 3 (Puerto 5085 / Hot Reload)        │
.NET API Server ─►│  Host Local: Backend Web API (.NET 10 / Puerto 5185)         │
                  │       │                                                     │
                  │       └──────────────────────┐                              │
                  │                              ▼                              │
                  │                   [residencia-v2-db] (Docker - Puerto 5439) │
                  └─────────────────────────────────────────────────────────────┘
```

> [!IMPORTANT]
> **Comparativa: Producción (3 Contenedores) vs Desarrollador (1 Contenedor DB)**
> - **Modo Producción / Staging (3 Contenedores)**: Todos los servicios (`residencia-v2-db`, `residencia-v2-backend` y `residencia-v2-frontend`) corren dentro de la red aislada de Docker. Garantiza reproducibilidad exacta ("Write once, run anywhere"), cero dependencias instaladas en el servidor anfitrión y Proxy Reverso seguro con Nginx.
> - **Modo Desarrollador (1 Contenedor DB)**: Se ejecuta en Docker exclusivamente la base de datos PostgreSQL (`residencia-v2-db` en puerto `5439`). El Backend .NET 10 (`dotnet run` en `:5185`) y el Frontend SPA (`pnpm dev` en `:5085`) se ejecutan directamente en el sistema operativo host.
> - **¿Por qué esta decisión?**: Evita tener que reconstruir imágenes Docker pesadas con cada pequeño cambio de código en desarrollo. Permite **Hot Reload instantáneo (Vite HMR)** en frontend, **recarga en caliente/compilación veloz** en C# .NET 10, **depuración fluida con breakpoints directos** desde la IDE (VS Code / Rider / Visual Studio) y reduce sustancialmente el consumo de recursos de CPU y memoria RAM.

---

## 🚀 Guía de Inicio Rápido

### Requisitos Previos

- **Node.js 20+** y gestor de paquetes (**pnpm** o **npm**) para el Frontend SPA (Vue 3 / Vite).
- **.NET 10 SDK** para el servidor Backend Web API.
- **Docker & Docker Compose** (para PostgreSQL 18-alpine y despliegue en contenedores).
- **PostgreSQL 17/18** (opcional, solo si se ejecuta sin Docker).

---

### Opción A: Despliegue Automatizado en Producción / Servidor Ubuntu (`deploy-server.sh` — Recomendado para Servidor)

Para desplegar automáticamente el stack completo en un servidor de producción (Ubuntu 24.04 LTS / Debian):

```bash
chmod +x deploy-server.sh
./deploy-server.sh
```

El script ejecuta un flujo de despliegue completo de producción:
1. Verificación de **Docker** y **Docker Compose v2**.
2. Diagnóstico y liberación interactiva de los puertos `5085` (Frontend Nginx), `5185` (Backend API) y `5439` (PostgreSQL DB).
3. Creación y asignación de permisos de almacenamiento persistente (`uploads/documents`, `uploads/templates/excel`).
4. Compilación e inicio en segundo plano de los 3 contenedores (`docker compose up -d --build`).
5. Verificación de salud (*Healthcheck*) automatizada en bucle hasta detectar la respuesta de la API REST.
6. Resumen de despliegue con URLs e IP del servidor.

---

### Opción B: Despliegue Manual en Producción / Staging (3 Contenedores)

Para iniciar manualmente todos los servicios del stack encapsulados en contenedores Docker:

```bash
docker-compose up -d --build
```

Esto levantará automáticamente los 3 contenedores:
1. **`residencia-v2-db`** (PostgreSQL 18-alpine en puerto `5439`): Inicializado con esquemas DDL, semillas esenciales (`docs/database/01_schema_and_essential_seeds.sql`) y datos demo opcionales (`docs/database/02_demo_data.sql`).
2. **`residencia-v2-backend`** (Web API C# .NET 10 en puerto `5185`): API REST compilada en Release, conectada internamente a la BD.
3. **`residencia-v2-frontend`** (Nginx + Vue 3 SPA en puerto `5085:80`): Servidor Web estático Nginx que sirve los assets compilados y procesa las llamadas `/api/*` mediante Reverse Proxy transparente hacia el contenedor del Backend.

---

### Opción C: Modo Desarrollador (Scripts de Inicio Rápido — Recomendado en Dev)

Se incluyen scripts automatizados para PowerShell (Windows) y Bash (Linux/macOS) que verifican requisitos, inician el contenedor de PostgreSQL en Docker y ejecutan Backend y Frontend en el host con depuración activa y puerto liberado:

- **Levantar todo el Stack en Desarrollo (Base de Datos en Docker + Backend & Frontend en Host):**
  - PowerShell: `.\start-all.ps1`
  - Bash: `./start-all.sh`

- **Levantar solo Backend (Contenedor PostgreSQL + API REST .NET 10 en Host):**
  - PowerShell: `.\start-backend.ps1`
  - Bash: `./start-backend.sh`

- **Levantar solo Frontend SPA (Vite + Vue 3 en Host):**
  - PowerShell: `.\start-frontend.ps1`
  - Bash: `./start-frontend.sh`

---

### Opción D: Ejecución Manual Paso a Paso en Desarrollo

#### 1. Iniciar Base de Datos PostgreSQL (vía Docker)

```bash
docker-compose up -d postgres
```
*PostgreSQL escuchará en `localhost:5439` con la base de datos `postgre_recidencias`.*

#### 2. Iniciar Backend Web API (C# .NET 10 en Host)

```bash
cd RTecNM_V2_Backend
dotnet restore
dotnet build
dotnet run
```
*La API REST estará escuchando en `http://localhost:5185` (Swagger/OpenAPI interactivo disponible en `http://localhost:5185/swagger`).*

#### 3. Iniciar Frontend SPA (Vite + Vue 3 en Host)

```bash
cd RTecNM_V2_Frontend
pnpm install   # O bien: npm install
pnpm dev       # O bien: npm run dev
```
*El cliente web estará disponible en `http://localhost:5085` (con proxy reverso Vite configurado internamente hacia `http://localhost:5185`).*

---

## 📁 Estructura del Proyecto

```text
TecnmResidencias/
├── docker-compose.yml              # Orquestación de contenedores Docker (Postgres, Backend, Frontend)
├── deploy-server.sh                # Script de despliegue automatizado para servidor de producción (Ubuntu)
├── .env.example                    # Variables de entorno globales del proyecto (puertos y credenciales)
├── README.md                       # Documentación principal del proyecto
├── GUIA_MIGRACION_FRONTEND.md      # Hoja de ruta y detalles de migración a Vue 3
├── GUIA_IMPLEMENTACION_BUSQUEDA_GLOBAL.md # Especificación del motor de búsqueda multitabla
├── start-all.ps1 / .sh             # Script para iniciar todo el stack en desarrollo con verificación de puertos
├── start-backend.ps1 / .sh         # Script para iniciar Backend local y Postgres en Docker
├── start-frontend.ps1 / .sh        # Script para iniciar Frontend Vite local
├── docs/                           # Documentación técnica y scripts SQL
│   ├── database/                   # Esquemas DDL, semillas y datos demo SQL
│   └── specs/                      # Especificaciones funcionales de módulos 00 al 09
├── RTecNM_V2_Backend/              # API REST en C# .NET 10 (Vertical Slice Architecture)
└── RTecNM_V2_Frontend/             # Frontend SPA en Vite + Vue 3 + Nginx
```

---

## ⚙️ Análisis Detallado del Backend y sus Componentes

El servidor Backend (`RTecNM_V2_Backend`) está construido en **C# (.NET 10 Web API)** implementando el patrón de diseño **Vertical Slice Architecture (Screaming Architecture)**. A diferencia de las arquitecturas tradicionales por capas (Controladores, Servicios, Repositorios globales separadas), Vertical Slice organiza el código en rebanadas funcionales autosuficientes agrupadas por módulo de negocio.

### 🏛️ Características Transversales de la Arquitectura Backend

1. **Patrón Vertical Slice (Colocation)**: Cada módulo contiene sus propios controladores (`*Controller.cs`), interfaces y servicios (`*Service.cs`), repositorios (`*Repository.cs`), Data Transfer Objects (`DTOs`) y validaciones en la misma estructura modular.
2. **Auditoría Inmutable en 10 Campos**: Todas las entidades principales heredan una estructura de auditoría integral y transparente que registra:
   - `CreatedAt`, `CreatedBy`, `CreatedIp` (Creación de registros)
   - `UpdatedAt`, `UpdatedBy`, `UpdatedIp` (Modificaciones)
   - `DeletedAt`, `DeletedBy`, `DeletedIp`, `IsDeleted` (Borrado lógico o *Soft Delete*)
3. **Control de Acceso basado en Roles (RBAC Granular)**: Autenticación vía JWT Bearer Tokens con validación de issuer/audience y tiempo de expiración estricto (`ClockSkew = TimeSpan.Zero`). Asignación dinámica de permisos por módulo y rol.
4. **Acceso a Datos Optimizado**: Uso de **Entity Framework Core 10 + Npgsql** mapeando tipos nativos de PostgreSQL 18.

---

### 🧩 Detalle de Todos y Cada uno de los Componentes del Backend

El backend se compone de **11 módulos funcionales**:

#### 1. `Auth/` (Autenticación y Seguridad RBAC)
- **Responsabilidad**: Gestión de identidad de usuarios, hashing seguro de contraseñas con `BCrypt.Net`, generación y validación de JSON Web Tokens (JWT), refresco de claims, limpieza/sincronización de perfiles según cambios de rol y control de permisos RBAC.
- **Componentes Clave**: `AuthController`, `RoleController`, `AuthService`, `RoleService`, `AuthRepository`, `RoleRepository`, `DbSeeder`.
- **Destacado**: `DbSeeder.cs` ejecuta automáticamente al iniciar la API la creación e inserción idempotente de los 6 roles institucionales, módulos, matriz de permisos y el usuario Super Administrador por defecto (`admin@monclova.tecnm.mx`).

#### 2. `Students/` (Expedientes y Gestión Estudiantil)
- **Responsabilidad**: Alta, edición, consulta y expediente integral del estudiante residente. Control de carreras institucionales (ISC, IIA, IME, CP, LA, IE), semestres, periodos lectivos, validación estricta del rol `student` y dictaminación del estado de elegibilidad (Elegible, No Elegible, En Proceso).
- **Componentes Clave**: `StudentController`, `StudentService`, `StudentRepository`, `StudentDto`, `StudentConfiguration`.

#### 3. `Advisors/` (Asesores Académicos y Docentes)
- **Responsabilidad**: Registro y administración de asesores internos y externos, catálogo de especialidades académicas, departamentos, validación estricta del rol `advisor`, seguimiento de carga docente y asignación de residentes a asesores.
- **Componentes Clave**: `AdvisorController`, `AdvisorService`, `AdvisorRepository`, `AdvisorDto`, `AdvisorAssignmentDto`.

#### 4. `Projects/` (Anteproyectos de Residencia Profesional)
- **Responsabilidad**: Gestión del ciclo de vida del anteproyecto de residencia: registro de propuesta, vinculación con empresa receptora, definición de banco de proyectos u opción propia, objetivos generales y específicos, y flujo de dictamen académico (Aprobado, Aprobado con Modificaciones, Rechazado).
- **Componentes Clave**: `ProjectController`, `ProjectService`, `ProjectRepository`, `ProjectObjectiveDto`, `ProjectReviewDto`.
- **Generación de PDF**: Integra `QuestPDF` para la emisión automatizada de dictámenes y formatos oficiales de anteproyecto.

#### 5. `Activities/` (Cronograma de 26 Semanas y Seguimiento)
- **Responsabilidad**: Matriz dinámica de planeación y seguimiento semanal de actividades del proyecto durante el periodo lectivo de 26 semanas. Permite el reporte de avances por parte del alumno y la validación/observaciones por parte del asesor.
- **Componentes Clave**: `ActivityController`, `ActivityService`, `ActivityRepository`, `WeeklyActivityDto`, `WeeklyProgressDto`.

#### 6. `Evaluations/` (Evaluación y Bitácora de Asesorías)
- **Responsabilidad**: Registro de las sesiones de asesoría técnica obligatorias y captura de evaluaciones cuantitativas y cualitativas de reportes parciales (Primer y Segundo Reporte) y Evaluación Final con ponderaciones oficiales TecNM.
- **Componentes Clave**: `EvaluationController`, `EvaluationService`, `EvaluationRepository`, `AdvisorySessionDto`, `EvaluationDto`.

#### 7. `Companies/` (Directorio de Empresas e Instituciones Receptoras)
- **Responsabilidad**: Administración del catálogo de empresas, instituciones públicas/privadas y organizaciones receptoras de residentes. Gestión de sectores industriales, convenios de colaboración vigentes, convenios marco y contactos/asesores externos.
- **Componentes Clave**: `CompanyController`, `CompanyService`, `CompanyRepository`, `CompanyDto`.

#### 8. `Documents/` (Expediente Digital Documental)
- **Responsabilidad**: Gestión de carga multipart, almacenamiento en disco persistente (`/app/uploads/documents`), clasificación por tipo documental (Solicitud, Carta de Aceptación, Carta de Presentación, Reportes, Carta de Liberación) y servidor seguro de descarga/previsualización PDF.
- **Componentes Clave**: `DocumentController`, `DocumentService`, `DocumentRepository`, `DocumentDto`.

#### 9. `Searches/` (Motor de Búsqueda Global Multitabla)
- **Responsabilidad**: Búsqueda global unificada y de alto rendimiento que escanea simultáneamente múltiples entidades (Alumnos, Asesores, Empresas, Anteproyectos, Documentos) en una sola petición HTTP.
- **Componentes Clave**: `SearchController`, `SearchService`, `SearchRegistry`.
- **Optimización SQL**: Respaldado por vistas de base de datos optimizadas (`vistas_busqueda.sql`) con índices de texto en PostgreSQL.

#### 10. `Admin/` (Dashboard de Indicadores y Configuración)
- **Responsabilidad**: Métricas ejecutivas y tablero de control para directivos/administradores (KPIs de residencias activas, alumnos concluidos, distribución por carrera y empresa), exportación masiva a Excel (`MiniExcel`) y PDFs (`QuestPDF`), y configuración de parámetros globales.
- **Componentes Clave**: `DashboardMetricsService`, `ReportGeneratorService`, `ExcelTemplateSeeder`.

#### 11. `Common/` (Infraestructura, Auditoría y Notificaciones)
- **Responsabilidad**: Núcleo de infraestructura transversal del backend.
- **Componentes Clave**:
  - `AppDbContext`: Contexto central de EF Core que aplica configuraciones fluentes (`IEntityTypeConfiguration`) de todas las entidades.
  - `CurrentUserService`: Extrae del `HttpContext` el ID del usuario autenticado y la dirección IP origen para poblar automáticamente los 10 campos de auditoría inmutable.
  - `Notifications/`: Motor de envío de correos electrónicos en segundo plano (`EmailBackgroundWorker` de tipo `IHostedService`, `EmailQueue`, `EmailTemplateService` con MailKit/MimeKit, configurado por defecto con credenciales de `noreply@monclova.tecnm.mx`).
  - `Settings/`: Servicio de configuración dinámica de parámetros del sistema (`ISystemSettingService`, `SystemSettingConfiguration`).

---

## 🌐 Uso de Docker y Nginx como Reverse Proxy

El proyecto aprovecha las tecnologías de contenedores **Docker** y el servidor de alto rendimiento **Nginx** para garantizar un despliegue seguro, optimizado y sin problemas de compatibilidad entre entornos.

```text
                              CONTENEDOR FRONTEND (Nginx)
                     ┌───────────────────────────────────────────┐
                     │                                           │
Petición Web UI ────►│ Port 80 (Host 5085)                        │
(HTTP / Static)      │ ├── /          ──► /usr/share/nginx/html  │
                     │ │                  (Vue 3 Build Assets)   │
                     │ │                                         │
Petición API ───────►│ └── /api/*     ──► Proxy Pass HTTP        │
(HTTP / REST)        │                    http://backend:5185    │
                     └──────────────────────────┬────────────────┘
                                                │ (Red Interna Docker)
                                                ▼
                              CONTENEDOR BACKEND (.NET 10 API)
                     ┌───────────────────────────────────────────┐
                     │ Port 5185                                 │
                     │ └── ASPNETCORE_URLS=http://+:5185         │
                     └───────────────────────────────────────────┘
```

### 1. Rol de Nginx en Producción (`nginx.conf`)

El archivo [`nginx.conf`](file:///home/lux_az/Documentos/Dev/Recidencias/TecnmResidencias/RTecNM_V2_Frontend/nginx.conf) cumple dos funciones críticas en el contenedor `residencia-v2-frontend`:
- **Servidor Web Estático Ultra-Rápido**: Entrega los archivos Javascript, CSS y HTML compilados por Vite desde `/usr/share/nginx/html` utilizando compresión y manejo eficiente de rutas SPA (`try_files $uri $uri/ /index.html`).
- **Reverse Proxy (Proxy Reverso API)**: Intercepta todas las llamadas cuyo path inicia con `/api/` y las reenvía internamente al contenedor `http://backend:5185/api/`.

#### Ventajas del Reverse Proxy Nginx:
- **Eliminación de CORS en Producción**: Al servirse la SPA y la API bajo el mismo origen virtual en el puerto `5085/80`, el navegador no bloquea peticiones por Same-Origin Policy (SOP).
- **Ocultamiento de Topología Interna**: La API REST backend no necesita exponerse públicamente a la red externa si no se desea; Nginx actúa como puerta de enlace perimetral.
- **Inyección de Encabezados de Red**: Transfiere de forma transparente encabezados como `X-Real-IP`, `X-Forwarded-For` y `X-Forwarded-Proto` necesarios para que el servicio de auditoría inmutable del Backend registre la IP real del cliente.

### 2. Construcción de Imágenes Multi-Stage en Docker

Tanto el backend como el frontend utilizan **Multi-Stage Builds** para mantener las imágenes finales lo más pequeñas y seguras posible:

- **Backend (`RTecNM_V2_Backend/Dockerfile`)**:
  - *Etapa 1 (Build)*: Utiliza `mcr.microsoft.com/dotnet/sdk:10.0` para restaurar dependencias NuGet y compilar la API REST en modo Release.
  - *Etapa 2 (Runtime Final)*: Copia únicamente los binarios publicados a la imagen ligera `mcr.microsoft.com/dotnet/aspnet:10.0`, reduciendo drásticamente el tamaño de la imagen y omitiendo herramientas de compilación no necesarias en ejecución.

- **Frontend (`RTecNM_V2_Frontend/Dockerfile`)**:
  - *Etapa 1 (Build)*: Utiliza `node:20-alpine` para instalar paquetes vía `npm` y generar la compilación optimizada de producción (`dist/`).
  - *Etapa 2 (Runtime Final)*: Copia únicamente la carpeta `dist/` y el archivo `nginx.conf` a una imagen ultraligera de `nginx:alpine`, expuesta en el puerto `80`.

---

## 🏛️ Justificación Arquitectónica (Architectural Rationale)

La arquitectura de este sistema fue seleccionada meticulosamente para resolver las necesidades particulares de gestión académica del TecNM, garantizando alta confiabilidad, mantenibilidad a largo plazo y ciclo de vida de desarrollo ágil:

### 1. ¿Por qué Vertical Slice Architecture (Screaming Architecture)?
- **Problema de la Arquitectura en Capas Tradicional**: En arquitecturas monolíticas por capas (Controllers, Services, Repositories), una sola funcionalidad (ej. registrar un anteproyecto) requiere modificar 5 o 6 archivos esparcidos en carpetas totalmente alejadas.
- **Solución Vertical Slice**: Organiza el proyecto en rebanadas verticales acopladas al **dominio de negocio**. Cada carpeta (`Projects`, `Students`, `Auth`) grita inmediatamente lo que hace el sistema. Cambiar el flujo de anteproyectos no afecta al módulo de empresas ni de evaluaciones, reduciendo la fricción entre desarrolladores y previniendo efectos secundarios colaterales.

### 2. ¿Por qué C# .NET 10 + PostgreSQL 18?
- **Rendimiento y Escalabilidad**: .NET 10 es una de las plataformas Web API de mayor velocidad en procesamiento de peticiones por segundo a nivel mundial.
- **Seguridad Tipada y Mantenibilidad**: C# 13 ofrece tipado estático fuerte, manejo transparente de asincronía (`async/await`) y una integración madura con **EF Core 10**.
- **PostgreSQL 18**: Base de datos relacional de grado empresarial, elegida por su soporte nativo para vistas SQL optimizadas en el motor de búsqueda multitabla, soporte JSONB, integridad referencial inquebrantable y licencia Open Source sin costos de licenciamiento institucional.

### 3. ¿Por qué Desacoplamiento SPA (Vue 3) + REST API (.NET 10)?
- **Separación Limpia de Responsabilidades**: El frontend es responsable únicamente de la experiencia de usuario (UX/UI reactiva) y el backend es responsable de la lógica de negocio, validaciones, seguridad y persistencia de datos.
- **Interoperabilidad**: Permite consumir la API REST desde otros clientes en el futuro (ej. aplicaciones móviles Android/iOS para residentes o asesores, quioscos informativos o sistemas de integración de la DITEC).

### 4. ¿Por qué la Estrategia Dual de Contenedores (1 en Dev vs 3 en Prod)?
- **Productividad en Desarrollo**: El desarrollo de software requiere bucles de retroalimentación inmediata. Levantar el frontend y backend en el sistema operativo host permite reflejar cambios de código instantáneamente (Hot Reload en Vue y dotnet watch/run) sin perder tiempo en la reconstrucción de contenedores.
- **Confiabilidad en Producción**: Empaquetar todo en 3 contenedores Docker con Nginx garantiza que el sistema funcione exactamente igual en cualquier servidor institucional del TecNM o infraestructura Cloud sin colisiones de versiones de software o dependencias faltantes.

---

## 📁 Estructura del Frontend (`RTecNM_V2_Frontend/`)

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
| `pnpm dev` / `npm run dev` | Inicia el servidor de desarrollo Vite en `http://localhost:5085` |
| `pnpm build` / `npm run build` | Compila los assets de producción en la carpeta `dist/` |
| `pnpm preview` / `npm run preview` | Previsualiza localmente el build de producción |

### Backend (.NET 10) y Base de Datos

| Comando | Descripción |
|---------|-------------|
| `dotnet build` | Compila la solución/proyecto C# Backend |
| `dotnet run` | Ejecuta la API REST en `http://localhost:5185` |
| `dotnet test` | Ejecuta las pruebas unitarias y de integración |
| `./deploy-server.sh` | Ejecuta el flujo automatizado de despliegue en servidor de producción (Ubuntu) |
| `docker-compose up -d postgres` | Inicia únicamente el contenedor PostgreSQL 18 (Modo Dev) |
| `docker-compose up -d --build` | Inicia los 3 contenedores Docker (Modo Producción) |
| `docker-compose logs -f` | Muestra los logs en tiempo real de los contenedores Docker |
| `docker-compose down` | Detiene y remueve los contenedores en ejecución |

<script setup>
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import apiClient from '@/services/api'
import TecnmBadge from '@/components/common/TecnmBadge.vue'

const router = useRouter()
const authStore = useAuthStore()

const TOTAL_WEEKS = 26
const CAREERS = {
  1: 'Ing. Informática',
  2: 'Ing. Industrial',
  3: 'Ing. Mecatrónica',
  4: 'Ing. en Sistemas Computacionales',
}

const isLoading = ref(true)
const adminMetrics = ref({})
const recentProjects = ref([])
const pendingProjects = ref([])

// Datos para Estudiante
const studentProfile = ref(null)
const studentProjects = ref([])
const studentDocs = ref([])
const completedWeeksCount = ref(0)

// Datos para Asesor
const advisorProjects = ref([])

const welcomeTitle = computed(() => {
  const role = authStore.currentRole
  if (role === 'admin') return 'Panel de Administración General'
  if (role === 'departmenthead') return 'Panel de la División Académica'
  if (role === 'advisor') return 'Portal de Asesoría de Residencias'
  if (role === 'student') return 'Portal del Estudiante Residente'
  if (role === 'director') return 'Panel Ejecutivo de Dirección'
  return 'Panel Principal - Sistema de Residencias'
})

const welcomeDescription = computed(() => {
  const role = authStore.currentRole
  if (role === 'admin') return 'Gestión institucional de alumnos, asesores, anteproyectos y reportes de residencia.'
  if (role === 'departmenthead') return 'Revisión de anteproyectos, dictámenes y avance general de residencias.'
  if (role === 'advisor') return 'Seguimiento de los residentes a tu cargo, dictámenes pendientes y evaluaciones.'
  if (role === 'student') return 'Seguimiento de tu anteproyecto, avance semanal y expediente digital.'
  if (role === 'director') return 'Vista ejecutiva y consulta global de indicadores del sistema de residencias.'
  return 'Sistema de Residencias Profesionales - TecNM Campus Monclova.'
})

const latestStudentProject = computed(() => {
  if (!studentProjects.value.length) return null
  const priority = {
    in_progress: 1,
    approved: 2,
    under_review: 3,
    pending: 4,
    proposed: 5,
    draft: 6,
    completed: 7,
    rejected: 8,
    cancelled: 9,
  }
  const sorted = [...studentProjects.value].sort((a, b) => {
    const pa = priority[(a.status || '').toLowerCase()] || 99
    const pb = priority[(b.status || '').toLowerCase()] || 99
    if (pa !== pb) return pa - pb
    return new Date(b.createdAt) - new Date(a.createdAt)
  })
  return sorted[0] || null
})

const studentProgressPercent = computed(() => {
  return Math.min(100, Math.round((completedWeeksCount.value / TOTAL_WEEKS) * 100))
})

const approvedDocsCount = computed(() => {
  return studentDocs.value.filter((d) => String(d.status).toLowerCase() === 'approved').length
})

const studentTasks = computed(() => {
  const tasks = []
  if (!latestStudentProject.value) {
    tasks.push({ text: 'Registrar tu anteproyecto', href: '/projects/proposal' })
    return tasks
  }

  const s = (latestStudentProject.value.status || '').toLowerCase()
  if (s === 'completed') {
    tasks.push({ text: 'Consultar calificaciones oficiales finales', href: '/evaluations/grades' })
    tasks.push({ text: 'Descargar constancias del expediente digital', href: '/documents' })
    tasks.push({ text: 'Consultar cronograma histórico concluido', href: '/activities/schedule' })
    return tasks
  }

  if (s === 'pending' || s === 'under_review' || s === 'proposed' || s === 'pendiente') {
    tasks.push({ text: 'Tu anteproyecto está en dictamen por la Academia', href: '/projects/proposal' })
    return tasks
  }

  if (s === 'draft') {
    tasks.push({ text: 'Completar y enviar solicitud de anteproyecto', href: '/projects/proposal' })
    return tasks
  }

  if (s === 'rejected') {
    tasks.push({ text: 'Corregir observaciones de tu anteproyecto', href: '/projects/proposal' })
    return tasks
  }

  const docsByType = {}
  studentDocs.value.forEach((d) => { docsByType[d.documentType] = d })

  if (!docsByType['solicitud']) tasks.push({ text: 'Subir tu Solicitud de Residencia', href: '/documents' })
  if (!docsByType['carta_aceptacion']) tasks.push({ text: 'Subir tu Carta de Aceptación', href: '/documents' })
  if (completedWeeksCount.value === 0) tasks.push({ text: 'Registrar avance en tu cronograma', href: '/activities/schedule' })

  return tasks
})

function formatTecNMDate(iso) {
  if (!iso) return '—'
  const d = new Date(iso)
  if (isNaN(d.getTime())) return '—'
  const MONTHS = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre']
  return `${String(d.getDate()).padStart(2, '0')}/${MONTHS[d.getMonth()]}/${d.getFullYear()}`
}

async function loadDashboard() {
  isLoading.value = true
  const role = authStore.currentRole

  try {
    if (authStore.isAdmin || role === 'departmenthead' || role === 'director') {
      const [mRes, rRes, pRes] = await Promise.all([
        apiClient.get('/v1/admin/dashboard').catch(() => ({ data: {} })),
        apiClient.get('/v1/projects', { params: { pageNumber: 1, pageSize: 5 } }).catch(() => ({ data: { items: [] } })),
        apiClient.get('/v1/projects', { params: { status: 'pending', pageNumber: 1, pageSize: 5 } }).catch(() => ({ data: { items: [] } })),
      ])
      adminMetrics.value = mRes.data || {}
      recentProjects.value = (rRes.data.items || []).filter((p) => p.isActive !== false)
      pendingProjects.value = (pRes.data.items || []).filter((p) => p.isActive !== false)
    } else if (role === 'student') {
      const [sRes, pRes] = await Promise.all([
        apiClient.get('/v1/students/me').catch(() => ({ data: null })),
        apiClient.get('/v1/projects/me', { params: { pageNumber: 1, pageSize: 10 } }).catch(() => ({ data: { items: [] } })),
      ])
      studentProfile.value = sRes.data
      studentProjects.value = pRes.data.items || []

      if (latestStudentProject.value) {
        const [dRes, aRes] = await Promise.all([
          apiClient.get(`/v1/documents/project/${latestStudentProject.value.id}`, { params: { pageNumber: 1, pageSize: 50 } }).catch(() => ({ data: { items: [] } })),
          apiClient.get(`/v1/projects/${latestStudentProject.value.id}/activities`).catch(() => ({ data: [] })),
        ])
        studentDocs.value = dRes.data.items || []

        const completedSet = new Set()
        const activities = Array.isArray(aRes.data) ? aRes.data : []
        activities.forEach((act) => {
          (act.progresses || []).forEach((pr) => {
            if (String(pr.status).toLowerCase() === 'completed') completedSet.add(pr.weekNumber)
          })
        })
        completedWeeksCount.value = completedSet.size
      }
    } else if (role === 'advisor') {
      const res = await apiClient.get('/v1/projects', { params: { pageNumber: 1, pageSize: 10 } }).catch(() => ({ data: { items: [] } }))
      advisorProjects.value = res.data.items || []
    }
  } catch (err) {
    console.error('Error al inicializar dashboard:', err)
  } finally {
    isLoading.value = false
  }
}

onMounted(() => {
  loadDashboard()
})
</script>

<template>
  <div>
    <!-- Encabezado de Página -->
    <div class="tecnm-actions-bar">
      <div>
        <h1 id="welcomeTitle" class="tecnm-page-title">{{ welcomeTitle }}</h1>
        <p id="welcomeDescription" class="tecnm-page-subtitle">{{ welcomeDescription }}</p>
      </div>
    </div>

    <!-- ========================================== -->
    <!-- 1. KPIs PARA ADMIN / JEFE DE DIVISIÓN / DIRECTOR -->
    <!-- ========================================== -->
    <div
      v-if="authStore.isAdmin || authStore.hasRole('departmenthead', 'director')"
      id="statsSection"
      class="kpi-grid"
      data-kpi-count="6"
    >
      <div class="kpi-card">
        <span class="kpi-icon">
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Zm8.25 2.25a2.625 2.625 0 1 1-5.25 0 2.625 2.625 0 0 1 5.25 0Z" />
          </svg>
        </span>
        <span class="kpi-body">
          <span class="kpi-label">Estudiantes Registrados</span>
          <span class="kpi-value">{{ adminMetrics.totalStudents ?? (isLoading ? '...' : 0) }}</span>
        </span>
      </div>

      <div class="kpi-card">
        <span class="kpi-icon">
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M18 18.72a9.094 9.094 0 0 0 3.741-.479 3 3 0 0 0-4.682-2.72m.94 3.198.001.031c0 .225-.012.447-.037.666A11.944 11.944 0 0 1 12 21c-2.17 0-4.207-.576-5.963-1.584A6.062 6.062 0 0 1 6 18.719m12 0a5.971 5.971 0 0 0-.941-3.197m0 0A5.995 5.995 0 0 0 12 12.75a5.995 5.995 0 0 0-5.058 2.772m0 0a3 3 0 0 0-4.681 2.72 8.986 8.986 0 0 0 3.74.477m.94-3.197a5.971 5.971 0 0 0-.94 3.197M15 6.75a3 3 0 1 1-6 0 3 3 0 0 1 6 0Zm6 3a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Zm-13.5 0a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Z" />
          </svg>
        </span>
        <span class="kpi-body">
          <span class="kpi-label">Asesores Activos</span>
          <span class="kpi-value">{{ adminMetrics.activeAdvisors ?? (isLoading ? '...' : 0) }}</span>
        </span>
      </div>

      <div class="kpi-card">
        <span class="kpi-icon">
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z" />
          </svg>
        </span>
        <span class="kpi-body">
          <span class="kpi-label">Proyectos Registrados</span>
          <span class="kpi-value">{{ adminMetrics.totalProjects ?? (isLoading ? '...' : 0) }}</span>
        </span>
      </div>

      <div class="kpi-card kpi-card--green">
        <span class="kpi-icon">
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12c0 1.268-.63 2.39-1.593 3.068a3.745 3.745 0 0 1-1.043 3.296 3.745 3.745 0 0 1-3.296 1.043A3.745 3.745 0 0 1 12 21c-2.17 0-2.39-.63-3.068-1.593a3.746 3.746 0 0 1-3.296-1.043 3.746 3.746 0 0 1-1.043-3.296A3.745 3.745 0 0 1 3 12c0-1.268.63-2.39 1.593-3.068a3.745 3.745 0 0 1 1.043-3.296 3.746 3.746 0 0 1 3.296-1.043A3.746 3.746 0 0 1 12 3c1.268 0 2.39.63 3.068 1.593a3.746 3.746 0 0 1 3.296 1.043 3.746 3.746 0 0 1 1.043 3.296A3.745 3.745 0 0 1 21 12Z" />
          </svg>
        </span>
        <span class="kpi-body">
          <span class="kpi-label">Proyectos Aprobados</span>
          <span class="kpi-value">{{ adminMetrics.approvedProjects ?? (isLoading ? '...' : 0) }}</span>
        </span>
      </div>

      <div class="kpi-card kpi-card--gold">
        <span class="kpi-icon">
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
          </svg>
        </span>
        <span class="kpi-body">
          <span class="kpi-label">Por Dictaminar</span>
          <span class="kpi-value">{{ adminMetrics.pendingProjects ?? (isLoading ? '...' : 0) }}</span>
        </span>
      </div>

      <div class="kpi-card kpi-card--green">
        <span class="kpi-icon">
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
          </svg>
        </span>
        <span class="kpi-body">
          <span class="kpi-label">Residencias Completadas</span>
          <span class="kpi-value">{{ adminMetrics.completedResidencies ?? (isLoading ? '...' : 0) }}</span>
        </span>
      </div>
    </div>

    <!-- ========================================== -->
    <!-- 2. KPIs PARA ESTUDIANTE -->
    <!-- ========================================== -->
    <div
      v-else-if="authStore.currentRole === 'student'"
      id="statsSection"
      class="kpi-grid"
      data-kpi-count="4"
    >
      <div class="kpi-card">
        <span class="kpi-icon">
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z" /></svg>
        </span>
        <span class="kpi-body">
          <span class="kpi-label">Anteproyectos</span>
          <span class="kpi-value">{{ studentProjects.length }}</span>
        </span>
      </div>

      <div class="kpi-card">
        <span class="kpi-icon">
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 0 1 2.25-2.25h13.5A2.25 2.25 0 0 1 21 7.5v11.25m-18 0A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75m-18 0v-7.5A2.25 2.25 0 0 1 5.25 9h13.5A2.25 2.25 0 0 1 21 11.25v7.5" /></svg>
        </span>
        <span class="kpi-body">
          <span class="kpi-label">Semanas Completadas</span>
          <span class="kpi-value">{{ completedWeeksCount }} / {{ TOTAL_WEEKS }}</span>
        </span>
      </div>

      <div class="kpi-card kpi-card--green">
        <span class="kpi-icon">
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M2.25 12.75V12A2.25 2.25 0 0 1 4.5 9.75h15A2.25 2.25 0 0 1 21.75 12v.75m-8.69-6.44-2.12-2.12a1.5 1.5 0 0 0-1.061-.44H4.5A2.25 2.25 0 0 0 2.25 6v12a2.25 2.25 0 0 0 2.25 2.25h15A2.25 2.25 0 0 0 21.75 18V9a2.25 2.25 0 0 0-2.25-2.25h-5.379a1.5 1.5 0 0 1-1.06-.44Z" /></svg>
        </span>
        <span class="kpi-body">
          <span class="kpi-label">Documentos Aprobados</span>
          <span class="kpi-value">{{ approvedDocsCount }}</span>
        </span>
      </div>

      <div class="kpi-card kpi-card--gold">
        <span class="kpi-icon">
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M11.48 3.499a.562.562 0 0 1 1.04 0l2.125 5.111a.563.563 0 0 0 .475.345l5.518.442c.499.04.701.663.321.988l-4.204 3.602a.563.563 0 0 0-.182.557l1.285 5.385a.562.562 0 0 1-.84.61l-4.725-2.885a.562.562 0 0 0-.586 0L6.982 20.54a.562.562 0 0 1-.84-.61l1.285-5.386a.562.562 0 0 0-.182-.557l-4.204-3.602a.562.562 0 0 1 .321-.988l5.518-.442a.563.563 0 0 0 .475-.345L11.48 3.5Z" /></svg>
        </span>
        <span class="kpi-body">
          <span class="kpi-label">Promedio General</span>
          <span class="kpi-value">{{ studentProfile?.gpa != null ? Number(studentProfile.gpa).toFixed(1) : '—' }}</span>
        </span>
      </div>
    </div>

    <!-- ========================================== -->
    <!-- 3. DASHBOARD GRID PRINCIPAL Y LATERAL -->
    <!-- ========================================== -->
    <div class="dashboard-grid">
      <!-- Columna Principal -->
      <div class="dashboard-main">
        <!-- VISTA ADMIN: Tabla de Anteproyectos Recientes -->
        <div
          v-if="authStore.isAdmin || authStore.hasRole('departmenthead', 'director')"
          id="contentCard"
          class="tecnm-card"
        >
          <div class="tecnm-card-header">
            <h3 class="tecnm-card-title">Anteproyectos Recientes</h3>
          </div>
          <div class="tecnm-card-body">
            <div class="tecnm-table-responsive">
              <table class="tecnm-table">
                <thead>
                  <tr>
                    <th>Título del Proyecto</th>
                    <th>Estudiante</th>
                    <th>Fecha</th>
                    <th>Estado</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-if="isLoading">
                    <td colspan="4" class="tecnm-table-empty">Cargando anteproyectos...</td>
                  </tr>
                  <tr v-else-if="recentProjects.length === 0">
                    <td colspan="4" class="tecnm-table-empty">No hay anteproyectos registrados.</td>
                  </tr>
                  <tr v-for="p in recentProjects" v-else :key="p.id">
                    <td>{{ p.title }}</td>
                    <td>{{ p.studentName || 'Estudiante' }}</td>
                    <td>{{ formatTecNMDate(p.createdAt) }}</td>
                    <td>
                      <TecnmBadge :status="p.status" />
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <!-- VISTA ESTUDIANTE: Tarjeta Mi Residencia y Avance -->
        <div
          v-else-if="authStore.currentRole === 'student'"
          id="contentCard"
          class="tecnm-card"
        >
          <div class="tecnm-card-header">
            <h3 class="tecnm-card-title">Mi Residencia</h3>
          </div>
          <div class="tecnm-card-body tecnm-card-body--loose">
            <div v-if="studentProfile" class="tecnm-profile-grid">
              <div>
                <span class="tecnm-field-label">Nombre</span>
                <span class="tecnm-field-value tecnm-field-value-emphasis">{{ studentProfile.firstName }} {{ studentProfile.lastName }}</span>
              </div>
              <div>
                <span class="tecnm-field-label">No. Control</span>
                <span class="tecnm-field-value">{{ studentProfile.controlNumber }}</span>
              </div>
              <div>
                <span class="tecnm-field-label">Carrera</span>
                <span class="tecnm-field-value">{{ CAREERS[studentProfile.careerId] || '—' }}</span>
              </div>
            </div>

            <div v-if="latestStudentProject" class="tecnm-mt-3" style="margin-top: 1rem;">
              <div class="tecnm-d-flex tecnm-justify-between tecnm-align-center tecnm-flex-wrap tecnm-gap-2">
                <div>
                  <span class="tecnm-field-label">Anteproyecto</span>
                  <span class="tecnm-field-value tecnm-field-value-emphasis">{{ latestStudentProject.title }}</span>
                </div>
                <TecnmBadge :status="latestStudentProject.status" />
              </div>
            </div>
            <div v-else class="tecnm-alert tecnm-alert-warning" style="margin-top: 1rem;">
              Aún no has registrado tu anteproyecto de residencia.
            </div>

            <div class="tecnm-form-group" style="margin-top: 1.25rem;">
              <span class="tecnm-label">Avance Semanal</span>
              <div class="progress-track">
                <div class="progress-fill" :style="{ width: `${studentProgressPercent}%` }"></div>
              </div>
              <span class="progress-label">{{ studentProgressPercent }}% del avance semanal registrado</span>
            </div>
          </div>
        </div>

        <!-- Acceso Rápido / Role Actions -->
        <section v-if="authStore.currentRole === 'student'" class="dashboard-section">
          <h2 class="dashboard-section-title">Acceso Rápido</h2>
          <div class="action-cards">
            <router-link to="/projects/proposal" class="action-card">
              <span class="action-card-icon">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="m16.862 4.487 1.687-1.688a1.875 1.875 0 1 1 2.652 2.652L10.582 16.07a4.5 4.5 0 0 1-1.897 1.13L6 18l.8-2.685a4.5 4.5 0 0 1 1.13-1.897l8.932-8.931Zm0 0L19.5 7.125M18 14v4.75A2.25 2.25 0 0 1 15.75 21H5.25A2.25 2.25 0 0 1 3 18.75V8.25A2.25 2.25 0 0 1 5.25 6H10" /></svg>
              </span>
              <span class="action-card-body">
                <span class="action-card-title">Anteproyecto</span>
                <span class="action-card-sub">Registrar o dar seguimiento</span>
              </span>
            </router-link>

            <router-link to="/activities/schedule" class="action-card">
              <span class="action-card-icon">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 0 1 2.25-2.25h13.5A2.25 2.25 0 0 1 21 7.5v11.25m-18 0A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75m-18 0v-7.5A2.25 2.25 0 0 1 5.25 9h13.5A2.25 2.25 0 0 1 21 11.25v7.5" /></svg>
              </span>
              <span class="action-card-body">
                <span class="action-card-title">Cronograma</span>
                <span class="action-card-sub">Avance de tus 26 semanas</span>
              </span>
            </router-link>

            <router-link to="/documents" class="action-card">
              <span class="action-card-icon">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M2.25 12.75V12A2.25 2.25 0 0 1 4.5 9.75h15A2.25 2.25 0 0 1 21.75 12v.75m-8.69-6.44-2.12-2.12a1.5 1.5 0 0 0-1.061-.44H4.5A2.25 2.25 0 0 0 2.25 6v12a2.25 2.25 0 0 0 2.25 2.25h15A2.25 2.25 0 0 0 21.75 18V9a2.25 2.25 0 0 0-2.25-2.25h-5.379a1.5 1.5 0 0 1-1.06-.44Z" /></svg>
              </span>
              <span class="action-card-body">
                <span class="action-card-title">Expediente Digital</span>
                <span class="action-card-sub">Documentos y evidencias</span>
              </span>
            </router-link>
          </div>
        </section>
      </div>

      <!-- Columna Lateral / Side Panel -->
      <div id="sidePanel" class="dashboard-side">
        <!-- VISTA ADMIN: Cola de Dictamen -->
        <div
          v-if="authStore.isAdmin || authStore.hasRole('departmenthead', 'director')"
          class="panel-card"
        >
          <h3 class="panel-title">Cola de Dictamen</h3>
          <ul class="list-panel">
            <li v-if="isLoading" class="list-panel-empty">Cargando anteproyectos pendientes...</li>
            <li v-else-if="pendingProjects.length === 0" class="list-panel-empty">
              Sin anteproyectos por dictaminar. ¡Al día!
            </li>
            <li
              v-for="p in pendingProjects"
              v-else
              :key="p.id"
              class="list-panel-item"
            >
              <div>
                <div class="list-panel-item-title">{{ p.title }}</div>
                <div class="list-panel-item-sub">{{ p.studentName || 'Estudiante' }}</div>
              </div>
            </li>
          </ul>

          <div class="tecnm-d-flex tecnm-flex-wrap tecnm-gap-2" style="margin-top: 1rem; display: flex; gap: 0.5rem; flex-wrap: wrap;">
            <router-link to="/projects/review" class="tecnm-btn tecnm-btn-outline tecnm-btn-sm">
              Ir al Dictamen
            </router-link>
            <router-link to="/admin/reports" class="tecnm-btn tecnm-btn-outline tecnm-btn-sm">
              Reportes y Liberación
            </router-link>
          </div>
        </div>

        <!-- VISTA ESTUDIANTE: Tareas Pendientes -->
        <div
          v-else-if="authStore.currentRole === 'student'"
          class="panel-card panel-card--loose"
        >
          <h3 class="panel-title">Tareas Pendientes</h3>
          <ul class="list-panel">
            <li v-if="studentTasks.length === 0" class="list-panel-empty">
              Sin tareas pendientes. ¡Vas al día!
            </li>
            <li
              v-for="(task, idx) in studentTasks"
              v-else
              :key="idx"
              class="list-panel-item"
            >
              <router-link :to="task.href" class="list-panel-link">
                {{ task.text }}
              </router-link>
            </li>
          </ul>
        </div>
      </div>
    </div>
  </div>
</template>

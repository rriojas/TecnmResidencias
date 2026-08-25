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
const selectedCareerFilter = ref('all')

// Datos para Estudiante
const studentProfile = ref(null)
const studentProjects = ref([])
const studentDocs = ref([])
const completedWeeksCount = ref(0)

// Datos para Asesor
const advisorProfile = ref(null)
const advisorProjects = ref([])

const welcomeTitle = computed(() => {
  const role = authStore.currentRole
  if (role === 'admin') return 'Panel de Administración General'
  if (role === 'departmenthead') return 'Panel de la División Académica'
  if (role === 'vinculacion') return 'Panel de Gestión Tecnológica y Vinculación'
  if (role === 'advisor') return 'Portal de Asesoría de Residencias'
  if (role === 'student') return 'Portal del Estudiante Residente'
  if (role === 'director') return 'Panel Ejecutivo de Dirección'
  return 'Panel Principal - Sistema de Residencias'
})

const welcomeDescription = computed(() => {
  const role = authStore.currentRole
  if (role === 'admin') return 'Gestión institucional de alumnos, asesores, anteproyectos y reportes de residencia.'
  if (role === 'departmenthead') return 'Revisión y dictamen de anteproyectos, asignación de asesores y avance académico.'
  if (role === 'vinculacion') return 'Gestión de empresas receptoras, cartas de presentación, convenios y expedientes.'
  if (role === 'advisor') return 'Seguimiento de los residentes a tu cargo, validación de avances semanales y evaluaciones.'
  if (role === 'student') return 'Seguimiento de tu anteproyecto, avance semanal y expediente digital.'
  if (role === 'director') return 'Vista ejecutiva y consulta global de indicadores del sistema de residencias.'
  return 'Sistema de Residencias Profesionales - TecNM Campus Monclova.'
})

const isStaff = computed(() => {
  return (
    authStore.isAdmin ||
    authStore.hasRole('departmenthead', 'director', 'vinculacion', 'academic') ||
    (authStore.currentRole !== 'student' && authStore.currentRole !== 'advisor')
  )
})

// Filtro de proyectos por carrera en staff
const filteredRecentProjects = computed(() => {
  if (selectedCareerFilter.value === 'all') return recentProjects.value
  return recentProjects.value.filter((p) => String(p.careerId) === String(selectedCareerFilter.value))
})

const filteredPendingProjects = computed(() => {
  if (selectedCareerFilter.value === 'all') return pendingProjects.value
  return pendingProjects.value.filter((p) => String(p.careerId) === String(selectedCareerFilter.value))
})

// Computados para Asesor
const advisorTotalResidents = computed(() => advisorProjects.value.length)

const advisorActiveResidents = computed(() => {
  return advisorProjects.value.filter((p) => {
    const s = String(p.status || '').toLowerCase()
    return ['in_progress', 'inprogress', 'en_progreso', 'en progreso', 'approved', 'aprobado'].includes(s)
  }).length
})

const advisorPendingDictamen = computed(() => {
  return advisorProjects.value.filter((p) => {
    const s = String(p.status || '').toLowerCase()
    return ['pending', 'under_review', 'proposed', 'pendiente'].includes(s)
  }).length
})

const advisorCompletedResidents = computed(() => {
  return advisorProjects.value.filter((p) => {
    const s = String(p.status || '').toLowerCase()
    return ['completed', 'completado', 'finalizado'].includes(s)
  }).length
})

const advisorInitials = computed(() => {
  const name = authStore.userDisplayName || 'Asesor'
  const parts = name.split(' ').filter(Boolean)
  if (parts.length >= 2) return (parts[0][0] + parts[1][0]).toUpperCase()
  return name.slice(0, 2).toUpperCase()
})

// Computados para Estudiante
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

const projectStepInfo = computed(() => {
  if (!latestStudentProject.value) return null
  const s = String(latestStudentProject.value.status || '').trim().toLowerCase()

  const isDraft = ['draft', 'borrador'].includes(s)
  const isReview = ['pending', 'pendiente', 'proposed', 'under_review', 'in_review', 'en_revision', 'en revision'].includes(s)
  const isRejected = ['rejected', 'rechazado', 'correcciones', 'correcciones requeridas'].includes(s)
  const isApproved = ['approved', 'aprobado', 'autorizado', 'vigente'].includes(s)
  const isInProgress = ['in_progress', 'inprogress', 'en_progreso', 'en progreso'].includes(s)
  const isCompleted = ['completed', 'completado', 'finalizado'].includes(s)

  return {
    step1: {
      completed: !isDraft,
      active: isDraft,
      label: 'Borrador',
    },
    line1: !isDraft,
    step2: {
      completed: isApproved || isInProgress || isCompleted,
      active: isReview,
      warning: isRejected,
      label: isRejected ? 'Correcciones' : 'En Revisión',
    },
    line2: isApproved || isInProgress || isCompleted,
    step3: {
      completed: isInProgress || isCompleted,
      active: isApproved,
      label: 'Dictamen Aprobado',
    },
    line3: isInProgress || isCompleted,
    step4: {
      completed: isCompleted,
      active: isInProgress,
      label: 'En Residencia',
    },
  }
})

const studentInitials = computed(() => {
  if (!studentProfile.value) return 'E'
  const f = studentProfile.value.firstName ? studentProfile.value.firstName[0].toUpperCase() : ''
  const l = studentProfile.value.lastName ? studentProfile.value.lastName[0].toUpperCase() : ''
  return `${f}${l}` || 'E'
})

const studentFullName = computed(() => {
  if (!studentProfile.value) return 'Estudiante'
  return [studentProfile.value.firstName, studentProfile.value.lastName, studentProfile.value.lastName2]
    .filter(Boolean)
    .join(' ')
    .trim()
})

const studentTasks = computed(() => {
  const tasks = []
  if (!latestStudentProject.value) {
    tasks.push({ text: 'Registrar tu anteproyecto', href: '/projects/proposal' })
    return tasks
  }

  const s = (latestStudentProject.value.status || '').toLowerCase()
  if (s === 'completed') {
    tasks.push({ text: 'Consultar calificaciones oficiales finales', href: '/evaluations/grading' })
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
  studentDocs.value.forEach((d) => {
    docsByType[d.documentType] = d
  })

  if (!docsByType['solicitud']) tasks.push({ text: 'Subir tu Solicitud de Residencia', href: '/documents' })
  if (!docsByType['carta_aceptacion']) tasks.push({ text: 'Subir tu Carta de Aceptación', href: '/documents' })
  if (completedWeeksCount.value === 0) tasks.push({ text: 'Registrar avance en tu cronograma', href: '/activities/schedule' })

  return tasks
})

function formatTecNMDate(iso) {
  if (!iso) return '—'
  const d = new Date(iso)
  if (isNaN(d.getTime())) return '—'
  const MONTHS = [
    'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
    'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre',
  ]
  return `${String(d.getDate()).padStart(2, '0')}/${MONTHS[d.getMonth()]}/${d.getFullYear()}`
}

function getProjectWeekProgress(project) {
  if (!project) return 0
  const s = String(project.status || '').toLowerCase()
  if (s === 'completed') return TOTAL_WEEKS
  if (s === 'draft' || s === 'rejected' || s === 'pending') return 0
  return project.completedWeeks || (s === 'in_progress' ? 14 : 0)
}

function getProjectWeekPercent(project) {
  const weeks = getProjectWeekProgress(project)
  return Math.min(100, Math.round((weeks / TOTAL_WEEKS) * 100))
}

async function loadDashboard() {
  isLoading.value = true
  const role = authStore.currentRole

  try {
    if (isStaff.value) {
      const [mRes, rRes, pRes] = await Promise.all([
        apiClient.get('/v1/admin/dashboard').catch(() => ({ data: {} })),
        apiClient.get('/v1/projects', { params: { pageNumber: 1, pageSize: 20 } }).catch(() => ({ data: { items: [] } })),
        apiClient.get('/v1/projects', { params: { status: 'pending', pageNumber: 1, pageSize: 20 } }).catch(() => ({ data: { items: [] } })),
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
          ;(act.progresses || []).forEach((pr) => {
            if (String(pr.status).toLowerCase() === 'completed') completedSet.add(pr.weekNumber)
          })
        })
        completedWeeksCount.value = completedSet.size
      }
    } else if (role === 'advisor') {
      const [advMeRes, advProjRes] = await Promise.all([
        apiClient.get('/v1/advisors/me').catch(() => ({ data: null })),
        apiClient.get('/v1/projects/advisor/me', { params: { pageNumber: 1, pageSize: 50 } }).catch(async () => {
          return apiClient.get('/v1/projects', { params: { pageNumber: 1, pageSize: 50 } }).catch(() => ({ data: { items: [] } }))
        }),
      ])
      advisorProfile.value = advMeRes.data
      const rawItems = advProjRes.data?.items || (Array.isArray(advProjRes.data) ? advProjRes.data : [])
      advisorProjects.value = rawItems.filter((p) => p.isActive !== false)
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

      <!-- Filtro rápido de carrera para Staff y Jefes de División -->
      <div v-if="isStaff && !authStore.hasRole('director')" class="tecnm-page-actions">
        <label for="dashCareerFilter" style="font-size: 0.8125rem; font-weight: 600; color: var(--tecnm-text-secondary); margin-right: 0.25rem;">
          Carrera:
        </label>
        <select
          id="dashCareerFilter"
          v-model="selectedCareerFilter"
          class="tecnm-form-select"
          style="width: auto; min-width: 180px; padding: 0.35rem 0.75rem; font-size: 0.85rem;"
        >
          <option value="all">Todas las Carreras</option>
          <option value="4">Ing. Sistemas Computacionales</option>
          <option value="1">Ing. Informática</option>
          <option value="3">Ing. Mecatrónica</option>
          <option value="2">Ing. Industrial</option>
        </select>
      </div>
    </div>

    <!-- ========================================== -->
    <!-- 1. KPIs PARA ADMIN / VINCULACIÓN / JEFE / DIRECTOR -->
    <!-- ========================================== -->
    <div
      v-if="isStaff"
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
    <!-- 2. KPIs PARA ASESOR ACADÉMICO -->
    <!-- ========================================== -->
    <div
      v-else-if="authStore.currentRole === 'advisor'"
      id="advisorStatsSection"
      class="kpi-grid"
      data-kpi-count="4"
    >
      <div class="kpi-card">
        <span class="kpi-icon">
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Zm8.25 2.25a2.625 2.625 0 1 1-5.25 0 2.625 2.625 0 0 1 5.25 0Z" />
          </svg>
        </span>
        <span class="kpi-body">
          <span class="kpi-label">Residentes a Cargo</span>
          <span class="kpi-value">{{ advisorTotalResidents }}</span>
        </span>
      </div>

      <div class="kpi-card kpi-card--green">
        <span class="kpi-icon">
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 0 1 2.25-2.25h13.5A2.25 2.25 0 0 1 21 7.5v11.25m-18 0A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75m-18 0v-7.5A2.25 2.25 0 0 1 5.25 9h13.5A2.25 2.25 0 0 1 21 11.25v7.5" />
          </svg>
        </span>
        <span class="kpi-body">
          <span class="kpi-label">Residencias Activas</span>
          <span class="kpi-value">{{ advisorActiveResidents }}</span>
        </span>
      </div>

      <div class="kpi-card kpi-card--gold">
        <span class="kpi-icon">
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
          </svg>
        </span>
        <span class="kpi-body">
          <span class="kpi-label">En Revisión / Dictamen</span>
          <span class="kpi-value">{{ advisorPendingDictamen }}</span>
        </span>
      </div>

      <div class="kpi-card kpi-card--green">
        <span class="kpi-icon">
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
          </svg>
        </span>
        <span class="kpi-body">
          <span class="kpi-label">Residencias Concluidas</span>
          <span class="kpi-value">{{ advisorCompletedResidents }}</span>
        </span>
      </div>
    </div>

    <!-- ========================================== -->
    <!-- 3. KPIs PARA ESTUDIANTE -->
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
    <!-- 4. DASHBOARD GRID PRINCIPAL Y LATERAL -->
    <!-- ========================================== -->
    <div class="dashboard-grid">
      <!-- Columna Principal -->
      <div class="dashboard-main">
        <!-- ======================================================== -->
        <!-- VISTA ADMIN / STAFF: Tabla de Anteproyectos Recientes -->
        <!-- ======================================================== -->
        <div
          v-if="isStaff"
          id="contentCard"
          class="tecnm-card"
        >
          <div class="tecnm-card-header">
            <h3 class="tecnm-card-title">
              Anteproyectos Recientes
              <span v-if="selectedCareerFilter !== 'all'" style="font-size: 0.85rem; color: var(--tecnm-blue-primary); font-weight: 500;">
                ({{ CAREERS[selectedCareerFilter] }})
              </span>
            </h3>
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
                  <tr v-else-if="filteredRecentProjects.length === 0">
                    <td colspan="4" class="tecnm-table-empty">No hay anteproyectos registrados para la selección.</td>
                  </tr>
                  <tr v-for="p in filteredRecentProjects" v-else :key="p.id">
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

        <!-- ======================================================== -->
        <!-- VISTA ASESOR: TABLA "MIS RESIDENTES A CARGO" -->
        <!-- ======================================================== -->
        <div
          v-else-if="authStore.currentRole === 'advisor'"
          class="tecnm-card"
        >
          <div class="tecnm-card-header">
            <div class="tecnm-d-flex tecnm-align-center tecnm-gap-2" style="display: flex; align-items: center; gap: 0.5rem;">
              <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" style="color: var(--tecnm-blue-primary, #1b396a);">
                <path stroke-linecap="round" stroke-linejoin="round" d="M18 18.72a9.094 9.094 0 0 0 3.741-.479 3 3 0 0 0-4.682-2.72m.94 3.198.001.031c0 .225-.012.447-.037.666A11.944 11.944 0 0 1 12 21c-2.17 0-4.207-.576-5.963-1.584A6.062 6.062 0 0 1 6 18.719m12 0a5.971 5.971 0 0 0-.941-3.197m0 0A5.995 5.995 0 0 0 12 12.75a5.995 5.995 0 0 0-5.058 2.772m0 0a3 3 0 0 0-4.681 2.72 8.986 8.986 0 0 0 3.74.477m.94-3.197a5.971 5.971 0 0 0-.94 3.197M15 6.75a3 3 0 1 1-6 0 3 3 0 0 1 6 0Zm6 3a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Zm-13.5 0a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Z" />
              </svg>
              <h3 class="tecnm-card-title">Mis Residentes a Cargo</h3>
            </div>
            <span class="tecnm-badge tecnm-badge-neutral">
              {{ advisorProjects.length }} Asignados
            </span>
          </div>

          <div class="tecnm-card-body">
            <div class="tecnm-table-responsive">
              <table class="tecnm-table tecnm-table-striped">
                <thead>
                  <tr>
                    <th>Estudiante</th>
                    <th>Proyecto y Empresa</th>
                    <th>Avance Semanal</th>
                    <th>Estado</th>
                    <th class="tecnm-th-actions" style="text-align: right;">Acciones Directas</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-if="isLoading">
                    <td colspan="5" class="tecnm-table-empty">Cargando residentes a tu cargo...</td>
                  </tr>
                  <tr v-else-if="advisorProjects.length === 0">
                    <td colspan="5" class="tecnm-table-empty">
                      Actualmente no tienes residentes asignados por la Jefatura de División.
                    </td>
                  </tr>
                  <tr v-for="p in advisorProjects" v-else :key="p.id">
                    <td>
                      <strong>{{ p.studentName || 'Estudiante' }}</strong>
                      <div style="font-size: 0.75rem; color: var(--tecnm-text-secondary, #64748b);">
                        {{ p.studentControlNumber ? `Ctrl: ${p.studentControlNumber}` : '' }}
                        {{ p.careerId ? `• ${CAREERS[p.careerId]}` : '' }}
                      </div>
                    </td>
                    <td>
                      <div style="font-weight: 500; max-width: 260px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;">
                        {{ p.title }}
                      </div>
                      <div style="font-size: 0.75rem; color: var(--tecnm-text-secondary, #64748b);">
                        🏢 {{ p.companyName || 'Empresa Receptora' }}
                      </div>
                    </td>
                    <td style="min-width: 140px;">
                      <div style="display: flex; justify-content: space-between; font-size: 0.75rem; margin-bottom: 0.25rem; font-weight: 600; color: var(--tecnm-blue-primary);">
                        <span>{{ getProjectWeekProgress(p) }}/{{ TOTAL_WEEKS }} sem</span>
                        <span>{{ getProjectWeekPercent(p) }}%</span>
                      </div>
                      <div class="progress-track" style="height: 6px; border-radius: 3px;">
                        <div class="progress-fill" :style="{ width: `${getProjectWeekPercent(p)}%`, borderRadius: '3px' }"></div>
                      </div>
                    </td>
                    <td>
                      <TecnmBadge :status="p.status" />
                    </td>
                    <td style="text-align: right;">
                      <div class="tecnm-row-actions" style="justify-content: flex-end; gap: 0.35rem;">
                        <router-link
                          to="/evaluations"
                          class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                          title="Bitácora de Asesorías"
                        >
                          Bitácora
                        </router-link>
                        <router-link
                          to="/activities/schedule"
                          class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                          title="Cronograma de Actividades"
                        >
                          Cronograma
                        </router-link>
                        <router-link
                          to="/evaluations/grading"
                          class="tecnm-btn tecnm-btn-primary tecnm-btn-sm"
                          title="Evaluar Parciales / Final"
                        >
                          Evaluar
                        </router-link>
                      </div>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <!-- ======================================================== -->
        <!-- VISTA ESTUDIANTE: Perfil y Tareas Pendientes -->
        <!-- ======================================================== -->
        <template v-else-if="authStore.currentRole === 'student'">
          <!-- Card de Identidad del Estudiante -->
          <div v-if="studentProfile" class="panel-card panel-card--loose dashboard-student-card">
            <div class="dashboard-student-header">
              <div class="dashboard-student-avatar">
                {{ studentInitials }}
              </div>
              <div class="dashboard-student-names">
                <h4 class="dashboard-student-name">{{ studentFullName }}</h4>
                <span class="dashboard-student-career">{{ CAREERS[studentProfile.careerId] || 'Estudiante' }}</span>
              </div>
            </div>
            
            <div class="dashboard-student-info-list">
              <div class="dashboard-student-info-item">
                <span class="dashboard-info-label">No. Control:</span>
                <strong class="dashboard-info-val">{{ studentProfile.controlNumber || '—' }}</strong>
              </div>
              <div class="dashboard-student-info-item">
                <span class="dashboard-info-label">Correo:</span>
                <span class="dashboard-info-val dashboard-info-val--email">{{ studentProfile.email || '—' }}</span>
              </div>
            </div>

            <div style="margin-top: 0.75rem; border-top: 1px solid var(--tecnm-border-color, #e2e8f0); padding-top: 0.75rem;">
              <router-link to="/students/profile" class="tecnm-btn tecnm-btn-outline tecnm-btn-sm" style="width: 100%; justify-content: center;">
                Ver Expediente Completo &rarr;
              </router-link>
            </div>
          </div>

          <!-- Card de Tareas Pendientes -->
          <div class="panel-card panel-card--loose">
            <div class="tecnm-d-flex tecnm-align-center tecnm-gap-2" style="margin-bottom: 0.75rem; display: flex; align-items: center; gap: 0.5rem;">
              <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" style="color: var(--tecnm-gold-accent, #d4a017);">
                <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-9 3.75h.008v.008H12v-.008z" />
              </svg>
              <h3 class="panel-title" style="margin-bottom: 0;">Tareas y Avisos</h3>
            </div>

            <ul class="list-panel">
              <li v-if="studentTasks.length === 0" class="list-panel-empty">
                Sin tareas pendientes. ¡Vas al día con tu residencia!
              </li>
              <li
                v-for="(task, idx) in studentTasks"
                v-else
                :key="idx"
                class="list-panel-item"
              >
                <router-link :to="task.href" class="list-panel-link">
                  {{ task.text }} &rarr;
                </router-link>
              </li>
            </ul>
          </div>

          <!-- Card de Formatos Oficiales TecNM para Descarga -->
          <div class="panel-card panel-card--loose">
            <div class="tecnm-d-flex tecnm-align-center tecnm-gap-2" style="margin-bottom: 0.75rem; display: flex; align-items: center; gap: 0.5rem;">
              <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" style="color: var(--tecnm-blue-primary, #1b396a);">
                <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 00-3.375-3.375h-1.5A1.125 1.125 0 0113.5 7.125v-1.5a3.375 3.375 0 00-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 00-9-9z" />
              </svg>
              <h3 class="panel-title" style="margin-bottom: 0;">Formatos Oficiales TecNM</h3>
            </div>

            <ul class="list-panel">
              <li class="list-panel-item">
                <router-link to="/documents" class="list-panel-link">
                  📄 Anexo XXIX - Carta de Aceptación &rarr;
                </router-link>
              </li>
              <li class="list-panel-item">
                <router-link to="/documents" class="list-panel-link">
                  📄 Anexo XXX - Solicitud de Residencia &rarr;
                </router-link>
              </li>
              <li class="list-panel-item">
                <router-link to="/activities/schedule" class="list-panel-link">
                  📄 Cronograma Modelo (26 Semanas) &rarr;
                </router-link>
              </li>
            </ul>
          </div>
        </template>
      </div>
    </div>
  </div>
</template>

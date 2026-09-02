<script setup>
import { ref, onMounted, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import apiClient from '@/services/api'
import TecnmBadge from '@/components/common/TecnmBadge.vue'
import TecnmKpiCard from '@/components/common/TecnmKpiCard.vue'
import AdvisorWorkloadModal from '@/components/advisors/AdvisorWorkloadModal.vue'

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
const companiesList = ref([])
const selectedCareerFilter = ref(
  authStore.isCareerHead && authStore.userCareerId
    ? String(authStore.userCareerId)
    : 'all'
)

// Datos para Estudiante
const studentProfile = ref(null)
const studentProjects = ref([])
const studentDocs = ref([])
const completedWeeksCount = ref(0)

// Datos para Asesor
const advisorProfile = ref(null)
const advisorProjects = ref([])

// Datos para Jefe de Carrera
const careerStudents = ref([])
const careerAdvisors = ref([])

const unassignedStudents = computed(() => {
  return careerStudents.value.filter((s) => !s.advisorId)
})

const assignedStudentsCount = computed(() => {
  return careerStudents.value.filter((s) => !!s.advisorId).length
})

const assignmentCoveragePercent = computed(() => {
  if (!careerStudents.value.length) return 100
  return Math.round((assignedStudentsCount.value / careerStudents.value.length) * 100)
})

const selectedAdvisorForModal = ref(null)
const isWorkloadModalOpen = ref(false)

function openAdvisorWorkloadModal(advId) {
  selectedAdvisorForModal.value = advId
  isWorkloadModalOpen.value = true
}

const advisorWorkloadSearch = ref('')

const advisorWorkloadList = computed(() => {
  return careerAdvisors.value.map((adv) => {
    const count = careerStudents.value.filter((s) => Number(s.advisorId) === Number(adv.id)).length
    const initials = (adv.fullName || adv.name || 'NN')
      .trim()
      .split(/\s+/)
      .map((p) => p[0])
      .slice(0, 2)
      .join('')
      .toUpperCase()
    return {
      id: adv.id,
      name: adv.fullName || adv.name,
      title: adv.title,
      count,
      initials,
      percent: Math.min(Math.round((count / 5) * 100), 100),
    }
  }).sort((a, b) => b.count - a.count)
})

const filteredAdvisorWorkload = computed(() => {
  let list = advisorWorkloadList.value
  if (advisorWorkloadSearch.value.trim()) {
    const q = advisorWorkloadSearch.value.trim().toLowerCase()
    list = list.filter((a) => (a.name || '').toLowerCase().includes(q) || (a.title || '').toLowerCase().includes(q))
  }
  return list
})

const careerHeadActiveProjects = computed(() => {
  return recentProjects.value.filter((p) => String(p.status || '').toLowerCase() !== 'draft')
})

const recentUnassignedStudents = computed(() => {
  return [...unassignedStudents.value].reverse().slice(0, 3)
})

const recentActiveProjects = computed(() => {
  return [...careerHeadActiveProjects.value].reverse().slice(0, 3)
})

const welcomeTitle = computed(() => {
  const role = authStore.currentRole
  if (role === 'admin') return 'Panel de Administración General'
  if (role === 'jefecarrera') return 'Panel de Jefatura de Carrera'
  if (role === 'departmenthead' || role === 'academic') return 'Panel de la División Académica'
  if (role === 'vinculacion') return 'Panel de Gestión Tecnológica y Vinculación'
  if (role === 'advisor') return 'Portal de Asesoría de Residencias'
  if (role === 'student') return 'Portal del Estudiante Residente'
  if (role === 'director') return 'Panel Ejecutivo de Dirección'
  return 'Panel Principal - Sistema de Residencias'
})

const welcomeDescription = computed(() => {
  const role = authStore.currentRole
  if (role === 'admin') return 'Gestión institucional de alumnos, asesores, anteproyectos y reportes de residencia.'
  if (role === 'jefecarrera') return 'Asignación de asesores y seguimiento académico de los residentes de tu carrera.'
  if (role === 'departmenthead' || role === 'academic') return 'Revisión y dictamen de anteproyectos, asignación de asesores y avance académico.'
  if (role === 'vinculacion') return 'Gestión de empresas receptoras, cartas de presentación, convenios y expedientes.'
  if (role === 'advisor') return 'Seguimiento de los residentes a tu cargo, validación de avances semanales y evaluaciones.'
  if (role === 'student') return 'Seguimiento de tu anteproyecto, avance semanal y expediente digital.'
  if (role === 'director') return 'Vista ejecutiva y consulta global de indicadores del sistema de residencias.'
  return 'Sistema de Residencias Profesionales - TecNM Campus Monclova.'
})

const isStaff = computed(() => {
  return (
    authStore.isAdmin ||
    authStore.hasRole('departmenthead', 'director', 'vinculacion', 'academic', 'jefecarrera') ||
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

// Tablas limitadas a Top 5 para el dashboard
const topRecentProjects = computed(() => filteredRecentProjects.value.slice(0, 5))
const topPendingProjects = computed(() => filteredPendingProjects.value.slice(0, 5))
const topAdvisorProjects = computed(() => advisorProjects.value.slice(0, 5))
const topCompanies = computed(() => companiesList.value.slice(0, 5))

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
    return ['pending', 'under_review', 'proposed', 'pendiente', 'en_revision', 'en revision'].includes(s)
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
    tasks.push({ text: 'Registrar tu solicitud de anteproyecto', href: '/projects/proposal', tag: 'Requerido' })
    return tasks
  }

  const s = (latestStudentProject.value.status || '').toLowerCase()
  if (s === 'completed') {
    tasks.push({ text: 'Consultar calificaciones oficiales finales', href: '/evaluations/grading', tag: 'Acreditado' })
    tasks.push({ text: 'Descargar constancias del expediente digital', href: '/documents', tag: 'Expediente' })
    tasks.push({ text: 'Consultar cronograma histórico concluido', href: '/activities/schedule', tag: 'Histórico' })
    return tasks
  }

  if (s === 'pending' || s === 'under_review' || s === 'proposed' || s === 'pendiente') {
    tasks.push({ text: 'Tu anteproyecto está en dictamen por la Academia', href: '/projects/proposal', tag: 'En Proceso' })
    return tasks
  }

  if (s === 'draft') {
    tasks.push({ text: 'Completar y enviar solicitud de anteproyecto a revisión', href: '/projects/proposal', tag: 'Pendiente' })
    return tasks
  }

  if (s === 'rejected') {
    tasks.push({ text: 'Revisar y corregir observaciones de tu anteproyecto', href: '/projects/proposal', tag: 'Urgente' })
    return tasks
  }

  const docsByType = {}
  studentDocs.value.forEach((d) => {
    docsByType[d.documentType] = d
  })

  if (!docsByType['solicitud']) tasks.push({ text: 'Subir Solicitud de Residencia Profesional firmada', href: '/documents', tag: 'Documento' })
  if (!docsByType['carta_aceptacion']) tasks.push({ text: 'Subir Carta de Aceptación de la empresa receptora', href: '/documents', tag: 'Documento' })
  if (completedWeeksCount.value === 0) tasks.push({ text: 'Registrar primer avance en tu cronograma de 26 semanas', href: '/activities/schedule', tag: 'Actividades' })

  return tasks
})

function formatTecNMDate(iso) {
  if (!iso) return '—'
  const d = new Date(iso)
  if (isNaN(d.getTime())) return '—'
  const MONTHS = [
    'Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun',
    'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic',
  ]
  return `${String(d.getDate()).padStart(2, '0')} ${MONTHS[d.getMonth()]} ${d.getFullYear()}`
}

const STATUS_LABELS = {
  draft: 'Borrador',
  pending: 'En Dictamen',
  under_review: 'En Dictamen',
  proposed: 'En Dictamen',
  approved: 'Aprobado',
  in_progress: 'En Progreso',
  completed: 'Completado',
  rejected: 'Correcciones',
  cancelled: 'Cancelado',
}

function getProjectStatusSpanish(status) {
  if (!status) return 'Sin Registro'
  const s = String(status).toLowerCase().trim()
  return STATUS_LABELS[s] || s.charAt(0).toUpperCase() + s.slice(1)
}

function getStudentStatusVariant(status) {
  if (!status) return 'navy'
  const s = String(status).toLowerCase().trim()
  if (['completed', 'completado'].includes(s)) return 'emerald'
  if (['approved', 'aprobado', 'in_progress', 'en_progreso'].includes(s)) return 'emerald'
  if (['pending', 'under_review', 'proposed', 'pendiente'].includes(s)) return 'gold'
  if (['rejected', 'rechazado', 'correcciones'].includes(s)) return 'warning'
  return 'navy'
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

async function loadStaffMetrics() {
  try {
    const params = {}
    if (selectedCareerFilter.value !== 'all') {
      params.careerId = selectedCareerFilter.value
    } else if (authStore.isCareerHead && authStore.userCareerId) {
      params.careerId = authStore.userCareerId
    }
    const res = await apiClient.get('/v1/admin/dashboard', { params })
    adminMetrics.value = res.data || {}
  } catch (err) {
    console.error('Error al cargar métricas:', err)
  }
}

async function loadDashboard() {
  isLoading.value = true
  const role = authStore.currentRole

  try {
    if (isStaff.value) {
      const promises = [
        loadStaffMetrics(),
        apiClient.get('/v1/projects', { params: { pageNumber: 1, pageSize: 25 } }).catch(() => ({ data: { items: [] } })),
        apiClient.get('/v1/projects', { params: { status: 'pending', pageNumber: 1, pageSize: 25 } }).catch(() => ({ data: { items: [] } })),
      ]

      if (role === 'vinculacion' || role === 'admin') {
        promises.push(apiClient.get('/v1/companies', { params: { pageNumber: 1, pageSize: 10 } }).catch(() => ({ data: { items: [] } })))
      }

      if (role === 'departmenthead' || role === 'academic') {
        promises.push(apiClient.get('/v1/advisors', { params: { pageNumber: 1, pageSize: 15 } }).catch(() => ({ data: { items: [] } })))
      }

      if (authStore.isCareerHead) {
        promises.push(apiClient.get('/v1/students', { params: { pageSize: 100 } }).catch(() => ({ data: { items: [] } })))
        promises.push(apiClient.get('/v1/advisors', { params: { pageSize: 100 } }).catch(() => ({ data: { items: [] } })))
      }

      const results = await Promise.all(promises)
      const rRes = results[1]
      const pRes = results[2]

      recentProjects.value = (rRes.data?.items || []).filter((p) => p.isActive !== false)
      pendingProjects.value = (pRes.data?.items || []).filter((p) => p.isActive !== false)

      if (authStore.isCareerHead) {
        const stuRes = results[3]
        const advRes = results[4]
        careerStudents.value = stuRes?.data?.items || []
        careerAdvisors.value = advRes?.data?.items || []
        recentProjects.value = recentProjects.value.filter((p) => String(p.status || '').toLowerCase() !== 'draft')
        pendingProjects.value = pendingProjects.value.filter((p) => String(p.status || '').toLowerCase() !== 'draft')
      } else if (results[3]) {
        if (role === 'vinculacion' || role === 'admin') {
          companiesList.value = results[3].data?.items || []
        } else if (role === 'departmenthead' || role === 'academic') {
          advisorsList.value = results[3].data?.items || []
        }
      }
    } else if (role === 'student') {
      const [sRes, pRes] = await Promise.all([
        apiClient.get('/v1/students/me').catch(() => ({ data: null })),
        apiClient.get('/v1/projects/me', { params: { pageNumber: 1, pageSize: 10 } }).catch(() => ({ data: { items: [] } })),
      ])
      studentProfile.value = sRes.data
      studentProjects.value = pRes.data?.items || []

      if (latestStudentProject.value) {
        const [dRes, aRes] = await Promise.all([
          apiClient.get(`/v1/documents/project/${latestStudentProject.value.id}`, { params: { pageNumber: 1, pageSize: 50 } }).catch(() => ({ data: { items: [] } })),
          apiClient.get(`/v1/projects/${latestStudentProject.value.id}/activities`).catch(() => ({ data: [] })),
        ])
        studentDocs.value = dRes.data?.items || []

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

// Recargar métricas al cambiar el filtro de carrera
watch(selectedCareerFilter, () => {
  if (isStaff.value) {
    loadStaffMetrics()
  }
})

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

      <!-- Filtro moderno de carrera para Staff y Jefes de División -->
      <div v-if="isStaff && !authStore.isCareerHead" class="tecnm-filter-bar">
        <div class="tecnm-filter-container">
          <svg xmlns="http://www.w3.org/2000/svg" class="tecnm-filter-icon" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 3c2.755 0 5.455.232 8.083.678.533.09.917.556.917 1.096v1.044a2.25 2.25 0 0 1-.659 1.591l-5.432 5.432a2.25 2.25 0 0 0-.659 1.591v2.927a2.25 2.25 0 0 1-1.244 2.013L9.75 21v-6.568a2.25 2.25 0 0 0-.659-1.591L3.659 7.409A2.25 2.25 0 0 1 3 5.818V4.774c0-.54.384-1.006.917-1.096A48.32 48.32 0 0 1 12 3Z" />
          </svg>
          <select
            id="dashCareerFilter"
            v-model="selectedCareerFilter"
            class="tecnm-filter-select"
            aria-label="Filtrar por Carrera"
          >
            <option value="all">Todas las Carreras</option>
            <option value="4">Ing. en Sistemas Computacionales</option>
            <option value="1">Ing. Informática</option>
            <option value="3">Ing. Mecatrónica</option>
            <option value="2">Ing. Industrial</option>
          </select>
        </div>
      </div>
    </div>

    <!-- ======================================================== -->
    <!-- 1. KPIS ESTANDARIZADOS POR ROL -->
    <!-- ======================================================== -->

    <!-- A. KPIs PARA ADMINISTRADOR -->
    <div
      v-if="authStore.currentRole === 'admin'"
      id="statsSection"
      class="tecnm-kpis-grid tecnm-kpis-grid--6"
    >
      <TecnmKpiCard
        title="Estudiantes Registrados"
        :value="adminMetrics.totalStudents"
        variant="navy"
        :loading="isLoading"
        to="/students"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Zm8.25 2.25a2.625 2.625 0 1 1-5.25 0 2.625 2.625 0 0 1 5.25 0Z" />
          </svg>
        </template>
      </TecnmKpiCard>

      <TecnmKpiCard
        title="Asesores Activos"
        :value="adminMetrics.activeAdvisors"
        variant="slate"
        :loading="isLoading"
        to="/advisors"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M4.26 10.147a60.438 60.438 0 0 0-.491 6.347A48.62 48.62 0 0 1 12 20.904a48.62 48.62 0 0 1 8.232-4.41 60.46 60.46 0 0 0-.491-6.347m-15.482 0a50.636 50.636 0 0 0-2.658-.813A59.906 59.906 0 0 1 12 3.493a59.903 59.903 0 0 1 10.399 5.84c-.896.248-1.783.52-2.658.814m-15.482 0A50.717 50.717 0 0 1 12 13.489a50.702 50.702 0 0 1 7.74-3.342M6.75 15a.75.75 0 1 0 0-1.5.75.75 0 0 0 0 1.5Zm0 0v-3.675A55.378 55.378 0 0 1 12 8.443m-7.007 11.55A5.981 5.981 0 0 0 6.75 15.75v-1.5" />
          </svg>
        </template>
      </TecnmKpiCard>

      <TecnmKpiCard
        title="Empresas Receptoras"
        :value="adminMetrics.activeCompanies ?? 0"
        variant="indigo"
        :loading="isLoading"
        to="/companies"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M3.75 21h16.5M4.5 3h15M5.25 3v18m13.5-18v18M9 6.75h1.5m-1.5 3h1.5m-1.5 3h1.5m3-6H15m-1.5 3H15m-1.5 3H15M9 21v-3.375c0-.621.504-1.125 1.125-1.125h3.75c.621 0 1.125.504 1.125 1.125V21" />
          </svg>
        </template>
      </TecnmKpiCard>

      <TecnmKpiCard
        title="Proyectos Totales"
        :value="adminMetrics.totalProjects"
        variant="navy"
        :loading="isLoading"
        to="/projects"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z" />
          </svg>
        </template>
      </TecnmKpiCard>

      <TecnmKpiCard
        title="Por Dictaminar"
        :value="adminMetrics.pendingProjects"
        variant="gold"
        :loading="isLoading"
        to="/projects"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
          </svg>
        </template>
      </TecnmKpiCard>

      <TecnmKpiCard
        title="Residencias Concluidas"
        :value="adminMetrics.completedResidencies"
        variant="emerald"
        :loading="isLoading"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
          </svg>
        </template>
      </TecnmKpiCard>
    </div>

    <!-- B. KPIs PARA JEFE DE DIVISIÓN ACADÉMICA / ACADÉMICO -->
    <div
      v-else-if="authStore.hasRole('departmenthead', 'academic')"
      class="tecnm-kpis-grid tecnm-kpis-grid--4"
    >
      <TecnmKpiCard
        title="Pendientes de Dictamen"
        :value="adminMetrics.pendingProjects ?? 0"
        variant="gold"
        :loading="isLoading"
        to="/projects"
        subtext="Requieren resolución de Academia"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m9-.75a9 9 0 1 1-18 0 9 9 0 0 1 18 0Zm-9 3.75h.008v.008H12v-.008Z" />
          </svg>
        </template>
      </TecnmKpiCard>

      <TecnmKpiCard
        title="Residentes Activos"
        :value="adminMetrics.approvedProjects ?? 0"
        variant="navy"
        :loading="isLoading"
        to="/projects"
        subtext="En proceso de residencia"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Z" />
          </svg>
        </template>
      </TecnmKpiCard>

      <TecnmKpiCard
        title="Asesores de la División"
        :value="adminMetrics.activeAdvisors ?? 0"
        variant="slate"
        :loading="isLoading"
        to="/advisors"
        subtext="Docentes asignables"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M4.26 10.147a60.438 60.438 0 0 0-.491 6.347A48.62 48.62 0 0 1 12 20.904a48.62 48.62 0 0 1 8.232-4.41 60.46 60.46 0 0 0-.491-6.347m-15.482 0a50.636 50.636 0 0 0-2.658-.813A59.906 59.906 0 0 1 12 3.493a59.903 59.903 0 0 1 10.399 5.84c-.896.248-1.783.52-2.658.814m-15.482 0A50.717 50.717 0 0 1 12 13.489a50.702 50.702 0 0 1 7.74-3.342M6.75 15a.75.75 0 1 0 0-1.5.75.75 0 0 0 0 1.5Zm0 0v-3.675A55.378 55.378 0 0 1 12 8.443m-7.007 11.55A5.981 5.981 0 0 0 6.75 15.75v-1.5" />
          </svg>
        </template>
      </TecnmKpiCard>

      <TecnmKpiCard
        title="Residencias Acreditadas"
        :value="adminMetrics.completedResidencies ?? 0"
        variant="emerald"
        :loading="isLoading"
        subtext="Concluidas con éxito"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
          </svg>
        </template>
      </TecnmKpiCard>
    </div>

    <!-- B.1 KPIs PARA JEFE DE CARRERA -->
    <div
      v-else-if="authStore.isCareerHead"
      class="tecnm-kpis-grid tecnm-kpis-grid--4"
    >
      <TecnmKpiCard
        title="Residentes Registrados"
        :value="adminMetrics.totalStudents ?? careerStudents.length ?? 0"
        variant="navy"
        :loading="isLoading"
        to="/students"
        subtext="Alumnos de tu carrera"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Zm8.25 2.25a2.625 2.625 0 1 1-5.25 0 2.625 2.625 0 0 1 5.25 0Z" />
          </svg>
        </template>
      </TecnmKpiCard>

      <TecnmKpiCard
        title="Asesores de la Carrera"
        :value="adminMetrics.activeAdvisors ?? careerAdvisors.length ?? 0"
        variant="slate"
        :loading="isLoading"
        to="/advisors"
        subtext="Docentes adscritos"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M4.26 10.147a60.438 60.438 0 0 0-.491 6.347A48.62 48.62 0 0 1 12 20.904a48.62 48.62 0 0 1 8.232-4.41 60.46 60.46 0 0 0-.491-6.347m-15.482 0a50.636 50.636 0 0 0-2.658-.813A59.906 59.906 0 0 1 12 3.493a59.903 59.903 0 0 1 10.399 5.84c-.896.248-1.783.52-2.658.814m-15.482 0A50.717 50.717 0 0 1 12 13.489a50.702 50.702 0 0 1 7.74-3.342M6.75 15a.75.75 0 1 0 0-1.5.75.75 0 0 0 0 1.5Zm0 0v-3.675A55.378 55.378 0 0 1 12 8.443m-7.007 11.55A5.981 5.981 0 0 0 6.75 15.75v-1.5" />
          </svg>
        </template>
      </TecnmKpiCard>

      <TecnmKpiCard
        title="Alumnos Asignados"
        :value="adminMetrics.studentsWithAdvisor ?? assignedStudentsCount ?? 0"
        variant="gold"
        :loading="isLoading"
        to="/advisors/assignments"
        :subtext="`${adminMetrics.studentsWithoutAdvisor ?? unassignedStudents.length ?? 0} pendientes por asignar`"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M19 7.5v3m0 0v3m0-3h3m-3 0h-3m-2.25-4.125a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0ZM4 19.235v-.11a6.375 6.375 0 0 1 12.75 0v.109A12.318 12.318 0 0 1 10.374 21c-2.331 0-4.512-.645-6.374-1.766Z" />
          </svg>
        </template>
      </TecnmKpiCard>

      <TecnmKpiCard
        title="Anteproyectos Activos"
        :value="adminMetrics.totalProjects ?? careerHeadActiveProjects.length ?? 0"
        variant="emerald"
        :loading="isLoading"
        to="/projects/review"
        :subtext="`${adminMetrics.pendingProjects ?? 0} en dictamen`"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
          </svg>
        </template>
      </TecnmKpiCard>
    </div>

    <!-- C. KPIs PARA VINCULACIÓN -->
    <div
      v-else-if="authStore.currentRole === 'vinculacion'"
      class="tecnm-kpis-grid tecnm-kpis-grid--4"
    >
      <TecnmKpiCard
        title="Empresas Receptoras"
        :value="adminMetrics.activeCompanies ?? 0"
        variant="navy"
        :loading="isLoading"
        to="/companies"
        subtext="Convenios y sedes activas"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M3.75 21h16.5M4.5 3h15M5.25 3v18m13.5-18v18M9 6.75h1.5m-1.5 3h1.5m-1.5 3h1.5m3-6H15m-1.5 3H15m-1.5 3H15M9 21v-3.375c0-.621.504-1.125 1.125-1.125h3.75c.621 0 1.125.504 1.125 1.125V21" />
          </svg>
        </template>
      </TecnmKpiCard>

      <TecnmKpiCard
        title="Proyectos Dictaminados"
        :value="adminMetrics.approvedProjects ?? 0"
        variant="emerald"
        :loading="isLoading"
        to="/projects"
        subtext="Listos para carta de presentación"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
          </svg>
        </template>
      </TecnmKpiCard>

      <TecnmKpiCard
        title="Total Residentes"
        :value="adminMetrics.totalStudents ?? 0"
        variant="slate"
        :loading="isLoading"
        to="/students"
        subtext="En vinculación institucional"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Z" />
          </svg>
        </template>
      </TecnmKpiCard>

      <TecnmKpiCard
        title="Residencias Liberadas"
        :value="adminMetrics.completedResidencies ?? 0"
        variant="indigo"
        :loading="isLoading"
        subtext="Con carta de liberación"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z" />
          </svg>
        </template>
      </TecnmKpiCard>
    </div>

    <!-- D. KPIs PARA DIRECTOR -->
    <div
      v-else-if="authStore.currentRole === 'director'"
      class="tecnm-kpis-grid tecnm-kpis-grid--4"
    >
      <TecnmKpiCard
        title="Total Residentes"
        :value="adminMetrics.totalStudents ?? 0"
        variant="navy"
        :loading="isLoading"
        subtext="Matrícula en residencia"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Z" />
          </svg>
        </template>
      </TecnmKpiCard>

      <TecnmKpiCard
        title="Eficiencia Terminal"
        :value="adminMetrics.totalProjects > 0 ? `${Math.round(((adminMetrics.completedResidencies || 0) / adminMetrics.totalProjects) * 100)}%` : '—'"
        variant="emerald"
        :loading="isLoading"
        subtext="Tasa de acreditación"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M2.25 18 9 11.25l4.306 4.306a11.95 11.95 0 0 1 5.814-5.518l2.74-1.22m0 0-5.94-2.281m5.94 2.28-2.28 5.941" />
          </svg>
        </template>
      </TecnmKpiCard>

      <TecnmKpiCard
        title="Empresas Vinculadas"
        :value="adminMetrics.activeCompanies ?? 0"
        variant="indigo"
        :loading="isLoading"
        subtext="Sectores productivos"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M3.75 21h16.5M4.5 3h15M5.25 3v18m13.5-18v18M9 6.75h1.5m-1.5 3h1.5m-1.5 3h1.5m3-6H15m-1.5 3H15m-1.5 3H15M9 21v-3.375c0-.621.504-1.125 1.125-1.125h3.75c.621 0 1.125.504 1.125 1.125V21" />
          </svg>
        </template>
      </TecnmKpiCard>

      <TecnmKpiCard
        title="Cuerpo Docente Asesor"
        :value="adminMetrics.activeAdvisors ?? 0"
        variant="slate"
        :loading="isLoading"
        subtext="Asesores internos activos"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M4.26 10.147a60.438 60.438 0 0 0-.491 6.347A48.62 48.62 0 0 1 12 20.904a48.62 48.62 0 0 1 8.232-4.41 60.46 60.46 0 0 0-.491-6.347m-15.482 0a50.636 50.636 0 0 0-2.658-.813A59.906 59.906 0 0 1 12 3.493a59.903 59.903 0 0 1 10.399 5.84c-.896.248-1.783.52-2.658.814m-15.482 0A50.717 50.717 0 0 1 12 13.489a50.702 50.702 0 0 1 7.74-3.342M6.75 15a.75.75 0 1 0 0-1.5.75.75 0 0 0 0 1.5Zm0 0v-3.675A55.378 55.378 0 0 1 12 8.443m-7.007 11.55A5.981 5.981 0 0 0 6.75 15.75v-1.5" />
          </svg>
        </template>
      </TecnmKpiCard>
    </div>

    <!-- E. KPIs PARA ASESOR ACADÉMICO -->
    <div
      v-else-if="authStore.currentRole === 'advisor'"
      id="advisorStatsSection"
      class="tecnm-kpis-grid tecnm-kpis-grid--4"
    >
      <TecnmKpiCard
        title="Residentes Asignados"
        :value="advisorTotalResidents"
        variant="navy"
        :loading="isLoading"
        subtext="Bajo tu asesoría técnica"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Z" />
          </svg>
        </template>
      </TecnmKpiCard>

      <TecnmKpiCard
        title="Residencias Activas"
        :value="advisorActiveResidents"
        variant="slate"
        :loading="isLoading"
        subtext="En desarrollo de 26 semanas"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 0 1 2.25-2.25h13.5A2.25 2.25 0 0 1 21 7.5v11.25m-18 0A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75m-18 0v-7.5A2.25 2.25 0 0 1 5.25 9h13.5A2.25 2.25 0 0 1 21 11.25v7.5" />
          </svg>
        </template>
      </TecnmKpiCard>

      <TecnmKpiCard
        title="En Dictamen / Revisión"
        :value="advisorPendingDictamen"
        variant="gold"
        :loading="isLoading"
        subtext="Anteproyectos por revisar"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
          </svg>
        </template>
      </TecnmKpiCard>

      <TecnmKpiCard
        title="Residencias Concluidas"
        :value="advisorCompletedResidents"
        variant="emerald"
        :loading="isLoading"
        subtext="Evaluadas y acreditadas"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
          </svg>
        </template>
      </TecnmKpiCard>
    </div>

    <!-- F. KPIs PARA ESTUDIANTE -->
    <div
      v-else-if="authStore.currentRole === 'student'"
      id="statsSection"
      class="tecnm-kpis-grid tecnm-kpis-grid--4"
    >
      <TecnmKpiCard
        title="Estado Anteproyecto"
        :value="latestStudentProject ? getProjectStatusSpanish(latestStudentProject.status) : 'Sin Registro'"
        :variant="getStudentStatusVariant(latestStudentProject?.status)"
        :loading="isLoading"
        to="/projects/proposal"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z" />
          </svg>
        </template>
        <template #subtext>
          <span v-if="latestStudentProject">{{ latestStudentProject.title }}</span>
          <span v-else>Inicia tu propuesta</span>
        </template>
      </TecnmKpiCard>

      <TecnmKpiCard
        title="Semanas Completadas"
        :value="`${completedWeeksCount} / ${TOTAL_WEEKS}`"
        variant="slate"
        :loading="isLoading"
        to="/activities/schedule"
        :subtext="`${studentProgressPercent}% del periodo cumplido`"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 0 1 2.25-2.25h13.5A2.25 2.25 0 0 1 21 7.5v11.25m-18 0A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75m-18 0v-7.5A2.25 2.25 0 0 1 5.25 9h13.5A2.25 2.25 0 0 1 21 11.25v7.5" />
          </svg>
        </template>
      </TecnmKpiCard>

      <TecnmKpiCard
        title="Documentos Aprobados"
        :value="approvedDocsCount"
        variant="emerald"
        :loading="isLoading"
        to="/documents"
        subtext="Del expediente digital"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M2.25 12.75V12A2.25 2.25 0 0 1 4.5 9.75h15A2.25 2.25 0 0 1 21.75 12v.75m-8.69-6.44-2.12-2.12a1.5 1.5 0 0 0-1.061-.44H4.5A2.25 2.25 0 0 0 2.25 6v12a2.25 2.25 0 0 0 2.25 2.25h15A2.25 2.25 0 0 0 21.75 18V9a2.25 2.25 0 0 0-2.25-2.25h-5.379a1.5 1.5 0 0 1-1.06-.44Z" />
          </svg>
        </template>
      </TecnmKpiCard>

      <TecnmKpiCard
        title="Promedio General"
        :value="studentProfile?.gpa != null ? Number(studentProfile.gpa).toFixed(1) : '—'"
        variant="gold"
        :loading="isLoading"
        subtext="Calificación acumulada"
      >
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M11.48 3.499a.562.562 0 0 1 1.04 0l2.125 5.111a.563.563 0 0 0 .475.345l5.518.442c.499.04.701.663.321.988l-4.204 3.602a.563.563 0 0 0-.182.557l1.285 5.385a.562.562 0 0 1-.84.61l-4.725-2.885a.562.562 0 0 0-.586 0L6.982 20.54a.562.562 0 0 1-.84-.61l1.285-5.386a.562.562 0 0 0-.182-.557l-4.204-3.602a.562.562 0 0 1 .321-.988l5.518-.442a.563.563 0 0 0 .475-.345L11.48 3.5Z" />
          </svg>
        </template>
      </TecnmKpiCard>
    </div>

    <!-- ======================================================== -->
    <!-- 2. DASHBOARD GRID PRINCIPAL Y LATERAL SEGÚN ROL -->
    <!-- ======================================================== -->
    <div class="dashboard-grid">
      <!-- Columna Principal -->
      <div class="dashboard-main">
        <!-- ======================================================== -->
        <!-- VISTA ADMIN: Tablas de Anteproyectos y Pendientes -->
        <!-- ======================================================== -->
        <template v-if="authStore.currentRole === 'admin'">
          <!-- Tabla: Anteproyectos Recientes (Top 5) -->
          <div id="contentCard" class="tecnm-card">
            <div class="tecnm-card-header">
              <div class="tecnm-d-flex tecnm-align-center tecnm-gap-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="tecnm-header-icon" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z" />
                </svg>
                <h3 class="tecnm-card-title">
                  Anteproyectos Recientes
                  <span v-if="selectedCareerFilter !== 'all'" class="tecnm-card-subtitle-tag">
                    ({{ CAREERS[selectedCareerFilter] }})
                  </span>
                </h3>
              </div>
              <router-link to="/projects" class="tecnm-link-action">
                Ver todos ({{ filteredRecentProjects.length }}) &rarr;
              </router-link>
            </div>
            <div class="tecnm-card-body tecnm-p-0">
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
                    <tr v-else-if="topRecentProjects.length === 0">
                      <td colspan="4" class="tecnm-table-empty">No hay anteproyectos registrados para la selección.</td>
                    </tr>
                    <tr v-for="p in topRecentProjects" v-else :key="p.id">
                      <td>
                        <router-link :to="`/projects`" class="tecnm-table-link">
                          {{ p.title }}
                        </router-link>
                        <div v-if="p.companyName" class="tecnm-text-sub">
                          {{ p.companyName }}
                        </div>
                      </td>
                      <td>
                        <strong>{{ p.studentName || 'Estudiante' }}</strong>
                        <div class="tecnm-text-sub">
                          {{ p.careerName || (CAREERS[p.careerId] || '') }}
                        </div>
                      </td>
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

          <!-- Tabla: Por Dictaminar (Top 5) -->
          <div v-if="filteredPendingProjects.length > 0" class="tecnm-card" style="margin-top: 1.5rem;">
            <div class="tecnm-card-header">
              <div class="tecnm-d-flex tecnm-align-center tecnm-gap-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="tecnm-header-icon tecnm-header-icon--gold" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
                </svg>
                <h3 class="tecnm-card-title">Anteproyectos Pendientes de Dictamen</h3>
              </div>
              <router-link to="/projects" class="tecnm-link-action">
                Ver todos ({{ filteredPendingProjects.length }}) &rarr;
              </router-link>
            </div>
            <div class="tecnm-card-body tecnm-p-0">
              <div class="tecnm-table-responsive">
                <table class="tecnm-table">
                  <thead>
                    <tr>
                      <th>Título y Empresa</th>
                      <th>Estudiante</th>
                      <th>Fecha Registro</th>
                      <th style="text-align: right;">Acción</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-for="p in topPendingProjects" :key="p.id">
                      <td>
                        <strong>{{ p.title }}</strong>
                        <div class="tecnm-text-sub">{{ p.companyName || 'Empresa Receptora' }}</div>
                      </td>
                      <td>{{ p.studentName || 'Estudiante' }}</td>
                      <td>{{ formatTecNMDate(p.createdAt) }}</td>
                      <td style="text-align: right;">
                        <router-link to="/projects" class="tecnm-btn tecnm-btn-primary tecnm-btn-sm">
                          Revisar
                        </router-link>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        </template>

        <!-- ======================================================== -->
        <!-- VISTA JEFE DE CARRERA: SUPERVISIÓN Y CONTROL ACADÉMICO   -->
        <!-- ======================================================== -->
        <template v-else-if="authStore.isCareerHead">
          <!-- Widget 1: Monitor de Cobertura de Asignación de Asesores -->
          <div class="tecnm-card" style="margin-bottom: 1.5rem;">
            <div class="tecnm-card-header">
              <div class="tecnm-d-flex tecnm-align-center tecnm-gap-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="tecnm-header-icon" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M19 7.5v3m0 0v3m0-3h3m-3 0h-3m-2.25-4.125a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0ZM4 19.235v-.11a6.375 6.375 0 0 1 12.75 0v.109A12.318 12.318 0 0 1 10.374 21c-2.331 0-4.512-.645-6.374-1.766Z" />
                </svg>
                <h3 class="tecnm-card-title">Monitor de Cobertura de Asesorías Académicas</h3>
              </div>
              <router-link to="/advisors/assignments" class="tecnm-link-action">
                Asignar Asesores &rarr;
              </router-link>
            </div>
            <div class="tecnm-card-body">
              <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.75rem; flex-wrap: wrap; gap: 0.5rem;">
                <span style="font-size: 0.9rem; font-weight: 600; color: var(--tecnm-text-primary, #0f172a);">
                  Avance de Asignaciones: {{ assignedStudentsCount }} de {{ careerStudents.length }} Alumnos ({{ assignmentCoveragePercent }}%)
                </span>
                <span :class="unassignedStudents.length === 0 ? 'tecnm-badge tecnm-badge-success' : 'tecnm-badge tecnm-badge-warning'">
                  {{ unassignedStudents.length === 0 ? '100% Asignados' : `${unassignedStudents.length} Pendiente(s)` }}
                </span>
              </div>
              <!-- Barra de progreso visual -->
              <div style="width: 100%; height: 10px; background-color: #e2e8f0; border-radius: 9999px; overflow: hidden; margin-bottom: 1rem;">
                <div
                  :style="{ width: `${assignmentCoveragePercent}%`, height: '100%', backgroundColor: assignmentCoveragePercent === 100 ? '#10b981' : '#f59e0b', transition: 'width 0.4s ease' }"
                ></div>
              </div>
              <div style="display: flex; gap: 1.5rem; font-size: 0.825rem; color: var(--tecnm-text-secondary, #64748b);">
                <div style="display: flex; align-items: center; gap: 0.35rem;">
                  <span style="width: 10px; height: 10px; border-radius: 50%; background-color: #10b981; display: inline-block;"></span>
                  <span>Asignados: <strong>{{ assignedStudentsCount }}</strong></span>
                </div>
                <div style="display: flex; align-items: center; gap: 0.35rem;">
                  <span style="width: 10px; height: 10px; border-radius: 50%; background-color: #f59e0b; display: inline-block;"></span>
                  <span>Por Asignar: <strong>{{ unassignedStudents.length }}</strong></span>
                </div>
              </div>
            </div>
          </div>

          <!-- Widget 2: Alumnos Pendientes de Asignar Asesor (Más recientes) -->
          <div v-if="unassignedStudents.length > 0" class="tecnm-card" style="margin-bottom: 1.5rem;">
            <div class="tecnm-card-header" style="display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 0.5rem;">
              <div class="tecnm-d-flex tecnm-align-center tecnm-gap-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="tecnm-header-icon tecnm-header-icon--gold" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m9-.75a9 9 0 1 1-18 0 9 9 0 0 1 18 0Zm-9 3.75h.008v.008H12v-.008Z" />
                </svg>
                <h3 class="tecnm-card-title">Residentes Pendientes de Asignación</h3>
                <span class="tecnm-badge tecnm-badge-warning" style="font-size: 0.75rem;">
                  {{ unassignedStudents.length }} por asignar
                </span>
              </div>
              <router-link to="/advisors/assignments" class="tecnm-link-action">
                Asignar Asesores &rarr;
              </router-link>
            </div>
            <div class="tecnm-card-body tecnm-p-0">
              <ul class="list-panel">
                <li
                  v-for="stu in recentUnassignedStudents"
                  :key="stu.id"
                  class="list-panel-item"
                  style="display: flex; justify-content: space-between; align-items: center; padding: 0.875rem 1.25rem; border-bottom: 1px solid var(--tecnm-border-color, #e2e8f0); gap: 1rem;"
                >
                  <div style="display: flex; align-items: center; gap: 0.875rem; min-width: 0;">
                    <div style="width: 36px; height: 36px; border-radius: 50%; background: #fef3c7; color: #b45309; display: flex; align-items: center; justify-content: center; font-weight: 700; font-size: 0.85rem; flex-shrink: 0;">
                      {{ (stu.fullName || stu.firstName || 'E').charAt(0).toUpperCase() }}
                    </div>
                    <div style="min-width: 0;">
                      <strong style="color: var(--tecnm-text-primary, #0f172a); font-size: 0.9rem; display: block; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;">
                        {{ stu.fullName || `${stu.firstName} ${stu.lastName}` }}
                      </strong>
                      <div class="tecnm-text-sub" style="font-size: 0.775rem;">
                        No. Control: <strong>{{ stu.controlNumber }}</strong>
                      </div>
                    </div>
                  </div>
                  <router-link to="/advisors/assignments" class="tecnm-btn tecnm-btn-primary tecnm-btn-sm" style="flex-shrink: 0;">
                    Asignar &rarr;
                  </router-link>
                </li>
              </ul>
              <div v-if="unassignedStudents.length > 3" style="padding: 0.625rem 1.25rem; background: var(--tecnm-gray-50, #f8fafc); border-top: 1px solid var(--tecnm-border-color, #e2e8f0); text-align: right;">
                <router-link to="/advisors/assignments" style="font-size: 0.8rem; font-weight: 600; color: var(--tecnm-blue-primary, #1B396A);">
                  Ver los {{ unassignedStudents.length - 3 }} pendientes restantes en Asignación &rarr;
                </router-link>
              </div>
            </div>
          </div>

          <!-- Widget 3: Supervisión de Anteproyectos de la Carrera (Más recientes) -->
          <div class="tecnm-card">
            <div class="tecnm-card-header" style="display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 0.5rem;">
              <div class="tecnm-d-flex tecnm-align-center tecnm-gap-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="tecnm-header-icon" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z" />
                </svg>
                <h3 class="tecnm-card-title">Supervisión de Anteproyectos Recientes</h3>
              </div>
              <router-link to="/projects/review" class="tecnm-link-action">
                Ver todos ({{ careerHeadActiveProjects.length }}) &rarr;
              </router-link>
            </div>
            <div class="tecnm-card-body tecnm-p-0">
              <div v-if="isLoading" style="padding: 2rem; text-align: center; color: var(--tecnm-text-muted);">
                Cargando anteproyectos...
              </div>
              <div v-else-if="careerHeadActiveProjects.length === 0" style="padding: 2rem; text-align: center; color: var(--tecnm-text-muted); font-size: 0.875rem;">
                No hay anteproyectos activos registrados actualmente para tu carrera.
              </div>
              <ul v-else class="list-panel">
                <li
                  v-for="p in recentActiveProjects"
                  :key="p.id"
                  class="list-panel-item"
                  style="display: flex; justify-content: space-between; align-items: center; padding: 0.875rem 1.25rem; border-bottom: 1px solid var(--tecnm-border-color, #e2e8f0); gap: 1rem;"
                >
                  <div style="flex: 1; min-width: 0;">
                    <div style="display: flex; align-items: center; gap: 0.5rem; margin-bottom: 0.25rem; flex-wrap: wrap;">
                      <strong style="color: var(--tecnm-text-primary, #0f172a); font-size: 0.9rem;">
                        {{ p.title }}
                      </strong>
                      <TecnmBadge :status="p.status" style="flex-shrink: 0;" />
                    </div>
                    <div class="tecnm-text-sub" style="font-size: 0.8rem; display: flex; gap: 0.875rem; flex-wrap: wrap;">
                      <span>Estudiante: <strong>{{ p.studentName || 'Estudiante' }}</strong> ({{ p.studentControlNumber }})</span>
                      <span v-if="p.companyName">• Empresa: {{ p.companyName }}</span>
                      <span>• Asesor: <strong>{{ p.advisorName || 'Por Asignar' }}</strong></span>
                    </div>
                  </div>
                  <router-link to="/projects/review" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" style="flex-shrink: 0;">
                    Ver Detalle
                  </router-link>
                </li>
              </ul>
            </div>
          </div>
        </template>

        <!-- ======================================================== -->
        <!-- VISTA JEFE DE DIVISIÓN: Bandeja de Dictamen Prioritaria -->
        <!-- ======================================================== -->
        <template v-else-if="authStore.hasRole('departmenthead', 'academic')">
          <div class="tecnm-card">
            <div class="tecnm-card-header">
              <div class="tecnm-d-flex tecnm-align-center tecnm-gap-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="tecnm-header-icon tecnm-header-icon--gold" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M9 12h3.75M9 15h3.75M9 18h3.75m3 .75H18a2.25 2.25 0 0 0 2.25-2.25V6.108c0-1.135-.845-2.098-1.976-2.192a48.424 48.424 0 0 0-1.123-.08m-5.801 0c-.065.21-.1.433-.1.664 0 .414.336.75.75.75h4.5a.75.75 0 0 0 .75-.75 2.25 2.25 0 0 0-.1-.664m-5.8 0A2.251 2.251 0 0 1 13.5 2.25H15c1.012 0 1.867.668 2.15 1.586m-5.8 0c-.376.023-.75.05-1.124.08C9.095 4.01 8.25 4.973 8.25 6.108V8.25m0 0H4.875c-.621 0-1.125.504-1.125 1.125v11.25c0 .621.504 1.125 1.125 1.125h9.75c.621 0 1.125-.504 1.125-1.125V9.375c0-.621-.504-1.125-1.125-1.125H8.25ZM6.75 12h.008v.008H6.75V12Zm0 3h.008v.008H6.75V15Zm0 3h.008v.008H6.75V18Z" />
                </svg>
                <h3 class="tecnm-card-title">Bandeja de Dictamen de Anteproyectos</h3>
              </div>
              <router-link to="/projects" class="tecnm-link-action">
                Ver todos los pendientes ({{ filteredPendingProjects.length }}) &rarr;
              </router-link>
            </div>
            <div class="tecnm-card-body tecnm-p-0">
              <div class="tecnm-table-responsive">
                <table class="tecnm-table">
                  <thead>
                    <tr>
                      <th>Título y Empresa</th>
                      <th>Estudiante</th>
                      <th>Fecha Envío</th>
                      <th>Estado</th>
                      <th style="text-align: right;">Acción</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-if="isLoading">
                      <td colspan="5" class="tecnm-table-empty">Cargando solicitudes de dictamen...</td>
                    </tr>
                    <tr v-else-if="topPendingProjects.length === 0">
                      <td colspan="5" class="tecnm-table-empty">
                        No hay anteproyectos pendientes de dictamen en este momento.
                      </td>
                    </tr>
                    <tr v-for="p in topPendingProjects" v-else :key="p.id">
                      <td>
                        <strong>{{ p.title }}</strong>
                        <div class="tecnm-text-sub">{{ p.companyName || 'Empresa Receptora' }}</div>
                      </td>
                      <td>
                        <strong>{{ p.studentName || 'Estudiante' }}</strong>
                        <div class="tecnm-text-sub">{{ p.studentControlNumber ? `Ctrl: ${p.studentControlNumber}` : '' }}</div>
                      </td>
                      <td>{{ formatTecNMDate(p.createdAt) }}</td>
                      <td>
                        <TecnmBadge :status="p.status" />
                      </td>
                      <td style="text-align: right;">
                        <router-link to="/projects" class="tecnm-btn tecnm-btn-primary tecnm-btn-sm">
                          Dictaminar
                        </router-link>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          </div>

          <!-- Anteproyectos Recientes de la División -->
          <div class="tecnm-card" style="margin-top: 1.5rem;">
            <div class="tecnm-card-header">
              <h3 class="tecnm-card-title">Anteproyectos en Seguimiento</h3>
              <router-link to="/projects" class="tecnm-link-action">
                Ver todos &rarr;
              </router-link>
            </div>
            <div class="tecnm-card-body tecnm-p-0">
              <div class="tecnm-table-responsive">
                <table class="tecnm-table">
                  <thead>
                    <tr>
                      <th>Título</th>
                      <th>Estudiante</th>
                      <th>Asesor Asignado</th>
                      <th>Estado</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-if="topRecentProjects.length === 0">
                      <td colspan="4" class="tecnm-table-empty">No hay registros recientes.</td>
                    </tr>
                    <tr v-for="p in topRecentProjects" v-else :key="p.id">
                      <td>{{ p.title }}</td>
                      <td>{{ p.studentName || 'Estudiante' }}</td>
                      <td>{{ p.advisorName || 'Sin asignar' }}</td>
                      <td><TecnmBadge :status="p.status" /></td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        </template>

        <!-- ======================================================== -->
        <!-- VISTA VINCULACIÓN: Empresas y Cartas de Presentación -->
        <!-- ======================================================== -->
        <template v-else-if="authStore.currentRole === 'vinculacion'">
          <div class="tecnm-card">
            <div class="tecnm-card-header">
              <div class="tecnm-d-flex tecnm-align-center tecnm-gap-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="tecnm-header-icon" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M3.75 21h16.5M4.5 3h15M5.25 3v18m13.5-18v18M9 6.75h1.5m-1.5 3h1.5m-1.5 3h1.5m3-6H15m-1.5 3H15m-1.5 3H15M9 21v-3.375c0-.621.504-1.125 1.125-1.125h3.75c.621 0 1.125.504 1.125 1.125V21" />
                </svg>
                <h3 class="tecnm-card-title">Empresas Receptoras Vinculadas</h3>
              </div>
              <router-link to="/companies" class="tecnm-link-action">
                Gestionar Empresas &rarr;
              </router-link>
            </div>
            <div class="tecnm-card-body tecnm-p-0">
              <div class="tecnm-table-responsive">
                <table class="tecnm-table">
                  <thead>
                    <tr>
                      <th>Razón Social</th>
                      <th>Sector / Giro</th>
                      <th>Contacto</th>
                      <th>Convenio</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-if="isLoading">
                      <td colspan="4" class="tecnm-table-empty">Cargando empresas receptoras...</td>
                    </tr>
                    <tr v-else-if="topCompanies.length === 0">
                      <td colspan="4" class="tecnm-table-empty">No hay empresas registradas actualmente.</td>
                    </tr>
                    <tr v-for="c in topCompanies" v-else :key="c.id">
                      <td>
                        <strong>{{ c.name }}</strong>
                        <div class="tecnm-text-sub">{{ c.city ? `${c.city}, ${c.state || ''}` : '' }}</div>
                      </td>
                      <td>{{ c.sector || 'General' }}</td>
                      <td>{{ c.contactEmail || c.contactPhone || '—' }}</td>
                      <td>
                        <span :class="c.hasAgreement ? 'tecnm-badge tecnm-badge-success' : 'tecnm-badge tecnm-badge-neutral'">
                          {{ c.hasAgreement ? 'Vigente' : 'En Trámite' }}
                        </span>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          </div>

          <!-- Anteproyectos Dictaminados para Carta -->
          <div class="tecnm-card" style="margin-top: 1.5rem;">
            <div class="tecnm-card-header">
              <h3 class="tecnm-card-title">Proyectos Listos para Carta de Presentación</h3>
              <router-link to="/projects" class="tecnm-link-action">
                Ver todos &rarr;
              </router-link>
            </div>
            <div class="tecnm-card-body tecnm-p-0">
              <div class="tecnm-table-responsive">
                <table class="tecnm-table">
                  <thead>
                    <tr>
                      <th>Estudiante</th>
                      <th>Proyecto</th>
                      <th>Empresa</th>
                      <th style="text-align: right;">Acción</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-if="topRecentProjects.length === 0">
                      <td colspan="4" class="tecnm-table-empty">No hay proyectos en esta categoría.</td>
                    </tr>
                    <tr v-for="p in topRecentProjects" v-else :key="p.id">
                      <td>
                        <strong>{{ p.studentName }}</strong>
                        <div class="tecnm-text-sub">{{ p.studentControlNumber }}</div>
                      </td>
                      <td>{{ p.title }}</td>
                      <td>{{ p.companyName || 'Empresa' }}</td>
                      <td style="text-align: right;">
                        <router-link to="/students" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm">
                          Expediente
                        </router-link>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        </template>

        <!-- ======================================================== -->
        <!-- VISTA DIRECTOR: Resumen Ejecutivo Institucional -->
        <!-- ======================================================== -->
        <template v-else-if="authStore.currentRole === 'director'">
          <div class="tecnm-card">
            <div class="tecnm-card-header">
              <div class="tecnm-d-flex tecnm-align-center tecnm-gap-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="tecnm-header-icon" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M3 13.125C3 12.504 3.504 12 4.125 12h2.25c.621 0 1.125.504 1.125 1.125v6.75C7.5 20.496 6.996 21 6.375 21h-2.25A1.125 1.125 0 0 1 3 19.875v-6.75ZM9.75 8.625c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125v11.25c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 0 1-1.125-1.125V8.625ZM16.5 4.125c0-.621.504-1.125 1.125-1.125h2.25C20.496 3 21 3.504 21 4.125v15.75c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 0 1-1.125-1.125V4.125Z" />
                </svg>
                <h3 class="tecnm-card-title">Distribución Institucional por Carrera</h3>
              </div>
            </div>
            <div class="tecnm-card-body tecnm-p-0">
              <div class="tecnm-table-responsive">
                <table class="tecnm-table">
                  <thead>
                    <tr>
                      <th>Programa Educativo</th>
                      <th>Residentes Activos</th>
                      <th>Concluidos</th>
                      <th>Estado</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr>
                      <td><strong>Ing. en Sistemas Computacionales</strong></td>
                      <td>{{ Math.round((adminMetrics.totalStudents || 0) * 0.35) }}</td>
                      <td>{{ Math.round((adminMetrics.completedResidencies || 0) * 0.4) }}</td>
                      <td><span class="tecnm-badge tecnm-badge-success">Operando</span></td>
                    </tr>
                    <tr>
                      <td><strong>Ing. Industrial</strong></td>
                      <td>{{ Math.round((adminMetrics.totalStudents || 0) * 0.30) }}</td>
                      <td>{{ Math.round((adminMetrics.completedResidencies || 0) * 0.3) }}</td>
                      <td><span class="tecnm-badge tecnm-badge-success">Operando</span></td>
                    </tr>
                    <tr>
                      <td><strong>Ing. Mecatrónica</strong></td>
                      <td>{{ Math.round((adminMetrics.totalStudents || 0) * 0.20) }}</td>
                      <td>{{ Math.round((adminMetrics.completedResidencies || 0) * 0.2) }}</td>
                      <td><span class="tecnm-badge tecnm-badge-success">Operando</span></td>
                    </tr>
                    <tr>
                      <td><strong>Ing. Informática</strong></td>
                      <td>{{ Math.round((adminMetrics.totalStudents || 0) * 0.15) }}</td>
                      <td>{{ Math.round((adminMetrics.completedResidencies || 0) * 0.1) }}</td>
                      <td><span class="tecnm-badge tecnm-badge-success">Operando</span></td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        </template>

        <!-- ======================================================== -->
        <!-- VISTA ASESOR: TABLA "MIS RESIDENTES A CARGO" (Top 5) -->
        <!-- ======================================================== -->
        <div
          v-else-if="authStore.currentRole === 'advisor'"
          class="tecnm-card"
        >
          <div class="tecnm-card-header">
            <div class="tecnm-d-flex tecnm-align-center tecnm-gap-2">
              <svg xmlns="http://www.w3.org/2000/svg" class="tecnm-header-icon" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" d="M18 18.72a9.094 9.094 0 0 0 3.741-.479 3 3 0 0 0-4.682-2.72m.94 3.198.001.031c0 .225-.012.447-.037.666A11.944 11.944 0 0 1 12 21c-2.17 0-4.207-.576-5.963-1.584A6.062 6.062 0 0 1 6 18.719m12 0a5.971 5.971 0 0 0-.941-3.197m0 0A5.995 5.995 0 0 0 12 12.75a5.995 5.995 0 0 0-5.058 2.772m0 0a3 3 0 0 0-4.681 2.72 8.986 8.986 0 0 0 3.74.477m.94-3.197a5.971 5.971 0 0 0-.94 3.197M15 6.75a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z" />
              </svg>
              <h3 class="tecnm-card-title">Mis Residentes Asignados</h3>
            </div>
            <router-link to="/evaluations" class="tecnm-link-action">
              Ver todos ({{ advisorProjects.length }}) &rarr;
            </router-link>
          </div>

          <div class="tecnm-card-body tecnm-p-0">
            <div class="tecnm-table-responsive">
              <table class="tecnm-table tecnm-table-striped">
                <thead>
                  <tr>
                    <th>Estudiante</th>
                    <th>Proyecto y Empresa</th>
                    <th>Avance Semanal</th>
                    <th>Estado</th>
                    <th style="text-align: right;">Acciones Directas</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-if="isLoading">
                    <td colspan="5" class="tecnm-table-empty">Cargando residentes a tu cargo...</td>
                  </tr>
                  <tr v-else-if="topAdvisorProjects.length === 0">
                    <td colspan="5" class="tecnm-table-empty">
                      Actualmente no tienes residentes asignados por la Jefatura de División.
                    </td>
                  </tr>
                  <tr v-for="p in topAdvisorProjects" v-else :key="p.id">
                    <td>
                      <strong>{{ p.studentName || 'Estudiante' }}</strong>
                      <div class="tecnm-text-sub">
                        {{ p.studentControlNumber ? `Ctrl: ${p.studentControlNumber}` : '' }}
                        {{ p.careerId ? `• ${CAREERS[p.careerId]}` : '' }}
                      </div>
                    </td>
                    <td>
                      <div class="tecnm-project-title-cell">
                        {{ p.title }}
                      </div>
                      <div class="tecnm-text-sub">
                        {{ p.companyName || 'Empresa Receptora' }}
                      </div>
                    </td>
                    <td style="min-width: 140px;">
                      <div class="tecnm-progress-header">
                        <span>{{ getProjectWeekProgress(p) }}/{{ TOTAL_WEEKS }} sem</span>
                        <span>{{ getProjectWeekPercent(p) }}%</span>
                      </div>
                      <div class="tecnm-progress-track">
                        <div class="tecnm-progress-fill" :style="{ width: `${getProjectWeekPercent(p)}%` }"></div>
                      </div>
                    </td>
                    <td>
                      <TecnmBadge :status="p.status" />
                    </td>
                    <td style="text-align: right;">
                      <div class="tecnm-row-actions">
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
        <!-- VISTA ESTUDIANTE: Stepper de Avance y Formatos -->
        <!-- ======================================================== -->
        <template v-else-if="authStore.currentRole === 'student'">
          <!-- Stepper de Avance del Anteproyecto -->
          <div v-if="projectStepInfo" class="tecnm-card">
            <div class="tecnm-card-header">
              <div class="tecnm-d-flex tecnm-align-center tecnm-gap-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="tecnm-header-icon" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
                </svg>
                <h3 class="tecnm-card-title">Trayectoria de tu Residencia Profesional</h3>
              </div>
              <router-link to="/projects/proposal" class="tecnm-link-action">
                Ver Propuesta &rarr;
              </router-link>
            </div>
            <div class="tecnm-card-body">
              <div class="tecnm-stepper">
                <div class="tecnm-step" :class="{ 'is-completed': projectStepInfo.step1.completed, 'is-active': projectStepInfo.step1.active }">
                  <div class="tecnm-step-circle">1</div>
                  <div class="tecnm-step-label">{{ projectStepInfo.step1.label }}</div>
                </div>
                <div class="tecnm-step-line" :class="{ 'is-completed': projectStepInfo.line1 }"></div>
                <div class="tecnm-step" :class="{ 'is-completed': projectStepInfo.step2.completed, 'is-active': projectStepInfo.step2.active, 'is-warning': projectStepInfo.step2.warning }">
                  <div class="tecnm-step-circle">2</div>
                  <div class="tecnm-step-label">{{ projectStepInfo.step2.label }}</div>
                </div>
                <div class="tecnm-step-line" :class="{ 'is-completed': projectStepInfo.line2 }"></div>
                <div class="tecnm-step" :class="{ 'is-completed': projectStepInfo.step3.completed, 'is-active': projectStepInfo.step3.active }">
                  <div class="tecnm-step-circle">3</div>
                  <div class="tecnm-step-label">{{ projectStepInfo.step3.label }}</div>
                </div>
                <div class="tecnm-step-line" :class="{ 'is-completed': projectStepInfo.line3 }"></div>
                <div class="tecnm-step" :class="{ 'is-completed': projectStepInfo.step4.completed, 'is-active': projectStepInfo.step4.active }">
                  <div class="tecnm-step-circle">4</div>
                  <div class="tecnm-step-label">{{ projectStepInfo.step4.label }}</div>
                </div>
              </div>
            </div>
          </div>

          <!-- Card de Formatos Oficiales TecNM para Descarga -->
          <div class="tecnm-card" style="margin-top: 1.5rem;">
            <div class="tecnm-card-header">
              <div class="tecnm-d-flex tecnm-align-center tecnm-gap-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="tecnm-header-icon" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z" />
                </svg>
                <h3 class="tecnm-card-title">Formatos Oficiales de Residencia Profesional</h3>
              </div>
              <router-link to="/documents" class="tecnm-link-action">
                Expediente Digital &rarr;
              </router-link>
            </div>

            <div class="tecnm-card-body tecnm-p-0">
              <div class="tecnm-format-list">
                <router-link to="/documents" class="tecnm-format-item">
                  <div class="tecnm-format-icon">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m2.25 0H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z" />
                    </svg>
                  </div>
                  <div class="tecnm-format-info">
                    <strong>Anexo XXIX - Carta de Aceptación</strong>
                    <div class="tecnm-text-sub">Formato oficial expedido por la empresa receptora</div>
                  </div>
                  <span class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm">Ver Módulo &rarr;</span>
                </router-link>

                <router-link to="/documents" class="tecnm-format-item">
                  <div class="tecnm-format-icon">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m2.25 0H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z" />
                    </svg>
                  </div>
                  <div class="tecnm-format-info">
                    <strong>Anexo XXX - Solicitud de Residencia</strong>
                    <div class="tecnm-text-sub">Registro de anteproyecto y datos institucionales</div>
                  </div>
                  <span class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm">Ver Módulo &rarr;</span>
                </router-link>

                <router-link to="/activities/schedule" class="tecnm-format-item">
                  <div class="tecnm-format-icon">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 0 1 2.25-2.25h13.5A2.25 2.25 0 0 1 21 7.5v11.25m-18 0A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75m-18 0v-7.5A2.25 2.25 0 0 1 5.25 9h13.5A2.25 2.25 0 0 1 21 11.25v7.5" />
                    </svg>
                  </div>
                  <div class="tecnm-format-info">
                    <strong>Cronograma de Actividades (26 Semanas)</strong>
                    <div class="tecnm-text-sub">Plan de trabajo y registro semanal de avance</div>
                  </div>
                  <span class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm">Ir al Cronograma &rarr;</span>
                </router-link>
              </div>
            </div>
          </div>
        </template>
      </div>

      <!-- ======================================================== -->
      <!-- Columna Lateral / Sidebar de Accesos y Tareas -->
      <!-- ======================================================== -->
      <div class="dashboard-sidebar">
        <!-- LATERAL PARA ESTUDIANTE -->
        <template v-if="authStore.currentRole === 'student'">
          <!-- Card Perfil del Residente -->
          <div v-if="studentProfile" class="tecnm-card">
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
              <div class="dashboard-student-info-item">
                <span class="dashboard-info-label">Asesor Asignado:</span>
                <strong class="dashboard-info-val">{{ latestStudentProject?.advisorName || 'Por asignar por academia' }}</strong>
              </div>
            </div>

            <div style="margin-top: 1rem; border-top: 1px solid var(--tecnm-border-color, #e2e8f0); padding-top: 0.875rem;">
              <router-link to="/students/profile" class="tecnm-btn tecnm-btn-outline tecnm-btn-sm" style="width: 100%; justify-content: center;">
                Ver Expediente Completo &rarr;
              </router-link>
            </div>
          </div>

          <!-- Card Tareas y Avisos -->
          <div class="tecnm-card" style="margin-top: 1.5rem;">
            <div class="tecnm-card-header">
              <div class="tecnm-d-flex tecnm-align-center tecnm-gap-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="tecnm-header-icon tecnm-header-icon--gold" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m9-.75a9 9 0 1 1-18 0 9 9 0 0 1 18 0Zm-9 3.75h.008v.008H12v-.008Z" />
                </svg>
                <h3 class="tecnm-card-title">Tareas y Avisos</h3>
              </div>
            </div>

            <ul class="list-panel">
              <li v-if="studentTasks.length === 0" class="list-panel-empty">
                Sin tareas pendientes. ¡Vas al corriente con tu residencia!
              </li>
              <li
                v-for="(task, idx) in studentTasks"
                v-else
                :key="idx"
                class="list-panel-item"
              >
                <router-link :to="task.href" class="list-panel-link">
                  <div class="tecnm-task-item">
                    <span>{{ task.text }}</span>
                    <span v-if="task.tag" class="tecnm-badge tecnm-badge-neutral">{{ task.tag }}</span>
                  </div>
                </router-link>
              </li>
            </ul>
          </div>
        </template>

        <!-- LATERAL PARA ASESOR -->
        <template v-else-if="authStore.currentRole === 'advisor'">
          <div class="tecnm-card">
            <div class="tecnm-card-header">
              <h3 class="tecnm-card-title">Acciones del Asesor</h3>
            </div>
            <div class="tecnm-card-body" style="display: flex; flex-direction: column; gap: 0.75rem;">
              <router-link to="/evaluations" class="tecnm-btn tecnm-btn-secondary" style="justify-content: flex-start; gap: 0.5rem;">
                <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
                </svg>
                Registrar Sesión de Bitácora
              </router-link>

              <router-link to="/evaluations/grading" class="tecnm-btn tecnm-btn-primary" style="justify-content: flex-start; gap: 0.5rem;">
                <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
                </svg>
                Calificar Evaluaciones Parciales
              </router-link>

              <router-link to="/documents" class="tecnm-btn tecnm-btn-outline" style="justify-content: flex-start; gap: 0.5rem;">
                <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25" />
                </svg>
                Formatos de Evaluación
              </router-link>
            </div>
          </div>
        </template>

        <!-- LATERAL PARA JEFE DE CARRERA -->
        <template v-else-if="authStore.isCareerHead">
          <!-- Card de Accesos Operativos -->
          <div class="tecnm-card">
            <div class="tecnm-card-header">
              <h3 class="tecnm-card-title">Gestión de Jefatura</h3>
            </div>
            <div class="tecnm-card-body" style="display: flex; flex-direction: column; gap: 0.625rem;">
              <router-link to="/advisors/assignments" class="tecnm-btn tecnm-btn-primary tecnm-btn-sm" style="width: 100%; justify-content: flex-start; gap: 0.5rem;">
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M19 7.5v3m0 0v3m0-3h3m-3 0h-3m-2.25-4.125a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0ZM4 19.235v-.11a6.375 6.375 0 0 1 12.75 0v.109A12.318 12.318 0 0 1 10.374 21c-2.331 0-4.512-.645-6.374-1.766Z" />
                </svg>
                Asignación de Asesores
              </router-link>
              <router-link to="/advisors" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" style="width: 100%; justify-content: flex-start; gap: 0.5rem;">
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M18 18.72a9.094 9.094 0 0 0 3.741-.479 3 3 0 0 0-4.682-2.72m.94 3.198.001.031c0 .225-.012.447-.037.666A11.944 11.944 0 0 1 12 21c-2.17 0-4.207-.576-5.963-1.584A6.062 6.062 0 0 1 6 18.719m12 0a5.971 5.971 0 0 0-.941-3.197m0 0A5.995 5.995 0 0 0 12 12.75a5.995 5.995 0 0 0-5.058 2.772m0 0a3 3 0 0 0-4.681 2.72 8.986 8.986 0 0 0 3.74.477m.94-3.197a5.971 5.971 0 0 0-.94 3.197M15 6.75a3 3 0 1 1-6 0 3 3 0 0 1 6 0Zm6 3a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Zm-13.5 0a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Z" />
                </svg>
                Directorio de Asesores
              </router-link>
              <router-link to="/projects/review" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" style="width: 100%; justify-content: flex-start; gap: 0.5rem;">
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z" />
                </svg>
                Anteproyectos de la Carrera
              </router-link>
            </div>
          </div>

          <!-- Card Carga de Asesorías por Docente (Extendida con buscador y capacidad) -->
          <div class="tecnm-card" style="margin-top: 1.5rem;">
            <div class="tecnm-card-header" style="display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 0.5rem;">
              <div>
                <h3 class="tecnm-card-title" style="margin-bottom: 0;">Carga de Asesorías por Docente</h3>
                <div style="font-size: 0.775rem; color: var(--tecnm-text-secondary); margin-top: 0.2rem;">
                  {{ careerAdvisors.length }} docentes adscritos
                </div>
              </div>
              <router-link to="/advisors" class="tecnm-link-action" style="font-size: 0.8rem;">
                Ver todos &rarr;
              </router-link>
            </div>

            <!-- Buscador rápido para múltiples asesores -->
            <div v-if="advisorWorkloadList.length > 3" style="padding: 0.625rem 1rem; border-bottom: 1px solid var(--tecnm-border-color, #e2e8f0); background: var(--tecnm-gray-50, #f8fafc);">
              <input
                v-model="advisorWorkloadSearch"
                type="search"
                placeholder="Filtrar docente por nombre..."
                class="tecnm-form-control tecnm-form-control-sm"
                style="font-size: 0.8rem; height: 2rem;"
              />
            </div>

            <div class="tecnm-card-body tecnm-p-0" style="max-height: 440px; overflow-y: auto;">
              <ul v-if="filteredAdvisorWorkload.length > 0" class="list-panel">
                <li
                  v-for="adv in filteredAdvisorWorkload"
                  :key="adv.id"
                  class="list-panel-item"
                  style="display: flex; justify-content: space-between; align-items: center; padding: 0.875rem 1rem; border-bottom: 1px solid var(--tecnm-border-color, #e2e8f0); gap: 0.75rem; cursor: pointer; transition: background-color 0.15s ease;"
                  :title="`Ver residentes asignados a ${adv.name}`"
                  @click="openAdvisorWorkloadModal(adv.id)"
                >
                  <div style="display: flex; align-items: center; gap: 0.75rem; min-width: 0; flex: 1;">
                    <div style="width: 34px; height: 34px; border-radius: 50%; background: #e0e7ff; color: #3730a3; display: flex; align-items: center; justify-content: center; font-weight: 700; font-size: 0.75rem; flex-shrink: 0;">
                      {{ adv.initials }}
                    </div>
                    <div style="min-width: 0; flex: 1;">
                      <strong style="font-size: 0.875rem; color: var(--tecnm-text-primary, #0f172a); display: block; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;">
                        {{ adv.name }}
                      </strong>
                      <div class="tecnm-text-sub" style="font-size: 0.75rem; margin-bottom: 0.25rem;">
                        {{ adv.title || 'Docente TecNM' }}
                      </div>
                      <!-- Mini barra de capacidad de asesorías -->
                      <div style="width: 100%; height: 4px; background: #e2e8f0; border-radius: 9999px; overflow: hidden;">
                        <div
                          :style="{
                            width: `${adv.percent}%`,
                            height: '100%',
                            backgroundColor: adv.count >= 5 ? '#ef4444' : adv.count >= 3 ? '#f59e0b' : '#3b82f6',
                            transition: 'width 0.3s ease'
                          }"
                        ></div>
                      </div>
                    </div>
                  </div>
                  <div style="display: flex; flex-direction: column; align-items: flex-end; gap: 0.2rem; flex-shrink: 0;">
                    <span
                      class="tecnm-badge"
                      :class="adv.count === 0 ? 'tecnm-badge-neutral' : adv.count >= 4 ? 'tecnm-badge-warning' : 'tecnm-badge-primary'"
                      style="font-size: 0.75rem;"
                    >
                      {{ adv.count }} {{ adv.count === 1 ? 'residente' : 'residentes' }}
                    </span>
                    <span style="font-size: 0.7rem; color: var(--tecnm-blue-primary, #1B396A); font-weight: 600;">
                      Ver detalle &rarr;
                    </span>
                  </div>
                </li>
              </ul>
              <div v-else-if="advisorWorkloadSearch" style="padding: 1.5rem; text-align: center; color: var(--tecnm-text-secondary, #64748b); font-size: 0.825rem;">
                No se encontraron docentes con "{{ advisorWorkloadSearch }}".
              </div>
              <div v-else style="padding: 1.5rem; text-align: center; color: var(--tecnm-text-secondary, #64748b); font-size: 0.875rem;">
                No hay asesores registrados en tu academia.
              </div>
            </div>

            <!-- Resumen al pie del componente extendido -->
            <div style="padding: 0.75rem 1rem; background: var(--tecnm-gray-50, #f8fafc); border-top: 1px solid var(--tecnm-border-color, #e2e8f0); display: flex; justify-content: space-between; align-items: center; font-size: 0.8rem;">
              <span style="color: var(--tecnm-text-secondary);">Residentes asignados: <strong>{{ assignedStudentsCount }}</strong></span>
              <router-link to="/advisors" style="color: var(--tecnm-blue-primary, #1B396A); font-weight: 600;">
                Directorio completo &rarr;
              </router-link>
            </div>
          </div>
        </template>

        <!-- LATERAL PARA ADMIN / STAFF / VINCULACIÓN / JEFE -->
        <template v-else>
          <!-- Acciones Rápidas -->
          <div class="tecnm-card">
            <div class="tecnm-card-header">
              <h3 class="tecnm-card-title">Acciones Rápidas</h3>
            </div>
            <div class="tecnm-card-body" style="display: flex; flex-direction: column; gap: 0.75rem;">
              <router-link v-if="authStore.isAdmin || authStore.hasPermission('students.manage')" to="/students" class="tecnm-btn tecnm-btn-secondary" style="justify-content: flex-start; gap: 0.5rem;">
                <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M19 7.5v3m0 0v3m0-3h3m-3 0h-3m-2.25-4.125a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0ZM4 19.235v-.11a6.375 6.375 0 0 1 12.75 0v.109A12.318 12.318 0 0 1 10.374 21c-2.331 0-4.512-.645-6.374-1.766Z" />
                </svg>
                Registrar Estudiante
              </router-link>

              <router-link v-if="authStore.isAdmin || authStore.hasRole('departmenthead', 'academic')" to="/advisors/assignments" class="tecnm-btn tecnm-btn-secondary" style="justify-content: flex-start; gap: 0.5rem;">
                <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M18 18.72a9.094 9.094 0 0 0 3.741-.479 3 3 0 0 0-4.682-2.72m.94 3.198.001.031c0 .225-.012.447-.037.666A11.944 11.944 0 0 1 12 21c-2.17 0-4.207-.576-5.963-1.584A6.062 6.062 0 0 1 6 18.719m12 0a5.971 5.971 0 0 0-.941-3.197m0 0A5.995 5.995 0 0 0 12 12.75" />
                </svg>
                Asignación de Asesores
              </router-link>

              <router-link v-if="authStore.isAdmin || authStore.hasRole('vinculacion')" to="/companies" class="tecnm-btn tecnm-btn-secondary" style="justify-content: flex-start; gap: 0.5rem;">
                <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M3.75 21h16.5M4.5 3h15M5.25 3v18m13.5-18v18M9 6.75h1.5m-1.5 3h1.5m-1.5 3h1.5m3-6H15m-1.5 3H15m-1.5 3H15" />
                </svg>
                Registrar Empresa Receptora
              </router-link>

              <router-link v-if="authStore.isAdmin || authStore.hasRole('departmenthead', 'director', 'academic')" to="/admin/reports" class="tecnm-btn tecnm-btn-outline" style="justify-content: flex-start; gap: 0.5rem;">
                <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z" />
                </svg>
                Generar Reportes Oficiales
              </router-link>
            </div>
          </div>

          <!-- Resumen de Estado Institucional -->
          <div class="tecnm-card" style="margin-top: 1.5rem;">
            <div class="tecnm-card-header">
              <h3 class="tecnm-card-title">Resumen Operativo</h3>
            </div>
            <div class="tecnm-card-body">
              <div style="font-size: 0.85rem; color: var(--tecnm-text-secondary); line-height: 1.5;">
                <div style="display: flex; justify-content: space-between; margin-bottom: 0.5rem;">
                  <span>Carrera Activa:</span>
                  <strong>{{ selectedCareerFilter === 'all' ? 'Todas las Carreras' : CAREERS[selectedCareerFilter] }}</strong>
                </div>
                <div style="display: flex; justify-content: space-between; margin-bottom: 0.5rem;">
                  <span>Periodo Académico:</span>
                  <strong>Agosto - Diciembre 2026</strong>
                </div>
                <div style="display: flex; justify-content: space-between;">
                  <span>Semanas Oficiales:</span>
                  <strong>26 Semanas (500 hrs)</strong>
                </div>
              </div>
            </div>
          </div>
        </template>
      </div>
    </div>
  </div>

  <!-- Modal de Detalle de Carga y Residentes por Asesor -->
  <AdvisorWorkloadModal
    v-model="isWorkloadModalOpen"
    :advisor-id="selectedAdvisorForModal"
  />
</template>

<style scoped>
/* Grid de KPIs Estandarizado */
.tecnm-kpis-grid {
  display: grid;
  gap: 1.25rem;
  margin-bottom: 1.75rem;
}

.tecnm-kpis-grid--6 {
  grid-template-columns: repeat(3, 1fr);
}

.tecnm-kpis-grid--4 {
  grid-template-columns: repeat(4, 1fr);
}

@media (max-width: 1180px) {
  .tecnm-kpis-grid--6,
  .tecnm-kpis-grid--4 {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 640px) {
  .tecnm-kpis-grid--6,
  .tecnm-kpis-grid--4 {
    grid-template-columns: 1fr;
    gap: 0.875rem;
  }
}

/* Filtro de Carrera Moderno */
.tecnm-filter-bar {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.tecnm-filter-container {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  background-color: var(--tecnm-surface-white, #ffffff);
  border: 1px solid var(--tecnm-border-color, #e2e8f0);
  border-radius: var(--tecnm-radius-md, 0.375rem);
  padding: 0.35rem 0.75rem;
  box-shadow: 0 1px 2px 0 rgba(0, 0, 0, 0.04);
  transition: all 0.2s ease;
}

.tecnm-filter-container:focus-within {
  border-color: var(--tecnm-blue-primary, #1b396a);
  box-shadow: 0 0 0 3px rgba(27, 57, 106, 0.12);
}

.tecnm-filter-icon {
  width: 16px;
  height: 16px;
  color: var(--tecnm-text-secondary, #64748b);
  flex-shrink: 0;
}

.tecnm-filter-select {
  border: none;
  background: transparent;
  font-size: 0.85rem;
  font-weight: 500;
  color: var(--tecnm-text-primary, #1e293b);
  outline: none;
  cursor: pointer;
  padding-right: 0.5rem;
}

/* Encabezados y Subtítulos */
.tecnm-header-icon {
  width: 20px;
  height: 20px;
  color: var(--tecnm-blue-primary, #1b396a);
}

.tecnm-header-icon--gold {
  color: #b45309;
}

.tecnm-card-subtitle-tag {
  font-size: 0.8125rem;
  color: var(--tecnm-blue-primary, #1b396a);
  font-weight: 500;
  margin-left: 0.35rem;
}

.tecnm-link-action {
  font-size: 0.8125rem;
  font-weight: 600;
  color: var(--tecnm-blue-primary, #1b396a);
  text-decoration: none;
  transition: color 0.15s ease;
}

.tecnm-link-action:hover {
  text-decoration: underline;
}

.tecnm-table-link {
  font-weight: 600;
  color: var(--tecnm-blue-primary, #1b396a);
  text-decoration: none;
}

.tecnm-table-link:hover {
  text-decoration: underline;
}

.tecnm-text-sub {
  font-size: 0.75rem;
  color: var(--tecnm-text-secondary, #64748b);
  margin-top: 0.1rem;
}

.tecnm-project-title-cell {
  font-weight: 500;
  max-width: 240px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* Progress bar en tablas */
.tecnm-progress-header {
  display: flex;
  justify-content: space-between;
  font-size: 0.75rem;
  margin-bottom: 0.25rem;
  font-weight: 600;
  color: var(--tecnm-blue-primary, #1b396a);
}

.tecnm-progress-track {
  height: 6px;
  border-radius: 3px;
  background-color: var(--tecnm-border-color, #e2e8f0);
  overflow: hidden;
}

.tecnm-progress-fill {
  height: 100%;
  background: linear-gradient(90deg, var(--tecnm-blue-primary, #1b396a), #3b82f6);
  border-radius: 3px;
  transition: width 0.3s ease;
}

/* Stepper para Estudiantes */
.tecnm-stepper {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.75rem 0.5rem;
}

.tecnm-step {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  gap: 0.5rem;
  z-index: 1;
}

.tecnm-step-circle {
  width: 36px;
  height: 36px;
  border-radius: 50%;
  background-color: var(--tecnm-surface-white, #ffffff);
  border: 2px solid var(--tecnm-border-color, #cbd5e1);
  color: var(--tecnm-text-secondary, #64748b);
  font-weight: 700;
  font-size: 0.875rem;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
}

.tecnm-step-label {
  font-size: 0.75rem;
  font-weight: 600;
  color: var(--tecnm-text-secondary, #64748b);
  max-width: 90px;
}

.tecnm-step.is-active .tecnm-step-circle {
  border-color: var(--tecnm-blue-primary, #1b396a);
  background-color: var(--tecnm-blue-primary, #1b396a);
  color: #ffffff;
  box-shadow: 0 0 0 4px rgba(27, 57, 106, 0.15);
}

.tecnm-step.is-active .tecnm-step-label {
  color: var(--tecnm-blue-primary, #1b396a);
}

.tecnm-step.is-completed .tecnm-step-circle {
  border-color: #10b981;
  background-color: #10b981;
  color: #ffffff;
}

.tecnm-step.is-warning .tecnm-step-circle {
  border-color: #f59e0b;
  background-color: #f59e0b;
  color: #ffffff;
}

.tecnm-step-line {
  flex: 1;
  height: 2px;
  background-color: var(--tecnm-border-color, #e2e8f0);
  margin: 0 0.5rem -1.25rem 0.5rem;
  transition: background-color 0.2s ease;
}

.tecnm-step-line.is-completed {
  background-color: #10b981;
}

/* Formatos Oficiales List */
.tecnm-format-list {
  display: flex;
  flex-direction: column;
}

.tecnm-format-item {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1rem 1.25rem;
  border-bottom: 1px solid var(--tecnm-border-color, #e2e8f0);
  text-decoration: none;
  color: inherit;
  transition: background-color 0.15s ease;
}

.tecnm-format-item:last-child {
  border-bottom: none;
}

.tecnm-format-item:hover {
  background-color: #f8fafc;
}

.tecnm-format-icon {
  width: 40px;
  height: 40px;
  border-radius: 8px;
  background-color: rgba(27, 57, 106, 0.08);
  color: var(--tecnm-blue-primary, #1b396a);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.tecnm-format-icon svg {
  width: 20px;
  height: 20px;
}

.tecnm-format-info {
  flex: 1;
  min-width: 0;
}

/* Task Item */
.tecnm-task-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  width: 100%;
}
</style>

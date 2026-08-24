<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const route = useRoute()
const authStore = useAuthStore()

const isMobileOpen = ref(false)
const openGroup = ref(null) // 'academica' | 'residencia' | 'evaluacion' | 'administracion' | null

function toggleGroup(groupKey) {
  if (openGroup.value === groupKey) {
    openGroup.value = null
  } else {
    openGroup.value = groupKey
  }
}

function toggleMobileNav() {
  isMobileOpen.value = !isMobileOpen.value
}

function closeAll() {
  openGroup.value = null
  isMobileOpen.value = false
}

// Cerrar dropdown al hacer click fuera del navbar
function handleDocumentClick(e) {
  if (!e.target.closest('.tecnm-nav-group')) {
    openGroup.value = null
  }
}

// Cerrar al presionar Escape
function handleKeydown(e) {
  if (e.key === 'Escape') {
    closeAll()
  }
}

onMounted(() => {
  document.addEventListener('click', handleDocumentClick)
  document.addEventListener('keydown', handleKeydown)
})

onUnmounted(() => {
  document.removeEventListener('click', handleDocumentClick)
  document.removeEventListener('keydown', handleKeydown)
})

// Detección de grupos activos por ruta actual
const isAcademicaActive = computed(() => {
  const p = route.path
  return p.startsWith('/students') || p.startsWith('/advisors') || p.startsWith('/companies')
})

const isResidenciaActive = computed(() => {
  const p = route.path
  return p.startsWith('/projects') || p.startsWith('/activities')
})

const isEvaluacionActive = computed(() => {
  const p = route.path
  return p.startsWith('/evaluations') || p.startsWith('/documents')
})

const isAdminActive = computed(() => {
  const p = route.path
  return p.startsWith('/admin')
})

// Visibilidad por permiso estricto (RBAC)
const showStudents = computed(() =>
  authStore.isAdmin ||
  authStore.hasPermission('students.manage') ||
  authStore.hasPermission('students.profile.view')
)

const showAdvisors = computed(() =>
  authStore.isAdmin ||
  authStore.hasPermission('advisors.manage')
)

const showAdvisorAssignments = computed(() =>
  authStore.isAdmin ||
  authStore.hasRole('admin', 'departmenthead', 'academic')
)

const showCompanies = computed(() =>
  authStore.isAdmin ||
  authStore.hasPermission('companies.view') ||
  authStore.hasPermission('companies.manage')
)

const showAcademicaGroup = computed(() =>
  showStudents.value || showAdvisors.value || showAdvisorAssignments.value || showCompanies.value
)

const showProposal = computed(() =>
  authStore.isAdmin ||
  authStore.hasPermission('projects.proposals')
)

const showReview = computed(() =>
  authStore.isAdmin ||
  authStore.hasPermission('projects.review')
)

const showSchedule = computed(() =>
  authStore.isAdmin ||
  authStore.hasPermission('activities.schedule')
)

const showResidenciaGroup = computed(() =>
  showProposal.value || showReview.value || showSchedule.value
)

const showAdvisories = computed(() =>
  authStore.isAdmin ||
  authStore.hasPermission('evaluations.advisories')
)

const showGrading = computed(() =>
  authStore.isAdmin ||
  authStore.hasPermission('evaluations.grading')
)

const showDocuments = computed(() =>
  authStore.isAdmin ||
  authStore.hasPermission('documents.digital')
)

const showEvaluacionGroup = computed(() =>
  showAdvisories.value || showGrading.value || showDocuments.value
)

const showReports = computed(() =>
  authStore.isAdmin ||
  authStore.hasPermission('admin.reports')
)

const showRoles = computed(() => authStore.isAdmin)

const showSettings = computed(() => authStore.isAdmin)

const showAdminGroup = computed(() =>
  showReports.value || showRoles.value || showSettings.value
)
</script>

<template>
  <nav class="tecnm-navbar">
    <div class="tecnm-container">
      <div class="tecnm-navbar-inner">
        <!-- Toggle Móvil -->
        <button
          type="button"
          id="navToggle"
          class="tecnm-nav-toggle"
          :aria-expanded="isMobileOpen"
          aria-controls="navbarNav"
          :aria-label="isMobileOpen ? 'Cerrar menú' : 'Abrir menú'"
          @click="toggleMobileNav"
        >
          <svg
            v-if="!isMobileOpen"
            class="tecnm-nav-toggle-icon tecnm-nav-toggle-icon-open"
            xmlns="http://www.w3.org/2000/svg"
            fill="none"
            viewBox="0 0 24 24"
            stroke-width="2"
            stroke="currentColor"
          >
            <path stroke-linecap="round" stroke-linejoin="round" d="M3.75 6.75h16.5M3.75 12h16.5m-16.5 5.25h16.5" />
          </svg>
          <svg
            v-else
            class="tecnm-nav-toggle-icon tecnm-nav-toggle-icon-close"
            xmlns="http://www.w3.org/2000/svg"
            fill="none"
            viewBox="0 0 24 24"
            stroke-width="2"
            stroke="currentColor"
          >
            <path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
          </svg>
        </button>

        <!-- Lista de Navegación -->
        <ul
          id="navbarNav"
          class="tecnm-navbar-nav"
          :class="{ open: isMobileOpen }"
        >
          <!-- Panel Principal -->
          <li class="tecnm-nav-item-standalone">
            <router-link
              to="/dashboard"
              class="tecnm-nav-item"
              :class="{ active: route.path === '/dashboard' }"
              data-nav-icon="home"
              @click="closeAll"
            >
              <span class="tecnm-nav-item-icon" aria-hidden="true">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M2.25 12 12 2.25 21.75 12M4.5 9.75v10.5a.75.75 0 0 0 .75.75h4.5a.75.75 0 0 0 .75-.75v-6a.75.75 0 0 1 .75-.75h3a.75.75 0 0 1 .75.75v6a.75.75 0 0 0 .75.75h4.5a.75.75 0 0 0 .75-.75V9.75M3.75 9.75 12 3l8.25 6.75" />
                </svg>
              </span>
              Panel Principal
            </router-link>
          </li>

          <!-- Gestión Académica -->
          <li
            v-if="showAcademicaGroup"
            class="tecnm-nav-group"
            :class="{ open: openGroup === 'academica', 'is-active': isAcademicaActive }"
            data-group="academica"
          >
            <button
              type="button"
              class="tecnm-nav-group-btn"
              :aria-expanded="openGroup === 'academica'"
              aria-controls="navg-academica"
              @click.stop="toggleGroup('academica')"
            >
              <span class="tecnm-nav-group-icon" data-nav-group-icon="users" aria-hidden="true">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Zm8.25 2.25a2.625 2.625 0 1 1-5.25 0 2.625 2.625 0 0 1 5.25 0Z" />
                </svg>
              </span>
              <span class="tecnm-nav-group-label">Gestión Académica</span>
              <span class="tecnm-nav-group-chevron" aria-hidden="true">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 8.25 12 15.75 4.5 8.25" />
                </svg>
              </span>
            </button>
            <ul id="navg-academica" class="tecnm-nav-sublist">
              <li v-if="showStudents">
                <router-link
                  to="/students"
                  class="tecnm-nav-item"
                  :class="{ active: route.path.startsWith('/students') }"
                  data-nav-icon="users"
                  @click="closeAll"
                >
                  <span class="tecnm-nav-item-icon" aria-hidden="true">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Zm8.25 2.25a2.625 2.625 0 1 1-5.25 0 2.625 2.625 0 0 1 5.25 0Z" />
                    </svg>
                  </span>
                  Estudiantes
                </router-link>
              </li>
              <li v-if="showAdvisors">
                <router-link
                  to="/advisors"
                  class="tecnm-nav-item"
                  :class="{ active: route.path === '/advisors' }"
                  data-nav-icon="user-group"
                  @click="closeAll"
                >
                  <span class="tecnm-nav-item-icon" aria-hidden="true">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M18 18.72a9.094 9.094 0 0 0 3.741-.479 3 3 0 0 0-4.682-2.72m.94 3.198.001.031c0 .225-.012.447-.037.666A11.944 11.944 0 0 1 12 21c-2.17 0-4.207-.576-5.963-1.584A6.062 6.062 0 0 1 6 18.719m12 0a5.971 5.971 0 0 0-.941-3.197m0 0A5.995 5.995 0 0 0 12 12.75a5.995 5.995 0 0 0-5.058 2.772m0 0a3 3 0 0 0-4.681 2.72 8.986 8.986 0 0 0 3.74.477m.94-3.197a5.971 5.971 0 0 0-.94 3.197M15 6.75a3 3 0 1 1-6 0 3 3 0 0 1 6 0Zm6 3a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Zm-13.5 0a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Z" />
                    </svg>
                  </span>
                  Directorio de Asesores
                </router-link>
              </li>
              <li v-if="showAdvisorAssignments">
                <router-link
                  to="/advisors/assignments"
                  class="tecnm-nav-item"
                  :class="{ active: route.path.startsWith('/advisors/assignments') }"
                  data-nav-icon="user-plus"
                  @click="closeAll"
                >
                  <span class="tecnm-nav-item-icon" aria-hidden="true">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M19 7.5v3m0 0v3m0-3h3m-3 0h-3m-2.25-4.125a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0ZM4 19.235v-.11a6.375 6.375 0 0 1 12.75 0v.109A12.318 12.318 0 0 1 10.374 21c-2.331 0-4.512-.645-6.374-1.766Z" />
                    </svg>
                  </span>
                  Asignación de Asesores
                </router-link>
              </li>
              <li v-if="showCompanies">
                <router-link
                  to="/companies"
                  class="tecnm-nav-item"
                  :class="{ active: route.path.startsWith('/companies') }"
                  data-nav-icon="building"
                  @click="closeAll"
                >
                  <span class="tecnm-nav-item-icon" aria-hidden="true">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M2.25 21h19.5m-18-18v18m10.5-18v18m6-13.5V21M6.75 6.75h.75m-.75 3h.75m-.75 3h.75m3-6h.75m-.75 3h.75m-.75 3h.75M6.75 21v-3.375c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125V21M3 3h12m-.75 4.5H21m-3.75 3.75h.008v.008h-.008v-.008Zm0 3h.008v.008h-.008v-.008Zm0 3h.008v.008h-.008v-.008Z" />
                    </svg>
                  </span>
                  Empresas Receptoras
                </router-link>
              </li>
            </ul>
          </li>

          <!-- Residencia Profesional -->
          <li
            v-if="showResidenciaGroup"
            class="tecnm-nav-group"
            :class="{ open: openGroup === 'residencia', 'is-active': isResidenciaActive }"
            data-group="residencia"
          >
            <button
              type="button"
              class="tecnm-nav-group-btn"
              :aria-expanded="openGroup === 'residencia'"
              aria-controls="navg-residencia"
              @click.stop="toggleGroup('residencia')"
            >
              <span class="tecnm-nav-group-icon" data-nav-group-icon="document" aria-hidden="true">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z" />
                </svg>
              </span>
              <span class="tecnm-nav-group-label">Residencia Profesional</span>
              <span class="tecnm-nav-group-chevron" aria-hidden="true">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 8.25 12 15.75 4.5 8.25" />
                </svg>
              </span>
            </button>
            <ul id="navg-residencia" class="tecnm-nav-sublist">
              <li v-if="showProposal">
                <router-link
                  to="/projects/proposal"
                  class="tecnm-nav-item"
                  :class="{ active: route.path === '/projects/proposal' }"
                  data-nav-icon="pencil"
                  @click="closeAll"
                >
                  <span class="tecnm-nav-item-icon" aria-hidden="true">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" d="m16.862 4.487 1.687-1.688a1.875 1.875 0 1 1 2.652 2.652L10.582 16.07a4.5 4.5 0 0 1-1.897 1.13L6 18l.8-2.685a4.5 4.5 0 0 1 1.13-1.897l8.932-8.931Zm0 0L19.5 7.125M18 14v4.75A2.25 2.25 0 0 1 15.75 21H5.25A2.25 2.25 0 0 1 3 18.75V8.25A2.25 2.25 0 0 1 5.25 6H10" />
                    </svg>
                  </span>
                  Solicitud de Anteproyecto
                </router-link>
              </li>
              <li v-if="showReview">
                <router-link
                  to="/projects/review"
                  class="tecnm-nav-item"
                  :class="{ active: route.path === '/projects/review' }"
                  data-nav-icon="clipboard"
                  @click="closeAll"
                >
                  <span class="tecnm-nav-item-icon" aria-hidden="true">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M9 12h3.75M9 15h3.75M9 18h3.75m3 .75H18a2.25 2.25 0 0 0 2.25-2.25V6.108c0-1.135-.845-2.098-1.976-2.192a48.424 48.424 0 0 0-1.123-.08m-5.801 0c-.065.21-.1.433-.1.664 0 .414.336.75.75.75h4.5a.75.75 0 0 0 .75-.75 2.25 2.25 0 0 0-.1-.664m-5.8 0A2.251 2.251 0 0 1 13.5 2.25H15c1.012 0 1.867.668 2.15 1.586m-5.8 0c-.376.023-.75.05-1.124.08C9.095 4.01 8.25 4.973 8.25 6.108V8.25m0 0H4.875c-.621 0-1.125.504-1.125 1.125v11.25c0 .621.504 1.125 1.125 1.125h9.75c.621 0 1.125-.504 1.125-1.125V9.375c0-.621-.504-1.125-1.125-1.125H8.25Z" />
                    </svg>
                  </span>
                  Dictamen de División
                </router-link>
              </li>
              <li v-if="showSchedule">
                <router-link
                  to="/activities/schedule"
                  class="tecnm-nav-item"
                  :class="{ active: route.path === '/activities/schedule' }"
                  data-nav-icon="calendar"
                  @click="closeAll"
                >
                  <span class="tecnm-nav-item-icon" aria-hidden="true">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 0 1 2.25-2.25h13.5A2.25 2.25 0 0 1 21 7.5v11.25m-18 0A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75m-18 0v-7.5A2.25 2.25 0 0 1 5.25 9h13.5A2.25 2.25 0 0 1 21 11.25v7.5" />
                    </svg>
                  </span>
                  Cronograma de Actividades
                </router-link>
              </li>
            </ul>
          </li>

          <!-- Evaluación y Seguimiento -->
          <li
            v-if="showEvaluacionGroup"
            class="tecnm-nav-group"
            :class="{ open: openGroup === 'evaluacion', 'is-active': isEvaluacionActive }"
            data-group="evaluacion"
          >
            <button
              type="button"
              class="tecnm-nav-group-btn"
              :aria-expanded="openGroup === 'evaluacion'"
              aria-controls="navg-evaluacion"
              @click.stop="toggleGroup('evaluacion')"
            >
              <span class="tecnm-nav-group-icon" data-nav-group-icon="book" aria-hidden="true">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M12 6.042A8.967 8.967 0 0 0 6 3.75c-1.052 0-2.062.18-3 .512v14.25A8.987 8.987 0 0 1 6 18c2.305 0 4.408.867 6 2.292m0-14.25a8.966 8.966 0 0 1 6-2.292c1.052 0 2.062.18 3 .512v14.25A8.987 8.987 0 0 0 18 18a8.967 8.967 0 0 0-6 2.292m0-14.25v14.25" />
                </svg>
              </span>
              <span class="tecnm-nav-group-label">Evaluación y Seguimiento</span>
              <span class="tecnm-nav-group-chevron" aria-hidden="true">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 8.25 12 15.75 4.5 8.25" />
                </svg>
              </span>
            </button>
            <ul id="navg-evaluacion" class="tecnm-nav-sublist">
              <li v-if="showAdvisories">
                <router-link
                  to="/evaluations"
                  class="tecnm-nav-item"
                  :class="{ active: route.path === '/evaluations' }"
                  data-nav-icon="book"
                  @click="closeAll"
                >
                  <span class="tecnm-nav-item-icon" aria-hidden="true">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M12 6.042A8.967 8.967 0 0 0 6 3.75c-1.052 0-2.062.18-3 .512v14.25A8.987 8.987 0 0 1 6 18c2.305 0 4.408.867 6 2.292m0-14.25a8.966 8.966 0 0 1 6-2.292c1.052 0 2.062.18 3 .512v14.25A8.987 8.987 0 0 0 18 18a8.967 8.967 0 0 0-6 2.292m0-14.25v14.25" />
                    </svg>
                  </span>
                  Bitácora de Asesorías
                </router-link>
              </li>
              <li v-if="showGrading">
                <router-link
                  to="/evaluations/grading"
                  class="tecnm-nav-item"
                  :class="{ active: route.path === '/evaluations/grading' }"
                  data-nav-icon="star"
                  @click="closeAll"
                >
                  <span class="tecnm-nav-item-icon" aria-hidden="true">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M11.48 3.499a.562.562 0 0 1 1.04 0l2.125 5.111a.563.563 0 0 0 .475.345l5.518.442c.499.04.701.663.321.988l-4.204 3.602a.563.563 0 0 0-.182.557l1.285 5.385a.562.562 0 0 1-.84.61l-4.725-2.885a.562.562 0 0 0-.586 0L6.982 20.54a.562.562 0 0 1-.84-.61l1.285-5.386a.562.562 0 0 0-.182-.557l-4.204-3.602a.562.562 0 0 1 .321-.988l5.518-.442a.563.563 0 0 0 .475-.345L11.48 3.5Z" />
                    </svg>
                  </span>
                  Evaluaciones
                </router-link>
              </li>
              <li v-if="showDocuments">
                <router-link
                  to="/documents"
                  class="tecnm-nav-item"
                  :class="{ active: route.path.startsWith('/documents') }"
                  data-nav-icon="folder"
                  @click="closeAll"
                >
                  <span class="tecnm-nav-item-icon" aria-hidden="true">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M2.25 12.75V12A2.25 2.25 0 0 1 4.5 9.75h15A2.25 2.25 0 0 1 21.75 12v.75m-8.69-6.44-2.12-2.12a1.5 1.5 0 0 0-1.061-.44H4.5A2.25 2.25 0 0 0 2.25 6v12a2.25 2.25 0 0 0 2.25 2.25h15A2.25 2.25 0 0 0 21.75 18V9a2.25 2.25 0 0 0-2.25-2.25h-5.379a1.5 1.5 0 0 1-1.06-.44Z" />
                    </svg>
                  </span>
                  Expediente Digital
                </router-link>
              </li>
            </ul>
          </li>

          <!-- Administración -->
          <li
            v-if="showAdminGroup"
            class="tecnm-nav-group"
            :class="{ open: openGroup === 'administracion', 'is-active': isAdminActive }"
            data-group="administracion"
          >
            <button
              type="button"
              class="tecnm-nav-group-btn"
              :aria-expanded="openGroup === 'administracion'"
              aria-controls="navg-administracion"
              @click.stop="toggleGroup('administracion')"
            >
              <span class="tecnm-nav-group-icon" data-nav-group-icon="chart" aria-hidden="true">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M3 13.125C3 12.504 3.504 12 4.125 12h2.25c.621 0 1.125.504 1.125 1.125v6.75C7.5 20.496 6.996 21 6.375 21h-2.25A1.125 1.125 0 0 1 3 19.875v-6.75ZM9.75 8.625c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125v11.25c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 0 1-1.125-1.125V8.625ZM16.5 4.125c0-.621.504-1.125 1.125-1.125h2.25C20.496 3 21 4.125 21 4.125v15.75c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 0 1-1.125-1.125V4.125Z" />
                </svg>
              </span>
              <span class="tecnm-nav-group-label">Administración</span>
              <span class="tecnm-nav-group-chevron" aria-hidden="true">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 8.25 12 15.75 4.5 8.25" />
                </svg>
              </span>
            </button>
            <ul id="navg-administracion" class="tecnm-nav-sublist">
              <li v-if="showReports">
                <router-link
                  to="/admin/reports"
                  class="tecnm-nav-item"
                  :class="{ active: route.path === '/admin/reports' }"
                  data-nav-icon="chart"
                  @click="closeAll"
                >
                  <span class="tecnm-nav-item-icon" aria-hidden="true">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M3 13.125C3 12.504 3.504 12 4.125 12h2.25c.621 0 1.125.504 1.125 1.125v6.75C7.5 20.496 6.996 21 6.375 21h-2.25A1.125 1.125 0 0 1 3 19.875v-6.75ZM9.75 8.625c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125v11.25c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 0 1-1.125-1.125V8.625ZM16.5 4.125c0-.621.504-1.125 1.125-1.125h2.25C20.496 3 21 4.125 21 4.125v15.75c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 0 1-1.125-1.125V4.125Z" />
                    </svg>
                  </span>
                  Reportes y Liberación
                </router-link>
              </li>
              <li v-if="showRoles">
                <router-link
                  to="/admin/roles"
                  class="tecnm-nav-item"
                  :class="{ active: route.path === '/admin/roles' }"
                  data-nav-icon="user-group"
                  @click="closeAll"
                >
                  <span class="tecnm-nav-item-icon" aria-hidden="true">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M18 18.72a9.094 9.094 0 0 0 3.741-.479 3 3 0 0 0-4.682-2.72m.94 3.198.001.031c0 .225-.012.447-.037.666A11.944 11.944 0 0 1 12 21c-2.17 0-4.207-.576-5.963-1.584A6.062 6.062 0 0 1 6 18.719m12 0a5.971 5.971 0 0 0-.941-3.197m0 0A5.995 5.995 0 0 0 12 12.75a5.995 5.995 0 0 0-5.058 2.772m0 0a3 3 0 0 0-4.681 2.72 8.986 8.986 0 0 0 3.74.477m.94-3.197a5.971 5.971 0 0 0-.94 3.197M15 6.75a3 3 0 1 1-6 0 3 3 0 0 1 6 0Zm6 3a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Zm-13.5 0a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Z" />
                    </svg>
                  </span>
                  Usuarios y Roles
                </router-link>
              </li>
              <li v-if="showSettings">
                <router-link
                  to="/admin/settings"
                  class="tecnm-nav-item"
                  :class="{ active: route.path === '/admin/settings' }"
                  data-nav-icon="settings"
                  @click="closeAll"
                >
                  <span class="tecnm-nav-item-icon" aria-hidden="true">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M9.594 3.94c.09-.542.56-.94 1.11-.94h3.593c.55 0 1.02.398 1.11.94l.213 1.281c.063.374.313.686.645.87.074.04.147.083.22.127.325.196.72.257 1.075.124l1.217-.456a1.125 1.125 0 0 1 1.37.49l1.796 3.111a1.125 1.125 0 0 1-.26 1.431l-1.003.827c-.293.241-.438.613-.43.992a7.723 7.723 0 0 1 0 .255c-.008.378.137.75.43.991l1.004.827c.424.35.534.955.26 1.43l-1.798 3.111a1.125 1.125 0 0 1-1.37.49l-1.216-.456c-.356-.133-.75-.072-1.076.124a6.57 6.57 0 0 1-.22.128c-.331.183-.581.495-.644.869l-.213 1.281c-.09.543-.56.94-1.11.94h-3.594c-.55 0-1.019-.398-1.11-.94l-.213-1.281c-.062-.374-.312-.686-.644-.87a6.52 6.52 0 0 1-.22-.127c-.325-.196-.72-.257-1.076-.124l-1.217.456a1.125 1.125 0 0 1-1.369-.49l-1.797-3.111a1.125 1.125 0 0 1 .26-1.431l1.004-.827c.292-.24.437-.613.43-.991a6.932 6.932 0 0 1 0-.255c.007-.38-.138-.751-.43-.992l-1.004-.827a1.125 1.125 0 0 1-.26-1.43l1.797-3.111a1.125 1.125 0 0 1 1.37-.49l1.216.456c.356.133.751.072 1.076-.124.072-.044.146-.086.22-.128.332-.183.582-.495.644-.869l.214-1.28Z" />
                      <path stroke-linecap="round" stroke-linejoin="round" d="M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z" />
                    </svg>
                  </span>
                  Configuración del Sistema
                </router-link>
              </li>
            </ul>
          </li>
        </ul>
      </div>
    </div>
  </nav>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useGlobalSearch } from '@/composables/useGlobalSearch'
import { useAudit } from '@/composables/useAudit'
import { useConfirm } from '@/composables/useConfirm'
import TecnmPagination from '@/components/common/TecnmPagination.vue'
import TecnmAutocomplete from '@/components/common/TecnmAutocomplete.vue'
import apiClient from '@/services/api'

const authStore = useAuthStore()
const { open: openGlobalSearch } = useGlobalSearch()
const { showAudit } = useAudit()
const { confirm } = useConfirm()

const isStudent = computed(() =>
  authStore.hasRole('student') && !authStore.hasRole('admin', 'departmenthead', 'advisor')
)
const isAdvisor = computed(() =>
  authStore.hasRole('advisor') && !authStore.hasRole('admin', 'departmenthead')
)
const isStaff = computed(() =>
  authStore.hasRole('admin') || authStore.hasRole('departmenthead')
)

const currentProject = ref(null)
const sessions = ref([])
const isLoading = ref(false)
const errorMessage = ref('')
const alertMessage = ref('')
const alertType = ref('info')

const isProjectCompleted = computed(() => {
  const st = String(currentProject.value?.status || '').toLowerCase()
  return st === 'completed' || currentProject.value?.isCompleted === true
})

const isProjectPending = computed(() => {
  const st = String(currentProject.value?.status || '').toLowerCase()
  return ['pending', 'proposed', 'under_review'].includes(st)
})

const isProjectDraft = computed(() => {
  const st = String(currentProject.value?.status || '').toLowerCase()
  return st === 'draft'
})

const isProjectRejected = computed(() => {
  const st = String(currentProject.value?.status || '').toLowerCase()
  return st === 'rejected'
})

const isProjectReadOnly = computed(() => {
  if (!currentProject.value) return true
  const st = String(currentProject.value?.status || '').toLowerCase()
  return isProjectCompleted.value || ['cancelled', 'rejected', 'pending', 'under_review', 'proposed', 'draft'].includes(st)
})

const canRecordSession = computed(() => {
  if (authStore.isReadOnly) return false
  if (!authStore.hasRole('admin', 'departmenthead', 'advisor')) return false
  if (!currentProject.value?.id) return false
  if (isAdvisor.value && isProjectReadOnly.value) return false
  return true
})

// Paginación y Filtros
const pageNumber = ref(1)
const pageSize = ref(10)
const totalCount = ref(0)
const totalPages = ref(0)
const search = ref('')
const sortBy = ref('SessionDate')
const sortDir = ref('desc')
const includeInactive = ref(false)

// Modales
const isCreateModalOpen = ref(false)
const isEditModalOpen = ref(false)
const isSubmitting = ref(false)

// Formulario de Alta
const createForm = ref({
  projectId: null,
  advisorId: null,
  sessionDate: new Date().toISOString().split('T')[0],
  topicsCovered: '',
  studentAgreements: '',
})
const initialAdvisor = ref(null)

// Formulario de Edición
const editForm = ref({
  id: null,
  advisorId: null,
  sessionDate: '',
  topicsCovered: '',
  studentAgreements: '',
})
const editInitialAdvisor = ref(null)

function showAlert(message, type = 'info') {
  alertMessage.value = message
  alertType.value = type
  setTimeout(() => {
    if (alertMessage.value === message) {
      alertMessage.value = ''
    }
  }, 5000)
}

const MONTH_NAMES_ES = [
  'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
  'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'
]

function formatTecNMDate(iso) {
  if (!iso) return '—'
  const d = new Date(iso)
  if (isNaN(d.getTime())) return '—'
  const day = String(d.getDate()).padStart(2, '0')
  const month = MONTH_NAMES_ES[d.getMonth()]
  const year = d.getFullYear()
  return `${day}/${month}/${year}`
}

const selectedProjectText = computed(() => {
  if (!currentProject.value) {
    return isStudent.value ? 'Sin anteproyecto activo' : 'Seleccione un anteproyecto'
  }
  const title = currentProject.value.title || currentProject.value.name || 'Anteproyecto'
  const student = currentProject.value.studentName || currentProject.value.student_name || ''
  const ctrl = currentProject.value.studentControlNumber || currentProject.value.student_control_number || ''
  const studentInfo = student ? ` (Alumno: ${student}${ctrl ? ' - ' + ctrl : ''})` : ''
  return `${title}${studentInfo}`
})

const projectStatusBadgeClass = computed(() => {
  if (!currentProject.value) return 'tecnm-badge-neutral'
  const st = String(currentProject.value.status || '').toLowerCase()
  if (st === 'completed') return 'tecnm-badge-approved'
  if (st === 'in_progress') return 'tecnm-badge-pending'
  if (st === 'approved') return 'tecnm-badge-approved'
  if (st === 'pending' || st === 'under_review' || st === 'proposed') return 'tecnm-badge-pending'
  if (st === 'rejected' || st === 'cancelled') return 'tecnm-badge-rejected'
  return 'tecnm-badge-neutral'
})

const projectStatusLabel = computed(() => {
  if (!currentProject.value) return isStudent.value ? 'Sin anteproyecto' : 'No seleccionado'
  const st = String(currentProject.value.status || '').toLowerCase()
  if (st === 'completed') return 'Concluido / Acreditado'
  if (st === 'in_progress') return 'En Desarrollo'
  if (st === 'approved') return 'Aprobado'
  if (st === 'pending' || st === 'under_review' || st === 'proposed') return 'En Revisión'
  if (st === 'draft') return 'Borrador'
  if (st === 'rejected') return 'Con Observaciones'
  if (st === 'cancelled') return 'Cancelado'
  return st
})

async function initPage() {
  if (isStudent.value) {
    await resolveStudentProject()
  } else {
    await loadInitialProjectForStaff()
  }
}

async function resolveStudentProject() {
  isLoading.value = true
  errorMessage.value = ''
  try {
    const res = await apiClient.get('/v1/projects/me/current')
    if (res.data && res.data.id) {
      currentProject.value = res.data
      await loadSessions()
    } else {
      currentProject.value = null
      sessions.value = []
    }
  } catch (err) {
    currentProject.value = null
    sessions.value = []
    if (err.response?.status === 404) {
      // Sin anteproyecto
    } else {
      errorMessage.value = 'Error al consultar la bitácora de asesorías.'
    }
  } finally {
    isLoading.value = false
  }
}

async function loadInitialProjectForStaff() {
  isLoading.value = true
  errorMessage.value = ''
  try {
    const endpoint = isAdvisor.value
      ? '/v1/projects/advisor/me?pageSize=50'
      : '/v1/projects?pageSize=50'
    const res = await apiClient.get(endpoint)
    const rawData = res.data
    let list = Array.isArray(rawData)
      ? rawData
      : (rawData && Array.isArray(rawData.items) ? rawData.items : [])
    list = list.filter((p) => (p.status || '').toLowerCase() !== 'draft')

    if (list.length === 0) {
      errorMessage.value =
        'No se encontraron anteproyectos asignados. Utilice el botón "Buscar Anteproyecto" para seleccionar uno.'
      currentProject.value = null
      sessions.value = []
      return
    }

    await selectProject(list[0])
  } catch {
    errorMessage.value = 'Haga clic en "Buscar Anteproyecto" para cargar la bitácora.'
    currentProject.value = null
    sessions.value = []
  } finally {
    isLoading.value = false
  }
}

async function selectProject(project) {
  if (!project || !project.id) return
  currentProject.value = project
  pageNumber.value = 1
  errorMessage.value = ''
  await loadSessions()

  // Enriquecer datos de estudiante si faltan
  if (!project.studentName && !project.student_name) {
    try {
      const res = await apiClient.get(`/v1/projects/${project.id}`)
      if (res.data) {
        currentProject.value = { ...project, ...res.data }
      }
    } catch {}
  }
}

async function loadSessions() {
  if (!currentProject.value?.id) {
    sessions.value = []
    totalCount.value = 0
    totalPages.value = 0
    return
  }

  isLoading.value = true
  errorMessage.value = ''

  try {
    const params = {
      pageNumber: pageNumber.value,
      pageSize: pageSize.value,
      search: search.value.trim(),
      sortBy: sortBy.value,
      sortDir: sortDir.value,
      includeInactive: includeInactive.value,
    }

    const res = await apiClient.get(
      `/v1/evaluations/sessions/project/${currentProject.value.id}`,
      { params }
    )
    const data = res.data || {}
    sessions.value = data.items || []
    totalCount.value = data.totalCount || 0
    totalPages.value = data.totalPages || 0
  } catch (err) {
    errorMessage.value =
      err.response?.data?.message || 'Error al obtener la bitácora de asesorías.'
    sessions.value = []
    totalCount.value = 0
    totalPages.value = 0
  } finally {
    isLoading.value = false
  }
}

function openProjectPicker() {
  openGlobalSearch({
    initialSource: 'PROJECTS',
    onSelect: (item) => {
      if (item && item.id) {
        selectProject(item)
      }
    },
  })
}

function toggleSort(field) {
  if (sortBy.value === field) {
    sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
  } else {
    sortBy.value = field
    sortDir.value = 'asc'
  }
  pageNumber.value = 1
  loadSessions()
}

function changePage(page) {
  pageNumber.value = page
  loadSessions()
}

// Modal de Creación
function openCreateModal() {
  if (!currentProject.value?.id) {
    showAlert('Debe seleccionar un proyecto válido.', 'warning')
    return
  }
  createForm.value = {
    projectId: currentProject.value.id,
    advisorId: currentProject.value.advisorId || null,
    sessionDate: new Date().toISOString().split('T')[0],
    topicsCovered: '',
    studentAgreements: '',
  }
  initialAdvisor.value = currentProject.value.advisorId
    ? {
        id: currentProject.value.advisorId,
        fullName: currentProject.value.advisorName || 'Asesor Asignado',
      }
    : null
  isCreateModalOpen.value = true
}

function closeCreateModal() {
  isCreateModalOpen.value = false
}

async function handleCreateSubmit() {
  if (!currentProject.value?.id) {
    showAlert('Debe seleccionar un proyecto.', 'warning')
    return
  }
  if (isStaff.value && !createForm.value.advisorId) {
    showAlert('Debe seleccionar al asesor responsable de la sesión.', 'warning')
    return
  }
  if (!createForm.value.topicsCovered.trim()) {
    showAlert('Debe describir los temas o avances abordados.', 'warning')
    return
  }

  isSubmitting.value = true
  try {
    const payload = {
      projectId: currentProject.value.id,
      advisorId: createForm.value.advisorId || 0,
      sessionDate: createForm.value.sessionDate
        ? new Date(createForm.value.sessionDate).toISOString()
        : null,
      topicsCovered: createForm.value.topicsCovered.trim(),
      studentAgreements: createForm.value.studentAgreements.trim(),
    }

    await apiClient.post('/v1/evaluations/sessions', payload)
    showAlert('¡Sesión de asesoría registrada correctamente!', 'success')
    closeCreateModal()
    await loadSessions()
  } catch (err) {
    const msg = err.response?.data?.message || 'Error al guardar la asesoría.'
    showAlert(msg, 'danger')
  } finally {
    isSubmitting.value = false
  }
}

// Modal de Edición
function openEditModal(session) {
  editForm.value = {
    id: session.id,
    advisorId: session.advisorId || null,
    sessionDate: session.sessionDate
      ? new Date(session.sessionDate).toISOString().split('T')[0]
      : '',
    topicsCovered: session.topicsCovered || '',
    studentAgreements: session.studentAgreements || '',
  }
  editInitialAdvisor.value = session.advisorId
    ? { id: session.advisorId, fullName: session.advisorName || 'Asesor' }
    : null
  isEditModalOpen.value = true
}

function closeEditModal() {
  isEditModalOpen.value = false
}

async function handleEditSubmit() {
  if (!editForm.value.topicsCovered.trim()) {
    showAlert('Debe especificar los temas abordados.', 'warning')
    return
  }

  isSubmitting.value = true
  try {
    const payload = {
      advisorId: editForm.value.advisorId || 0,
      sessionDate: editForm.value.sessionDate
        ? new Date(editForm.value.sessionDate).toISOString()
        : null,
      topicsCovered: editForm.value.topicsCovered.trim(),
      studentAgreements: editForm.value.studentAgreements.trim(),
    }

    await apiClient.put(`/v1/evaluations/sessions/${editForm.value.id}`, payload)
    showAlert('Sesión de asesoría actualizada correctamente.', 'success')
    closeEditModal()
    await loadSessions()
  } catch (err) {
    const msg = err.response?.data?.message || 'Error al actualizar la sesión.'
    showAlert(msg, 'danger')
  } finally {
    isSubmitting.value = false
  }
}

async function handleDeleteSession(session) {
  const ok = await confirm({
    title: 'Eliminar Sesión de Asesoría',
    message: `¿Está seguro de que desea eliminar la sesión del día ${formatTecNMDate(session.sessionDate)}? Esta acción no se puede deshacer.`,
    okText: 'Eliminar',
    cancelText: 'Cancelar',
  })
  if (!ok) return

  try {
    await apiClient.delete(`/v1/evaluations/sessions/${session.id}`)
    showAlert('Sesión de asesoría eliminada correctamente.', 'success')
    await loadSessions()
  } catch (err) {
    const msg = err.response?.data?.message || 'Error al eliminar la sesión.'
    showAlert(msg, 'danger')
  }
}

function handleOpenAudit(session) {
  showAudit({
    title: `Auditoría — Asesoría #${session.id}`,
    item: {
      ...session,
      title: `Sesión de Asesoría #${session.id} (${currentProject.value?.title || 'Anteproyecto'})`,
    },
  })
}

async function handleExportPdf() {
  if (!currentProject.value?.id) return
  const params = new URLSearchParams({
    search: search.value,
    sortBy: sortBy.value,
    sortDir: sortDir.value,
    includeInactive: includeInactive.value,
    projectId: currentProject.value.id,
  })
  window.open(`/api/v1/evaluations/sessions/export?${params}`, '_blank')
}

onMounted(() => {
  initPage()
})
</script>

<template>
  <div>
    <!-- Notificación Flotante Superior Derecha -->
    <div
      v-if="alertMessage"
      id="alertContainer"
      class="tecnm-alert"
      :class="`tecnm-alert-${alertType}`"
      role="alert"
    >
      <span>{{ alertMessage }}</span>
      <button
        type="button"
        class="tecnm-alert-close"
        aria-label="Cerrar"
        @click="alertMessage = ''"
      >
        &times;
      </button>
    </div>

    <!-- Barra de Acciones y Título -->
    <div class="tecnm-actions-bar">
      <div>
        <h1 class="tecnm-page-title">Bitácora de Asesorías de Residencia</h1>
        <p class="tecnm-page-subtitle">Registro de reuniones, temas abordados y compromisos del estudiante</p>
      </div>
      <div class="tecnm-page-actions">
        <button
          v-if="!isStudent"
          type="button"
          class="tecnm-btn tecnm-btn-secondary"
          @click="openProjectPicker"
        >
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
          </svg>
          <span>Abrir búsqueda</span>
        </button>
        <span v-if="!isStudent" class="tecnm-page-actions-divider" aria-hidden="true"></span>
        <button
          v-if="canRecordSession"
          id="openAdvisoryModalBtn"
          type="button"
          class="tecnm-btn tecnm-btn-primary"
          :disabled="!currentProject?.id"
          @click="openCreateModal"
        >
          + Registrar Sesión de Asesoría
        </button>
      </div>
    </div>

    <!-- Banner Contextual según Estado del Proyecto -->
    <div v-if="isProjectCompleted" class="tecnm-alert tecnm-alert-success" role="alert" style="margin-bottom: 1rem;">
      <span><strong>✓ Residencia Profesional Concluida:</strong> La bitácora histórica de sesiones de asesoría se encuentra en modo solo lectura.</span>
    </div>
    <div v-else-if="isProjectPending" class="tecnm-alert tecnm-alert-warning" role="alert" style="margin-bottom: 1rem;">
      <span><strong>⏳ Anteproyecto en Dictamen:</strong> Las asesorías formales se registrarán una vez que el anteproyecto sea aprobado.</span>
    </div>
    <div v-else-if="isProjectDraft" class="tecnm-alert tecnm-alert-info" role="alert" style="margin-bottom: 1rem;">
      <span><strong>📝 Anteproyecto en Borrador:</strong> Envía tu solicitud a revisión en el módulo de <router-link to="/projects/proposal"><strong>Solicitud de Anteproyecto</strong></router-link>.</span>
    </div>
    <div v-else-if="isProjectRejected" class="tecnm-alert tecnm-alert-danger" role="alert" style="margin-bottom: 1rem;">
      <span><strong>⚠️ Anteproyecto con Observaciones:</strong> Realiza las correcciones solicitadas en el módulo de <router-link to="/projects/proposal"><strong>Solicitud de Anteproyecto</strong></router-link>.</span>
    </div>
    <div v-else-if="!currentProject && !isLoading && isStudent" class="tecnm-alert tecnm-alert-info" role="alert" style="margin-bottom: 1rem; display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 0.5rem;">
      <span><strong>ℹ️ Sin Anteproyecto Registrado:</strong> Aún no cuentas con un anteproyecto para consultar la bitácora de asesorías.</span>
      <router-link to="/projects/proposal" class="tecnm-btn tecnm-btn-primary tecnm-btn-sm">
        + Registrar Solicitud de Anteproyecto
      </router-link>
    </div>

    <!-- Tarjeta Principal con Historial de Sesiones -->
    <div class="tecnm-card">
      <div class="tecnm-card-header">
        <h3 class="tecnm-card-title">Historial de Sesiones Registradas</h3>
      </div>

      <div class="tecnm-card-toolbar">
        <div id="projectSearchContainer" class="tecnm-d-flex tecnm-align-center tecnm-gap-2">
          <button
            v-if="!isStudent"
            id="searchProjectBtn"
            type="button"
            class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
            @click="openProjectPicker"
          >
            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
            </svg>
            <span>Buscar Anteproyecto</span>
          </button>
          <span id="selectedProjectBadge" class="tecnm-badge" :class="projectStatusBadgeClass">
            {{ selectedProjectText }} — [{{ projectStatusLabel }}]
          </span>
        </div>

        <div class="tecnm-toolbar-actions">
          <label id="sessionsInactiveLabel" class="tecnm-switch-label">
            <span class="tecnm-switch">
              <input
                id="sessionsIncludeInactiveToggle"
                v-model="includeInactive"
                type="checkbox"
                @change="loadSessions"
              />
              <span class="tecnm-switch-slider"></span>
            </span>
            Mostrar inactivos
          </label>
          <button
            v-if="isStaff"
            id="exportSessionsBtn"
            type="button"
            class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
            :disabled="!currentProject?.id || sessions.length === 0"
            @click="handleExportPdf"
          >
            Exportar PDF
          </button>
        </div>
      </div>

      <div class="tecnm-card-body">
        <div class="tecnm-table-responsive">
          <table id="sessionsTable" class="tecnm-table tecnm-table-striped">
            <thead>
              <tr>
                <th
                  class="tecnm-th-sortable"
                  @click="handleSort('sessionDate')"
                >
                  Fecha
                  <span v-if="sortBy === 'sessionDate'">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
                </th>
                <th
                  class="tecnm-th-sortable"
                  @click="handleSort('studentName')"
                >
                  Estudiante
                  <span v-if="sortBy === 'studentName'">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
                </th>
                <th
                  class="tecnm-th-sortable"
                  @click="handleSort('advisorName')"
                >
                  Asesor
                  <span v-if="sortBy === 'advisorName'">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
                </th>
                <th
                  class="tecnm-th-sortable"
                  @click="handleSort('topicsCovered')"
                >
                  Temas Abordados
                  <span v-if="sortBy === 'topicsCovered'">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
                </th>
                <th
                  class="tecnm-th-sortable"
                  @click="handleSort('studentAgreements')"
                >
                  Acuerdos y Compromisos
                  <span v-if="sortBy === 'studentAgreements'">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
                </th>
                <th class="tecnm-th-actions">Acciones</th>
              </tr>
            </thead>
            <tbody id="sessionsTableBody">
              <tr v-if="isLoading">
                <td colspan="6" class="tecnm-table-empty">
                  Cargando sesiones de asesoría...
                </td>
              </tr>
              <tr v-else-if="errorMessage">
                <td colspan="6" class="tecnm-table-empty tecnm-text-danger">
                  {{ errorMessage }}
                </td>
              </tr>
              <tr v-else-if="!currentProject">
                <td colspan="6" class="tecnm-table-empty">
                  <p style="margin-bottom: 0.5rem;">No tienes un anteproyecto registrado para consultar las asesorías.</p>
                  <router-link v-if="isStudent" to="/projects/proposal" class="tecnm-btn tecnm-btn-primary tecnm-btn-sm">
                    Registrar Solicitud de Anteproyecto
                  </router-link>
                </td>
              </tr>
              <tr v-else-if="sessions.length === 0">
                <td colspan="6" class="tecnm-table-empty">
                  <span v-if="isProjectCompleted">No hay sesiones de asesoría registradas en este proyecto concluido.</span>
                  <span v-else-if="isProjectPending">Las sesiones de asesoría se habilitarán una vez dictaminado favorablemente el anteproyecto.</span>
                  <span v-else>No hay sesiones de asesoría registradas en la base de datos para este proyecto.</span>
                </td>
              </tr>
              <tr
                v-for="s in sessions"
                v-else
                :key="s.id"
              >
                <td><strong>{{ formatTecNMDate(s.sessionDate) }}</strong></td>
                <td>{{ s.studentName || 'Estudiante' }}</td>
                <td>{{ s.advisorName || 'Asesor' }}</td>
                <td>{{ s.topicsCovered }}</td>
                <td>{{ s.studentAgreements || 'N/A' }}</td>
                <td>
                  <div class="tecnm-row-actions">
                    <button
                      v-if="isStaff"
                      type="button"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                      @click="openEditModal(s)"
                    >
                      Editar
                    </button>
                    <button
                      type="button"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                      @click="handleOpenAudit(s)"
                    >
                      Auditoría
                    </button>
                    <button
                      v-if="isStaff"
                      type="button"
                      class="tecnm-btn tecnm-btn-danger tecnm-btn-sm"
                      @click="handleDeleteSession(s)"
                    >
                      Eliminar
                    </button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <TecnmPagination
          v-if="totalPages > 1"
          :current-page="pageNumber"
          :total-pages="totalPages"
          :total-count="totalCount"
          :page-size="pageSize"
          @page-change="changePage"
        />
      </div>
    </div>

    <!-- Modal Registrar Sesión de Asesoría -->
    <div
      id="createAdvisoryModal"
      class="modal-backdrop"
      :class="{ active: isCreateModalOpen }"
      aria-modal="true"
      role="dialog"
    >
      <div class="modal-card">
        <div class="tecnm-modal-header">
          <h3 class="tecnm-modal-title">Registrar Sesión de Asesoría</h3>
          <button
            id="closeAdvisoryModalBtn"
            type="button"
            class="tecnm-modal-close"
            aria-label="Cerrar"
            @click="closeCreateModal"
          >
            &times;
          </button>
        </div>

        <form id="advisoryForm" @submit.prevent="handleCreateSubmit">
          <div class="tecnm-form-group">
            <label class="tecnm-label">Proyecto / Alumno Seleccionado *</label>
            <div class="tecnm-d-flex tecnm-align-center tecnm-gap-2">
              <span
                id="modalSelectedProjectBadge"
                class="tecnm-badge tecnm-badge-info"
                style="font-size: 0.875rem; padding: 0.5rem 0.75rem; flex-grow: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 100%;"
              >
                {{ selectedProjectText }}
              </span>
              <button
                v-if="!isStudent"
                id="selectProjectForAdvisoryBtn"
                type="button"
                class="tecnm-btn tecnm-btn-secondary"
                style="white-space: nowrap;"
                @click="openProjectPicker"
              >
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
                </svg>
                <span>Buscar Alumno / Anteproyecto</span>
              </button>
            </div>
          </div>

          <div v-if="!isAdvisor" id="advisorFormGroup" class="tecnm-form-group">
            <label for="advisorId" class="tecnm-label">Asesor Responsable *</label>
            <div id="advisorAutocompleteWrapper">
              <TecnmAutocomplete
                v-model="createForm.advisorId"
                endpoint="/v1/advisors"
                global-search-source="ADVISORS"
                placeholder="Buscar asesor responsable..."
                :initial-item="initialAdvisor"
              />
            </div>
          </div>

          <div class="tecnm-form-group">
            <label for="sessionDate" class="tecnm-label">Fecha de Sesión *</label>
            <input
              id="sessionDate"
              v-model="createForm.sessionDate"
              type="date"
              class="tecnm-form-control"
              required
            />
          </div>

          <div class="tecnm-form-group">
            <label for="topicsCovered" class="tecnm-label">Temas o Avances Abordados *</label>
            <textarea
              id="topicsCovered"
              v-model="createForm.topicsCovered"
              class="tecnm-form-control"
              rows="3"
              placeholder="Describa los puntos revisados durante la sesión..."
              required
            ></textarea>
          </div>

          <div class="tecnm-form-group">
            <label for="studentAgreements" class="tecnm-label">Acuerdos y Compromisos del Estudiante</label>
            <textarea
              id="studentAgreements"
              v-model="createForm.studentAgreements"
              class="tecnm-form-control"
              rows="2"
              placeholder="Compromisos o entregables acordados para la siguiente sesión..."
            ></textarea>
          </div>

          <div class="tecnm-modal-footer">
            <button
              id="cancelAdvisoryModalBtn"
              type="button"
              class="tecnm-btn tecnm-btn-secondary"
              @click="closeCreateModal"
            >
              Cancelar
            </button>
            <button
              type="submit"
              class="tecnm-btn tecnm-btn-primary"
              :disabled="isSubmitting"
            >
              {{ isSubmitting ? 'Guardando...' : 'Guardar Sesión' }}
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Modal Editar Sesión de Asesoría -->
    <div
      id="editAdvisoryModal"
      class="modal-backdrop"
      :class="{ active: isEditModalOpen }"
      aria-modal="true"
      role="dialog"
    >
      <div class="modal-card">
        <div class="tecnm-modal-header">
          <h3 class="tecnm-modal-title">Editar Sesión de Asesoría</h3>
          <button
            id="closeEditAdvisoryModalBtn"
            type="button"
            class="tecnm-modal-close"
            aria-label="Cerrar"
            @click="closeEditModal"
          >
            &times;
          </button>
        </div>

        <form id="editAdvisoryForm" @submit.prevent="handleEditSubmit">
          <div v-if="isStaff" id="editAdvisorFormGroup" class="tecnm-form-group">
            <label for="editAdvisorId" class="tecnm-label">Asesor Responsable *</label>
            <div id="editAdvisorAutocompleteWrapper">
              <TecnmAutocomplete
                v-model="editForm.advisorId"
                endpoint="/v1/advisors"
                global-search-source="ADVISORS"
                placeholder="Buscar asesor responsable..."
                :initial-item="editInitialAdvisor"
              />
            </div>
          </div>

          <div class="tecnm-form-group">
            <label for="editSessionDate" class="tecnm-label">Fecha de Sesión *</label>
            <input
              id="editSessionDate"
              v-model="editForm.sessionDate"
              type="date"
              class="tecnm-form-control"
              required
            />
          </div>

          <div class="tecnm-form-group">
            <label for="editTopicsCovered" class="tecnm-label">Temas o Avances Abordados *</label>
            <textarea
              id="editTopicsCovered"
              v-model="editForm.topicsCovered"
              class="tecnm-form-control"
              rows="3"
              required
            ></textarea>
          </div>

          <div class="tecnm-form-group">
            <label for="editStudentAgreements" class="tecnm-label">Acuerdos y Compromisos del Estudiante</label>
            <textarea
              id="editStudentAgreements"
              v-model="editForm.studentAgreements"
              class="tecnm-form-control"
              rows="2"
            ></textarea>
          </div>

          <div class="tecnm-modal-footer">
            <button
              id="cancelEditAdvisoryModalBtn"
              type="button"
              class="tecnm-btn tecnm-btn-secondary"
              @click="closeEditModal"
            >
              Cancelar
            </button>
            <button
              type="submit"
              class="tecnm-btn tecnm-btn-primary"
              :disabled="isSubmitting"
            >
              {{ isSubmitting ? 'Guardando...' : 'Guardar Cambios' }}
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

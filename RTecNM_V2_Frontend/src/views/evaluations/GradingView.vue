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
const evaluations = ref([])
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

const canGrade = computed(() => {
  if (authStore.isReadOnly) return false
  if (!authStore.hasRole('admin', 'departmenthead', 'advisor')) return false
  if (!currentProject.value?.id) return false
  if (isAdvisor.value && isProjectReadOnly.value) return false
  return true
})

// Paginación
const pageNumber = ref(1)
const pageSize = ref(10)
const totalCount = ref(0)
const totalPages = ref(0)

// Modales
const isCreateModalOpen = ref(false)
const isEditModalOpen = ref(false)
const isSubmitting = ref(false)

// Formulario de Alta
const createForm = ref({
  projectId: null,
  evaluatorId: null,
  evaluationPeriod: 'partial_1',
  score: null,
  feedback: '',
})
const createInitialProject = ref(null)
const createInitialAdvisor = ref(null)

// Formulario de Edición
const editForm = ref({
  id: null,
  projectId: null,
  evaluatorId: null,
  evaluationPeriod: '',
  score: null,
  feedback: '',
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

function formatPeriod(p) {
  const period = String(p || '').toLowerCase()
  if (period === 'partial_1' || period === 'parcial_1') return 'Primer Reporte Parcial'
  if (period === 'partial_2' || period === 'parcial_2') return 'Segundo Reporte Parcial'
  if (period === 'final') return 'Reporte Final'
  return p || 'Evaluación'
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
      await loadEvaluations()
    } else {
      currentProject.value = null
      evaluations.value = []
    }
  } catch (err) {
    currentProject.value = null
    evaluations.value = []
    if (err.response?.status === 404) {
      // Sin anteproyecto
    } else {
      errorMessage.value = 'Error al consultar las calificaciones del residente.'
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
      evaluations.value = []
      return
    }

    await selectProject(list[0])
  } catch {
    errorMessage.value = 'Haga clic en "Buscar Anteproyecto" para cargar calificaciones.'
    currentProject.value = null
    evaluations.value = []
  } finally {
    isLoading.value = false
  }
}

async function selectProject(project) {
  if (!project || !project.id) return
  currentProject.value = project
  pageNumber.value = 1
  errorMessage.value = ''
  await loadEvaluations()

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

async function loadEvaluations() {
  if (!currentProject.value?.id) {
    evaluations.value = []
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
    }

    const res = await apiClient.get(
      `/v1/evaluations/project/${currentProject.value.id}`,
      { params }
    )
    const data = res.data || {}
    evaluations.value = data.items || []
    totalCount.value = data.totalCount || 0
    totalPages.value = data.totalPages || 0
  } catch (err) {
    errorMessage.value =
      err.response?.data?.message || 'Error al obtener las calificaciones.'
    evaluations.value = []
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

const sortBy = ref('evaluationPeriod')
const sortDir = ref('asc')

function handleSort(field) {
  if (sortBy.value === field) {
    sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
  } else {
    sortBy.value = field
    sortDir.value = 'asc'
  }
}

const sortedEvaluations = computed(() => {
  let list = [...evaluations.value]
  const field = sortBy.value
  const dir = sortDir.value === 'asc' ? 1 : -1
  return list.sort((a, b) => {
    let valA = a[field] ?? ''
    let valB = b[field] ?? ''
    if (typeof valA === 'string') valA = valA.toLowerCase()
    if (typeof valB === 'string') valB = valB.toLowerCase()
    if (valA < valB) return -1 * dir
    if (valA > valB) return 1 * dir
    return 0
  })
})

function changePage(page) {
  pageNumber.value = page
  loadEvaluations()
}

// Modal Registrar Calificación
function openCreateModal() {
  createForm.value = {
    projectId: currentProject.value?.id || null,
    evaluatorId: currentProject.value?.advisorId || null,
    evaluationPeriod: 'partial_1',
    score: null,
    feedback: '',
  }
  createInitialProject.value = currentProject.value
    ? { id: currentProject.value.id, title: currentProject.value.title || 'Anteproyecto' }
    : null
  createInitialAdvisor.value = currentProject.value?.advisorId
    ? { id: currentProject.value.advisorId, fullName: currentProject.value.advisorName || 'Asesor' }
    : null
  isCreateModalOpen.value = true
}

function closeCreateModal() {
  isCreateModalOpen.value = false
}

async function handleCreateSubmit() {
  const projectId = createForm.value.projectId || currentProject.value?.id
  if (!projectId) {
    showAlert('Debe seleccionar un proyecto válido.', 'warning')
    return
  }
  if (isStaff.value && !createForm.value.evaluatorId) {
    showAlert('Debe seleccionar al asesor o evaluador responsable.', 'warning')
    return
  }
  const score = parseFloat(createForm.value.score)
  if (isNaN(score) || score < 0 || score > 100) {
    showAlert('La calificación debe ser un valor numérico entre 0 y 100.', 'warning')
    return
  }

  isSubmitting.value = true
  try {
    const payload = {
      projectId,
      evaluatorId: createForm.value.evaluatorId || 0,
      evaluationPeriod: createForm.value.evaluationPeriod,
      score,
      feedback: createForm.value.feedback.trim(),
    }

    await apiClient.post('/v1/evaluations', payload)
    showAlert('¡Calificación guardada correctamente!', 'success')
    closeCreateModal()
    await loadEvaluations()
  } catch (err) {
    const msg = err.response?.data?.message || 'Error al registrar la calificación.'
    showAlert(msg, 'danger')
  } finally {
    isSubmitting.value = false
  }
}

// Modal Editar Calificación
function openEditModal(evaluation) {
  editForm.value = {
    id: evaluation.id,
    projectId: evaluation.projectId || currentProject.value?.id,
    evaluatorId: evaluation.evaluatorId || null,
    evaluationPeriod: evaluation.evaluationPeriod,
    score: evaluation.score,
    feedback: evaluation.feedback || '',
  }
  editInitialAdvisor.value = evaluation.evaluatorId
    ? { id: evaluation.evaluatorId, fullName: evaluation.evaluatorName || 'Asesor' }
    : null
  isEditModalOpen.value = true
}

function closeEditModal() {
  isEditModalOpen.value = false
}

async function handleEditSubmit() {
  const score = parseFloat(editForm.value.score)
  if (isNaN(score) || score < 0 || score > 100) {
    showAlert('La calificación debe ser un valor numérico entre 0 y 100.', 'warning')
    return
  }

  isSubmitting.value = true
  try {
    const payload = {
      projectId: editForm.value.projectId,
      evaluatorId: editForm.value.evaluatorId || 0,
      evaluationPeriod: editForm.value.evaluationPeriod,
      score,
      feedback: editForm.value.feedback.trim(),
    }

    await apiClient.post('/v1/evaluations', payload)
    showAlert('Calificación actualizada correctamente.', 'success')
    closeEditModal()
    await loadEvaluations()
  } catch (err) {
    const msg = err.response?.data?.message || 'Error al actualizar la calificación.'
    showAlert(msg, 'danger')
  } finally {
    isSubmitting.value = false
  }
}

async function handleDeleteEvaluation(evaluation) {
  const ok = await confirm({
    title: 'Eliminar Calificación',
    message: `¿Está seguro de eliminar la calificación de ${formatPeriod(evaluation.evaluationPeriod)} (${evaluation.score}/100)? Esta acción no se puede deshacer.`,
    okText: 'Eliminar',
    cancelText: 'Cancelar',
  })
  if (!ok) return

  try {
    await apiClient.delete(`/v1/evaluations/${evaluation.id}`)
    showAlert('Calificación eliminada correctamente.', 'success')
    await loadEvaluations()
  } catch (err) {
    const msg = err.response?.data?.message || 'Error al eliminar la calificación.'
    showAlert(msg, 'danger')
  }
}

function handleOpenAudit(evaluation) {
  showAudit({
    title: `Auditoría — Evaluación #${evaluation.id}`,
    item: {
      ...evaluation,
      title: `${formatPeriod(evaluation.evaluationPeriod)} - Calificación: ${evaluation.score}`,
    },
  })
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

    <!-- Barra de Título y Acciones -->
    <div class="tecnm-actions-bar">
      <div>
        <h1 class="tecnm-page-title">Evaluación y Calificaciones del Residente</h1>
        <p class="tecnm-page-subtitle">Captura de evaluaciones parciales y reporte final</p>
      </div>
      <button
        v-if="canGrade"
        id="openGradingModalBtn"
        type="button"
        class="tecnm-btn tecnm-btn-primary"
        @click="openCreateModal"
      >
        + Registrar Calificación
      </button>
    </div>

    <!-- Banner Contextual según Estado del Proyecto -->
    <div v-if="isProjectCompleted" class="tecnm-alert tecnm-alert-success" role="alert" style="margin-bottom: 1rem;">
      <span><strong>✓ Residencia Profesional Concluida y Acreditada:</strong> A continuación se muestran las calificaciones oficiales finales registradas en el sistema.</span>
    </div>
    <div v-else-if="isProjectPending" class="tecnm-alert tecnm-alert-warning" role="alert" style="margin-bottom: 1rem;">
      <span><strong>⏳ Anteproyecto en Dictamen:</strong> Las evaluaciones parciales se habilitarán una vez que el anteproyecto sea aprobado y comience el período operativo.</span>
    </div>
    <div v-else-if="isProjectDraft" class="tecnm-alert tecnm-alert-info" role="alert" style="margin-bottom: 1rem;">
      <span><strong>📝 Anteproyecto en Borrador:</strong> Envía tu solicitud a revisión en el módulo de <router-link to="/projects/proposal"><strong>Solicitud de Anteproyecto</strong></router-link>.</span>
    </div>
    <div v-else-if="isProjectRejected" class="tecnm-alert tecnm-alert-danger" role="alert" style="margin-bottom: 1rem;">
      <span><strong>⚠️ Anteproyecto con Observaciones:</strong> Realiza las correcciones solicitadas en el módulo de <router-link to="/projects/proposal"><strong>Solicitud de Anteproyecto</strong></router-link>.</span>
    </div>
    <div v-else-if="!currentProject && !isLoading && isStudent" class="tecnm-alert tecnm-alert-info" role="alert" style="margin-bottom: 1rem; display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 0.5rem;">
      <span><strong>ℹ️ Sin Anteproyecto Registrado:</strong> Aún no cuentas con un anteproyecto para consultar calificaciones.</span>
      <router-link to="/projects/proposal" class="tecnm-btn tecnm-btn-primary tecnm-btn-sm">
        + Registrar Solicitud de Anteproyecto
      </router-link>
    </div>

    <!-- Tarjeta Principal con Lista de Calificaciones -->
    <div class="tecnm-card">
      <div class="tecnm-card-header">
        <h3 class="tecnm-card-title">Calificaciones Registradas en Base de Datos</h3>
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
      </div>

      <div class="tecnm-card-body">
        <div class="tecnm-table-responsive">
          <table class="tecnm-table tecnm-table-striped">
            <thead>
              <tr>
                <th class="tecnm-th-sortable" @click="handleSort('evaluationPeriod')">
                  Período Evaluado
                  <span class="tecnm-sort-icon" :class="{ active: sortBy === 'evaluationPeriod' }">
                    {{ sortBy === 'evaluationPeriod' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th class="tecnm-th-sortable" @click="handleSort('score')">
                  Calificación
                  <span class="tecnm-sort-icon" :class="{ active: sortBy === 'score' }">
                    {{ sortBy === 'score' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th class="tecnm-th-sortable" @click="handleSort('studentName')">
                  Estudiante
                  <span class="tecnm-sort-icon" :class="{ active: sortBy === 'studentName' }">
                    {{ sortBy === 'studentName' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th class="tecnm-th-sortable" @click="handleSort('feedback')">
                  Observaciones
                  <span class="tecnm-sort-icon" :class="{ active: sortBy === 'feedback' }">
                    {{ sortBy === 'feedback' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th class="tecnm-th-sortable" @click="handleSort('createdAt')">
                  Fecha de Registro
                  <span class="tecnm-sort-icon" :class="{ active: sortBy === 'createdAt' }">
                    {{ sortBy === 'createdAt' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th class="tecnm-th-actions">Acciones</th>
              </tr>
            </thead>
            <tbody id="evaluationsTableBody">
              <tr v-if="isLoading">
                <td colspan="6" class="tecnm-table-empty">
                  Cargando evaluaciones...
                </td>
              </tr>
              <tr v-else-if="errorMessage">
                <td colspan="6" class="tecnm-table-empty tecnm-text-danger">
                  {{ errorMessage }}
                </td>
              </tr>
              <tr v-else-if="!currentProject">
                <td colspan="6" class="tecnm-table-empty">
                  <p style="margin-bottom: 0.5rem;">No tienes un anteproyecto registrado para consultar calificaciones.</p>
                  <router-link v-if="isStudent" to="/projects/proposal" class="tecnm-btn tecnm-btn-primary tecnm-btn-sm">
                    Registrar Solicitud de Anteproyecto
                  </router-link>
                </td>
              </tr>
              <tr v-else-if="sortedEvaluations.length === 0">
                <td colspan="6" class="tecnm-table-empty">
                  No hay calificaciones registradas para este anteproyecto.
                </td>
              </tr>
              <tr
                v-for="e in evaluations"
                v-else
                :key="e.id"
              >
                <td>
                  <span class="tecnm-badge tecnm-badge-info">
                    {{ formatPeriod(e.evaluationPeriod) }}
                  </span>
                </td>
                <td><strong>{{ e.score }}</strong> / 100</td>
                <td>{{ e.studentName || currentProject?.studentName || 'Estudiante' }}</td>
                <td>{{ e.feedback || 'Sin observaciones' }}</td>
                <td>{{ formatTecNMDate(e.createdAt) }}</td>
                <td>
                  <div class="tecnm-row-actions">
                    <button
                      v-if="isStaff || isAdvisor"
                      type="button"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                      @click="openEditModal(e)"
                    >
                      Editar
                    </button>
                    <button
                      type="button"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                      @click="handleOpenAudit(e)"
                    >
                      Auditoría
                    </button>
                    <button
                      v-if="isStaff"
                      type="button"
                      class="tecnm-btn tecnm-btn-danger tecnm-btn-sm"
                      @click="handleDeleteEvaluation(e)"
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

    <!-- Modal Registrar Calificación -->
    <div
      id="createGradingModal"
      class="modal-backdrop"
      :class="{ active: isCreateModalOpen }"
      aria-modal="true"
      role="dialog"
    >
      <div class="modal-card">
        <div class="tecnm-modal-header">
          <h3 class="tecnm-modal-title">Registrar Calificación de Residencia</h3>
          <button
            id="closeGradingModalBtn"
            type="button"
            class="tecnm-modal-close"
            aria-label="Cerrar"
            @click="closeCreateModal"
          >
            &times;
          </button>
        </div>

        <form id="gradingForm" @submit.prevent="handleCreateSubmit">
          <div class="tecnm-form-group">
            <label for="modalGradeProjectId" class="tecnm-label">Proyecto Seleccionado *</label>
            <div id="gradeProjectAutocompleteWrapper">
              <TecnmAutocomplete
                v-model="createForm.projectId"
                endpoint="/v1/projects"
                global-search-source="PROJECTS"
                placeholder="Buscar anteproyecto por título o estudiante..."
                :initial-item="createInitialProject"
              />
            </div>
          </div>

          <div v-if="!isAdvisor" id="evaluatorFormGroup" class="tecnm-form-group">
            <label for="evaluatorId" class="tecnm-label">Evaluador / Asesor *</label>
            <div id="evaluatorAutocompleteWrapper">
              <TecnmAutocomplete
                v-model="createForm.evaluatorId"
                endpoint="/v1/advisors"
                global-search-source="ADVISORS"
                placeholder="Buscar evaluador / asesor..."
                :initial-item="createInitialAdvisor"
              />
            </div>
          </div>

          <div class="tecnm-form-group">
            <label for="evaluationPeriod" class="tecnm-label">Período a Evaluar *</label>
            <select
              id="evaluationPeriod"
              v-model="createForm.evaluationPeriod"
              class="tecnm-form-control"
              required
            >
              <option value="partial_1">Primer Reporte Parcial</option>
              <option value="partial_2">Segundo Reporte Parcial</option>
              <option value="final">Reporte Final</option>
            </select>
          </div>

          <div class="tecnm-form-group">
            <label for="score" class="tecnm-label">Calificación (0 - 100) *</label>
            <input
              id="score"
              v-model="createForm.score"
              type="number"
              class="tecnm-form-control"
              min="0"
              max="100"
              step="0.01"
              placeholder="Ej. 95.00"
              required
            />
          </div>

          <div class="tecnm-form-group">
            <label for="feedback" class="tecnm-label">Observaciones y Retroalimentación</label>
            <textarea
              id="feedback"
              v-model="createForm.feedback"
              class="tecnm-form-control"
              rows="3"
              placeholder="Ingrese comentarios y observaciones sobre el reporte evaluado..."
            ></textarea>
          </div>

          <div class="tecnm-modal-footer">
            <button
              id="cancelGradingModalBtn"
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
              {{ isSubmitting ? 'Guardando...' : 'Guardar Calificación' }}
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Modal Editar Calificación -->
    <div
      id="editGradingModal"
      class="modal-backdrop"
      :class="{ active: isEditModalOpen }"
      aria-modal="true"
      role="dialog"
    >
      <div class="modal-card">
        <div class="tecnm-modal-header">
          <h3 class="tecnm-modal-title">Editar Calificación</h3>
          <button
            id="closeEditGradingModalBtn"
            type="button"
            class="tecnm-modal-close"
            aria-label="Cerrar"
            @click="closeEditModal"
          >
            &times;
          </button>
        </div>

        <form id="editGradingForm" @submit.prevent="handleEditSubmit">
          <div class="tecnm-form-group">
            <label class="tecnm-label">Período Evaluado</label>
            <input
              type="text"
              class="tecnm-form-control"
              :value="formatPeriod(editForm.evaluationPeriod)"
              disabled
            />
          </div>

          <div v-if="isStaff" id="editEvaluatorFormGroup" class="tecnm-form-group">
            <label for="editEvaluatorId" class="tecnm-label">Evaluador / Asesor *</label>
            <div id="editEvaluatorAutocompleteWrapper">
              <TecnmAutocomplete
                v-model="editForm.evaluatorId"
                endpoint="/v1/advisors"
                global-search-source="ADVISORS"
                placeholder="Buscar evaluador / asesor..."
                :initial-item="editInitialAdvisor"
              />
            </div>
          </div>

          <div class="tecnm-form-group">
            <label for="editScore" class="tecnm-label">Calificación (0 - 100) *</label>
            <input
              id="editScore"
              v-model="editForm.score"
              type="number"
              class="tecnm-form-control"
              min="0"
              max="100"
              step="0.01"
              required
            />
          </div>

          <div class="tecnm-form-group">
            <label for="editFeedback" class="tecnm-label">Observaciones y Retroalimentación</label>
            <textarea
              id="editFeedback"
              v-model="editForm.feedback"
              class="tecnm-form-control"
              rows="3"
            ></textarea>
          </div>

          <div class="tecnm-modal-footer">
            <button
              id="cancelEditGradingModalBtn"
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

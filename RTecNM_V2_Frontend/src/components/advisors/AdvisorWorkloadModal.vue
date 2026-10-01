<script setup>
import { ref, watch, computed } from 'vue'
import { useRouter } from 'vue-router'
import apiClient from '@/services/api'
import TecnmBadge from '@/components/common/TecnmBadge.vue'
import { useAuthStore } from '@/stores/auth'
import { useConfirm } from '@/composables/useConfirm'

const props = defineProps({
  modelValue: {
    type: Boolean,
    default: false,
  },
  advisorId: {
    type: [Number, String],
    default: null,
  },
})

const emit = defineEmits(['update:modelValue', 'close', 'updated'])

const router = useRouter()
const authStore = useAuthStore()
const { confirm } = useConfirm()

const isLoading = ref(false)
const errorMessage = ref('')
const advisorData = ref(null)

// Notificaciones internas del modal
const alertMessage = ref('')
const alertType = ref('success')
let alertTimer = null

function showAlert(msg, type = 'success') {
  alertMessage.value = msg
  alertType.value = type
  clearTimeout(alertTimer)
  alertTimer = setTimeout(() => {
    alertMessage.value = ''
  }, 4500)
}

const canManageAssignments = computed(() => {
  return authStore.isAdmin || authStore.isCareerHead || authStore.hasPermission('projects.advisor.assign')
})

const advisorInitials = computed(() => {
  if (!advisorData.value?.fullName) return 'DC'
  return advisorData.value.fullName
    .trim()
    .split(/\s+/)
    .map((p) => p[0])
    .slice(0, 2)
    .join('')
    .toUpperCase()
})

async function fetchAdvisorDetails(id) {
  if (!id) return
  isLoading.value = true
  errorMessage.value = ''

  try {
    const res = await apiClient.get(`/v1/advisors/${id}/residents`)
    advisorData.value = res.data
  } catch (err) {
    console.error('Error al cargar expediente del asesor:', err)
    errorMessage.value =
      err.response?.data?.message || 'No fue posible consultar los residentes asignados a este asesor.'
  } finally {
    isLoading.value = false
  }
}



function closeModal() {
  emit('update:modelValue', false)
  emit('close')
}

function goToAssignments() {
  closeModal()
  router.push('/advisors/assignments')
}

function goToReview() {
  closeModal()
  router.push('/projects/review')
}

// ---------------------------------------------------------------------------
// 1. Quitar / Desasignar Alumno
// ---------------------------------------------------------------------------
const processingStudentId = ref(null)

async function handleUnassignResident(resident) {
  const confirmed = await confirm({
    title: 'Desasignar Residente',
    message: `¿Desea retirar a "${resident.fullName}" (${resident.controlNumber}) de la lista de residentes de este asesor?`,
    okText: 'Quitar Residente',
    cancelText: 'Cancelar',
  })

  if (!confirmed) return

  processingStudentId.value = resident.studentId
  try {
    await apiClient.delete(`/v1/students/${resident.studentId}/advisor`)
    showAlert(`Residente "${resident.fullName}" desasignado correctamente.`, 'success')
    await fetchAdvisorDetails(props.advisorId)
    emit('updated')
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al desasignar el residente.', 'danger')
  } finally {
    processingStudentId.value = null
  }
}

// ---------------------------------------------------------------------------
// 2. Cambiar / Reasignar a Otro Asesor
// ---------------------------------------------------------------------------
const isReassignOpen = ref(false)
const targetStudent = ref(null)
const selectedNewAdvisorId = ref('')
const availableAdvisors = ref([])
const isLoadingAdvisors = ref(false)
const isSubmittingReassign = ref(false)

async function loadAdvisorsOptions() {
  if (availableAdvisors.value.length > 0) return
  isLoadingAdvisors.value = true
  try {
    const res = await apiClient.get('/v1/advisors/options')
    availableAdvisors.value = res.data || []
  } catch (err) {
    console.error('Error al cargar lista de asesores:', err)
  } finally {
    isLoadingAdvisors.value = false
  }
}

const otherAdvisors = computed(() => {
  const currentId = Number(props.advisorId)
  return availableAdvisors.value.filter((a) => Number(a.id) !== currentId)
})

function openReassignModal(resident) {
  targetStudent.value = resident
  selectedNewAdvisorId.value = ''
  isReassignOpen.value = true
  loadAdvisorsOptions()
}

function closeReassignModal() {
  isReassignOpen.value = false
  targetStudent.value = null
  selectedNewAdvisorId.value = ''
}

async function submitReassignment() {
  if (!selectedNewAdvisorId.value || !targetStudent.value) return
  isSubmittingReassign.value = true
  try {
    await apiClient.put(`/v1/students/${targetStudent.value.studentId}/advisor`, {
      advisorId: Number(selectedNewAdvisorId.value),
    })
    const newAdv = availableAdvisors.value.find((a) => Number(a.id) === Number(selectedNewAdvisorId.value))
    showAlert(
      `Residente reasignado exitosamente a ${newAdv ? newAdv.fullName : 'nuevo asesor'}.`,
      'success'
    )
    closeReassignModal()
    await fetchAdvisorDetails(props.advisorId)
    emit('updated')
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al reasignar asesor.', 'danger')
  } finally {
    isSubmittingReassign.value = false
  }
}

// ---------------------------------------------------------------------------
// 3. Añadir Residentes a Este Asesor
// ---------------------------------------------------------------------------
const isAddOpen = ref(false)
const candidateSearch = ref('')
const candidateList = ref([])
const isLoadingCandidates = ref(false)
const selectedCandidateIds = ref([])
const isSubmittingAdd = ref(false)
const filterOnlyUnassigned = ref(true)

function getAcceptanceBadge(student) {
  if (!student) return { text: 'Pendiente', class: 'tecnm-badge-warning' }
  const type = (student.projectType || '').toLowerCase()
  const title = (student.projectTitle || '').toLowerCase()
  const isAccred =
    student.isAccreditation ||
    student.exemptionReason ||
    type.includes('hackatec') ||
    title.includes('hackatec') ||
    type.includes('innovatec') ||
    title.includes('innovatec')
  if (isAccred) return { text: 'Acreditado', class: 'tecnm-badge-info' }
  if (student.hasAcceptanceLetter) return { text: 'Carta Cargada', class: 'tecnm-badge-success' }
  return { text: 'Sin Carta', class: 'tecnm-badge-warning' }
}

async function fetchCandidates() {
  isLoadingCandidates.value = true
  try {
    const params = {
      pageNumber: 1,
      pageSize: 250,
      search: candidateSearch.value.trim() || undefined,
      includeInactive: false,
      assignmentStatus: filterOnlyUnassigned.value ? 'unassigned' : undefined,
    }
    const res = await apiClient.get('/v1/students', { params })
    const data = res.data
    const items = Array.isArray(data) ? data : data.items || []
    const currentResidentIds = (advisorData.value?.residents || []).map((r) => r.studentId)
    candidateList.value = items.filter((s) => !currentResidentIds.includes(s.id))
  } catch (err) {
    console.error('Error al cargar alumnos candidatos:', err)
  } finally {
    isLoadingCandidates.value = false
  }
}

let searchTimer = null
function onSearchCandidateInput() {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    fetchCandidates()
  }, 300)
}

function openAddResidentsModal() {
  candidateSearch.value = ''
  selectedCandidateIds.value = []
  isAddOpen.value = true
  fetchCandidates()
}

function closeAddResidentsModal() {
  isAddOpen.value = false
  selectedCandidateIds.value = []
  candidateSearch.value = ''
}

function toggleCandidate(studentId) {
  const idx = selectedCandidateIds.value.indexOf(studentId)
  if (idx > -1) {
    selectedCandidateIds.value.splice(idx, 1)
  } else {
    selectedCandidateIds.value.push(studentId)
  }
}

function toggleSelectAllCandidates(e) {
  if (e.target.checked) {
    selectedCandidateIds.value = candidateList.value.map((s) => s.id)
  } else {
    selectedCandidateIds.value = []
  }
}

async function assignCandidate(student) {
  isSubmittingAdd.value = true
  try {
    await apiClient.put(`/v1/students/${student.id}/advisor`, {
      advisorId: Number(props.advisorId),
    })
    showAlert(
      `Estudiante "${student.fullName || student.controlNumber}" asignado exitosamente a este asesor.`,
      'success'
    )
    candidateList.value = candidateList.value.filter((s) => s.id !== student.id)
    await fetchAdvisorDetails(props.advisorId)
    emit('updated')
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al asignar estudiante al asesor.', 'danger')
  } finally {
    isSubmittingAdd.value = false
  }
}

async function submitBatchAdd() {
  if (selectedCandidateIds.value.length === 0) return
  isSubmittingAdd.value = true
  try {
    const res = await apiClient.post('/v1/students/batch-assign-advisor', {
      advisorId: Number(props.advisorId),
      studentIds: selectedCandidateIds.value.map(Number),
    })
    showAlert(
      res.data?.message ||
        `${selectedCandidateIds.value.length} estudiantes asignados exitosamente a este asesor.`,
      'success'
    )
    closeAddResidentsModal()
    await fetchAdvisorDetails(props.advisorId)
    emit('updated')
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error en la asignación masiva de residentes.', 'danger')
  } finally {
    isSubmittingAdd.value = false
  }
}

watch(
  () => [props.modelValue, props.advisorId],
  ([isOpen, id]) => {
    if (isOpen && id) {
      alertMessage.value = ''
      fetchAdvisorDetails(id)
    } else if (!isOpen) {
      advisorData.value = null
      errorMessage.value = ''
      alertMessage.value = ''
      closeReassignModal()
      closeAddResidentsModal()
    }
  },
  { immediate: true }
)
</script>

<template>
  <div v-if="modelValue" class="modal-backdrop" @click.self="closeModal">
    <div class="tecnm-modal-dialog" role="dialog" aria-modal="true">
      <!-- Modal Header -->
      <div class="tecnm-modal-header">
        <div class="tecnm-d-flex tecnm-align-center tecnm-gap-2">
          <svg xmlns="http://www.w3.org/2000/svg" class="tecnm-header-icon" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M15.75 6a3.75 3.75 0 1 1-7.5 0 3.75 3.75 0 0 1 7.5 0ZM4.501 20.118a7.5 7.5 0 0 1 14.998 0A17.933 17.933 0 0 1 12 21.75c-2.676 0-5.216-.584-7.499-1.632Z" />
          </svg>
          <h2 class="tecnm-modal-title">Expediente de Carga Docente</h2>
        </div>
        <button type="button" class="tecnm-modal-close" aria-label="Cerrar modal" @click="closeModal">
          &times;
        </button>
      </div>

      <!-- Loading State -->
      <div v-if="isLoading" class="modal-body-loading">
        <div class="spinner"></div>
        <p>Consultando residentes asignados y carga académica...</p>
      </div>

      <!-- Error State -->
      <div v-else-if="errorMessage" class="modal-body-content">
        <div class="tecnm-alert tecnm-alert-danger" style="margin: 1.5rem;">
          {{ errorMessage }}
        </div>
      </div>

      <!-- Main Content -->
      <div v-else-if="advisorData" class="modal-body-content">
        <!-- Notificación Interna -->
        <div
          v-if="alertMessage"
          :class="['tecnm-alert', `tecnm-alert-${alertType}`]"
          role="alert"
        >
          <span>{{ alertMessage }}</span>
        </div>

        <!-- Ficha del Asesor -->
        <div class="advisor-summary-banner">
          <div class="advisor-avatar-circle">
            {{ advisorInitials }}
          </div>
          <div class="advisor-info-col">
            <div class="advisor-name-row">
              <h3 class="advisor-name">{{ advisorData.fullName }}</h3>
            </div>
            <div class="advisor-meta-row">
              <span class="advisor-title-badge">{{ advisorData.title || 'Docente TecNM' }}</span>
              <span class="meta-sep">•</span>
              <span class="advisor-dept">{{ advisorData.departmentName }}</span>
            </div>
            <div class="advisor-contact-row">
              <a v-if="advisorData.email" :href="`mailto:${advisorData.email}`" class="advisor-contact-link">
                <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                </svg>
                {{ advisorData.email }}
              </a>
              <span v-if="advisorData.phone" class="advisor-contact-phone">
                <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 5a2 2 0 012-2h3.28a1 1 0 01.948.684l1.498 4.493a1 1 0 01-.502 1.21l-2.257 1.13a11.042 11.042 0 005.516 5.516l1.13-2.257a1 1 0 011.21-.502l4.493 1.498a1 1 0 01.684.949V19a2 2 0 01-2 2h-1C9.716 21 3 14.284 3 6V5z" />
                </svg>
                {{ advisorData.phone }}
              </span>
            </div>
          </div>
        </div>

        <!-- Sección de Residentes Asignados -->
        <div class="residents-section">
          <div class="residents-section-header">
            <h4 class="residents-title">
              Residentes Asignados ({{ advisorData.residents.length }})
            </h4>
            <div v-if="canManageAssignments" class="residents-header-actions">
              <button
                type="button"
                class="tecnm-btn tecnm-btn-primary tecnm-btn-sm"
                @click="openAddResidentsModal"
              >
                <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4.5v15m7.5-7.5h-15" />
                </svg>
                <span>+ Añadir Alumno(s)</span>
              </button>
              <button
                type="button"
                class="tecnm-btn tecnm-btn-outline-secondary tecnm-btn-sm"
                @click="goToAssignments"
              >
                Módulo Asignaciones &rarr;
              </button>
            </div>
          </div>

          <!-- Tabla de Residentes si los hay -->
          <div v-if="advisorData.residents.length > 0" class="residents-list">
            <div
              v-for="res in advisorData.residents"
              :key="res.studentId"
              class="resident-card-row"
            >
              <div class="resident-left-col">
                <div class="resident-avatar">
                  {{ (res.fullName || 'E').charAt(0).toUpperCase() }}
                </div>
                <div>
                  <div class="resident-name">{{ res.fullName }}</div>
                  <div class="resident-meta">
                    <span>No. Control: <strong>{{ res.controlNumber }}</strong></span>
                    <span class="meta-sep">•</span>
                    <span>{{ res.careerName }}</span>
                  </div>
                  <div v-if="res.projectTitle" class="resident-project-line">
                    <span class="project-tag">Proyecto:</span>
                    <strong>{{ res.projectTitle }}</strong>
                    <span v-if="res.companyName" class="company-tag">({{ res.companyName }})</span>
                  </div>
                  <div v-else class="resident-no-project">
                    Sin anteproyecto registrado actualmente
                  </div>
                </div>
              </div>

              <div class="resident-right-col">
                <div class="resident-status-box">
                  <TecnmBadge v-if="res.projectStatus" :status="res.projectStatus" />
                  <span v-else class="tecnm-badge tecnm-badge-neutral">Pendiente</span>
                  <div class="advisory-count-badge" :title="`${res.advisoryCount} sesiones registradas`">
                    {{ res.advisoryCount }} {{ res.advisoryCount === 1 ? 'asesoría' : 'asesorías' }}
                  </div>
                </div>

                <div class="resident-actions-cluster">
                  <button
                    v-if="res.projectId"
                    type="button"
                    class="tecnm-btn tecnm-btn-outline-primary tecnm-btn-sm"
                    title="Ver anteproyecto registrado"
                    @click="goToReview"
                  >
                    Ver Proyecto
                  </button>

                  <button
                    v-if="canManageAssignments"
                    type="button"
                    class="tecnm-btn tecnm-btn-outline-secondary tecnm-btn-sm"
                    title="Cambiar a otro asesor académico"
                    :disabled="processingStudentId === res.studentId"
                    @click="openReassignModal(res)"
                  >
                    <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7.5 21 3 16.5m0 0L7.5 12M3 16.5h13.5m0-13.5L21 7.5m0 0L16.5 12M21 7.5H7.5" />
                    </svg>
                    <span>Cambiar</span>
                  </button>

                  <button
                    v-if="canManageAssignments"
                    type="button"
                    class="tecnm-btn tecnm-btn-outline-danger tecnm-btn-sm"
                    title="Quitar este residente de este asesor"
                    :disabled="processingStudentId === res.studentId"
                    @click="handleUnassignResident(res)"
                  >
                    <span v-if="processingStudentId === res.studentId" class="spinner-inline"></span>
                    <svg v-else xmlns="http://www.w3.org/2000/svg" width="13" height="13" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18 18 6M6 6l12 12" />
                    </svg>
                    <span>Quitar</span>
                  </button>
                </div>
              </div>
            </div>
          </div>

          <!-- Empty State si no tiene residentes -->
          <div v-else class="empty-residents-box">
            <div class="empty-icon-circle">
              <svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
              </svg>
            </div>
            <h5 style="margin: 0.5rem 0 0.25rem; font-size: 1rem; color: var(--tecnm-text-primary, #0f172a);">
              Docente Disponible
            </h5>
            <p style="margin: 0; font-size: 0.85rem; color: var(--tecnm-text-secondary, #64748b);">
              Este asesor no tiene residentes asignados en este periodo académico.
            </p>
            <button
              v-if="canManageAssignments"
              type="button"
              class="tecnm-btn tecnm-btn-primary tecnm-btn-sm"
              style="margin-top: 1rem;"
              @click="openAddResidentsModal"
            >
              + Añadir Residentes a este Asesor
            </button>
          </div>
        </div>
      </div>

      <!-- Modal Footer -->
      <div class="tecnm-modal-footer" style="display: flex; justify-content: space-between; align-items: center;">
        <button
          v-if="canManageAssignments"
          type="button"
          class="tecnm-btn tecnm-btn-outline-secondary tecnm-btn-sm"
          @click="goToAssignments"
        >
          Ir a Módulo de Asignaciones &rarr;
        </button>
        <div v-else></div>
        <button type="button" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" @click="closeModal">
          Cerrar
        </button>
      </div>
    </div>
  </div>

  <!-- Sub-Modal: Cambiar Asesor a Residente -->
  <Teleport to="body">
    <div v-if="isReassignOpen" class="submodal-backdrop" @click.self="closeReassignModal">
      <div class="submodal-dialog" role="dialog" aria-modal="true">
        <div class="tecnm-modal-header">
          <div class="tecnm-d-flex tecnm-align-center tecnm-gap-2">
            <svg xmlns="http://www.w3.org/2000/svg" class="tecnm-header-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
              <path stroke-linecap="round" stroke-linejoin="round" d="M7.5 21 3 16.5m0 0L7.5 12M3 16.5h13.5m0-13.5L21 7.5m0 0L16.5 12M21 7.5H7.5" />
            </svg>
            <h3 class="tecnm-modal-title">Cambiar Asesor Académico</h3>
          </div>
          <button type="button" class="tecnm-modal-close" aria-label="Cerrar" @click="closeReassignModal">
            &times;
          </button>
        </div>

        <div class="submodal-body">
          <p class="submodal-desc">
            Seleccione el nuevo asesor al que se le transferirá el estudiante 
            <strong>{{ targetStudent?.fullName }}</strong> ({{ targetStudent?.controlNumber }}).
          </p>

          <div class="reassign-card">
            <div class="reassign-item">
              <span class="reassign-label">Asesor Actual:</span>
              <span class="reassign-val">{{ advisorData?.fullName }}</span>
            </div>
            <div class="reassign-item">
              <span class="reassign-label">Carrera:</span>
              <span class="reassign-val">{{ targetStudent?.careerName }}</span>
            </div>
            <div v-if="targetStudent?.projectTitle" class="reassign-item">
              <span class="reassign-label">Proyecto:</span>
              <span class="reassign-val">{{ targetStudent?.projectTitle }}</span>
            </div>
          </div>

          <div class="tecnm-form-group" style="margin-top: 0.75rem;">
            <label class="tecnm-label" for="newAdvisorSelect">
              Nuevo Asesor Destino <span class="tecnm-required">*</span>
            </label>
            <div v-if="isLoadingAdvisors" class="submodal-loading-inline">
              <span class="spinner-inline"></span> Cargando catálogo de asesores...
            </div>
            <select
              v-else
              id="newAdvisorSelect"
              v-model="selectedNewAdvisorId"
              class="tecnm-form-control"
            >
              <option value="" disabled>-- Seleccione un asesor --</option>
              <option
                v-for="adv in otherAdvisors"
                :key="adv.id"
                :value="adv.id"
              >
                {{ adv.fullName }} ({{ adv.assignedStudentsCount }} residentes actuales)
              </option>
            </select>
          </div>
        </div>

        <div class="submodal-footer">
          <button
            type="button"
            class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
            :disabled="isSubmittingReassign"
            @click="closeReassignModal"
          >
            Cancelar
          </button>
          <button
            type="button"
            class="tecnm-btn tecnm-btn-primary tecnm-btn-sm"
            :disabled="isSubmittingReassign || !selectedNewAdvisorId"
            @click="submitReassignment"
          >
            <span v-if="isSubmittingReassign" class="spinner-inline"></span>
            <span>Confirmar Transferencia</span>
          </button>
        </div>
      </div>
    </div>
  </Teleport>

  <!-- Sub-Modal: Añadir Residentes a Este Asesor -->
  <Teleport to="body">
    <div v-if="isAddOpen" class="submodal-backdrop" @click.self="closeAddResidentsModal">
      <div class="submodal-dialog submodal-dialog-large" role="dialog" aria-modal="true">
        <div class="tecnm-modal-header">
          <div class="tecnm-d-flex tecnm-align-center tecnm-gap-2">
            <svg xmlns="http://www.w3.org/2000/svg" class="tecnm-header-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
              <path stroke-linecap="round" stroke-linejoin="round" d="M19 7.5v3m0 0v3m0-3h3m-3 0h-3m-2.25-4.125a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0ZM4 19.235v-.11a6.375 6.375 0 0 1 12.75 0v.109A12.318 12.318 0 0 1 10.374 21c-2.331 0-4.512-.645-6.374-1.766Z" />
            </svg>
            <h3 class="tecnm-modal-title">Añadir Residentes a {{ advisorData?.fullName }}</h3>
          </div>
          <button type="button" class="tecnm-modal-close" aria-label="Cerrar" @click="closeAddResidentsModal">
            &times;
          </button>
        </div>

        <div class="submodal-body">
          <!-- Barra de Búsqueda y Filtro -->
          <div class="candidate-toolbar">
            <div class="tecnm-form-group tecnm-mb-0" style="flex: 1; min-width: 240px;">
              <input
                v-model="candidateSearch"
                type="search"
                class="tecnm-form-control"
                placeholder="Buscar estudiante por nombre o no. control..."
                @input="onSearchCandidateInput"
              />
            </div>
            <label class="candidate-checkbox-label">
              <input
                v-model="filterOnlyUnassigned"
                type="checkbox"
                @change="fetchCandidates"
              />
              <span>Solo sin asesor asignado</span>
            </label>
          </div>

          <!-- Estado de Carga -->
          <div v-if="isLoadingCandidates" class="submodal-loading">
            <div class="spinner"></div>
            <p>Consultando estudiantes candidatos...</p>
          </div>

          <!-- Lista de Candidatos -->
          <div v-else-if="candidateList.length > 0" class="candidate-list-scroll">
            <table class="tecnm-table candidate-table">
              <thead>
                <tr>
                  <th style="width: 38px;">
                    <input
                      type="checkbox"
                      :checked="candidateList.length > 0 && selectedCandidateIds.length === candidateList.length"
                      @change="toggleSelectAllCandidates"
                    />
                  </th>
                  <th>Estudiante</th>
                  <th>Carrera / Anteproyecto</th>
                  <th>Estado Carta</th>
                  <th style="text-align: right;">Acción</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="cand in candidateList"
                  :key="cand.id"
                  :class="{ 'row-selected': selectedCandidateIds.includes(cand.id) }"
                >
                  <td>
                    <input
                      type="checkbox"
                      :value="cand.id"
                      :checked="selectedCandidateIds.includes(cand.id)"
                      @change="toggleCandidate(cand.id)"
                    />
                  </td>
                  <td>
                    <div class="candidate-name">{{ cand.fullName || cand.name }}</div>
                    <div class="candidate-control">{{ cand.controlNumber }}</div>
                  </td>
                  <td>
                    <div class="candidate-career">{{ cand.careerName || 'Carrera general' }}</div>
                    <div v-if="cand.projectTitle" class="candidate-project" :title="cand.projectTitle">
                      {{ cand.projectTitle }}
                    </div>
                    <div v-else class="candidate-no-project">Sin anteproyecto</div>
                  </td>
                  <td>
                    <span
                      :class="['tecnm-badge', getAcceptanceBadge(cand).class]"
                      style="font-size: 0.725rem;"
                    >
                      {{ getAcceptanceBadge(cand).text }}
                    </span>
                  </td>
                  <td style="text-align: right;">
                    <button
                      type="button"
                      class="tecnm-btn tecnm-btn-primary tecnm-btn-sm"
                      :disabled="isSubmittingAdd"
                      title="Asignar este alumno individualmente"
                      @click="assignCandidate(cand)"
                    >
                      + Asignar
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- Empty State -->
          <div v-else class="submodal-empty">
            <p>No se encontraron estudiantes disponibles con los criterios actuales.</p>
          </div>
        </div>

        <div class="submodal-footer" style="display: flex; justify-content: space-between; align-items: center;">
          <span class="candidate-selected-count">
            {{ selectedCandidateIds.length }} estudiante(s) seleccionado(s)
          </span>
          <div class="tecnm-d-flex tecnm-gap-2">
            <button
              type="button"
              class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
              :disabled="isSubmittingAdd"
              @click="closeAddResidentsModal"
            >
              Cerrar
            </button>
            <button
              v-if="selectedCandidateIds.length > 0"
              type="button"
              class="tecnm-btn tecnm-btn-primary tecnm-btn-sm"
              :disabled="isSubmittingAdd"
              @click="submitBatchAdd"
            >
              <span v-if="isSubmittingAdd" class="spinner-inline"></span>
              <span>Asignar Seleccionados ({{ selectedCandidateIds.length }})</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
.modal-backdrop {
  position: fixed;
  inset: 0;
  background-color: rgba(15, 23, 42, 0.6);
  backdrop-filter: blur(3px);
  z-index: 1100;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1rem;
}

.tecnm-modal-dialog {
  background: #ffffff;
  border-radius: 12px;
  box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.15), 0 8px 10px -6px rgba(0, 0, 0, 0.1);
  width: 100%;
  max-width: 860px;
  max-height: 90vh;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border-top: 4px solid var(--tecnm-gold-primary, #D4AF37);
  animation: modalEnter 0.2s ease-out;
}

@keyframes modalEnter {
  from {
    opacity: 0;
    transform: scale(0.96) translateY(8px);
  }
  to {
    opacity: 1;
    transform: scale(1) translateY(0);
  }
}

.tecnm-modal-header {
  padding: 1.25rem 1.5rem;
  border-bottom: 1px solid var(--tecnm-border-color, #e2e8f0);
  display: flex;
  justify-content: space-between;
  align-items: center;
  background-color: #ffffff;
}

.tecnm-modal-title {
  font-size: 1.125rem;
  font-weight: 700;
  color: var(--tecnm-blue-primary, #1B396A);
  margin: 0;
}

.tecnm-modal-close {
  background: transparent;
  border: none;
  font-size: 1.5rem;
  line-height: 1;
  color: var(--tecnm-text-secondary, #64748b);
  cursor: pointer;
  padding: 0.25rem 0.5rem;
  border-radius: 6px;
  transition: all 0.2s;
}

.tecnm-modal-close:hover {
  background-color: #f1f5f9;
  color: #0f172a;
}

.modal-body-loading {
  padding: 3rem 1.5rem;
  text-align: center;
  color: var(--tecnm-text-secondary, #64748b);
}

.spinner {
  width: 32px;
  height: 32px;
  border: 3px solid #e2e8f0;
  border-top-color: var(--tecnm-blue-primary, #1B396A);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  margin: 0 auto 1rem;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.spinner-inline {
  display: inline-block;
  width: 12px;
  height: 12px;
  border: 2px solid rgba(255, 255, 255, 0.4);
  border-top-color: currentColor;
  border-radius: 50%;
  animation: spin 0.6s linear infinite;
  margin-right: 4px;
  vertical-align: middle;
}

.modal-body-content {
  overflow-y: auto;
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.advisor-summary-banner {
  display: flex;
  gap: 1.25rem;
  padding: 1.25rem;
  background: linear-gradient(135deg, #f8fafc 0%, #f1f5f9 100%);
  border: 1px solid var(--tecnm-border-color, #e2e8f0);
  border-radius: 10px;
}

.advisor-avatar-circle {
  width: 60px;
  height: 60px;
  border-radius: 50%;
  background: linear-gradient(135deg, #1B396A 0%, #2563eb 100%);
  color: #ffffff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.25rem;
  font-weight: 700;
  flex-shrink: 0;
  box-shadow: 0 4px 6px -1px rgba(27, 57, 106, 0.2);
}

.advisor-info-col {
  flex: 1;
  min-width: 0;
}

.advisor-name-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-bottom: 0.25rem;
}

.advisor-name {
  font-size: 1.15rem;
  font-weight: 700;
  color: var(--tecnm-text-primary, #0f172a);
  margin: 0;
}

.advisor-meta-row {
  font-size: 0.825rem;
  color: var(--tecnm-text-secondary, #64748b);
  display: flex;
  align-items: center;
  gap: 0.4rem;
  margin-bottom: 0.5rem;
  flex-wrap: wrap;
}

.advisor-title-badge {
  font-weight: 600;
  color: var(--tecnm-blue-primary, #1B396A);
}

.meta-sep {
  color: #cbd5e1;
}

.advisor-contact-row {
  display: flex;
  gap: 1rem;
  font-size: 0.8rem;
  flex-wrap: wrap;
  margin-bottom: 0;
}

.advisor-contact-link,
.advisor-contact-phone {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  color: var(--tecnm-blue-primary, #1B396A);
  text-decoration: none;
}

.advisor-contact-link:hover {
  text-decoration: underline;
}

.residents-section {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.residents-section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.residents-header-actions {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.residents-title {
  font-size: 0.95rem;
  font-weight: 700;
  color: var(--tecnm-blue-primary, #1B396A);
  margin: 0;
}

.residents-list {
  display: flex;
  flex-direction: column;
  gap: 0.625rem;
}

.resident-card-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.875rem 1rem;
  border: 1px solid var(--tecnm-border-color, #e2e8f0);
  border-radius: 8px;
  background-color: #ffffff;
  gap: 1rem;
  transition: border-color 0.2s, box-shadow 0.2s;
}

.resident-card-row:hover {
  border-color: #cbd5e1;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.04);
}

.resident-left-col {
  display: flex;
  align-items: center;
  gap: 0.875rem;
  min-width: 0;
  flex: 1;
}

.resident-avatar {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  background: #f1f5f9;
  color: var(--tecnm-blue-primary, #1B396A);
  font-weight: 700;
  font-size: 0.9rem;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.resident-name {
  font-size: 0.925rem;
  font-weight: 600;
  color: var(--tecnm-text-primary, #0f172a);
}

.resident-meta {
  font-size: 0.775rem;
  color: var(--tecnm-text-secondary, #64748b);
  display: flex;
  align-items: center;
  gap: 0.4rem;
  margin-top: 0.1rem;
}

.resident-project-line {
  font-size: 0.8rem;
  color: var(--tecnm-text-primary, #0f172a);
  margin-top: 0.25rem;
}

.project-tag {
  color: var(--tecnm-text-secondary, #64748b);
  font-size: 0.75rem;
  margin-right: 0.25rem;
}

.company-tag {
  color: var(--tecnm-text-secondary, #64748b);
  font-size: 0.75rem;
  margin-left: 0.35rem;
}

.resident-no-project {
  font-size: 0.775rem;
  color: #94a3b8;
  font-style: italic;
  margin-top: 0.2rem;
}

.resident-right-col {
  display: flex;
  align-items: center;
  gap: 1rem;
  flex-shrink: 0;
  flex-wrap: wrap;
}

.resident-status-box {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 0.25rem;
}

.advisory-count-badge {
  font-size: 0.725rem;
  color: var(--tecnm-text-secondary, #64748b);
  background: #f1f5f9;
  padding: 0.1rem 0.4rem;
  border-radius: 4px;
}

.resident-actions-cluster {
  display: flex;
  align-items: center;
  gap: 0.4rem;
}

.empty-residents-box {
  text-align: center;
  padding: 2.5rem 1.5rem;
  background: #f8fafc;
  border: 1px dashed #cbd5e1;
  border-radius: 8px;
}

.empty-icon-circle {
  width: 52px;
  height: 52px;
  border-radius: 50%;
  background: #ecfdf5;
  color: #10b981;
  display: flex;
  align-items: center;
  justify-content: center;
  margin: 0 auto;
}

.tecnm-modal-footer {
  padding: 1rem 1.5rem;
  border-top: 1px solid var(--tecnm-border-color, #e2e8f0);
  background: #ffffff;
}

/* Sub-Modales (Reasignación y Añadir Alumnos) */
.submodal-backdrop {
  position: fixed;
  inset: 0;
  background-color: rgba(15, 23, 42, 0.7);
  backdrop-filter: blur(4px);
  z-index: 1200;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1rem;
}

.submodal-dialog {
  background: #ffffff;
  border-radius: 12px;
  box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.25);
  width: 100%;
  max-width: 540px;
  max-height: 85vh;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border-top: 4px solid var(--tecnm-gold-primary, #D4AF37);
  animation: modalEnter 0.2s ease-out;
}

.submodal-dialog-large {
  max-width: 820px;
}

.submodal-body {
  padding: 1.25rem;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.submodal-desc {
  font-size: 0.875rem;
  color: var(--tecnm-text-secondary, #64748b);
  margin: 0;
}

.reassign-card {
  background: #f8fafc;
  border: 1px solid var(--tecnm-border-color, #e2e8f0);
  border-radius: 8px;
  padding: 0.75rem 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}

.reassign-item {
  display: flex;
  font-size: 0.825rem;
  gap: 0.5rem;
}

.reassign-label {
  font-weight: 600;
  color: var(--tecnm-text-secondary, #64748b);
  min-width: 100px;
}

.reassign-val {
  color: var(--tecnm-text-primary, #0f172a);
}

.submodal-footer {
  padding: 0.875rem 1.25rem;
  border-top: 1px solid var(--tecnm-border-color, #e2e8f0);
  background: #ffffff;
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
}

.candidate-toolbar {
  display: flex;
  align-items: center;
  gap: 1rem;
  flex-wrap: wrap;
}

.candidate-checkbox-label {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  font-size: 0.825rem;
  color: var(--tecnm-text-primary, #0f172a);
  cursor: pointer;
  user-select: none;
}

.candidate-list-scroll {
  max-height: 380px;
  overflow-y: auto;
  border: 1px solid var(--tecnm-border-color, #e2e8f0);
  border-radius: 8px;
}

.candidate-table {
  margin: 0;
  width: 100%;
}

.candidate-table th {
  position: sticky;
  top: 0;
  background: #f8fafc;
  z-index: 1;
}

.candidate-name {
  font-weight: 600;
  font-size: 0.85rem;
  color: var(--tecnm-text-primary, #0f172a);
}

.candidate-control {
  font-size: 0.75rem;
  color: var(--tecnm-text-secondary, #64748b);
}

.candidate-career {
  font-size: 0.8rem;
  color: var(--tecnm-text-primary, #0f172a);
}

.candidate-project {
  font-size: 0.75rem;
  color: var(--tecnm-text-secondary, #64748b);
  max-width: 260px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.candidate-no-project {
  font-size: 0.75rem;
  color: #94a3b8;
  font-style: italic;
}

.row-selected {
  background-color: #f0f7ff;
}

.submodal-empty {
  text-align: center;
  padding: 2rem 1rem;
  color: var(--tecnm-text-secondary, #64748b);
  font-size: 0.875rem;
}

.submodal-loading {
  text-align: center;
  padding: 2rem 1rem;
  color: var(--tecnm-text-secondary, #64748b);
}

.submodal-loading-inline {
  font-size: 0.85rem;
  color: var(--tecnm-text-secondary, #64748b);
  padding: 0.5rem 0;
}

.candidate-selected-count {
  font-size: 0.825rem;
  color: var(--tecnm-text-secondary, #64748b);
  font-weight: 500;
}
</style>

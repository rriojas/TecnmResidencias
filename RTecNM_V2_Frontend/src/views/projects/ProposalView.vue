<script setup>
import { ref, onMounted, computed } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useConfirm } from '@/composables/useConfirm'
import { useAudit } from '@/composables/useAudit'
import { useGlobalSearch } from '@/composables/useGlobalSearch'
import apiClient from '@/services/api'
import TecnmPagination from '@/components/common/TecnmPagination.vue'
import TecnmBadge from '@/components/common/TecnmBadge.vue'
import TecnmAutocomplete from '@/components/common/TecnmAutocomplete.vue'

const authStore = useAuthStore()
const { confirm } = useConfirm()
const { showAudit } = useAudit()
const { open: openSearch } = useGlobalSearch()

// Estado
const proposals = ref([])
const statusFilter = ref('all')
const includeInactive = ref(false)
const isLoading = ref(false)
const isSubmitting = ref(false)

// Paginación
const pageNumber = ref(1)
const pageSize = ref(10)
const totalCount = ref(0)
const totalPages = ref(0)

// Notificaciones
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

// Modales
const isModalOpen = ref(false)
const isEditMode = ref(false)
const editingProposalId = ref(null)
const formError = ref('')

const isDetailOpen = ref(false)
const selectedProject = ref(null)

// Initial items para autocompletes
const initialStudent = ref(null)
const initialCompany = ref(null)
const initialAdvisor = ref(null)

const form = ref({
  studentId: '',
  companyId: '',
  advisorId: '',
  title: '',
  projectType: '',
  problemStatement: '',
  justification: '',
  generalObjective: '',
  objectives: [''],
})

// Estados de anteproyectos
const DRAFT_STATUSES = ['draft', 'borrador', 'rejected', 'correcciones requeridas']
const PRINTABLE_STATUSES = ['approved', 'aprobado', 'in_progress', 'inprogress', 'en_progreso', 'completed', 'completado']
const ACTIVE_STATUSES = ['draft', 'borrador', 'pending', 'pendiente', 'proposed', 'under_review', 'in_review', 'en revision', 'revision', 'approved', 'aprobado', 'in_progress', 'inprogress', 'en_progreso']
const CANCELLABLE_STUDENT_STATUSES = ['draft', 'borrador', 'pending', 'pendiente', 'proposed', 'under_review', 'in_review', 'en revision', 'revision', 'rejected', 'correcciones requeridas']

const isStaff = computed(() => {
  return (
    authStore.isAdmin ||
    authStore.hasRole('departmenthead', 'advisor', 'vinculacion', 'director')
  )
})

function canCancelProposal(proposal) {
  if (!proposal || authStore.isReadOnly) return false
  const st = String(proposal.status || '').toLowerCase()
  if (!proposal.isActive || st === 'cancelled' || st === 'completed') return false
  if (isStaff.value) return true
  // Si es estudiante: NO puede cancelar una vez que ya fue aprobado o está en curso
  return CANCELLABLE_STUDENT_STATUSES.includes(st)
}

const studentProfile = ref(null)

const hasAdvisor = computed(() => {
  if (isStaff.value) return true
  return !!(studentProfile.value && studentProfile.value.advisorId)
})

// Para estudiantes: detectar si ya cuenta con un anteproyecto activo
const activeProposal = computed(() => {
  if (isStaff.value) return null
  return proposals.value.find((p) => {
    const st = (p.status || '').toLowerCase()
    return ACTIVE_STATUSES.includes(st) && p.isActive !== false
  })
})

const canCreateProposal = computed(() => {
  if (authStore.isReadOnly) return false
  if (isStaff.value) return true
  return !activeProposal.value && hasAdvisor.value
})

async function loadStudentProposals() {
  isLoading.value = true
  try {
    if (authStore.hasRole('student') && !isStaff.value) {
      try {
        const sRes = await apiClient.get('/v1/students/me')
        studentProfile.value = sRes.data
      } catch {
        studentProfile.value = null
      }
    }

    const params = {
      pageNumber: pageNumber.value,
      pageSize: pageSize.value,
      includeInactive: includeInactive.value,
    }
    if (statusFilter.value && statusFilter.value !== 'all') {
      params.status = statusFilter.value
    }
    const res = await apiClient.get('/v1/projects', { params })
    const data = res.data
    proposals.value = Array.isArray(data) ? data : (data.items || [])
    totalCount.value = data.totalCount || proposals.value.length
    totalPages.value = data.totalPages || Math.ceil(totalCount.value / pageSize.value) || 1
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al cargar anteproyectos.', 'danger')
    proposals.value = []
  } finally {
    isLoading.value = false
  }
}

function addObjective() {
  form.value.objectives.push('')
}

function removeObjective(index) {
  if (form.value.objectives.length > 1) {
    form.value.objectives.splice(index, 1)
  } else {
    showAlert('Debe ingresar al menos un objetivo específico.', 'warning')
  }
}

function openCreateModal() {
  if (authStore.isReadOnly) return
  isEditMode.value = false
  editingProposalId.value = null
  initialStudent.value = null
  initialCompany.value = null
  initialAdvisor.value = null
  form.value = {
    studentId: '',
    companyId: '',
    advisorId: '',
    title: '',
    projectType: '',
    problemStatement: '',
    justification: '',
    generalObjective: '',
    objectives: [''],
  }
  formError.value = ''
  isModalOpen.value = true
}

async function openEditModal(proposal) {
  if (authStore.isReadOnly) {
    showAlert('El rol de supervisión no cuenta con permisos de edición.', 'warning')
    return
  }
  try {
    const res = await apiClient.get(`/v1/projects/${proposal.id}`)
    const p = res.data
    const st = (p.status || '').toLowerCase()
    const editable = isStaff.value ? !['completed', 'cancelled'].includes(st) : DRAFT_STATUSES.includes(st)

    if (!editable) {
      showAlert(
        isStaff.value
          ? 'No se puede editar un anteproyecto completado o cancelado.'
          : 'Solo puedes editar anteproyectos en estado de borrador o devueltos con correcciones.',
        'warning'
      )
      return
    }

    isEditMode.value = true
    editingProposalId.value = p.id
    initialStudent.value = p.studentId ? { id: p.studentId, fullName: p.studentName, controlNumber: p.studentControlNumber } : null
    initialCompany.value = p.companyId ? { id: p.companyId, name: p.companyName } : null
    initialAdvisor.value = p.advisorId ? { id: p.advisorId, fullName: p.advisorName } : null

    form.value = {
      studentId: p.studentId || '',
      companyId: p.companyId || '',
      advisorId: p.advisorId || '',
      title: p.title || '',
      projectType: p.projectType || '',
      problemStatement: p.problemStatement || '',
      justification: p.justification || '',
      generalObjective: p.generalObjective || '',
      objectives: (p.objectives && p.objectives.length > 0)
        ? p.objectives.map((o) => o.description || o)
        : [''],
    }
    formError.value = ''
    isModalOpen.value = true
  } catch {
    showAlert('No se pudieron cargar los datos del anteproyecto.', 'danger')
  }
}

async function openDetailModal(proposal) {
  try {
    const res = await apiClient.get(`/v1/projects/${proposal.id}`)
    selectedProject.value = res.data
    isDetailOpen.value = true
  } catch {
    showAlert('No se pudieron cargar los detalles del anteproyecto.', 'danger')
  }
}

async function handleProposalSubmit() {
  if (authStore.isReadOnly) return
  formError.value = ''

  if (!form.value.companyId) {
    formError.value = 'Seleccione la empresa receptora vinculada.'
    return
  }
  if (!form.value.advisorId) {
    formError.value = 'Seleccione el asesor interno asignado.'
    return
  }
  if (authStore.isAdmin || authStore.hasRole('departmenthead')) {
    if (!form.value.studentId) {
      formError.value = 'Seleccione el estudiante destinatario.'
      return
    }
  }
  if (!form.value.title.trim()) {
    formError.value = 'Ingrese el título del proyecto.'
    return
  }
  if (!form.value.problemStatement.trim()) {
    formError.value = 'Ingrese el planteamiento del problema.'
    return
  }
  if (!form.value.justification.trim()) {
    formError.value = 'Ingrese la justificación del proyecto.'
    return
  }
  if (!form.value.generalObjective.trim()) {
    formError.value = 'Ingrese el objetivo general.'
    return
  }

  const validObjs = form.value.objectives.map((o) => o.trim()).filter(Boolean)
  if (validObjs.length === 0) {
    formError.value = 'Debe ingresar al menos un objetivo específico.'
    return
  }

  isSubmitting.value = true

  const payload = {
    studentId: form.value.studentId ? Number(form.value.studentId) : undefined,
    companyId: Number(form.value.companyId),
    advisorId: Number(form.value.advisorId),
    title: form.value.title.trim(),
    projectType: form.value.projectType.trim() || undefined,
    problemStatement: form.value.problemStatement.trim(),
    justification: form.value.justification.trim(),
    generalObjective: form.value.generalObjective.trim(),
    objectives: validObjs.map((desc, idx) => ({
      sequence: idx + 1,
      description: desc,
    })),
  }

  try {
    if (isEditMode.value) {
      await apiClient.put(`/v1/projects/${editingProposalId.value}`, payload)
      showAlert('Anteproyecto actualizado exitosamente.', 'success')
    } else {
      await apiClient.post('/v1/projects', payload)
      showAlert('Solicitud de anteproyecto registrada como borrador.', 'success')
    }
    isModalOpen.value = false
    loadStudentProposals()
  } catch (err) {
    formError.value =
      err.response?.data?.message ||
      'Error al registrar el anteproyecto. Verifique los campos obligatorios.'
  } finally {
    isSubmitting.value = false
  }
}

async function submitProposal(proposal) {
  if (authStore.isReadOnly) return
  const confirmed = await confirm({
    title: 'Enviar a Revisión',
    message: `¿Desea enviar a revisión el anteproyecto "${proposal.title}"? Una vez enviado, la División Académica procederá con su dictamen.`,
    okText: 'Enviar a Revisión',
    cancelText: 'Cancelar',
  })
  if (!confirmed) return

  try {
    await apiClient.patch(`/v1/projects/${proposal.id}/submit`)
    showAlert('Anteproyecto enviado a revisión exitosamente.', 'success')
    loadStudentProposals()
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al enviar a revisión.', 'danger')
  }
}

async function cancelProposal(proposal) {
  if (authStore.isReadOnly) return
  const confirmed = await confirm({
    title: 'Cancelar Solicitud de Anteproyecto',
    message: `¿Está seguro de cancelar el anteproyecto "${proposal.title}"? Esto liberará tu registro para presentar una nueva propuesta.`,
    okText: 'Cancelar Anteproyecto',
    cancelText: 'Volver',
  })
  if (!confirmed) return

  try {
    await apiClient.patch(`/v1/projects/${proposal.id}/cancel`)
    showAlert('Solicitud de anteproyecto cancelada correctamente.', 'success')
    loadStudentProposals()
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al cancelar anteproyecto.', 'danger')
  }
}

async function reactivateProposal(proposal) {
  if (authStore.isReadOnly) return
  try {
    await apiClient.patch(`/v1/projects/${proposal.id}/activate`)
    showAlert('Anteproyecto reactivado correctamente.', 'success')
    loadStudentProposals()
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al reactivar anteproyecto.', 'danger')
  }
}

async function downloadProposalPdf(proposal) {
  if (!proposal) return
  try {
    const res = await apiClient.get(`/v1/projects/${proposal.id}/pdf`, {
      responseType: 'blob',
    })
    const blob = new Blob([res.data], { type: 'application/pdf' })
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `anteproyecto_${proposal.id}.pdf`
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    window.URL.revokeObjectURL(url)
  } catch {
    showAlert('Error al descargar el PDF del anteproyecto.', 'danger')
  }
}

function handleAudit(proposal) {
  showAudit({
    title: `Auditoría — Anteproyecto #${proposal.id}`,
    item: proposal,
  })
}

function formatTecNMDate(dateStr) {
  if (!dateStr) return '—'
  const d = new Date(dateStr)
  if (isNaN(d.getTime())) return dateStr
  return d.toLocaleDateString('es-MX', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  })
}

onMounted(() => {
  loadStudentProposals()
})
</script>

<template>
  <div>
    <!-- Notificaciones -->
    <div
      v-if="alertMessage"
      id="alertContainer"
      class="tecnm-alert"
      :class="`tecnm-alert-${alertType}`"
      role="alert"
    >
      <span>{{ alertMessage }}</span>
    </div>

    <!-- Alerta de Asesor No Asignado para Estudiantes -->
    <div
      v-if="authStore.hasRole('student') && !isStaff && !hasAdvisor"
      class="tecnm-alert tecnm-alert-warning"
      role="alert"
      style="margin-bottom: 1rem;"
    >
      <span><strong>Aviso de Asignación de Asesor:</strong> No cuentas con un Asesor Académico asignado. Tu Jefatura de División Académica debe asignarte un asesor antes de que puedas registrar o enviar tu anteproyecto.</span>
    </div>

    <!-- Barra de Acciones -->
    <div class="tecnm-actions-bar">
      <div>
        <h1 class="tecnm-page-title">Solicitud de Anteproyecto de Residencia</h1>
        <p class="tecnm-page-subtitle">Registro y seguimiento de propuestas de residencia profesional</p>
      </div>
      <div class="tecnm-page-actions">
        <button
          v-if="isStaff"
          type="button"
          class="tecnm-btn tecnm-btn-secondary"
          @click="openSearch({ initialSource: 'PROJECTS' })"
        >
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
          </svg>
          <span>Abrir búsqueda</span>
        </button>
        <span v-if="isStaff" class="tecnm-page-actions-divider" aria-hidden="true"></span>
        <button
          v-if="canCreateProposal"
          id="openProposalModalBtn"
          type="button"
          class="tecnm-btn tecnm-btn-primary"
          @click="openCreateModal"
        >
          + Registrar Nuevo Anteproyecto
        </button>
      </div>
    </div>

    <!-- Tarjeta Principal de Tabla -->
    <div class="tecnm-card">
      <div class="tecnm-card-header">
        <h3 class="tecnm-card-title">{{ isStaff ? 'Historial de Anteproyectos Registrados' : 'Mis Anteproyectos Registrados' }}</h3>
      </div>
      <div class="tecnm-card-toolbar">
        <div class="tecnm-form-group tecnm-mb-0 tecnm-filter-group" style="margin-bottom: 0;">
          <label for="proposalStatusFilter" class="tecnm-label tecnm-sr-only">Filtrar por Estatus</label>
          <select
            id="proposalStatusFilter"
            v-model="statusFilter"
            class="tecnm-form-control"
            @change="loadStudentProposals"
          >
            <option value="all">Todos los Estatus</option>
            <option value="draft">Borradores</option>
            <option value="pending">En Revisión / Pendientes</option>
            <option value="approved">Aprobados</option>
            <option value="in_progress">En Residencia / En Curso</option>
            <option value="rejected">Devueltos con Observaciones</option>
            <option value="completed">Concluidos</option>
            <option value="cancelled">Cancelados</option>
          </select>
        </div>

        <div class="tecnm-toolbar-actions">
          <label class="tecnm-switch-label">
            <span class="tecnm-switch">
              <input
                id="proposalIncludeInactiveToggle"
                v-model="includeInactive"
                type="checkbox"
                @change="loadStudentProposals"
              />
              <span class="tecnm-switch-slider"></span>
            </span>
            Mostrar inactivos
          </label>
          <button
            id="refreshProposalsBtn"
            type="button"
            class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
            @click="loadStudentProposals"
          >
            Recargar Historial
          </button>
        </div>
      </div>

      <div class="tecnm-card-body">
        <div class="tecnm-table-responsive">
          <table class="tecnm-table tecnm-table-striped">
            <thead>
              <tr>
                <th>Título del Proyecto</th>
                <th v-if="isStaff">Estudiante</th>
                <th>Empresa Receptora</th>
                <th>Tipo</th>
                <th>Fecha Registro</th>
                <th>Estado</th>
                <th>Acciones</th>
              </tr>
            </thead>
            <tbody id="studentProposalsTableBody">
              <tr v-if="isLoading">
                <td :colspan="isStaff ? 7 : 6" class="tecnm-table-empty">
                  Cargando solicitudes de anteproyecto...
                </td>
              </tr>
              <tr v-else-if="proposals.length === 0">
                <td :colspan="isStaff ? 7 : 6" class="tecnm-table-empty">
                  No hay solicitudes de anteproyecto registradas.
                </td>
              </tr>
              <tr
                v-for="p in proposals"
                v-else
                :key="p.id"
              >
                <td>
                  <strong>{{ p.title }}</strong>
                  <div
                    v-if="['rejected', 'rechazado'].includes((p.status||'').toLowerCase()) && p.reviewComments"
                    class="tecnm-text-muted"
                    style="font-size: 0.78rem; color: var(--tecnm-warning, #d97706); margin-top: 0.25rem; display: flex; align-items: center; gap: 0.25rem;"
                  >
                    <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m9-.75a9 9 0 1 1-18 0 9 9 0 0 1 18 0Zm-9 3.75h.008v.008H12v-.008Z" />
                    </svg>
                    <span>Observaciones: {{ p.reviewComments.length > 80 ? p.reviewComments.substring(0, 80) + '...' : p.reviewComments }}</span>
                  </div>
                </td>
                <td v-if="isStaff">{{ p.studentName || '—' }}</td>
                <td>{{ p.companyName || '—' }}</td>
                <td>{{ p.projectType || 'Desarrollo' }}</td>
                <td>{{ formatTecNMDate(p.createdAt) }}</td>
                <td>
                  <TecnmBadge :status="p.status" />
                </td>
                <td>
                  <div class="tecnm-row-actions">
                    <button
                      type="button"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                      @click="openDetailModal(p)"
                    >
                      Ver detalle
                    </button>
                    <button
                      v-if="!authStore.isReadOnly && (isStaff ? !['completed', 'cancelled'].includes((p.status||'').toLowerCase()) : DRAFT_STATUSES.includes((p.status||'').toLowerCase()))"
                      type="button"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                      @click="openEditModal(p)"
                    >
                      {{ isStaff ? 'Editar' : 'Editar borrador' }}
                    </button>
                    <button
                      v-if="!authStore.isReadOnly && ['draft', 'rejected'].includes((p.status||'').toLowerCase())"
                      type="button"
                      class="tecnm-btn tecnm-btn-primary tecnm-btn-sm"
                      @click="submitProposal(p)"
                    >
                      Enviar a revisión
                    </button>
                    <button
                      v-if="PRINTABLE_STATUSES.includes((p.status||'').toLowerCase())"
                      type="button"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                      @click="downloadProposalPdf(p)"
                    >
                      Descargar PDF
                    </button>
                    <button
                      v-if="authStore.canSeeAudit"
                      type="button"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                      @click="handleAudit(p)"
                    >
                      Auditoría
                    </button>
                    <button
                      v-if="canCancelProposal(p)"
                      type="button"
                      class="tecnm-btn tecnm-btn-danger tecnm-btn-sm"
                      @click="cancelProposal(p)"
                    >
                      Cancelar solicitud
                    </button>
                    <button
                      v-if="(!p.isActive || (p.status||'').toLowerCase() === 'cancelled') && authStore.canManageRegistry && !authStore.isReadOnly"
                      type="button"
                      class="tecnm-btn tecnm-btn-success tecnm-btn-sm"
                      @click="reactivateProposal(p)"
                    >
                      Reactivar
                    </button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Paginación -->
        <TecnmPagination
          v-if="totalCount > 0"
          v-model:currentPage="pageNumber"
          v-model:pageSize="pageSize"
          :totalPages="totalPages"
          :totalCount="totalCount"
          @page-change="loadStudentProposals"
        />
      </div>
    </div>

    <!-- Modal Registrar / Editar Anteproyecto -->
    <div
      v-if="isModalOpen"
      id="createProposalModal"
      class="modal-backdrop active"
      role="dialog"
      aria-modal="true"
      @click.self="isModalOpen = false"
    >
      <div class="modal-card modal-card-wide">
        <div class="tecnm-modal-header">
          <h3 id="createProposalModalTitle" class="tecnm-modal-title">
            {{ isEditMode ? 'Editar Solicitud de Anteproyecto' : 'Registrar Nueva Solicitud de Anteproyecto' }}
          </h3>
          <button
            id="closeProposalModalBtn"
            type="button"
            class="tecnm-modal-close"
            aria-label="Cerrar"
            @click="isModalOpen = false"
          >
            &times;
          </button>
        </div>

        <form id="proposalForm" @submit.prevent="handleProposalSubmit">
          <div
            v-if="formError"
            class="tecnm-alert tecnm-alert-danger"
            style="margin-bottom: 1rem;"
            role="alert"
          >
            <span>{{ formError }}</span>
          </div>

          <!-- Estudiante Destinatario (Solo visible para Staff) -->
          <div
            v-if="authStore.isAdmin || authStore.hasRole('departmenthead')"
            id="adminStudentGroup"
            class="tecnm-form-group"
          >
            <label for="adminStudentId" class="tecnm-label">
              Estudiante Destinatario * <span class="tecnm-text-muted">(Administración asigna el anteproyecto a un alumno)</span>
            </label>
            <div id="studentAutocompleteWrapper">
              <TecnmAutocomplete
                v-model="form.studentId"
                endpoint="/v1/students"
                global-search-source="STUDENTS"
                placeholder="Buscar estudiante por nombre o no. control..."
                :initial-item="initialStudent"
              />
            </div>
          </div>

          <div class="tecnm-form-group">
            <label for="companyId" class="tecnm-label">Empresa Receptora *</label>
            <div id="companyAutocompleteWrapper">
              <TecnmAutocomplete
                v-model="form.companyId"
                endpoint="/v1/companies"
                global-search-source="COMPANIES"
                placeholder="Buscar empresa por nombre o RFC..."
                :initial-item="initialCompany"
              />
            </div>
          </div>

          <div class="tecnm-form-group">
            <label for="advisorId" class="tecnm-label">
              Asesor Interno Asignado * <span class="tecnm-text-muted">(Requerido antes de registrar anteproyecto)</span>
            </label>
            <div id="advisorAutocompleteWrapper">
              <TecnmAutocomplete
                v-model="form.advisorId"
                endpoint="/v1/advisors"
                global-search-source="ADVISORS"
                placeholder="Buscar asesor interno por nombre..."
                :initial-item="initialAdvisor"
              />
            </div>
          </div>

          <div class="tecnm-form-group">
            <label for="title" class="tecnm-label">Título del Proyecto *</label>
            <input
              id="title"
              v-model="form.title"
              type="text"
              class="tecnm-form-control"
              placeholder="Ej. Sistema de Control de Inventarios Tecnológicos"
              maxlength="250"
              :disabled="isSubmitting"
              required
            />
          </div>

          <div class="tecnm-form-group">
            <label for="projectType" class="tecnm-label">Tipo de Proyecto</label>
            <input
              id="projectType"
              v-model="form.projectType"
              type="text"
              class="tecnm-form-control"
              placeholder="Ej. Desarrollo Tecnológico, Investigación, etc."
              maxlength="100"
              :disabled="isSubmitting"
            />
          </div>

          <div class="tecnm-form-group">
            <label for="problemStatement" class="tecnm-label">Planteamiento del Problema *</label>
            <textarea
              id="problemStatement"
              v-model="form.problemStatement"
              class="tecnm-form-control"
              rows="3"
              placeholder="Describa detalladamente el problema a resolver..."
              :disabled="isSubmitting"
              required
            ></textarea>
          </div>

          <div class="tecnm-form-group">
            <label for="justification" class="tecnm-label">Justificación *</label>
            <textarea
              id="justification"
              v-model="form.justification"
              class="tecnm-form-control"
              rows="3"
              placeholder="Explique el impacto, viabilidad y relevancia técnica de la residencia..."
              :disabled="isSubmitting"
              required
            ></textarea>
          </div>

          <div class="tecnm-form-group">
            <label for="generalObjective" class="tecnm-label">Objetivo General *</label>
            <textarea
              id="generalObjective"
              v-model="form.generalObjective"
              class="tecnm-form-control"
              rows="2"
              placeholder="Defina el objetivo general que engloba el proyecto..."
              :disabled="isSubmitting"
              required
            ></textarea>
          </div>

          <div class="tecnm-form-group">
            <label class="tecnm-label">Objetivos Específicos *</label>
            <div id="objectivesContainer">
              <div
                v-for="(obj, idx) in form.objectives"
                :key="idx"
                class="objective-row"
              >
                <input
                  v-model="form.objectives[idx]"
                  type="text"
                  class="tecnm-form-control"
                  :placeholder="`Objetivo específico #${idx + 1}`"
                  :disabled="isSubmitting"
                  required
                />
                <button
                  v-if="form.objectives.length > 1"
                  type="button"
                  class="tecnm-btn tecnm-btn-danger tecnm-btn-sm"
                  :disabled="isSubmitting"
                  @click="removeObjective(idx)"
                >
                  &times;
                </button>
              </div>
            </div>
            <button
              type="button"
              class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
              style="margin-top: 0.5rem;"
              :disabled="isSubmitting"
              @click="addObjective"
            >
              + Agregar Objetivo Específico
            </button>
          </div>

          <div class="tecnm-modal-footer">
            <button
              id="cancelProposalModalBtn"
              type="button"
              class="tecnm-btn tecnm-btn-secondary"
              :disabled="isSubmitting"
              @click="isModalOpen = false"
            >
              Cancelar
            </button>
            <button
              id="submitProposalBtn"
              type="submit"
              class="tecnm-btn tecnm-btn-primary"
              :disabled="isSubmitting"
            >
              <span v-if="!isSubmitting">{{ isEditMode ? 'Actualizar Anteproyecto' : 'Guardar Borrador' }}</span>
              <span v-else class="login-spinner"></span>
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Modal Detalle del Anteproyecto -->
    <div
      v-if="isDetailOpen && selectedProject"
      id="proposalDetailModal"
      class="modal-backdrop active"
      role="dialog"
      aria-modal="true"
      @click.self="isDetailOpen = false"
    >
      <div class="modal-card modal-card-wide">
        <div class="tecnm-modal-header">
          <h3 class="tecnm-modal-title">Detalle del Anteproyecto</h3>
          <button
            type="button"
            class="tecnm-modal-close"
            aria-label="Cerrar"
            @click="isDetailOpen = false"
          >
            &times;
          </button>
        </div>

        <div>
          <!-- Cabecera de Estado y Metadatos -->
          <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: var(--tecnm-spacing-md);">
            <div>
              <h4 class="tecnm-field-label" style="margin-bottom: 0.25rem;">Título del Proyecto</h4>
              <p class="tecnm-field-value tecnm-field-value-emphasis" style="margin-bottom: 0;">
                {{ selectedProject.title }}
              </p>
            </div>
            <div>
              <TecnmBadge :status="selectedProject.status" />
            </div>
          </div>

          <!-- Stepper de Progreso del Trámite -->
          <div class="proposal-stepper">
            <div
              class="step-item"
              :class="{
                active: ['draft', 'borrador'].includes((selectedProject.status || '').toLowerCase()),
                completed: !['draft', 'borrador'].includes((selectedProject.status || '').toLowerCase())
              }"
            >
              <div class="step-circle">1</div>
              <div class="step-label">Borrador</div>
            </div>
            <div
              class="step-line"
              :class="{ completed: !['draft', 'borrador'].includes((selectedProject.status || '').toLowerCase()) }"
            ></div>
            <div
              class="step-item"
              :class="{
                active: ['pending', 'pendiente', 'proposed', 'under_review', 'in_review'].includes((selectedProject.status || '').toLowerCase()),
                warning: ['rejected', 'rechazado'].includes((selectedProject.status || '').toLowerCase()),
                completed: ['approved', 'aprobado', 'in_progress', 'inprogress', 'completed', 'completado'].includes((selectedProject.status || '').toLowerCase())
              }"
            >
              <div class="step-circle">2</div>
              <div class="step-label">
                {{ ['rejected', 'rechazado'].includes((selectedProject.status || '').toLowerCase()) ? 'Correcciones' : 'En Revisión' }}
              </div>
            </div>
            <div
              class="step-line"
              :class="{ completed: ['approved', 'aprobado', 'in_progress', 'inprogress', 'completed', 'completado'].includes((selectedProject.status || '').toLowerCase()) }"
            ></div>
            <div
              class="step-item"
              :class="{
                active: ['approved', 'aprobado'].includes((selectedProject.status || '').toLowerCase()),
                completed: ['in_progress', 'inprogress', 'completed', 'completado'].includes((selectedProject.status || '').toLowerCase())
              }"
            >
              <div class="step-circle">3</div>
              <div class="step-label">Dictamen Aprobado</div>
            </div>
            <div
              class="step-line"
              :class="{ completed: ['in_progress', 'inprogress', 'completed', 'completado'].includes((selectedProject.status || '').toLowerCase()) }"
            ></div>
            <div
              class="step-item"
              :class="{
                active: ['in_progress', 'inprogress'].includes((selectedProject.status || '').toLowerCase()),
                completed: ['completed', 'completado'].includes((selectedProject.status || '').toLowerCase())
              }"
            >
              <div class="step-circle">4</div>
              <div class="step-label">En Residencia</div>
            </div>
          </div>

          <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 1rem; margin-bottom: var(--tecnm-spacing-md);">
            <div>
              <h4 class="tecnm-field-label">Empresa Receptora</h4>
              <p class="tecnm-field-value">{{ selectedProject.companyName || '—' }}</p>
            </div>
            <div>
              <h4 class="tecnm-field-label">Asesor Interno Asignado</h4>
              <p class="tecnm-field-value">{{ selectedProject.advisorName || '—' }}</p>
            </div>
            <div>
              <h4 class="tecnm-field-label">Tipo de Proyecto</h4>
              <p class="tecnm-field-value">{{ selectedProject.projectType || 'Desarrollo Tecnológico' }}</p>
            </div>
            <div>
              <h4 class="tecnm-field-label">Fecha de Registro</h4>
              <p class="tecnm-field-value">{{ formatTecNMDate(selectedProject.createdAt) }}</p>
            </div>
          </div>

          <h4 class="tecnm-field-label">Planteamiento del Problema</h4>
          <p class="tecnm-field-value tecnm-field-value-box">{{ selectedProject.problemStatement || '—' }}</p>

          <h4 class="tecnm-field-label">Justificación</h4>
          <p class="tecnm-field-value tecnm-field-value-box">{{ selectedProject.justification || '—' }}</p>

          <h4 class="tecnm-field-label">Objetivo General</h4>
          <p class="tecnm-field-value tecnm-field-value-emphasis">{{ selectedProject.generalObjective || '—' }}</p>

          <h4 class="tecnm-field-label">Objetivos Específicos</h4>
          <ul class="tecnm-field-list">
            <li
              v-for="(obj, idx) in selectedProject.objectives || []"
              :key="idx"
            >
              {{ obj.description || obj }}
            </li>
          </ul>

          <!-- Alerta de Observaciones del Dictamen -->
          <div v-if="selectedProject.reviewComments" style="margin-top: var(--tecnm-spacing-md);">
            <div
              class="tecnm-alert"
              :class="['rejected', 'rechazado'].includes((selectedProject.status || '').toLowerCase()) ? 'tecnm-alert-warning' : 'tecnm-alert-info'"
              style="margin-bottom: 0;"
            >
              <h4 class="tecnm-field-label" style="margin-top: 0; color: inherit;">
                {{ ['rejected', 'rechazado'].includes((selectedProject.status || '').toLowerCase()) ? 'Observaciones y Correcciones Requeridas por la División / Revisor:' : 'Observaciones Registradas en el Dictamen:' }}
              </h4>
              <p style="margin: 0.25rem 0 0 0; white-space: pre-wrap;">{{ selectedProject.reviewComments }}</p>
              <div v-if="['rejected', 'rechazado'].includes((selectedProject.status || '').toLowerCase()) && !authStore.isReadOnly" style="margin-top: 0.75rem;">
                <button
                  type="button"
                  class="tecnm-btn tecnm-btn-warning tecnm-btn-sm"
                  style="display: inline-flex; align-items: center; gap: 0.35rem;"
                  @click="isDetailOpen = false; openEditModal(selectedProject)"
                >
                  <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                    <path stroke-linecap="round" stroke-linejoin="round" d="m16.862 4.487 1.687-1.688a1.875 1.875 0 1 1 2.652 2.652L10.582 16.07a4.5 4.5 0 0 1-1.897 1.13L6 18l.8-2.685a4.5 4.5 0 0 1 1.13-1.897l8.932-8.931Zm0 0L19.5 7.125M18 14v4.75A2.25 2.25 0 0 1 15.75 21H5.25A2.25 2.25 0 0 1 3 18.75V8.25A2.25 2.25 0 0 1 5.25 6H10" />
                  </svg>
                  Realizar Correcciones
                </button>
              </div>
            </div>
          </div>
        </div>

        <div class="tecnm-modal-footer">
          <button
            v-if="PRINTABLE_STATUSES.includes((selectedProject.status || '').toLowerCase())"
            type="button"
            class="tecnm-btn tecnm-btn-primary"
            @click="downloadProposalPdf(selectedProject)"
          >
            Descargar PDF Oficial
          </button>
          <button
            type="button"
            class="tecnm-btn tecnm-btn-secondary"
            @click="isDetailOpen = false"
          >
            Cerrar
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.tecnm-row-actions {
  display: inline-flex;
  gap: 0.35rem;
}

/* Banner de solicitud activa (Minimalista) */
.active-proposal-banner {
  background: #ffffff;
  border: 1px solid var(--tecnm-border-color, #e2e8f0);
  border-left: 4px solid var(--tecnm-blue-primary, #1b396a);
  border-radius: var(--tecnm-radius-md, 8px);
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.04);
  transition: box-shadow 0.2s ease;
  margin-bottom: 1.5rem;
}

.active-proposal-banner:hover {
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.07);
}

.active-proposal-body {
  padding: 0.95rem 1.25rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
  flex-wrap: wrap;
}

.active-proposal-info {
  flex: 1;
  min-width: 240px;
}

.active-proposal-header {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.25rem;
  flex-wrap: wrap;
}

.active-proposal-title {
  font-size: 1rem;
  font-weight: 700;
  color: var(--tecnm-blue-primary, #1b396a);
  word-break: break-word;
  overflow-wrap: anywhere;
}

.active-proposal-desc {
  margin: 0;
  font-size: 0.85rem;
  color: var(--tecnm-text-secondary, #64748b);
  line-height: 1.4;
}

.active-proposal-actions {
  display: flex;
  gap: 0.5rem;
  flex-shrink: 0;
}

/* Stepper de Progreso */
.proposal-stepper {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin: 1.25rem 0 1.75rem 0;
  padding: 1rem 1.25rem;
  background: #f8fafc;
  border-radius: 8px;
  border: 1px solid var(--tecnm-border, #e2e8f0);
}

.step-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.35rem;
  z-index: 1;
}

.step-circle {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background: #cbd5e1;
  color: #fff;
  font-weight: 700;
  font-size: 0.85rem;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
}

.step-label {
  font-size: 0.78rem;
  color: #64748b;
  font-weight: 600;
  text-align: center;
}

.step-item.active .step-circle {
  background: var(--tecnm-primary, #1b396a);
  box-shadow: 0 0 0 3px rgba(27, 57, 106, 0.2);
}

.step-item.active .step-label {
  color: var(--tecnm-primary, #1b396a);
  font-weight: 700;
}

.step-item.completed .step-circle {
  background: var(--tecnm-success, #16a34a);
}

.step-item.completed .step-label {
  color: var(--tecnm-success, #16a34a);
}

.step-item.warning .step-circle {
  background: var(--tecnm-warning, #d97706);
  box-shadow: 0 0 0 3px rgba(217, 119, 6, 0.2);
}

.step-item.warning .step-label {
  color: var(--tecnm-warning, #d97706);
  font-weight: 700;
}

.step-line {
  flex: 1;
  height: 3px;
  background: #cbd5e1;
  margin: 0 0.5rem;
  margin-bottom: 1.25rem;
}

.step-line.completed {
  background: var(--tecnm-success, #16a34a);
}
</style>

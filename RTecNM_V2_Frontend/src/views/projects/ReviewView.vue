<script setup>
import { ref, computed, onMounted } from 'vue'
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

// Estado de la tabla y filtros
const projects = ref([])
const totalCount = ref(0)
const totalPages = ref(0)
const pageNumber = ref(1)
const pageSize = ref(10)
const statusFilter = ref('all')
const sortBy = ref('CreatedAt')
const sortDir = ref('desc')
const includeInactive = ref(false)
const isLoading = ref(false)

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

function formatTecNMDate(iso) {
  if (!iso) return '—'
  const d = new Date(iso)
  if (isNaN(d.getTime())) return '—'
  const MONTHS = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre']
  return `${String(d.getDate()).padStart(2, '0')}/${MONTHS[d.getMonth()]}/${d.getFullYear()}`
}

// Helpers de ciclo de vida
const DICTAMINABLE_STATUSES = ['pending', 'pendiente', 'under_review', 'underreview', 'proposed', 'propuesto']
const PRINTABLE_STATUSES = ['approved', 'aprobado', 'in_progress', 'inprogress', 'en_progreso', 'completed', 'completado']

function isDictaminable(status) {
  return DICTAMINABLE_STATUSES.includes((status || '').toLowerCase())
}

function getActionLabel(project) {
  if (!project) return 'Ver Detalle'
  const st = (project.status || '').toLowerCase()
  if (!authStore.isReadOnly && !authStore.hasRole('vinculacion') && isDictaminable(st)) {
    return 'Revisar y Dictaminar'
  }
  if (st === 'rejected' || st === 'rechazado') {
    return 'Ver Observaciones'
  }
  return 'Ver Detalle'
}

// Modal de Revisión y Dictamen
const isReviewModalOpen = ref(false)
const selectedProject = ref(null)
const reviewComments = ref('')
const selectedAdvisorId = ref('')
const initialReviewAdvisor = ref(null)
const isSubmitting = ref(false)

// Catálogo de Carreras
const CAREERS = {
  1: 'Ing. en Sistemas Computacionales',
  2: 'Ing. Industrial',
  3: 'Ing. Mecatrónica',
  4: 'Ing. en Gestión Empresarial',
  5: 'Ing. Electrónica',
  6: 'Ing. Informática',
}

const selectedCareerFilter = ref('all')
const searchTerm = ref('')

const sortedProjects = computed(() => {
  let list = [...projects.value]

  if (selectedCareerFilter.value !== 'all') {
    const cid = Number(selectedCareerFilter.value)
    list = list.filter((p) => Number(p.careerId) === cid)
  }

  if (searchTerm.value.trim()) {
    const term = searchTerm.value.trim().toLowerCase()
    list = list.filter((p) => {
      const title = (p.title || '').toLowerCase()
      const student = (p.studentName || '').toLowerCase()
      const control = (p.studentControlNumber || '').toLowerCase()
      const company = (p.companyName || '').toLowerCase()
      const career = (CAREERS[p.careerId] || p.career || '').toLowerCase()
      return title.includes(term) || student.includes(term) || control.includes(term) || company.includes(term) || career.includes(term)
    })
  }

  const field = sortBy.value
  const dir = sortDir.value === 'asc' ? 1 : -1

  return list.sort((a, b) => {
    let valA = ''
    let valB = ''

    if (field === 'Title') {
      valA = a.title || ''
      valB = b.title || ''
    } else if (field === 'StudentName') {
      valA = a.studentName || ''
      valB = b.studentName || ''
    } else if (field === 'CompanyName') {
      valA = a.companyName || ''
      valB = b.companyName || ''
    } else if (field === 'CreatedAt') {
      valA = a.createdAt || ''
      valB = b.createdAt || ''
    } else if (field === 'Status') {
      valA = a.status || ''
      valB = b.status || ''
    } else {
      valA = a[field] ?? ''
      valB = b[field] ?? ''
    }

    if (typeof valA === 'string') valA = valA.toLowerCase()
    if (typeof valB === 'string') valB = valB.toLowerCase()

    if (valA < valB) return -1 * dir
    if (valA > valB) return 1 * dir
    return 0
  })
})

async function loadProjects({ silent = false } = {}) {
  if (!silent) isLoading.value = true
  try {
    const params = {
      pageNumber: pageNumber.value,
      pageSize: pageSize.value,
      sortBy: sortBy.value,
      sortDir: sortDir.value,
      includeInactive: includeInactive.value,
    }

    const res = await apiClient.get('/v1/projects', { params })
    const data = res.data
    projects.value = data.items || []
    totalCount.value = data.totalCount || 0
    totalPages.value = data.totalPages || 0
  } catch (err) {
    if (!silent) {
      showAlert(err.response?.data?.message || 'Error al cargar lista de anteproyectos.', 'danger')
      projects.value = []
      totalCount.value = 0
      totalPages.value = 0
    }
  } finally {
    if (!silent) isLoading.value = false
  }
}

function toggleSort(field) {
  if (sortBy.value === field) {
    sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
  } else {
    sortBy.value = field
    sortDir.value = 'asc'
  }
  pageNumber.value = 1
  loadProjects({ silent: true })
}

async function openReviewModal(project) {
  try {
    const res = await apiClient.get(`/v1/projects/${project.id}`)
    selectedProject.value = res.data
    reviewComments.value = res.data.reviewComments || ''
    selectedAdvisorId.value = res.data.advisorId || ''
    initialReviewAdvisor.value = res.data.advisorId ? { id: res.data.advisorId, fullName: res.data.advisorName } : null
    isReviewModalOpen.value = true
  } catch {
    showAlert('Error al cargar datos del anteproyecto.', 'danger')
  }
}

async function handleAssignAdvisor() {
  if (!selectedProject.value || !selectedAdvisorId.value) return
  isSubmitting.value = true
  try {
    await apiClient.post('/v1/advisors/assign', {
      advisorId: Number(selectedAdvisorId.value),
      projectId: selectedProject.value.id,
    })
    const updatedRes = await apiClient.get(`/v1/projects/${selectedProject.value.id}`)
    selectedProject.value = updatedRes.data
    showAlert('Asesor asignado al anteproyecto exitosamente.', 'success')
    loadProjects({ silent: true })
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al asignar el asesor.', 'danger')
  } finally {
    isSubmitting.value = false
  }
}

async function handleApprove() {
  if (!selectedProject.value) return

  const confirmed = await confirm({
    title: 'Dictamen Aprobado',
    message: `¿Está seguro de emitir dictamen de APROBADO para el anteproyecto "${selectedProject.value.title}"?`,
    okText: 'Aprobar Anteproyecto',
    cancelText: 'Cancelar',
  })
  if (!confirmed) return

  isSubmitting.value = true
  try {
    await apiClient.patch(`/v1/projects/${selectedProject.value.id}/status`, {
      status: 'approved',
      comments: reviewComments.value.trim() || undefined,
    })
    showAlert('Anteproyecto APROBADO exitosamente.', 'success')
    isReviewModalOpen.value = false
    loadProjects()
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al emitir el dictamen.', 'danger')
  } finally {
    isSubmitting.value = false
  }
}

async function handleReject() {
  if (!selectedProject.value) return

  if (!reviewComments.value.trim()) {
    showAlert('Debe ingresar los comentarios u observaciones para solicitar correcciones.', 'warning')
    return
  }

  const confirmed = await confirm({
    title: 'Solicitar Correcciones',
    message: `¿Desea solicitar correcciones al residente con las observaciones ingresadas?`,
    okText: 'Solicitar Correcciones',
    cancelText: 'Cancelar',
  })
  if (!confirmed) return

  isSubmitting.value = true
  try {
    await apiClient.patch(`/v1/projects/${selectedProject.value.id}/status`, {
      status: 'rejected',
      comments: reviewComments.value.trim(),
    })
    showAlert('Se han solicitado correcciones al residente.', 'warning')
    isReviewModalOpen.value = false
    loadProjects()
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al actualizar el estado.', 'danger')
  } finally {
    isSubmitting.value = false
  }
}

async function handleSoftDelete() {
  if (!selectedProject.value) return

  const confirmed = await confirm({
    title: 'Eliminar Anteproyecto',
    message: `¿Está seguro de dar de baja lógica este anteproyecto (${selectedProject.value.title})?`,
    okText: 'Eliminar',
    cancelText: 'Cancelar',
  })
  if (!confirmed) return

  isSubmitting.value = true
  try {
    await apiClient.delete(`/v1/projects/${selectedProject.value.id}`)
    showAlert('Anteproyecto eliminado correctamente.', 'success')
    isReviewModalOpen.value = false
    loadProjects()
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al eliminar anteproyecto.', 'danger')
  } finally {
    isSubmitting.value = false
  }
}

async function downloadProjectPdf(project) {
  if (!project) return
  try {
    const res = await apiClient.get(`/v1/projects/${project.id}/pdf`, {
      responseType: 'blob',
    })
    const blob = new Blob([res.data], { type: 'application/pdf' })
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `anteproyecto_${project.id}.pdf`
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    window.URL.revokeObjectURL(url)
  } catch {
    showAlert('Error al descargar el PDF del anteproyecto.', 'danger')
  }
}

function handleAudit(project) {
  showAudit({
    title: `Auditoría — Anteproyecto #${project.id}`,
    item: project,
  })
}

async function handleExportPdf() {
  try {
    const params = {
      status: statusFilter.value !== 'all' ? statusFilter.value : undefined,
      sortBy: sortBy.value,
      sortDir: sortDir.value,
      includeInactive: includeInactive.value,
    }
    const res = await apiClient.get('/v1/projects/export', {
      params,
      responseType: 'blob',
    })
    const blob = new Blob([res.data], { type: 'application/pdf' })
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = 'anteproyectos_tecnm.pdf'
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    window.URL.revokeObjectURL(url)
  } catch {
    showAlert('Error al exportar el reporte PDF.', 'danger')
  }
}

onMounted(() => {
  loadProjects()
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

    <!-- Barra de Acciones -->
    <div class="tecnm-actions-bar">
      <div>
        <h1 class="tecnm-page-title">Revisión y Dictamen de Anteproyectos</h1>
        <p class="tecnm-page-subtitle">Evaluación técnica y emisión de dictamen de anteproyectos de residencia profesional</p>
      </div>
      <div class="tecnm-page-actions">
        <button
          type="button"
          class="tecnm-btn tecnm-btn-secondary"
          @click="openSearch({ initialSource: 'PROJECTS' })"
        >
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
          </svg>
          <span>Abrir búsqueda</span>
        </button>
      </div>
    </div>

    <!-- Tarjeta Principal de Tabla -->
    <div class="tecnm-card">
      <div class="tecnm-card-header">
        <h3 class="tecnm-card-title">Lista de Anteproyectos</h3>
      </div>
      <div class="tecnm-card-toolbar">
        <div class="tecnm-form-group tecnm-mb-0 tecnm-search-box" style="margin-bottom: 0; min-width: 260px;">
          <input
            id="reviewSearchInput"
            v-model="searchTerm"
            type="search"
            class="tecnm-form-control"
            placeholder="Buscar por título, alumno, matrícula..."
          />
        </div>

        <div class="tecnm-d-flex tecnm-align-center tecnm-gap-2" style="display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap;">
          <label for="reviewCareerFilter" class="tecnm-field-label" style="margin-bottom: 0; white-space: nowrap; font-size: 0.85rem;">Carrera:</label>
          <select
            id="reviewCareerFilter"
            v-model="selectedCareerFilter"
            class="tecnm-form-control"
            style="min-width: 220px; font-size: 0.85rem;"
          >
            <option value="all">Todas las Carreras</option>
            <option v-for="(name, id) in CAREERS" :key="id" :value="id">
              {{ name }}
            </option>
          </select>
        </div>

        <div class="tecnm-toolbar-actions">
          <label class="tecnm-switch-label">
            <span class="tecnm-switch">
              <input
                id="includeInactiveToggle"
                v-model="includeInactive"
                type="checkbox"
                @change="loadProjects"
              />
              <span class="tecnm-switch-slider"></span>
            </span>
            Mostrar inactivos
          </label>
          <button
            id="exportProjectsBtn"
            type="button"
            class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
            @click="handleExportPdf"
          >
            Exportar PDF
          </button>
        </div>
      </div>

      <div class="tecnm-card-body">
        <div class="tecnm-table-responsive">
          <table id="projectsTable" class="tecnm-table tecnm-table-striped">
            <thead>
              <tr>
                <th
                  class="tecnm-th-sortable"
                  @click="toggleSort('Title')"
                >
                  Título del Proyecto
                  <span class="tecnm-sort-icon" :class="{ active: sortBy === 'Title' }">
                    {{ sortBy === 'Title' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th
                  class="tecnm-th-sortable"
                  @click="toggleSort('StudentName')"
                >
                  Estudiante y Carrera
                  <span class="tecnm-sort-icon" :class="{ active: sortBy === 'StudentName' }">
                    {{ sortBy === 'StudentName' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th
                  class="tecnm-th-sortable"
                  @click="toggleSort('CompanyName')"
                >
                  Empresa / Institución
                  <span class="tecnm-sort-icon" :class="{ active: sortBy === 'CompanyName' }">
                    {{ sortBy === 'CompanyName' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th
                  class="tecnm-th-sortable"
                  @click="toggleSort('CreatedAt')"
                >
                  Fecha Registro
                  <span class="tecnm-sort-icon" :class="{ active: sortBy === 'CreatedAt' }">
                    {{ sortBy === 'CreatedAt' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th
                  class="tecnm-th-sortable"
                  @click="toggleSort('Status')"
                >
                  Estado
                  <span class="tecnm-sort-icon" :class="{ active: sortBy === 'Status' }">
                    {{ sortBy === 'Status' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th class="tecnm-th-actions">Acciones</th>
              </tr>
            </thead>
            <tbody id="projectsTableBody">
              <tr v-if="isLoading">
                <td colspan="6" class="tecnm-table-empty">
                  Cargando anteproyectos...
                </td>
              </tr>
              <tr v-else-if="sortedProjects.length === 0">
                <td colspan="6" class="tecnm-table-empty">
                  <span v-if="includeInactive">No hay anteproyectos inactivos (deshabilitados) registrados.</span>
                  <span v-else>No hay anteproyectos que coincidan con los filtros seleccionados.</span>
                </td>
              </tr>
              <tr
                v-for="p in sortedProjects"
                v-else
                :key="p.id"
              >
                <td><strong>{{ p.title }}</strong></td>
                <td>
                  <div>{{ p.studentName || '—' }}</div>
                  <small v-if="p.careerId || p.career" style="color: var(--tecnm-blue-primary, #1b396a); font-size: 0.75rem;">
                    {{ CAREERS[p.careerId] || p.career }}
                  </small>
                </td>
                <td>{{ p.companyName || '—' }}</td>
                <td>{{ formatTecNMDate(p.createdAt) }}</td>
                <td>
                  <TecnmBadge :status="p.status" />
                </td>
                <td>
                  <div class="tecnm-row-actions">
                    <button
                      type="button"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                      @click="openReviewModal(p)"
                    >
                      {{ getActionLabel(p) }}
                    </button>
                    <button
                      v-if="PRINTABLE_STATUSES.includes((p.status||'').toLowerCase())"
                      type="button"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                      title="Descargar Anteproyecto PDF"
                      @click="downloadProjectPdf(p)"
                    >
                      PDF
                    </button>
                    <button
                      v-if="authStore.canSeeAudit"
                      type="button"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                      @click="handleAudit(p)"
                    >
                      Auditoría
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
          @page-change="loadProjects"
        />
      </div>
    </div>

    <!-- Modal de Revisión y Dictamen -->
    <div
      v-if="isReviewModalOpen && selectedProject"
      id="reviewModal"
      class="modal-backdrop active"
      role="dialog"
      aria-modal="true"
      @click.self="isReviewModalOpen = false"
    >
      <div class="modal-card modal-card-wide">
        <div class="tecnm-modal-header">
          <h3 class="tecnm-modal-title">
            Detalle de Solicitud de Anteproyecto
            <span id="modalProjectId" style="display: none;">{{ selectedProject.id }}</span>
          </h3>
          <button
            id="closeModalBtn"
            type="button"
            class="tecnm-modal-close"
            aria-label="Cerrar"
            @click="isReviewModalOpen = false"
          >
            &times;
          </button>
        </div>

        <div>
          <!-- Estado y Datos Generales -->
          <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: var(--tecnm-spacing-md);">
            <div>
              <h4 class="tecnm-field-label" style="margin-bottom: 0.25rem;">Estudiante Residente</h4>
              <p id="modalStudentName" class="tecnm-field-value tecnm-field-value-emphasis" style="margin-bottom: 0;">
                {{ selectedProject.studentName || '—' }} <span v-if="selectedProject.studentControlNumber" class="tecnm-text-muted">({{ selectedProject.studentControlNumber }})</span>
              </p>
            </div>
            <div>
              <TecnmBadge :status="selectedProject.status" />
            </div>
          </div>

          <h4 class="tecnm-field-label">Título del Proyecto</h4>
          <p id="modalProjectTitle" class="tecnm-field-value tecnm-field-value-emphasis">
            {{ selectedProject.title }}
          </p>

          <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 1rem; margin-bottom: var(--tecnm-spacing-md);">
            <div>
              <h4 class="tecnm-field-label">Empresa Receptora</h4>
              <p class="tecnm-field-value">{{ selectedProject.companyName || '—' }}</p>
            </div>
            <div>
              <h4 class="tecnm-field-label">Asesor Interno Asignado</h4>
              <p class="tecnm-field-value">
                <span v-if="selectedProject.advisorName" class="tecnm-badge tecnm-badge-success" style="font-size: 0.85rem;">
                  {{ selectedProject.advisorName }}
                </span>
                <span v-else class="tecnm-badge tecnm-badge-warning" style="font-size: 0.85rem;">
                  Pendiente de asignación
                </span>
              </p>
            </div>
          </div>

          <!-- Selector de Asesor para Jefatura / División Académica -->
          <div
            v-if="!authStore.isReadOnly && (authStore.isAdmin || authStore.hasRole('departmenthead', 'academic')) && !['completed', 'cancelled'].includes((selectedProject.status || '').toLowerCase())"
            class="tecnm-form-group"
            style="background: var(--tecnm-bg-light, #f8fafc); padding: 1rem; border-radius: 8px; border: 1px solid var(--tecnm-border-color, #e2e8f0); margin-bottom: 1rem;"
          >
            <label class="tecnm-label" style="font-weight: 600;">
              Asignar / Cambiar Asesor Académico por Anteproyecto:
            </label>
            <div style="display: flex; gap: 0.5rem; align-items: center; margin-top: 0.5rem;">
              <div style="flex: 1;">
                <TecnmAutocomplete
                  v-model="selectedAdvisorId"
                  endpoint="/v1/advisors"
                  global-search-source="ADVISORS"
                  placeholder="Buscar asesor académico por nombre..."
                  :initial-item="initialReviewAdvisor"
                />
              </div>
              <button
                type="button"
                class="tecnm-btn tecnm-btn-primary"
                :disabled="isSubmitting || !selectedAdvisorId || Number(selectedAdvisorId) === Number(selectedProject.advisorId)"
                @click="handleAssignAdvisor"
              >
                Guardar Asesor
              </button>
            </div>
          </div>

          <h4 class="tecnm-field-label">Planteamiento del Problema</h4>
          <p id="modalProblemStatement" class="tecnm-field-value tecnm-field-value-box">
            {{ selectedProject.problemStatement }}
          </p>

          <h4 class="tecnm-field-label">Justificación</h4>
          <p id="modalJustification" class="tecnm-field-value tecnm-field-value-box">
            {{ selectedProject.justification }}
          </p>

          <h4 class="tecnm-field-label">Objetivo General</h4>
          <p id="modalGeneralObjective" class="tecnm-field-value tecnm-field-value-emphasis">
            {{ selectedProject.generalObjective }}
          </p>

          <h4 class="tecnm-field-label">Objetivos Específicos</h4>
          <ul id="modalObjectivesList" class="tecnm-field-list">
            <li v-if="!selectedProject.objectives || selectedProject.objectives.length === 0">
              Sin objetivos específicos registrados.
            </li>
            <li
              v-for="(obj, idx) in selectedProject.objectives"
              v-else
              :key="idx"
            >
              {{ obj.description || obj }}
            </li>
          </ul>

          <!-- Bloque de Avisos e Información según el Estado -->

          <!-- 1. Proyecto Aprobado / En Progreso / Concluido -->
          <template v-if="['approved', 'aprobado', 'in_progress', 'inprogress', 'completed', 'completado'].includes((selectedProject.status || '').toLowerCase())">
            <div id="reviewNoticeApproved" class="tecnm-alert tecnm-alert-info">
              Este anteproyecto cuenta con dictamen <strong>APROBADO</strong>. El dictamen técnico es definitivo y el proyecto se encuentra registrado en el expediente institucional de residencias.
            </div>

            <div v-if="selectedProject.reviewComments" class="tecnm-form-group">
              <h4 class="tecnm-field-label">Observaciones Registradas en el Dictamen</h4>
              <p class="tecnm-field-value tecnm-field-value-box" style="background-color: var(--tecnm-bg-light, #f8fafc);">
                {{ selectedProject.reviewComments }}
              </p>
            </div>
          </template>

          <!-- 2. Proyecto Rechazado / Devuelto con Observaciones -->
          <template v-else-if="['rejected', 'rechazado'].includes((selectedProject.status || '').toLowerCase())">
            <div id="reviewNoticeRejected" class="tecnm-alert tecnm-alert-warning">
              Se han solicitado correcciones al residente. El dictamen formal queda en pausa en espera de que el estudiante realice los ajustes y reenvíe su anteproyecto a revisión.
            </div>

            <div v-if="selectedProject.reviewComments" class="tecnm-form-group">
              <h4 class="tecnm-field-label">Observaciones y Correcciones Requeridas Enviadas</h4>
              <p class="tecnm-field-value tecnm-field-value-box" style="border-left: 4px solid var(--tecnm-warning, #d97706); background-color: #fffbeb;">
                {{ selectedProject.reviewComments }}
              </p>
            </div>
          </template>

          <!-- 3. Proyecto en Borrador -->
          <template v-else-if="['draft', 'borrador'].includes((selectedProject.status || '').toLowerCase())">
            <div id="reviewNoticeDraft" class="tecnm-alert tecnm-alert-secondary">
              Este anteproyecto se encuentra en estado de <strong>Borrador</strong>. El residente aún se encuentra editándolo y no lo ha enviado formalmente a revisión.
            </div>
          </template>

          <!-- 4. Proyecto Cancelado -->
          <template v-else-if="['cancelled', 'cancelado'].includes((selectedProject.status || '').toLowerCase())">
            <div id="reviewNoticeCancelled" class="tecnm-alert tecnm-alert-danger">
              Esta solicitud de anteproyecto ha sido <strong>Cancelada</strong>.
            </div>
          </template>

          <!-- 5. Proyecto Pendiente / En Revisión (Dictaminable) -->
          <template v-else-if="isDictaminable(selectedProject.status) && !authStore.isReadOnly">
            <div id="reviewCommentsGroup" class="tecnm-form-group">
              <label for="reviewComments" class="tecnm-label">
                Comentarios u Observaciones del Dictamen *
                <span class="tecnm-text-muted">(Obligatorio si solicita correcciones; opcional para dictamen aprobado)</span>
              </label>
              <textarea
                id="reviewComments"
                v-model="reviewComments"
                class="tecnm-form-control"
                rows="3"
                placeholder="Ingrese observaciones técnicas, recomendaciones o motivo del dictamen..."
                :disabled="isSubmitting"
              ></textarea>
            </div>
          </template>
        </div>

        <div class="tecnm-modal-footer">
          <!-- Botón de Soft Delete (solo admin/jefatura y si no es read-only) -->
          <button
            v-if="authStore.canManageRegistry && !authStore.isReadOnly && !authStore.hasRole('vinculacion')"
            id="modalSoftDeleteBtn"
            type="button"
            class="tecnm-btn tecnm-btn-danger"
            :disabled="isSubmitting"
            @click="handleSoftDelete"
          >
            Eliminar (Soft Delete)
          </button>

          <!-- Descargar PDF si está aprobado / en curso -->
          <button
            v-if="PRINTABLE_STATUSES.includes((selectedProject.status || '').toLowerCase())"
            type="button"
            class="tecnm-btn tecnm-btn-secondary"
            @click="downloadProjectPdf(selectedProject)"
          >
            Descargar PDF Oficial
          </button>

          <!-- Asignar Asesor Interno directo si está aprobado -->
          <router-link
            v-if="PRINTABLE_STATUSES.includes((selectedProject.status || '').toLowerCase()) && (authStore.isAdmin || authStore.hasRole('departmenthead', 'academic'))"
            to="/advisors/assignments"
            class="tecnm-btn tecnm-btn-primary"
          >
            Asignar Asesor &rarr;
          </router-link>

          <!-- Botones de Dictamen solo si está Pendiente/En Revisión y usuario tiene permisos operativos -->
          <template v-if="isDictaminable(selectedProject.status) && !authStore.isReadOnly && !authStore.hasRole('vinculacion')">
            <button
              id="rejectBtn"
              type="button"
              class="tecnm-btn tecnm-btn-warning"
              :disabled="isSubmitting"
              @click="handleReject"
            >
              Solicitar Correcciones
            </button>
            <button
              id="approveBtn"
              type="button"
              class="tecnm-btn tecnm-btn-success"
              :disabled="isSubmitting"
              @click="handleApprove"
            >
              Dictaminar Aprobado
            </button>
          </template>

          <button
            type="button"
            class="tecnm-btn tecnm-btn-secondary"
            @click="isReviewModalOpen = false"
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
</style>

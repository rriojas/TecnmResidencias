<script setup>
import { ref, onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useConfirm } from '@/composables/useConfirm'
import { useAudit } from '@/composables/useAudit'
import { useGlobalSearch } from '@/composables/useGlobalSearch'
import apiClient from '@/services/api'
import TecnmPagination from '@/components/common/TecnmPagination.vue'
import TecnmBadge from '@/components/common/TecnmBadge.vue'

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
  if (!authStore.isReadOnly && isDictaminable(st)) {
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
const isSubmitting = ref(false)

async function loadProjects() {
  isLoading.value = true
  try {
    const params = {
      pageNumber: pageNumber.value,
      pageSize: pageSize.value,
      sortBy: sortBy.value,
      sortDir: sortDir.value,
      includeInactive: includeInactive.value,
    }
    if (statusFilter.value && statusFilter.value !== 'all') {
      params.status = statusFilter.value
    }

    const res = await apiClient.get('/v1/projects', { params })
    const data = res.data
    projects.value = data.items || []
    totalCount.value = data.totalCount || 0
    totalPages.value = data.totalPages || 0
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al cargar lista de anteproyectos.', 'danger')
    projects.value = []
    totalCount.value = 0
    totalPages.value = 0
  } finally {
    isLoading.value = false
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
  loadProjects()
}

async function openReviewModal(project) {
  try {
    const res = await apiClient.get(`/v1/projects/${project.id}`)
    selectedProject.value = res.data
    reviewComments.value = res.data.reviewComments || ''
    isReviewModalOpen.value = true
  } catch {
    showAlert('Error al cargar datos del anteproyecto.', 'danger')
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
        <div class="tecnm-form-group tecnm-mb-0 tecnm-filter-group" style="margin-bottom: 0;">
          <label for="statusFilter" class="tecnm-label tecnm-sr-only">Filtrar por Estatus</label>
          <select
            id="statusFilter"
            v-model="statusFilter"
            class="tecnm-form-control"
            @change="loadProjects"
          >
            <option value="all">Todos los Estatus</option>
            <option value="pending">Pendientes de Dictamen</option>
            <option value="approved">Aprobados</option>
            <option value="in_progress">En Residencia / En Curso</option>
            <option value="rejected">Devueltos / Correcciones</option>
            <option value="completed">Concluidos</option>
            <option value="cancelled">Cancelados</option>
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
                  data-sort="Title"
                  class="tecnm-sort-th"
                  :class="{
                    'tecnm-sort-asc': sortBy === 'Title' && sortDir === 'asc',
                    'tecnm-sort-desc': sortBy === 'Title' && sortDir === 'desc',
                  }"
                  style="cursor: pointer;"
                  @click="toggleSort('Title')"
                >
                  Título del Proyecto
                </th>
                <th>Estudiante</th>
                <th
                  data-sort="CreatedAt"
                  class="tecnm-sort-th"
                  :class="{
                    'tecnm-sort-asc': sortBy === 'CreatedAt' && sortDir === 'asc',
                    'tecnm-sort-desc': sortBy === 'CreatedAt' && sortDir === 'desc',
                  }"
                  style="cursor: pointer;"
                  @click="toggleSort('CreatedAt')"
                >
                  Fecha Registro
                </th>
                <th
                  data-sort="Status"
                  class="tecnm-sort-th"
                  :class="{
                    'tecnm-sort-asc': sortBy === 'Status' && sortDir === 'asc',
                    'tecnm-sort-desc': sortBy === 'Status' && sortDir === 'desc',
                  }"
                  style="cursor: pointer;"
                  @click="toggleSort('Status')"
                >
                  Estado
                </th>
                <th>Acciones</th>
              </tr>
            </thead>
            <tbody id="projectsTableBody">
              <tr v-if="isLoading">
                <td colspan="5" class="tecnm-table-empty">
                  Cargando anteproyectos...
                </td>
              </tr>
              <tr v-else-if="projects.length === 0">
                <td colspan="5" class="tecnm-table-empty">
                  No hay anteproyectos que coincidan con el filtro.
                </td>
              </tr>
              <tr
                v-for="p in projects"
                v-else
                :key="p.id"
              >
                <td><strong>{{ p.title }}</strong></td>
                <td>{{ p.studentName || '—' }}</td>
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
              <p class="tecnm-field-value">{{ selectedProject.advisorName || '—' }}</p>
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
            v-if="authStore.canManageRegistry && !authStore.isReadOnly"
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

          <!-- Botones de Dictamen solo si está Pendiente/En Revisión y usuario tiene permisos operativos -->
          <template v-if="isDictaminable(selectedProject.status) && !authStore.isReadOnly">
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

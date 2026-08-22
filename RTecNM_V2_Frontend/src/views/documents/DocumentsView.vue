<script setup>
import { ref, computed, onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useGlobalSearch } from '@/composables/useGlobalSearch'
import { useAudit } from '@/composables/useAudit'
import { useConfirm } from '@/composables/useConfirm'
import TecnmPagination from '@/components/common/TecnmPagination.vue'
import TecnmAutocomplete from '@/components/common/TecnmAutocomplete.vue'
import TecnmBadge from '@/components/common/TecnmBadge.vue'
import apiClient from '@/services/api'

const authStore = useAuthStore()
const { open: openGlobalSearch } = useGlobalSearch()
const { showAudit } = useAudit()
const { confirm } = useConfirm()

const isStudent = computed(() =>
  authStore.hasRole('student') && !authStore.hasRole('admin', 'departmenthead', 'advisor', 'vinculacion', 'director')
)
const isAdvisor = computed(() =>
  authStore.hasRole('advisor') && !authStore.hasRole('admin', 'departmenthead')
)
const isStaff = computed(() =>
  authStore.hasRole('admin') || authStore.hasRole('departmenthead') || authStore.hasRole('vinculacion') || authStore.hasRole('director')
)
const canEvaluateDoc = computed(() =>
  !authStore.isReadOnly && (isStaff.value || isAdvisor.value)
)

const currentProject = ref(null)
const documents = ref([])
const isLoading = ref(false)
const errorMessage = ref('')
const alertMessage = ref('')
const alertType = ref('info')

// Paginación y Filtros
const pageNumber = ref(1)
const pageSize = ref(10)
const totalCount = ref(0)
const totalPages = ref(0)
const search = ref('')
const sortBy = ref('UploadedAt')
const sortDir = ref('desc')
const includeInactive = ref(false)

// Modales
const isUploadModalOpen = ref(false)
const isPreviewModalOpen = ref(false)
const isStatusModalOpen = ref(false)
const isSubmitting = ref(false)

// Modal Subida
const uploadForm = ref({
  projectId: null,
  documentType: '',
  file: null,
})
const uploadInitialProject = ref(null)
const uploadPreviewUrl = ref(null)
const uploadPreviewType = ref('')

// Modal Vista Previa
const previewDoc = ref(null)
const previewObjectUrl = ref(null)

// Modal Estado
const statusForm = ref({
  id: null,
  typeLabel: '',
  fileName: '',
  status: 'approved',
  rejectionReason: '',
})

const documentTypeLabels = {
  solicitud: 'Solicitud de Residencia',
  carta_presentacion: 'Carta de Presentación',
  carta_aceptacion: 'Carta de Aceptación',
  anteproyecto: 'Anteproyecto Técnico',
  dictamen: 'Dictamen de Aprobación',
  manual_usuario: 'Manual de Usuario',
  manual_tecnico: 'Manual Técnico',
  libranza: 'Oficio de Liberación',
  otro: 'Otro / Evidencia',
}

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

function formatFileSize(bytes) {
  if (!bytes || bytes === 0) return '0 Bytes'
  const k = 1024
  const sizes = ['Bytes', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(2))} ${sizes[i]}`
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
      await loadDocuments()
    } else {
      errorMessage.value =
        'No tienes un proyecto aprobado o en curso para consultar el expediente digital.'
      documents.value = []
    }
  } catch (err) {
    if (err.response?.status === 404) {
      errorMessage.value =
        'No tienes un proyecto aprobado o en curso para consultar el expediente digital.'
    } else {
      errorMessage.value = 'Error al consultar los documentos del expediente.'
    }
    documents.value = []
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
      documents.value = []
      return
    }

    await selectProject(list[0])
  } catch {
    errorMessage.value = 'Haga clic en "Buscar Anteproyecto" para cargar documentos del expediente.'
    currentProject.value = null
    documents.value = []
  } finally {
    isLoading.value = false
  }
}

async function selectProject(project) {
  if (!project || !project.id) return
  currentProject.value = project
  pageNumber.value = 1
  errorMessage.value = ''
  await loadDocuments()

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

async function loadDocuments() {
  if (!currentProject.value?.id) {
    documents.value = []
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
      `/v1/documents/project/${currentProject.value.id}`,
      { params }
    )
    const data = res.data || {}
    documents.value = data.items || []
    totalCount.value = data.totalCount || 0
    totalPages.value = data.totalPages || 0
  } catch (err) {
    errorMessage.value =
      err.response?.data?.message || 'Error al obtener los documentos del expediente.'
    documents.value = []
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
  loadDocuments()
}

function changePage(page) {
  pageNumber.value = page
  loadDocuments()
}

// Modal Subida
function openUploadModal() {
  uploadForm.value = {
    projectId: currentProject.value?.id || null,
    documentType: '',
    file: null,
  }
  uploadInitialProject.value = currentProject.value
    ? { id: currentProject.value.id, title: currentProject.value.title || 'Anteproyecto' }
    : null
  clearLocalPreview()
  isUploadModalOpen.value = true
}

function closeUploadModal() {
  isUploadModalOpen.value = false
  clearLocalPreview()
}

function onFileSelected(e) {
  const file = e.target.files[0]
  if (!file) {
    clearLocalPreview()
    return
  }

  const allowed = ['.pdf', '.jpg', '.jpeg', '.png']
  const ext = '.' + (file.name.split('.').pop() || '').toLowerCase()
  if (!allowed.includes(ext)) {
    showAlert('Solo se permiten archivos en formato PDF, JPG o PNG.', 'danger')
    e.target.value = ''
    clearLocalPreview()
    return
  }

  if (file.size > 10 * 1024 * 1024) {
    showAlert('El archivo seleccionado excede el límite máximo de 10MB.', 'danger')
    e.target.value = ''
    clearLocalPreview()
    return
  }

  uploadForm.value.file = file
  if (uploadPreviewUrl.value) URL.revokeObjectURL(uploadPreviewUrl.value)
  uploadPreviewUrl.value = URL.createObjectURL(file)
  uploadPreviewType.value = file.type.startsWith('image/') ? 'image' : 'pdf'
}

function clearLocalPreview() {
  if (uploadPreviewUrl.value) {
    URL.revokeObjectURL(uploadPreviewUrl.value)
    uploadPreviewUrl.value = null
  }
  uploadPreviewType.value = ''
  uploadForm.value.file = null
}

async function handleUploadSubmit() {
  const projectId = uploadForm.value.projectId || currentProject.value?.id
  if (!projectId) {
    showAlert('Debe seleccionar un proyecto válido.', 'warning')
    return
  }
  if (!uploadForm.value.documentType) {
    showAlert('Debe seleccionar el tipo de documento.', 'warning')
    return
  }
  if (!uploadForm.value.file) {
    showAlert('Seleccione un archivo PDF o imagen válido.', 'danger')
    return
  }

  isSubmitting.value = true
  const formData = new FormData()
  formData.append('projectId', projectId)
  formData.append('documentType', uploadForm.value.documentType)
  formData.append('file', uploadForm.value.file)

  try {
    await apiClient.post('/v1/documents', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    showAlert('¡Documento subido correctamente al expediente!', 'success')
    closeUploadModal()
    await loadDocuments()
  } catch (err) {
    const msg = err.response?.data?.message || 'Error al subir el documento.'
    showAlert(msg, 'danger')
  } finally {
    isSubmitting.value = false
  }
}

// Modal Vista Previa
async function openPreviewModal(doc) {
  previewDoc.value = doc
  isPreviewModalOpen.value = true
  if (previewObjectUrl.value) {
    URL.revokeObjectURL(previewObjectUrl.value)
    previewObjectUrl.value = null
  }

  try {
    const res = await apiClient.get(`/v1/documents/${doc.id}/download`, {
      responseType: 'blob',
    })
    previewObjectUrl.value = URL.createObjectURL(res.data)
  } catch {
    showAlert('No se pudo cargar la vista previa del documento.', 'danger')
  }
}

function closePreviewModal() {
  isPreviewModalOpen.value = false
  if (previewObjectUrl.value) {
    URL.revokeObjectURL(previewObjectUrl.value)
    previewObjectUrl.value = null
  }
  previewDoc.value = null
}

// Modal Estado / Evaluación
async function openStatusModal(doc) {
  const typeLabel = documentTypeLabels[doc.documentType] || doc.documentType
  statusForm.value = {
    id: doc.id,
    typeLabel,
    fileName: doc.fileName,
    status: doc.status || 'approved',
    rejectionReason: doc.rejectionReason || '',
  }
  isStatusModalOpen.value = true
  if (previewObjectUrl.value) {
    URL.revokeObjectURL(previewObjectUrl.value)
    previewObjectUrl.value = null
  }

  try {
    const res = await apiClient.get(`/v1/documents/${doc.id}/download`, {
      responseType: 'blob',
    })
    previewObjectUrl.value = URL.createObjectURL(res.data)
  } catch {}
}

function closeStatusModal() {
  isStatusModalOpen.value = false
  if (previewObjectUrl.value) {
    URL.revokeObjectURL(previewObjectUrl.value)
    previewObjectUrl.value = null
  }
}

async function handleSaveStatus() {
  isSubmitting.value = true
  try {
    const payload = {
      status: statusForm.value.status,
      rejectionReason: statusForm.value.rejectionReason.trim(),
    }
    await apiClient.patch(`/v1/documents/${statusForm.value.id}/status`, payload)
    showAlert('Estado del documento actualizado correctamente.', 'success')
    closeStatusModal()
    await loadDocuments()
  } catch (err) {
    const msg = err.response?.data?.message || 'Error al actualizar estado del documento.'
    showAlert(msg, 'danger')
  } finally {
    isSubmitting.value = false
  }
}

async function handleDeleteDocument(doc) {
  const ok = await confirm({
    title: 'Eliminar Documento',
    message: `¿Está seguro de eliminar el archivo "${doc.fileName}" del expediente? Esta acción no se puede deshacer.`,
    okText: 'Eliminar',
    cancelText: 'Cancelar',
  })
  if (!ok) return

  try {
    await apiClient.delete(`/v1/documents/${doc.id}`)
    showAlert('Documento eliminado del expediente.', 'warning')
    await loadDocuments()
  } catch (err) {
    const msg = err.response?.data?.message || 'Error al eliminar el documento.'
    showAlert(msg, 'danger')
  }
}

function handleOpenAudit(doc) {
  showAudit({
    title: `Auditoría — Documento #${doc.id}`,
    item: {
      ...doc,
      title: `${documentTypeLabels[doc.documentType] || doc.documentType} (${doc.fileName})`,
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
        <h1 class="tecnm-page-title">Expediente Digital de Residencia Profesional</h1>
        <p class="tecnm-page-subtitle">Carga, visualización y dictaminación de documentos oficiales</p>
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
          v-if="!authStore.isReadOnly"
          id="openUploadModalBtn"
          type="button"
          class="tecnm-btn tecnm-btn-primary"
          @click="openUploadModal"
        >
          + Subir Documento
        </button>
      </div>
    </div>

    <!-- Tarjeta Principal con Lista de Documentos -->
    <div class="tecnm-card">
      <div class="tecnm-card-header">
        <h3 class="tecnm-card-title">Documentos Cargados en el Expediente</h3>
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
          <span id="selectedProjectBadge" class="tecnm-badge tecnm-badge-info">
            {{ selectedProjectText }}
          </span>
        </div>

        <div class="tecnm-toolbar-actions">
          <label class="tecnm-switch-label">
            <span class="tecnm-switch">
              <input
                id="documentsIncludeInactiveToggle"
                v-model="includeInactive"
                type="checkbox"
                @change="loadDocuments"
              />
              <span class="tecnm-switch-slider"></span>
            </span>
            Mostrar inactivos
          </label>
        </div>
      </div>

      <div class="tecnm-card-body">
        <div class="tecnm-table-responsive">
          <table id="documentsTable" class="tecnm-table tecnm-table-striped">
            <thead>
              <tr>
                <th
                  data-sort="DocumentType"
                  class="tecnm-sort-th"
                  :class="{
                    'tecnm-sort-asc': sortBy === 'DocumentType' && sortDir === 'asc',
                    'tecnm-sort-desc': sortBy === 'DocumentType' && sortDir === 'desc',
                  }"
                  style="cursor: pointer;"
                  @click="toggleSort('DocumentType')"
                >
                  Tipo de Documento
                </th>
                <th
                  data-sort="FileName"
                  class="tecnm-sort-th"
                  :class="{
                    'tecnm-sort-asc': sortBy === 'FileName' && sortDir === 'asc',
                    'tecnm-sort-desc': sortBy === 'FileName' && sortDir === 'desc',
                  }"
                  style="cursor: pointer;"
                  @click="toggleSort('FileName')"
                >
                  Nombre de Archivo
                </th>
                <th>Tamaño</th>
                <th
                  data-sort="UploadedAt"
                  class="tecnm-sort-th"
                  :class="{
                    'tecnm-sort-asc': sortBy === 'UploadedAt' && sortDir === 'asc',
                    'tecnm-sort-desc': sortBy === 'UploadedAt' && sortDir === 'desc',
                  }"
                  style="cursor: pointer;"
                  @click="toggleSort('UploadedAt')"
                >
                  Fecha de Carga
                </th>
                <th>Estado</th>
                <th>Acciones</th>
              </tr>
            </thead>
            <tbody id="documentsTableBody">
              <tr v-if="isLoading">
                <td colspan="6" class="tecnm-table-empty">
                  Cargando documentos del expediente...
                </td>
              </tr>
              <tr v-else-if="errorMessage">
                <td colspan="6" class="tecnm-table-empty tecnm-text-danger">
                  {{ errorMessage }}
                </td>
              </tr>
              <tr v-else-if="documents.length === 0">
                <td colspan="6" class="tecnm-table-empty">
                  No hay documentos registrados para este proyecto.
                </td>
              </tr>
              <tr
                v-for="doc in documents"
                v-else
                :key="doc.id"
              >
                <td><strong>{{ documentTypeLabels[doc.documentType] || doc.documentType }}</strong></td>
                <td>{{ doc.fileName }}</td>
                <td>{{ formatFileSize(doc.fileSize) }}</td>
                <td>{{ formatTecNMDate(doc.uploadedAt || doc.createdAt) }}</td>
                <td>
                  <TecnmBadge :status="doc.status" />
                </td>
                <td>
                  <div class="tecnm-row-actions">
                    <button
                      type="button"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                      @click="openPreviewModal(doc)"
                    >
                      Vista Previa
                    </button>
                    <a
                      :href="`/api/v1/documents/${doc.id}/download`"
                      :download="doc.fileName"
                      target="_blank"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                      title="Descargar"
                    >
                      ⬇ Descargar
                    </a>
                    <button
                      v-if="canEvaluateDoc"
                      type="button"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                      @click="openStatusModal(doc)"
                    >
                      Evaluar
                    </button>
                    <button
                      type="button"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                      @click="handleOpenAudit(doc)"
                    >
                      Auditoría
                    </button>
                    <button
                      v-if="isStaff"
                      type="button"
                      class="tecnm-btn tecnm-btn-danger tecnm-btn-sm"
                      @click="handleDeleteDocument(doc)"
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

    <!-- Modal Subir Documento -->
    <div
      id="uploadModal"
      class="modal-backdrop"
      :class="{ active: isUploadModalOpen }"
      aria-modal="true"
      role="dialog"
    >
      <div class="modal-card">
        <div class="tecnm-modal-header">
          <h3 class="tecnm-modal-title">Subir Documento al Expediente</h3>
          <button
            id="closeUploadModalBtn"
            type="button"
            class="tecnm-modal-close"
            aria-label="Cerrar"
            @click="closeUploadModal"
          >
            &times;
          </button>
        </div>

        <form id="uploadDocumentForm" @submit.prevent="handleUploadSubmit">
          <div class="tecnm-form-group">
            <label for="uploadProjectId" class="tecnm-label">Proyecto Destino *</label>
            <div id="uploadProjectAutocompleteWrapper">
              <TecnmAutocomplete
                v-model="uploadForm.projectId"
                endpoint="/v1/projects"
                global-search-source="PROJECTS"
                placeholder="Buscar anteproyecto por título o estudiante..."
                :initial-item="uploadInitialProject"
              />
            </div>
          </div>

          <div class="tecnm-form-group">
            <label for="uploadDocumentType" class="tecnm-label">Tipo de Documento *</label>
            <select
              id="uploadDocumentType"
              v-model="uploadForm.documentType"
              class="tecnm-form-control"
              required
            >
              <option value="">-- Seleccionar Tipo --</option>
              <option value="solicitud">Solicitud de Residencia Profesional</option>
              <option value="carta_presentacion">Carta de Presentación</option>
              <option value="carta_aceptacion">Carta de Aceptación</option>
              <option value="anteproyecto">Anteproyecto Técnico</option>
              <option value="dictamen">Dictamen de Aprobación</option>
              <option value="manual_usuario">Manual de Usuario</option>
              <option value="manual_tecnico">Manual Técnico</option>
              <option value="libranza">Oficio de Liberación</option>
              <option value="otro">Otro / Evidencia Adicional</option>
            </select>
          </div>

          <div class="tecnm-form-group">
            <label for="documentFile" class="tecnm-label">Archivo PDF o Imagen (Máximo 10MB) *</label>
            <input
              id="documentFile"
              type="file"
              class="tecnm-form-control"
              accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png"
              required
              @change="onFileSelected"
            />
          </div>

          <div
            v-if="uploadPreviewUrl"
            id="uploadPreviewContainer"
            class="document-preview-container document-preview-compact"
            style="margin-bottom: 1rem; border: 1px solid var(--tecnm-gray-200); border-radius: var(--tecnm-radius-md); overflow: hidden; max-height: 250px; text-align: center;"
          >
            <img
              v-if="uploadPreviewType === 'image'"
              :src="uploadPreviewUrl"
              alt="Vista previa del archivo seleccionado"
              style="max-width: 100%; max-height: 240px; object-fit: contain;"
            />
            <embed
              v-else
              :src="uploadPreviewUrl"
              type="application/pdf"
              style="width: 100%; height: 240px; border: none;"
            />
          </div>

          <div class="tecnm-modal-footer">
            <button
              id="cancelUploadBtn"
              type="button"
              class="tecnm-btn tecnm-btn-secondary"
              @click="closeUploadModal"
            >
              Cancelar
            </button>
            <button
              id="submitUploadBtn"
              type="submit"
              class="tecnm-btn tecnm-btn-primary"
              :disabled="isSubmitting"
            >
              {{ isSubmitting ? 'Subiendo...' : 'Subir Documento' }}
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Modal Vista Previa de Documento -->
    <div
      id="previewModal"
      class="modal-backdrop"
      :class="{ active: isPreviewModalOpen }"
      aria-modal="true"
      role="dialog"
    >
      <div class="modal-card modal-card-wide" style="max-width: 900px; width: 90vw;">
        <div class="tecnm-modal-header">
          <h3 class="tecnm-modal-title">
            Vista Previa: <span id="previewDocName">{{ previewDoc?.fileName }}</span>
          </h3>
          <button
            id="closePreviewModalBtn"
            type="button"
            class="tecnm-modal-close"
            aria-label="Cerrar"
            @click="closePreviewModal"
          >
            &times;
          </button>
        </div>
        <div
          id="previewContainer"
          class="document-preview-container"
          style="min-height: 500px; height: 75vh; width: 100%; display: flex; align-items: center; justify-content: center; background-color: var(--tecnm-gray-100); border-radius: var(--tecnm-radius-sm); overflow: hidden;"
        >
          <div v-if="!previewObjectUrl" class="tecnm-spinner"></div>
          <img
            v-else-if="previewDoc?.fileName?.toLowerCase().endsWith('.png') || previewDoc?.fileName?.toLowerCase().endsWith('.jpg') || previewDoc?.fileName?.toLowerCase().endsWith('.jpeg')"
            :src="previewObjectUrl"
            alt="Vista previa del documento"
            style="max-width: 100%; max-height: 100%; object-fit: contain;"
          />
          <embed
            v-else
            :src="previewObjectUrl"
            type="application/pdf"
            style="width: 100%; height: 100%; border: none;"
          />
        </div>
      </div>
    </div>

    <!-- Modal Revisión de Estado de Documento -->
    <div
      id="statusModal"
      class="modal-backdrop"
      :class="{ active: isStatusModalOpen }"
      aria-modal="true"
      role="dialog"
    >
      <div class="modal-card">
        <div class="tecnm-modal-header">
          <h3 class="tecnm-modal-title">
            Revisar Documento #<span id="statusDocId">{{ statusForm.id }}</span>
          </h3>
          <button
            id="closeStatusModalBtn"
            type="button"
            class="tecnm-modal-close"
            aria-label="Cerrar"
            @click="closeStatusModal"
          >
            &times;
          </button>
        </div>

        <div>
          <p><strong>Tipo:</strong> <span id="statusDocType">{{ statusForm.typeLabel }}</span></p>
          <p><strong>Archivo:</strong> <span id="statusDocName">{{ statusForm.fileName }}</span></p>

          <div
            v-if="previewObjectUrl"
            id="statusPreviewContainer"
            class="document-preview-container document-preview-compact"
            style="margin-bottom: 1rem; border: 1px solid var(--tecnm-gray-200); border-radius: var(--tecnm-radius-md); overflow: hidden; max-height: 200px; text-align: center;"
          >
            <img
              v-if="statusForm.fileName?.toLowerCase().endsWith('.png') || statusForm.fileName?.toLowerCase().endsWith('.jpg') || statusForm.fileName?.toLowerCase().endsWith('.jpeg')"
              :src="previewObjectUrl"
              alt="Vista previa del documento"
              style="max-width: 100%; max-height: 190px; object-fit: contain;"
            />
            <embed
              v-else
              :src="previewObjectUrl"
              type="application/pdf"
              style="width: 100%; height: 190px; border: none;"
            />
          </div>

          <div class="tecnm-form-group">
            <label for="statusSelect" class="tecnm-label">Estado del Documento *</label>
            <select
              id="statusSelect"
              v-model="statusForm.status"
              class="tecnm-form-control"
            >
              <option value="approved">Aprobado</option>
              <option value="rejected">Rechazado</option>
              <option value="under_review">En Revisión</option>
            </select>
          </div>

          <div
            v-if="statusForm.status === 'rejected'"
            id="rejectionReasonGroup"
            class="tecnm-form-group"
          >
            <label for="rejectionReasonInput" class="tecnm-label">Motivo del Rechazo / Observaciones</label>
            <textarea
              id="rejectionReasonInput"
              v-model="statusForm.rejectionReason"
              class="tecnm-form-control"
              rows="3"
              placeholder="Detalle los motivos por los que el documento fue rechazado..."
            ></textarea>
          </div>
        </div>

        <div class="tecnm-modal-footer">
          <button
            id="cancelStatusBtn"
            type="button"
            class="tecnm-btn tecnm-btn-secondary"
            @click="closeStatusModal"
          >
            Cancelar
          </button>
          <button
            id="saveStatusBtn"
            type="button"
            class="tecnm-btn tecnm-btn-success"
            :disabled="isSubmitting"
            @click="handleSaveStatus"
          >
            {{ isSubmitting ? 'Guardando...' : 'Guardar Estado' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

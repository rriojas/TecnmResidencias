<script setup>
import { ref, computed, watch, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useGlobalSearch } from '@/composables/useGlobalSearch'
import { useAudit } from '@/composables/useAudit'
import { useConfirm } from '@/composables/useConfirm'
import TecnmPagination from '@/components/common/TecnmPagination.vue'
import TecnmAutocomplete from '@/components/common/TecnmAutocomplete.vue'
import TecnmBadge from '@/components/common/TecnmBadge.vue'
import apiClient, { getUploadErrorMessage } from '@/services/api'

const route = useRoute()
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
const canAssignAdvisor = computed(() => {
  if (authStore.isReadOnly) return false
  return (
    authStore.isAdmin ||
    authStore.isCareerHead ||
    authStore.hasRole('admin', 'departmenthead', 'jefecarrera', 'careerhead', 'academic')
  )
})

// Fechas límite y estados de formatos para estudiante
const studentDeadlineInfo = ref({
  formato29Deadline: null,
  formato30Deadline: null,
  formato29Status: 'not_uploaded',
  formato29RejectionReason: null,
  formato29V2Status: 'not_uploaded',
  formato29V2RejectionReason: null,
  formato30Status: 'not_uploaded',
  formato30RejectionReason: null,
  canUploadSecondPhase: false,
  isDocumentBlocked: false,
  blockedReason: null
})

async function fetchStudentDeadlineInfo() {
  if (!isStudent.value) return
  try {
    const res = await apiClient.get('/v1/students/me/document-deadlines')
    if (res.data) {
      studentDeadlineInfo.value = res.data
    }
  } catch (err) {
    console.error('Error al cargar fechas límite:', err)
  }
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

// Helper para verificar si la fecha límite ha pasado
function isDeadlinePassed(deadline) {
  if (!deadline) return false
  const now = new Date()
  const deadlineDate = new Date(deadline)
  return now > deadlineDate
}

// Helper para obtener el estado de la fecha límite
function getDeadlineStatus(deadline) {
  if (!deadline) return 'Sin fecha límite'
  if (isDeadlinePassed(deadline)) {
    return 'Vencida'
  }
  const daysLeft = Math.ceil((new Date(deadline) - new Date()) / (1000 * 60 * 60 * 24))
  if (daysLeft <= 3) {
    return `Vence en ${daysLeft} día(s)`
  }
  return `Vence en ${daysLeft} días`
}

// Modo de visualización: 'matrix' (tabla general para staff) o 'detail' (expediente de un proyecto)
const viewMode = ref('matrix')
const matrixItems = ref([])
const matrixLoading = ref(false)
const matrixPage = ref(1)
const matrixPageSize = ref(10)
const matrixTotalCount = ref(0)
const matrixTotalPages = ref(0)
const matrixSearch = ref('')
const careersList = ref([])
const matrixCareerFilter = ref(
  authStore.isCareerHead && authStore.userCareerId
    ? String(authStore.userCareerId)
    : authStore.isCoordinator && authStore.userCareerIds.length > 0
    ? String(authStore.userCareerIds[0])
    : ''
)
const matrixCompletionFilter = ref('')
const isExportingMatrix = ref(false)

const currentProject = ref(null)
const documents = ref([])
const isLoading = ref(false)
const errorMessage = ref('')
const alertMessage = ref('')
const alertType = ref('info')

// Gestión y Carga de Asesor Académico en Expediente
const cartaAceptacionDoc = computed(() => {
  return documents.value.find(
    (d) => ['carta_aceptacion', 'carta_aprobacion'].includes((d.documentType || '').toLowerCase()) && d.isActive
  ) || null
})

const currentAdvisorLoad = ref(null)
const selectedAdvisorId = ref(null)
const initialAdvisorItem = ref(null)
const selectedAdvisorCandidate = ref(null)
const isAssigningAdvisor = ref(false)

async function loadCurrentAdvisorInfo() {
  if (!currentProject.value?.advisorId) {
    currentAdvisorLoad.value = null
    initialAdvisorItem.value = null
    selectedAdvisorId.value = null
    return
  }
  selectedAdvisorId.value = currentProject.value.advisorId
  try {
    const advRes = await apiClient.get(`/v1/advisors/${currentProject.value.advisorId}`)
    currentAdvisorLoad.value = advRes.data?.assignedStudentsCount ?? null
    initialAdvisorItem.value = {
      id: currentProject.value.advisorId,
      fullName: currentProject.value.advisorName,
      assignedStudentsCount: advRes.data?.assignedStudentsCount,
    }
  } catch {
    currentAdvisorLoad.value = null
    initialAdvisorItem.value = {
      id: currentProject.value.advisorId,
      fullName: currentProject.value.advisorName,
    }
  }
}

async function handleAssignAdvisorInExpediente() {
  if (!currentProject.value || !selectedAdvisorId.value) return
  isAssigningAdvisor.value = true
  try {
    await apiClient.post('/v1/advisors/assign', {
      advisorId: Number(selectedAdvisorId.value),
      projectId: currentProject.value.id,
      advisorType: 'internal',
    })
    const res = await apiClient.get(`/v1/projects/${currentProject.value.id}`)
    currentProject.value = res.data
    selectedAdvisorCandidate.value = null
    await loadCurrentAdvisorInfo()
    showAlert('Asesor académico asignado al expediente exitosamente.', 'success')
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al asignar el asesor académico.', 'danger')
  } finally {
    isAssigningAdvisor.value = false
  }
}

watch(
  () => currentProject.value?.advisorId,
  () => {
    loadCurrentAdvisorInfo()
  }
)

const isProjectCompleted = computed(() => {
  const st = String(currentProject.value?.status || '').toLowerCase()
  return st === 'completed' || currentProject.value?.isCompleted === true
})

const isAccreditationProject = computed(() => {
  if (!currentProject.value) return false
  const t = String(currentProject.value?.projectType || '').toLowerCase()
  return t === 'acreditacion_innovatec' || t === 'acreditacion_hackatec'
})

const isAccreditationActive = computed(() => {
  if (!isAccreditationProject.value) return false
  const st = String(currentProject.value?.status || '').toLowerCase()
  return !['rejected', 'cancelled'].includes(st)
})

const isAccreditationUnderReview = computed(() => {
  if (!isAccreditationProject.value) return false
  const st = String(currentProject.value?.status || '').toLowerCase()
  return ['under_review', 'pending', 'proposed'].includes(st) && !currentProject.value?.reviewComments
})

const isAccreditationReturned = computed(() => {
  if (!isAccreditationProject.value) return false
  const st = String(currentProject.value?.status || '').toLowerCase()
  return (st === 'draft' || st === 'rejected') && !!currentProject.value?.reviewComments
})

const isAccreditationCompleted = computed(() => {
  if (!isAccreditationProject.value) return false
  const st = String(currentProject.value?.status || '').toLowerCase()
  return st === 'completed' || currentProject.value?.isCompleted === true
})

const isAccreditationDenied = computed(() => {
  if (!isAccreditationProject.value) return false
  const st = String(currentProject.value?.status || '').toLowerCase()
  return (st === 'rejected' || st === 'cancelled') && !currentProject.value?.reviewComments
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

const isProjectApproved = computed(() => {
  const st = String(currentProject.value?.status || '').toLowerCase()
  return ['approved', 'aprobado', 'in_progress', 'inprogress', 'en_progreso', 'completed', 'completado'].includes(st)
})

const isProjectReadOnly = computed(() => {
  if (!currentProject.value) return true
  const st = String(currentProject.value?.status || '').toLowerCase()
  return isProjectCompleted.value || st === 'cancelled'
})

const canUploadDocument = computed(() => {
  if (authStore.hasRole('vinculacion')) return false
  if (isStaff.value) return true
  if (isAdvisor.value) return false
  if (!currentProject.value?.id) return false
  // Los estudiantes con InnovaTecNM pueden seguir subiendo diplomas/evidencias libremente
  if (isAccreditationActive.value) {
    const st = String(currentProject.value?.status || '').toLowerCase()
    return st !== 'cancelled'
  }
  return !isProjectReadOnly.value
})

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
  carta_aceptacion: 'Carta de Aceptación',
  dictamen: 'Dictamen de Aprobación',
  manual_usuario: 'Manual de Usuario',
  manual_tecnico: 'Manual Técnico',
  libranza: 'Oficio de Liberación',
  formato_29: 'Formato 29 (Primer Seguimiento)',
  formato_29v2: 'Formato 29 (Segundo Seguimiento)',
  formato_30: 'Formato 30 (Evaluación Final)',
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

async function loadCareers() {
  try {
    const res = await apiClient.get('/v1/careers/all')
    careersList.value = res.data || []
  } catch {}
}

async function loadMatrix() {
  matrixLoading.value = true
  try {
    const params = {
      pageNumber: matrixPage.value,
      pageSize: matrixPageSize.value,
      search: matrixSearch.value.trim(),
    }
    if (matrixCareerFilter.value) {
      params.careerId = matrixCareerFilter.value
    }
    if (matrixCompletionFilter.value) {
      params.completionStatus = matrixCompletionFilter.value
    }
    const res = await apiClient.get('/v1/documents/matrix', { params })
    const data = res.data || {}
    matrixItems.value = data.items || []
    matrixTotalCount.value = data.totalCount || 0
    matrixTotalPages.value = data.totalPages || 0
  } catch (err) {
    console.error('Error al cargar matriz de expedientes:', err)
    showAlert('Error al consultar matriz de expedientes.', 'danger')
  } finally {
    matrixLoading.value = false
  }
}

function onMatrixPageChange(p) {
  matrixPage.value = p
  loadMatrix()
}

function handleMatrixSearch() {
  matrixPage.value = 1
  loadMatrix()
}

async function handleExportMatrixExcel() {
  if (isExportingMatrix.value) return
  isExportingMatrix.value = true
  try {
    const params = {}
    if (matrixSearch.value.trim()) {
      params.search = matrixSearch.value.trim()
    }
    if (matrixCareerFilter.value) {
      params.careerId = matrixCareerFilter.value
    }
    if (matrixCompletionFilter.value) {
      params.completionStatus = matrixCompletionFilter.value
    }

    const res = await apiClient.get('/v1/documents/matrix/export', {
      params,
      responseType: 'blob',
    })

    const blob = new Blob([res.data], {
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    })
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `expedientes_digitales_${new Date().toISOString().slice(0, 10)}.xlsx`
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    window.URL.revokeObjectURL(url)
    showAlert('Archivo Excel descargado correctamente.', 'success')
  } catch (err) {
    showAlert('Error al descargar el archivo Excel de expedientes.', 'danger')
  } finally {
    isExportingMatrix.value = false
  }
}

async function openProjectDetail(item) {
  isLoading.value = true
  try {
    const res = await apiClient.get(`/v1/projects/${item.projectId}`)
    currentProject.value = res.data
    viewMode.value = 'detail'
    pageNumber.value = 1
    await loadDocuments()
    await loadCurrentAdvisorInfo()
  } catch (err) {
    showAlert('No se pudo cargar el expediente del proyecto.', 'danger')
  } finally {
    isLoading.value = false
  }
}

function backToMatrix() {
  viewMode.value = 'matrix'
  loadMatrix()
}

async function initPage() {
  if (isStudent.value) {
    viewMode.value = 'detail'
    await resolveStudentProject()
  } else {
    await loadCareers()
    const qPid = route.query.projectId
    if (qPid) {
      try {
        const res = await apiClient.get(`/v1/projects/${qPid}`)
        if (res.data) {
          currentProject.value = res.data
          viewMode.value = 'detail'
          await loadDocuments()
          return
        }
      } catch {}
    }
    viewMode.value = 'matrix'
    await loadMatrix()
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
      currentProject.value = null
      documents.value = []
    }
  } catch (err) {
    currentProject.value = null
    documents.value = []
    if (err.response?.status === 404) {
      // Sin anteproyecto
    } else {
      errorMessage.value = 'Error al consultar los documentos del expediente.'
    }
  } finally {
    isLoading.value = false
  }
}

const isEmptyState = ref(false)

async function loadInitialProjectForStaff() {
  isLoading.value = true
  errorMessage.value = ''
  isEmptyState.value = false
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
      isEmptyState.value = true
      currentProject.value = null
      documents.value = []
      return
    }

    await selectProject(list[0])
  } catch (err) {
    if (err.response?.status === 404 || !err.response) {
      isEmptyState.value = true
    } else {
      errorMessage.value = 'Error al consultar documentos del expediente.'
    }
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
  await loadCurrentAdvisorInfo()
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
    const items = data.items || []
    documents.value = items.filter(
      (d) => d.documentType !== 'anteproyecto' && d.documentType !== 'carta_presentacion'
    )
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
  if (sortBy.value.toLowerCase() === field.toLowerCase()) {
    sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
  } else {
    sortBy.value = field
    sortDir.value = 'asc'
  }
  pageNumber.value = 1
  loadDocuments()
}
const handleSort = toggleSort

function changePage(page) {
  pageNumber.value = page
  loadDocuments()
}

// Modal Subida
async function openUploadModal() {
  if (!currentProject.value?.id) {
    showAlert('Debe seleccionar o registrar un anteproyecto primero.', 'warning')
    return
  }

  // Estudiantes InnovaTec pueden subir diplomas continuamente sin bloqueos ni alertas
  if (isAccreditationActive.value) {
    const st = String(currentProject.value?.status || '').toLowerCase()
    if (st === 'cancelled') {
      showAlert('El anteproyecto se encuentra cancelado. No se permiten cargas.', 'warning')
      return
    }
    uploadForm.value = {
      projectId: currentProject.value?.id || null,
      documentType: 'constancia_acreditacion',
      file: null,
    }
    uploadInitialProject.value = currentProject.value
      ? { id: currentProject.value.id, title: currentProject.value.title || 'Anteproyecto' }
      : null
    clearLocalPreview()
    isUploadModalOpen.value = true
    return
  }

  if (isStudent.value) {
    await fetchStudentDeadlineInfo()
    if (studentDeadlineInfo.value.isDocumentBlocked) {
      showAlert(studentDeadlineInfo.value.blockedReason || 'Acceso restringido: Las fechas límite han vencido. Únicamente puede cargar los formatos requeridos pendientes.', 'warning')
    }
  }

  if (!isStaff.value && isProjectReadOnly.value) {
    if (isProjectCompleted.value) {
      showAlert('El expediente de este proyecto concluido se encuentra en modo solo lectura.', 'info')
    } else {
      showAlert('El anteproyecto se encuentra cancelado. No se permiten cargas.', 'warning')
    }
    return
  }

  let defaultDocType = !isProjectApproved.value ? 'carta_aceptacion' : ''
  if (isStudent.value && studentDeadlineInfo.value.isDocumentBlocked) {
    if (studentDeadlineInfo.value.formato29Status !== 'approved') {
      defaultDocType = 'formato_29'
    } else if (studentDeadlineInfo.value.formato29V2Status !== 'approved' && studentDeadlineInfo.value.formato30Status === 'approved') {
      defaultDocType = 'formato_29v2'
    } else if (studentDeadlineInfo.value.formato30Status !== 'approved' && studentDeadlineInfo.value.formato29V2Status === 'approved') {
      defaultDocType = 'formato_30'
    } else {
      defaultDocType = 'formato_29v2'
    }
  }

  uploadForm.value = {
    projectId: currentProject.value?.id || null,
    documentType: defaultDocType,
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
    showAlert('No se pudo seleccionar: formato no permitido (solo PDF, JPG o PNG).', 'danger')
    e.target.value = ''
    clearLocalPreview()
    return
  }

  if (file.size > 5 * 1024 * 1024) {
    showAlert('No se pudo seleccionar: el archivo supera el límite permitido de 5MB.', 'danger')
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

  // Validación de orden de formatos para estudiantes
  if (isStudent.value) {
    // Obtener documentos existentes para este proyecto
    const existingDocs = documents.value || []
    const tipoSeleccionado = uploadForm.value.documentType.toLowerCase()

    // Regla: Formato 29v2 y Formato 30 requieren que Formato 29 esté aprobado por la Coordinación
    if (tipoSeleccionado === 'formato_29v2' || tipoSeleccionado === 'formato_30') {
      if (!studentDeadlineInfo.value.canUploadSecondPhase) {
        showAlert('No se puede subir Formato 29 (segunda entrega) ni Formato 30. Primero debe ser validado y aprobado el Formato 29 por la Coordinación.', 'warning')
        return
      }
    }

    // Regla: Restricción de subida si fecha límite ha vencido
    if (studentDeadlineInfo.value.isDocumentBlocked) {
      if (studentDeadlineInfo.value.formato29Status !== 'approved' && tipoSeleccionado !== 'formato_29') {
        showAlert('La fecha límite del Formato 29 ha vencido. Únicamente tiene permitido cargar el Formato 29 pendiente.', 'warning')
        return
      }
      if ((studentDeadlineInfo.value.formato29V2Status !== 'approved' || studentDeadlineInfo.value.formato30Status !== 'approved') &&
          tipoSeleccionado !== 'formato_29v2' && tipoSeleccionado !== 'formato_30') {
        showAlert('La fecha límite para los formatos finales ha vencido. Únicamente tiene permitido cargar el Formato 29v2 o Formato 30.', 'warning')
        return
      }
    }
  }

  if (isStudent.value && !isProjectApproved.value) {
    const preApprovalAllowed = ['carta_aceptacion', 'otro']
    if (!preApprovalAllowed.includes(uploadForm.value.documentType)) {
      showAlert('En esta etapa previa al dictamen, solo se requiere subir tu Carta de Aceptación de la empresa.', 'warning')
      return
    }
  }

  isSubmitting.value = true
  const formData = new FormData()
  formData.append('projectId', projectId)
  formData.append('documentType', uploadForm.value.documentType)
  formData.append('file', uploadForm.value.file)

  try {
    await apiClient.post('/v1/documents', formData)
    showAlert('¡Documento subido correctamente al expediente!', 'success')
    closeUploadModal()
    if (viewMode.value === 'matrix') {
      await loadMatrix()
    }
    if (currentProject.value?.id) {
      await loadDocuments()
    }
  } catch (err) {
    showAlert(getUploadErrorMessage(err, 'subir el documento'), 'danger')
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

    // Si se dictaminó como aprobado y se seleccionó un asesor nuevo
    if (
      statusForm.value.status === 'approved' &&
      canAssignAdvisor.value &&
      currentProject.value &&
      selectedAdvisorId.value &&
      Number(selectedAdvisorId.value) !== Number(currentProject.value.advisorId)
    ) {
      try {
        await apiClient.post('/v1/advisors/assign', {
          advisorId: Number(selectedAdvisorId.value),
          projectId: currentProject.value.id,
          advisorType: 'internal',
        })
        const res = await apiClient.get(`/v1/projects/${currentProject.value.id}`)
        currentProject.value = res.data
        await loadCurrentAdvisorInfo()
      } catch (assignErr) {
        console.warn('Error al auto-asignar asesor desde modal de evaluación:', assignErr)
      }
    }

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
          v-if="!isStudent && viewMode === 'detail'"
          type="button"
          class="tecnm-btn tecnm-btn-secondary"
          @click="backToMatrix"
        >
          &larr; Ver Lista General
        </button>
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
          v-if="!authStore.isReadOnly && canUploadDocument"
          id="openUploadModalBtn"
          type="button"
          class="tecnm-btn tecnm-btn-primary"
          @click="openUploadModal"
        >
          + Subir Documento
        </button>
      </div>
    </div>

    <!-- Banner Contextual según Estado del Proyecto (Solo para Estudiante) -->
    <template v-if="isStudent">
      <!-- Casos para Modalidad InnovaTecNM Nacional -->
      <template v-if="isAccreditationProject">
        <div v-if="isAccreditationCompleted" class="tecnm-alert tecnm-alert-success" role="alert" style="margin-bottom: 1rem;">
          <span><strong>Residencia Acreditada y Liberada al 100% por InnovaTecNM Nacional:</strong> Tu constancia oficial fue validada por la Jefatura. Tu expediente digital se encuentra exento de carta de aceptación, solicitud y anexos adicionales.</span>
        </div>
        <div v-else-if="isAccreditationReturned" class="tecnm-alert tecnm-alert-warning" role="alert" style="margin-bottom: 1rem;">
          <div class="tecnm-d-flex tecnm-justify-between tecnm-align-center tecnm-flex-wrap tecnm-gap-2">
            <span><strong>Constancia de InnovaTecNM Nacional con Observaciones:</strong> La Jefatura de Carrera solicitó correcciones a tu constancia: <em>{{ currentProject?.reviewComments }}</em>.</span>
            <router-link to="/dashboard" class="tecnm-btn tecnm-btn-primary tecnm-btn-sm">
              Sustituir en el Panel &rarr;
            </router-link>
          </div>
        </div>
        <div v-else-if="isAccreditationUnderReview" class="tecnm-alert tecnm-alert-info" role="alert" style="margin-bottom: 1rem;">
          <span><strong>Trámite de InnovaTecNM Nacional en Revisión:</strong> Tu constancia oficial está siendo analizada por la Jefatura de Carrera. Por tu modalidad de residencia, <strong>no requieres subir carta de aceptación ni ningún tipo de formato ordinario</strong>.</span>
        </div>
        <div v-else-if="isAccreditationDenied" class="tecnm-alert tecnm-alert-danger" role="alert" style="margin-bottom: 1rem;">
          <span><strong>Acreditación por InnovaTecNM No Aprobada:</strong> Tu constancia no fue validada. Se han reactivado las opciones ordinarias y la entrega de formatos.</span>
        </div>
      </template>

      <!-- Casos para Flujo Ordinario -->
      <template v-else>
        <div v-if="isProjectCompleted" class="tecnm-alert tecnm-alert-success" role="alert" style="margin-bottom: 1rem;">
          <span><strong>Expediente Digital Concluido:</strong> Este proyecto de residencia profesional ha sido finalizado. Puedes consultar y descargar todos los documentos y evidencias registradas.</span>
        </div>
        <div v-else-if="isProjectPending" class="tecnm-alert tecnm-alert-info" role="alert" style="margin-bottom: 1rem;">
          <span><strong>Anteproyecto en Dictamen:</strong> Tu solicitud se encuentra en revisión. Ya puedes subir tu <strong>Carta de Aceptación</strong> para que el Jefe de Carrera la revise. Los formatos restantes se habilitarán tras la aprobación.</span>
        </div>
        <div v-else-if="isProjectDraft" class="tecnm-alert tecnm-alert-info" role="alert" style="margin-bottom: 1rem;">
          <span><strong>Anteproyecto en Borrador:</strong> Puedes subir tu <strong>Carta de Aceptación</strong> desde aquí o revisar tu solicitud en <router-link to="/projects/proposal"><strong>Solicitud de Anteproyecto</strong></router-link>.</span>
        </div>
        <div v-else-if="isProjectRejected" class="tecnm-alert tecnm-alert-warning" role="alert" style="margin-bottom: 1rem;">
          <span><strong>Anteproyecto con Observaciones:</strong> Puedes subir una versión corregida de tu <strong>Carta de Aceptación</strong> o atender las observaciones en tu <router-link to="/projects/proposal"><strong>Solicitud de Anteproyecto</strong></router-link>.</span>
        </div>
      </template>

      <div v-if="!currentProject && !isLoading" class="tecnm-alert tecnm-alert-info" role="alert" style="margin-bottom: 1rem; display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 0.5rem;">
        <span><strong>Sin Anteproyecto Registrado:</strong> Aún no cuentas con un anteproyecto para consultar el expediente digital.</span>
        <router-link to="/projects/proposal" class="tecnm-btn tecnm-btn-primary tecnm-btn-sm">
          + Registrar Solicitud de Anteproyecto
        </router-link>
      </div>
    </template>

    <!-- ======================================================== -->
    <!-- TABLA GENERAL: MATRIZ DE EXPEDIENTES DIGITALES POR RESIDENTE -->
    <!-- ======================================================== -->
    <div v-if="!isStudent && viewMode === 'matrix'" class="tecnm-card">
      <div class="tecnm-card-header">
        <div class="tecnm-d-flex tecnm-align-center tecnm-gap-2">
          <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" class="tecnm-header-icon" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M3.75 9.776c.112-.017.227-.026.344-.026h15.812c.117 0 .232.009.344.026m-16.5 0a2.25 2.25 0 0 0-1.883 2.542l.857 6a2.25 2.25 0 0 0 2.227 1.932H19.05a2.25 2.25 0 0 0 2.227-1.932l.857-6a2.25 2.25 0 0 0-1.883-2.542m-16.5 0V6A2.25 2.25 0 0 1 6 3.75h3.879a1.5 1.5 0 0 1 1.06.44l2.122 2.12a1.5 1.5 0 0 0 1.06.44H18A2.25 2.25 0 0 1 20.25 9v.776" />
          </svg>
          <h3 class="tecnm-card-title">Matriz de Expedientes Digitales por Residente</h3>
        </div>
        <div class="tecnm-d-flex tecnm-align-center tecnm-gap-2">
          <span class="tecnm-badge tecnm-badge-neutral">{{ matrixTotalCount }} Residentes Registrados</span>
        </div>
      </div>

      <div class="tecnm-card-toolbar" style="flex-wrap: wrap; gap: 0.75rem;">
        <div class="tecnm-search-box" style="flex: 1; min-width: 250px;">
          <input
            v-model="matrixSearch"
            type="search"
            class="tecnm-form-control tecnm-form-control-sm"
            placeholder="Buscar por estudiante, no. de control o anteproyecto..."
            @keyup.enter="handleMatrixSearch"
          />
        </div>

        <select
          v-if="!authStore.isCareerHead"
          v-model="matrixCareerFilter"
          class="tecnm-form-control tecnm-form-control-sm"
          style="width: auto; min-width: 180px;"
          @change="handleMatrixSearch"
        >
          <option value="">Todas las Carreras</option>
          <option v-for="c in careersList" :key="c.id" :value="String(c.id)">
            {{ c.name }}
          </option>
        </select>

        <select
          v-model="matrixCompletionFilter"
          class="tecnm-form-control tecnm-form-control-sm"
          style="width: auto; min-width: 190px;"
          @change="handleMatrixSearch"
        >
          <option value="">Todos los Estados</option>
          <option value="completed">Completados (100% de archivos)</option>
          <option value="incomplete">Incompletos (Archivos faltantes)</option>
        </select>

        <button type="button" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" @click="handleMatrixSearch">
          Filtrar
        </button>

        <button
          id="exportMatrixExcelBtn"
          type="button"
          class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
          :disabled="isExportingMatrix || matrixLoading"
          title="Descargar matriz completa en Excel con los filtros seleccionados"
          @click="handleExportMatrixExcel"
        >
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M3 16.5v2.25A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75V16.5M16.5 12 12 16.5m0 0L7.5 12m4.5 4.5V3" />
          </svg>
          <span>{{ isExportingMatrix ? 'Descargando...' : 'Descargar Excel' }}</span>
        </button>
      </div>

      <div class="tecnm-card-body tecnm-p-0">
        <div class="tecnm-table-responsive">
          <table class="tecnm-table tecnm-table-striped">
            <thead>
              <tr>
                <th>Estudiante</th>
                <th>Carrera</th>
                <th title="Solicitud de Residencia">Solicitud</th>
                <th title="Carta de Aceptación">C. Aceptación</th>
                <th title="Dictamen de Aprobación">Dictamen</th>
                <th title="Oficio de Liberación">Liberación</th>
                <th>Estatus Expediente</th>
                <th style="text-align: right;">Acciones</th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="matrixLoading">
                <td colspan="8" class="tecnm-table-empty">Cargando matriz de expedientes...</td>
              </tr>
              <tr v-else-if="matrixItems.length === 0">
                <td colspan="8" class="tecnm-table-empty">No se encontraron expedientes con los criterios seleccionados.</td>
              </tr>
              <tr v-for="item in matrixItems" v-else :key="item.projectId">
                <td>
                  <strong>{{ item.studentName }}</strong>
                  <div class="tecnm-text-sub">Ctrl: {{ item.studentControlNumber }}</div>
                </td>
                <td>
                  <span>{{ item.careerName }}</span>
                </td>
                <td>
                  <span v-if="item.isAccreditation" class="tecnm-badge tecnm-badge-secondary" title="Exento por InnovaTecNM">Exento</span>
                  <span v-else-if="item.documents['solicitud']" class="tecnm-badge tecnm-badge-success" title="Subido">Subido</span>
                  <span v-else class="tecnm-badge tecnm-badge-warning" style="font-weight: 600;" title="Sin entregar">Faltante</span>
                </td>
                <td>
                  <span v-if="item.isAccreditation" class="tecnm-badge tecnm-badge-secondary" title="Exento por InnovaTecNM">Exento</span>
                  <span v-else-if="item.documents['carta_aceptacion']" class="tecnm-badge tecnm-badge-success" title="Subido">Subido</span>
                  <span v-else class="tecnm-badge tecnm-badge-warning" style="font-weight: 600;" title="Sin entregar">Faltante</span>
                </td>
                <td>
                  <span v-if="item.isAccreditation" class="tecnm-badge tecnm-badge-secondary" title="Exento por InnovaTecNM">Exento</span>
                  <span v-else-if="item.documents['dictamen']" class="tecnm-badge tecnm-badge-success" title="Subido">Subido</span>
                  <span v-else class="tecnm-badge tecnm-badge-warning" style="font-weight: 600;" title="Sin entregar">Faltante</span>
                </td>
                <td>
                  <span v-if="item.isAccreditation" class="tecnm-badge tecnm-badge-secondary" title="Exento por InnovaTecNM">Exento</span>
                  <span v-else-if="item.documents['libranza']" class="tecnm-badge tecnm-badge-success" title="Subido">Subido</span>
                  <span v-else class="tecnm-badge tecnm-badge-warning" style="font-weight: 600;" title="Sin entregar">Faltante</span>
                </td>
                <td>
                  <span v-if="item.isCompleted" class="tecnm-badge tecnm-badge-success" style="font-weight: 700;">
                    {{ item.isAccreditation ? '✓ Acreditado (InnovaTec)' : '✓ Completado' }}
                  </span>
                  <span v-else class="tecnm-badge tecnm-badge-warning">
                    Incompleto ({{ item.uploadedCount }}/{{ item.requiredCount }})
                  </span>
                </td>
                <td style="text-align: right;">
                  <button
                    type="button"
                    class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                    @click="openProjectDetail(item)"
                  >
                    Ver Archivos &rarr;
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div class="tecnm-card-footer">
        <TecnmPagination
          :current-page="matrixPage"
          :total-pages="matrixTotalPages"
          :total-count="matrixTotalCount"
          :page-size="matrixPageSize"
          @update:current-page="onMatrixPageChange"
          @page-change="loadMatrix"
        />
      </div>
    </div>

    <!-- ======================================================== -->
    <!-- DETALLE DE EXPEDIENTE: ARCHIVOS DE UN ANTEPROYECTO -->
    <!-- ======================================================== -->
    <template v-if="isStudent || viewMode === 'detail'">
      <div v-if="!isStudent" class="tecnm-mb-3">
        <button type="button" class="tecnm-btn tecnm-btn-outline tecnm-btn-sm" @click="backToMatrix">
          &larr; Volver a la Lista General de Alumnos y Archivos
        </button>
      </div>

      <!-- Banner de Bloqueo por Fecha Límite en Expediente -->
      <div
        v-if="isStudent && studentDeadlineInfo.isDocumentBlocked && !isAccreditationProject"
        class="tecnm-card tecnm-mb-3"
        style="border-left: 5px solid #dc2626; background: #fff5f5;"
      >
        <div class="tecnm-card-body" style="display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 1rem;">
          <div style="display: flex; align-items: center; gap: 0.75rem;">
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" fill="none" viewBox="0 0 24 24" stroke="#dc2626" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126ZM12 15.75h.007v.008H12v-.008Z" />
            </svg>
            <div>
              <strong style="color: #dc2626; font-size: 1rem;">Entrega Retrasada: Sistema Restringido</strong>
              <p style="margin: 0.25rem 0 0 0; color: #7f1d1d; font-size: 0.875rem;">
                {{ studentDeadlineInfo.blockedReason || 'La fecha límite de formatos obligatorios ha vencido. Sus operaciones se limitan a subir o corregir los formatos pendientes.' }}
              </p>
            </div>
          </div>
          <button type="button" class="tecnm-btn tecnm-btn-primary tecnm-btn-sm" @click="openUploadModal">
            Subir Formato Requerido
          </button>
        </div>
      </div>

      <!-- Widget Institucional: Asignación y Carga de Asesor Académico -->
      <div v-if="currentProject" class="tecnm-card" style="margin-bottom: 1.25rem; border: 1px solid var(--tecnm-border-color, #e2e8f0);">
        <div class="tecnm-card-header" style="background: var(--tecnm-bg-light, #f8fafc); padding: 0.75rem 1.25rem; display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 0.5rem;">
          <h3 class="tecnm-card-title" style="font-size: 0.95rem; margin: 0; display: flex; align-items: center; gap: 0.5rem;">
            <span>Asesor Académico Asignado</span>
          </h3>
          <span v-if="currentProject.advisorName && currentAdvisorLoad !== null" class="tecnm-badge tecnm-badge-info" style="font-size: 0.8rem;">
            Carga docente: {{ currentAdvisorLoad }} alumno{{ currentAdvisorLoad === 1 ? '' : 's' }}
          </span>
        </div>
        <div class="tecnm-card-body" style="padding: 1rem 1.25rem;">
          <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 1rem; align-items: center;">
            <div>
              <div class="tecnm-text-muted" style="font-size: 0.75rem; text-transform: uppercase; font-weight: 600; margin-bottom: 0.25rem;">
                Docente Asesor
              </div>
              <div style="display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap;">
                <span v-if="currentProject.advisorName" class="tecnm-badge tecnm-badge-success" style="font-size: 0.88rem;">
                  {{ currentProject.advisorName }}
                </span>
                <span v-else class="tecnm-badge tecnm-badge-warning" style="font-size: 0.88rem;">
                  Pendiente de Asignación
                </span>
                <span v-if="cartaAceptacionDoc" class="tecnm-badge tecnm-badge-outline" style="font-size: 0.75rem;">
                  Carta Aceptación: {{ cartaAceptacionDoc.status === 'approved' ? 'Aprobada' : 'Cargada' }}
                </span>
              </div>
            </div>

            <!-- Acciones de asignación para Staff con Carta de Aceptación -->
            <div v-if="canAssignAdvisor">
              <template v-if="cartaAceptacionDoc">
                <div style="display: flex; gap: 0.5rem; align-items: center; flex-wrap: wrap;">
                  <div style="flex: 1; min-width: 220px;">
                    <TecnmAutocomplete
                      v-model="selectedAdvisorId"
                      endpoint="/v1/advisors"
                      global-search-source="ADVISORS"
                      placeholder="Buscar docente asesor..."
                      :initial-item="initialAdvisorItem"
                      @select="item => selectedAdvisorCandidate = item"
                      @clear="selectedAdvisorCandidate = null"
                    />
                  </div>
                  <button
                    type="button"
                    class="tecnm-btn tecnm-btn-primary tecnm-btn-sm"
                    :disabled="isAssigningAdvisor || !selectedAdvisorId || Number(selectedAdvisorId) === Number(currentProject.advisorId)"
                    @click="handleAssignAdvisorInExpediente"
                  >
                    {{ currentProject.advisorName ? 'Cambiar Asesor' : 'Asignar Asesor' }}
                  </button>
                </div>
                <div
                  v-if="selectedAdvisorCandidate && (selectedAdvisorCandidate.assignedStudentsCount !== undefined || selectedAdvisorCandidate.assigned_students_count !== undefined)"
                  class="tecnm-text-muted"
                  style="margin-top: 0.35rem; font-size: 0.8rem;"
                >
                  <span>Carga docente del seleccionado: </span>
                  <strong style="color: var(--tecnm-blue-primary, #1b396a);">
                    {{ selectedAdvisorCandidate.assignedStudentsCount ?? selectedAdvisorCandidate.assigned_students_count }} alumnos asignados
                  </strong>
                </div>
              </template>
              <div v-else class="tecnm-text-muted" style="font-size: 0.82rem;">
                <em>La asignación de asesor se habilitará al contar con la Carta de Aceptación en el expediente.</em>
              </div>
            </div>
          </div>
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
          <span v-if="currentProject" id="selectedProjectBadge" class="tecnm-badge" :class="projectStatusBadgeClass">
            {{ selectedProjectText }} — [{{ projectStatusLabel }}]
          </span>
        </div>

        <div class="tecnm-toolbar-actions">
          <label v-if="!authStore.isCareerHead" class="tecnm-switch-label">
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
                    'tecnm-sort-asc': sortBy.toLowerCase() === 'documenttype' && sortDir === 'asc',
                    'tecnm-sort-desc': sortBy.toLowerCase() === 'documenttype' && sortDir === 'desc',
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
                    'tecnm-sort-asc': sortBy.toLowerCase() === 'filename' && sortDir === 'asc',
                    'tecnm-sort-desc': sortBy.toLowerCase() === 'filename' && sortDir === 'desc',
                  }"
                  style="cursor: pointer;"
                  @click="toggleSort('FileName')"
                >
                  Nombre de Archivo
                </th>
                <th
                  data-sort="FileSize"
                  class="tecnm-sort-th"
                  :class="{
                    'tecnm-sort-asc': sortBy.toLowerCase() === 'filesize' && sortDir === 'asc',
                    'tecnm-sort-desc': sortBy.toLowerCase() === 'filesize' && sortDir === 'desc',
                  }"
                  style="cursor: pointer;"
                  @click="toggleSort('FileSize')"
                >
                  Tamaño
                </th>
                <th
                  data-sort="UploadedAt"
                  class="tecnm-sort-th"
                  :class="{
                    'tecnm-sort-asc': sortBy.toLowerCase() === 'uploadedat' && sortDir === 'asc',
                    'tecnm-sort-desc': sortBy.toLowerCase() === 'uploadedat' && sortDir === 'desc',
                  }"
                  style="cursor: pointer;"
                  @click="toggleSort('UploadedAt')"
                >
                  Fecha de Carga
                </th>
                <th
                  data-sort="Status"
                  class="tecnm-sort-th"
                  :class="{
                    'tecnm-sort-asc': sortBy.toLowerCase() === 'status' && sortDir === 'asc',
                    'tecnm-sort-desc': sortBy.toLowerCase() === 'status' && sortDir === 'desc',
                  }"
                  style="cursor: pointer;"
                  @click="toggleSort('Status')"
                >
                  Estado
                </th>
                <th>Acciones</th>
              </tr>
            </thead>
            <tbody id="documentsTableBody">
              <tr v-if="isLoading">
                <td colspan="6" class="tecnm-table-empty">
                  Cargando documentos del expediente...
                </td>
              </tr>
              <tr v-else-if="isEmptyState || !currentProject">
                <td colspan="6" style="padding: 3rem 1.5rem; text-align: center;">
                  <div style="max-width: 500px; margin: 0 auto; display: flex; flex-direction: column; align-items: center; gap: 0.75rem;">
                    <div style="width: 54px; height: 54px; border-radius: 50%; background: #e0f2fe; color: #0284c7; display: flex; align-items: center; justify-content: center;">
                      <svg xmlns="http://www.w3.org/2000/svg" width="28" height="28" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.8">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m2.25 0H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z" />
                      </svg>
                    </div>
                    <h4 style="margin: 0; font-size: 1.1rem; font-weight: 600; color: #1e293b;">
                      {{ authStore.isCareerHead ? 'Sin expedientes activos en tu carrera' : 'Sin anteproyecto seleccionado' }}
                    </h4>
                    <p style="margin: 0; font-size: 0.875rem; color: #64748b; line-height: 1.4;">
                      {{ authStore.isCareerHead
                        ? 'No se registran expedientes vigentes de estudiantes de tu carrera con documentos cargados. Los expedientes de proyectos aprobados se reflejarán aquí para tu consulta.'
                        : 'No se encontraron anteproyectos asignados. Puedes buscar manualmente un anteproyecto para consultar o cargar sus documentos.' }}
                    </p>
                    <button
                      v-if="!isStudent"
                      type="button"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                      style="margin-top: 0.5rem;"
                      @click="openProjectPicker"
                    >
                      <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                        <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
                      </svg>
                      <span>Buscar Anteproyecto</span>
                    </button>
                    <router-link v-else to="/projects/proposal" class="tecnm-btn tecnm-btn-primary tecnm-btn-sm" style="margin-top: 0.5rem;">
                      Registrar Solicitud de Anteproyecto
                    </router-link>
                  </div>
                </td>
              </tr>
              <tr v-else-if="errorMessage">
                <td colspan="6" class="tecnm-table-empty tecnm-text-danger">
                  {{ errorMessage }}
                </td>
              </tr>
              <tr v-else-if="documents.length === 0">
                <td colspan="6" class="tecnm-table-empty">
                  <span v-if="isProjectCompleted && !isAccreditationProject">No hay documentos registrados en este expediente concluido.</span>
                  <span v-else-if="isAccreditationProject && !isAccreditationDenied">La acreditación por InnovaTecNM Nacional no requiere entrega de documentos ordinarios adicionales.</span>
                  <span v-else-if="isProjectPending">El expediente se habilitará una vez aprobado el anteproyecto.</span>
                  <span v-else>No hay documentos registrados para este proyecto. Haga clic en "+ Subir Documento".</span>
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
                      v-if="authStore.canSeeAudit"
                      type="button"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                      @click="handleOpenAudit(doc)"
                    >
                      Auditoría
                    </button>
                    <button
                      v-if="isStaff && !authStore.hasRole('vinculacion')"
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
    </template>

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
              <option
                v-if="isAccreditationActive || isStaff"
                value="constancia_acreditacion"
              >
                Diploma / Constancia de Acreditación (InnovaTecNM / HackaTec)
              </option>
              <option
                v-if="!isAccreditationActive && (!isStudent || (!studentDeadlineInfo.isDocumentBlocked && !isProjectApproved))"
                value="carta_aceptacion"
              >
                Carta de Aceptación / Aprobación *
              </option>
              <option
                v-if="!isAccreditationActive && (!isStudent || !studentDeadlineInfo.isDocumentBlocked || (studentDeadlineInfo.isDocumentBlocked && studentDeadlineInfo.formato29Status !== 'approved'))"
                value="formato_29"
              >
                Formato 29 (Primer Seguimiento)
              </option>
              <option
                v-if="!isAccreditationActive && (!isStudent || studentDeadlineInfo.canUploadSecondPhase) && (!isStudent || !studentDeadlineInfo.isDocumentBlocked || (studentDeadlineInfo.isDocumentBlocked && studentDeadlineInfo.formato29Status === 'approved'))"
                value="formato_29v2"
              >
                Formato 29 (Segundo Seguimiento)
              </option>
              <option
                v-if="!isAccreditationActive && (!isStudent || studentDeadlineInfo.canUploadSecondPhase) && (!isStudent || !studentDeadlineInfo.isDocumentBlocked || (studentDeadlineInfo.isDocumentBlocked && studentDeadlineInfo.formato29Status === 'approved'))"
                value="formato_30"
              >
                Formato 30 (Evaluación Final)
              </option>
              <option v-if="!isAccreditationActive && (isProjectApproved || isStaff) && (!isStudent || !studentDeadlineInfo.isDocumentBlocked)" value="solicitud">Solicitud de Residencia Profesional</option>
              <option v-if="!isAccreditationActive && (isProjectApproved || isStaff) && (!isStudent || !studentDeadlineInfo.isDocumentBlocked)" value="dictamen">Dictamen de Aprobación</option>
              <option v-if="!isAccreditationActive && (isProjectApproved || isStaff) && (!isStudent || !studentDeadlineInfo.isDocumentBlocked)" value="manual_usuario">Manual de Usuario</option>
              <option v-if="!isAccreditationActive && (isProjectApproved || isStaff) && (!isStudent || !studentDeadlineInfo.isDocumentBlocked)" value="manual_tecnico">Manual Técnico</option>
              <option v-if="!isAccreditationActive && (isProjectApproved || isStaff) && (!isStudent || !studentDeadlineInfo.isDocumentBlocked)" value="libranza">Oficio de Liberación</option>
              <option v-if="!isStudent || !studentDeadlineInfo.isDocumentBlocked || isAccreditationActive" value="otro">Otro / Evidencia Adicional</option>
            </select>

            <div
              v-if="isStudent && !isAccreditationActive && studentDeadlineInfo.isDocumentBlocked"
              style="margin-top: 0.5rem; padding: 0.5rem 0.75rem; background: #fff5f5; border: 1px solid #fecaca; border-radius: 4px; color: #b91c1c; font-size: 0.85rem;"
            >
              <strong>Atención:</strong> Acceso restringido por fecha límite vencida. Únicamente puede cargar y entregar los formatos oficiales pendientes ({{ studentDeadlineInfo.formato29Status !== 'approved' ? 'Formato 29' : 'Formato 29v2 y Formato 30' }}).
            </div>

            <small
              v-if="isStudent && !isAccreditationActive && !studentDeadlineInfo.canUploadSecondPhase && !studentDeadlineInfo.isDocumentBlocked"
              class="tecnm-form-hint"
              style="color: var(--tecnm-gray-600); margin-top: 0.35rem; display: block;"
            >
              * Los formatos de entrega final (Formato 29v2 y Formato 30) se habilitarán únicamente una vez que su primer Formato 29 sea validado y aprobado por la Coordinación.
            </small>

            <!-- Panel informativo de Fecha Límite según formato -->
            <div
              v-if="uploadForm.documentType === 'formato_29' && studentDeadlineInfo.formato29Deadline"
              class="tecnm-card"
              style="margin-top: 0.5rem; padding: 0.6rem 0.8rem; background: #F8FAFC; border-left: 4px solid var(--tecnm-blue-primary);"
            >
              <div style="display: flex; justify-content: space-between; align-items: center; font-size: 0.875rem;">
                <strong>Fecha Límite Formato 29:</strong>
                <span
                  class="tecnm-badge"
                  :class="isDeadlinePassed(studentDeadlineInfo.formato29Deadline) ? 'tecnm-badge-danger' : 'tecnm-badge-info'"
                >
                  {{ formatTecNMDate(studentDeadlineInfo.formato29Deadline) }} ({{ getDeadlineStatus(studentDeadlineInfo.formato29Deadline) }})
                </span>
              </div>
              <p v-if="studentDeadlineInfo.formato29RejectionReason" style="margin: 0.35rem 0 0 0; color: #dc2626; font-size: 0.825rem;">
                <strong>Corrección solicitada por coordinación:</strong> {{ studentDeadlineInfo.formato29RejectionReason }}
              </p>
            </div>

            <div
              v-else-if="(uploadForm.documentType === 'formato_29v2' || uploadForm.documentType === 'formato_30') && studentDeadlineInfo.formato30Deadline"
              class="tecnm-card"
              style="margin-top: 0.5rem; padding: 0.6rem 0.8rem; background: #F8FAFC; border-left: 4px solid var(--tecnm-gold-accent);"
            >
              <div style="display: flex; justify-content: space-between; align-items: center; font-size: 0.875rem;">
                <strong>Fecha Límite Entrega Final (29v2 y 30):</strong>
                <span
                  class="tecnm-badge"
                  :class="isDeadlinePassed(studentDeadlineInfo.formato30Deadline) ? 'tecnm-badge-danger' : 'tecnm-badge-info'"
                >
                  {{ formatTecNMDate(studentDeadlineInfo.formato30Deadline) }} ({{ getDeadlineStatus(studentDeadlineInfo.formato30Deadline) }})
                </span>
              </div>
              <p
                v-if="uploadForm.documentType === 'formato_29v2' && studentDeadlineInfo.formato29V2RejectionReason"
                style="margin: 0.35rem 0 0 0; color: #dc2626; font-size: 0.825rem;"
              >
                <strong>Corrección solicitada (Formato 29v2):</strong> {{ studentDeadlineInfo.formato29V2RejectionReason }}
              </p>
              <p
                v-if="uploadForm.documentType === 'formato_30' && studentDeadlineInfo.formato30RejectionReason"
                style="margin: 0.35rem 0 0 0; color: #dc2626; font-size: 0.825rem;"
              >
                <strong>Corrección solicitada (Formato 30):</strong> {{ studentDeadlineInfo.formato30RejectionReason }}
              </p>
            </div>
          </div>

          <div class="tecnm-form-group">
            <label for="documentFile" class="tecnm-label">Archivo PDF o Imagen (Solo hasta 5MB) *</label>
            <input
              id="documentFile"
              type="file"
              class="tecnm-form-control"
              accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png"
              required
              @change="onFileSelected"
            />
            <span class="tecnm-form-hint">Solo se permiten archivos en formato PDF, JPG o PNG de hasta 5MB.</span>
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

          <!-- Asignación de Asesor Académico en Evaluación de Carta de Aceptación -->
          <div
            v-if="canAssignAdvisor && currentProject && (statusForm.typeLabel?.includes('Carta de Aceptación') || statusForm.typeLabel?.includes('Aprobación'))"
            class="tecnm-card"
            style="margin-top: 1rem; padding: 0.75rem 1rem; background: var(--tecnm-bg-light, #f8fafc); border: 1px solid var(--tecnm-border-color, #e2e8f0); border-radius: var(--tecnm-radius-md);"
          >
            <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.5rem; flex-wrap: wrap; gap: 0.25rem;">
              <span style="font-weight: 600; font-size: 0.88rem; color: var(--tecnm-blue-primary, #1b396a);">
                Asignación de Asesor Académico
              </span>
              <span v-if="currentProject.advisorName && currentAdvisorLoad !== null" class="tecnm-badge tecnm-badge-info" style="font-size: 0.75rem;">
                Carga actual: {{ currentAdvisorLoad }} alumno{{ currentAdvisorLoad === 1 ? '' : 's' }}
              </span>
            </div>
            <p style="font-size: 0.78rem; margin-bottom: 0.5rem;" class="tecnm-text-muted">
              Al validar este documento oficial de empresa, puede asignar o ratificar al asesor de este residente:
            </p>
            <div style="display: flex; gap: 0.5rem; align-items: center; flex-wrap: wrap;">
              <div style="flex: 1; min-width: 220px;">
                <TecnmAutocomplete
                  v-model="selectedAdvisorId"
                  endpoint="/v1/advisors"
                  global-search-source="ADVISORS"
                  placeholder="Buscar docente asesor..."
                  :initial-item="initialAdvisorItem"
                  @select="item => selectedAdvisorCandidate = item"
                  @clear="selectedAdvisorCandidate = null"
                />
              </div>
              <button
                type="button"
                class="tecnm-btn tecnm-btn-primary tecnm-btn-sm"
                :disabled="isAssigningAdvisor || !selectedAdvisorId || Number(selectedAdvisorId) === Number(currentProject.advisorId)"
                @click="handleAssignAdvisorInExpediente"
              >
                {{ currentProject.advisorName ? 'Cambiar Asesor' : 'Asignar Asesor' }}
              </button>
            </div>
            <div
              v-if="selectedAdvisorCandidate && (selectedAdvisorCandidate.assignedStudentsCount !== undefined || selectedAdvisorCandidate.assigned_students_count !== undefined)"
              class="tecnm-text-muted"
              style="margin-top: 0.35rem; font-size: 0.78rem;"
            >
              <span>Carga docente del seleccionado: </span>
              <strong style="color: var(--tecnm-blue-primary, #1b396a);">
                {{ selectedAdvisorCandidate.assignedStudentsCount ?? selectedAdvisorCandidate.assigned_students_count }} alumnos asignados
              </strong>
            </div>
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

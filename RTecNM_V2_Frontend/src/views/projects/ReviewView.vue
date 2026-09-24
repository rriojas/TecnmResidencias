<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
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
const includeCancelled = ref(false)
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

function isAccreditation(project) {
  if (!project) return false
  const t = String(project.projectType || '').toLowerCase()
  if (t.includes('innovatec') || t.includes('hackatec') || t.startsWith('acreditacion')) return true
  if (accreditationDocs.value && accreditationDocs.value.length > 0) return true
  if (accreditationDoc.value) return true
  return false
}

function getAccreditationBadgeLabel(project) {
  return 'InnovaTecNM Nacional'
}

function getActionLabel(project) {
  if (!project) return 'Ver Detalle'
  const st = (project.status || '').toLowerCase()
  if (isAccreditation(project)) {
    if (!authStore.isReadOnly && !authStore.hasRole('vinculacion') && isDictaminable(st)) {
      return 'Revisar Acreditación'
    }
  }
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
const currentAdvisorLoad = ref(null)
const selectedAdvisorCandidate = ref(null)
const accreditationDoc = ref(null)
const accreditationDocs = ref([])
const activeAccreditationDocId = ref(null)
const cartaAceptacionDoc = ref(null)
const isPreviewModalOpen = ref(false)
const previewDoc = ref(null)
const previewObjectUrl = ref(null)
const isSubmitting = ref(false)

// Visor Inline integrado en Tarjetas (Carta de Aceptación y Constancia)
const isCartaInlineVisible = ref(false)
const inlineCartaUrl = ref(null)
const inlineCartaLoading = ref(false)
const inlineCartaError = ref(null)

const isAccreditationInlineVisible = ref(false)
const inlineAccreditationUrl = ref(null)
const inlineAccreditationLoading = ref(false)
const inlineAccreditationError = ref(null)

const canAssignAdvisor = computed(() => {
  if (authStore.isReadOnly) return false
  return (
    authStore.isAdmin ||
    authStore.isCareerHead ||
    authStore.hasRole('jefecarrera', 'careerhead', 'departmenthead', 'academic') ||
    authStore.hasPermission('projects.advisor.assign')
  )
})

// Catálogo de Carreras
const defaultCareersMap = {
  1: 'Ing. Informática',
  2: 'Ing. Industrial',
  3: 'Ing. Mecatrónica',
  4: 'Ing. en Energías Renovables',
  5: 'Ing. Electrónica',
  6: 'Ing. en Gestión Empresarial',
  7: 'Ing. Mecánica',
}

const CAREERS = ref({ ...defaultCareersMap })

async function loadCareersCatalog() {
  try {
    const res = await apiClient.get('/v1/careers/all')
    const list = res.data || []
    if (list.length > 0) {
      const map = {}
      list.forEach(c => {
        map[c.id] = c.name
      })
      CAREERS.value = map
    }
  } catch {}
}

const selectedCareerFilter = ref('all')
const searchTerm = ref('')

const filteredCareers = computed(() => {
  if (authStore.isCoordinator && authStore.userCareerIds.length > 0) {
    const res = {}
    authStore.userCareerIds.forEach(id => {
      if (CAREERS.value[id]) res[id] = CAREERS.value[id]
    })
    return res
  }
  return CAREERS.value
})

const sortedProjects = computed(() => {
  return projects.value
})

let searchTimer = null
function onSearchInput() {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    pageNumber.value = 1
    loadProjects()
  }, 300)
}

async function loadProjects({ silent = false } = {}) {
  if (!silent) isLoading.value = true
  try {
    const params = {
      pageNumber: pageNumber.value,
      pageSize: pageSize.value,
      sortBy: sortBy.value,
      sortDir: sortDir.value,
      search: searchTerm.value.trim() || undefined,
      includeInactive: includeInactive.value,
      includeCancelled: includeCancelled.value,
      careerId: selectedCareerFilter.value !== 'all' ? Number(selectedCareerFilter.value) : undefined,
    }

    const res = await apiClient.get('/v1/projects', { params })
    const data = res.data
    const items = data.items || []
    projects.value = authStore.isCareerHead
      ? items.filter((p) => String(p.status || '').toLowerCase() !== 'draft')
      : items
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

function handleOpenSearch() {
  openSearch({
    initialSource: 'PROJECTS',
    onSelect: (item) => {
      if (!item) return
      searchTerm.value = item.title || item.student_name || item.studentName || String(item.id || '')
      pageNumber.value = 1
      loadProjects()
    },
  })
}

function handleSort(col) {
  if (sortBy.value.toLowerCase() === col.toLowerCase()) {
    sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
  } else {
    sortBy.value = col
    sortDir.value = 'asc'
  }
  pageNumber.value = 1
  loadProjects({ silent: true })
}
const toggleSort = handleSort

async function openReviewModal(project) {
  try {
    const res = await apiClient.get(`/v1/projects/${project.id}`)
    selectedProject.value = res.data
    reviewComments.value = res.data.reviewComments || ''
    selectedAdvisorId.value = res.data.advisorId || ''
    initialReviewAdvisor.value = res.data.advisorId ? { id: res.data.advisorId, fullName: res.data.advisorName } : null
    currentAdvisorLoad.value = null
    selectedAdvisorCandidate.value = null

    if (res.data.advisorId) {
      try {
        const advRes = await apiClient.get(`/v1/advisors/${res.data.advisorId}`)
        currentAdvisorLoad.value = advRes.data?.assignedStudentsCount ?? null
        initialReviewAdvisor.value = {
          id: res.data.advisorId,
          fullName: res.data.advisorName,
          assignedStudentsCount: advRes.data?.assignedStudentsCount,
        }
      } catch {
        initialReviewAdvisor.value = { id: res.data.advisorId, fullName: res.data.advisorName }
      }
    }
    cleanupInlineDocs()
    accreditationDoc.value = null
    accreditationDocs.value = []
    activeAccreditationDocId.value = null
    cartaAceptacionDoc.value = null

    try {
      const dRes = await apiClient.get(`/v1/documents/project/${project.id}`, {
        params: { pageSize: 50, _t: Date.now() },
      })
      const docs = dRes.data?.items || []
      accreditationDocs.value = docs.filter((d) =>
        ['constancia_acreditacion', 'acreditacion', 'diploma'].includes((d.documentType || '').toLowerCase()) ||
        (d.fileName || '').toLowerCase().includes('diploma') ||
        (d.fileName || '').toLowerCase().includes('constancia') ||
        (d.fileName || '').toLowerCase().includes('innovatec')
      )
      accreditationDoc.value = accreditationDocs.value[0] || null
      cartaAceptacionDoc.value =
        docs.find((d) => ['carta_aceptacion', 'carta_aprobacion'].includes((d.documentType || '').toLowerCase()) && d.isActive) || null
    } catch {}

    isReviewModalOpen.value = true
  } catch {
    showAlert('Error al cargar datos del anteproyecto.', 'danger')
  }
}

function cleanupInlineDocs() {
  isCartaInlineVisible.value = false
  inlineCartaLoading.value = false
  inlineCartaError.value = null
  if (inlineCartaUrl.value) {
    window.URL.revokeObjectURL(inlineCartaUrl.value)
    inlineCartaUrl.value = null
  }

  isAccreditationInlineVisible.value = false
  inlineAccreditationLoading.value = false
  inlineAccreditationError.value = null
  activeAccreditationDocId.value = null
  if (inlineAccreditationUrl.value) {
    window.URL.revokeObjectURL(inlineAccreditationUrl.value)
    inlineAccreditationUrl.value = null
  }
}

function closeDetailModal() {
  isReviewModalOpen.value = false
  cleanupInlineDocs()
}

async function toggleInlineCarta() {
  if (isCartaInlineVisible.value) {
    isCartaInlineVisible.value = false
    if (inlineCartaUrl.value) {
      window.URL.revokeObjectURL(inlineCartaUrl.value)
      inlineCartaUrl.value = null
    }
    return
  }

  if (!cartaAceptacionDoc.value?.id) return

  isCartaInlineVisible.value = true
  inlineCartaLoading.value = true
  inlineCartaError.value = null

  if (inlineCartaUrl.value) {
    window.URL.revokeObjectURL(inlineCartaUrl.value)
    inlineCartaUrl.value = null
  }

  try {
    const res = await apiClient.get(`/v1/documents/${cartaAceptacionDoc.value.id}/view`, {
      responseType: 'blob',
    })

    const fileName = (cartaAceptacionDoc.value.fileName || '').toLowerCase()
    let mimeType = 'application/pdf'
    if (fileName.endsWith('.png')) {
      mimeType = 'image/png'
    } else if (fileName.endsWith('.jpg') || fileName.endsWith('.jpeg')) {
      mimeType = 'image/jpeg'
    }

    const blob = new Blob([res.data], { type: mimeType })
    inlineCartaUrl.value = window.URL.createObjectURL(blob)
  } catch (err) {
    console.error('Error al cargar carta de aceptación inline:', err)
    inlineCartaError.value = 'No se pudo visualizar el documento directamente en pantalla. Por favor utilice el botón Descargar.'
  } finally {
    inlineCartaLoading.value = false
  }
}

async function toggleInlineAccreditation(doc = null) {
  const targetDoc = doc || accreditationDoc.value
  if (!targetDoc?.id) return

  if (isAccreditationInlineVisible.value && activeAccreditationDocId.value === targetDoc.id) {
    isAccreditationInlineVisible.value = false
    activeAccreditationDocId.value = null
    if (inlineAccreditationUrl.value) {
      window.URL.revokeObjectURL(inlineAccreditationUrl.value)
      inlineAccreditationUrl.value = null
    }
    return
  }

  activeAccreditationDocId.value = targetDoc.id
  isAccreditationInlineVisible.value = true
  inlineAccreditationLoading.value = true
  inlineAccreditationError.value = null

  if (inlineAccreditationUrl.value) {
    window.URL.revokeObjectURL(inlineAccreditationUrl.value)
    inlineAccreditationUrl.value = null
  }

  try {
    const res = await apiClient.get(`/v1/documents/${targetDoc.id}/view`, {
      responseType: 'blob',
    })

    const fileName = (targetDoc.fileName || '').toLowerCase()
    let mimeType = 'application/pdf'
    if (fileName.endsWith('.png')) {
      mimeType = 'image/png'
    } else if (fileName.endsWith('.jpg') || fileName.endsWith('.jpeg')) {
      mimeType = 'image/jpeg'
    }

    const blob = new Blob([res.data], { type: mimeType })
    inlineAccreditationUrl.value = window.URL.createObjectURL(blob)
  } catch (err) {
    console.error('Error al cargar diploma/constancia inline:', err)
    inlineAccreditationError.value = 'No se pudo visualizar el diploma directamente en pantalla. Por favor utilice el botón Descargar.'
  } finally {
    inlineAccreditationLoading.value = false
  }
}

async function openDoc(doc) {
  if (!doc?.id) return
  previewDoc.value = doc
  isPreviewModalOpen.value = true
  if (previewObjectUrl.value) {
    window.URL.revokeObjectURL(previewObjectUrl.value)
    previewObjectUrl.value = null
  }
  try {
    const res = await apiClient.get(`/v1/documents/${doc.id}/view`, {
      responseType: 'blob',
    })
    const fileName = (doc.fileName || '').toLowerCase()
    let mimeType = 'application/pdf'
    if (fileName.endsWith('.png')) {
      mimeType = 'image/png'
    } else if (fileName.endsWith('.jpg') || fileName.endsWith('.jpeg')) {
      mimeType = 'image/jpeg'
    }
    const file = new Blob([res.data], { type: mimeType })
    previewObjectUrl.value = window.URL.createObjectURL(file)
  } catch {
    showAlert('Error al cargar la vista previa del documento.', 'danger')
    closePreviewModal()
  }
}

function closePreviewModal() {
  isPreviewModalOpen.value = false
  if (previewObjectUrl.value) {
    window.URL.revokeObjectURL(previewObjectUrl.value)
    previewObjectUrl.value = null
  }
  previewDoc.value = null
}

onUnmounted(() => {
  cleanupInlineDocs()
  closePreviewModal()
})

async function downloadDoc(doc, defaultFileName) {
  if (!doc?.id) return
  try {
    const res = await apiClient.get(`/v1/documents/${doc.id}/download`, {
      responseType: 'blob',
    })
    const url = window.URL.createObjectURL(new Blob([res.data]))
    const link = document.createElement('a')
    link.href = url
    link.setAttribute('download', doc.fileName || defaultFileName)
    document.body.appendChild(link)
    link.click()
    link.remove()
    window.URL.revokeObjectURL(url)
  } catch {
    showAlert('Error al descargar el documento.', 'danger')
  }
}

async function downloadAccreditationDoc() {
  if (!accreditationDoc.value) return
  await downloadDoc(accreditationDoc.value, 'Constancia_Acreditacion.pdf')
}

async function handleValidateAccreditation(approved, denied = false) {
  if (!selectedProject.value) return

  if (!approved && !reviewComments.value.trim()) {
    showAlert(
      denied
        ? 'Debe ingresar el motivo de la denegación en las observaciones.'
        : 'Debe ingresar las observaciones de calidad o formato antes de regresar la constancia.',
      'warning'
    )
    return
  }

  const title = approved
    ? 'Validar y Liberar Residencia'
    : denied
      ? 'Denegar Acreditación por InnovaTecNM'
      : 'Regresar con Observaciones'

  const message = approved
    ? `¿Está seguro de validar la constancia y liberar la residencia de "${selectedProject.value.studentName}" con calificación del 100%?`
    : denied
      ? `¿Está seguro de denegar la solicitud de acreditación de "${selectedProject.value.studentName}"? Al denegarla, se reactivarán sus opciones para registrar anteproyecto ordinario.`
      : `¿Está seguro de regresar la constancia al estudiante con las observaciones de calidad/formato indicadas?`

  const okText = approved
    ? 'Validar y Liberar (100%)'
    : denied
      ? 'Confirmar Denegación'
      : 'Regresar con Observaciones'

  const confirmed = await confirm({
    title,
    message,
    okText,
    cancelText: 'Cancelar',
  })
  if (!confirmed) return

  isSubmitting.value = true
  try {
    await apiClient.post(`/v1/projects/${selectedProject.value.id}/accreditation/validate`, {
      approved,
      denied,
      observations: reviewComments.value.trim() || undefined,
    })
    showAlert(
      approved
        ? '¡Acreditación VALIDADA y Residencia LIBERADA con calificación de 100%!'
        : denied
          ? 'Acreditación DENEGADA. Se reactivaron automáticamente las secciones ordinarias del estudiante.'
          : 'Observaciones enviadas al estudiante. Podrá re-enviar su constancia corregida.',
      approved ? 'success' : denied ? 'danger' : 'warning'
    )
    isReviewModalOpen.value = false
    loadProjects()
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al dictaminar la acreditación.', 'danger')
  } finally {
    isSubmitting.value = false
  }
}

async function handleAssignAdvisor() {
  if (!selectedProject.value || !selectedAdvisorId.value) return
  isSubmitting.value = true
  try {
    await apiClient.post('/v1/advisors/assign', {
      advisorId: Number(selectedAdvisorId.value),
      projectId: selectedProject.value.id,
      advisorType: 'internal',
    })
    const updatedRes = await apiClient.get(`/v1/projects/${selectedProject.value.id}`)
    selectedProject.value = updatedRes.data
    selectedAdvisorCandidate.value = null
    if (updatedRes.data.advisorId) {
      try {
        const advRes = await apiClient.get(`/v1/advisors/${updatedRes.data.advisorId}`)
        currentAdvisorLoad.value = advRes.data?.assignedStudentsCount ?? null
        initialReviewAdvisor.value = {
          id: updatedRes.data.advisorId,
          fullName: updatedRes.data.advisorName,
          assignedStudentsCount: advRes.data?.assignedStudentsCount,
        }
      } catch {
        currentAdvisorLoad.value = null
        initialReviewAdvisor.value = { id: updatedRes.data.advisorId, fullName: updatedRes.data.advisorName }
      }
    } else {
      currentAdvisorLoad.value = null
      initialReviewAdvisor.value = null
    }
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
    if (selectedAdvisorId.value && Number(selectedAdvisorId.value) !== Number(selectedProject.value.advisorId)) {
      try {
        await apiClient.post('/v1/advisors/assign', {
          advisorId: Number(selectedAdvisorId.value),
          projectId: selectedProject.value.id,
          advisorType: 'internal',
        })
      } catch {}
    }

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
      includeCancelled: includeCancelled.value,
      careerId: selectedCareerFilter.value !== 'all' ? Number(selectedCareerFilter.value) : undefined,
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
  loadCareersCatalog()
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
        <h1 class="tecnm-page-title">
          {{ authStore.isCareerHead ? 'Dictamen de Anteproyectos' : 'Revisión y Dictamen de Anteproyectos' }}
        </h1>
        <p class="tecnm-page-subtitle">
          {{ authStore.isCareerHead ? 'Evaluación técnica y emisión de dictamen de anteproyectos de los estudiantes de tu carrera' : 'Evaluación técnica y emisión de dictamen de anteproyectos de residencia profesional' }}
        </p>
      </div>
      <div class="tecnm-page-actions">
        <button
          type="button"
          class="tecnm-btn tecnm-btn-secondary"
          @click="handleOpenSearch"
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
            @input="onSearchInput"
          />
        </div>

        <div v-if="!authStore.isCareerHead" class="tecnm-d-flex tecnm-align-center tecnm-gap-2" style="display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap;">
          <label for="reviewCareerFilter" class="tecnm-field-label" style="margin-bottom: 0; white-space: nowrap; font-size: 0.85rem;">Carrera:</label>
          <select
            id="reviewCareerFilter"
            v-model="selectedCareerFilter"
            class="tecnm-form-control"
            style="min-width: 220px; font-size: 0.85rem;"
            @change="pageNumber = 1; loadProjects()"
          >
            <option value="all">{{ authStore.isCoordinator ? 'Mis Carreras Asignadas' : 'Todas las Carreras' }}</option>
            <option v-for="(name, id) in filteredCareers" :key="id" :value="id">
              {{ name }}
            </option>
          </select>
        </div>

        <div class="tecnm-toolbar-actions">
          <label v-if="!authStore.isCareerHead" class="tecnm-switch-label">
            <span class="tecnm-switch">
              <input
                id="includeInactiveToggle"
                v-model="includeInactive"
                type="checkbox"
                @change="pageNumber = 1; loadProjects()"
              />
              <span class="tecnm-switch-slider"></span>
            </span>
            Mostrar inactivos
          </label>
          <label class="tecnm-switch-label">
            <span class="tecnm-switch">
              <input
                id="includeCancelledToggle"
                v-model="includeCancelled"
                type="checkbox"
                @change="pageNumber = 1; loadProjects()"
              />
              <span class="tecnm-switch-slider"></span>
            </span>
            Mostrar cancelados
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
                  style="width: 55px; text-align: center;"
                  @click="toggleSort('AlphabeticalIndex')"
                >
                  #
                  <span class="tecnm-sort-icon" :class="{ active: ['alphabeticalindex', 'index'].includes(sortBy.toLowerCase()) }">
                    {{ ['alphabeticalindex', 'index'].includes(sortBy.toLowerCase()) ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th
                  class="tecnm-th-sortable"
                  @click="toggleSort('Title')"
                >
                  Título del Proyecto
                  <span class="tecnm-sort-icon" :class="{ active: sortBy.toLowerCase() === 'title' }">
                    {{ sortBy.toLowerCase() === 'title' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th
                  class="tecnm-th-sortable"
                  @click="toggleSort('StudentName')"
                >
                  Estudiante y Carrera
                  <span class="tecnm-sort-icon" :class="{ active: sortBy.toLowerCase() === 'studentname' }">
                    {{ sortBy.toLowerCase() === 'studentname' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th
                  class="tecnm-th-sortable"
                  @click="toggleSort('AdvisorName')"
                >
                  Asesor Asignado
                  <span class="tecnm-sort-icon" :class="{ active: ['advisorname', 'advisor'].includes(sortBy.toLowerCase()) }">
                    {{ ['advisorname', 'advisor'].includes(sortBy.toLowerCase()) ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th
                  class="tecnm-th-sortable"
                  @click="toggleSort('CompanyName')"
                >
                  Empresa / Institución
                  <span class="tecnm-sort-icon" :class="{ active: sortBy.toLowerCase() === 'companyname' }">
                    {{ sortBy.toLowerCase() === 'companyname' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th
                  class="tecnm-th-sortable"
                  @click="toggleSort('CreatedAt')"
                >
                  Fecha Registro
                  <span class="tecnm-sort-icon" :class="{ active: sortBy.toLowerCase() === 'createdat' }">
                    {{ sortBy.toLowerCase() === 'createdat' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th
                  class="tecnm-th-sortable"
                  @click="toggleSort('Status')"
                >
                  Estado
                  <span class="tecnm-sort-icon" :class="{ active: sortBy.toLowerCase() === 'status' }">
                    {{ sortBy.toLowerCase() === 'status' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th class="tecnm-th-actions">Acciones</th>
              </tr>
            </thead>
            <tbody id="projectsTableBody">
              <tr v-if="isLoading">
                <td colspan="8" class="tecnm-table-empty">
                  Cargando anteproyectos...
                </td>
              </tr>
              <tr v-else-if="sortedProjects.length === 0">
                <td colspan="8" class="tecnm-table-empty">
                  <span v-if="includeInactive">No hay anteproyectos inactivos (deshabilitados) registrados.</span>
                  <span v-else>No hay anteproyectos que coincidan con los filtros seleccionados.</span>
                </td>
              </tr>
              <tr
                v-for="p in sortedProjects"
                v-else
                :key="p.id"
              >
                <td style="text-align: center; font-weight: 600; color: var(--tecnm-gray-700, #4b5563);">
                  {{ p.alphabeticalIndex != null ? p.alphabeticalIndex : '—' }}
                </td>
                <td>
                  <strong>{{ p.title }}</strong>
                  <span
                    v-if="isAccreditation(p)"
                    class="tecnm-badge"
                    style="margin-left: 0.5rem; font-size: 0.72rem; background-color: var(--tecnm-gold-accent, #C5A059); color: #fff;"
                  >
                    {{ getAccreditationBadgeLabel(p) }}
                  </span>
                </td>
                <td>
                  <div>{{ p.studentName || '—' }}</div>
                  <small v-if="p.careerId || p.career" style="color: var(--tecnm-blue-primary, #1b396a); font-size: 0.75rem;">
                    {{ CAREERS[p.careerId] || p.career }}
                  </small>
                </td>
                <td>
                  <div v-if="p.advisorName && p.advisorName.trim()">
                    <strong style="color: var(--tecnm-blue-primary, #1b396a);">{{ p.advisorName }}</strong>
                    <div>
                      <span
                        class="tecnm-badge"
                        style="font-size: 0.72rem; margin-top: 2px; background: #e0f2fe; color: #0369a1; border: 1px solid #bae6fd;"
                      >
                        {{ p.advisorAssignedStudentsCount ?? 0 }} alumno(s) asignado(s)
                      </span>
                    </div>
                  </div>
                  <span v-else class="tecnm-badge tecnm-badge-secondary" style="font-size: 0.75rem;">
                    No asignado
                  </span>
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
      @click.self="closeDetailModal"
    >
      <div class="modal-card modal-card-wide">
        <div class="tecnm-modal-header">
          <h3 class="tecnm-modal-title">
            {{ isAccreditation(selectedProject) ? 'Revisión de Acreditación (' + getAccreditationBadgeLabel(selectedProject) + ')' : 'Detalle de Solicitud de Anteproyecto' }}
            <span id="modalProjectId" style="display: none;">{{ selectedProject.id }}</span>
          </h3>
          <button
            id="closeModalBtn"
            type="button"
            class="tecnm-modal-close"
            aria-label="Cerrar"
            @click="closeDetailModal"
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

          <h4 class="tecnm-field-label">
            {{ isAccreditation(selectedProject) ? 'Nombre del Proyecto o Solución (Evento)' : 'Título del Proyecto' }}
          </h4>
          <p id="modalProjectTitle" class="tecnm-field-value tecnm-field-value-emphasis">
            {{ selectedProject.title }}
          </p>

          <!-- SECCIÓN ESPECIAL ACREDITACIÓN INNOVATECNM NACIONAL -->
          <template v-if="isAccreditation(selectedProject)">
            <div class="tecnm-alert tecnm-alert-info" style="margin-bottom: 1rem;">
              <strong>Modalidad de Acreditación Directa:</strong>
              El residente tramitó su acreditación mediante <strong>{{ getAccreditationBadgeLabel(selectedProject) }}</strong>. No requiere anteproyecto ordinario ni asignación de asesor. Al validar la constancia oficial, la residencia se liberará automáticamente al 100%.
            </div>

            <!-- Card de Constancia / Diplomas Adjuntos -->
            <div class="tecnm-card" style="margin-bottom: 1.25rem; border: 1px solid var(--tecnm-border-color, #e2e8f0);">
              <div class="tecnm-card-header" style="background: var(--tecnm-bg-light, #f8fafc); padding: 0.75rem 1rem;">
                <h4 class="tecnm-card-title" style="font-size: 0.95rem; margin: 0;">
                  Diploma(s) / Constancia Oficial de InnovaTecNM
                </h4>
              </div>
              <div class="tecnm-card-body" style="padding: 1rem;">
                <div v-if="accreditationDocs.length > 0" style="display: flex; flex-direction: column; gap: 0.75rem;">
                  <div
                    v-for="(doc, idx) in accreditationDocs"
                    :key="doc.id || idx"
                    class="tecnm-d-flex tecnm-justify-between tecnm-align-center"
                    style="gap: 1rem; flex-wrap: wrap; padding-bottom: 0.75rem;"
                    :style="idx < accreditationDocs.length - 1 ? 'border-bottom: 1px solid var(--tecnm-border-color, #f1f5f9);' : ''"
                  >
                    <div>
                      <div style="font-weight: 600; color: var(--tecnm-blue-primary, #1b396a);">
                        {{ doc.fileName }}
                      </div>
                      <div class="tecnm-text-sub" style="font-size: 0.8rem;">
                        Subido: {{ formatTecNMDate(doc.uploadedAt) }} &bull; Estado: <TecnmBadge :status="doc.status" />
                      </div>
                    </div>
                    <div style="display: flex; gap: 0.5rem; align-items: center; flex-wrap: wrap;">
                      <button
                        type="button"
                        class="tecnm-btn tecnm-btn-sm"
                        :class="isAccreditationInlineVisible && activeAccreditationDocId === doc.id ? 'tecnm-btn-outline' : 'tecnm-btn-primary'"
                        @click="toggleInlineAccreditation(doc)"
                      >
                        <span v-if="isAccreditationInlineVisible && activeAccreditationDocId === doc.id">✕ Ocultar Diploma</span>
                        <span v-else>👁️ Ver Diploma</span>
                      </button>
                      <button
                        type="button"
                        class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                        @click="downloadDoc(doc, doc.fileName || 'Diploma_InnovaTec.pdf')"
                        title="Descargar archivo físico"
                      >
                        📥 Descargar
                      </button>
                    </div>
                  </div>
                </div>
                <div v-else class="tecnm-text-muted" style="font-size: 0.875rem;">
                  No se encontró archivo de diploma o constancia cargado en el expediente.
                </div>

                <!-- Visor Embebido Inline en la Misma Tarjeta -->
                <div
                  v-if="isAccreditationInlineVisible"
                  style="margin-top: 1rem; border-top: 1px dashed var(--tecnm-border-color, #e2e8f0); padding-top: 1rem;"
                >
                  <div v-if="inlineAccreditationLoading" class="tecnm-d-flex tecnm-align-center tecnm-justify-center" style="padding: 2.5rem; gap: 0.75rem; color: var(--tecnm-blue-primary);">
                    <div class="tecnm-spinner"></div>
                    <span style="font-weight: 500; font-size: 0.9rem;">Cargando diploma en pantalla...</span>
                  </div>

                  <div v-else-if="inlineAccreditationError" class="tecnm-alert tecnm-alert-danger" style="margin-bottom: 0;">
                    {{ inlineAccreditationError }}
                  </div>

                  <div
                    v-else-if="inlineAccreditationUrl"
                    style="border: 1px solid var(--tecnm-border-color, #cbd5e1); border-radius: var(--tecnm-radius-md, 6px); overflow: hidden; background: #525659;"
                  >
                    <img
                      v-if="accreditationDocs.find(d => d.id === activeAccreditationDocId)?.fileName?.toLowerCase().endsWith('.png') || accreditationDocs.find(d => d.id === activeAccreditationDocId)?.fileName?.toLowerCase().endsWith('.jpg') || accreditationDocs.find(d => d.id === activeAccreditationDocId)?.fileName?.toLowerCase().endsWith('.jpeg')"
                      :src="inlineAccreditationUrl"
                      alt="Diploma Oficial"
                      style="max-width: 100%; max-height: 600px; display: block; margin: 0 auto; object-fit: contain; background: #ffffff;"
                    />
                    <iframe
                      v-else
                      :src="inlineAccreditationUrl"
                      title="Diploma Oficial de Acreditación"
                      style="width: 100%; height: 580px; border: none; display: block;"
                    ></iframe>
                  </div>
                </div>
              </div>
            </div>
          </template>

          <!-- SECCIÓN ANTEPROYECTO TRADICIONAL -->
          <template v-else>
            <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 1rem; margin-bottom: var(--tecnm-spacing-md);">
              <div>
                <h4 class="tecnm-field-label">Empresa Receptora</h4>
                <p class="tecnm-field-value">{{ selectedProject.companyName || '—' }}</p>
              </div>
              <div>
                <h4 class="tecnm-field-label">Asesor Interno Asignado</h4>
                <div style="display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap; margin-bottom: 0.35rem;">
                  <span v-if="selectedProject.advisorName" class="tecnm-badge tecnm-badge-success" style="font-size: 0.85rem;">
                    {{ selectedProject.advisorName }}
                  </span>
                  <span v-else class="tecnm-badge tecnm-badge-warning" style="font-size: 0.85rem;">
                    Pendiente de asignación
                  </span>
                  <span
                    v-if="selectedProject.advisorName && currentAdvisorLoad !== null"
                    class="tecnm-badge tecnm-badge-info"
                    style="font-size: 0.78rem;"
                    title="Alumnos actualmente asignados a este asesor"
                  >
                    {{ currentAdvisorLoad }} alumno{{ currentAdvisorLoad === 1 ? '' : 's' }} asignado{{ currentAdvisorLoad === 1 ? '' : 's' }}
                  </span>
                </div>

                <!-- Asignar Asesor Interno directamente debajo del campo cuando esté disponible con Carta de Aceptación o Diploma -->
                <div
                  v-if="canAssignAdvisor && (cartaAceptacionDoc || accreditationDocs.length > 0) && !['completed', 'cancelled'].includes((selectedProject.status || '').toLowerCase())"
                  style="margin-top: 0.5rem;"
                >
                  <div style="display: flex; gap: 0.5rem; align-items: center; flex-wrap: wrap;">
                    <div style="flex: 1; min-width: 220px;">
                      <TecnmAutocomplete
                        v-model="selectedAdvisorId"
                        endpoint="/v1/advisors"
                        global-search-source="ADVISORS"
                        placeholder="Buscar asesor académico por nombre..."
                        :initial-item="initialReviewAdvisor"
                        @select="item => selectedAdvisorCandidate = item"
                        @clear="selectedAdvisorCandidate = null"
                      />
                    </div>
                    <button
                      type="button"
                      class="tecnm-btn tecnm-btn-primary tecnm-btn-sm"
                      :disabled="isSubmitting || !selectedAdvisorId || Number(selectedAdvisorId) === Number(selectedProject.advisorId)"
                      @click="handleAssignAdvisor"
                    >
                      {{ selectedProject.advisorName ? 'Cambiar Asesor' : 'Asignar Asesor' }}
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
                </div>
                <div
                  v-else-if="canAssignAdvisor && !cartaAceptacionDoc && accreditationDocs.length === 0 && !['completed', 'cancelled'].includes((selectedProject.status || '').toLowerCase())"
                  class="tecnm-text-muted"
                  style="margin-top: 0.35rem; font-size: 0.75rem;"
                >
                  <em>La asignación se habilitará al contar con la carta de aceptación o diploma.</em>
                </div>
              </div>
            </div>

            <!-- Card de Documento Requerido para Dictamen (Carta de Aceptación / Aprobación o Diploma de InnovaTecNM) -->
            <div class="tecnm-card" style="margin-bottom: 1.25rem; border: 1px solid var(--tecnm-border-color, #e2e8f0);">
              <div class="tecnm-card-header" style="background: var(--tecnm-bg-light, #f8fafc); padding: 0.75rem 1rem;">
                <h4 class="tecnm-card-title" style="font-size: 0.95rem; margin: 0;">
                  {{ !cartaAceptacionDoc && accreditationDocs.length > 0 ? 'Diploma(s) / Constancia de InnovaTecNM' : 'Carta de Aceptación / Aprobación de la Empresa Receptora' }}
                </h4>
              </div>
              <div class="tecnm-card-body" style="padding: 1rem;">
                <!-- Si es InnovaTec o tiene Diplomas y no Carta -->
                <div v-if="!cartaAceptacionDoc && accreditationDocs.length > 0" style="display: flex; flex-direction: column; gap: 0.75rem;">
                  <div
                    v-for="(doc, idx) in accreditationDocs"
                    :key="doc.id || idx"
                    class="tecnm-d-flex tecnm-justify-between tecnm-align-center"
                    style="gap: 1rem; flex-wrap: wrap; padding-bottom: 0.75rem;"
                    :style="idx < accreditationDocs.length - 1 ? 'border-bottom: 1px solid var(--tecnm-border-color, #f1f5f9);' : ''"
                  >
                    <div>
                      <div style="font-weight: 600; color: var(--tecnm-blue-primary, #1b396a);">
                        {{ doc.fileName }}
                      </div>
                      <div class="tecnm-text-sub" style="font-size: 0.8rem;">
                        Subido: {{ formatTecNMDate(doc.uploadedAt) }} &bull; Estado: <TecnmBadge :status="doc.status" />
                      </div>
                    </div>
                    <div style="display: flex; gap: 0.5rem; align-items: center; flex-wrap: wrap;">
                      <button
                        type="button"
                        class="tecnm-btn tecnm-btn-sm"
                        :class="isAccreditationInlineVisible && activeAccreditationDocId === doc.id ? 'tecnm-btn-outline' : 'tecnm-btn-primary'"
                        @click="toggleInlineAccreditation(doc)"
                      >
                        <span v-if="isAccreditationInlineVisible && activeAccreditationDocId === doc.id">✕ Ocultar Diploma</span>
                        <span v-else>👁️ Ver Diploma</span>
                      </button>
                      <button
                        type="button"
                        class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                        @click="downloadDoc(doc, doc.fileName || 'Diploma_InnovaTec.pdf')"
                        title="Descargar archivo físico"
                      >
                        📥 Descargar
                      </button>
                    </div>
                  </div>

                  <!-- Visor Embebido Inline para Diploma en Sección Tradicional -->
                  <div
                    v-if="isAccreditationInlineVisible"
                    style="margin-top: 1rem; border-top: 1px dashed var(--tecnm-border-color, #e2e8f0); padding-top: 1rem;"
                  >
                    <div v-if="inlineAccreditationLoading" class="tecnm-d-flex tecnm-align-center tecnm-justify-center" style="padding: 2.5rem; gap: 0.75rem; color: var(--tecnm-blue-primary);">
                      <div class="tecnm-spinner"></div>
                      <span style="font-weight: 500; font-size: 0.9rem;">Cargando diploma en pantalla...</span>
                    </div>
                    <div v-else-if="inlineAccreditationError" class="tecnm-alert tecnm-alert-danger" style="margin-bottom: 0;">
                      {{ inlineAccreditationError }}
                    </div>
                    <div
                      v-else-if="inlineAccreditationUrl"
                      style="border: 1px solid var(--tecnm-border-color, #cbd5e1); border-radius: var(--tecnm-radius-md, 6px); overflow: hidden; background: #525659;"
                    >
                      <img
                        v-if="accreditationDocs.find(d => d.id === activeAccreditationDocId)?.fileName?.toLowerCase().endsWith('.png') || accreditationDocs.find(d => d.id === activeAccreditationDocId)?.fileName?.toLowerCase().endsWith('.jpg') || accreditationDocs.find(d => d.id === activeAccreditationDocId)?.fileName?.toLowerCase().endsWith('.jpeg')"
                        :src="inlineAccreditationUrl"
                        alt="Diploma Oficial"
                        style="max-width: 100%; max-height: 600px; display: block; margin: 0 auto; object-fit: contain; background: #ffffff;"
                      />
                      <iframe
                        v-else
                        :src="inlineAccreditationUrl"
                        title="Diploma Oficial de Acreditación"
                        style="width: 100%; height: 580px; border: none; display: block;"
                      ></iframe>
                    </div>
                  </div>
                </div>

                <!-- Caso Estándar: Carta de Aceptación -->
                <div v-else class="tecnm-d-flex tecnm-justify-between tecnm-align-center" style="gap: 1rem; flex-wrap: wrap;">
                  <div>
                    <div style="font-weight: 600; color: var(--tecnm-blue-primary, #1b396a);">
                      Carta de Aceptación Oficial de la Empresa
                    </div>
                    <div v-if="cartaAceptacionDoc" class="tecnm-text-sub" style="font-size: 0.8rem;">
                      {{ cartaAceptacionDoc.fileName }} &bull; Subido: {{ formatTecNMDate(cartaAceptacionDoc.uploadedAt) }} &bull; <TecnmBadge :status="cartaAceptacionDoc.status" />
                    </div>
                    <div v-else class="tecnm-text-muted" style="font-size: 0.8rem;">
                      El estudiante aún no ha adjuntado su carta de aceptación emitida por la empresa receptora.
                    </div>
                  </div>
                  <div v-if="cartaAceptacionDoc" style="display: flex; gap: 0.5rem; align-items: center; flex-wrap: wrap;">
                    <button
                      type="button"
                      class="tecnm-btn tecnm-btn-sm"
                      :class="isCartaInlineVisible ? 'tecnm-btn-outline' : 'tecnm-btn-primary'"
                      @click="toggleInlineCarta"
                    >
                      <span v-if="isCartaInlineVisible">✕ Ocultar Carta</span>
                      <span v-else>👁️ Ver Carta</span>
                    </button>
                    <button
                      type="button"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                      @click="downloadDoc(cartaAceptacionDoc, 'Carta_Aceptacion.pdf')"
                      title="Descargar archivo físico"
                    >
                      📥 Descargar
                    </button>
                  </div>
                  <span v-else class="tecnm-badge tecnm-badge-warning" style="font-size: 0.75rem;">
                    Pendiente de carga
                  </span>
                </div>

                <!-- Visor Embebido Inline para Carta de Aceptación -->
                <div
                  v-if="isCartaInlineVisible && cartaAceptacionDoc"
                  style="margin-top: 1rem; border-top: 1px dashed var(--tecnm-border-color, #e2e8f0); padding-top: 1rem;"
                >
                  <div v-if="inlineCartaLoading" class="tecnm-d-flex tecnm-align-center tecnm-justify-center" style="padding: 2.5rem; gap: 0.75rem; color: var(--tecnm-blue-primary);">
                    <div class="tecnm-spinner"></div>
                    <span style="font-weight: 500; font-size: 0.9rem;">Cargando carta de aceptación en pantalla...</span>
                  </div>

                  <div v-else-if="inlineCartaError" class="tecnm-alert tecnm-alert-danger" style="margin-bottom: 0;">
                    {{ inlineCartaError }}
                  </div>

                  <div
                    v-else-if="inlineCartaUrl"
                    style="border: 1px solid var(--tecnm-border-color, #cbd5e1); border-radius: var(--tecnm-radius-md, 6px); overflow: hidden; background: #525659;"
                  >
                    <img
                      v-if="cartaAceptacionDoc?.fileName?.toLowerCase().endsWith('.png') || cartaAceptacionDoc?.fileName?.toLowerCase().endsWith('.jpg') || cartaAceptacionDoc?.fileName?.toLowerCase().endsWith('.jpeg')"
                      :src="inlineCartaUrl"
                      alt="Carta de Aceptación Oficial"
                      style="max-width: 100%; max-height: 600px; display: block; margin: 0 auto; object-fit: contain; background: #ffffff;"
                    />
                    <iframe
                      v-else
                      :src="inlineCartaUrl"
                      title="Carta de Aceptación Oficial de la Empresa"
                      style="width: 100%; height: 580px; border: none; display: block;"
                    ></iframe>
                  </div>
                </div>
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
          </template>

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
          <!-- Botón de Soft Delete (solo admin) -->
          <button
            v-if="authStore.isAdmin && !authStore.isReadOnly"
            id="modalSoftDeleteBtn"
            type="button"
            class="tecnm-btn tecnm-btn-danger"
            :disabled="isSubmitting"
            @click="handleSoftDelete"
          >
            Eliminar
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


          <!-- Botones de Dictamen para Acreditación InnovaTecNM Nacional -->
          <template v-if="isAccreditation(selectedProject) && isDictaminable(selectedProject.status) && !authStore.isReadOnly && !authStore.hasRole('vinculacion')">
            <button
              type="button"
              class="tecnm-btn tecnm-btn-danger"
              :disabled="isSubmitting"
              @click="handleValidateAccreditation(false, true)"
            >
              Denegar Acreditación
            </button>
            <button
              type="button"
              class="tecnm-btn tecnm-btn-warning"
              :disabled="isSubmitting"
              @click="handleValidateAccreditation(false, false)"
            >
              Regresar con Observaciones
            </button>
            <button
              type="button"
              class="tecnm-btn tecnm-btn-success"
              :disabled="isSubmitting"
              @click="handleValidateAccreditation(true, false)"
            >
              Validar y Liberar Residencia (100%)
            </button>
          </template>

          <!-- Botones de Dictamen Ordinario si está Pendiente/En Revisión -->
          <template v-else-if="!isAccreditation(selectedProject) && isDictaminable(selectedProject.status) && !authStore.isReadOnly && !authStore.hasRole('vinculacion')">
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
            @click="closeDetailModal"
          >
            Cerrar
          </button>
        </div>
      </div>
    </div>

    <!-- Modal Vista Previa de Documento (Carta de Aceptación / Constancia) -->
    <div
      v-if="isPreviewModalOpen"
      id="previewDocModal"
      class="tecnm-modal-backdrop"
      role="dialog"
      aria-modal="true"
      style="z-index: 1060;"
      @click.self="closePreviewModal"
    >
      <div class="tecnm-modal" style="max-width: 950px; width: 92vw; max-height: 90vh; display: flex; flex-direction: column;">
        <div class="tecnm-modal-header" style="display: flex; justify-content: space-between; align-items: center;">
          <h3 class="tecnm-modal-title">
            Vista Previa: <span id="previewDocName">{{ previewDoc?.fileName }}</span>
          </h3>
          <div style="display: flex; gap: 0.5rem; align-items: center;">
            <button
              v-if="previewDoc"
              type="button"
              class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
              @click="downloadDoc(previewDoc, previewDoc.fileName)"
            >
              Descargar
            </button>
            <button
              type="button"
              class="tecnm-modal-close"
              aria-label="Cerrar vista previa"
              @click="closePreviewModal"
            >
              &times;
            </button>
          </div>
        </div>
        <div
          class="tecnm-modal-body"
          style="flex: 1; min-height: 520px; height: 75vh; padding: 0; background-color: var(--tecnm-gray-100); display: flex; align-items: center; justify-content: center; overflow: hidden;"
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
        <div class="tecnm-modal-footer">
          <button
            type="button"
            class="tecnm-btn tecnm-btn-secondary"
            @click="closePreviewModal"
          >
            Cerrar Vista Previa
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

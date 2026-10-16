<script setup>
import { ref, computed, onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useAudit } from '@/composables/useAudit'
import { useConfirm } from '@/composables/useConfirm'
import apiClient from '@/services/api'
import TecnmPagination from '@/components/common/TecnmPagination.vue'
import TecnmBadge from '@/components/common/TecnmBadge.vue'
import TecnmModal from '@/components/common/TecnmModal.vue'
import TecnmKpiCard from '@/components/common/TecnmKpiCard.vue'

const authStore = useAuthStore()
const { showAudit, formatAuditDate } = useAudit()
const { confirm } = useConfirm()

const templates = ref([])
const isLoading = ref(false)
const isSubmitting = ref(false)

// Paginación
const pageNumber = ref(1)
const pageSize = ref(10)
const totalCount = ref(0)
const totalPages = ref(0)

// Filtros
const searchTerm = ref('')
const categoryFilter = ref('all')
const statusFilter = ref('all') // 'all' | 'active' | 'inactive'

const CATEGORIES = [
  'General',
  'Inicial',
  'Seguimiento',
  'Final',
  'Acreditación',
  'Convenios'
]

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

// Métricas para Admin
const totalActive = computed(() => templates.value.filter((t) => t.isActive).length)
const totalInactive = computed(() => templates.value.filter((t) => !t.isActive).length)

// ----------------------------------------------------
// Carga de Datos
// ----------------------------------------------------
async function loadTemplates() {
  isLoading.value = true
  try {
    const params = {
      pageNumber: pageNumber.value,
      pageSize: pageSize.value,
      search: searchTerm.value.trim() || undefined,
      category: categoryFilter.value !== 'all' ? categoryFilter.value : undefined,
    }

    if (authStore.isAdmin) {
      if (statusFilter.value === 'inactive') {
        params.includeInactive = true
      } else if (statusFilter.value === 'all') {
        params.includeInactive = true
      }
    }

    const res = await apiClient.get('/v1/official-templates', { params })
    const data = res.data
    let items = data.items || []

    if (authStore.isAdmin && statusFilter.value === 'inactive') {
      items = items.filter((t) => !t.isActive)
    } else if (authStore.isAdmin && statusFilter.value === 'active') {
      items = items.filter((t) => t.isActive)
    }

    templates.value = items
    totalCount.value = data.totalCount || 0
    totalPages.value = data.totalPages || 0
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al cargar los formatos oficiales.', 'danger')
  } finally {
    isLoading.value = false
  }
}

function handleSearch() {
  pageNumber.value = 1
  loadTemplates()
}

function handleCategoryChange() {
  pageNumber.value = 1
  loadTemplates()
}

function handleStatusChange() {
  pageNumber.value = 1
  loadTemplates()
}

// ----------------------------------------------------
// Descargas de Archivos
// ----------------------------------------------------
const downloadingId = ref(null)

async function downloadTemplate(template) {
  downloadingId.value = template.id
  try {
    const res = await apiClient.get(`/v1/official-templates/${template.id}/download`, {
      responseType: 'blob',
    })
    triggerBrowserDownload(res.data, template.currentFileName, template.currentContentType)
  } catch (err) {
    showAlert('Error al descargar el archivo del formato institucional.', 'danger')
  } finally {
    downloadingId.value = null
  }
}

async function downloadSpecificVersion(templateId, version) {
  try {
    const res = await apiClient.get(`/v1/official-templates/${templateId}/versions/${version.id}/download`, {
      responseType: 'blob',
    })
    triggerBrowserDownload(res.data, version.fileName, version.contentType)
  } catch (err) {
    showAlert('Error al descargar la versión histórica solicitada.', 'danger')
  }
}

function triggerBrowserDownload(blobData, fileName, contentType) {
  const blob = new Blob([blobData], { type: contentType || 'application/octet-stream' })
  const url = window.URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.setAttribute('download', fileName || 'formato_oficial')
  document.body.appendChild(link)
  link.click()
  link.remove()
  window.URL.revokeObjectURL(url)
}

// ----------------------------------------------------
// Modal: Crear / Editar Formato
// ----------------------------------------------------
const isModalOpen = ref(false)
const isEditing = ref(false)
const editingId = ref(null)
const selectedFile = ref(null)
const fileInputRef = ref(null)

const form = ref({
  code: '',
  name: '',
  description: '',
  category: 'General',
})

function openCreateModal() {
  isEditing.value = false
  editingId.value = null
  form.value = {
    code: '',
    name: '',
    description: '',
    category: 'General',
  }
  selectedFile.value = null
  if (fileInputRef.value) fileInputRef.value.value = ''
  isModalOpen.value = true
}

function openEditModal(template) {
  isEditing.value = true
  editingId.value = template.id
  form.value = {
    code: template.code,
    name: template.name,
    description: template.description || '',
    category: template.category || 'General',
  }
  selectedFile.value = null
  isModalOpen.value = true
}

function handleFileChange(event) {
  const file = event.target.files[0]
  if (!file) return

  const ext = file.name.substring(file.name.lastIndexOf('.')).toLowerCase()
  if (!['.pdf', '.docx', '.doc'].includes(ext)) {
    showAlert('Solo se permiten archivos en formato PDF o Word (.docx, .doc).', 'warning')
    event.target.value = ''
    selectedFile.value = null
    return
  }
  if (file.size > 25 * 1024 * 1024) {
    showAlert('El archivo excede el tamaño máximo permitido (25MB).', 'warning')
    event.target.value = ''
    selectedFile.value = null
    return
  }
  selectedFile.value = file
}

async function saveTemplate() {
  if (!form.value.code.trim()) {
    showAlert('El código del formato es obligatorio.', 'warning')
    return
  }
  if (!form.value.name.trim()) {
    showAlert('El nombre del formato es obligatorio.', 'warning')
    return
  }
  if (!isEditing.value && !selectedFile.value) {
    showAlert('Debe seleccionar el archivo inicial (PDF o Word).', 'warning')
    return
  }

  isSubmitting.value = true
  try {
    if (isEditing.value) {
      await apiClient.put(`/v1/official-templates/${editingId.value}`, {
        code: form.value.code.trim().toUpperCase(),
        name: form.value.name.trim(),
        description: form.value.description.trim() || null,
        category: form.value.category,
      })
      showAlert('Formato oficial actualizado correctamente.')
    } else {
      const formData = new FormData()
      formData.append('code', form.value.code.trim().toUpperCase())
      formData.append('name', form.value.name.trim())
      formData.append('description', form.value.description.trim() || '')
      formData.append('category', form.value.category)
      formData.append('file', selectedFile.value)

      await apiClient.post('/v1/official-templates', formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
      showAlert('Formato oficial registrado y cargado exitosamente.')
    }

    isModalOpen.value = false
    loadTemplates()
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al guardar el formato institucional.', 'danger')
  } finally {
    isSubmitting.value = false
  }
}

// ----------------------------------------------------
// Modal: Subir Nueva Versión
// ----------------------------------------------------
const isVersionModalOpen = ref(false)
const targetTemplate = ref(null)
const versionFile = ref(null)
const versionNotes = ref('')
const versionFileInputRef = ref(null)

function openNewVersionModal(template) {
  targetTemplate.value = template
  versionFile.value = null
  versionNotes.value = ''
  if (versionFileInputRef.value) versionFileInputRef.value.value = ''
  isVersionModalOpen.value = true
}

function handleVersionFileChange(event) {
  const file = event.target.files[0]
  if (!file) return

  const ext = file.name.substring(file.name.lastIndexOf('.')).toLowerCase()
  if (!['.pdf', '.docx', '.doc'].includes(ext)) {
    showAlert('Solo se permiten archivos en formato PDF o Word (.docx, .doc).', 'warning')
    event.target.value = ''
    versionFile.value = null
    return
  }
  if (file.size > 25 * 1024 * 1024) {
    showAlert('El archivo excede el tamaño máximo permitido (25MB).', 'warning')
    event.target.value = ''
    versionFile.value = null
    return
  }
  versionFile.value = file
}

async function submitNewVersion() {
  if (!versionFile.value) {
    showAlert('Debe seleccionar el nuevo archivo para actualizar la versión.', 'warning')
    return
  }

  isSubmitting.value = true
  try {
    const formData = new FormData()
    formData.append('file', versionFile.value)
    formData.append('changeNotes', versionNotes.value.trim() || '')

    await apiClient.post(`/v1/official-templates/${targetTemplate.value.id}/new-version`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })

    showAlert(`Se actualizó exitosamente a la versión ${targetTemplate.value.currentVersion + 1}.`)
    isVersionModalOpen.value = false
    loadTemplates()
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al subir la nueva versión.', 'danger')
  } finally {
    isSubmitting.value = false
  }
}

// ----------------------------------------------------
// Modal: Historial de Versiones (Admin)
// ----------------------------------------------------
const isHistoryModalOpen = ref(false)
const historyTemplate = ref(null)
const historyVersions = ref([])
const isHistoryLoading = ref(false)

async function openHistoryModal(template) {
  historyTemplate.value = template
  historyVersions.value = []
  isHistoryModalOpen.value = true
  isHistoryLoading.value = true

  try {
    const res = await apiClient.get(`/v1/official-templates/${template.id}/history`)
    historyVersions.value = res.data || []
  } catch (err) {
    showAlert('Error al consultar el historial de versiones.', 'danger')
  } finally {
    isHistoryLoading.value = false
  }
}

// ----------------------------------------------------
// Desactivar / Reactivar (Admin)
// ----------------------------------------------------
async function toggleActive(template) {
  const actionText = template.isActive ? 'desactivar' : 'reactivar'
  const ok = await confirm({
    title: `${template.isActive ? 'Desactivar' : 'Reactivar'} Formato Oficial`,
    message: `¿Está seguro de ${actionText} el formato "${template.name}" (${template.code})? ${
      template.isActive ? 'Los usuarios sin rol administrador no podrán visualizarlo ni descargarlo.' : 'El formato volverá a estar disponible para todos los usuarios.'
    }`,
    okText: template.isActive ? 'Desactivar' : 'Reactivar',
    cancelText: 'Cancelar',
  })
  if (!ok) return

  try {
    if (template.isActive) {
      await apiClient.delete(`/v1/official-templates/${template.id}`)
      showAlert(`El formato "${template.code}" fue desactivado.`)
    } else {
      await apiClient.patch(`/v1/official-templates/${template.id}/activate`)
      showAlert(`El formato "${template.code}" fue reactivado.`)
    }
    loadTemplates()
  } catch (err) {
    showAlert(err.response?.data?.message || `Error al ${actionText} el formato.`, 'danger')
  }
}

// ----------------------------------------------------
// Utilidades de formato
// ----------------------------------------------------
function formatBytes(bytes) {
  if (!bytes || bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i]
}

function isWordDocument(fileName, contentType) {
  const name = (fileName || '').toLowerCase()
  const type = (contentType || '').toLowerCase()
  return name.endsWith('.docx') || name.endsWith('.doc') || type.includes('word')
}

onMounted(() => {
  loadTemplates()
})
</script>

<template>
  <div class="tecnm-page-container">
    <!-- Feedback visual -->
    <div
      v-if="alertMessage"
      class="tecnm-alert"
      :class="`tecnm-alert-${alertType}`"
      role="alert"
    >
      {{ alertMessage }}
    </div>

    <!-- Header institucional -->
    <div class="tecnm-card" style="margin-bottom: 1.5rem;">
      <div class="tecnm-card-header" style="display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 1rem;">
        <div>
          <h2 class="tecnm-section-title" style="margin: 0; display: flex; align-items: center; gap: 0.5rem;">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
              <polyline points="14 2 14 8 20 8" />
              <line x1="16" y1="13" x2="8" y2="13" />
              <line x1="16" y1="17" x2="8" y2="17" />
              <polyline points="10 9 9 9 8 9" />
            </svg>
            Formatos Oficiales y Plantillas
          </h2>
          <p class="tecnm-card-subtitle" style="margin: 0.25rem 0 0 0;">
            Repositorio institucional de formatos descargables (PDF y Word) para el proceso de residencias profesionales.
          </p>
        </div>

        <div v-if="authStore.isAdmin" class="tecnm-header-actions">
          <button
            type="button"
            class="tecnm-btn tecnm-btn-primary"
            @click="openCreateModal"
          >
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="margin-right: 0.4rem;">
              <line x1="12" y1="5" x2="12" y2="19" />
              <line x1="5" y1="12" x2="19" y2="12" />
            </svg>
            Subir Nuevo Formato
          </button>
        </div>
      </div>
    </div>

    <!-- KPI Cards para Administrador -->
    <div v-if="authStore.isAdmin" class="tecnm-kpi-grid" style="display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1rem; margin-bottom: 1.5rem;">
      <TecnmKpiCard
        title="Total en Catálogo"
        :value="totalCount"
        color="primary"
      />
      <TecnmKpiCard
        title="Formatos Activos"
        :value="totalActive"
        color="success"
      />
      <TecnmKpiCard
        title="Formatos Inactivos"
        :value="totalInactive"
        color="warning"
      />
    </div>

    <!-- Barra de Filtros y Búsqueda -->
    <div class="tecnm-card" style="margin-bottom: 1.5rem;">
      <div class="tecnm-card-body" style="display: flex; gap: 1rem; flex-wrap: wrap; align-items: center; justify-content: space-between;">
        <div style="display: flex; gap: 0.75rem; flex: 1; min-width: 280px; flex-wrap: wrap;">
          <!-- Buscador -->
          <div style="flex: 2; min-width: 220px;">
            <input
              v-model="searchTerm"
              type="text"
              class="tecnm-input"
              placeholder="Buscar por código, nombre o palabra clave..."
              @keyup.enter="handleSearch"
            />
          </div>

          <!-- Filtro de Categoría -->
          <div style="flex: 1; min-width: 160px;">
            <select
              v-model="categoryFilter"
              class="tecnm-select"
              @change="handleCategoryChange"
            >
              <option value="all">Todas las categorías</option>
              <option v-for="cat in CATEGORIES" :key="cat" :value="cat">
                {{ cat }}
              </option>
            </select>
          </div>

          <!-- Filtro de Estado (solo Admin) -->
          <div v-if="authStore.isAdmin" style="flex: 1; min-width: 140px;">
            <select
              v-model="statusFilter"
              class="tecnm-select"
              @change="handleStatusChange"
            >
              <option value="all">Todos los estados</option>
              <option value="active">Activos</option>
              <option value="inactive">Inactivos</option>
            </select>
          </div>

          <button
            type="button"
            class="tecnm-btn tecnm-btn-secondary"
            @click="handleSearch"
          >
            Buscar
          </button>
        </div>
      </div>
    </div>

    <!-- Listado Principal de Formatos -->
    <div class="tecnm-card">
      <div class="tecnm-card-body" style="padding: 0;">
        <div v-if="isLoading" style="padding: 2.5rem; text-align: center; color: var(--tecnm-gray-600);">
          Cargando formatos institucionales...
        </div>

        <div v-else-if="templates.length === 0" style="padding: 3rem; text-align: center; color: var(--tecnm-gray-500);">
          <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" style="margin-bottom: 0.75rem; opacity: 0.6;">
            <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
            <polyline points="14 2 14 8 20 8" />
          </svg>
          <p style="margin: 0; font-size: 1.1rem; font-weight: 500;">No se encontraron formatos institucionales disponibles.</p>
        </div>

        <div v-else class="tecnm-table-responsive">
          <table class="tecnm-table">
            <thead>
              <tr>
                <th style="width: 140px;">Código</th>
                <th>Nombre del Formato</th>
                <th style="width: 130px;">Categoría</th>
                <th style="width: 90px; text-align: center;">Versión</th>
                <th style="width: 80px; text-align: center;">Tipo</th>
                <th style="width: 100px;">Tamaño</th>
                <th style="width: 140px;">Fecha Actualización</th>
                <th v-if="authStore.isAdmin" style="width: 100px; text-align: center;">Estado</th>
                <th style="width: 220px; text-align: right;">Acciones</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="item in templates" :key="item.id" :class="{ 'row-inactive': !item.isActive }">
                <!-- Código -->
                <td>
                  <span class="tecnm-badge tecnm-badge-primary" style="font-family: monospace; font-size: 0.85rem;">
                    {{ item.code }}
                  </span>
                </td>

                <!-- Nombre y Descripción -->
                <td>
                  <div style="font-weight: 600; color: var(--tecnm-blue-primary);">
                    {{ item.name }}
                  </div>
                  <div v-if="item.description" style="font-size: 0.85rem; color: var(--tecnm-gray-600); margin-top: 0.2rem;">
                    {{ item.description }}
                  </div>
                </td>

                <!-- Categoría -->
                <td>
                  <span class="tecnm-badge tecnm-badge-secondary">
                    {{ item.category }}
                  </span>
                </td>

                <!-- Versión -->
                <td style="text-align: center;">
                  <span class="tecnm-badge tecnm-badge-info" style="font-weight: bold;">
                    v{{ item.currentVersion }}
                  </span>
                </td>

                <!-- Tipo -->
                <td style="text-align: center;">
                  <span
                    class="tecnm-badge"
                    :class="isWordDocument(item.currentFileName, item.currentContentType) ? 'tecnm-badge-primary' : 'tecnm-badge-danger'"
                    style="font-size: 0.75rem; text-transform: uppercase;"
                  >
                    {{ isWordDocument(item.currentFileName, item.currentContentType) ? 'WORD' : 'PDF' }}
                  </span>
                </td>

                <!-- Tamaño -->
                <td style="font-size: 0.85rem; color: var(--tecnm-gray-600);">
                  {{ formatBytes(item.currentFileSize) }}
                </td>

                <!-- Fecha -->
                <td style="font-size: 0.85rem;">
                  {{ formatAuditDate(item.updatedAt || item.createdAt) }}
                </td>

                <!-- Estado (Admin) -->
                <td v-if="authStore.isAdmin" style="text-align: center;">
                  <TecnmBadge :status="item.isActive" />
                </td>

                <!-- Acciones -->
                <td style="text-align: right; white-space: nowrap;">
                  <div style="display: inline-flex; gap: 0.4rem; align-items: center;">
                    <!-- Botón Descargar (Todos los roles) -->
                    <button
                      type="button"
                      class="tecnm-btn tecnm-btn-sm tecnm-btn-primary"
                      :disabled="downloadingId === item.id"
                      title="Descargar versión vigente"
                      @click="downloadTemplate(item)"
                    >
                      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="margin-right: 0.3rem;">
                        <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
                        <polyline points="7 10 12 15 17 10" />
                        <line x1="12" y1="15" x2="12" y2="3" />
                      </svg>
                      {{ downloadingId === item.id ? 'Descargando...' : 'Descargar' }}
                    </button>

                    <!-- Acciones exclusivas Admin -->
                    <template v-if="authStore.isAdmin">
                      <!-- Subir Nueva Versión -->
                      <button
                        type="button"
                        class="tecnm-btn tecnm-btn-sm tecnm-btn-outline"
                        title="Subir nueva versión del archivo"
                        @click="openNewVersionModal(item)"
                      >
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                          <polyline points="16 16 12 12 8 16" />
                          <line x1="12" y1="12" x2="12" y2="21" />
                          <path d="M20.39 18.39A5 5 0 0 0 18 9h-1.26A8 8 0 1 0 3 16.3" />
                        </svg>
                      </button>

                      <!-- Historial de Versiones -->
                      <button
                        type="button"
                        class="tecnm-btn tecnm-btn-sm tecnm-btn-secondary"
                        title="Ver historial de versiones"
                        @click="openHistoryModal(item)"
                      >
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                          <circle cx="12" cy="12" r="10" />
                          <polyline points="12 6 12 12 16 14" />
                        </svg>
                      </button>

                      <!-- Editar metadatos -->
                      <button
                        type="button"
                        class="tecnm-btn tecnm-btn-sm tecnm-btn-outline"
                        title="Editar metadatos"
                        @click="openEditModal(item)"
                      >
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                          <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
                          <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
                        </svg>
                      </button>

                      <!-- Auditoría de 10 campos -->
                      <button
                        type="button"
                        class="tecnm-btn tecnm-btn-sm tecnm-btn-outline"
                        title="Auditoría institucional"
                        @click="showAudit({ title: `Auditoría — Formato ${item.code}`, item })"
                      >
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                          <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
                        </svg>
                      </button>

                      <!-- Activar / Desactivar -->
                      <button
                        type="button"
                        class="tecnm-btn tecnm-btn-sm"
                        :class="item.isActive ? 'tecnm-btn-outline-danger' : 'tecnm-btn-success'"
                        :title="item.isActive ? 'Desactivar formato' : 'Reactivar formato'"
                        @click="toggleActive(item)"
                      >
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                          <polyline v-if="item.isActive" points="3 6 5 6 21 6" />
                          <path v-if="item.isActive" d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                          <polyline v-else points="20 6 9 17 4 12" />
                        </svg>
                      </button>
                    </template>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Paginador institucional -->
        <div style="padding: 1rem;">
          <TecnmPagination
            :current-page="pageNumber"
            :total-pages="totalPages"
            :total-count="totalCount"
            :page-size="pageSize"
            @update:current-page="pageNumber = $event"
            @page-change="loadTemplates"
          />
        </div>
      </div>
    </div>

    <!-- ==================================================== -->
    <!-- MODAL 1: Subir / Editar Formato Oficial (Admin) -->
    <!-- ==================================================== -->
    <TecnmModal
      v-model="isModalOpen"
      :title="isEditing ? 'Editar Formato Institucional' : 'Subir Nuevo Formato Oficial'"
      max-width="620px"
    >
      <form @submit.prevent="saveTemplate">
        <div class="tecnm-form-group" style="margin-bottom: 1rem;">
          <label class="tecnm-label">Código Oficial *</label>
          <input
            v-model="form.code"
            type="text"
            class="tecnm-input"
            placeholder="Ej. ITLP-AC-PO-005-01"
            maxlength="50"
            required
          />
          <small class="tecnm-help-text">Código clave institucional de control documental.</small>
        </div>

        <div class="tecnm-form-group" style="margin-bottom: 1rem;">
          <label class="tecnm-label">Nombre del Formato *</label>
          <input
            v-model="form.name"
            type="text"
            class="tecnm-input"
            placeholder="Ej. Solicitud de Residencia Profesional"
            maxlength="200"
            required
          />
        </div>

        <div class="tecnm-form-group" style="margin-bottom: 1rem;">
          <label class="tecnm-label">Categoría *</label>
          <select v-model="form.category" class="tecnm-select" required>
            <option v-for="cat in CATEGORIES" :key="cat" :value="cat">
              {{ cat }}
            </option>
          </select>
        </div>

        <div class="tecnm-form-group" style="margin-bottom: 1rem;">
          <label class="tecnm-label">Descripción / Instrucciones (Opcional)</label>
          <textarea
            v-model="form.description"
            class="tecnm-input"
            rows="3"
            placeholder="Indicaciones sobre cuándo debe entregarse o completarse este formato..."
          ></textarea>
        </div>

        <!-- Carga de Archivo (Obligatorio sólo en creación) -->
        <div v-if="!isEditing" class="tecnm-form-group" style="margin-bottom: 1rem;">
          <label class="tecnm-label">Archivo Inicial (.PDF o .DOCX / Word) *</label>
          <input
            ref="fileInputRef"
            type="file"
            class="tecnm-input"
            accept=".pdf,.docx,.doc,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document,application/msword"
            required
            @change="handleFileChange"
          />
          <small class="tecnm-help-text">Tamaño máximo: 25MB. Formatos válidos: PDF, DOCX, DOC.</small>
        </div>

        <div style="display: flex; justify-content: flex-end; gap: 0.75rem; margin-top: 1.5rem;">
          <button
            type="button"
            class="tecnm-btn tecnm-btn-secondary"
            :disabled="isSubmitting"
            @click="isModalOpen = false"
          >
            Cancelar
          </button>
          <button
            type="submit"
            class="tecnm-btn tecnm-btn-primary"
            :disabled="isSubmitting"
          >
            {{ isSubmitting ? 'Guardando...' : (isEditing ? 'Actualizar Metadatos' : 'Subir y Publicar') }}
          </button>
        </div>
      </form>
    </TecnmModal>

    <!-- ==================================================== -->
    <!-- MODAL 2: Subir Nueva Versión (Admin) -->
    <!-- ==================================================== -->
    <TecnmModal
      v-model="isVersionModalOpen"
      title="Subir Nueva Versión de Formato"
      max-width="560px"
    >
      <form v-if="targetTemplate" @submit.prevent="submitNewVersion">
        <div style="margin-bottom: 1.25rem; padding: 0.75rem; background: var(--tecnm-bg-main); border-radius: 6px;">
          <div style="font-weight: 600; color: var(--tecnm-blue-primary);">
            {{ targetTemplate.name }} ({{ targetTemplate.code }})
          </div>
          <div style="font-size: 0.85rem; color: var(--tecnm-gray-600); margin-top: 0.25rem;">
            Versión vigente actual: <strong>v{{ targetTemplate.currentVersion }}</strong> &bull; Archivo: {{ targetTemplate.currentFileName }}
          </div>
        </div>

        <div class="tecnm-form-group" style="margin-bottom: 1rem;">
          <label class="tecnm-label">Nuevo Archivo (.PDF o .DOCX / Word) *</label>
          <input
            ref="versionFileInputRef"
            type="file"
            class="tecnm-input"
            accept=".pdf,.docx,.doc,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document,application/msword"
            required
            @change="handleVersionFileChange"
          />
          <small class="tecnm-help-text">
            Este archivo se registrará como la <strong>versión {{ targetTemplate.currentVersion + 1 }}</strong>.
          </small>
        </div>

        <div class="tecnm-form-group" style="margin-bottom: 1rem;">
          <label class="tecnm-label">Motivo o Notas de Cambio</label>
          <textarea
            v-model="versionNotes"
            class="tecnm-input"
            rows="3"
            placeholder="Ej. Actualización de logotipo institucional 2026, adición de cláusula de evaluación..."
          ></textarea>
        </div>

        <div style="display: flex; justify-content: flex-end; gap: 0.75rem; margin-top: 1.5rem;">
          <button
            type="button"
            class="tecnm-btn tecnm-btn-secondary"
            :disabled="isSubmitting"
            @click="isVersionModalOpen = false"
          >
            Cancelar
          </button>
          <button
            type="submit"
            class="tecnm-btn tecnm-btn-primary"
            :disabled="isSubmitting"
          >
            {{ isSubmitting ? 'Guardando...' : `Publicar Versión ${targetTemplate.currentVersion + 1}` }}
          </button>
        </div>
      </form>
    </TecnmModal>

    <!-- ==================================================== -->
    <!-- MODAL 3: Historial Completo de Versiones (Admin) -->
    <!-- ==================================================== -->
    <TecnmModal
      v-model="isHistoryModalOpen"
      title="Historial de Versiones Institucionales"
      max-width="780px"
    >
      <div v-if="historyTemplate">
        <div style="margin-bottom: 1rem; padding: 0.75rem; background: var(--tecnm-bg-main); border-radius: 6px;">
          <span class="tecnm-badge tecnm-badge-primary" style="margin-right: 0.5rem;">{{ historyTemplate.code }}</span>
          <strong>{{ historyTemplate.name }}</strong>
        </div>

        <div v-if="isHistoryLoading" style="text-align: center; padding: 2rem; color: var(--tecnm-gray-600);">
          Cargando historial de versiones...
        </div>

        <div v-else-if="historyVersions.length === 0" style="text-align: center; padding: 2rem; color: var(--tecnm-gray-500);">
          No se encontraron versiones registradas para este formato.
        </div>

        <div v-else class="tecnm-table-responsive" style="max-height: 420px; overflow-y: auto;">
          <table class="tecnm-table">
            <thead>
              <tr>
                <th style="width: 80px; text-align: center;">Versión</th>
                <th>Archivo</th>
                <th style="width: 130px;">Subido por</th>
                <th style="width: 140px;">Fecha</th>
                <th>Notas de Cambio</th>
                <th style="width: 90px; text-align: center;">Descarga</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="ver in historyVersions" :key="ver.id">
                <td style="text-align: center;">
                  <span
                    class="tecnm-badge"
                    :class="ver.versionNumber === historyTemplate.currentVersion ? 'tecnm-badge-success' : 'tecnm-badge-secondary'"
                  >
                    v{{ ver.versionNumber }}
                    <span v-if="ver.versionNumber === historyTemplate.currentVersion"> (actual)</span>
                  </span>
                </td>
                <td>
                  <div style="font-weight: 500; font-size: 0.9rem;">{{ ver.fileName }}</div>
                  <small style="color: var(--tecnm-gray-600);">{{ formatBytes(ver.fileSize) }}</small>
                </td>
                <td style="font-size: 0.85rem;">
                  {{ ver.uploaderName || 'Administrador' }}
                </td>
                <td style="font-size: 0.85rem;">
                  {{ formatAuditDate(ver.uploadedAt) }}
                </td>
                <td style="font-size: 0.85rem; color: var(--tecnm-gray-700);">
                  {{ ver.changeNotes || '—' }}
                </td>
                <td style="text-align: center;">
                  <button
                    type="button"
                    class="tecnm-btn tecnm-btn-sm tecnm-btn-outline"
                    title="Descargar este archivo histórico"
                    @click="downloadSpecificVersion(historyTemplate.id, ver)"
                  >
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                      <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
                      <polyline points="7 10 12 15 17 10" />
                      <line x1="12" y1="15" x2="12" y2="3" />
                    </svg>
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div style="display: flex; justify-content: flex-end; margin-top: 1.5rem;">
          <button
            type="button"
            class="tecnm-btn tecnm-btn-secondary"
            @click="isHistoryModalOpen = false"
          >
            Cerrar
          </button>
        </div>
      </div>
    </TecnmModal>
  </div>
</template>

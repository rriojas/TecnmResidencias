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
import AdvisorWorkloadModal from '@/components/advisors/AdvisorWorkloadModal.vue'

const authStore = useAuthStore()
const { confirm } = useConfirm()
const { showAudit } = useAudit()
const { open: openSearch } = useGlobalSearch()

// Estado
const advisors = ref([])
const includeInactive = ref(false)
const sortBy = ref('FullName')
const sortDir = ref('asc')
const searchTerm = ref('')
const isLoading = ref(false)

// Paginación
const pageNumber = ref(1)
const pageSize = ref(10)
const totalCount = ref(0)
const totalPages = ref(0)

// Modal de Expediente de Carga Docente
const selectedAdvisorForModal = ref(null)
const isWorkloadModalOpen = ref(false)

function openAdvisorWorkloadModal(advId) {
  selectedAdvisorForModal.value = advId
  isWorkloadModalOpen.value = true
}

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

// Modal Formulario
const isModalOpen = ref(false)
const isEditMode = ref(false)
const editingAdvisorId = ref(null)
const isSubmitting = ref(false)
const formError = ref('')

const DEPARTMENTS = ref({})

async function loadDepartmentsCatalog() {
  try {
    const res = await apiClient.get('/v1/careers/all')
    const list = res.data || []
    list.forEach(c => {
      DEPARTMENTS.value[c.id] = c.name
    })
  } catch {}
}

const form = ref({
  userId: '',
  fullName: '',
  title: '',
  departmentId: 4,
  advisorType: 1,
  phone: '',
})

const selectedUserInitial = ref(null)

const canCreate = computed(() => {
  if (authStore.hasRole('vinculacion')) return false
  return (
    authStore.isAdmin ||
    authStore.isCareerHead ||
    authStore.hasPermission('advisors.manage') ||
    authStore.hasRole('admin', 'departmenthead', 'academic', 'careerhead', 'jefecarrera')
  )
})

// Modal Carga Masiva Excel
const isImportModalOpen = ref(false)
const importFile = ref(null)
const importError = ref('')
const importResult = ref(null)
const isImporting = ref(false)

function openImportModal() {
  importFile.value = null
  importError.value = ''
  importResult.value = null
  isImportModalOpen.value = true
}

function handleImportFileChange(e) {
  const file = e.target.files?.[0]
  importFile.value = file || null
  importError.value = ''
}

async function handleDownloadAdvisorTemplate() {
  try {
    const res = await apiClient.get('/v1/advisors/import/template', { responseType: 'blob' })
    const url = window.URL.createObjectURL(new Blob([res.data]))
    const link = document.createElement('a')
    link.href = url
    link.setAttribute('download', 'Plantilla_Asesores.xlsx')
    document.body.appendChild(link)
    link.click()
    link.remove()
    window.URL.revokeObjectURL(url)
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al descargar la plantilla de asesores.', 'danger')
  }
}

async function handleImportSubmit() {
  if (!importFile.value) {
    importError.value = 'Por favor seleccione un archivo Excel (.xlsx o .xls).'
    return
  }

  isImporting.value = true
  importError.value = ''
  importResult.value = null

  try {
    const formData = new FormData()
    formData.append('file', importFile.value)

    const res = await apiClient.post('/v1/advisors/import/excel', formData, {
      headers: { 'Content-Type': 'multipart/form-data' }
    })

    importResult.value = res.data
    showAlert(`Importación finalizada: ${res.data.successCount} asesor(es) registrado(s).`, 'success')
    loadAdvisors()
  } catch (err) {
    importError.value = err.response?.data?.message || 'Error al procesar el archivo Excel.'
  } finally {
    isImporting.value = false
  }
}

const sortedAdvisors = computed(() => {
  let list = [...advisors.value]
  const field = sortBy.value
  const dir = sortDir.value === 'asc' ? 1 : -1

  return list.sort((a, b) => {
    let valA = ''
    let valB = ''

    if (field === 'FullName') {
      valA = a.fullName || a.name || ''
      valB = b.fullName || b.name || ''
    } else if (field === 'AdvisorType') {
      valA = a.advisorType || 0
      valB = b.advisorType || 0
    } else if (field === 'Title') {
      valA = a.title || ''
      valB = b.title || ''
    } else if (field === 'DepartmentId') {
      valA = a.departmentName || a.departmentId || ''
      valB = b.departmentName || b.departmentId || ''
    } else if (field === 'Phone') {
      valA = a.phone || ''
      valB = b.phone || ''
    } else if (field === 'IsActive') {
      valA = a.isActive ? 1 : 0
      valB = b.isActive ? 1 : 0
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

async function loadAdvisors({ silent = false } = {}) {
  if (!silent) isLoading.value = true
  try {
    const params = {
      pageNumber: pageNumber.value,
      pageSize: pageSize.value,
      sortBy: sortBy.value,
      sortDir: sortDir.value,
      includeInactive: includeInactive.value,
    }
    const res = await apiClient.get('/v1/advisors', { params })
    const data = res.data
    advisors.value = Array.isArray(data) ? data : (data.items || [])
    totalCount.value = data.totalCount || advisors.value.length
    totalPages.value = data.totalPages || Math.ceil(totalCount.value / pageSize.value) || 1
  } catch (err) {
    if (!silent) {
      showAlert(err.response?.data?.message || 'Error al cargar directorio de asesores.', 'danger')
      advisors.value = []
    }
  } finally {
    if (!silent) isLoading.value = false
  }
}

function handleSort(col) {
  if (sortBy.value === col) {
    sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
  } else {
    sortBy.value = col
    sortDir.value = 'asc'
  }
  loadAdvisors({ silent: true })
}

function getSortClass(col) {
  if (sortBy.value !== col) return 'tecnm-sort-th'
  return sortDir.value === 'asc' ? 'tecnm-sort-th tecnm-sort-asc' : 'tecnm-sort-th tecnm-sort-desc'
}

function openCreateModal() {
  isEditMode.value = false
  editingAdvisorId.value = null
  selectedUserInitial.value = null
  form.value = {
    email: '',
    password: '',
    firstName: '',
    lastName: '',
    title: '',
    departmentId: authStore.isCareerHead ? (authStore.user?.careerId || 4) : 4,
    advisorType: 1,
    phone: '',
  }
  formError.value = ''
  isModalOpen.value = true
}

async function openEditModal(advisor) {
  try {
    const res = await apiClient.get(`/v1/advisors/${advisor.id}`)
    const a = res.data
    isEditMode.value = true
    editingAdvisorId.value = a.id
    selectedUserInitial.value = a.userId ? { id: a.userId, email: a.userEmail, fullName: a.fullName } : null
    
    const rawName = (a.fullName || a.name || '').trim()
    const nameParts = rawName ? rawName.split(/\s+/) : []
    const firstName = nameParts.length > 1 ? nameParts.slice(0, Math.max(1, nameParts.length - 2)).join(' ') : (nameParts[0] || '')
    const lastName = nameParts.length > 1 ? nameParts.slice(Math.max(1, nameParts.length - 2)).join(' ') : ''

    form.value = {
      email: a.userEmail || a.email || '',
      password: '',
      firstName: firstName,
      lastName: lastName,
      title: a.title || '',
      departmentId: a.departmentId || 1,
      advisorType: a.advisorType || 1,
      phone: a.phone || '',
    }
    formError.value = ''
    isModalOpen.value = true
  } catch {
    showAlert('Error al cargar datos del asesor.', 'danger')
  }
}

function onUserSelected(user) {
  if (user && user.fullName && !form.value.fullName) {
    form.value.fullName = user.fullName
  }
}

async function handleSubmit() {
  formError.value = ''

  if (!form.value.firstName.trim()) {
    formError.value = 'Ingrese el nombre o nombres del asesor.'
    return
  }

  if (!form.value.lastName.trim()) {
    formError.value = 'Ingrese los apellidos del asesor.'
    return
  }

  if (!isEditMode.value) {
    const cleanEmail = (form.value.email || '').trim().toLowerCase()
    if (!cleanEmail || !cleanEmail.includes('@') || !cleanEmail.includes('.')) {
      formError.value = 'Ingrese un correo electrónico válido para la cuenta de usuario del asesor.'
      return
    }
  }

  const computedFullName = `${form.value.firstName.trim()} ${form.value.lastName.trim()}`.trim()
  isSubmitting.value = true

  try {
    if (isEditMode.value) {
      await apiClient.put(`/v1/advisors/${editingAdvisorId.value}`, {
        firstName: form.value.firstName.trim(),
        lastName: form.value.lastName.trim(),
        fullName: computedFullName,
        title: form.value.title.trim() || undefined,
        departmentId: Number(form.value.departmentId),
        advisorType: Number(form.value.advisorType),
        phone: form.value.phone.trim() || undefined,
      })
      showAlert('Asesor actualizado exitosamente.', 'success')
    } else {
      await apiClient.post('/v1/advisors', {
        email: form.value.email.trim().toLowerCase(),
        password: form.value.password?.trim() || undefined,
        firstName: form.value.firstName.trim(),
        lastName: form.value.lastName.trim(),
        fullName: computedFullName,
        title: form.value.title.trim() || undefined,
        departmentId: Number(form.value.departmentId),
        advisorType: Number(form.value.advisorType),
        phone: form.value.phone.trim() || undefined,
      })
      showAlert('Asesor y cuenta de usuario registrados exitosamente.', 'success')
    }
    isModalOpen.value = false
    loadAdvisors()
  } catch (err) {
    formError.value =
      err.response?.data?.message ||
      'Error al guardar el asesor. Verifique los datos ingresados.'
  } finally {
    isSubmitting.value = false
  }
}

async function handleDeactivate(advisor) {
  const confirmed = await confirm({
    title: 'Desactivar Asesor',
    message: `¿Está seguro de desactivar al asesor ${advisor.fullName || advisor.name}?`,
    okText: 'Desactivar',
    cancelText: 'Cancelar',
  })
  if (!confirmed) return

  try {
    await apiClient.delete(`/v1/advisors/${advisor.id}`)
    showAlert('Asesor desactivado correctamente.', 'success')
    loadAdvisors()
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al desactivar asesor.', 'danger')
  }
}

async function handleReactivate(advisor) {
  try {
    await apiClient.patch(`/v1/advisors/${advisor.id}/activate`)
    showAlert('Asesor reactivado correctamente.', 'success')
    loadAdvisors()
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al reactivar asesor.', 'danger')
  }
}

function handleAudit(advisor) {
  showAudit({
    title: `Auditoría — Asesor #${advisor.id} (${advisor.fullName || advisor.name})`,
    item: advisor,
  })
}

async function handleExportPdf() {
  try {
    const params = {
      sortBy: sortBy.value,
      sortDir: sortDir.value,
      includeInactive: includeInactive.value,
    }
    const res = await apiClient.get('/v1/advisors/export', {
      params,
      responseType: 'blob',
    })
    const blob = new Blob([res.data], { type: 'application/pdf' })
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = 'asesores_tecnm.pdf'
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    window.URL.revokeObjectURL(url)
  } catch {
    showAlert('Error al exportar el PDF de asesores.', 'danger')
  }
}

onMounted(() => {
  loadDepartmentsCatalog()
  loadAdvisors()
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
        <h1 class="tecnm-page-title">Directorio de Asesores Institucionales</h1>
        <p class="tecnm-page-subtitle">Gestión y catálogo de docentes y asesores vinculados a residencias profesionales</p>
      </div>
      <div class="tecnm-page-actions">
        <button
          type="button"
          class="tecnm-btn tecnm-btn-secondary"
          @click="openSearch({ initialSource: 'ADVISORS' })"
        >
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
          </svg>
          <span>Abrir búsqueda</span>
        </button>
        <span class="tecnm-page-actions-divider" aria-hidden="true"></span>
        <button
          v-if="canCreate"
          id="openBatchImportAdvisorBtn"
          type="button"
          class="tecnm-btn tecnm-btn-secondary"
          @click="openImportModal"
        >
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" style="margin-right: 0.35rem; display: inline-block; vertical-align: middle;">
            <path stroke-linecap="round" stroke-linejoin="round" d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5m-13.5-9L12 3m0 0l4.5 4.5M12 3v13.5" />
          </svg>
          <span>Importar Asesores</span>
        </button>
        <button
          v-if="canCreate"
          id="openCreateAdvisorModalBtn"
          type="button"
          class="tecnm-btn tecnm-btn-primary"
          @click="openCreateModal"
        >
          + Registrar Nuevo Asesor
        </button>
      </div>
    </div>

    <!-- Tarjeta Principal de Tabla -->
    <div class="tecnm-card">
      <div class="tecnm-card-header">
        <h3 class="tecnm-card-title">Asesores Registrados</h3>
      </div>
      <div class="tecnm-card-toolbar">
        <div class="tecnm-toolbar-actions">
          <label v-if="!authStore.isCareerHead" class="tecnm-switch-label">
            <span class="tecnm-switch">
              <input
                id="advisorIncludeInactiveToggle"
                v-model="includeInactive"
                type="checkbox"
                @change="loadAdvisors"
              />
              <span class="tecnm-switch-slider"></span>
            </span>
            Mostrar inactivos
          </label>
          <button
            id="exportAdvisorsBtn"
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
          <table class="tecnm-table tecnm-table-striped">
            <thead>
              <tr>
                <th
                  class="tecnm-th-sortable"
                  @click="handleSort('FullName')"
                >
                  Nombre Completo
                  <span class="tecnm-sort-icon" :class="{ active: sortBy === 'FullName' }">
                    {{ sortBy === 'FullName' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th
                  class="tecnm-th-sortable"
                  @click="handleSort('AdvisorType')"
                >
                  Tipo de Asesor
                  <span class="tecnm-sort-icon" :class="{ active: sortBy === 'AdvisorType' }">
                    {{ sortBy === 'AdvisorType' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th
                  class="tecnm-th-sortable"
                  @click="handleSort('Title')"
                >
                  Título / Grado
                  <span class="tecnm-sort-icon" :class="{ active: sortBy === 'Title' }">
                    {{ sortBy === 'Title' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th
                  class="tecnm-th-sortable"
                  @click="handleSort('DepartmentId')"
                >
                  Departamento
                  <span class="tecnm-sort-icon" :class="{ active: sortBy === 'DepartmentId' }">
                    {{ sortBy === 'DepartmentId' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th
                  class="tecnm-th-sortable"
                  @click="handleSort('Phone')"
                >
                  Teléfono
                  <span class="tecnm-sort-icon" :class="{ active: sortBy === 'Phone' }">
                    {{ sortBy === 'Phone' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th
                  class="tecnm-th-sortable"
                  @click="handleSort('IsActive')"
                >
                  Estado
                  <span class="tecnm-sort-icon" :class="{ active: sortBy === 'IsActive' }">
                    {{ sortBy === 'IsActive' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th class="tecnm-th-actions">Acciones</th>
              </tr>
            </thead>
            <tbody id="advisorsTableBody">
              <tr v-if="isLoading">
                <td colspan="7" class="tecnm-table-empty">
                  Cargando catálogo de asesores...
                </td>
              </tr>
              <tr v-else-if="sortedAdvisors.length === 0">
                <td colspan="7" class="tecnm-table-empty">
                  <span v-if="includeInactive">No hay asesores inactivos (deshabilitados) registrados.</span>
                  <span v-else>No hay asesores registrados.</span>
                </td>
              </tr>
              <tr
                v-for="a in sortedAdvisors"
                v-else
                :key="a.id"
              >
                <td>
                  <strong
                    style="cursor: pointer; color: var(--tecnm-blue-primary, #1B396A);"
                    :title="`Ver expediente de ${a.fullName || a.name}`"
                    @click="openAdvisorWorkloadModal(a.id)"
                  >
                    {{ a.fullName || a.name }}
                  </strong>
                </td>
                <td>
                  <span class="tecnm-badge tecnm-badge-neutral">
                    {{ a.advisorType === 2 ? 'Externo (Empresa)' : 'Interno (TecNM)' }}
                  </span>
                </td>
                <td>{{ a.title || '—' }}</td>
                <td>{{ a.departmentName || DEPARTMENTS[a.departmentId] || 'General' }}</td>
                <td>{{ a.phone || '—' }}</td>
                <td>
                  <TecnmBadge :status="a.isActive ? 'Activo' : 'Inactivo'" />
                </td>
                <td>
                  <div class="tecnm-row-actions">
                    <button
                      type="button"
                      class="tecnm-btn tecnm-btn-outline-primary tecnm-btn-sm"
                      title="Ver residentes asignados y carga docente"
                      @click="openAdvisorWorkloadModal(a.id)"
                    >
                      <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Z" />
                      </svg>
                      <span>Residentes</span>
                    </button>
                    <button
                      v-if="!authStore.isReadOnly && canCreate"
                      type="button"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                      @click="openEditModal(a)"
                    >
                      Editar
                    </button>
                    <button
                      v-if="authStore.canSeeAudit && !authStore.isCareerHead"
                      type="button"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                      @click="handleAudit(a)"
                    >
                      Auditoría
                    </button>
                    <template v-if="!authStore.isReadOnly && canCreate">
                      <button
                        v-if="a.isActive"
                        type="button"
                        class="tecnm-btn tecnm-btn-danger tecnm-btn-sm"
                        @click="handleDeactivate(a)"
                      >
                        Desactivar
                      </button>
                      <button
                        v-else
                        type="button"
                        class="tecnm-btn tecnm-btn-success tecnm-btn-sm"
                        @click="handleReactivate(a)"
                      >
                        Reactivar
                      </button>
                    </template>
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
          @page-change="loadAdvisors"
        />
      </div>
    </div>

    <!-- Modal Registrar / Editar Asesor -->
    <div
      v-if="isModalOpen"
      id="createAdvisorModal"
      class="modal-backdrop active"
      role="dialog"
      aria-modal="true"
      @click.self="isModalOpen = false"
    >
      <div class="modal-card">
        <div class="tecnm-modal-header">
          <h3 id="advisorModalTitle" class="tecnm-modal-title">
            {{ isEditMode ? 'Editar Asesor' : 'Registrar Nuevo Asesor' }}
          </h3>
          <button
            type="button"
            class="tecnm-modal-close"
            aria-label="Cerrar"
            @click="isModalOpen = false"
          >
            &times;
          </button>
        </div>

        <form id="advisorForm" @submit.prevent="handleSubmit">
          <div
            v-if="formError"
            class="tecnm-alert tecnm-alert-danger"
            style="margin-bottom: 1rem;"
            role="alert"
          >
            <span>{{ formError }}</span>
          </div>

          <div class="tecnm-form-grid">
            <div class="tecnm-form-group">
              <label for="advisorEmail" class="tecnm-label">Correo Electrónico (Cuenta de Acceso) *</label>
              <input
                id="advisorEmail"
                v-model="form.email"
                type="email"
                class="tecnm-form-control"
                placeholder="ejemplo@monclova.tecnm.mx"
                :disabled="isSubmitting || isEditMode"
                required
              />
              <small class="tecnm-form-hint">Cuenta de acceso con la que el asesor iniciará sesión. Se le asignará el rol de asesor.</small>
            </div>

            <div v-if="!isEditMode" class="tecnm-form-group">
              <label for="advisorPassword" class="tecnm-label">Contraseña Inicial</label>
              <input
                id="advisorPassword"
                v-model="form.password"
                type="password"
                class="tecnm-form-control"
                placeholder="Opcional (Por defecto: Docente2026!)"
                :disabled="isSubmitting"
              />
              <small class="tecnm-form-hint">Opcional. Si se deja vacía, se asignará la contraseña institucional: Docente2026!</small>
            </div>

            <div class="tecnm-form-group">
              <label for="advisorFirstName" class="tecnm-label">Nombre(s) *</label>
              <input
                id="advisorFirstName"
                v-model="form.firstName"
                type="text"
                class="tecnm-form-control"
                placeholder="Ej. Carlos"
                :disabled="isSubmitting"
                required
              />
            </div>

            <div class="tecnm-form-group">
              <label for="advisorLastName" class="tecnm-label">Apellidos *</label>
              <input
                id="advisorLastName"
                v-model="form.lastName"
                type="text"
                class="tecnm-form-control"
                placeholder="Ej. Mendoza Sánchez"
                :disabled="isSubmitting"
                required
              />
            </div>

            <div class="tecnm-form-group">
              <label for="title" class="tecnm-label">Título / Grado Académico</label>
              <input
                id="title"
                v-model="form.title"
                type="text"
                class="tecnm-form-control"
                placeholder="Ej. Ing., Dr., M.C."
                :disabled="isSubmitting"
              />
            </div>

            <div class="tecnm-form-group">
              <label for="departmentId" class="tecnm-label">Departamento Académico *</label>
              <select
                id="departmentId"
                v-model="form.departmentId"
                class="tecnm-form-control"
                :disabled="isSubmitting || authStore.isCareerHead"
                required
              >
                <option v-for="(name, id) in DEPARTMENTS" :key="id" :value="Number(id)">
                  {{ name }}
                </option>
              </select>
            </div>

            <div class="tecnm-form-group">
              <label for="advisorType" class="tecnm-label">Tipo de Asesor *</label>
              <select
                id="advisorType"
                v-model="form.advisorType"
                class="tecnm-form-control"
                :disabled="isSubmitting"
                required
              >
                <option :value="1">Interno (Docente TecNM)</option>
                <option :value="2">Externo (Empresa / Institución)</option>
              </select>
            </div>

            <div class="tecnm-form-group">
              <label for="phone" class="tecnm-label">Teléfono de Contacto</label>
              <input
                id="phone"
                v-model="form.phone"
                type="text"
                class="tecnm-form-control"
                placeholder="Ej. 8661234567"
                :disabled="isSubmitting"
              />
            </div>
          </div>

          <div class="tecnm-modal-footer">
            <button
              id="cancelAdvisorModalBtn"
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
              <span v-if="!isSubmitting">Guardar Asesor</span>
              <span v-else class="login-spinner"></span>
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>

  <!-- Modal Carga Masiva Excel Asesores -->
  <div
    v-if="isImportModalOpen"
    id="importAdvisorsModal"
    class="modal-backdrop active"
    role="dialog"
    aria-modal="true"
    @click.self="isImportModalOpen = false"
  >
    <div class="modal-card" style="max-width: 800px; width: 95%;">
      <div class="tecnm-modal-header">
        <h3 class="tecnm-modal-title">Carga Masiva de Asesores vía Excel</h3>
        <button
          type="button"
          class="tecnm-modal-close"
          aria-label="Cerrar"
          @click="isImportModalOpen = false"
        >
          &times;
        </button>
      </div>

      <form @submit.prevent="handleImportSubmit">
        <div class="tecnm-card" style="margin-bottom: 1rem; border: 1px solid var(--tecnm-border-color, #e2e8f0); background: #f8fafc; padding: 0.75rem; border-radius: 8px;">
          <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.5rem; flex-wrap: wrap; gap: 0.5rem;">
            <span style="font-weight: 700; font-size: 0.9rem; color: var(--tecnm-primary, #1b396a); display: inline-flex; align-items: center; gap: 0.35rem;">
              <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z" />
              </svg>
              <span>Especificación de Columnas</span>
              <span style="font-size: 0.8rem; color: #b91c1c; font-weight: 600;">(Todos los campos son obligatorios sin excepción)</span>
            </span>
            <button
              type="button"
              class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
              @click="handleDownloadAdvisorTemplate"
            >
              <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" style="margin-right: 0.25rem; display: inline-block; vertical-align: middle;">
                <path stroke-linecap="round" stroke-linejoin="round" d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5M16.5 12 12 16.5m0 0L7.5 12m4.5 4.5V3" />
              </svg>
              <span>Descargar Plantilla Excel</span>
            </button>
          </div>
          <div style="overflow-x: auto;">
            <table class="tecnm-table" style="font-size: 0.8rem; margin-bottom: 0; background: #fff;">
              <thead>
                <tr style="background: #eef2f6;">
                  <th style="padding: 0.4rem 0.6rem;">Columna</th>
                  <th style="padding: 0.4rem 0.6rem;">Formato Esperado</th>
                  <th style="padding: 0.4rem 0.6rem;">Valores Aceptados / Ejemplo</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td style="padding: 0.35rem 0.6rem;"><code>Nombre</code></td>
                  <td style="padding: 0.35rem 0.6rem;">Nombre completo del docente o asesor</td>
                  <td style="padding: 0.35rem 0.6rem;"><em>Dr. Carlos Mendoza Sánchez</em></td>
                </tr>
                <tr>
                  <td style="padding: 0.35rem 0.6rem;"><code>Titulo</code></td>
                  <td style="padding: 0.35rem 0.6rem;">Grado académico o abreviatura</td>
                  <td style="padding: 0.35rem 0.6rem;"><em>M.T.I., Dr., Ing., Lic., M.C.</em></td>
                </tr>
                <tr>
                  <td style="padding: 0.35rem 0.6rem;"><code>Email</code></td>
                  <td style="padding: 0.35rem 0.6rem;">Correo electrónico institucional para la cuenta de acceso</td>
                  <td style="padding: 0.35rem 0.6rem;"><code>carlos.mendoza@monclova.tecnm.mx</code></td>
                </tr>
                <tr>
                  <td style="padding: 0.35rem 0.6rem;"><code>Telefono</code></td>
                  <td style="padding: 0.35rem 0.6rem;">Número telefónico de contacto (10 dígitos)</td>
                  <td style="padding: 0.35rem 0.6rem;"><code>8661234567</code></td>
                </tr>
                <tr>
                  <td style="padding: 0.35rem 0.6rem;"><code>Departamento</code></td>
                  <td style="padding: 0.35rem 0.6rem;">ID numérico de la carrera/departamento (1 a 7)</td>
                  <td style="padding: 0.35rem 0.6rem;">
                    <code>1</code>: Ing. Informática (INF)<br />
                    <code>2</code>: Ing. Industrial (IND)<br />
                    <code>3</code>: Ing. Mecatrónica (MEC)<br />
                    <code>4</code>: Ing. en Energías Renovables (IER)<br />
                    <code>5</code>: Ing. Electrónica (ELE)<br />
                    <code>6</code>: Ing. en Gestión Empresarial (IGE)<br />
                    <code>7</code>: Ing. Mecánica (IME)<br />
                    <small v-if="authStore.isCareerHead" style="color: #0369a1; font-weight: 600;">
                      * Nota: Como Jefe de Carrera, se asignará automáticamente a su carrera.
                    </small>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <div v-if="importError" class="tecnm-alert tecnm-alert-danger" style="margin-bottom: 1rem;" role="alert">
          <span>{{ importError }}</span>
        </div>

        <div v-if="importResult" class="tecnm-alert tecnm-alert-info" style="margin-bottom: 1rem;">
          <strong>Resumen de Importación:</strong>
          <ul>
            <li>Filas procesadas: {{ importResult.totalRows }}</li>
            <li>Asesores registrados: {{ importResult.successCount }}</li>
            <li>Omitidos (Duplicados): {{ importResult.skippedCount }}</li>
            <li>Errores de fila: {{ importResult.errorCount }}</li>
          </ul>
          <div v-if="importResult.errors && importResult.errors.length > 0" style="margin-top: 0.5rem; max-height: 120px; overflow-y: auto;">
            <small class="tecnm-text-danger">
              <strong>Detalle de errores:</strong>
              <ul style="margin: 0; padding-left: 1.2rem;">
                <li v-for="(err, idx) in importResult.errors" :key="idx">{{ err }}</li>
              </ul>
            </small>
          </div>
          <div v-if="importResult.skipped && importResult.skipped.length > 0" style="margin-top: 0.5rem; max-height: 100px; overflow-y: auto;">
            <small class="tecnm-text-muted">
              <strong>Detalle de omitidos:</strong>
              <ul style="margin: 0; padding-left: 1.2rem;">
                <li v-for="(skip, idx) in importResult.skipped" :key="idx">{{ skip }}</li>
              </ul>
            </small>
          </div>
        </div>

        <div class="tecnm-form-group">
          <label for="importAdvisorFile" class="tecnm-label">Seleccionar Archivo Excel (.xlsx, .xls) *</label>
          <input
            id="importAdvisorFile"
            type="file"
            class="tecnm-form-control"
            accept=".xlsx, .xls"
            :disabled="isImporting"
            required
            @change="handleImportFileChange"
          />
          <small class="tecnm-form-hint">
            Los asesores nuevos se registrarán automáticamente con cuenta de acceso institucional rol Asesor.
          </small>
        </div>

        <div class="tecnm-modal-footer">
          <button
            type="button"
            class="tecnm-btn tecnm-btn-secondary"
            :disabled="isImporting"
            @click="isImportModalOpen = false"
          >
            Cerrar
          </button>
          <button
            type="submit"
            class="tecnm-btn tecnm-btn-primary"
            :disabled="isImporting || !importFile"
          >
            <span v-if="!isImporting">Iniciar Carga Masiva</span>
            <span v-else class="login-spinner"></span>
          </button>
        </div>
      </form>
    </div>
  </div>

  <!-- Modal de Detalle de Carga y Residentes por Asesor -->
  <AdvisorWorkloadModal
    v-model="isWorkloadModalOpen"
    :advisor-id="selectedAdvisorForModal"
  />
</template>

<style scoped>
.tecnm-row-actions {
  display: inline-flex;
  gap: 0.35rem;
}
</style>

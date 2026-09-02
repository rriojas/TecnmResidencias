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
  if (authStore.hasRole('vinculacion') || authStore.isCareerHead) return false
  return (
    authStore.isAdmin ||
    authStore.hasPermission('advisors.manage') ||
    authStore.hasRole('admin', 'departmenthead', 'academic')
  )
})

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
    userId: '',
    fullName: '',
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
    form.value = {
      userId: a.userId || '',
      fullName: a.fullName || a.name || '',
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

  if (!isEditMode.value && !form.value.userId) {
    formError.value = 'Seleccione una cuenta de usuario institucional para vincular al asesor.'
    return
  }

  if (!form.value.fullName.trim()) {
    formError.value = 'Ingrese el nombre completo del asesor.'
    return
  }

  isSubmitting.value = true

  try {
    if (isEditMode.value) {
      await apiClient.put(`/v1/advisors/${editingAdvisorId.value}`, {
        fullName: form.value.fullName.trim(),
        title: form.value.title.trim() || undefined,
        departmentId: Number(form.value.departmentId),
        advisorType: Number(form.value.advisorType),
        phone: form.value.phone.trim() || undefined,
      })
      showAlert('Asesor actualizado exitosamente.', 'success')
    } else {
      await apiClient.post('/v1/advisors', {
        userId: Number(form.value.userId),
        fullName: form.value.fullName.trim(),
        title: form.value.title.trim() || undefined,
        departmentId: Number(form.value.departmentId),
        advisorType: Number(form.value.advisorType),
        phone: form.value.phone.trim() || undefined,
      })
      showAlert('Asesor registrado exitosamente.', 'success')
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
          <label class="tecnm-switch-label">
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
            <div v-if="!isEditMode" id="advisorUserFormGroup" class="tecnm-form-group">
              <label for="userId" class="tecnm-label">Cuenta de acceso del asesor *</label>
              <div id="advisorUserAutocompleteWrapper">
                <TecnmAutocomplete
                  v-model="form.userId"
                  endpoint="/v1/searches/autocomplete"
                  :extra-params="{ sourceKey: 'USERS' }"
                  global-search-source="USERS"
                  placeholder="Buscar usuario por correo o nombre..."
                  :initial-item="selectedUserInitial"
                  @select="onUserSelected"
                />
              </div>
              <small class="tecnm-form-hint">Vincula una cuenta de usuario existente (correo institucional) con este perfil. Al guardar se le asignará el rol Asesor.</small>
            </div>

            <div class="tecnm-form-group">
              <label for="fullName" class="tecnm-label">Nombre Completo *</label>
              <input
                id="fullName"
                v-model="form.fullName"
                type="text"
                class="tecnm-form-control"
                placeholder="Nombre y apellidos"
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

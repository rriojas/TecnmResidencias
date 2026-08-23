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

const form = ref({
  userId: '',
  fullName: '',
  title: '',
  departmentId: 1,
  advisorType: 1,
  phone: '',
})

const selectedUserInitial = ref(null)

const canCreate = computed(() => {
  return (
    authStore.isAdmin ||
    authStore.hasPermission('advisors.manage') ||
    authStore.hasRole('admin', 'departmenthead')
  )
})

async function loadAdvisors() {
  isLoading.value = true
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
    showAlert(err.response?.data?.message || 'Error al cargar directorio de asesores.', 'danger')
    advisors.value = []
  } finally {
    isLoading.value = false
  }
}

function handleSort(col) {
  if (sortBy.value === col) {
    sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
  } else {
    sortBy.value = col
    sortDir.value = 'asc'
  }
  loadAdvisors()
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
    departmentId: 1,
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
                  data-sort="FullName"
                  :class="getSortClass('FullName')"
                  @click="handleSort('FullName')"
                >
                  Nombre Completo
                </th>
                <th>Tipo de Asesor</th>
                <th>Título / Grado</th>
                <th>Departamento</th>
                <th>Teléfono</th>
                <th
                  data-sort="IsActive"
                  :class="getSortClass('IsActive')"
                  @click="handleSort('IsActive')"
                >
                  Estado
                </th>
                <th>Acciones</th>
              </tr>
            </thead>
            <tbody id="advisorsTableBody">
              <tr v-if="isLoading">
                <td colspan="7" class="tecnm-table-empty">
                  Cargando catálogo de asesores...
                </td>
              </tr>
              <tr v-else-if="advisors.length === 0">
                <td colspan="7" class="tecnm-table-empty">
                  No hay asesores registrados.
                </td>
              </tr>
              <tr
                v-for="a in advisors"
                v-else
                :key="a.id"
              >
                <td><strong>{{ a.fullName || a.name }}</strong></td>
                <td>
                  <span class="tecnm-badge tecnm-badge-neutral">
                    {{ a.advisorType === 2 ? 'Externo (Empresa)' : 'Interno (TecNM)' }}
                  </span>
                </td>
                <td>{{ a.title || '—' }}</td>
                <td>{{ a.departmentName || (a.departmentId === 1 ? 'ISC' : a.departmentId === 2 ? 'Industrial' : a.departmentId === 3 ? 'IGE' : 'Mecatrónica') }}</td>
                <td>{{ a.phone || '—' }}</td>
                <td>
                  <TecnmBadge :status="a.isActive ? 'Activo' : 'Inactivo'" />
                </td>
                <td>
                  <div class="tecnm-row-actions">
                    <button
                      v-if="!authStore.isReadOnly && canCreate"
                      type="button"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                      @click="openEditModal(a)"
                    >
                      Editar
                    </button>
                    <button
                      v-if="authStore.canSeeAudit"
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
                :disabled="isSubmitting"
                required
              >
                <option :value="1">Ingeniería en Sistemas Computacionales</option>
                <option :value="2">Ingeniería Industrial</option>
                <option :value="3">Ingeniería en Gestión Empresarial</option>
                <option :value="4">Ingeniería Mecatrónica</option>
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
</template>

<style scoped>
.tecnm-row-actions {
  display: inline-flex;
  gap: 0.35rem;
}
</style>

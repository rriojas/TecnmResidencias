<script setup>
import { ref, computed, onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useConfirm } from '@/composables/useConfirm'
import apiClient from '@/services/api'
import TecnmPagination from '@/components/common/TecnmPagination.vue'
import TecnmKpiCard from '@/components/common/TecnmKpiCard.vue'

const authStore = useAuthStore()
const { confirm } = useConfirm()

const careers = ref([])
const isLoading = ref(false)
const isSubmitting = ref(false)

const pageNumber = ref(1)
const pageSize = ref(10)
const totalCount = ref(0)
const totalPages = ref(0)

const searchTerm = ref('')
const statusFilter = ref('all') // 'all' | 'active' | 'inactive'
const sortBy = ref('Name')
const sortDir = ref('asc')

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

// Métricas KPI
const totalCareersCount = computed(() => totalCount.value)
const activeCareersCount = computed(() => careers.value.filter(c => c.isActive).length)
const inactiveCareersCount = computed(() => careers.value.filter(c => !c.isActive).length)

// Modal Crear/Editar
const isModalOpen = ref(false)
const isEditing = ref(false)
const editingId = ref(null)

const form = ref({
  code: '',
  name: '',
  acronym: '',
  departmentId: 1
})

const DEPARTMENTS = ref([])

async function syncDepartmentsCatalog() {
  try {
    const res = await apiClient.get('/v1/careers/all')
    const list = res.data || []
    if (list.length > 0) {
      DEPARTMENTS.value = list.map(c => ({ id: c.id, name: c.name }))
    }
  } catch {}
}

function getDepartmentName(deptId) {
  if (!deptId) return 'Sin Departamento'
  const dept = DEPARTMENTS.value.find(d => Number(d.id) === Number(deptId))
  return dept ? dept.name : `Dept. #${deptId}`
}

async function loadCareers({ silent = false } = {}) {
  if (!silent) isLoading.value = true
  try {
    const params = {
      pageNumber: pageNumber.value,
      pageSize: pageSize.value,
      search: searchTerm.value || undefined,
      status: statusFilter.value !== 'all' ? statusFilter.value : undefined,
      includeInactive: true,
      sortBy: sortBy.value,
      sortDir: sortDir.value
    }
    const res = await apiClient.get('/v1/careers', { params })
    const data = res.data
    careers.value = Array.isArray(data) ? data : (data.items || [])
    totalCount.value = data.totalCount || careers.value.length
    totalPages.value = data.totalPages || Math.ceil(totalCount.value / pageSize.value) || 1
  } catch (err) {
    if (!silent) {
      showAlert(err.response?.data?.message || 'Error al cargar el catálogo de carreras.', 'danger')
      careers.value = []
    }
  } finally {
    if (!silent) isLoading.value = false
  }
}

function handleSearch() {
  pageNumber.value = 1
  loadCareers()
}

function handlePageChange(newPage) {
  pageNumber.value = newPage
  loadCareers()
}

function openCreateModal() {
  isEditing.value = false
  editingId.value = null
  form.value = {
    code: '',
    name: '',
    acronym: '',
    departmentId: 1
  }
  isModalOpen.value = true
}

function openEditModal(career) {
  isEditing.value = true
  editingId.value = career.id
  form.value = {
    code: career.code || '',
    name: career.name || '',
    acronym: career.acronym || '',
    departmentId: career.departmentId || 1
  }
  isModalOpen.value = true
}

function closeModal() {
  isModalOpen.value = false
  isEditing.value = false
  editingId.value = null
}

async function handleSubmit() {
  if (!form.value.code.trim() || !form.value.name.trim()) {
    showAlert('El código y el nombre de la carrera son obligatorios.', 'warning')
    return
  }

  isSubmitting.value = true
  try {
    const payload = {
      code: form.value.code.trim().toUpperCase(),
      name: form.value.name.trim(),
      acronym: form.value.acronym.trim().toUpperCase() || form.value.code.trim().toUpperCase(),
      departmentId: form.value.departmentId ? Number(form.value.departmentId) : null
    }

    if (isEditing.value) {
      await apiClient.put(`/v1/careers/${editingId.value}`, payload)
      showAlert('¡Carrera actualizada correctamente!', 'success')
    } else {
      await apiClient.post('/v1/careers', payload)
      showAlert('¡Nueva carrera registrada correctamente!', 'success')
    }

    closeModal()
    loadCareers({ silent: true })
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al guardar la carrera.', 'danger')
  } finally {
    isSubmitting.value = false
  }
}

async function toggleCareerStatus(career) {
  const isDeactivating = career.isActive
  const actionText = isDeactivating ? 'dar de baja' : 'reactivar'
  const titleText = isDeactivating ? 'Baja de Carrera' : 'Reactivación de Carrera'

  const confirmed = await confirm({
    title: titleText,
    message: `¿Está seguro de que desea ${actionText} la carrera "${career.name}" (${career.code})?`,
    okText: isDeactivating ? 'Sí, Dar de Baja' : 'Sí, Reactivar',
    cancelText: 'Cancelar',
    isDanger: isDeactivating
  })

  if (!confirmed) return

  try {
    await apiClient.patch(`/v1/careers/${career.id}/toggle-status`)
    showAlert(`Carrera ${isDeactivating ? 'dada de baja' : 'reactivada'} correctamente.`, 'success')
    loadCareers({ silent: true })
  } catch (err) {
    showAlert(err.response?.data?.message || `Error al ${actionText} la carrera.`, 'danger')
  }
}

onMounted(() => {
  syncDepartmentsCatalog()
  loadCareers()
})
</script>

<template>
  <div>
    <!-- Notificación Alert -->
    <div
      v-if="alertMessage"
      class="tecnm-alert"
      :class="`tecnm-alert-${alertType}`"
      role="alert"
    >
      <span>{{ alertMessage }}</span>
      <button type="button" class="tecnm-alert-close" aria-label="Cerrar" @click="alertMessage = ''">&times;</button>
    </div>

    <!-- Encabezado de Página -->
    <div class="tecnm-actions-bar">
      <div>
        <h1 class="tecnm-page-title">Gestión de Carreras / Oferta Educativa</h1>
        <p class="tecnm-page-subtitle">Administración exclusiva del catálogo institucional de programas académicos</p>
      </div>
      <button
        v-if="authStore.isAdmin"
        id="createCareerBtn"
        type="button"
        class="tecnm-btn tecnm-btn-primary"
        @click="openCreateModal"
      >
        + Nueva Carrera
      </button>
    </div>

    <!-- Métricas KPI Rápidas -->
    <div class="tecnm-kpi-grid" style="margin-bottom: 1.5rem;">
      <TecnmKpiCard
        title="Total de Carreras"
        :value="totalCareersCount"
        variant="primary"
        subtext="Registradas en catálogo"
      />
      <TecnmKpiCard
        title="Carreras Activas"
        :value="activeCareersCount"
        variant="success"
        subtext="Vigentes en oferta educativa"
      />
      <TecnmKpiCard
        title="Carreras Inactivas"
        :value="inactiveCareersCount"
        variant="warning"
        subtext="Dadas de baja / En pausa"
      />
    </div>

    <!-- Tarjeta Principal -->
    <div class="tecnm-card">
      <div class="tecnm-card-header">
        <h3 class="tecnm-card-title">Catálogo Oficial de Carreras</h3>
      </div>

      <!-- Barra de Búsqueda y Filtros -->
      <div class="tecnm-card-toolbar" style="display: flex; gap: 1rem; flex-wrap: wrap; align-items: center; justify-content: space-between;">
        <div style="display: flex; gap: 0.75rem; flex-wrap: wrap; align-items: center; flex: 1;">
          <div class="tecnm-form-group tecnm-mb-0 tecnm-search-box" style="margin-bottom: 0; min-width: 280px; flex: 1;">
            <input
              id="careerSearchInput"
              v-model="searchTerm"
              type="text"
              class="tecnm-form-control"
              placeholder="Buscar por código, nombre o acrónimo..."
              @keyup.enter="handleSearch"
            />
          </div>

          <div style="min-width: 160px;">
            <select v-model="statusFilter" class="tecnm-form-control" @change="handleSearch">
              <option value="all">Todas las carreras</option>
              <option value="active">Solo activas</option>
              <option value="inactive">Solo inactivas</option>
            </select>
          </div>

          <button type="button" class="tecnm-btn tecnm-btn-secondary" @click="handleSearch">
            Buscar
          </button>
        </div>
      </div>

      <div class="tecnm-card-body">
        <div class="tecnm-table-responsive">
          <table class="tecnm-table">
            <thead>
              <tr>
                <th style="width: 70px;">ID</th>
                <th style="width: 100px;">Código</th>
                <th>Nombre de la Carrera</th>
                <th style="width: 120px;">Acrónimo</th>
                <th>Departamento Asignado</th>
                <th style="width: 120px; text-align: center;">Estado</th>
                <th style="width: 160px; text-align: right;">Acciones</th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="isLoading">
                <td colspan="7" class="tecnm-table-empty">Cargando carreras académicas...</td>
              </tr>
              <tr v-else-if="careers.length === 0">
                <td colspan="7" class="tecnm-table-empty">No se encontraron carreras con los criterios especificados.</td>
              </tr>
              <tr v-for="c in careers" v-else :key="c.id">
                <td><strong>#{{ c.id }}</strong></td>
                <td><span class="tecnm-badge tecnm-badge-primary" style="font-family: monospace;">{{ c.code }}</span></td>
                <td><strong>{{ c.name }}</strong></td>
                <td>{{ c.acronym }}</td>
                <td>{{ getDepartmentName(c.departmentId) }}</td>
                <td style="text-align: center;">
                  <span class="tecnm-badge" :class="c.isActive ? 'tecnm-badge-approved' : 'tecnm-badge-rejected'">
                    {{ c.isActive ? 'Activa' : 'Inactiva' }}
                  </span>
                </td>
                <td style="text-align: right;">
                  <div class="tecnm-d-flex tecnm-gap-1 tecnm-justify-end" style="display: flex; justify-content: flex-end; gap: 0.35rem;">
                    <button
                      type="button"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                      title="Editar Carrera"
                      @click="openEditModal(c)"
                    >
                      Editar
                    </button>
                    <button
                      type="button"
                      class="tecnm-btn tecnm-btn-sm"
                      :class="c.isActive ? 'tecnm-btn-danger' : 'tecnm-btn-primary'"
                      :title="c.isActive ? 'Dar de Baja' : 'Reactivar'"
                      @click="toggleCareerStatus(c)"
                    >
                      {{ c.isActive ? 'Dar de baja' : 'Reactivar' }}
                    </button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Paginador -->
        <div v-if="totalPages > 1" style="margin-top: 1rem;">
          <TecnmPagination
            :current-page="pageNumber"
            :total-pages="totalPages"
            :total-count="totalCount"
            :page-size="pageSize"
            @page-change="handlePageChange"
          />
        </div>
      </div>
    </div>

    <!-- Modal Registrar/Editar Carrera -->
    <div class="modal-backdrop" :class="{ active: isModalOpen }" role="dialog" aria-modal="true">
      <div class="modal-card" style="max-width: 520px;">
        <div class="tecnm-modal-header">
          <h3 class="tecnm-modal-title">{{ isEditing ? 'Editar Carrera Académica' : 'Alta de Nueva Carrera' }}</h3>
          <button type="button" class="tecnm-modal-close" aria-label="Cerrar" @click="closeModal">&times;</button>
        </div>

        <form @submit.prevent="handleSubmit">
          <div class="tecnm-modal-body" style="display: flex; flex-direction: column; gap: 1rem;">
            <div class="tecnm-form-group">
              <label for="careerCodeInput" class="tecnm-label">Código de la Carrera *</label>
              <input
                id="careerCodeInput"
                v-model="form.code"
                type="text"
                class="tecnm-form-control"
                placeholder="Ej. IER, INF, IND, MEC..."
                required
                maxlength="50"
              />
            </div>

            <div class="tecnm-form-group">
              <label for="careerNameInput" class="tecnm-label">Nombre de la Carrera *</label>
              <input
                id="careerNameInput"
                v-model="form.name"
                type="text"
                class="tecnm-form-control"
                placeholder="Ej. Ingeniería en Energías Renovables"
                required
                maxlength="200"
              />
            </div>

            <div class="tecnm-form-group">
              <label for="careerAcronymInput" class="tecnm-label">Acrónimo / Siglas</label>
              <input
                id="careerAcronymInput"
                v-model="form.acronym"
                type="text"
                class="tecnm-form-control"
                placeholder="Ej. IER"
                maxlength="20"
              />
            </div>

            <div class="tecnm-form-group">
              <label for="careerDeptSelect" class="tecnm-label">Departamento Académico Asociado</label>
              <select id="careerDeptSelect" v-model="form.departmentId" class="tecnm-form-control">
                <option v-for="d in DEPARTMENTS" :key="d.id" :value="d.id">
                  {{ d.name }}
                </option>
              </select>
            </div>
          </div>

          <div class="tecnm-modal-footer" style="margin-top: 1.5rem; display: flex; justify-content: flex-end; gap: 0.5rem;">
            <button type="button" class="tecnm-btn tecnm-btn-secondary" @click="closeModal">Cancelar</button>
            <button type="submit" class="tecnm-btn tecnm-btn-primary" :disabled="isSubmitting">
              {{ isSubmitting ? 'Guardando...' : (isEditing ? 'Guardar Cambios' : 'Registrar Carrera') }}
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<style scoped>
.modal-backdrop {
  position: fixed;
  top: 0;
  left: 0;
  width: 100vw;
  height: 100vh;
  background: rgba(15, 23, 42, 0.5);
  backdrop-filter: blur(3px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 999;
  opacity: 0;
  pointer-events: none;
  transition: opacity 0.2s ease;
}
.modal-backdrop.active {
  opacity: 1;
  pointer-events: auto;
}
.modal-card {
  background: #ffffff;
  border-radius: 0.75rem;
  box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04);
  width: 90%;
  padding: 1.5rem;
}
</style>

<script setup>
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useConfirm } from '@/composables/useConfirm'
import { useAudit } from '@/composables/useAudit'
import { useGlobalSearch } from '@/composables/useGlobalSearch'
import apiClient from '@/services/api'
import TecnmPagination from '@/components/common/TecnmPagination.vue'
import TecnmBadge from '@/components/common/TecnmBadge.vue'

const router = useRouter()
const authStore = useAuthStore()
const { confirm } = useConfirm()
const { showAudit } = useAudit()
const { open: openSearch } = useGlobalSearch()

// Estado
const students = ref([])
const includeInactive = ref(false)
const sortBy = ref('ControlNumber')
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
const editingStudentId = ref(null)
const isSubmitting = ref(false)
const formError = ref('')

const form = ref({
  controlNumber: '',
  firstName: '',
  lastName: '',
  email: '',
  careerId: 1,
  gpa: '',
})

async function loadStudents() {
  isLoading.value = true
  try {
    const params = {
      pageNumber: pageNumber.value,
      pageSize: pageSize.value,
      sortBy: sortBy.value,
      sortDir: sortDir.value,
      search: searchTerm.value.trim() || undefined,
      includeInactive: includeInactive.value,
    }
    const res = await apiClient.get('/v1/students', { params })
    const data = res.data
    students.value = Array.isArray(data) ? data : (data.items || [])
    totalCount.value = data.totalCount || students.value.length
    totalPages.value = data.totalPages || Math.ceil(totalCount.value / pageSize.value) || 1
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al cargar estudiantes.', 'danger')
    students.value = []
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
  loadStudents()
}

function getSortClass(col) {
  if (sortBy.value !== col) return 'tecnm-sort-th'
  return sortDir.value === 'asc' ? 'tecnm-sort-th tecnm-sort-asc' : 'tecnm-sort-th tecnm-sort-desc'
}

function openCreateModal() {
  isEditMode.value = false
  editingStudentId.value = null
  form.value = {
    controlNumber: '',
    firstName: '',
    lastName: '',
    email: '',
    careerId: 1,
    gpa: '',
  }
  formError.value = ''
  isModalOpen.value = true
}

async function openEditModal(student) {
  try {
    const res = await apiClient.get(`/v1/students/${student.id}`)
    const s = res.data
    isEditMode.value = true
    editingStudentId.value = s.id
    form.value = {
      controlNumber: s.controlNumber || '',
      firstName: s.firstName || '',
      lastName: s.lastName || '',
      email: s.email || '',
      careerId: s.careerId || 1,
      gpa: s.gpa != null ? s.gpa : '',
    }
    formError.value = ''
    isModalOpen.value = true
  } catch {
    showAlert('Error al cargar datos del estudiante.', 'danger')
  }
}

function validateEmailDomain(email) {
  const allowed = ['@monclova.tecnm.mx', '@tecnm.mx', '@cenidet.tecnm.mx']
  const lower = email.toLowerCase().trim()
  return allowed.some((d) => lower.endsWith(d))
}

async function handleSubmit() {
  formError.value = ''

  if (!form.value.controlNumber.trim()) {
    formError.value = 'El número de control es obligatorio.'
    return
  }
  if (!form.value.firstName.trim() || !form.value.lastName.trim()) {
    formError.value = 'El nombre y los apellidos son obligatorios.'
    return
  }
  if (!form.value.email.trim()) {
    formError.value = 'El correo institucional es obligatorio.'
    return
  }
  if (!validateEmailDomain(form.value.email)) {
    formError.value = 'El correo debe pertenecer al dominio institucional (@monclova.tecnm.mx).'
    return
  }

  isSubmitting.value = true

  try {
    if (isEditMode.value) {
      await apiClient.put(`/v1/students/${editingStudentId.value}`, {
        firstName: form.value.firstName.trim(),
        lastName: form.value.lastName.trim(),
        careerId: Number(form.value.careerId),
        gpa: form.value.gpa !== '' ? Number(form.value.gpa) : undefined,
      })
      showAlert('Estudiante actualizado exitosamente.', 'success')
    } else {
      await apiClient.post('/v1/students', {
        controlNumber: form.value.controlNumber.trim(),
        firstName: form.value.firstName.trim(),
        lastName: form.value.lastName.trim(),
        email: form.value.email.trim().toLowerCase(),
        careerId: Number(form.value.careerId),
        gpa: form.value.gpa !== '' ? Number(form.value.gpa) : undefined,
      })
      showAlert('Estudiante registrado exitosamente.', 'success')
    }
    isModalOpen.value = false
    loadStudents()
  } catch (err) {
    formError.value =
      err.response?.data?.message ||
      'Error al procesar la solicitud. Verifique los datos ingresados.'
  } finally {
    isSubmitting.value = false
  }
}

async function handleDeactivate(student) {
  const confirmed = await confirm({
    title: 'Desactivar Estudiante',
    message: `¿Está seguro de desactivar al estudiante ${student.fullName || student.firstName}? El expediente pasará a estado inactivo.`,
    okText: 'Desactivar',
    cancelText: 'Cancelar',
  })
  if (!confirmed) return

  try {
    await apiClient.delete(`/v1/students/${student.id}`)
    showAlert('Estudiante desactivado correctamente.', 'success')
    loadStudents()
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al desactivar estudiante.', 'danger')
  }
}

async function handleReactivate(student) {
  try {
    await apiClient.patch(`/v1/students/${student.id}/activate`)
    showAlert('Estudiante reactivado correctamente.', 'success')
    loadStudents()
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al reactivar estudiante.', 'danger')
  }
}

function handleAudit(student) {
  showAudit({
    title: `Auditoría — Estudiante ${student.controlNumber}`,
    item: student,
  })
}

function viewProfile(student) {
  router.push(`/students/profile?id=${student.id}`)
}

async function handleExportPdf() {
  try {
    const params = {
      search: searchTerm.value.trim() || undefined,
      sortBy: sortBy.value,
      sortDir: sortDir.value,
      includeInactive: includeInactive.value,
    }
    const res = await apiClient.get('/v1/students/export', {
      params,
      responseType: 'blob',
    })
    const blob = new Blob([res.data], { type: 'application/pdf' })
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = 'estudiantes_tecnm.pdf'
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    window.URL.revokeObjectURL(url)
  } catch {
    showAlert('Error al exportar el reporte PDF.', 'danger')
  }
}

onMounted(() => {
  loadStudents()
})
</script>

<template>
  <div>
    <!-- Barra Superior de Acciones -->
    <div class="tecnm-actions-bar">
      <div>
        <h1 class="tecnm-page-title">Gestión de Estudiantes Residentes</h1>
        <p class="tecnm-page-subtitle">Catálogo de estudiantes y consulta de expedientes</p>
      </div>
      <div class="tecnm-page-actions">
        <button
          type="button"
          class="tecnm-btn tecnm-btn-secondary"
          @click="openSearch({ initialSource: 'STUDENTS' })"
        >
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
          </svg>
          <span>Abrir búsqueda</span>
        </button>
        <span class="tecnm-page-actions-divider" aria-hidden="true"></span>
        <button
          v-if="!authStore.isReadOnly"
          id="openCreateModalBtn"
          type="button"
          class="tecnm-btn tecnm-btn-primary"
          @click="openCreateModal"
        >
          Registrar Nuevo Estudiante
        </button>
      </div>
    </div>

    <!-- Alert de Notificación -->
    <div
      v-if="alertMessage"
      id="alertContainer"
      class="tecnm-alert"
      :class="`tecnm-alert-${alertType}`"
      role="alert"
    >
      <span>{{ alertMessage }}</span>
    </div>

    <!-- Tarjeta Principal de Tabla -->
    <div class="tecnm-card">
      <div class="tecnm-card-header">
        <h3 class="tecnm-card-title">Directorio de Estudiantes</h3>
      </div>

      <div class="tecnm-card-toolbar">
        <div class="tecnm-toolbar-actions">
          <label class="tecnm-switch-label">
            <span class="tecnm-switch">
              <input
                id="studentsIncludeInactiveToggle"
                v-model="includeInactive"
                type="checkbox"
                @change="loadStudents"
              />
              <span class="tecnm-switch-slider"></span>
            </span>
            Mostrar inactivos
          </label>
          <button
            id="exportStudentsBtn"
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
          <table id="studentsTable" class="tecnm-table">
            <thead>
              <tr>
                <th
                  data-sort="ControlNumber"
                  :class="getSortClass('ControlNumber')"
                  @click="handleSort('ControlNumber')"
                >
                  No. Control
                </th>
                <th
                  data-sort="FullName"
                  :class="getSortClass('FullName')"
                  @click="handleSort('FullName')"
                >
                  Nombre Completo
                </th>
                <th>Carrera</th>
                <th>Correo Institucional</th>
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
            <tbody id="studentsTableBody">
              <tr v-if="isLoading">
                <td colspan="6" class="tecnm-table-empty">
                  Cargando catálogo de estudiantes...
                </td>
              </tr>
              <tr v-else-if="students.length === 0">
                <td colspan="6" class="tecnm-table-empty">
                  No se encontraron estudiantes registrados.
                </td>
              </tr>
              <tr
                v-for="s in students"
                v-else
                :key="s.id"
              >
                <td><strong>{{ s.controlNumber }}</strong></td>
                <td>{{ s.fullName || `${s.firstName} ${s.lastName}` }}</td>
                <td>{{ s.career || (s.careerId === 1 ? 'Informática' : s.careerId === 2 ? 'Industrial' : s.careerId === 3 ? 'Mecatrónica' : 'ISC') }}</td>
                <td>{{ s.email }}</td>
                <td>
                  <TecnmBadge :status="s.isActive ? 'Activo' : 'Inactivo'" />
                </td>
                <td class="tecnm-row-actions">
                  <button
                    type="button"
                    class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                    @click="viewProfile(s)"
                  >
                    Expediente
                  </button>
                  <button
                    v-if="!authStore.isReadOnly"
                    type="button"
                    class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                    @click="openEditModal(s)"
                  >
                    Editar
                  </button>
                  <button
                    v-if="authStore.canSeeAudit"
                    type="button"
                    class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                    @click="handleAudit(s)"
                  >
                    Auditoría
                  </button>
                  <button
                    v-if="s.isActive && !authStore.isReadOnly"
                    type="button"
                    class="tecnm-btn tecnm-btn-danger tecnm-btn-sm"
                    @click="handleDeactivate(s)"
                  >
                    Desactivar
                  </button>
                  <button
                    v-else-if="!authStore.isReadOnly"
                    type="button"
                    class="tecnm-btn tecnm-btn-success tecnm-btn-sm"
                    @click="handleReactivate(s)"
                  >
                    Reactivar
                  </button>
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
          @page-change="loadStudents"
        />
      </div>
    </div>

    <!-- Modal Registrar / Editar Estudiante -->
    <div
      v-if="isModalOpen"
      id="studentModal"
      class="modal-backdrop active"
      role="dialog"
      aria-modal="true"
      @click.self="isModalOpen = false"
    >
      <div class="modal-card">
        <div class="tecnm-modal-header">
          <h3 id="modalTitle" class="tecnm-modal-title">
            {{ isEditMode ? 'Editar Estudiante' : 'Registrar Nuevo Estudiante' }}
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

        <form id="studentForm" @submit.prevent="handleSubmit">
          <div
            v-if="formError"
            id="studentFormAlert"
            class="tecnm-alert tecnm-alert-danger"
            style="margin-bottom: 1rem;"
            role="alert"
          >
            <span>{{ formError }}</span>
          </div>

          <div class="tecnm-form-grid">
            <div class="tecnm-form-group">
              <label for="controlNumber" class="tecnm-label">Número de Control</label>
              <input
                id="controlNumber"
                v-model="form.controlNumber"
                type="text"
                class="tecnm-form-control"
                placeholder="20680123"
                :disabled="isEditMode || isSubmitting"
                required
              />
            </div>

            <div class="tecnm-form-group">
              <label for="firstName" class="tecnm-label">Nombre(s)</label>
              <input
                id="firstName"
                v-model="form.firstName"
                type="text"
                class="tecnm-form-control"
                placeholder="Juan"
                :disabled="isSubmitting"
                required
              />
            </div>

            <div class="tecnm-form-group">
              <label for="lastName" class="tecnm-label">Apellido Paterno / Materno</label>
              <input
                id="lastName"
                v-model="form.lastName"
                type="text"
                class="tecnm-form-control"
                placeholder="Pérez López"
                :disabled="isSubmitting"
                required
              />
            </div>

            <div class="tecnm-form-group">
              <label for="email" class="tecnm-label">Correo institucional</label>
              <input
                id="email"
                v-model="form.email"
                type="email"
                class="tecnm-form-control"
                placeholder="20680123@monclova.tecnm.mx"
                :disabled="isEditMode || isSubmitting"
                required
              />
            </div>

            <div class="tecnm-form-group">
              <label for="careerId" class="tecnm-label">Carrera</label>
              <select
                id="careerId"
                v-model="form.careerId"
                class="tecnm-form-control"
                :disabled="isSubmitting"
                required
              >
                <option :value="1">Ingeniería Informática</option>
                <option :value="2">Ingeniería Industrial</option>
                <option :value="3">Ingeniería Mecatrónica</option>
                <option :value="4">Ingeniería en Sistemas Computacionales</option>
              </select>
            </div>

            <div class="tecnm-form-group">
              <label for="gpa" class="tecnm-label">Promedio General</label>
              <input
                id="gpa"
                v-model="form.gpa"
                type="number"
                step="0.1"
                min="0"
                max="100"
                class="tecnm-form-control"
                placeholder="92.5"
                :disabled="isSubmitting"
                required
              />
            </div>
          </div>

          <div class="tecnm-modal-footer">
            <button
              id="closeModalBtn"
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
              <span v-if="!isSubmitting">Guardar Estudiante</span>
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

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

// Catálogos
const ACADEMIC_PERIODS = [
  { id: 1, name: 'Ene-Jun 2026' },
  { id: 2, name: 'Ago-Dic 2026' },
  { id: 3, name: 'Ene-Jun 2027' },
  { id: 4, name: 'Ago-Dic 2027' },
]

const GENDER_OPTIONS = [
  { value: 'Masculino', label: 'Masculino' },
  { value: 'Femenino', label: 'Femenino' },
  { value: 'Otro', label: 'Otro / No especificado' },
]

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
  lastName2: '',
  curp: '',
  gender: 'Masculino',
  email: '',
  careerId: 1,
  academicPeriodId: 1,
  gpa: '',
})

const canImport = computed(() => {
  return (
    authStore.isAdmin ||
    authStore.hasPermission('students.import.excel') ||
    authStore.hasRole('admin', 'vinculacion')
  )
})

// Modal Importar Excel Estudiantes
const isImportModalOpen = ref(false)
const importFile = ref(null)
const isImporting = ref(false)
const importError = ref('')
const importResult = ref(null)

function openImportModal() {
  importFile.value = null
  isImporting.value = false
  importError.value = ''
  importResult.value = null
  isImportModalOpen.value = true
}

function handleFileChange(event) {
  const files = event.target.files
  if (files && files.length > 0) {
    importFile.value = files[0]
  } else {
    importFile.value = null
  }
}

async function handleImportSubmit() {
  importError.value = ''
  importResult.value = null

  if (!importFile.value) {
    importError.value = 'Seleccione un archivo Excel (.xlsx o .xls).'
    return
  }

  isImporting.value = true
  const formData = new FormData()
  formData.append('file', importFile.value)

  try {
    const res = await apiClient.post('/v1/students/import-excel', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    importResult.value = res.data
    showAlert(`Importación finalizada. ${res.data.successCount} estudiantes creados.`, 'success')
    loadStudents()
  } catch (err) {
    importError.value =
      err.response?.data?.message ||
      'Error al procesar el archivo Excel. Verifique que cumpla con el formato y columnas requeridas.'
  } finally {
    isImporting.value = false
  }
}

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
    lastName2: '',
    curp: '',
    gender: 'Masculino',
    email: '',
    careerId: 1,
    academicPeriodId: 1,
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
      lastName2: s.lastName2 || '',
      curp: s.curp || '',
      gender: s.gender || 'Masculino',
      email: s.email || '',
      careerId: s.careerId || 1,
      academicPeriodId: s.academicPeriodId || 1,
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
    formError.value = 'El nombre y el apellido paterno son obligatorios.'
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
        lastName2: form.value.lastName2.trim() || undefined,
        curp: form.value.curp.trim().toUpperCase() || undefined,
        gender: form.value.gender || undefined,
        careerId: Number(form.value.careerId),
        academicPeriodId: form.value.academicPeriodId ? Number(form.value.academicPeriodId) : undefined,
        gpa: form.value.gpa !== '' ? Number(form.value.gpa) : undefined,
      })
      showAlert('Estudiante actualizado exitosamente.', 'success')
    } else {
      await apiClient.post('/v1/students', {
        controlNumber: form.value.controlNumber.trim().toUpperCase(),
        firstName: form.value.firstName.trim(),
        lastName: form.value.lastName.trim(),
        lastName2: form.value.lastName2.trim() || undefined,
        curp: form.value.curp.trim().toUpperCase() || undefined,
        gender: form.value.gender || undefined,
        email: form.value.email.trim().toLowerCase(),
        careerId: Number(form.value.careerId),
        academicPeriodId: form.value.academicPeriodId ? Number(form.value.academicPeriodId) : undefined,
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

const isSendingMassLetters = ref(false)
const isSendingIndividualLetterId = ref(null)

async function handleMassSendPresentationLetters() {
  const confirmed = await confirm({
    title: 'Enviar Cartas de Presentación',
    message: 'Se generará y enviará por correo electrónico la Carta de Presentación Oficial en formato PDF únicamente a los alumnos que aún no la hayan recibido. ¿Desea proceder?',
    okText: 'Enviar Cartas',
    cancelText: 'Cancelar',
  })
  if (!confirmed) return

  isSendingMassLetters.value = true
  try {
    const res = await apiClient.post('/v1/students/presentation-letters/mass-send')
    const count = res.data.sentCount || 0
    showAlert(res.data.message || `Se encoló el envío para ${count} carta(s) de presentación.`, 'success')
    loadStudents()
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al procesar el envío de cartas.', 'danger')
  } finally {
    isSendingMassLetters.value = false
  }
}

async function handleSendIndividualPresentationLetter(student) {
  const confirmed = await confirm({
    title: 'Enviar Carta de Presentación',
    message: `¿Desea enviar la Carta de Presentación en PDF al correo (${student.email}) del alumno ${student.fullName || student.firstName}?`,
    okText: 'Enviar',
    cancelText: 'Cancelar',
  })
  if (!confirmed) return

  isSendingIndividualLetterId.value = student.id
  try {
    await apiClient.post(`/v1/students/${student.id}/presentation-letter/send`)
    showAlert(`Carta de presentación encolada para ${student.email} exitosamente.`, 'success')
    loadStudents()
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al enviar la carta de presentación.', 'danger')
  } finally {
    isSendingIndividualLetterId.value = null
  }
}

async function handleDownloadPresentationLetterPdf(student) {
  try {
    const res = await apiClient.get(`/v1/students/${student.id}/presentation-letter/pdf`, {
      responseType: 'blob'
    })
    const blob = new Blob([res.data], { type: 'application/pdf' })
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `Carta_Presentacion_${student.controlNumber}.pdf`
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    window.URL.revokeObjectURL(url)
  } catch {
    showAlert('Error al descargar el PDF de la Carta de Presentación.', 'danger')
  }
}

async function handleDownloadStudentTemplate() {
  try {
    const res = await apiClient.get('/v1/students/import/template', { responseType: 'blob' })
    const blob = new Blob([res.data], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = 'Plantilla_Alumnos.xlsx'
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    window.URL.revokeObjectURL(url)
  } catch {
    showAlert('Error al descargar la plantilla de alumnos.', 'danger')
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
        <button
          v-if="canImport"
          id="openImportStudentModalBtn"
          type="button"
          class="tecnm-btn tecnm-btn-secondary"
          @click="openImportModal"
        >
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" style="margin-right: 0.35rem; display: inline-block; vertical-align: middle;">
            <path stroke-linecap="round" stroke-linejoin="round" d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5m-13.5-9L12 3m0 0l4.5 4.5M12 3v13.5" />
          </svg>
          <span>Importar Excel</span>
        </button>
        <button
          v-if="authStore.isAdmin || authStore.hasRole('vinculacion')"
          id="massSendPresentationLettersBtn"
          type="button"
          class="tecnm-btn tecnm-btn-secondary"
          :disabled="isSendingMassLetters"
          @click="handleMassSendPresentationLetters"
        >
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" style="margin-right: 0.35rem; display: inline-block; vertical-align: middle;">
            <path stroke-linecap="round" stroke-linejoin="round" d="M21.75 6.75v10.5a2.25 2.25 0 01-2.25 2.25h-15a2.25 2.25 0 01-2.25-2.25V6.75m19.5 0A2.25 2.25 0 0019.5 4.5h-15a2.25 2.25 0 00-2.25 2.25m19.5 0v.243a2.25 2.25 0 01-1.07 1.916l-7.5 4.615a2.25 2.25 0 01-2.36 0L3.32 8.91a2.25 2.25 0 01-1.07-1.916V6.75" />
          </svg>
          <span>{{ isSendingMassLetters ? 'Enviando...' : 'Enviar Cartas de Presentación (Nuevos)' }}</span>
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
          <table class="tecnm-table tecnm-table-striped">
            <thead>
              <tr>
                <th
                  class="tecnm-th-sortable"
                  @click="handleSort('ControlNumber')"
                >
                  N° Control
                  <span v-if="sortBy === 'ControlNumber'">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
                </th>
                <th
                  class="tecnm-th-sortable"
                  @click="handleSort('FullName')"
                >
                  Nombre Completo
                  <span v-if="sortBy === 'FullName'">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
                </th>
                <th
                  class="tecnm-th-sortable"
                  @click="handleSort('CareerId')"
                >
                  Programa Educativo
                  <span v-if="sortBy === 'CareerId'">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
                </th>
                <th
                  class="tecnm-th-sortable"
                  @click="handleSort('Email')"
                >
                  Correo Institucional
                  <span v-if="sortBy === 'Email'">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
                </th>
                <th
                  class="tecnm-th-sortable"
                  @click="handleSort('IsPresentationLetterSent')"
                >
                  Carta Presentación
                  <span v-if="sortBy === 'IsPresentationLetterSent'">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
                </th>
                <th
                  class="tecnm-th-sortable"
                  @click="handleSort('IsActive')"
                >
                  Estatus
                  <span v-if="sortBy === 'IsActive'">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
                </th>
                <th class="tecnm-th-actions">Acciones</th>
              </tr>
            </thead>
            <tbody id="studentsTableBody">
              <tr v-if="isLoading">
                <td colspan="7" class="tecnm-table-empty">
                  Cargando catálogo de estudiantes...
                </td>
              </tr>
              <tr v-else-if="students.length === 0">
                <td colspan="7" class="tecnm-table-empty">
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
                  <TecnmBadge :status="s.isPresentationLetterSent ? 'Aprobado' : 'Pendiente'" />
                </td>
                <td>
                  <TecnmBadge :status="s.isActive ? 'Activo' : 'Inactivo'" />
                </td>
                <td class="tecnm-row-actions">
                  <button
                    v-if="authStore.isAdmin || authStore.hasRole('vinculacion')"
                    type="button"
                    class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                    :disabled="isSendingIndividualLetterId === s.id"
                    :title="s.isPresentationLetterSent ? 'Reenviar Carta de Presentación por Correo' : 'Enviar Carta de Presentación por Correo'"
                    @click="handleSendIndividualPresentationLetter(s)"
                  >
                    {{ isSendingIndividualLetterId === s.id ? 'Enviando...' : (s.isPresentationLetterSent ? 'Reenviar' : 'Enviar Carta') }}
                  </button>
                  <button
                    v-if="authStore.isAdmin || authStore.hasRole('vinculacion') || authStore.hasRole('departmenthead') || authStore.hasRole('director')"
                    type="button"
                    class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                    title="Descargar PDF de la Carta de Presentación"
                    @click="handleDownloadPresentationLetterPdf(s)"
                  >
                    PDF
                  </button>
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
              <label for="controlNumber" class="tecnm-label">Número de Control *</label>
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
              <label for="curp" class="tecnm-label">CURP</label>
              <input
                id="curp"
                v-model="form.curp"
                type="text"
                maxlength="18"
                class="tecnm-form-control"
                placeholder="ABCD010203HDFRLL09"
                style="text-transform: uppercase;"
                :disabled="isSubmitting"
              />
            </div>

            <div class="tecnm-form-group">
              <label for="firstName" class="tecnm-label">Nombre(s) *</label>
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
              <label for="lastName" class="tecnm-label">Apellido Paterno *</label>
              <input
                id="lastName"
                v-model="form.lastName"
                type="text"
                class="tecnm-form-control"
                placeholder="Pérez"
                :disabled="isSubmitting"
                required
              />
            </div>

            <div class="tecnm-form-group">
              <label for="lastName2" class="tecnm-label">Apellido Materno</label>
              <input
                id="lastName2"
                v-model="form.lastName2"
                type="text"
                class="tecnm-form-control"
                placeholder="Gómez"
                :disabled="isSubmitting"
              />
            </div>

            <div class="tecnm-form-group">
              <label for="gender" class="tecnm-label">Género</label>
              <select
                id="gender"
                v-model="form.gender"
                class="tecnm-form-control"
                :disabled="isSubmitting"
              >
                <option
                  v-for="g in GENDER_OPTIONS"
                  :key="g.value"
                  :value="g.value"
                >
                  {{ g.label }}
                </option>
              </select>
            </div>

            <div class="tecnm-form-group">
              <label for="email" class="tecnm-label">Correo institucional *</label>
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
              <label for="careerId" class="tecnm-label">Carrera *</label>
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
              <label for="academicPeriodId" class="tecnm-label">Periodo Académico</label>
              <select
                id="academicPeriodId"
                v-model="form.academicPeriodId"
                class="tecnm-form-control"
                :disabled="isSubmitting"
              >
                <option
                  v-for="p in ACADEMIC_PERIODS"
                  :key="p.id"
                  :value="p.id"
                >
                  {{ p.name }}
                </option>
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

    <!-- Modal Importar Excel Estudiantes -->
    <div
      v-if="isImportModalOpen"
      id="importStudentModal"
      class="modal-backdrop active"
      role="dialog"
      aria-modal="true"
      @click.self="isImportModalOpen = false"
    >
      <div class="modal-card" style="max-width: 600px;">
        <div class="tecnm-modal-header">
          <h3 class="tecnm-modal-title">📊 Carga Masiva de Estudiantes vía Excel</h3>
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
          <div class="tecnm-alert tecnm-alert-warning" style="margin-bottom: 1rem;">
            <div style="display: flex; justify-content: space-between; align-items: flex-start; gap: 0.75rem; flex-wrap: wrap;">
              <div>
                <strong>Requisito Estricto de Columnas:</strong><br />
                El archivo Excel debe contener las siguientes columnas en la primera fila:<br />
                <code>Matricula, Apellidos, Nombre, Sexo, Carrera, Semestre, Email</code>
              </div>
              <button
                type="button"
                class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                style="margin-top: 0.25rem;"
                @click="handleDownloadStudentTemplate"
              >
                Descargar Plantilla Excel
              </button>
            </div>
          </div>

          <div v-if="importError" class="tecnm-alert tecnm-alert-danger" style="margin-bottom: 1rem;" role="alert">
            <span>{{ importError }}</span>
          </div>

          <div v-if="importResult" class="tecnm-alert tecnm-alert-info" style="margin-bottom: 1rem;">
            <strong>Resumen de Importación:</strong>
            <ul>
              <li>Filas procesadas: {{ importResult.totalRows }}</li>
              <li>Estudiantes registrados: {{ importResult.successCount }}</li>
              <li>Omitidos (Duplicados): {{ importResult.skippedCount }}</li>
              <li>Errores de fila: {{ importResult.errorCount }}</li>
            </ul>
            <div v-if="importResult.errors && importResult.errors.length > 0" style="margin-top: 0.5rem; max-height: 120px; overflow-y: auto;">
              <small class="tecnm-text-danger">
                <strong>Detalle de errores:</strong>
                <ul>
                  <li v-for="(e, idx) in importResult.errors" :key="idx">{{ e }}</li>
                </ul>
              </small>
            </div>
            <div v-if="importResult.skipped && importResult.skipped.length > 0" style="margin-top: 0.5rem; max-height: 100px; overflow-y: auto;">
              <small class="tecnm-text-muted">
                <strong>Omitidos:</strong>
                <ul>
                  <li v-for="(s, idx) in importResult.skipped" :key="idx">{{ s }}</li>
                </ul>
              </small>
            </div>
          </div>

          <div class="tecnm-form-group">
            <label for="studentExcelFile" class="tecnm-label">Seleccionar Archivo Excel (.xlsx / .xls) *</label>
            <input
              id="studentExcelFile"
              type="file"
              accept=".xlsx, .xls"
              class="tecnm-form-control"
              :disabled="isImporting"
              required
              @change="handleFileChange"
            />
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
              <span v-if="!isImporting">Subir e Importar</span>
              <span v-else class="login-spinner">Procesando...</span>
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

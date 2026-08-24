<script setup>
import { ref, computed, onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useConfirm } from '@/composables/useConfirm'
import { useGlobalSearch } from '@/composables/useGlobalSearch'
import apiClient from '@/services/api'
import TecnmPagination from '@/components/common/TecnmPagination.vue'

const authStore = useAuthStore()
const { confirm } = useConfirm()
const { open: openGlobalSearch } = useGlobalSearch()

const students = ref([])
const advisors = ref([])
const isLoading = ref(false)
const isSubmitting = ref(false)

const pageNumber = ref(1)
const pageSize = ref(10)
const totalCount = ref(0)
const totalPages = ref(0)

const searchTerm = ref('')
const sortBy = ref('ControlNumber')
const sortDir = ref('asc')
const includeInactive = ref(false)

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

// Modal Asignación Masiva
const isBatchModalOpen = ref(false)
const selectedAdvisorId = ref('')
const selectedStudentIds = ref([])

async function loadAdvisorsOptions() {
  try {
    const res = await apiClient.get('/v1/advisors', { params: { pageSize: 200 } })
    const data = res.data
    advisors.value = Array.isArray(data) ? data : (data.items || [])
  } catch (err) {
    showAlert('Error al cargar la lista de asesores.', 'danger')
  }
}

async function loadStudents({ silent = false } = {}) {
  if (!silent) isLoading.value = true
  try {
    const params = {
      pageNumber: pageNumber.value,
      pageSize: pageSize.value,
      includeInactive: includeInactive.value,
    }
    const res = await apiClient.get('/v1/students', { params })
    const data = res.data
    students.value = Array.isArray(data) ? data : (data.items || [])
    totalCount.value = data.totalCount || students.value.length
    totalPages.value = data.totalPages || Math.ceil(totalCount.value / pageSize.value) || 1
  } catch (err) {
    if (!silent) {
      showAlert(err.response?.data?.message || 'Error al cargar lista de estudiantes.', 'danger')
      students.value = []
    }
  } finally {
    if (!silent) isLoading.value = false
  }
}

const sortedStudents = computed(() => {
  let list = [...students.value]

  if (searchTerm.value.trim()) {
    const term = searchTerm.value.trim().toLowerCase()
    list = list.filter((s) => {
      const control = (s.controlNumber || '').toLowerCase()
      const name = (s.fullName || `${s.firstName} ${s.lastName}`).toLowerCase()
      const career = (s.career || '').toLowerCase()
      const advisor = (s.advisorName || '').toLowerCase()
      return control.includes(term) || name.includes(term) || career.includes(term) || advisor.includes(term)
    })
  }

  const field = sortBy.value
  const dir = sortDir.value === 'asc' ? 1 : -1

  return list.sort((a, b) => {
    let valA = ''
    let valB = ''

    if (field === 'ControlNumber') {
      valA = a.controlNumber || ''
      valB = b.controlNumber || ''
    } else if (field === 'FullName') {
      valA = a.fullName || `${a.firstName} ${a.lastName}`
      valB = b.fullName || `${b.firstName} ${b.lastName}`
    } else if (field === 'Career') {
      valA = a.career || ''
      valB = b.career || ''
    } else if (field === 'AdvisorName') {
      valA = a.advisorName || ''
      valB = b.advisorName || ''
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

function handleSort(col) {
  if (sortBy.value === col) {
    sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
  } else {
    sortBy.value = col
    sortDir.value = 'asc'
  }
}

async function handleIndividualAssign(student, newAdvisorId) {
  if (!newAdvisorId) return
  const advisorObj = advisors.value.find(a => a.id === Number(newAdvisorId))
  const advisorName = advisorObj ? (advisorObj.fullName || advisorObj.name) : 'el asesor seleccionado'

  const confirmed = await confirm({
    title: 'Confirmar Asignación de Asesor',
    message: `¿Desea asignar a ${advisorName} como asesor académico del estudiante ${student.fullName || student.controlNumber}?`,
    okText: 'Asignar Asesor',
    cancelText: 'Cancelar',
  })

  if (!confirmed) {
    loadStudents({ silent: true })
    return
  }

  try {
    await apiClient.put(`/v1/students/${student.id}/advisor`, { advisorId: Number(newAdvisorId) })
    showAlert(`Asesor asignado correctamente a ${student.controlNumber}.`, 'success')
    loadStudents({ silent: true })
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al asignar asesor.', 'danger')
    loadStudents({ silent: true })
  }
}

const allStudentsForBatch = ref([])
const isBatchLoading = ref(false)
const batchPage = ref(1)
const batchPageSize = ref(10)

async function openBatchModal() {
  selectedAdvisorId.value = ''
  selectedStudentIds.value = []
  batchPage.value = 1
  isBatchModalOpen.value = true
  isBatchLoading.value = true
  try {
    const res = await apiClient.get('/v1/students', { params: { pageNumber: 1, pageSize: 500, includeInactive: false } })
    const data = res.data
    allStudentsForBatch.value = Array.isArray(data) ? data : (data.items || [])
  } catch {
    allStudentsForBatch.value = [...students.value]
  } finally {
    isBatchLoading.value = false
  }
}

const batchTotalCount = computed(() => allStudentsForBatch.value.length)
const batchTotalPages = computed(() => Math.ceil(batchTotalCount.value / batchPageSize.value) || 1)

const paginatedBatchStudents = computed(() => {
  const start = (batchPage.value - 1) * batchPageSize.value
  return allStudentsForBatch.value.slice(start, start + batchPageSize.value)
})

function toggleSelectAllBatch(event) {
  const currentIds = paginatedBatchStudents.value.map(s => s.id)
  if (event.target.checked) {
    const combined = new Set([...selectedStudentIds.value, ...currentIds])
    selectedStudentIds.value = Array.from(combined)
  } else {
    selectedStudentIds.value = selectedStudentIds.value.filter(id => !currentIds.includes(id))
  }
}

async function submitBatchAssignment() {
  if (!selectedAdvisorId.value) {
    showAlert('Debe seleccionar un Asesor Académico.', 'warning')
    return
  }
  if (selectedStudentIds.value.length === 0) {
    showAlert('Debe seleccionar al menos un estudiante.', 'warning')
    return
  }

  const advisorObj = advisors.value.find(a => a.id === Number(selectedAdvisorId.value))
  const advisorName = advisorObj ? (advisorObj.fullName || advisorObj.name) : 'el asesor'

  const confirmed = await confirm({
    title: 'Asignación Masiva',
    message: `¿Desea asignar a ${advisorName} como asesor para los ${selectedStudentIds.value.length} estudiantes seleccionados?`,
    okText: 'Confirmar Asignación Masiva',
    cancelText: 'Cancelar',
  })

  if (!confirmed) return

  isSubmitting.value = true
  try {
    await apiClient.post('/v1/students/batch-assign-advisor', {
      advisorId: Number(selectedAdvisorId.value),
      studentIds: selectedStudentIds.value,
    })
    showAlert(`Se han asignado ${selectedStudentIds.value.length} estudiantes al asesor correctamente.`, 'success')
    isBatchModalOpen.value = false
    loadStudents()
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error en la asignación masiva.', 'danger')
  } finally {
    isSubmitting.value = false
  }
}

onMounted(() => {
  loadAdvisorsOptions()
  loadStudents()
})
</script>

<template>
  <div>
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

    <!-- Barra de Acciones -->
    <div class="tecnm-actions-bar">
      <div>
        <h1 class="tecnm-page-title">Asignación de Asesores Académicos</h1>
        <p class="tecnm-page-subtitle">Asignación individual y masiva de asesores académicos a estudiantes residentes</p>
      </div>
      <div class="tecnm-page-actions">
        <button
          type="button"
          class="tecnm-btn tecnm-btn-secondary"
          @click="openGlobalSearch({ initialSource: 'STUDENTS' })"
        >
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
          </svg>
          <span>Abrir búsqueda</span>
        </button>
        <button
          type="button"
          class="tecnm-btn tecnm-btn-primary"
          @click="openBatchModal"
        >
          + Asignación Masiva por Asesor
        </button>
      </div>
    </div>

    <!-- Tarjeta Principal -->
    <div class="tecnm-card">
      <div class="tecnm-card-header">
        <h3 class="tecnm-card-title">Estudiantes y Asesores Asignados</h3>
      </div>
      <div class="tecnm-card-toolbar">
        <div class="tecnm-form-group tecnm-mb-0 tecnm-search-box" style="margin-bottom: 0; min-width: 300px;">
          <input
            id="assignmentSearchInput"
            v-model="searchTerm"
            type="search"
            class="tecnm-form-control"
            placeholder="Buscar por alumno, matrícula, carrera o asesor..."
          />
        </div>

        <div class="tecnm-toolbar-actions">
          <label class="tecnm-switch-label">
            <span class="tecnm-switch">
              <input
                id="assignmentIncludeInactiveToggle"
                v-model="includeInactive"
                type="checkbox"
                @change="loadStudents"
              />
              <span class="tecnm-switch-slider"></span>
            </span>
            Mostrar inactivos
          </label>
        </div>
      </div>

      <div class="tecnm-card-body">
        <div class="tecnm-table-responsive">
          <table class="tecnm-table tecnm-table-striped">
            <thead>
              <tr>
                <th class="tecnm-th-sortable" @click="handleSort('ControlNumber')">
                  N° Control
                  <span class="tecnm-sort-icon" :class="{ active: sortBy === 'ControlNumber' }">
                    {{ sortBy === 'ControlNumber' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th class="tecnm-th-sortable" @click="handleSort('FullName')">
                  Nombre del Estudiante
                  <span class="tecnm-sort-icon" :class="{ active: sortBy === 'FullName' }">
                    {{ sortBy === 'FullName' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th class="tecnm-th-sortable" @click="handleSort('Career')">
                  Programa Educativo
                  <span class="tecnm-sort-icon" :class="{ active: sortBy === 'Career' }">
                    {{ sortBy === 'Career' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th class="tecnm-th-sortable" @click="handleSort('AdvisorName')">
                  Asesor Académico Asignado
                  <span class="tecnm-sort-icon" :class="{ active: sortBy === 'AdvisorName' }">
                    {{ sortBy === 'AdvisorName' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th>Estado Asignación</th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="isLoading">
                <td colspan="5" class="tecnm-table-empty">Cargando directorio de asignaciones...</td>
              </tr>
              <tr v-else-if="sortedStudents.length === 0">
                <td colspan="5" class="tecnm-table-empty">
                  <span v-if="includeInactive">No hay estudiantes inactivos registrados.</span>
                  <span v-else>No se encontraron estudiantes para la asignación.</span>
                </td>
              </tr>
              <tr v-for="s in sortedStudents" v-else :key="s.id">
                <td><strong>{{ s.controlNumber }}</strong></td>
                <td>{{ s.fullName || `${s.firstName} ${s.lastName}` }}</td>
                <td>{{ s.career || 'ISC' }}</td>
                <td>
                  <select
                    :value="s.advisorId || ''"
                    class="tecnm-form-control tecnm-form-control-sm"
                    style="max-width: 280px;"
                    @change="handleIndividualAssign(s, $event.target.value)"
                  >
                    <option value="">-- Sin Asesor Asignado --</option>
                    <option v-for="adv in advisors" :key="adv.id" :value="adv.id">
                      {{ adv.title ? `${adv.title} ` : '' }}{{ adv.fullName || adv.name }}
                    </option>
                  </select>
                </td>
                <td>
                  <span v-if="s.advisorId" class="tecnm-badge tecnm-badge-success">Asignado</span>
                  <span v-else class="tecnm-badge tecnm-badge-warning">Pendiente</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div class="tecnm-card-footer">
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

    <!-- Modal Asignación Masiva por Asesor -->
    <div
      v-if="isBatchModalOpen"
      class="modal-backdrop active"
      role="dialog"
      aria-modal="true"
      @click.self="isBatchModalOpen = false"
    >
      <div class="modal-card" style="max-width: 720px;">
        <div class="tecnm-modal-header">
          <h3 class="tecnm-modal-title">Asignación Masiva por Asesor Académico</h3>
          <button
            type="button"
            class="tecnm-modal-close"
            aria-label="Cerrar"
            @click="isBatchModalOpen = false"
          >
            &times;
          </button>
        </div>
        <div class="tecnm-modal-body" style="padding: 1.25rem;">
          <div class="tecnm-form-group" style="margin-bottom: 1rem;">
            <label class="tecnm-label">Seleccionar Asesor Académico destinatario <span class="tecnm-required">*</span></label>
            <select v-model="selectedAdvisorId" class="tecnm-form-control">
              <option value="">-- Seleccionar Asesor --</option>
              <option v-for="adv in advisors" :key="adv.id" :value="adv.id">
                {{ adv.title ? `${adv.title} ` : '' }}{{ adv.fullName || adv.name }} ({{ adv.departmentName || 'General' }})
              </option>
            </select>
          </div>

          <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.75rem; flex-wrap: wrap; gap: 0.5rem;">
            <label class="tecnm-label" style="margin-bottom: 0;">Selección de estudiantes a asignar:</label>
            <button
              type="button"
              class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
              @click="openGlobalSearch({ initialSource: 'STUDENTS' })"
            >
              <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" style="margin-right: 0.25rem; display: inline-block; vertical-align: middle;">
                <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
              </svg>
              <span>Usar Buscador Global</span>
            </button>
          </div>

          <div class="tecnm-table-responsive" style="max-height: 320px; overflow-y: auto; border: 1px solid var(--tecnm-border-color); border-radius: 6px;">
            <table class="tecnm-table tecnm-table-striped" style="font-size: 0.85rem; margin-bottom: 0;">
              <thead>
                <tr>
                  <th style="width: 40px; text-align: center;">
                    <input type="checkbox" @change="toggleSelectAllBatch" />
                  </th>
                  <th>N° Control</th>
                  <th>Nombre Estudiante</th>
                  <th>Asesor Actual</th>
                </tr>
              </thead>
              <tbody>
                <tr v-if="isBatchLoading">
                  <td colspan="4" class="tecnm-table-empty">Cargando alumnos...</td>
                </tr>
                <tr v-else-if="paginatedBatchStudents.length === 0">
                  <td colspan="4" class="tecnm-table-empty">No hay estudiantes disponibles.</td>
                </tr>
                <tr v-for="st in paginatedBatchStudents" v-else :key="st.id">
                  <td style="text-align: center;">
                    <input type="checkbox" :value="st.id" v-model="selectedStudentIds" />
                  </td>
                  <td><strong>{{ st.controlNumber }}</strong></td>
                  <td>{{ st.fullName || `${st.firstName} ${st.lastName}` }}</td>
                  <td>
                    <span v-if="st.advisorName" class="tecnm-text-muted">{{ st.advisorName }}</span>
                    <span v-else class="tecnm-badge tecnm-badge-warning" style="font-size: 0.7rem;">Sin Asesor</span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <div style="margin-top: 0.75rem;">
            <TecnmPagination
              v-if="batchTotalCount > 0"
              v-model:currentPage="batchPage"
              v-model:pageSize="batchPageSize"
              :totalPages="batchTotalPages"
              :totalCount="batchTotalCount"
            />
          </div>
        </div>

        <div class="tecnm-modal-footer">
          <button type="button" class="tecnm-btn tecnm-btn-secondary" @click="isBatchModalOpen = false">Cancelar</button>
          <button
            type="button"
            class="tecnm-btn tecnm-btn-primary"
            :disabled="isSubmitting || !selectedAdvisorId || selectedStudentIds.length === 0"
            @click="submitBatchAssignment"
          >
            {{ isSubmitting ? 'Asignando...' : `Asignar a ${selectedStudentIds.length} Estudiantes` }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

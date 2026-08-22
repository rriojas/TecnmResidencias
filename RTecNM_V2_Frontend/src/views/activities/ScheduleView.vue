<script setup>
import { ref, computed, onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useGlobalSearch } from '@/composables/useGlobalSearch'
import apiClient from '@/services/api'

const authStore = useAuthStore()
const { open: openGlobalSearch } = useGlobalSearch()

const isStudent = computed(() =>
  authStore.hasRole('student') && !authStore.hasRole('admin', 'departmenthead', 'advisor')
)
const isAdvisor = computed(() =>
  authStore.hasRole('advisor') && !authStore.hasRole('admin', 'departmenthead')
)
const canAddActivity = computed(() => {
  if (isAdvisor.value) return false
  return !!currentProject.value?.id
})

const currentProject = ref(null)
const activities = ref([])
const isLoading = ref(false)
const errorMessage = ref('')
const alertMessage = ref('')
const alertType = ref('info')

// Modal de agregar actividad
const isModalOpen = ref(false)
const activityTitle = ref('')
const plannedWeeks = ref(2)
const isSubmitting = ref(false)

function showAlert(message, type = 'info') {
  alertMessage.value = message
  alertType.value = type
  setTimeout(() => {
    if (alertMessage.value === message) {
      alertMessage.value = ''
    }
  }, 5000)
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

async function initSchedule() {
  if (isStudent.value) {
    await resolveStudentProject()
  } else {
    await loadInitialProjectForStaff()
  }
}

async function resolveStudentProject() {
  isLoading.value = true
  errorMessage.value = ''
  try {
    const res = await apiClient.get('/v1/projects/me/current')
    if (res.data && res.data.id) {
      currentProject.value = res.data
      await loadSchedule(res.data.id)
    } else {
      errorMessage.value =
        'No tienes un proyecto aprobado o en curso. Registra tu solicitud de anteproyecto para generar tu cronograma de actividades.'
    }
  } catch (err) {
    if (err.response?.status === 404) {
      errorMessage.value =
        'No tienes un proyecto aprobado o en curso. Registra tu solicitud de anteproyecto para generar tu cronograma de actividades.'
    } else if (err.response?.status === 403) {
      errorMessage.value =
        'El cronograma personal es exclusivo para estudiantes con un proyecto vigente.'
    } else {
      errorMessage.value = 'Error al cargar el cronograma de actividades desde el servidor.'
    }
  } finally {
    isLoading.value = false
  }
}

async function loadInitialProjectForStaff() {
  isLoading.value = true
  errorMessage.value = ''
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
      errorMessage.value =
        'No se encontraron anteproyectos asignados. Utilice el botón "Buscar Anteproyecto" para seleccionar uno.'
      currentProject.value = null
      activities.value = []
      return
    }

    await selectProject(list[0])
  } catch {
    errorMessage.value = 'Haga clic en "Buscar Anteproyecto" para cargar un cronograma.'
    currentProject.value = null
    activities.value = []
  } finally {
    isLoading.value = false
  }
}

async function selectProject(project) {
  if (!project || !project.id) return
  currentProject.value = project
  errorMessage.value = ''
  await loadSchedule(project.id)

  // Enriquecer datos de estudiante si faltan
  if (!project.studentName && !project.student_name) {
    try {
      const res = await apiClient.get(`/v1/projects/${project.id}`)
      if (res.data) {
        currentProject.value = { ...project, ...res.data }
      }
    } catch {}
  }
}

async function loadSchedule(projectId) {
  if (!projectId) return
  isLoading.value = true
  errorMessage.value = ''
  try {
    const res = await apiClient.get(`/v1/projects/${projectId}/activities`)
    activities.value = Array.isArray(res.data) ? res.data : []
  } catch (err) {
    errorMessage.value = 'Error al cargar el cronograma de actividades desde la base de datos.'
    activities.value = []
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

function getWeekProgress(act, weekNum) {
  const progresses = act.progresses || []
  return progresses.find((p) => p.weekNumber === weekNum) || { status: 'pending' }
}

function getStatusClass(status) {
  const s = String(status || '').toLowerCase()
  if (s === 'completed' || s === 'completado') return 'completed'
  if (s === 'in_progress' || s === 'en_proceso' || s === 'en_progreso') return 'in_progress'
  return 'pending'
}

function getStatusSymbol(status) {
  const s = String(status || '').toLowerCase()
  if (s === 'completed' || s === 'completado') return '✓'
  if (s === 'in_progress' || s === 'en_proceso' || s === 'en_progreso') return '•'
  return ''
}

function getStatusLabelSpanish(status) {
  const s = String(status || '').toLowerCase()
  if (s === 'completed' || s === 'completado') return 'Completado'
  if (s === 'in_progress' || s === 'en_proceso' || s === 'en_progreso') return 'En Proceso'
  return 'Pendiente'
}

async function cycleWeekStatus(act, weekNum) {
  if (!currentProject.value?.id) return

  const currentProg = getWeekProgress(act, weekNum)
  const currentStatus = currentProg.status || 'pending'

  let nextStatus = 'in_progress'
  if (currentStatus === 'in_progress' || currentStatus === 'en_proceso' || currentStatus === 'en_progreso') {
    nextStatus = 'completed'
  } else if (currentStatus === 'completed' || currentStatus === 'completado') {
    nextStatus = 'pending'
  }

  // Actualización optimista local
  if (!act.progresses) act.progresses = []
  const existingIdx = act.progresses.findIndex((p) => p.weekNumber === weekNum)
  if (existingIdx >= 0) {
    act.progresses[existingIdx].status = nextStatus
  } else {
    act.progresses.push({ weekNumber: weekNum, status: nextStatus })
  }

  try {
    const res = await apiClient.post(
      `/v1/projects/${currentProject.value.id}/activities/progress`,
      {
        activityId: act.id,
        weekNumber: weekNum,
        status: nextStatus,
      }
    )
    if (res.status === 200 || res.status === 201 || res.status === 204) {
      const statusSpanish = getStatusLabelSpanish(nextStatus)
      showAlert(`Avance actualizado: Semana ${weekNum} (${statusSpanish}).`, 'success')
    }
  } catch (err) {
    // Revertir en caso de error
    if (existingIdx >= 0) {
      act.progresses[existingIdx].status = currentStatus
    }
    showAlert('Error al actualizar el avance semanal.', 'danger')
  }
}

function openAddModal() {
  if (!currentProject.value?.id) {
    showAlert('Debe seleccionar o registrar un anteproyecto activo primero.', 'warning')
    return
  }
  activityTitle.value = ''
  plannedWeeks.value = 2
  isModalOpen.value = true
}

function closeModal() {
  isModalOpen.value = false
  activityTitle.value = ''
  plannedWeeks.value = 2
}

async function handleAddActivity() {
  if (!currentProject.value?.id) {
    showAlert('Debe seleccionar un anteproyecto válido.', 'warning')
    return
  }
  const title = activityTitle.value.trim()
  if (!title) {
    showAlert('Ingrese una descripción válida para la actividad.', 'warning')
    return
  }

  isSubmitting.value = true
  const activityNumber = activities.value.length + 1

  try {
    const payload = {
      projectId: currentProject.value.id,
      activityNumber,
      title,
      plannedWeeks: plannedWeeks.value || 2,
    }

    await apiClient.post(`/v1/projects/${currentProject.value.id}/activities`, payload)
    showAlert('¡Actividad agregada correctamente al cronograma!', 'success')
    closeModal()
    await loadSchedule(currentProject.value.id)
  } catch (err) {
    const msg = err.response?.data?.message || 'Error al agregar la actividad.'
    showAlert(msg, 'danger')
  } finally {
    isSubmitting.value = false
  }
}

onMounted(() => {
  initSchedule()
})
</script>

<template>
  <div>
    <!-- Barra de Título y Acciones -->
    <div class="tecnm-actions-bar">
      <div>
        <h1 class="tecnm-page-title">Cronograma de Actividades (26 Semanas)</h1>
        <p class="tecnm-page-subtitle">Seguimiento semanal del plan de trabajo de residencia profesional</p>
      </div>
      <button
        v-if="!isAdvisor"
        id="addActivityBtn"
        type="button"
        class="tecnm-btn tecnm-btn-primary"
        :disabled="!canAddActivity"
        @click="openAddModal"
      >
        + Nueva Actividad
      </button>
    </div>

    <!-- Alert de Notificación Flotante Institucional -->
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

    <!-- Tarjeta Principal con Matriz de 26 Semanas -->
    <div class="tecnm-card">
      <div class="tecnm-card-header tecnm-d-flex tecnm-justify-between tecnm-align-center">
        <div class="tecnm-d-flex tecnm-align-center tecnm-gap-2 tecnm-flex-wrap">
          <h3 class="tecnm-card-title">Matriz de Avance Semanal</h3>
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
            <span id="selectedProjectBadge" class="tecnm-badge tecnm-badge-info">
              {{ selectedProjectText }}
            </span>
          </div>
        </div>

        <div class="tecnm-legend">
          <span>Leyenda:</span>
          <span class="tecnm-badge tecnm-badge-approved">Completado</span>
          <span class="tecnm-badge tecnm-badge-pending">En Proceso</span>
          <span class="tecnm-badge tecnm-badge-neutral">Pendiente</span>
        </div>
      </div>

      <div class="tecnm-card-body">
        <div class="tecnm-table-responsive">
          <table class="matrix-table tecnm-table">
            <thead>
              <tr>
                <th>#</th>
                <th class="act-title-col">Descripción de la Actividad</th>
                <th
                  v-for="w in 26"
                  :key="w"
                  class="matrix-week-header"
                  :title="`Semana ${w}`"
                >
                  S{{ w }}
                </th>
              </tr>
            </thead>
            <tbody id="scheduleTableBody">
              <tr v-if="isLoading">
                <td colspan="28" class="tecnm-table-empty">
                  Cargando cronograma de actividades...
                </td>
              </tr>
              <tr v-else-if="errorMessage">
                <td colspan="28" class="tecnm-table-empty tecnm-text-danger">
                  {{ errorMessage }}
                </td>
              </tr>
              <tr v-else-if="activities.length === 0">
                <td colspan="28" class="tecnm-table-empty">
                  No hay actividades registradas en el cronograma. Haga clic en "+ Nueva Actividad".
                </td>
              </tr>
              <tr
                v-for="act in activities"
                v-else
                :key="act.id"
              >
                <td><strong>{{ act.activityNumber }}</strong></td>
                <td class="act-title-col">{{ act.title }}</td>
                <td
                  v-for="w in 26"
                  :key="w"
                  class="week-cell"
                  :class="getStatusClass(getWeekProgress(act, w).status)"
                  :data-activity-id="act.id"
                  :data-week="w"
                  :data-status="getWeekProgress(act, w).status"
                  :title="`Actividad: ${act.title} - Semana ${w} (${getStatusLabelSpanish(getWeekProgress(act, w).status)})`"
                  @click="cycleWeekStatus(act, w)"
                >
                  {{ getStatusSymbol(getWeekProgress(act, w).status) }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- Modal Registrar Nueva Actividad -->
    <div
      id="createActivityModal"
      class="modal-backdrop"
      :class="{ active: isModalOpen }"
      aria-modal="true"
      role="dialog"
    >
      <div class="modal-card">
        <div class="tecnm-modal-header">
          <h3 class="tecnm-modal-title">Agregar Nueva Actividad al Cronograma</h3>
          <button
            id="closeActivityModalBtn"
            type="button"
            class="tecnm-modal-close"
            aria-label="Cerrar"
            @click="closeModal"
          >
            &times;
          </button>
        </div>

        <form id="activityForm" @submit.prevent="handleAddActivity">
          <div class="tecnm-form-group">
            <label for="activityTitleInput" class="tecnm-label">Descripción de la Actividad *</label>
            <input
              id="activityTitleInput"
              v-model="activityTitle"
              type="text"
              class="tecnm-form-control"
              placeholder="Ej. Análisis de requerimientos y diseño de base de datos..."
              required
            />
          </div>

          <div class="tecnm-form-group">
            <label for="plannedWeeksInput" class="tecnm-label">Semanas Estimadas de Duración</label>
            <input
              id="plannedWeeksInput"
              v-model.number="plannedWeeks"
              type="number"
              class="tecnm-form-control"
              min="1"
              max="26"
            />
          </div>

          <div class="tecnm-modal-footer">
            <button
              id="cancelActivityModalBtn"
              type="button"
              class="tecnm-btn tecnm-btn-secondary"
              @click="closeModal"
            >
              Cancelar
            </button>
            <button
              id="submitActivityBtn"
              type="submit"
              class="tecnm-btn tecnm-btn-primary"
              :disabled="isSubmitting"
            >
              {{ isSubmitting ? 'Guardando...' : 'Guardar Actividad' }}
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

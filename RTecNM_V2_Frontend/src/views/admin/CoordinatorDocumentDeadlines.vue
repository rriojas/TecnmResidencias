<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useGlobalSearch } from '@/composables/useGlobalSearch'
import apiClient from '@/services/api'
import TecnmPagination from '@/components/common/TecnmPagination.vue'
import TecnmBadge from '@/components/common/TecnmBadge.vue'

const router = useRouter()
const authStore = useAuthStore()
const { open: openGlobalSearch } = useGlobalSearch()

const students = ref([])
const isLoading = ref(false)
const isSavingGlobal = ref(false)

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

// Permisos para editar fechas globales
const canEditDeadlines = computed(() => {
  return (
    authStore.isAdmin ||
    authStore.isCoordinator ||
    authStore.isCareerHead ||
    authStore.hasRole('coordinadora') ||
    authStore.hasRole('coordinator') ||
    authStore.hasRole('jefecarrera') ||
    authStore.hasRole('departmenthead')
  ) && !authStore.isReadOnly
})

// Fechas Límite Globales
const globalDeadlines = ref({
  formato29Deadline: '',
  formato30Deadline: '',
  updatedAt: null
})

// Filtros y Paginación
const pageNumber = ref(1)
const pageSize = ref(10)
const totalCount = ref(0)
const totalPages = ref(0)
const searchTerm = ref('')
const selectedCareer = ref(
  authStore.isCareerHead && authStore.userCareerId
    ? String(authStore.userCareerId)
    : authStore.isCoordinator && authStore.userCareerIds.length > 0
    ? String(authStore.userCareerIds[0])
    : 'all'
)
const careersList = ref([])

// Ordenamiento Ascendente y Descendente en todas las columnas
const sortBy = ref('controlNumber')
const sortDir = ref('asc')

function toggleSort(col) {
  if (sortBy.value.toLowerCase() === col.toLowerCase()) {
    sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
  } else {
    sortBy.value = col
    sortDir.value = 'asc'
  }
}

function getCareerName(careerId) {
  const c = careersList.value.find((item) => Number(item.id) === Number(careerId))
  return c ? (c.shortName || c.code || c.name) : '—'
}

const sortedStudents = computed(() => {
  const list = [...students.value]
  if (!sortBy.value) return list

  return list.sort((a, b) => {
    let valA, valB
    switch (sortBy.value.toLowerCase()) {
      case 'controlnumber':
        valA = a.controlNumber || ''
        valB = b.controlNumber || ''
        break
      case 'fullname':
        valA = a.fullName || ''
        valB = b.fullName || ''
        break
      case 'career':
        valA = a.careerName || getCareerName(a.careerId) || ''
        valB = b.careerName || getCareerName(b.careerId) || ''
        break
      case 'formato29deadline':
        valA = a.formato29Deadline || globalDeadlines.value.formato29Deadline || ''
        valB = b.formato29Deadline || globalDeadlines.value.formato29Deadline || ''
        break
      case 'formato29status':
        valA = a.formato29Status || ''
        valB = b.formato29Status || ''
        break
      case 'formato30deadline':
        valA = a.formato30Deadline || globalDeadlines.value.formato30Deadline || ''
        valB = b.formato30Deadline || globalDeadlines.value.formato30Deadline || ''
        break
      case 'formato30status':
        valA = a.formato30Status || ''
        valB = b.formato30Status || ''
        break
      default:
        return 0
    }
    const cmp = String(valA).localeCompare(String(valB), 'es', { numeric: true, sensitivity: 'base' })
    return sortDir.value === 'asc' ? cmp : -cmp
  })
})

// Formato de Fechas
const MONTH_NAMES_ES = [
  'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
  'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'
]

function formatTecNMDate(iso) {
  if (!iso) return '—'
  const d = new Date(iso)
  if (isNaN(d.getTime())) return '—'
  const day = String(d.getDate()).padStart(2, '0')
  const month = MONTH_NAMES_ES[d.getMonth()]
  const year = d.getFullYear()
  return `${day}/${month}/${year}`
}

function isDeadlinePassed(iso) {
  if (!iso) return false
  return new Date() > new Date(iso)
}

function getDeadlineRemaining(iso) {
  if (!iso) return 'Sin fecha límite'
  if (isDeadlinePassed(iso)) return 'Vencida'
  const days = Math.ceil((new Date(iso) - new Date()) / (1000 * 60 * 60 * 24))
  return days <= 3 ? `Vence en ${days} día(s)` : `Vence en ${days} días`
}

// Carga de Carreras
async function loadCareers() {
  try {
    const res = await apiClient.get('/v1/careers/all')
    const list = res.data || []
    if (authStore.isCoordinator && authStore.userCareerIds.length > 0) {
      const allowed = authStore.userCareerIds.map(Number)
      careersList.value = list.filter((c) => allowed.includes(Number(c.id)))
    } else {
      careersList.value = list
    }
  } catch (err) {
    console.error('Error al cargar carreras:', err)
  }
}

// Carga de Fechas Límite Globales
async function loadGlobalDeadlines() {
  try {
    const res = await apiClient.get('/v1/students/document-deadlines/global')
    const data = res.data || {}
    globalDeadlines.value = {
      formato29Deadline: data.formato29Deadline ? new Date(data.formato29Deadline).toISOString().split('T')[0] : '',
      formato30Deadline: data.formato30Deadline ? new Date(data.formato30Deadline).toISOString().split('T')[0] : '',
      updatedAt: data.updatedAt || null
    }
  } catch (err) {
    console.error('Error al cargar fechas globales:', err)
  }
}

// Guardar Fechas Límite Globales
async function saveGlobalDeadlines() {
  if (!canEditDeadlines.value) return
  isSavingGlobal.value = true
  try {
    const payload = {
      formato29Deadline: globalDeadlines.value.formato29Deadline
        ? new Date(globalDeadlines.value.formato29Deadline + 'T23:59:59Z').toISOString()
        : null,
      formato30Deadline: globalDeadlines.value.formato30Deadline
        ? new Date(globalDeadlines.value.formato30Deadline + 'T23:59:59Z').toISOString()
        : null
    }
    await apiClient.put('/v1/students/document-deadlines/global', payload)
    showAlert('Fechas límite globales actualizadas y aplicadas correctamente a todos los estudiantes.', 'success')
    await loadGlobalDeadlines()
    await loadStudents()
  } catch (err) {
    console.error('Error al guardar fechas globales:', err)
    showAlert(err.response?.data?.message || 'Error al guardar fechas límite globales.', 'danger')
  } finally {
    isSavingGlobal.value = false
  }
}

function setQuickDate(field, daysFromNow) {
  const target = new Date()
  target.setDate(target.getDate() + daysFromNow)
  globalDeadlines.value[field] = target.toISOString().split('T')[0]
}

// Carga de Estudiantes
async function loadStudents() {
  isLoading.value = true
  try {
    const params = {
      pageNumber: pageNumber.value,
      pageSize: pageSize.value,
      search: searchTerm.value.trim() || undefined,
      status: 'active',
      careerId: selectedCareer.value !== 'all' ? Number(selectedCareer.value) : undefined
    }

    const res = await apiClient.get('/v1/students', { params })
    const data = res.data
    students.value = data.items || []
    totalCount.value = data.totalCount || 0
    totalPages.value = data.totalPages || 0
    pageNumber.value = data.pageNumber || 1
  } catch (err) {
    console.error('Error al cargar estudiantes:', err)
    showAlert('Error al consultar lista de estudiantes.', 'danger')
    students.value = []
  } finally {
    isLoading.value = false
  }
}

// Búsqueda Global en toolbar
function openSearchFromToolbar() {
  openGlobalSearch({
    initialSource: 'STUDENTS',
    onSelect: handleGlobalSearchSelect
  })
}

function handleGlobalSearchSelect(item) {
  if (!item) return
  searchTerm.value = item.controlNumber || item.title || item.name || ''
  pageNumber.value = 1
  loadStudents()
}

function clearSearch() {
  searchTerm.value = ''
  pageNumber.value = 1
  loadStudents()
}

function onCareerChange() {
  pageNumber.value = 1
  loadStudents()
}

// Redirección a la vista organizada de formatos
function goToReview(s) {
  router.push({ name: 'StudentFormatReview', params: { id: s.id } })
}

onMounted(async () => {
  await Promise.all([loadCareers(), loadGlobalDeadlines(), loadStudents()])
})
</script>

<template>
  <div>
    <!-- Notificación Flotante -->
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

    <!-- Barra de Título -->
    <div class="tecnm-actions-bar">
      <div>
        <h1 class="tecnm-page-title">Gestión de Fechas Límite de Formatos (29 y 30)</h1>
        <p class="tecnm-page-subtitle">
          Control institucional de plazos globales de entrega y acceso al expediente de formatos por estudiante
        </p>
      </div>
    </div>

    <!-- Panel de Configuración de Fechas Límite Institucionales (Globales) -->
    <div class="tecnm-card tecnm-mb-3" style="border-left: 4px solid var(--tecnm-blue-primary, #1B396A);">
      <div class="tecnm-card-header" style="display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 0.5rem;">
        <div>
          <h3 class="tecnm-card-title" style="margin-bottom: 0.2rem;">
            Plazos Oficiales Institucionales (Fechas Globales)
          </h3>
          <p class="tecnm-text-sub" style="margin-bottom: 0; font-size: 0.85rem;">
            Las fechas configuradas aplican de manera global a todos los estudiantes de residencia. No se asignan uno por uno.
          </p>
        </div>
        <span v-if="globalDeadlines.updatedAt" class="tecnm-badge tecnm-badge-outline" style="font-size: 0.8rem;">
          Última actualización: {{ formatTecNMDate(globalDeadlines.updatedAt) }}
        </span>
      </div>

      <div class="tecnm-card-body" style="padding: 1.25rem;">
        <form @submit.prevent="saveGlobalDeadlines">
          <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 1.25rem;">
            <!-- Fecha Global Formato 29 -->
            <div class="tecnm-form-group tecnm-mb-0">
              <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.4rem;">
                <label for="globalFormato29Date" class="tecnm-label" style="margin-bottom: 0; font-weight: 600;">
                  Fecha Límite Global: Formato 29 (1er Seguimiento)
                </label>
                <div v-if="canEditDeadlines" style="display: flex; gap: 0.25rem;">
                  <button type="button" class="tecnm-btn tecnm-btn-outline tecnm-btn-xs" @click="setQuickDate('formato29Deadline', 15)">+15d</button>
                  <button type="button" class="tecnm-btn tecnm-btn-outline tecnm-btn-xs" @click="setQuickDate('formato29Deadline', 30)">+30d</button>
                </div>
              </div>
              <input
                id="globalFormato29Date"
                v-model="globalDeadlines.formato29Deadline"
                type="date"
                class="tecnm-form-control"
                :disabled="!canEditDeadlines || isSavingGlobal"
              />
              <span class="tecnm-form-hint" style="font-size: 0.8rem;">
                Plazo límite para la primera entrega del reporte de seguimiento. Al vencer sin aprobación, bloquea el avance del residente.
              </span>
            </div>

            <!-- Fecha Global Formato 30 y 29v2 -->
            <div class="tecnm-form-group tecnm-mb-0">
              <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.4rem;">
                <label for="globalFormato30Date" class="tecnm-label" style="margin-bottom: 0; font-weight: 600;">
                  Fecha Límite Global: Formato 30 y 29v2 (Entrega Final)
                </label>
                <div v-if="canEditDeadlines" style="display: flex; gap: 0.25rem;">
                  <button type="button" class="tecnm-btn tecnm-btn-outline tecnm-btn-xs" @click="setQuickDate('formato30Deadline', 60)">+60d</button>
                  <button type="button" class="tecnm-btn tecnm-btn-outline tecnm-btn-xs" @click="setQuickDate('formato30Deadline', 90)">+90d</button>
                </div>
              </div>
              <input
                id="globalFormato30Date"
                v-model="globalDeadlines.formato30Deadline"
                type="date"
                class="tecnm-form-control"
                :disabled="!canEditDeadlines || isSavingGlobal"
              />
              <span class="tecnm-form-hint" style="font-size: 0.8rem;">
                Plazo para la subida conjunta del segundo reporte Formato 29 y el Formato 30 de evaluación final.
              </span>
            </div>
          </div>

          <div v-if="canEditDeadlines" style="margin-top: 1rem; display: flex; justify-content: flex-end;">
            <button
              id="saveGlobalDeadlinesBtn"
              type="submit"
              class="tecnm-btn tecnm-btn-primary"
              :disabled="isSavingGlobal"
            >
              <span v-if="isSavingGlobal">Guardando Plazos Globales...</span>
              <span v-else>Guardar Plazos Globales</span>
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Tarjeta Principal con Toolbar y Tabla -->
    <div class="tecnm-card">
      <div class="tecnm-card-header" style="display: flex; justify-content: space-between; align-items: center;">
        <h3 class="tecnm-card-title">Seguimiento de Formatos por Estudiante</h3>
        <span class="tecnm-badge tecnm-badge-outline">
          Total: {{ totalCount }} estudiantes
        </span>
      </div>

      <!-- Toolbar: Botón de Búsqueda Global reemplazando la textbox genérica, junto al combobox -->
      <div class="tecnm-card-toolbar" style="display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 0.75rem;">
        <div style="display: flex; align-items: center; gap: 0.75rem; flex-wrap: wrap;">
          <!-- Botón de Búsqueda Global Integrado -->
          <button
            id="globalSearchBtnToolbar"
            type="button"
            class="tecnm-btn tecnm-btn-secondary"
            title="Abrir buscador global del sistema"
            style="display: inline-flex; align-items: center; gap: 0.4rem;"
            @click="openSearchFromToolbar"
          >
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
            </svg>
            <span>Buscar Estudiante (Global)</span>
          </button>

          <!-- Indicador si hay filtro de búsqueda activo con opción de limpiar -->
          <div v-if="searchTerm" style="display: inline-flex; align-items: center; gap: 0.35rem; background: var(--tecnm-gray-100); padding: 0.25rem 0.5rem; border-radius: 4px; font-size: 0.85rem;">
            <span>Filtrado: <strong>{{ searchTerm }}</strong></span>
            <button
              type="button"
              class="tecnm-btn tecnm-btn-xs tecnm-btn-outline"
              style="padding: 0 0.35rem; line-height: 1.2;"
              title="Limpiar búsqueda"
              @click="clearSearch"
            >
              &times;
            </button>
          </div>
        </div>

        <!-- Combobox de Carrera -->
        <div v-if="!authStore.isCareerHead" style="display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap;">
          <label for="careerFilterSelect" class="tecnm-field-label" style="margin-bottom: 0; white-space: nowrap; font-size: 0.85rem;">Carrera:</label>
          <select
            id="careerFilterSelect"
            v-model="selectedCareer"
            class="tecnm-form-control"
            style="min-width: 230px; font-size: 0.85rem;"
            @change="onCareerChange"
          >
            <option value="all">{{ authStore.isCoordinator ? 'Mis Carreras Asignadas' : 'Todas las Carreras' }}</option>
            <option v-for="c in careersList" :key="c.id" :value="String(c.id)">
              {{ c.name }}
            </option>
          </select>
        </div>
      </div>

      <!-- Tabla Institucional con Ordenamiento Ascendente y Descendente en Todas las Columnas -->
      <div class="tecnm-card-body" style="padding: 0;">
        <div class="tecnm-table-responsive">
          <table class="tecnm-table tecnm-table-striped" aria-label="Tabla de fechas límite y formatos">
            <thead>
              <tr>
                <th class="tecnm-sortable-th" @click="toggleSort('controlNumber')">
                  <div style="display: flex; align-items: center; justify-content: space-between; gap: 0.25rem; cursor: pointer;">
                    <span>No. Control</span>
                    <span class="tecnm-sort-icon">
                      {{ sortBy.toLowerCase() === 'controlnumber' ? (sortDir === 'asc' ? '▲' : '▼') : '↕' }}
                    </span>
                  </div>
                </th>
                <th class="tecnm-sortable-th" @click="toggleSort('fullName')">
                  <div style="display: flex; align-items: center; justify-content: space-between; gap: 0.25rem; cursor: pointer;">
                    <span>Estudiante</span>
                    <span class="tecnm-sort-icon">
                      {{ sortBy.toLowerCase() === 'fullname' ? (sortDir === 'asc' ? '▲' : '▼') : '↕' }}
                    </span>
                  </div>
                </th>
                <th class="tecnm-sortable-th" @click="toggleSort('career')">
                  <div style="display: flex; align-items: center; justify-content: space-between; gap: 0.25rem; cursor: pointer;">
                    <span>Carrera</span>
                    <span class="tecnm-sort-icon">
                      {{ sortBy.toLowerCase() === 'career' ? (sortDir === 'asc' ? '▲' : '▼') : '↕' }}
                    </span>
                  </div>
                </th>
                <th class="tecnm-sortable-th" @click="toggleSort('formato29Deadline')">
                  <div style="display: flex; align-items: center; justify-content: space-between; gap: 0.25rem; cursor: pointer;">
                    <span>Fecha Límite 29</span>
                    <span class="tecnm-sort-icon">
                      {{ sortBy.toLowerCase() === 'formato29deadline' ? (sortDir === 'asc' ? '▲' : '▼') : '↕' }}
                    </span>
                  </div>
                </th>
                <th class="tecnm-sortable-th" @click="toggleSort('formato29Status')">
                  <div style="display: flex; align-items: center; justify-content: space-between; gap: 0.25rem; cursor: pointer;">
                    <span>Estado Formato 29</span>
                    <span class="tecnm-sort-icon">
                      {{ sortBy.toLowerCase() === 'formato29status' ? (sortDir === 'asc' ? '▲' : '▼') : '↕' }}
                    </span>
                  </div>
                </th>
                <th class="tecnm-sortable-th" @click="toggleSort('formato30Deadline')">
                  <div style="display: flex; align-items: center; justify-content: space-between; gap: 0.25rem; cursor: pointer;">
                    <span>Fecha Límite 30 / 29v2</span>
                    <span class="tecnm-sort-icon">
                      {{ sortBy.toLowerCase() === 'formato30deadline' ? (sortDir === 'asc' ? '▲' : '▼') : '↕' }}
                    </span>
                  </div>
                </th>
                <th class="tecnm-sortable-th" @click="toggleSort('formato30Status')">
                  <div style="display: flex; align-items: center; justify-content: space-between; gap: 0.25rem; cursor: pointer;">
                    <span>Estado Final (29v2 / 30)</span>
                    <span class="tecnm-sort-icon">
                      {{ sortBy.toLowerCase() === 'formato30status' ? (sortDir === 'asc' ? '▲' : '▼') : '↕' }}
                    </span>
                  </div>
                </th>
                <th class="tecnm-th-actions">Acciones</th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="isLoading">
                <td colspan="8" class="tecnm-table-empty">
                  Consultando plazos y estados de formatos...
                </td>
              </tr>
              <tr v-else-if="sortedStudents.length === 0">
                <td colspan="8" class="tecnm-table-empty">
                  No se encontraron estudiantes para los filtros seleccionados.
                </td>
              </tr>
              <tr v-for="s in sortedStudents" :key="s.id">
                <td>
                  <strong>{{ s.controlNumber }}</strong>
                </td>
                <td>
                  <div>{{ s.fullName }}</div>
                  <small style="color: var(--tecnm-gray-600);">{{ s.email }}</small>
                </td>

                <!-- Columna Carrera -->
                <td>
                  <span style="font-size: 0.85rem; font-weight: 500;">
                    {{ s.careerName || getCareerName(s.careerId) }}
                  </span>
                </td>

                <!-- Columna Formato 29 -->
                <td>
                  <template v-if="s.isAccreditation">
                    <span class="tecnm-badge tecnm-badge-secondary" title="Exento de formatos por InnovaTecNM">Exento</span>
                  </template>
                  <template v-else>
                    <div>{{ formatTecNMDate(s.formato29Deadline || globalDeadlines.formato29Deadline) }}</div>
                    <small
                      v-if="s.formato29Deadline || globalDeadlines.formato29Deadline"
                      :style="{ color: isDeadlinePassed(s.formato29Deadline || globalDeadlines.formato29Deadline) && s.formato29Status !== 'approved' ? 'var(--tecnm-red)' : 'var(--tecnm-gray-600)' }"
                    >
                      {{ s.formato29Status === 'approved' ? 'Entregado a tiempo' : getDeadlineRemaining(s.formato29Deadline || globalDeadlines.formato29Deadline) }}
                    </small>
                  </template>
                </td>
                <td>
                  <span
                    v-if="s.isAccreditation"
                    class="tecnm-badge tecnm-badge-outline"
                    title="No requiere formatos ordinarios"
                  >
                    No aplica
                  </span>
                  <span
                    v-else
                    class="tecnm-badge"
                    :class="s.formato29Status === 'approved' ? 'tecnm-badge-success' : (s.formato29Status === 'rejected' ? 'tecnm-badge-danger' : (s.formato29Status === 'uploaded' || s.formato29Status === 'under_review' ? 'tecnm-badge-info' : 'tecnm-badge-secondary'))"
                  >
                    {{ s.formato29Status === 'approved' ? 'Validado y Aprobado' : (s.formato29Status === 'rejected' ? 'Corrección Requerida' : (s.formato29Status === 'uploaded' || s.formato29Status === 'under_review' ? 'En Revisión' : 'Pendiente')) }}
                  </span>
                </td>

                <!-- Columna Formato 30 -->
                <td>
                  <template v-if="s.isAccreditation">
                    <span class="tecnm-badge tecnm-badge-secondary" title="Exento de formatos por InnovaTecNM">Exento</span>
                  </template>
                  <template v-else>
                    <div>{{ formatTecNMDate(s.formato30Deadline || globalDeadlines.formato30Deadline) }}</div>
                    <small
                      v-if="s.formato30Deadline || globalDeadlines.formato30Deadline"
                      :style="{ color: isDeadlinePassed(s.formato30Deadline || globalDeadlines.formato30Deadline) && s.formato30Status !== 'approved' ? 'var(--tecnm-red)' : 'var(--tecnm-gray-600)' }"
                    >
                      {{ s.formato30Status === 'approved' ? 'Entregado a tiempo' : getDeadlineRemaining(s.formato30Deadline || globalDeadlines.formato30Deadline) }}
                    </small>
                  </template>
                </td>
                <td>
                  <span
                    v-if="s.isAccreditation"
                    class="tecnm-badge tecnm-badge-outline"
                    title="No requiere formatos ordinarios"
                  >
                    No aplica
                  </span>
                  <span
                    v-else
                    class="tecnm-badge"
                    :class="s.formato30Status === 'approved' && s.formato29V2Status === 'approved' ? 'tecnm-badge-success' : (!s.canUploadSecondPhase ? 'tecnm-badge-secondary' : 'tecnm-badge-warning')"
                  >
                    {{ !s.canUploadSecondPhase ? 'Bloqueado (Falta 29)' : (s.formato30Status === 'approved' ? 'Aprobados' : 'Habilitado') }}
                  </span>
                </td>

                <!-- Acciones: Redirección a la vista organizada de formatos -->
                <td class="tecnm-row-actions">
                  <button
                    type="button"
                    class="tecnm-btn tecnm-btn-primary tecnm-btn-sm"
                    title="Ver expediente organizado y formatos"
                    @click="goToReview(s)"
                  >
                    Ver Formatos &rarr;
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Paginación -->
        <div style="padding: 1rem;">
          <TecnmPagination
            :current-page="pageNumber"
            :total-pages="totalPages"
            :total-count="totalCount"
            :page-size="pageSize"
            @update:current-page="pageNumber = $event"
            @page-change="loadStudents"
          />
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
.tecnm-sortable-th {
  user-select: none;
  cursor: pointer;
  white-space: nowrap;
}
.tecnm-sortable-th:hover {
  background-color: var(--tecnm-gray-200, #E2E8F0);
}
.tecnm-sort-icon {
  font-size: 0.75rem;
  color: var(--tecnm-gray-500);
}
</style>
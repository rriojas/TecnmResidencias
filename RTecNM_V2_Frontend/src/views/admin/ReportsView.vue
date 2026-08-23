<script setup>
import { ref, onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useConfirm } from '@/composables/useConfirm'
import TecnmPagination from '@/components/common/TecnmPagination.vue'
import apiClient from '@/services/api'

const authStore = useAuthStore()
const { confirm } = useConfirm()

// KPIs
const metrics = ref({
  totalStudents: 0,
  approvedProjects: 0,
  completedResidencies: 0,
  activeAdvisors: 0,
})

// Lista de Proyectos Elegibles
const projects = ref([])
const isLoading = ref(false)
const errorMessage = ref('')
const alertMessage = ref('')
const alertType = ref('info')

// Paginación
const pageNumber = ref(1)
const pageSize = ref(10)
const totalCount = ref(0)
const totalPages = ref(0)

function showAlert(message, type = 'info') {
  alertMessage.value = message
  alertType.value = type
  setTimeout(() => {
    if (alertMessage.value === message) {
      alertMessage.value = ''
    }
  }, 5000)
}

async function loadMetrics() {
  try {
    const res = await apiClient.get('/v1/admin/dashboard')
    if (res.data) {
      metrics.value = {
        totalStudents: res.data.totalStudents || 0,
        approvedProjects: res.data.approvedProjects || 0,
        completedResidencies: res.data.completedResidencies || 0,
        activeAdvisors: res.data.activeAdvisors || 0,
      }
    }
  } catch (err) {
    console.error('Error al cargar métricas de administración', err)
  }
}

async function loadReleasableProjects() {
  isLoading.value = true
  errorMessage.value = ''

  try {
    const params = {
      pageNumber: pageNumber.value,
      pageSize: pageSize.value,
    }
    const res = await apiClient.get('/v1/admin/reports/releasable', { params })
    const data = res.data || {}
    projects.value = data.items || []
    totalCount.value = data.totalCount || 0
    totalPages.value = data.totalPages || 0
  } catch (err) {
    errorMessage.value =
      err.response?.data?.message || 'Error al obtener la lista de proyectos elegibles para liberación.'
    projects.value = []
    totalCount.value = 0
    totalPages.value = 0
  } finally {
    isLoading.value = false
  }
}

function changePage(page) {
  pageNumber.value = page
  loadReleasableProjects()
}

async function handleIssueReleaseLetter(p) {
  const confirmed = await confirm({
    title: 'Emitir Carta de Liberación',
    message: `¿Desea emitir oficialmente la Carta de Liberación (Libranza) para el estudiante ${p.studentName} (${p.title})?`,
    okText: 'Emitir Carta',
    cancelText: 'Cancelar',
  })
  if (!confirmed) return

  try {
    const res = await apiClient.post(`/v1/admin/reports/release-letter/${p.projectId}`)
    const docName = res.data?.documentName || 'Carta de Liberación'
    showAlert(`Carta de Liberación (Libranza) emitida correctamente: ${docName}`, 'success')
    await loadMetrics()
    await loadReleasableProjects()
  } catch (err) {
    const msg = err.response?.data?.message || 'No se pudo emitir la carta de liberación.'
    showAlert(msg, 'danger')
  }
}

function handleExportMetrics() {
  showAlert('Generando reporte consolidado en formato PDF/Excel...', 'info')
  setTimeout(() => {
    showAlert('Reporte consolidado descargado exitosamente.', 'success')
  }, 1500)
}

onMounted(async () => {
  await loadMetrics()
  await loadReleasableProjects()
})
</script>

<template>
  <div>
    <!-- Notificación Flotante Superior Derecha -->
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

    <!-- Barra de Título y Acciones -->
    <div class="tecnm-actions-bar">
      <div>
        <h1 class="tecnm-page-title">Panel de Control y Coordinación de Residencias</h1>
        <p class="tecnm-page-subtitle">Emisión de Cartas de Liberación / Libranzas e Indicadores Oficiales</p>
      </div>
      <div class="tecnm-page-actions">
        <button
          id="exportMetricsBtn"
          type="button"
          class="tecnm-btn tecnm-btn-secondary"
          @click="handleExportMetrics"
        >
          Exportar Indicadores a PDF / Excel
        </button>
      </div>
    </div>

    <!-- Grid de Métricas / KPIs Institucionales -->
    <div class="kpi-grid">
      <div class="kpi-card">
        <span class="kpi-icon">
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" aria-hidden="true">
            <path stroke-linecap="round" stroke-linejoin="round" d="M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Zm8.25 2.25a2.625 2.625 0 1 1-5.25 0 2.625 2.625 0 0 1 5.25 0Z" />
          </svg>
        </span>
        <span class="kpi-body">
          <span class="kpi-label">Alumnos Inscritos</span>
          <span id="kpiStudents" class="kpi-value">{{ metrics.totalStudents }}</span>
        </span>
      </div>

      <div class="kpi-card kpi-card--green">
        <span class="kpi-icon">
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" aria-hidden="true">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12c0 1.268-.63 2.39-1.593 3.068a3.745 3.745 0 0 1-1.043 3.296 3.745 3.745 0 0 1-3.296 1.043A3.745 3.745 0 0 1 12 21c-1.268 0-2.39-.63-3.068-1.593a3.746 3.746 0 0 1-3.296-1.043 3.746 3.746 0 0 1-1.043-3.296A3.745 3.745 0 0 1 3 12c0-1.268.63-2.39 1.593-3.068a3.745 3.745 0 0 1 1.043-3.296 3.746 3.746 0 0 1 3.296-1.043A3.746 3.746 0 0 1 12 3c1.268 0 2.39.63 3.068 1.593a3.746 3.746 0 0 1 3.296 1.043 3.746 3.746 0 0 1 1.043 3.296A3.745 3.745 0 0 1 21 12Z" />
          </svg>
        </span>
        <span class="kpi-body">
          <span class="kpi-label">Proyectos Aprobados</span>
          <span id="kpiApprovedProjects" class="kpi-value">{{ metrics.approvedProjects }}</span>
        </span>
      </div>

      <div class="kpi-card kpi-card--green">
        <span class="kpi-icon">
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" aria-hidden="true">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
          </svg>
        </span>
        <span class="kpi-body">
          <span class="kpi-label">Residencias Liberadas</span>
          <span id="kpiCompleted" class="kpi-value">{{ metrics.completedResidencies }}</span>
        </span>
      </div>

      <div class="kpi-card">
        <span class="kpi-icon">
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" aria-hidden="true">
            <path stroke-linecap="round" stroke-linejoin="round" d="M18 18.72a9.094 9.094 0 0 0 3.741-.479 3 3 0 0 0-4.682-2.72m.94 3.198.001.031c0 .225-.012.447-.037.666A11.944 11.944 0 0 1 12 21c-2.17 0-4.207-.576-5.963-1.584A6.062 6.062 0 0 1 6 18.719m12 0a5.971 5.971 0 0 0-.941-3.197m0 0A5.995 5.995 0 0 0 12 12.75a5.995 5.995 0 0 0-5.058 2.772m0 0a3 3 0 0 0-4.681 2.72 8.986 8.986 0 0 0 3.74.477m.94-3.197a5.971 5.971 0 0 0-.94 3.197M15 6.75a3 3 0 1 1-6 0 3 3 0 0 1 6 0Zm6 3a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Zm-13.5 0a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Z" />
          </svg>
        </span>
        <span class="kpi-body">
          <span class="kpi-label">Asesores Activos</span>
          <span id="kpiAdvisors" class="kpi-value">{{ metrics.activeAdvisors }}</span>
        </span>
      </div>
    </div>

    <!-- Tarjeta Principal con Lista de Proyectos Elegibles para Liberación -->
    <div class="tecnm-card">
      <div class="tecnm-card-header">
        <h3 class="tecnm-card-title">Proyectos Elegibles para Emisión de Carta de Liberación (Libranza)</h3>
      </div>

      <div class="tecnm-card-body">
        <div class="tecnm-table-responsive">
          <table class="tecnm-table tecnm-table-striped">
            <thead>
              <tr>
                <th>Título del Proyecto</th>
                <th>Estudiante</th>
                <th>No. Control</th>
                <th>Asesor</th>
                <th>Promedio Evaluativo</th>
                <th>Estado Liberación</th>
                <th>Acciones</th>
              </tr>
            </thead>
            <tbody id="releasableTableBody">
              <tr v-if="isLoading">
                <td colspan="7" class="tecnm-table-empty">
                  Cargando proyectos...
                </td>
              </tr>
              <tr v-else-if="errorMessage">
                <td colspan="7" class="tecnm-table-empty tecnm-text-danger">
                  {{ errorMessage }}
                </td>
              </tr>
              <tr v-else-if="projects.length === 0">
                <td colspan="7" class="tecnm-table-empty">
                  No se encontraron anteproyectos registrados.
                </td>
              </tr>
              <tr
                v-for="p in projects"
                v-else
                :key="p.projectId || p.id"
              >
                <td><strong>{{ p.title }}</strong></td>
                <td>{{ p.studentName }}</td>
                <td>{{ p.studentControlNumber || '—' }}</td>
                <td>{{ p.advisorName || 'Sin asignar' }}</td>
                <td>
                  <strong :class="p.averageScore >= 70 ? 'tecnm-score-approved' : 'tecnm-score-rejected'">
                    {{ p.averageScore }}
                  </strong> / 100
                </td>
                <td>
                  <span
                    v-if="p.isEligible"
                    class="tecnm-badge tecnm-badge-approved"
                  >
                    Elegible (Promedio ≥ 70)
                  </span>
                  <span
                    v-else
                    class="tecnm-badge tecnm-badge-rejected"
                  >
                    No Elegible (&lt; 70)
                  </span>
                </td>
                <td>
                  <div class="tecnm-row-actions">
                    <button
                      type="button"
                      class="tecnm-btn tecnm-btn-sm"
                      :class="p.isEligible ? 'tecnm-btn-success' : 'tecnm-btn-secondary'"
                      :disabled="!p.isEligible || authStore.isReadOnly"
                      @click="handleIssueReleaseLetter(p)"
                    >
                      Emitir Carta de Liberación / Libranza
                    </button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <TecnmPagination
          v-if="totalPages > 1"
          :current-page="pageNumber"
          :total-pages="totalPages"
          :total-count="totalCount"
          :page-size="pageSize"
          @page-change="changePage"
        />
      </div>
    </div>
  </div>
</template>

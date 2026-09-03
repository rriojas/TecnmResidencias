<script setup>
import { ref, computed, onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import apiClient from '@/services/api'
import SupervisionNoteModal from '@/components/evaluations/SupervisionNoteModal.vue'
import TecnmAutocomplete from '@/components/common/TecnmAutocomplete.vue'

const authStore = useAuthStore()

const isLoading = ref(false)
const errorMessage = ref('')
const alertMessage = ref('')
const alertType = ref('success')

const summary = ref({
  totalAdvisors: 0,
  healthyCount: 0,
  warningCount: 0,
  criticalCount: 0,
  irregularCount: 0,
  totalSessions: 0,
  observedSessionsCount: 0,
  advisorHealthMetrics: []
})

const searchQuery = ref('')
const selectedAutocompleteId = ref(null)
const selectedAutocompleteItem = ref(null)
// quickFilter: 'all' | 'attention' | 'healthy' | 'warning' | 'critical' | 'irregular'
const activeQuickFilter = ref('all')
const expandedAdvisors = ref(new Set())

// Modales
const isNoteModalOpen = ref(false)
const selectedSessionForNote = ref(null)
const isSubmittingNote = ref(false)

const activeSessionDetail = ref(null)
const activeStudentHistory = ref(null)

const canSupervise = computed(() => {
  return authStore.isAdmin || authStore.isCareerHead || authStore.hasRole('departmenthead', 'academic')
})

const MONTH_NAMES_ES = [
  'Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun',
  'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'
]

function formatShortDate(iso) {
  if (!iso) return '—'
  const d = new Date(iso)
  if (isNaN(d.getTime())) return '—'
  const day = String(d.getDate()).padStart(2, '0')
  const month = MONTH_NAMES_ES[d.getMonth()]
  return `${day}/${month}`
}

function formatFullDate(iso) {
  if (!iso) return '—'
  const d = new Date(iso)
  if (isNaN(d.getTime())) return '—'
  const day = String(d.getDate()).padStart(2, '0')
  const month = [
    'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
    'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'
  ][d.getMonth()]
  const year = d.getFullYear()
  return `${day}/${month}/${year}`
}

function calculateDaysBetween(date1, date2) {
  const d1 = new Date(date1)
  const d2 = new Date(date2)
  const diffTime = Math.abs(d2 - d1)
  return Math.round(diffTime / (1000 * 60 * 60 * 24))
}

function formatGapLabel(days) {
  if (days === null || days === undefined) return '—'
  if (days < 0) return `En ${Math.abs(days)} d`
  if (days === 0) return 'Hoy'
  if (days === 1) return '1 d'
  return `${days} d`
}

function formatCurrentStatusPill(days, totalSessions) {
  if (totalSessions === 0) return 'Sin asesorías registradas'
  if (days === null || days === undefined) return 'Sin registro'
  if (days < 0) return `Programada (en ${Math.abs(days)} d)`
  if (days === 0) return 'Sesión realizada hoy'
  if (days === 1) return 'Última sesión: ayer'
  return `Última sesión: hace ${days} d`
}

function showAlert(msg, type = 'success') {
  alertMessage.value = msg
  alertType.value = type
  setTimeout(() => {
    if (alertMessage.value === msg) {
      alertMessage.value = ''
    }
  }, 4500)
}

async function loadData() {
  isLoading.value = true
  errorMessage.value = ''
  try {
    const params = {}
    if (authStore.isCareerHead && authStore.userCareerId) {
      params.careerId = authStore.userCareerId
    }
    const res = await apiClient.get('/v1/evaluations/timeline/health', { params })
    summary.value = res.data || {
      totalAdvisors: 0,
      healthyCount: 0,
      warningCount: 0,
      criticalCount: 0,
      irregularCount: 0,
      totalSessions: 0,
      observedSessionsCount: 0,
      advisorHealthMetrics: []
    }

    // Prioridad UX: Expandir por defecto únicamente asesores con alertas o inactividad
    const newExpanded = new Set()
    summary.value.advisorHealthMetrics.forEach((adv) => {
      if (adv.healthStatus === 'critical' || adv.healthStatus === 'warning') {
        newExpanded.add(adv.advisorId)
      }
    })
    // Si no hay ninguno con alerta, expandir el primero
    if (newExpanded.size === 0 && summary.value.advisorHealthMetrics.length > 0) {
      newExpanded.add(summary.value.advisorHealthMetrics[0].advisorId)
    }
    expandedAdvisors.value = newExpanded
  } catch (err) {
    errorMessage.value = err.response?.data?.message || 'Error al cargar el seguimiento de asesores.'
  } finally {
    isLoading.value = false
  }
}

// Items estructurados para el Autocomplete inteligente
const autocompleteItems = computed(() => {
  const items = []
  const metrics = summary.value.advisorHealthMetrics || []

  for (const adv of metrics) {
    items.push({
      id: `adv-${adv.advisorId}`,
      type: 'advisor',
      advisorId: adv.advisorId,
      advisorName: adv.advisorName,
      title: `${adv.advisorTitle ? adv.advisorTitle + ' ' : ''}${adv.advisorName}`,
      subtitle: `Docente Asesor • ${adv.totalAssignedResidents} alumno(s) • ${adv.totalSessions} asesoría(s) • ${adv.departmentName || ''}`,
      raw: adv
    })

    for (const st of (adv.students || [])) {
      items.push({
        id: `st-${st.studentId}`,
        type: 'student',
        advisorId: adv.advisorId,
        studentId: st.studentId,
        studentName: st.studentName,
        studentControlNumber: st.studentControlNumber,
        title: st.studentName,
        subtitle: `No. Control: ${st.studentControlNumber} • Asesor: ${adv.advisorName} • Proyecto: ${st.projectTitle || '—'}`,
        raw: st,
        advisor: adv
      })
    }
  }

  return items
})

function handleAutocompleteSelect(item) {
  selectedAutocompleteItem.value = item
  selectedAutocompleteId.value = item.id
  if (item.type === 'advisor') {
    searchQuery.value = item.advisorName
    expandedAdvisors.value.add(item.advisorId)
  } else if (item.type === 'student') {
    searchQuery.value = item.studentName
    expandedAdvisors.value.add(item.advisorId)
  }
}

function handleAutocompleteClear() {
  selectedAutocompleteItem.value = null
  selectedAutocompleteId.value = null
  searchQuery.value = ''
}

function handleAutocompleteQueryChange(q) {
  if (!selectedAutocompleteItem.value) {
    searchQuery.value = q
  }
}

function setQuickFilter(filter) {
  activeQuickFilter.value = activeQuickFilter.value === filter ? 'all' : filter
  if (selectedAutocompleteItem.value) {
    handleAutocompleteClear()
  }
}

// Filtro y ordenamiento prioritario
const filteredAdvisors = computed(() => {
  let list = [...(summary.value.advisorHealthMetrics || [])]

  // Si se seleccionó un item puntual del autocomplete:
  if (selectedAutocompleteItem.value) {
    if (selectedAutocompleteItem.value.type === 'advisor') {
      list = list.filter((adv) => adv.advisorId === selectedAutocompleteItem.value.advisorId)
    } else if (selectedAutocompleteItem.value.type === 'student') {
      list = list
        .filter((adv) => adv.advisorId === selectedAutocompleteItem.value.advisorId)
        .map((adv) => ({
          ...adv,
          students: adv.students.filter(
            (st) => st.studentId === selectedAutocompleteItem.value.studentId
          )
        }))
    }
    return list
  }

  // Filtro Rápido
  if (activeQuickFilter.value === 'attention') {
    list = list.filter((adv) => adv.healthStatus === 'critical' || adv.healthStatus === 'warning')
  } else if (activeQuickFilter.value === 'healthy') {
    list = list.filter((adv) => adv.healthStatus === 'healthy')
  } else if (activeQuickFilter.value === 'critical') {
    list = list.filter((adv) => adv.healthStatus === 'critical')
  } else if (activeQuickFilter.value === 'warning') {
    list = list.filter((adv) => adv.healthStatus === 'warning')
  } else if (activeQuickFilter.value === 'irregular') {
    list = list.filter((adv) => adv.healthStatus === 'irregular')
  }

  // Búsqueda en vivo al escribir en el autocomplete (mientras no se haya fijado un item)
  const q = searchQuery.value.trim().toLowerCase()
  if (q) {
    list = list.filter((adv) => {
      const matchAdv = (adv.advisorName || '').toLowerCase().includes(q)
      const matchStudent = (adv.students || []).some(
        (st) =>
          (st.studentName || '').toLowerCase().includes(q) ||
          (st.studentControlNumber || '').toLowerCase().includes(q) ||
          (st.projectTitle || '').toLowerCase().includes(q)
      )
      return matchAdv || matchStudent
    })
  }

  // Ordenamiento prioritario: 🔴 Críticos primero, luego 🟡 Advertencias, luego 🟣 Atípicos, y finalmente 🟢 Al día
  list.sort((a, b) => {
    const score = (status) => {
      if (status === 'critical') return 1
      if (status === 'warning') return 2
      if (status === 'irregular') return 3
      return 4
    }
    const diff = score(a.healthStatus) - score(b.healthStatus)
    if (diff !== 0) return diff
    return (b.daysWithoutActivity || 0) - (a.daysWithoutActivity || 0)
  })

  return list
})

const attentionCount = computed(() => {
  return summary.value.criticalCount + summary.value.warningCount
})

function toggleAdvisor(advisorId) {
  if (expandedAdvisors.value.has(advisorId)) {
    expandedAdvisors.value.delete(advisorId)
  } else {
    expandedAdvisors.value.add(advisorId)
  }
}

function expandAll() {
  const allIds = (summary.value.advisorHealthMetrics || []).map((a) => a.advisorId)
  expandedAdvisors.value = new Set(allIds)
}

function collapseAll() {
  expandedAdvisors.value = new Set()
}

function openSessionDetail(session, student, advisor) {
  activeSessionDetail.value = {
    ...session,
    studentName: student.studentName,
    studentControlNumber: student.studentControlNumber,
    projectTitle: student.projectTitle,
    advisorName: advisor.advisorName
  }
}

function closeSessionDetail() {
  activeSessionDetail.value = null
}

function openStudentHistory(student, advisor) {
  activeStudentHistory.value = {
    student,
    advisor
  }
}

function closeStudentHistory() {
  activeStudentHistory.value = null
}

function openNoteModalForSession(session) {
  selectedSessionForNote.value = session
  isNoteModalOpen.value = true
}

function closeNoteModal() {
  isNoteModalOpen.value = false
  selectedSessionForNote.value = null
}

async function handleSubmitNote({ sessionId, notes }) {
  isSubmittingNote.value = true
  try {
    await apiClient.patch(`/v1/evaluations/sessions/${sessionId}/supervision-note`, {
      notes
    })
    const msg = notes
      ? '¡Nota de supervisión guardada exitosamente!'
      : 'Nota de supervisión eliminada.'
    showAlert(msg, 'success')
    closeNoteModal()
    if (activeSessionDetail.value && activeSessionDetail.value.id === sessionId) {
      activeSessionDetail.value.supervisionNotes = notes
      activeSessionDetail.value.hasSupervisionNote = Boolean(notes)
    }
    await loadData()
  } catch (err) {
    const msg = err.response?.data?.message || 'Error al guardar la nota de supervisión.'
    showAlert(msg, 'danger')
  } finally {
    isSubmittingNote.value = false
  }
}

const isExportingPdf = ref(false)

async function handleExportPdf() {
  isExportingPdf.value = true
  try {
    const params = {}
    if (authStore.isCareerHead && authStore.userCareerId) {
      params.careerId = authStore.userCareerId
    }
    const res = await apiClient.get('/v1/evaluations/timeline/export', {
      params,
      responseType: 'blob'
    })
    const blob = new Blob([res.data], { type: 'application/pdf' })
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.setAttribute('download', `reporte_supervision_asesorias_${new Date().toISOString().slice(0, 10)}.pdf`)
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    window.URL.revokeObjectURL(url)
    showAlert('Reporte PDF descargado exitosamente.', 'success')
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al exportar el reporte en PDF.', 'danger')
  } finally {
    isExportingPdf.value = false
  }
}

onMounted(() => {
  loadData()
})
</script>

<template>
  <div>
    <!-- Notificación Flotante -->
    <div
      v-if="alertMessage"
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

    <!-- 1. Widgets Semafóricos Ejecutivos -->
    <div class="tecnm-semaphore-grid">
      <!-- Al Día -->
      <div
        class="tecnm-semaphore-card tecnm-semaphore-healthy"
        :class="{ active: activeQuickFilter === 'healthy' }"
        role="button"
        tabindex="0"
        @click="setQuickFilter('healthy')"
      >
        <div class="tecnm-semaphore-header">
          <span class="tecnm-semaphore-title">Al Día (&le; 14 días)</span>
          <div class="tecnm-semaphore-icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
              <path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5" />
            </svg>
          </div>
        </div>
        <div class="tecnm-semaphore-count">{{ summary.healthyCount }}</div>
        <div class="tecnm-semaphore-desc">Asesores con avance y reuniones constantes</div>
      </div>

      <!-- En Alerta -->
      <div
        class="tecnm-semaphore-card tecnm-semaphore-warning"
        :class="{ active: activeQuickFilter === 'warning' }"
        role="button"
        tabindex="0"
        @click="setQuickFilter('warning')"
      >
        <div class="tecnm-semaphore-header">
          <span class="tecnm-semaphore-title">En Alerta (15-21 días)</span>
          <div class="tecnm-semaphore-icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m9-.75a9 9 0 1 1-18 0 9 9 0 0 1 18 0Zm-9 3.75h.008v.008H12v-.008Z" />
            </svg>
          </div>
        </div>
        <div class="tecnm-semaphore-count">{{ summary.warningCount }}</div>
        <div class="tecnm-semaphore-desc">Riesgo de retraso; requieren recordatorio</div>
      </div>

      <!-- Inactividad Crítica -->
      <div
        class="tecnm-semaphore-card tecnm-semaphore-critical"
        :class="{ active: activeQuickFilter === 'critical' }"
        role="button"
        tabindex="0"
        @click="setQuickFilter('critical')"
      >
        <div class="tecnm-semaphore-header">
          <span class="tecnm-semaphore-title">Inactividad Crítica (&gt; 21 días)</span>
          <div class="tecnm-semaphore-icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126ZM12 15.75h.007v.008H12v-.008Z" />
            </svg>
          </div>
        </div>
        <div class="tecnm-semaphore-count">{{ summary.criticalCount }}</div>
        <div class="tecnm-semaphore-desc">Sin reuniones recientes o 0 registradas</div>
      </div>

      <!-- Carga Irregular -->
      <div
        class="tecnm-semaphore-card tecnm-semaphore-irregular"
        :class="{ active: activeQuickFilter === 'irregular' }"
        role="button"
        tabindex="0"
        @click="setQuickFilter('irregular')"
      >
        <div class="tecnm-semaphore-header">
          <span class="tecnm-semaphore-title">Seguimiento Atípico</span>
          <div class="tecnm-semaphore-icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 12c0-1.232-.046-2.453-.138-3.662a4.006 4.006 0 0 0-3.7-3.7 48.678 48.678 0 0 0-7.324 0 4.006 4.006 0 0 0-3.7 3.7c-.017.22-.032.441-.046.662M19.5 12l3-3m-3 3-3-3m-12 3c0 1.232.046 2.453.138 3.662a4.006 4.006 0 0 0 3.7 3.7 48.656 48.656 0 0 0 7.324 0 4.006 4.006 0 0 0 3.7-3.7c.017-.22.032-.441.046-.662M4.5 12l3 3m-3-3-3 3" />
            </svg>
          </div>
        </div>
        <div class="tecnm-semaphore-count">{{ summary.irregularCount }}</div>
        <div class="tecnm-semaphore-desc">Carga concentrada en un solo día</div>
      </div>
    </div>

    <!-- 2. Barra de Herramientas y Filtros Inteligentes -->
    <div class="tecnm-card tecnm-mb-4" style="margin-bottom: 1.25rem;">
      <div class="tecnm-card-body" style="padding: 1rem 1.25rem;">
        <div style="display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 1rem;">
          <!-- Búsqueda con Autocomplete -->
          <div style="flex: 1; min-width: 280px; max-width: 440px;">
            <TecnmAutocomplete
              v-model="selectedAutocompleteId"
              :items="autocompleteItems"
              placeholder="Buscar asesor, estudiante o no. control..."
              :min-chars="1"
              :title-extractor="(item) => item.title"
              :subtitle-extractor="(item) => item.subtitle"
              @select="handleAutocompleteSelect"
              @clear="handleAutocompleteClear"
              @query-change="handleAutocompleteQueryChange"
            />
          </div>

          <!-- Filtros Rápidos (Pills) -->
          <div class="tecnm-filter-pills">
            <button
              type="button"
              class="tecnm-filter-pill"
              :class="{ active: activeQuickFilter === 'all' && !selectedAutocompleteItem }"
              @click="setQuickFilter('all')"
            >
              <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Zm8.25 2.25a2.625 2.625 0 1 1-5.25 0 2.625 2.625 0 0 1 5.25 0Z" />
              </svg>
              <span>Todos ({{ summary.totalAdvisors }})</span>
            </button>
            <button
              type="button"
              class="tecnm-filter-pill alert-filter"
              :class="{ active: activeQuickFilter === 'attention' }"
              @click="setQuickFilter('attention')"
            >
              <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126ZM12 15.75h.007v.008H12v-.008Z" />
              </svg>
              <span>Requieren Atención ({{ attentionCount }})</span>
            </button>
            <button
              type="button"
              class="tecnm-filter-pill healthy"
              :class="{ active: activeQuickFilter === 'healthy' }"
              @click="setQuickFilter('healthy')"
            >
              <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
                <path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5" />
              </svg>
              <span>Al Corriente ({{ summary.healthyCount }})</span>
            </button>
          </div>

          <!-- Acciones de Visualización y Reporte -->
          <div style="display: flex; gap: 0.5rem; align-items: center; flex-wrap: wrap;">
            <button
              type="button"
              class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
              title="Expandir todos los docentes"
              @click="expandAll"
            >
              Expandir
            </button>
            <button
              type="button"
              class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
              title="Colapsar todos los docentes"
              @click="collapseAll"
            >
              Colapsar
            </button>
            <button
              type="button"
              class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
              :disabled="isExportingPdf"
              title="Descargar reporte oficial en PDF"
              @click="handleExportPdf"
            >
              <svg v-if="!isExportingPdf" xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M3 16.5v2.25A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75V16.5M16.5 12 12 16.5m0 0L7.5 12m4.5 4.5V3" />
              </svg>
              <svg v-else class="tecnm-animate-spin" style="animation: spin 1s linear infinite;" xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
              </svg>
              <span>{{ isExportingPdf ? 'Generando...' : 'Reporte PDF' }}</span>
            </button>
            <button
              type="button"
              class="tecnm-btn tecnm-btn-primary tecnm-btn-sm"
              :disabled="isLoading"
              title="Recargar datos"
              @click="loadData"
            >
              <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0 3.181 3.183a8.25 8.25 0 0 0 13.803-3.7M4.031 9.865a8.25 8.25 0 0 1 13.803-3.7l3.181 3.182m0-4.991v4.99" />
              </svg>
              <span>Actualizar</span>
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- 3. Estado de Carga / Error -->
    <div v-if="isLoading" style="padding: 3rem; text-align: center; color: #64748B;">
      <svg class="tecnm-animate-spin" style="animation: spin 1s linear infinite; display: inline-block; margin-bottom: 0.75rem;" xmlns="http://www.w3.org/2000/svg" width="28" height="28" fill="none" viewBox="0 0 24 24">
        <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
        <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
      </svg>
      <div>Consultando líneas de tiempo de asesorías por docente...</div>
    </div>

    <div v-else-if="errorMessage" class="tecnm-alert tecnm-alert-danger" role="alert">
      <span>{{ errorMessage }}</span>
    </div>

    <div v-else-if="filteredAdvisors.length === 0" style="padding: 3.5rem 1.5rem; text-align: center;">
      <div style="max-width: 440px; margin: 0 auto; display: flex; flex-direction: column; align-items: center; gap: 0.75rem;">
        <div style="width: 56px; height: 56px; border-radius: 50%; background: #EBF3FF; color: var(--tecnm-blue-primary, #1B396A); display: flex; align-items: center; justify-content: center;">
          <svg xmlns="http://www.w3.org/2000/svg" width="28" height="28" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.8">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
          </svg>
        </div>
        <h4 style="margin: 0; font-weight: 700; color: #1E293B;">No se encontraron docentes</h4>
        <p style="margin: 0; font-size: 0.875rem; color: #64748B;">
          No hay asesores que coincidan con los filtros aplicados.
        </p>
      </div>
    </div>

    <!-- 4. Lista Priorizada de Asesores (Críticos primero) -->
    <div v-else>
      <div
        v-for="adv in filteredAdvisors"
        :key="adv.advisorId"
        class="tecnm-advisor-card"
        :class="adv.healthStatus"
      >
        <!-- Cabecera del Asesor -->
        <div
          class="tecnm-advisor-header"
          @click="toggleAdvisor(adv.advisorId)"
        >
          <div class="tecnm-advisor-info">
            <div class="tecnm-advisor-avatar">
              {{ (adv.advisorName || 'AS').slice(0, 2).toUpperCase() }}
            </div>
            <div>
              <div class="tecnm-advisor-name">
                {{ adv.advisorTitle ? `${adv.advisorTitle} ` : '' }}{{ adv.advisorName }}
              </div>
              <div class="tecnm-advisor-subtext">
                {{ adv.advisorEmail || 'Docente TecNM' }} • {{ adv.departmentName }}
              </div>
            </div>
          </div>

          <div class="tecnm-advisor-kpis">
            <span class="tecnm-badge tecnm-badge-neutral">
              {{ adv.totalAssignedResidents }} alumno(s)
            </span>
            <span class="tecnm-badge tecnm-badge-neutral">
              {{ adv.totalSessions }} asesoría(s)
            </span>

            <!-- Badge de Salud -->
            <span
              v-if="adv.healthStatus === 'healthy'"
              class="tecnm-track-status-pill healthy"
            >
              <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
                <path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5" />
              </svg>
              <span>Al Día (&le; 14 d)</span>
            </span>
            <span
              v-else-if="adv.healthStatus === 'warning'"
              class="tecnm-track-status-pill warning"
            >
              <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126ZM12 15.75h.007v.008H12v-.008Z" />
              </svg>
              <span>En Alerta ({{ adv.daysWithoutActivity }} días sin sesión)</span>
            </span>
            <span
              v-else-if="adv.healthStatus === 'critical'"
              class="tecnm-track-status-pill critical"
            >
              <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
              </svg>
              <span>Inactividad Crítica ({{ adv.daysWithoutActivity }} días)</span>
            </span>
            <span
              v-else-if="adv.healthStatus === 'irregular'"
              class="tecnm-track-status-pill"
              style="background-color: #EDE9FE; color: #5B21B6; border: 1px solid #DDD6FE;"
            >
              <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 12c0-1.232-.046-2.453-.138-3.662a4.006 4.006 0 0 0-3.7-3.7 48.678 48.678 0 0 0-7.324 0 4.006 4.006 0 0 0-3.7 3.7c-.017.22-.032.441-.046.662M19.5 12l3-3m-3 3-3-3m-12 3c0 1.232.046 2.453.138 3.662a4.006 4.006 0 0 0 3.7 3.7 48.656 48.656 0 0 0 7.324 0 4.006 4.006 0 0 0 3.7-3.7c.017-.22.032-.441.046-.662M4.5 12l3 3m-3-3-3 3" />
              </svg>
              <span>Seguimiento Atípico</span>
            </span>

            <!-- Chevron -->
            <svg
              xmlns="http://www.w3.org/2000/svg"
              width="20"
              height="20"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              stroke-width="2"
              :style="{
                transform: expandedAdvisors.has(adv.advisorId) ? 'rotate(180deg)' : 'rotate(0deg)',
                transition: 'transform 0.2s ease',
                color: '#64748B'
              }"
            >
              <path stroke-linecap="round" stroke-linejoin="round" d="m19.5 8.25-7.5 7.5-7.5-7.5" />
            </svg>
          </div>
        </div>

        <!-- Cuerpo del Asesor: Lista de Alumnos con su Línea de Tiempo -->
        <div v-show="expandedAdvisors.has(adv.advisorId)" class="tecnm-advisor-body">
          <div v-if="adv.students.length === 0" style="padding: 1rem; text-align: center; color: #94A3B8; font-size: 0.85rem;">
            No tiene residentes con anteproyecto aprobado en curso actualmente.
          </div>

          <div
            v-for="st in adv.students"
            :key="st.studentId"
            class="tecnm-student-timeline-box"
          >
            <!-- Meta del Alumno y Acciones -->
            <div class="tecnm-student-meta">
              <div>
                <div class="tecnm-student-name" style="display: flex; align-items: center; gap: 0.4rem;">
                  <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.8" style="color: var(--tecnm-blue-primary, #1B396A); flex-shrink: 0;">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M4.26 10.147a60.438 60.438 0 0 0-.491 6.347A48.62 48.62 0 0 1 12 20.904a48.62 48.62 0 0 1 8.232-4.41 60.46 60.46 0 0 0-.491-6.347m-15.482 0a50.636 50.636 0 0 0-2.658-.813A59.906 59.906 0 0 1 12 3.493a59.903 59.903 0 0 1 10.399 5.84c-.896.248-1.783.52-2.658.814m-15.482 0A50.717 50.717 0 0 1 12 13.489a50.702 50.702 0 0 1 7.74-3.342M6.75 15a.75.75 0 1 0 0-1.5.75.75 0 0 0 0 1.5Zm0 0v-3.675A55.378 55.378 0 0 1 12 8.443m-5.25 6.557c1.398-1.42 3.435-2.25 5.25-2.25" />
                  </svg>
                  <span>{{ st.studentName }}</span>
                  <span style="font-weight: 400; font-size: 0.8rem; color: #64748B;">
                    (Ctrl: {{ st.studentControlNumber }})
                  </span>
                </div>
                <div class="tecnm-student-project">
                  <strong>Proyecto:</strong> {{ st.projectTitle }}
                </div>
              </div>

              <!-- Estado de Seguimiento y Botón Ver Histórico -->
              <div style="display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap;">
                <span
                  v-if="st.healthStatus === 'healthy'"
                  class="tecnm-track-status-pill healthy"
                >
                  <svg xmlns="http://www.w3.org/2000/svg" width="11" height="11" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
                    <path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5" />
                  </svg>
                  <span>{{ st.alertMessage }}</span>
                </span>
                <span
                  v-else-if="st.healthStatus === 'warning'"
                  class="tecnm-track-status-pill warning"
                >
                  <svg xmlns="http://www.w3.org/2000/svg" width="11" height="11" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126ZM12 15.75h.007v.008H12v-.008Z" />
                  </svg>
                  <span>{{ st.alertMessage }}</span>
                </span>
                <span
                  v-else
                  class="tecnm-track-status-pill critical"
                >
                  <svg xmlns="http://www.w3.org/2000/svg" width="11" height="11" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126ZM12 15.75h.007v.008H12v-.008Z" />
                  </svg>
                  <span>{{ st.alertMessage }}</span>
                </span>

                <button
                  type="button"
                  class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                  style="font-size: 0.75rem; padding: 0.25rem 0.6rem; gap: 0.35rem;"
                  title="Ver bitácora completa de este alumno"
                  @click="openStudentHistory(st, adv)"
                >
                  <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z" />
                  </svg>
                  <span>Ver Historial</span>
                </button>
              </div>
            </div>

            <!-- Línea de Tiempo Horizontal (Timeline Track) -->
            <div class="tecnm-track-container">
              <div v-if="st.sessions.length === 0" style="padding: 0.6rem 0.75rem; background: #FEF2F2; border: 1px dashed #F87171; border-radius: 4px; color: #991B1B; font-size: 0.8rem; display: flex; align-items: center; gap: 0.5rem;">
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126ZM12 15.75h.007v.008H12v-.008Z" />
                </svg>
                <span><strong>Sin asesorías registradas:</strong> El asesor docente aún no reporta ninguna reunión de acompañamiento con este alumno.</span>
              </div>

              <div v-else class="tecnm-track">
                <!-- Iterar sobre los nodos de sesión -->
                <div
                  v-for="(session, sIndex) in st.sessions"
                  :key="session.id"
                  class="tecnm-track-node-wrapper"
                >
                  <!-- Conector entre sesiones previas -->
                  <div
                    v-if="sIndex > 0"
                    class="tecnm-track-connector"
                    :class="{
                      warning: calculateDaysBetween(st.sessions[sIndex - 1].sessionDate, session.sessionDate) >= 15 && calculateDaysBetween(st.sessions[sIndex - 1].sessionDate, session.sessionDate) <= 21,
                      critical: calculateDaysBetween(st.sessions[sIndex - 1].sessionDate, session.sessionDate) > 21
                    }"
                  >
                    <span class="tecnm-track-connector-label">
                      {{ calculateDaysBetween(st.sessions[sIndex - 1].sessionDate, session.sessionDate) }} d
                    </span>
                  </div>

                  <!-- Nodo de la Sesión -->
                  <div
                    class="tecnm-track-node"
                    :title="'Clic para ver acuerdos de la Sesión #' + session.sessionNumber"
                    @click="openSessionDetail(session, st, adv)"
                  >
                    <div
                      class="tecnm-track-circle"
                      :class="{ 'has-note': session.hasSupervisionNote }"
                    >
                      #{{ session.sessionNumber }}
                    </div>
                    <span class="tecnm-track-date">
                      {{ formatShortDate(session.sessionDate) }}
                    </span>
                  </div>
                </div>

                <!-- Brecha hasta el día de hoy -->
                <div
                  class="tecnm-track-connector"
                  :class="{
                    warning: st.daysWithoutActivity >= 15 && st.daysWithoutActivity <= 21,
                    critical: st.daysWithoutActivity > 21
                  }"
                  style="min-width: 60px;"
                >
                  <span class="tecnm-track-connector-label">
                    {{ formatGapLabel(st.daysWithoutActivity) }}
                  </span>
                </div>

                <!-- Nodo final de seguimiento actual -->
                <span
                  class="tecnm-track-status-pill"
                  :class="st.healthStatus"
                >
                  {{ formatCurrentStatusPill(st.daysWithoutActivity, st.totalSessions) }}
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 5. Modal de Detalle de Sesión Individual -->
    <div
      v-if="activeSessionDetail"
      class="modal-backdrop active"
      role="dialog"
      aria-modal="true"
    >
      <div class="modal-card" style="max-width: 580px;">
        <div class="tecnm-modal-header">
          <h3 class="tecnm-modal-title">
            Detalle de Asesoría #{{ activeSessionDetail.sessionNumber }}
          </h3>
          <button
            type="button"
            class="tecnm-modal-close"
            aria-label="Cerrar"
            @click="closeSessionDetail"
          >
            &times;
          </button>
        </div>

        <div style="padding: 1.25rem;">
          <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; background: #F8FAFC; border: 1px solid #E2E8F0; border-radius: 6px; padding: 0.75rem; margin-bottom: 1rem;">
            <div>
              <div style="font-size: 0.7rem; font-weight: 700; color: #64748B; text-transform: uppercase;">Alumno</div>
              <div style="font-size: 0.85rem; font-weight: 600; color: #1E293B;">{{ activeSessionDetail.studentName }}</div>
              <div style="font-size: 0.75rem; color: #64748B;">Ctrl: {{ activeSessionDetail.studentControlNumber }}</div>
            </div>
            <div>
              <div style="font-size: 0.7rem; font-weight: 700; color: #64748B; text-transform: uppercase;">Asesor Docente</div>
              <div style="font-size: 0.85rem; font-weight: 600; color: #1E293B;">{{ activeSessionDetail.advisorName }}</div>
              <div style="font-size: 0.75rem; color: #64748B;">Fecha: {{ formatFullDate(activeSessionDetail.sessionDate) }}</div>
            </div>
          </div>

          <div style="margin-bottom: 1rem;">
            <div style="font-size: 0.75rem; font-weight: 700; text-transform: uppercase; color: var(--tecnm-blue-primary, #1B396A); margin-bottom: 0.25rem;">
              Temas y Avances Abordados
            </div>
            <p style="margin: 0; font-size: 0.875rem; color: #1E293B; line-height: 1.5; background: #F1F5F9; padding: 0.75rem; border-radius: 4px;">
              {{ activeSessionDetail.topicsCovered }}
            </p>
          </div>

          <div v-if="activeSessionDetail.studentAgreements" style="margin-bottom: 1rem;">
            <div style="font-size: 0.75rem; font-weight: 700; text-transform: uppercase; color: var(--tecnm-blue-primary, #1B396A); margin-bottom: 0.25rem;">
              Acuerdos y Compromisos del Estudiante
            </div>
            <p style="margin: 0; font-size: 0.85rem; color: #475569; font-style: italic; background: #F8FAFC; padding: 0.75rem; border-radius: 4px;">
              "{{ activeSessionDetail.studentAgreements }}"
            </p>
          </div>

          <!-- Observación de Jefatura si existe -->
          <div v-if="activeSessionDetail.supervisionNotes" class="tecnm-timeline-review-alert" style="margin-bottom: 1rem;">
            <strong>Observación de Jefatura:</strong> {{ activeSessionDetail.supervisionNotes }}
            <div v-if="activeSessionDetail.supervisedAt" style="font-size: 0.7rem; margin-top: 0.25rem; opacity: 0.85;">
              Registrada el {{ formatFullDate(activeSessionDetail.supervisedAt) }}
            </div>
          </div>
        </div>

        <div class="tecnm-modal-footer" style="display: flex; justify-content: space-between; align-items: center;">
          <button
            v-if="canSupervise"
            type="button"
            class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
            @click="openNoteModalForSession(activeSessionDetail)"
          >
            {{ activeSessionDetail.hasSupervisionNote ? 'Editar Nota de Jefatura' : 'Añadir Nota de Jefatura' }}
          </button>
          <div v-else></div>

          <button
            type="button"
            class="tecnm-btn tecnm-btn-secondary"
            @click="closeSessionDetail"
          >
            Cerrar
          </button>
        </div>
      </div>
    </div>

    <!-- 6. Modal de Histórico Completo del Alumno (Drill-Down) -->
    <div
      v-if="activeStudentHistory"
      class="modal-backdrop active"
      role="dialog"
      aria-modal="true"
    >
      <div class="modal-card" style="max-width: 720px;">
        <div class="tecnm-modal-header">
          <h3 class="tecnm-modal-title">
            Bitácora de Asesorías: {{ activeStudentHistory.student.studentName }}
          </h3>
          <button
            type="button"
            class="tecnm-modal-close"
            aria-label="Cerrar"
            @click="closeStudentHistory"
          >
            &times;
          </button>
        </div>

        <div style="padding: 1.25rem; max-height: 65vh; overflow-y: auto;">
          <div style="background: #F8FAFC; border: 1px solid #E2E8F0; border-radius: 6px; padding: 0.75rem 1rem; margin-bottom: 1.25rem;">
            <div style="font-size: 0.85rem; font-weight: 600; color: #1E293B;">
              Proyecto: {{ activeStudentHistory.student.projectTitle }}
            </div>
            <div style="font-size: 0.75rem; color: #64748B; margin-top: 0.25rem;">
              Asesor responsable: {{ activeStudentHistory.advisor.advisorName }} • Total de sesiones: {{ activeStudentHistory.student.sessions.length }}
            </div>
          </div>

          <div v-if="activeStudentHistory.student.sessions.length === 0" style="padding: 2rem; text-align: center; color: #94A3B8;">
            No hay sesiones registradas para este alumno.
          </div>

          <div v-else style="display: flex; flex-direction: column; gap: 1rem;">
            <div
              v-for="s in activeStudentHistory.student.sessions"
              :key="s.id"
              style="border: 1px solid #E2E8F0; border-radius: 6px; padding: 0.85rem; background: #FFFFFF;"
            >
              <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.5rem; border-bottom: 1px solid #F1F5F9; padding-bottom: 0.35rem;">
                <span style="font-weight: 700; color: var(--tecnm-blue-primary, #1B396A); font-size: 0.85rem;">
                  Sesión #{{ s.sessionNumber }} — {{ formatFullDate(s.sessionDate) }}
                </span>
                <span v-if="s.hasSupervisionNote" class="tecnm-badge tecnm-badge-rejected" style="font-size: 0.7rem;">
                  Con Nota de Jefatura
                </span>
              </div>
              <div style="font-size: 0.85rem; color: #1E293B; margin-bottom: 0.5rem;">
                <strong>Temas:</strong> {{ s.topicsCovered }}
              </div>
              <div v-if="s.studentAgreements" style="font-size: 0.8rem; color: #64748B; font-style: italic;">
                <strong>Compromisos:</strong> "{{ s.studentAgreements }}"
              </div>
              <div v-if="s.supervisionNotes" class="tecnm-timeline-review-alert" style="margin-top: 0.5rem;">
                <strong>Observación de Jefatura:</strong> {{ s.supervisionNotes }}
              </div>
            </div>
          </div>
        </div>

        <div class="tecnm-modal-footer">
          <button
            type="button"
            class="tecnm-btn tecnm-btn-secondary"
            @click="closeStudentHistory"
          >
            Cerrar
          </button>
        </div>
      </div>
    </div>

    <!-- 7. Modal de Nota de Supervisión de Jefatura -->
    <SupervisionNoteModal
      :is-open="isNoteModalOpen"
      :session="selectedSessionForNote"
      :is-submitting="isSubmittingNote"
      @close="closeNoteModal"
      @submit="handleSubmitNote"
    />
  </div>
</template>

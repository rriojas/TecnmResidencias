<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import apiClient from '@/services/api'
import TecnmBadge from '@/components/common/TecnmBadge.vue'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

const studentId = computed(() => route.params.id)

const student = ref(null)
const formatData = ref(null)
const deadlineInfo = ref(null)
const isLoading = ref(true)
const isActionSubmitting = ref(false)

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

async function loadData() {
  if (!studentId.value) return
  isLoading.value = true
  try {
    const [studentRes, formatsRes, deadlinesRes] = await Promise.all([
      apiClient.get(`/v1/students/${studentId.value}`).catch(() => ({ data: null })),
      apiClient.get(`/v1/students/${studentId.value}/format-documents`).catch(() => ({ data: null })),
      apiClient.get(`/v1/students/${studentId.value}/document-deadlines`).catch(() => ({ data: null }))
    ])

    student.value = studentRes.data
    formatData.value = formatsRes.data
    deadlineInfo.value = deadlinesRes.data
  } catch (err) {
    console.error('Error al cargar datos del estudiante:', err)
    showAlert('Error al cargar los datos y formatos del residente.', 'danger')
  } finally {
    isLoading.value = false
  }
}

function getFormatDoc(type) {
  if (!formatData.value?.documents) return null
  return (
    formatData.value.documents.find(
      (d) => (d.documentType || '').toLowerCase() === type.toLowerCase() && d.isActive
    ) || null
  )
}

// Acciones de Aprobación y Rechazo
const rejectingDocId = ref(null)
const rejectionReason = ref('')

function startReject(doc) {
  rejectingDocId.value = doc.id
  rejectionReason.value = ''
}

function cancelReject() {
  rejectingDocId.value = null
  rejectionReason.value = ''
}

async function approveDoc(doc) {
  if (!doc?.id) return
  if (!confirm(`¿Confirmas la validación y aprobación de ${doc.fileName}?`)) return
  isActionSubmitting.value = true
  try {
    await apiClient.patch(`/v1/documents/${doc.id}/status`, {
      status: 'approved',
      rejectionReason: null
    })
    showAlert(`Formato "${doc.fileName}" aprobado correctamente.`, 'success')
    await loadData()
  } catch (err) {
    console.error('Error al aprobar formato:', err)
    showAlert(err.response?.data?.message || 'Error al aprobar formato.', 'danger')
  } finally {
    isActionSubmitting.value = false
  }
}

async function submitReject(doc) {
  if (!rejectionReason.value.trim()) {
    showAlert('Debe ingresar las observaciones de rechazo.', 'warning')
    return
  }
  isActionSubmitting.value = true
  try {
    await apiClient.patch(`/v1/documents/${doc.id}/status`, {
      status: 'rejected',
      rejectionReason: rejectionReason.value.trim()
    })
    showAlert(`Formato "${doc.fileName}" devuelto con observaciones.`, 'warning')
    cancelReject()
    await loadData()
  } catch (err) {
    console.error('Error al rechazar formato:', err)
    showAlert(err.response?.data?.message || 'Error al registrar observaciones.', 'danger')
  } finally {
    isActionSubmitting.value = false
  }
}

// Visor Modal de Documento (PDF/Imagen)
const isPreviewModalOpen = ref(false)
const previewDoc = ref(null)
const previewObjectUrl = ref(null)

async function previewDocModal(doc) {
  if (!doc?.id) return
  previewDoc.value = doc
  isPreviewModalOpen.value = true
  if (previewObjectUrl.value) {
    window.URL.revokeObjectURL(previewObjectUrl.value)
    previewObjectUrl.value = null
  }
  try {
    const res = await apiClient.get(`/v1/documents/${doc.id}/download`, { responseType: 'blob' })
    const file = new Blob([res.data], { type: res.headers['content-type'] || 'application/pdf' })
    previewObjectUrl.value = window.URL.createObjectURL(file)
  } catch (err) {
    console.error('Error al generar vista previa:', err)
    showAlert('Error al descargar vista previa del documento.', 'danger')
    closePreviewModal()
  }
}

function closePreviewModal() {
  isPreviewModalOpen.value = false
  if (previewObjectUrl.value) {
    window.URL.revokeObjectURL(previewObjectUrl.value)
    previewObjectUrl.value = null
  }
  previewDoc.value = null
}

async function downloadDoc(doc, fallbackName = 'documento.pdf') {
  if (!doc?.id) return
  try {
    const res = await apiClient.get(`/v1/documents/${doc.id}/download`, { responseType: 'blob' })
    const url = window.URL.createObjectURL(new Blob([res.data]))
    const link = document.createElement('a')
    link.href = url
    link.setAttribute('download', doc.fileName || fallbackName)
    document.body.appendChild(link)
    link.click()
    link.remove()
    window.URL.revokeObjectURL(url)
  } catch (err) {
    console.error('Error al descargar:', err)
    showAlert('Error al descargar el documento.', 'danger')
  }
}

function goBack() {
  router.push('/admin/document-deadlines')
}

onMounted(async () => {
  await loadData()
})

onUnmounted(() => {
  closePreviewModal()
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

    <!-- Barra de Navegación y Título -->
    <div class="tecnm-actions-bar">
      <div>
        <button
          type="button"
          class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm tecnm-mb-2"
          style="display: inline-flex; align-items: center; gap: 0.35rem;"
          @click="goBack"
        >
          <span>&larr; Volver a Fechas Límite</span>
        </button>
        <h1 class="tecnm-page-title">
          Expediente de Formatos Institucionales (29 y 30)
        </h1>
        <p class="tecnm-page-subtitle">
          Supervisión técnica, validación de avances y dictamen de reportes de seguimiento y evaluación
        </p>
      </div>
    </div>

    <div v-if="isLoading" style="text-align: center; padding: 3rem;">
      <div class="tecnm-spinner"></div>
      <p style="margin-top: 0.75rem; color: var(--tecnm-gray-600);">Cargando expediente del residente...</p>
    </div>

    <div v-else-if="!student" class="tecnm-card">
      <div class="tecnm-card-body" style="text-align: center; padding: 2.5rem;">
        <h3>No se encontró el estudiante</h3>
        <p class="tecnm-text-muted">El expediente solicitado no existe o no se encuentra activo.</p>
        <button type="button" class="tecnm-btn tecnm-btn-primary" @click="goBack">
          Volver a la Lista
        </button>
      </div>
    </div>

    <div v-else style="display: flex; flex-direction: column; gap: 1.5rem;">
      <!-- TARJETA 1: DATOS ORGANIZADOS DEL ESTUDIANTE -->
      <div class="tecnm-card" style="border-left: 4px solid var(--tecnm-blue-primary, #1B396A);">
        <div class="tecnm-card-header" style="display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 0.5rem;">
          <div>
            <h3 class="tecnm-card-title" style="margin-bottom: 0.2rem;">
              {{ student.fullName }}
            </h3>
            <span class="tecnm-text-sub" style="font-size: 0.85rem;">
              Número de Control: <strong>{{ student.controlNumber }}</strong> &bull; Correo: {{ student.email }}
            </span>
          </div>
          <div style="display: flex; gap: 0.5rem; align-items: center;">
            <span
              class="tecnm-badge"
              :class="deadlineInfo?.isDocumentBlocked ? 'tecnm-badge-danger' : 'tecnm-badge-outline'"
            >
              {{ deadlineInfo?.isDocumentBlocked ? 'Acceso Restringido' : 'Acceso Normal' }}
            </span>
            <TecnmBadge :status="student.projectStatus || 'active'" />
          </div>
        </div>

        <div class="tecnm-card-body" style="padding: 1.25rem;">
          <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 1.25rem;">
            <div>
              <span class="tecnm-field-label">Carrera Profesional</span>
              <p class="tecnm-field-value" style="font-weight: 600;">
                {{ student.careerName || '—' }}
              </p>
            </div>
            <div>
              <span class="tecnm-field-label">Asesor Interno Asignado</span>
              <p class="tecnm-field-value">
                {{ student.advisorName || 'Sin asignar' }}
              </p>
            </div>
            <div>
              <span class="tecnm-field-label">Proyecto de Residencia</span>
              <p class="tecnm-field-value" style="font-weight: 500;">
                {{ student.projectTitle || formatData?.projectTitle || 'Sin anteproyecto registrado' }}
              </p>
            </div>
            <div>
              <span class="tecnm-field-label">Etapa de Residencia</span>
              <p class="tecnm-field-value">
                {{ student.residencyStage || '—' }}
              </p>
            </div>
          </div>

          <!-- Bloque de Fechas Límite Aplicables -->
          <div style="margin-top: 1rem; padding-top: 1rem; border-top: 1px solid var(--tecnm-gray-200); display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 1rem;">
            <div>
              <span class="tecnm-field-label">Fecha Límite: Formato 29 (1er Seguimiento)</span>
              <div style="display: flex; align-items: center; gap: 0.5rem;">
                <strong>{{ formatTecNMDate(deadlineInfo?.formato29Deadline || student.formato29Deadline) }}</strong>
                <small
                  v-if="deadlineInfo?.formato29Deadline || student.formato29Deadline"
                  :style="{ color: isDeadlinePassed(deadlineInfo?.formato29Deadline || student.formato29Deadline) && deadlineInfo?.formato29Status !== 'approved' ? 'var(--tecnm-red)' : 'var(--tecnm-gray-600)' }"
                >
                  ({{ deadlineInfo?.formato29Status === 'approved' ? 'Aprobado a tiempo' : getDeadlineRemaining(deadlineInfo?.formato29Deadline || student.formato29Deadline) }})
                </small>
              </div>
            </div>
            <div>
              <span class="tecnm-field-label">Fecha Límite: Formato 30 y 29v2 (Entrega Final)</span>
              <div style="display: flex; align-items: center; gap: 0.5rem;">
                <strong>{{ formatTecNMDate(deadlineInfo?.formato30Deadline || student.formato30Deadline) }}</strong>
                <small
                  v-if="deadlineInfo?.formato30Deadline || student.formato30Deadline"
                  :style="{ color: isDeadlinePassed(deadlineInfo?.formato30Deadline || student.formato30Deadline) && deadlineInfo?.formato30Status !== 'approved' ? 'var(--tecnm-red)' : 'var(--tecnm-gray-600)' }"
                >
                  ({{ deadlineInfo?.formato30Status === 'approved' ? 'Aprobados a tiempo' : getDeadlineRemaining(deadlineInfo?.formato30Deadline || student.formato30Deadline) }})
                </small>
              </div>
            </div>
          </div>

          <!-- Alerta si está bloqueado -->
          <div v-if="deadlineInfo?.isDocumentBlocked" class="tecnm-alert tecnm-alert-warning" style="margin-top: 1rem; margin-bottom: 0;">
            <strong>Acciones Restringidas:</strong> {{ deadlineInfo.blockedReason }}
          </div>
        </div>
      </div>

      <!-- TARJETA 2: FORMATO 29 (PRIMER SEGUIMIENTO) -->
      <div class="tecnm-card">
        <div class="tecnm-card-header" style="background: var(--tecnm-bg-light, #F8FAFC); display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 0.5rem;">
          <h3 class="tecnm-card-title" style="font-size: 1.05rem; margin: 0; color: var(--tecnm-blue-primary, #1B396A);">
            1. Formato 29 — Primer Reporte de Seguimiento
          </h3>
          <TecnmBadge v-if="getFormatDoc('formato_29')" :status="getFormatDoc('formato_29').status" />
          <span v-else class="tecnm-badge tecnm-badge-secondary">No Cargado</span>
        </div>

        <div class="tecnm-card-body" style="padding: 1.25rem;">
          <div v-if="getFormatDoc('formato_29')" style="display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 1rem;">
            <div>
              <h4 style="margin: 0 0 0.25rem 0; font-size: 1rem;">{{ getFormatDoc('formato_29').fileName }}</h4>
              <p class="tecnm-text-sub" style="margin: 0; font-size: 0.85rem;">
                Subido el <strong>{{ formatTecNMDate(getFormatDoc('formato_29').uploadedAt) }}</strong>
                <span v-if="getFormatDoc('formato_29').updatedAt"> &bull; Última modificación: {{ formatTecNMDate(getFormatDoc('formato_29').updatedAt) }}</span>
              </p>
              <div v-if="getFormatDoc('formato_29').rejectionReason" style="margin-top: 0.5rem; padding: 0.5rem 0.75rem; background: #FEF2F2; border-left: 3px solid var(--tecnm-red); color: var(--tecnm-red); font-size: 0.85rem;">
                <strong>Observaciones registradas:</strong> {{ getFormatDoc('formato_29').rejectionReason }}
              </div>
            </div>

            <div style="display: flex; gap: 0.5rem; flex-wrap: wrap; align-items: center;">
              <button
                type="button"
                class="tecnm-btn tecnm-btn-primary"
                @click="previewDocModal(getFormatDoc('formato_29'))"
              >
                Ver Documento &rarr;
              </button>
              <button
                type="button"
                class="tecnm-btn tecnm-btn-secondary"
                @click="downloadDoc(getFormatDoc('formato_29'), 'Formato29_PrimerSeguimiento.pdf')"
              >
                Descargar PDF
              </button>
              <template v-if="canEditDeadlines">
                <button
                  v-if="getFormatDoc('formato_29').status !== 'approved'"
                  type="button"
                  class="tecnm-btn tecnm-btn-success"
                  :disabled="isActionSubmitting"
                  @click="approveDoc(getFormatDoc('formato_29'))"
                >
                  Validar y Aprobar
                </button>
                <button
                  v-if="rejectingDocId !== getFormatDoc('formato_29').id && getFormatDoc('formato_29').status !== 'rejected'"
                  type="button"
                  class="tecnm-btn tecnm-btn-warning"
                  :disabled="isActionSubmitting"
                  @click="startReject(getFormatDoc('formato_29'))"
                >
                  Rechazar con Observaciones
                </button>
              </template>
            </div>
          </div>

          <div v-else class="tecnm-text-muted" style="padding: 0.5rem 0;">
            El estudiante aún no ha cargado el primer Formato 29 de seguimiento en la plataforma.
          </div>

          <!-- Formulario Desplegable de Observaciones de Rechazo -->
          <div v-if="rejectingDocId === getFormatDoc('formato_29')?.id" style="margin-top: 1.25rem; padding: 1rem; background: #FFFBEB; border: 1px solid #FCD34D; border-radius: 8px;">
            <label class="tecnm-label" style="font-weight: 600;">Observaciones y Motivo del Rechazo:</label>
            <textarea
              v-model="rejectionReason"
              class="tecnm-form-control"
              rows="3"
              placeholder="Escribe detalladamente las correcciones técnicas que el residente debe solventar..."
            ></textarea>
            <div style="margin-top: 0.75rem; display: flex; justify-content: flex-end; gap: 0.5rem;">
              <button type="button" class="tecnm-btn tecnm-btn-secondary" @click="cancelReject">Cancelar</button>
              <button type="button" class="tecnm-btn tecnm-btn-danger" :disabled="isActionSubmitting" @click="submitReject(getFormatDoc('formato_29'))">
                Confirmar Rechazo
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- TARJETA 3: FORMATO 29 (SEGUNDA ENTREGA) -->
      <div class="tecnm-card">
        <div class="tecnm-card-header" style="background: var(--tecnm-bg-light, #F8FAFC); display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 0.5rem;">
          <h3 class="tecnm-card-title" style="font-size: 1.05rem; margin: 0; color: var(--tecnm-blue-primary, #1B396A);">
            2. Formato 29 — Segundo Reporte de Seguimiento
          </h3>
          <TecnmBadge v-if="getFormatDoc('formato_29v2')" :status="getFormatDoc('formato_29v2').status" />
          <span v-else class="tecnm-badge tecnm-badge-secondary">No Cargado</span>
        </div>

        <div class="tecnm-card-body" style="padding: 1.25rem;">
          <div v-if="getFormatDoc('formato_29v2')" style="display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 1rem;">
            <div>
              <h4 style="margin: 0 0 0.25rem 0; font-size: 1rem;">{{ getFormatDoc('formato_29v2').fileName }}</h4>
              <p class="tecnm-text-sub" style="margin: 0; font-size: 0.85rem;">
                Subido el <strong>{{ formatTecNMDate(getFormatDoc('formato_29v2').uploadedAt) }}</strong>
              </p>
              <div v-if="getFormatDoc('formato_29v2').rejectionReason" style="margin-top: 0.5rem; padding: 0.5rem 0.75rem; background: #FEF2F2; border-left: 3px solid var(--tecnm-red); color: var(--tecnm-red); font-size: 0.85rem;">
                <strong>Observaciones registradas:</strong> {{ getFormatDoc('formato_29v2').rejectionReason }}
              </div>
            </div>

            <div style="display: flex; gap: 0.5rem; flex-wrap: wrap; align-items: center;">
              <button
                type="button"
                class="tecnm-btn tecnm-btn-primary"
                @click="previewDocModal(getFormatDoc('formato_29v2'))"
              >
                Ver Documento &rarr;
              </button>
              <button
                type="button"
                class="tecnm-btn tecnm-btn-secondary"
                @click="downloadDoc(getFormatDoc('formato_29v2'), 'Formato29_SegundoSeguimiento.pdf')"
              >
                Descargar PDF
              </button>
              <template v-if="canEditDeadlines">
                <button
                  v-if="getFormatDoc('formato_29v2').status !== 'approved'"
                  type="button"
                  class="tecnm-btn tecnm-btn-success"
                  :disabled="isActionSubmitting"
                  @click="approveDoc(getFormatDoc('formato_29v2'))"
                >
                  Validar y Aprobar
                </button>
                <button
                  v-if="rejectingDocId !== getFormatDoc('formato_29v2').id && getFormatDoc('formato_29v2').status !== 'rejected'"
                  type="button"
                  class="tecnm-btn tecnm-btn-warning"
                  :disabled="isActionSubmitting"
                  @click="startReject(getFormatDoc('formato_29v2'))"
                >
                  Rechazar con Observaciones
                </button>
              </template>
            </div>
          </div>

          <div v-else class="tecnm-text-muted" style="padding: 0.5rem 0;">
            <span v-if="!deadlineInfo?.canUploadSecondPhase">
              Bloqueado: Requiere la previa aprobación del primer Formato 29.
            </span>
            <span v-else>
              El estudiante aún no ha cargado el segundo Formato 29.
            </span>
          </div>

          <!-- Formulario Desplegable de Observaciones de Rechazo -->
          <div v-if="rejectingDocId === getFormatDoc('formato_29v2')?.id" style="margin-top: 1.25rem; padding: 1rem; background: #FFFBEB; border: 1px solid #FCD34D; border-radius: 8px;">
            <label class="tecnm-label" style="font-weight: 600;">Observaciones y Motivo del Rechazo:</label>
            <textarea
              v-model="rejectionReason"
              class="tecnm-form-control"
              rows="3"
              placeholder="Detalla las correcciones necesarias..."
            ></textarea>
            <div style="margin-top: 0.75rem; display: flex; justify-content: flex-end; gap: 0.5rem;">
              <button type="button" class="tecnm-btn tecnm-btn-secondary" @click="cancelReject">Cancelar</button>
              <button type="button" class="tecnm-btn tecnm-btn-danger" :disabled="isActionSubmitting" @click="submitReject(getFormatDoc('formato_29v2'))">
                Confirmar Rechazo
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- TARJETA 4: FORMATO 30 (EVALUACIÓN FINAL) -->
      <div class="tecnm-card">
        <div class="tecnm-card-header" style="background: var(--tecnm-bg-light, #F8FAFC); display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 0.5rem;">
          <h3 class="tecnm-card-title" style="font-size: 1.05rem; margin: 0; color: var(--tecnm-blue-primary, #1B396A);">
            3. Formato 30 — Evaluación de Residencia Profesional
          </h3>
          <TecnmBadge v-if="getFormatDoc('formato_30')" :status="getFormatDoc('formato_30').status" />
          <span v-else class="tecnm-badge tecnm-badge-secondary">No Cargado</span>
        </div>

        <div class="tecnm-card-body" style="padding: 1.25rem;">
          <div v-if="getFormatDoc('formato_30')" style="display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 1rem;">
            <div>
              <h4 style="margin: 0 0 0.25rem 0; font-size: 1rem;">{{ getFormatDoc('formato_30').fileName }}</h4>
              <p class="tecnm-text-sub" style="margin: 0; font-size: 0.85rem;">
                Subido el <strong>{{ formatTecNMDate(getFormatDoc('formato_30').uploadedAt) }}</strong>
              </p>
              <div v-if="getFormatDoc('formato_30').rejectionReason" style="margin-top: 0.5rem; padding: 0.5rem 0.75rem; background: #FEF2F2; border-left: 3px solid var(--tecnm-red); color: var(--tecnm-red); font-size: 0.85rem;">
                <strong>Observaciones registradas:</strong> {{ getFormatDoc('formato_30').rejectionReason }}
              </div>
            </div>

            <div style="display: flex; gap: 0.5rem; flex-wrap: wrap; align-items: center;">
              <button
                type="button"
                class="tecnm-btn tecnm-btn-primary"
                @click="previewDocModal(getFormatDoc('formato_30'))"
              >
                Ver Documento &rarr;
              </button>
              <button
                type="button"
                class="tecnm-btn tecnm-btn-secondary"
                @click="downloadDoc(getFormatDoc('formato_30'), 'Formato30_EvaluacionFinal.pdf')"
              >
                Descargar PDF
              </button>
              <template v-if="canEditDeadlines">
                <button
                  v-if="getFormatDoc('formato_30').status !== 'approved'"
                  type="button"
                  class="tecnm-btn tecnm-btn-success"
                  :disabled="isActionSubmitting"
                  @click="approveDoc(getFormatDoc('formato_30'))"
                >
                  Validar y Aprobar
                </button>
                <button
                  v-if="rejectingDocId !== getFormatDoc('formato_30').id && getFormatDoc('formato_30').status !== 'rejected'"
                  type="button"
                  class="tecnm-btn tecnm-btn-warning"
                  :disabled="isActionSubmitting"
                  @click="startReject(getFormatDoc('formato_30'))"
                >
                  Rechazar con Observaciones
                </button>
              </template>
            </div>
          </div>

          <div v-else class="tecnm-text-muted" style="padding: 0.5rem 0;">
            <span v-if="!deadlineInfo?.canUploadSecondPhase">
              Bloqueado: Requiere la previa aprobación del primer Formato 29.
            </span>
            <span v-else>
              El estudiante aún no ha cargado el Formato 30 de evaluación final.
            </span>
          </div>

          <!-- Formulario Desplegable de Observaciones de Rechazo -->
          <div v-if="rejectingDocId === getFormatDoc('formato_30')?.id" style="margin-top: 1.25rem; padding: 1rem; background: #FFFBEB; border: 1px solid #FCD34D; border-radius: 8px;">
            <label class="tecnm-label" style="font-weight: 600;">Observaciones y Motivo del Rechazo:</label>
            <textarea
              v-model="rejectionReason"
              class="tecnm-form-control"
              rows="3"
              placeholder="Detalla las correcciones necesarias..."
            ></textarea>
            <div style="margin-top: 0.75rem; display: flex; justify-content: flex-end; gap: 0.5rem;">
              <button type="button" class="tecnm-btn tecnm-btn-secondary" @click="cancelReject">Cancelar</button>
              <button type="button" class="tecnm-btn tecnm-btn-danger" :disabled="isActionSubmitting" @click="submitReject(getFormatDoc('formato_30'))">
                Confirmar Rechazo
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Modal Visor Integrado de Documentos -->
    <div
      v-if="isPreviewModalOpen"
      id="previewFormatDocModal"
      class="tecnm-modal-backdrop"
      role="dialog"
      aria-modal="true"
      style="z-index: 1070;"
      @click.self="closePreviewModal"
    >
      <div class="tecnm-modal" style="max-width: 950px; width: 92vw; max-height: 90vh; display: flex; flex-direction: column;">
        <div class="tecnm-modal-header" style="display: flex; justify-content: space-between; align-items: center;">
          <h3 class="tecnm-modal-title">
            Vista Previa: <span id="previewDocName">{{ previewDoc?.fileName }}</span>
          </h3>
          <div style="display: flex; gap: 0.5rem; align-items: center;">
            <button
              v-if="previewDoc"
              type="button"
              class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
              @click="downloadDoc(previewDoc, previewDoc.fileName)"
            >
              Descargar
            </button>
            <button
              type="button"
              class="tecnm-modal-close"
              aria-label="Cerrar vista previa"
              @click="closePreviewModal"
            >
              &times;
            </button>
          </div>
        </div>
        <div
          class="tecnm-modal-body"
          style="flex: 1; min-height: 520px; height: 75vh; padding: 0; background-color: var(--tecnm-gray-100); display: flex; align-items: center; justify-content: center; overflow: hidden;"
        >
          <div v-if="!previewObjectUrl" class="tecnm-spinner"></div>
          <img
            v-else-if="previewDoc?.fileName?.toLowerCase().endsWith('.png') || previewDoc?.fileName?.toLowerCase().endsWith('.jpg') || previewDoc?.fileName?.toLowerCase().endsWith('.jpeg')"
            :src="previewObjectUrl"
            alt="Vista previa del documento"
            style="max-width: 100%; max-height: 100%; object-fit: contain;"
          />
          <embed
            v-else
            :src="previewObjectUrl"
            type="application/pdf"
            style="width: 100%; height: 100%; border: none;"
          />
        </div>
        <div class="tecnm-modal-footer">
          <button
            type="button"
            class="tecnm-btn tecnm-btn-secondary"
            @click="closePreviewModal"
          >
            Cerrar Vista Previa
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.tecnm-field-label {
  display: block;
  font-size: 0.8rem;
  color: var(--tecnm-gray-600);
  margin-bottom: 0.2rem;
  text-transform: uppercase;
  letter-spacing: 0.03em;
}
.tecnm-field-value {
  margin: 0;
  color: var(--tecnm-gray-800);
  font-size: 0.95rem;
}
</style>

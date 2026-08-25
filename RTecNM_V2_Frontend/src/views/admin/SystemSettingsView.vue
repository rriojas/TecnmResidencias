<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import apiClient from '@/services/api'
import { useConfirm } from '@/composables/useConfirm'

const authStore = useAuthStore()
const router = useRouter()
const { confirm } = useConfirm()

const activeTab = ref('smtp') // 'smtp' | 'template'

// Alert state
const alertMessage = ref('')
const alertType = ref('success')
let alertTimer = null

function showAlert(msg, type = 'success') {
  alertMessage.value = msg
  alertType.value = type
  clearTimeout(alertTimer)
  alertTimer = setTimeout(() => {
    alertMessage.value = ''
  }, 5000)
}

// SMTP State
const smtpForm = ref({
  host: 'smtp.gmail.com',
  port: 587,
  senderName: 'TecNM Residencias Monclova',
  senderEmail: 'residencias@monclova.tecnm.mx',
  username: '',
  password: '',
  enableSsl: true,
  useMockInDev: false
})

const isLoadingSmtp = ref(false)
const isSavingSmtp = ref(false)
const testEmail = ref('')
const isTestingSmtp = ref(false)

async function loadSmtpConfig() {
  isLoadingSmtp.value = true
  try {
    const res = await apiClient.get('/v1/system/settings/smtp')
    smtpForm.value = {
      host: res.data.host || 'smtp.gmail.com',
      port: res.data.port || 587,
      senderName: res.data.senderName || 'TecNM Residencias Monclova',
      senderEmail: res.data.senderEmail || 'residencias@monclova.tecnm.mx',
      username: res.data.username || '',
      password: res.data.password || '',
      enableSsl: res.data.enableSsl !== false,
      useMockInDev: Boolean(res.data.useMockInDev)
    }
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al cargar la configuración SMTP.', 'danger')
  } finally {
    isLoadingSmtp.value = false
  }
}

async function handleSaveSmtp() {
  isSavingSmtp.value = true
  try {
    await apiClient.put('/v1/system/settings/smtp', smtpForm.value)
    showAlert('Configuración SMTP guardada y aplicada correctamente.', 'success')
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al guardar la configuración SMTP.', 'danger')
  } finally {
    isSavingSmtp.value = false
  }
}

async function handleTestSmtp() {
  if (!testEmail.value.trim()) {
    showAlert('Ingresa un correo electrónico de destino para la prueba.', 'warning')
    return
  }

  isTestingSmtp.value = true
  try {
    const res = await apiClient.post('/v1/system/settings/smtp/test', {
      recipientEmail: testEmail.value.trim(),
      customConfig: smtpForm.value
    })
    showAlert(res.data.message || 'Correo de prueba enviado con éxito.', 'success')
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error durante la prueba SMTP.', 'danger')
  } finally {
    isTestingSmtp.value = false
  }
}

// Template State
const templateHtml = ref('')
const isLoadingTemplate = ref(false)
const isSavingTemplate = ref(false)
const fileInputRef = ref(null)
const wordFileInputRef = ref(null)
const isUploadingWord = ref(false)

const templateCharCount = computed(() => {
  const count = templateHtml.value ? templateHtml.value.length : 0
  return count.toLocaleString('es-MX') + ' caracteres'
})

const templateVariables = [
  { code: '[NOMBRE_ALUMNO]', label: 'Nombre del Estudiante' },
  { code: '[MATRICULA]', label: 'No. Control' },
  { code: '[CARRERA]', label: 'Carrera' },
  { code: '[EMPRESA]', label: 'Empresa / Dependencia' },
  { code: '[FECHA]', label: 'Fecha Oficial' },
  { code: '[FOLIO]', label: 'Folio de Documento' }
]

function copyVariableToClipboard(code) {
  navigator.clipboard.writeText(code)
  showAlert(`Variable ${code} copiada al portapapeles.`, 'info')
}

async function loadTemplate() {
  isLoadingTemplate.value = true
  try {
    const res = await apiClient.get('/v1/system/settings/template/presentation-letter')
    templateHtml.value = res.data.templateHtml || ''
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al cargar la plantilla.', 'danger')
  } finally {
    isLoadingTemplate.value = false
  }
}

async function handleSaveTemplate() {
  isSavingTemplate.value = true
  try {
    await apiClient.put('/v1/system/settings/template/presentation-letter', {
      templateHtml: templateHtml.value
    })
    showAlert('Plantilla oficial guardada y actualizada correctamente.', 'success')
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al guardar la plantilla.', 'danger')
  } finally {
    isSavingTemplate.value = false
  }
}

function handleDownloadTemplate() {
  const blob = new Blob([templateHtml.value], { type: 'text/html;charset=utf-8' })
  const url = window.URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = 'plantilla_carta_presentacion.html'
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
  window.URL.revokeObjectURL(url)
}

function triggerWordUpload() {
  if (wordFileInputRef.value) {
    wordFileInputRef.value.click()
  }
}

async function handleWordFileUpload(event) {
  const file = event.target.files[0]
  if (!file) return

  const formData = new FormData()
  formData.append('file', file)

  isUploadingWord.value = true
  try {
    const res = await apiClient.post('/v1/system/settings/template/presentation-letter/upload-word', formData, {
      headers: { 'Content-Type': 'multipart/form-data' }
    })
    templateHtml.value = res.data.templateHtml || templateHtml.value
    showAlert(res.data.message || 'Plantilla Word (.docx) procesada y desglosada correctamente.', 'success')
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al procesar el archivo Word.', 'danger')
  } finally {
    isUploadingWord.value = false
    event.target.value = ''
  }
}

function triggerFileUpload() {
  if (fileInputRef.value) {
    fileInputRef.value.click()
  }
}

async function handleFileUpload(event) {
  const file = event.target.files[0]
  if (!file) return

  const formData = new FormData()
  formData.append('file', file)

  try {
    const res = await apiClient.post('/v1/system/settings/template/presentation-letter/upload', formData, {
      headers: { 'Content-Type': 'multipart/form-data' }
    })
    showAlert(res.data.message || 'Archivo de plantilla aplicado.', 'success')
    await loadTemplate()
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al subir el archivo HTML.', 'danger')
  } finally {
    event.target.value = ''
  }
}

async function handleResetTemplate() {
  const confirmed = await confirm({
    title: 'Restablecer Plantilla',
    message: '¿Deseas restablecer la plantilla de la Carta de Presentación al formato oficial por defecto del TecNM?',
    okText: 'Restablecer',
    cancelText: 'Cancelar'
  })
  if (!confirmed) return

  try {
    const res = await apiClient.post('/v1/system/settings/template/presentation-letter/reset')
    showAlert(res.data.message || 'Plantilla restablecida por defecto.', 'success')
    await loadTemplate()
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al restablecer la plantilla.', 'danger')
  }
}

onMounted(() => {
  if (!authStore.isAdmin && !authStore.hasRole('vinculacion', 'departmenthead')) {
    router.replace('/dashboard')
    return
  }
  loadSmtpConfig()
  loadTemplate()
})
</script>

<template>
  <div class="tecnm-settings-view">
    <!-- Header Institucional -->
    <div class="tecnm-actions-bar settings-header-bar">
      <div>
        <h1 class="tecnm-page-title">Configuración del Sistema y Vinculación</h1>
        <p class="tecnm-page-subtitle">Gestión de Servidor de Correo Saliente y Plantillas Oficiales de Residencias</p>
      </div>
      <div class="settings-header-badge">
        <svg xmlns="http://www.w3.org/2000/svg" width="15" height="15" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M10.343 3.94c.09-.542.56-.94 1.11-.94h1.093c.55 0 1.02.398 1.11.94l.149.894c.07.424.384.764.78.93.398.164.855.142 1.205-.108l.737-.527a1.125 1.125 0 0 1 1.45.12l.773.774c.39.389.44 1.002.12 1.45l-.527.737c-.25.35-.272.806-.107 1.204.165.397.505.71.93.78l.893.15c.543.09.94.559.94 1.109v1.094c0 .55-.397 1.02-.94 1.11l-.894.149c-.424.07-.764.383-.929.78-.165.398-.143.854.107 1.204l.527.738c.32.447.27.1.06-.12 1.45l-.774.773a1.125 1.125 0 0 1-1.449.12l-.738-.527c-.35-.25-.806-.272-1.203-.107-.398.165-.71.505-.781.929l-.15.894c-.09.542-.56.94-1.11.94h-1.094c-.55 0-1.019-.398-1.11-.94l-.148-.894c-.071-.424-.384-.764-.781-.93-.398-.164-.854-.142-1.204.108l-.738.527c-.447.32-1.06.27-1.45-.12l-.773-.774a1.125 1.125 0 0 1-.12-1.45l.527-.737c.25-.35.272-.806.108-1.204-.165-.397-.506-.71-.93-.78l-.894-.15c-.542-.09-.94-.56-.94-1.109v-1.094c0-.55.398-1.02.94-1.11l.894-.149c.424-.07.765-.383.93-.78.165-.398.143-.854-.108-1.204l-.526-.738a1.125 1.125 0 0 1 .12-1.45l.773-.773a1.125 1.125 0 0 1 1.45-.12l.737.527c.35.25.807.272 1.204.107.397-.165.71-.505.78-.929l.15-.894Z" />
          <path stroke-linecap="round" stroke-linejoin="round" d="M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z" />
        </svg>
        <span>Módulo de Vinculación &amp; Administración</span>
      </div>
    </div>

    <!-- Alert Notificación -->
    <div
      v-if="alertMessage"
      class="tecnm-alert"
      :class="`tecnm-alert-${alertType}`"
      role="alert"
      style="margin-bottom: 1.25rem;"
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

    <!-- Tabs Navigation -->
    <div class="settings-tabs-container">
      <button
        type="button"
        class="settings-tab-btn"
        :class="{ active: activeTab === 'smtp' }"
        @click="activeTab = 'smtp'"
      >
        <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M21.75 6.75v10.5a2.25 2.25 0 0 1-2.25 2.25h-15a2.25 2.25 0 0 1-2.25-2.25V6.75m19.5 0A2.25 2.25 0 0 0 19.5 4.5h-15a2.25 2.25 0 0 0-2.25 2.25m19.5 0v.243a2.25 2.25 0 0 1-1.07 1.916l-7.5 4.615a2.25 2.25 0 0 1-2.36 0L3.32 8.91a2.25 2.25 0 0 1-1.07-1.916V6.75" />
        </svg>
        <span>Servidor SMTP (Correos)</span>
      </button>

      <button
        type="button"
        class="settings-tab-btn"
        :class="{ active: activeTab === 'template' }"
        @click="activeTab = 'template'"
      >
        <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m2.25 0H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z" />
          <path stroke-linecap="round" stroke-linejoin="round" d="M9 14.25h6m-6 3h3.75" />
        </svg>
        <span>Plantilla Carta de Presentación</span>
      </button>
    </div>

    <!-- ======================================================== -->
    <!-- TAB 1: SERVIDOR SMTP -->
    <!-- ======================================================== -->
    <div v-if="activeTab === 'smtp'" class="settings-main-card">
      <!-- Card Top Header -->
      <div class="settings-card-top-header">
        <div class="settings-header-icon-box">
          <svg xmlns="http://www.w3.org/2000/svg" width="22" height="22" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" style="color: var(--tecnm-blue-primary, #1b396a);">
            <path stroke-linecap="round" stroke-linejoin="round" d="M5.25 14.25h13.5m-13.5 0a3 3 0 0 1-3-3m3 3a3 3 0 1 0 0 6h13.5a3 3 0 1 0 0-6m-16.5-3a3 3 0 0 1 3-3h13.5a3 3 0 0 1 3 3m-19.5 0a4.5 4.5 0 0 1 .9-2.7L5.75 5.1a3 3 0 0 1 2.4-1.1h7.7a3 3 0 0 1 2.4 1.1l2.1 3.45a4.5 4.5 0 0 1 .9 2.7" />
          </svg>
        </div>
        <div class="settings-header-titles">
          <h3 class="settings-card-main-title">Parámetros del Servidor de Correo Saliente (SMTP)</h3>
          <p class="settings-card-sub-title">Configura los accesos para el envío automatizado de notificaciones y cartas en PDF</p>
        </div>
        <div class="settings-header-status-pill">
          <span v-if="smtpForm.useMockInDev" class="status-pill status-pill-warning">
            Modo Simulación Activo
          </span>
          <span v-else class="status-pill status-pill-success">
            Modo Producción Activo
          </span>
        </div>
      </div>

      <div class="settings-card-body">
        <div v-if="isLoadingSmtp" class="settings-loading-box">
          <div class="tecnm-spinner"></div>
          <p>Cargando parámetros del servidor SMTP...</p>
        </div>

        <form v-else @submit.prevent="handleSaveSmtp">
          <!-- Sección 1: Datos del Servidor y Remitente -->
          <div class="settings-section">
            <div class="settings-section-header">
              <span class="settings-section-badge">1</span>
              <h4 class="settings-section-title">Datos del Servidor y Remitente</h4>
            </div>

            <div class="settings-fields-grid-5">
              <div class="settings-field-item">
                <label class="settings-label">Servidor SMTP (Host) *</label>
                <input
                  v-model="smtpForm.host"
                  type="text"
                  class="settings-input"
                  placeholder="smtp.gmail.com"
                  required
                />
                <span class="settings-hint">Ejemplo: smtp.office365.com, smtp.gmail.com</span>
              </div>

              <div class="settings-field-item">
                <label class="settings-label">Puerto SMTP *</label>
                <input
                  v-model.number="smtpForm.port"
                  type="number"
                  class="settings-input"
                  placeholder="587"
                  required
                />
                <span class="settings-hint">587 (TLS estándar) o 465 (SSL)</span>
              </div>

              <div class="settings-field-item">
                <label class="settings-label">Nombre del Remitente *</label>
                <input
                  v-model="smtpForm.senderName"
                  type="text"
                  class="settings-input"
                  placeholder="TecNM Residencias Monclova"
                  required
                />
                <span class="settings-hint">Nombre institucional visible en el buzón del alumno</span>
              </div>

              <div class="settings-field-item">
                <label class="settings-label">Correo Remitente *</label>
                <input
                  v-model="smtpForm.senderEmail"
                  type="email"
                  class="settings-input"
                  placeholder="residencias@monclova.tecnm.mx"
                  required
                />
                <span class="settings-hint">Dirección de correo remitente</span>
              </div>

              <div class="settings-field-item">
                <label class="settings-label">Usuario SMTP / Cuenta</label>
                <input
                  v-model="smtpForm.username"
                  type="text"
                  class="settings-input"
                  placeholder="tu_correo@monclova.tecnm.mx"
                />
                <span class="settings-hint">Usuario de autenticación en el servidor</span>
              </div>
            </div>

            <div class="settings-fields-row-single" style="margin-top: 1rem;">
              <div class="settings-field-item" style="max-width: 320px;">
                <label class="settings-label">Contraseña SMTP / App Password</label>
                <input
                  v-model="smtpForm.password"
                  type="password"
                  class="settings-input"
                  placeholder="••••••••••••"
                />
                <span class="settings-hint">Contraseña de aplicación o clave de autenticación</span>
              </div>
            </div>
          </div>

          <!-- Sección 2: Seguridad y Modo de Operación -->
          <div class="settings-section" style="margin-top: 2rem;">
            <div class="settings-section-header">
              <span class="settings-section-badge">2</span>
              <h4 class="settings-section-title">Seguridad y Modo de Operación</h4>
            </div>

            <div class="settings-switches-grid">
              <!-- Switch Card 1: TLS / SSL -->
              <div class="switch-option-card">
                <div class="switch-card-header">
                  <div class="switch-card-text">
                    <h5 class="switch-card-title">Habilitar Seguridad TLS / SSL</h5>
                    <p class="switch-card-desc">Cifra las comunicaciones de correo saliente mediante StartTLS o SSL directo.</p>
                  </div>
                  <label class="tecnm-switch">
                    <input v-model="smtpForm.enableSsl" type="checkbox" />
                    <span class="tecnm-switch-slider"></span>
                  </label>
                </div>
                <div class="switch-card-footer">
                  <span class="badge-pill-blue">Recomendado para servidores en la nube</span>
                </div>
              </div>

              <!-- Switch Card 2: Mock Dev -->
              <div class="switch-option-card switch-option-card--warning">
                <div class="switch-card-header">
                  <div class="switch-card-text">
                    <h5 class="switch-card-title">Modo Simulación (Mock en Desarrollo)</h5>
                    <p class="switch-card-desc">Los correos no se envían a buzones reales; se registran en los logs del servidor para pruebas y auditoría.</p>
                  </div>
                  <label class="tecnm-switch">
                    <input v-model="smtpForm.useMockInDev" type="checkbox" />
                    <span class="tecnm-switch-slider"></span>
                  </label>
                </div>
                <div class="switch-card-footer">
                  <span class="badge-pill-gold">Buzón seguro activado (Sin salida externa)</span>
                </div>
              </div>
            </div>
          </div>

          <!-- Botón Guardar -->
          <div class="settings-actions-footer">
            <button
              type="submit"
              class="tecnm-btn tecnm-btn-primary"
              :disabled="isSavingSmtp"
              style="display: inline-flex; align-items: center; gap: 0.5rem;"
            >
              <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
                <path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5" />
              </svg>
              <span>{{ isSavingSmtp ? 'Guardando...' : 'Guardar Configuración SMTP' }}</span>
            </button>
          </div>
        </form>

        <!-- Bloque de Prueba SMTP en Vivo -->
        <div class="smtp-test-box">
          <div class="smtp-test-icon-badge">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" style="color: #0284c7;">
              <path stroke-linecap="round" stroke-linejoin="round" d="M6 12 3.269 3.125A59.769 59.769 0 0 1 21.485 12 59.768 59.768 0 0 1 3.27 20.875L5.999 12Zm0 0h7.5" />
            </svg>
          </div>
          <div class="smtp-test-content">
            <h4 class="smtp-test-title">Probar Conexión SMTP en Vivo</h4>
            <p class="smtp-test-desc">
              Envía un correo de verificación en tiempo real para confirmar que las credenciales y el puerto configurado respondan adecuadamente.
            </p>
            <div class="smtp-test-form-row">
              <input
                v-model="testEmail"
                type="email"
                class="settings-input smtp-test-input"
                placeholder="Ingresa tu correo de prueba (ej. prueba@correo.com)"
              />
              <button
                type="button"
                class="tecnm-btn tecnm-btn-secondary"
                :disabled="isTestingSmtp"
                @click="handleTestSmtp"
                style="display: inline-flex; align-items: center; gap: 0.4rem; white-space: nowrap;"
              >
                <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M6 12 3.269 3.125A59.769 59.769 0 0 1 21.485 12 59.768 59.768 0 0 1 3.27 20.875L5.999 12Zm0 0h7.5" />
                </svg>
                <span>{{ isTestingSmtp ? 'Enviando...' : 'Enviar Correo de Prueba' }}</span>
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- ======================================================== -->
    <!-- TAB 2: PLANTILLA CARTA DE PRESENTACIÓN -->
    <!-- ======================================================== -->
    <div v-if="activeTab === 'template'" class="settings-main-card">
      <!-- Card Top Header -->
      <div class="settings-card-top-header" style="flex-wrap: wrap; gap: 1rem;">
        <div class="tecnm-d-flex tecnm-align-center" style="display: flex; align-items: center; gap: 0.85rem; flex: 1; min-width: 300px;">
          <div class="settings-header-icon-box">
            <svg xmlns="http://www.w3.org/2000/svg" width="22" height="22" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" style="color: var(--tecnm-blue-primary, #1b396a);">
              <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z" />
            </svg>
          </div>
          <div class="settings-header-titles">
            <h3 class="settings-card-main-title">Plantilla Oficial para Carta de Presentación</h3>
            <p class="settings-card-sub-title">Diseño membretado institucional que se convertirá automáticamente a formato PDF</p>
          </div>
        </div>

        <!-- Botones de Acción de Plantilla -->
        <div class="settings-template-actions">
          <button
            type="button"
            class="tecnm-btn tecnm-btn-primary tecnm-btn-sm"
            :disabled="isUploadingWord"
            @click="triggerWordUpload"
            style="display: inline-flex; align-items: center; gap: 0.35rem;"
          >
            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M3 16.5v2.25A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75V16.5m-13.5-9L12 3m0 0 4.5 4.5M12 3v13.5" />
            </svg>
            <span>{{ isUploadingWord ? 'Procesando Word...' : 'Cargar Plantilla Word (.docx)' }}</span>
          </button>
          <input
            ref="wordFileInputRef"
            type="file"
            accept=".docx,.doc"
            style="display: none;"
            @change="handleWordFileUpload"
          />

          <button
            type="button"
            class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
            @click="triggerFileUpload"
            style="display: inline-flex; align-items: center; gap: 0.35rem;"
          >
            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M3 16.5v2.25A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75V16.5m-13.5-9L12 3m0 0 4.5 4.5M12 3v13.5" />
            </svg>
            <span>Subir .html</span>
          </button>
          <input
            ref="fileInputRef"
            type="file"
            accept=".html,.htm"
            style="display: none;"
            @change="handleFileUpload"
          />

          <button
            type="button"
            class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
            @click="handleDownloadTemplate"
            style="display: inline-flex; align-items: center; gap: 0.35rem;"
          >
            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M3 16.5v2.25A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75V16.5M16.5 12 12 16.5m0 0L7.5 12m4.5 4.5V3" />
            </svg>
            <span>Descargar .html</span>
          </button>

          <button
            type="button"
            class="tecnm-btn tecnm-btn-outline-danger tecnm-btn-sm"
            @click="handleResetTemplate"
            style="display: inline-flex; align-items: center; gap: 0.35rem;"
          >
            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0 3.181 3.183a8.25 8.25 0 0 0 13.803-3.7M4.031 9.865a8.25 8.25 0 0 1 13.803-3.7l3.181 3.182m0-4.991v4.99" />
            </svg>
            <span>Restablecer</span>
          </button>
        </div>
      </div>

      <div class="settings-card-body">
        <!-- Banner Verde: Carga Inteligente desde Word -->
        <div class="word-info-card">
          <div class="word-info-header">
            <div class="word-info-icon">
              <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" style="color: #16a34a;">
                <path stroke-linecap="round" stroke-linejoin="round" d="M11.25 11.25l.041-.02a.75.75 0 0 1 1.063.852l-.708 2.836a.75.75 0 0 0 1.063.853l.041-.021M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Zm-9-3.75h.008v.008H12V8.25Z" />
              </svg>
            </div>
            <h5 class="word-info-title">Carga Inteligente desde Microsoft Word (.docx)</h5>
          </div>

          <p class="word-info-desc">
            Puedes diseñar y membretar la carta en <strong>Microsoft Word</strong> e insertarle las variables entre corchetes <code>[...]</code>. Al cargarla aquí, el sistema la convertirá automáticamente conservando la estructura, tipografía, párrafos y tablas.
          </p>

          <div class="word-variables-section">
            <span class="word-variables-title">
              Variables dinámicas disponibles <small class="text-muted">(haz clic sobre cualquiera para copiar su código):</small>
            </span>
            <div class="word-variables-grid">
              <button
                v-for="v in templateVariables"
                :key="v.code"
                type="button"
                class="var-pill-btn"
                :title="`Copiar ${v.code}`"
                @click="copyVariableToClipboard(v.code)"
              >
                <strong class="var-pill-code">{{ v.code }}</strong>
                <span class="var-pill-label">{{ v.label }}</span>
              </button>
            </div>
          </div>
        </div>

        <!-- Vista Lado a Lado: Editor y Preview -->
        <div v-if="isLoadingTemplate" class="settings-loading-box">
          <div class="tecnm-spinner"></div>
          <p>Cargando código y vista previa de la plantilla...</p>
        </div>

        <div v-else class="template-split-grid">
          <!-- Columna Izquierda: Código Fuente HTML -->
          <div class="template-editor-column">
            <div class="column-box-header">
              <div class="column-box-title-group">
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" style="color: var(--tecnm-blue-primary, #1b396a);">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M17.25 6.75 22.5 12l-5.25 5.25m-10.5 0L1.5 12l5.25-5.25m7.5-3-4.5 16.5" />
                </svg>
                <span class="column-box-title">Código Fuente HTML</span>
              </div>
              <span class="char-count-badge">{{ templateCharCount }}</span>
            </div>

            <div class="editor-textarea-wrapper">
              <textarea
                v-model="templateHtml"
                rows="24"
                class="template-code-textarea"
                placeholder="Ingresa o edita el código HTML aquí..."
                spellcheck="false"
              ></textarea>
            </div>

            <div class="editor-column-footer">
              <button
                type="button"
                class="tecnm-btn tecnm-btn-primary"
                :disabled="isSavingTemplate"
                @click="handleSaveTemplate"
                style="display: inline-flex; align-items: center; gap: 0.5rem; justify-content: center; width: 100%; max-width: 320px;"
              >
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
                  <path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5" />
                </svg>
                <span>{{ isSavingTemplate ? 'Guardando...' : 'Guardar Cambios en Plantilla' }}</span>
              </button>
            </div>
          </div>

          <!-- Columna Derecha: Vista Previa en Vivo -->
          <div class="template-preview-column">
            <div class="column-box-header">
              <div class="column-box-title-group">
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" style="color: var(--tecnm-blue-primary, #1b396a);">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M2.036 12.322a1.012 1.012 0 0 1 0-.639C3.423 7.51 7.36 4.5 12 4.5c4.638 0 8.573 3.007 9.963 7.178.07.207.07.431 0 .639C20.577 16.49 16.64 19.5 12 19.5c-4.638 0-8.573-3.007-9.963-7.178Z" />
                  <path stroke-linecap="round" stroke-linejoin="round" d="M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z" />
                </svg>
                <span class="column-box-title">Vista Previa en Vivo</span>
              </div>
              <span class="preview-badge-status">Formato Membretado Oficial</span>
            </div>

            <div class="preview-iframe-wrapper">
              <iframe
                :srcdoc="templateHtml"
                class="template-preview-iframe"
                title="Vista previa en vivo de la Carta de Presentación"
              ></iframe>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.tecnm-settings-view {
  display: flex;
  flex-direction: column;
}

/* Header Bar & Badge */
.settings-header-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1.5rem;
  flex-wrap: wrap;
  gap: 1rem;
}

.settings-header-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
  padding: 0.4rem 0.85rem;
  background-color: #fffbeb;
  border: 1px solid #fde68a;
  border-radius: 9999px;
  color: #b45309;
  font-size: 0.825rem;
  font-weight: 600;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.03);
}

/* Tabs Navigation */
.settings-tabs-container {
  display: flex;
  gap: 6px;
  border-bottom: 1px solid #e2e8f0;
  margin-bottom: 1.5rem;
  padding-bottom: 0;
  align-items: flex-end;
}

.settings-tab-btn {
  display: inline-flex;
  align-items: center;
  gap: 0.6rem;
  padding: 0.65rem 1.35rem;
  font-size: 0.9rem;
  font-weight: 600;
  border-radius: 6px 6px 0 0;
  cursor: pointer;
  transition: all 0.18s ease;
  background: #e8edf5;
  border: 1px solid transparent;
  border-bottom: 3px solid transparent;
  color: var(--tecnm-blue-primary, #1b396a);
  position: relative;
  bottom: -1px;
}

.settings-tab-btn:hover {
  background: #dfe6f2;
  color: var(--tecnm-blue-dark, #0f2548);
}

.settings-tab-btn.active {
  background: var(--tecnm-blue-primary, #1b396a);
  color: #ffffff;
  font-weight: 700;
  border-color: transparent;
  border-bottom: 3px solid var(--tecnm-gold-accent, #c5a059);
  box-shadow: 0 -2px 6px rgba(27, 57, 106, 0.08);
}

.settings-tab-btn.active svg {
  color: #ffffff;
}

.settings-tab-btn:not(.active) svg {
  color: var(--tecnm-blue-primary, #1b396a);
}

/* Main Card */
.settings-main-card {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-top: 3px solid var(--tecnm-gold-accent, #c5a059);
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04);
  overflow: hidden;
}

.settings-card-top-header {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1.25rem 1.5rem;
  border-bottom: 1px solid #e2e8f0;
  background: #ffffff;
}

.settings-header-icon-box {
  width: 44px;
  height: 44px;
  border-radius: 8px;
  background: #eff6ff;
  border: 1px solid #dbeafe;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.settings-header-titles {
  flex: 1;
}

.settings-card-main-title {
  margin: 0;
  font-size: 1.15rem;
  font-weight: 700;
  color: var(--tecnm-blue-primary, #1b396a);
}

.settings-card-sub-title {
  margin: 0.2rem 0 0 0;
  font-size: 0.85rem;
  color: #64748b;
}

.settings-header-status-pill {
  flex-shrink: 0;
}

.status-pill {
  display: inline-block;
  padding: 0.35rem 0.85rem;
  border-radius: 9999px;
  font-size: 0.8rem;
  font-weight: 600;
}

.status-pill-warning {
  background: #fffbeb;
  color: #d97706;
  border: 1px solid #fde68a;
}

.status-pill-success {
  background: #f0fdf4;
  color: #15803d;
  border: 1px solid #bbf7d0;
}

.settings-card-body {
  padding: 1.5rem;
}

.settings-loading-box {
  text-align: center;
  padding: 3rem 1rem;
  color: #64748b;
}

/* Sections */
.settings-section-header {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  margin-bottom: 1.25rem;
}

.settings-section-badge {
  width: 24px;
  height: 24px;
  background: var(--tecnm-blue-primary, #1b396a);
  color: #ffffff;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 0.8rem;
  font-weight: 700;
  flex-shrink: 0;
}

.settings-section-title {
  margin: 0;
  font-size: 0.95rem;
  font-weight: 700;
  color: var(--tecnm-blue-primary, #1b396a);
}

/* Form Fields */
.settings-fields-grid-5 {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1.25rem;
}

.settings-field-item {
  display: flex;
  flex-direction: column;
}

.settings-label {
  font-size: 0.825rem;
  font-weight: 600;
  color: #334155;
  margin-bottom: 0.4rem;
}

.settings-input {
  height: 38px;
  border: 1px solid #cbd5e1;
  border-radius: 6px;
  padding: 0.4rem 0.75rem;
  font-size: 0.875rem;
  color: #1e293b;
  background: #ffffff;
  transition: border-color 0.15s ease, box-shadow 0.15s ease;
  width: 100%;
}

.settings-input:focus {
  outline: none;
  border-color: var(--tecnm-blue-primary, #1b396a);
  box-shadow: 0 0 0 3px rgba(27, 57, 106, 0.12);
}

.settings-hint {
  font-size: 0.725rem;
  color: #94a3b8;
  margin-top: 0.35rem;
  line-height: 1.3;
}

/* Switch Option Cards */
.settings-switches-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
  gap: 1.25rem;
}

.switch-option-card {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 1.25rem;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  gap: 1rem;
}

.switch-option-card--warning {
  border-color: #fed7aa;
  border-left: 4px solid #f59e0b;
  background: #fffdfa;
}

.switch-card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
}

.switch-card-title {
  margin: 0 0 0.3rem 0;
  font-size: 0.95rem;
  font-weight: 700;
  color: #1e293b;
}

.switch-card-desc {
  margin: 0;
  font-size: 0.825rem;
  color: #64748b;
  line-height: 1.45;
}

.switch-card-footer {
  display: flex;
  align-items: center;
}

.badge-pill-blue {
  background: #eff6ff;
  color: #0369a1;
  border: 1px solid #bfdbfe;
  padding: 0.25rem 0.65rem;
  border-radius: 9999px;
  font-size: 0.75rem;
  font-weight: 600;
}

.badge-pill-gold {
  color: #b45309;
  font-size: 0.75rem;
  font-weight: 600;
}

.settings-actions-footer {
  margin-top: 2rem;
  padding-top: 1.25rem;
  border-top: 1px solid #e2e8f0;
}

/* SMTP Test Live Box */
.smtp-test-box {
  margin-top: 2.25rem;
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 1.25rem 1.5rem;
  display: flex;
  gap: 1.25rem;
  align-items: flex-start;
}

.smtp-test-icon-badge {
  width: 38px;
  height: 38px;
  border-radius: 50%;
  background: #e0f2fe;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.smtp-test-content {
  flex: 1;
}

.smtp-test-title {
  margin: 0 0 0.25rem 0;
  font-size: 1rem;
  font-weight: 700;
  color: var(--tecnm-blue-primary, #1b396a);
}

.smtp-test-desc {
  margin: 0 0 0.85rem 0;
  font-size: 0.85rem;
  color: #64748b;
  line-height: 1.4;
}

.smtp-test-form-row {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
  max-width: 650px;
}

.smtp-test-input {
  flex: 1;
  min-width: 260px;
}

/* Template View Elements */
.settings-template-actions {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
  align-items: center;
}

.tecnm-btn-outline-danger {
  background: #ffffff;
  border: 1px solid #fecaca;
  color: #dc2626;
  font-weight: 600;
}

.tecnm-btn-outline-danger:hover {
  background: #fef2f2;
  border-color: #f87171;
  color: #b91c1c;
}

/* Word Info Card */
.word-info-card {
  background-color: #f0fdf4;
  border: 1px solid #bbf7d0;
  border-radius: 8px;
  padding: 1.25rem;
  margin-bottom: 1.5rem;
}

.word-info-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.4rem;
}

.word-info-title {
  margin: 0;
  font-size: 0.95rem;
  font-weight: 700;
  color: #166534;
}

.word-info-desc {
  margin: 0 0 1rem 0;
  font-size: 0.85rem;
  color: #15803d;
  line-height: 1.5;
}

.word-variables-section {
  border-top: 1px dashed #86efac;
  padding-top: 0.85rem;
}

.word-variables-title {
  display: block;
  font-size: 0.825rem;
  font-weight: 700;
  color: #166534;
  margin-bottom: 0.6rem;
}

.word-variables-grid {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.var-pill-btn {
  background: #ffffff;
  border: 1px solid #86efac;
  border-radius: 4px;
  padding: 4px 8px;
  font-size: 0.8rem;
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  cursor: pointer;
  transition: all 0.15s ease;
}

.var-pill-btn:hover {
  background: #dcfce7;
  border-color: #4ade80;
  transform: translateY(-1px);
}

.var-pill-code {
  color: #166534;
  font-family: monospace;
  font-weight: 700;
}

.var-pill-label {
  color: #475569;
  font-size: 0.775rem;
}

/* Template Split Grid */
.template-split-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.25rem;
}

@media (max-width: 1024px) {
  .template-split-grid {
    grid-template-columns: 1fr;
  }
}

.template-editor-column,
.template-preview-column {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.column-box-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem 1rem;
  background: #f8fafc;
  border-bottom: 1px solid #e2e8f0;
}

.column-box-title-group {
  display: flex;
  align-items: center;
  gap: 0.4rem;
}

.column-box-title {
  font-size: 0.9rem;
  font-weight: 700;
  color: var(--tecnm-blue-primary, #1b396a);
}

.char-count-badge {
  background: #f1f5f9;
  border: 1px solid #e2e8f0;
  color: #64748b;
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 0.75rem;
  font-weight: 500;
}

.preview-badge-status {
  background: #dcfce7;
  border: 1px solid #bbf7d0;
  color: #15803d;
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 0.75rem;
  font-weight: 600;
}

.editor-textarea-wrapper {
  padding: 0.75rem;
  background: #f8fafc;
  flex: 1;
}

.template-code-textarea {
  width: 100%;
  height: 520px;
  font-family: 'Fira Code', 'Consolas', 'Courier New', monospace;
  font-size: 0.825rem;
  line-height: 1.5;
  color: #1e293b;
  background: #ffffff;
  border: 1px solid #cbd5e1;
  border-radius: 6px;
  padding: 0.75rem;
  resize: vertical;
  white-space: pre;
}

.template-code-textarea:focus {
  outline: none;
  border-color: var(--tecnm-blue-primary, #1b396a);
}

.editor-column-footer {
  padding: 0.75rem 1rem;
  border-top: 1px solid #e2e8f0;
  background: #ffffff;
  display: flex;
  justify-content: center;
}

.preview-iframe-wrapper {
  padding: 0.75rem;
  background: #f8fafc;
  flex: 1;
  min-height: 580px;
  display: flex;
}

.template-preview-iframe {
  width: 100%;
  height: 100%;
  min-height: 560px;
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.05);
}
</style>

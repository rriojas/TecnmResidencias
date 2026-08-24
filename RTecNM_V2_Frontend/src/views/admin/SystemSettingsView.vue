<script setup>
import { ref, onMounted } from 'vue'
import apiClient from '@/services/api'
import { useConfirm } from '@/composables/useConfirm'

const { confirm } = useConfirm()

const activeTab = ref('smtp') // 'smtp' | 'template'

// Alert state
const alertMessage = ref('')
const alertType = ref('success')

function showAlert(msg, type = 'success') {
  alertMessage.value = msg
  alertType.value = type
  setTimeout(() => { alertMessage.value = '' }, 5000)
}

// SMTP State
const smtpForm = ref({
  host: 'smtp.gmail.com',
  port: 587,
  senderName: 'TecNM Residencias Monclova',
  senderEmail: '',
  username: '',
  password: '',
  enableSsl: true,
  useMockInDev: true
})

const isLoadingSmtp = ref(false)
const isSavingSmtp = ref(false)
const testEmail = ref('')
const isTestingSmtp = ref(false)

// Template State

async function loadSmtpConfig() {
  isLoadingSmtp.value = true
  try {
    const res = await apiClient.get('/v1/system/settings/smtp')
    smtpForm.value = res.data
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
    showAlert('Configuración SMTP guardada correctamente.', 'success')
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

const templateHtml = ref('')
const isLoadingTemplate = ref(false)
const isSavingTemplate = ref(false)
const fileInputRef = ref(null)
const wordFileInputRef = ref(null)
const isUploadingWord = ref(false)
const editorMode = ref('split')

async function loadTemplate() {
  isLoadingTemplate.value = true
  try {
    const res = await apiClient.get('/v1/system/settings/template/presentation-letter')
    templateHtml.value = res.data.templateHtml
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
    showAlert('Plantilla guardada y actualizada correctamente.', 'success')
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
  loadSmtpConfig()
  loadTemplate()
})
</script>

<template>
  <div class="tecnm-settings-page">
    <!-- Header -->
    <div class="tecnm-actions-bar">
      <div>
        <h1 class="tecnm-page-title">Configuración del Sistema y Vinculación</h1>
        <p class="tecnm-page-subtitle">Gestión de Servidor de Correo SMTP y Plantillas Oficiales</p>
      </div>
    </div>

    <!-- Alert -->
    <div
      v-if="alertMessage"
      class="tecnm-alert"
      :class="`tecnm-alert-${alertType}`"
      role="alert"
    >
      <span>{{ alertMessage }}</span>
    </div>

    <!-- Tabs Navigation -->
    <div class="tecnm-tabs-nav" style="margin-bottom: 1.5rem; display: flex; gap: 0.5rem; border-bottom: 2px solid var(--tecnm-border, #e5e7eb); padding-bottom: 0.5rem;">
      <button
        type="button"
        class="tecnm-btn"
        :class="activeTab === 'smtp' ? 'tecnm-btn-primary' : 'tecnm-btn-secondary'"
        @click="activeTab = 'smtp'"
      >
        📧 Servidor SMTP (Correos)
      </button>
      <button
        type="button"
        class="tecnm-btn"
        :class="activeTab === 'template' ? 'tecnm-btn-primary' : 'tecnm-btn-secondary'"
        @click="activeTab = 'template'"
      >
        📄 Plantilla Carta de Presentación
      </button>
    </div>

    <!-- Tab 1: Servidor SMTP -->
    <div v-if="activeTab === 'smtp'" class="tecnm-card">
      <div class="tecnm-card-header">
        <h3 class="tecnm-card-title">Parámetros del Servidor de Correo Saliente</h3>
      </div>

      <div class="tecnm-card-body">
        <div v-if="isLoadingSmtp" style="text-align: center; padding: 2rem;">
          <div class="tecnm-spinner" style="margin: 0 auto 1rem auto;"></div>
          <p>Cargando parámetros SMTP...</p>
        </div>

        <form v-else @submit.prevent="handleSaveSmtp">
          <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 1.25rem;">
            <div class="tecnm-form-group">
              <label class="tecnm-form-label">Servidor SMTP (Host) *</label>
              <input v-model="smtpForm.host" type="text" class="tecnm-form-input" placeholder="smtp.gmail.com" required />
            </div>

            <div class="tecnm-form-group">
              <label class="tecnm-form-label">Puerto SMTP *</label>
              <input v-model.number="smtpForm.port" type="number" class="tecnm-form-input" placeholder="587" required />
            </div>

            <div class="tecnm-form-group">
              <label class="tecnm-form-label">Nombre del Remitente *</label>
              <input v-model="smtpForm.senderName" type="text" class="tecnm-form-input" placeholder="TecNM Residencias Monclova" required />
            </div>

            <div class="tecnm-form-group">
              <label class="tecnm-form-label">Correo Remitente *</label>
              <input v-model="smtpForm.senderEmail" type="email" class="tecnm-form-input" placeholder="residencias@monclova.tecnm.mx" required />
            </div>

            <div class="tecnm-form-group">
              <label class="tecnm-form-label">Usuario SMTP / Cuenta</label>
              <input v-model="smtpForm.username" type="text" class="tecnm-form-input" placeholder="tu_correo@gmail.com" />
            </div>

            <div class="tecnm-form-group">
              <label class="tecnm-form-label">Contraseña SMTP / App Password</label>
              <input v-model="smtpForm.password" type="password" class="tecnm-form-input" placeholder="••••••••••••" />
            </div>
          </div>

          <div style="margin-top: 1.25rem; display: flex; flex-direction: column; gap: 0.75rem;">
            <label style="display: flex; align-items: center; gap: 0.5rem; cursor: pointer; font-weight: 500;">
              <input v-model="smtpForm.enableSsl" type="checkbox" />
              <span>Habilitar Seguridad TLS / SSL</span>
            </label>

            <label style="display: flex; align-items: center; gap: 0.5rem; cursor: pointer; font-weight: 600; color: #d97706;">
              <input v-model="smtpForm.useMockInDev" type="checkbox" />
              <span>Modo Simulación (Mock) — Los correos no saldrán a servidores reales y solo se registrarán en la consola.</span>
            </label>
          </div>

          <div style="margin-top: 1.5rem; display: flex; gap: 1rem; border-top: 1px solid #e5e7eb; padding-top: 1.25rem;">
            <button type="submit" class="tecnm-btn tecnm-btn-primary" :disabled="isSavingSmtp">
              {{ isSavingSmtp ? 'Guardando...' : 'Guardar Configuración SMTP' }}
            </button>
          </div>
        </form>

        <!-- Prueba SMTP -->
        <div style="margin-top: 2.5rem; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 1.25rem;">
          <h4 style="margin-top: 0; color: #1B396A; font-size: 1.1rem;">Probar Conexión SMTP</h4>
          <p style="font-size: 0.9rem; color: #64748b; margin-bottom: 1rem;">
            Envía un correo de prueba en vivo para verificar que los datos SMTP ingresados sean válidos y funcionen correctamente.
          </p>
          <div style="display: flex; gap: 0.75rem; max-width: 600px; flex-wrap: wrap;">
            <input v-model="testEmail" type="email" class="tecnm-form-input" style="flex: 1; min-width: 250px;" placeholder="Ingresa tu correo personal para probar" />
            <button type="button" class="tecnm-btn tecnm-btn-secondary" :disabled="isTestingSmtp" @click="handleTestSmtp">
              {{ isTestingSmtp ? 'Enviando...' : 'Enviar Correo de Prueba' }}
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Tab 2: Plantilla Word / HTML -->
    <div v-if="activeTab === 'template'" class="tecnm-card">
      <div class="tecnm-card-header" style="display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 0.75rem;">
        <h3 class="tecnm-card-title">Plantilla Oficial para Carta de Presentación</h3>
        <div style="display: flex; gap: 0.5rem; flex-wrap: wrap;">
          <button type="button" class="tecnm-btn tecnm-btn-primary tecnm-btn-sm" :disabled="isUploadingWord" @click="triggerWordUpload">
            {{ isUploadingWord ? 'Procesando Word...' : 'Cargar Plantilla Word (.docx)' }}
          </button>
          <input ref="wordFileInputRef" type="file" accept=".docx,.doc" style="display: none;" @change="handleWordFileUpload" />
          <button type="button" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" @click="handleDownloadTemplate">
            Descargar .html
          </button>
          <button type="button" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" @click="triggerFileUpload">
            Subir .html
          </button>
          <input ref="fileInputRef" type="file" accept=".html,.htm" style="display: none;" @change="handleFileUpload" />
          <button type="button" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" @click="handleResetTemplate">
            Restablecer Formato
          </button>
        </div>
      </div>

      <div class="tecnm-card-body">
        <!-- Panel de Carga Word y Variables -->
        <div style="background-color: #f0fdf4; border: 1px solid #bbf7d0; border-radius: 6px; padding: 1rem; margin-bottom: 1.25rem;">
          <p style="margin: 0 0 0.5rem 0; font-weight: bold; color: #166534;">Carga Oficial desde Documento de Word (.docx):</p>
          <p style="margin: 0; font-size: 0.85rem; color: #15803d; line-height: 1.5;">
            Sube directamente la carta membretada oficial en formato <strong>Word (.docx)</strong>. El sistema la convertirá automáticamente a estructura HTML conservando estilos, formato y los marcadores variables entre corchetes <code>[...]</code>.
          </p>
          <div style="margin-top: 0.75rem; border-top: 1px dashed #86efac; padding-top: 0.75rem;">
            <p style="margin: 0 0 0.35rem 0; font-weight: bold; font-size: 0.85rem; color: #166534;">Variables dinámicas a colocar en el archivo Word entre corchetes [ ]:</p>
            <div style="display: flex; gap: 0.5rem; flex-wrap: wrap; font-family: monospace; font-size: 0.85rem;">
              <span style="background: #ffffff; padding: 2px 6px; border-radius: 4px; border: 1px solid #86efac; font-weight: bold;">[NOMBRE_ALUMNO]</span>
              <span style="background: #ffffff; padding: 2px 6px; border-radius: 4px; border: 1px solid #86efac; font-weight: bold;">[MATRICULA]</span>
              <span style="background: #ffffff; padding: 2px 6px; border-radius: 4px; border: 1px solid #86efac; font-weight: bold;">[CARRERA]</span>
              <span style="background: #ffffff; padding: 2px 6px; border-radius: 4px; border: 1px solid #86efac; font-weight: bold;">[EMPRESA]</span>
              <span style="background: #ffffff; padding: 2px 6px; border-radius: 4px; border: 1px solid #86efac; font-weight: bold;">[FECHA]</span>
              <span style="background: #ffffff; padding: 2px 6px; border-radius: 4px; border: 1px solid #86efac; font-weight: bold;">[FOLIO]</span>
            </div>
          </div>
        </div>

        <!-- Selector de Modo de Visualización -->
        <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; flex-wrap: wrap; gap: 0.75rem; background: #f8fafc; padding: 0.75rem 1rem; border-radius: 8px; border: 1px solid #e2e8f0;">
          <div style="display: flex; align-items: center; gap: 0.5rem;">
            <span style="font-size: 0.85rem; font-weight: bold; color: #475569;">Modo de Vista:</span>
            <div style="display: flex; gap: 0.25rem; background: #e2e8f0; padding: 3px; border-radius: 6px;">
              <button
                type="button"
                class="tecnm-btn tecnm-btn-sm"
                :style="editorMode === 'preview' ? 'background: #ffffff; color: #1e293b; font-weight: bold; box-shadow: 0 1px 3px rgba(0,0,0,0.1);' : 'background: transparent; color: #64748b; border: none;'"
                @click="editorMode = 'preview'"
              >
                Vista Previa
              </button>
              <button
                type="button"
                class="tecnm-btn tecnm-btn-sm"
                :style="editorMode === 'code' ? 'background: #ffffff; color: #1e293b; font-weight: bold; box-shadow: 0 1px 3px rgba(0,0,0,0.1);' : 'background: transparent; color: #64748b; border: none;'"
                @click="editorMode = 'code'"
              >
                Código Fuente
              </button>
              <button
                type="button"
                class="tecnm-btn tecnm-btn-sm"
                :style="editorMode === 'split' ? 'background: #ffffff; color: #1e293b; font-weight: bold; box-shadow: 0 1px 3px rgba(0,0,0,0.1);' : 'background: transparent; color: #64748b; border: none;'"
                @click="editorMode = 'split'"
              >
                Lado a Lado
              </button>
            </div>
          </div>

          <button
            type="button"
            class="tecnm-btn tecnm-btn-primary tecnm-btn-sm"
            :disabled="isSavingTemplate"
            @click="handleSaveTemplate"
          >
            {{ isSavingTemplate ? 'Guardando...' : 'Guardar Cambios en Plantilla' }}
          </button>
        </div>

        <div v-if="isLoadingTemplate" style="text-align: center; padding: 3rem;">
          <div class="tecnm-spinner" style="margin: 0 auto 1rem auto;"></div>
          <p style="color: #64748b;">Cargando código de la plantilla...</p>
        </div>

        <div v-else>
          <!-- Modo 1: Código Fuente Completo (100% Ancho) -->
          <div v-if="editorMode === 'code'">
            <label class="tecnm-form-label" style="font-weight: bold; margin-bottom: 0.5rem; display: block;">Código Fuente HTML (Editor Completo)</label>
            <textarea
              v-model="templateHtml"
              rows="28"
              class="tecnm-form-input"
              style="font-family: 'Fira Code', 'Consolas', 'Monaco', monospace; font-size: 0.9rem; line-height: 1.6; background-color: #0f172a; color: #38bdf8; border: 1px solid #1e293b; padding: 1.25rem; border-radius: 8px; width: 100%; white-space: pre;"
              placeholder="Ingresa o edita el código HTML aquí..."
            ></textarea>
          </div>

          <!-- Modo 2: Vista Previa Completa (100% Ancho) -->
          <div v-else-if="editorMode === 'preview'">
            <label class="tecnm-form-label" style="font-weight: bold; margin-bottom: 0.5rem; display: block;">Vista Previa en Vivo (Pantalla Completa)</label>
            <div style="border: 1px solid #cbd5e1; border-radius: 8px; height: 680px; overflow: hidden; background: #ffffff; box-shadow: 0 4px 12px rgba(0,0,0,0.05);">
              <iframe
                :srcdoc="templateHtml"
                style="width: 100%; height: 100%; border: none;"
                title="Vista previa de plantilla completa"
              ></iframe>
            </div>
          </div>

          <!-- Modo 3: Vista Dividida Lado a Lado -->
          <div v-else style="display: grid; grid-template-columns: repeat(auto-fit, minmax(400px, 1fr)); gap: 1.5rem;">
            <!-- Editor HTML -->
            <div>
              <label class="tecnm-form-label" style="font-weight: bold; margin-bottom: 0.5rem; display: block;">Código Fuente HTML</label>
              <textarea
                v-model="templateHtml"
                rows="24"
                class="tecnm-form-input"
                style="font-family: 'Fira Code', 'Consolas', 'Monaco', monospace; font-size: 0.85rem; line-height: 1.5; background-color: #0f172a; color: #38bdf8; border: 1px solid #1e293b; padding: 1rem; border-radius: 8px; width: 100%; white-space: pre;"
              ></textarea>
            </div>

            <!-- Vista Previa Live -->
            <div>
              <label class="tecnm-form-label" style="font-weight: bold; margin-bottom: 0.5rem; display: block;">Vista Previa en Vivo</label>
              <div style="border: 1px solid #cbd5e1; border-radius: 8px; height: 560px; overflow: hidden; background: #ffffff; box-shadow: 0 2px 8px rgba(0,0,0,0.05);">
                <iframe
                  :srcdoc="templateHtml"
                  style="width: 100%; height: 100%; border: none;"
                  title="Vista previa de plantilla"
                ></iframe>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

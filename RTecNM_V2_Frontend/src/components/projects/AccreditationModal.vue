<script setup>
import { ref, watch, computed } from 'vue'
import TecnmModal from '@/components/common/TecnmModal.vue'
import apiClient from '@/services/api'

const props = defineProps({
  modelValue: {
    type: Boolean,
    default: false,
  },
  isResubmit: {
    type: Boolean,
    default: false,
  },
  projectId: {
    type: [Number, String],
    default: null,
  },
  previousObservations: {
    type: String,
    default: '',
  },
})

const emit = defineEmits(['update:modelValue', 'success'])

const eventType = ref('innovatec')
const projectTitle = ref('')
const selectedFile = ref(null)
const fileName = ref('')
const fileSizeText = ref('')
const fileError = ref('')
const errorMessage = ref('')
const isSubmitting = ref(false)

const modalTitle = computed(() => {
  return props.isResubmit
    ? 'Sustituir Constancia de Acreditación'
    : 'Acreditación por InnovaTecNM Nacional'
})

watch(
  () => props.modelValue,
  (val) => {
    if (val) {
      errorMessage.value = ''
      fileError.value = ''
      selectedFile.value = null
      fileName.value = ''
      fileSizeText.value = ''
      if (!props.isResubmit) {
        eventType.value = 'innovatec'
        projectTitle.value = ''
      }
    }
  }
)

function closeModal() {
  if (isSubmitting.value) return
  emit('update:modelValue', false)
}

function handleFileChange(event) {
  fileError.value = ''
  const file = event.target.files?.[0]
  if (!file) {
    selectedFile.value = null
    fileName.value = ''
    fileSizeText.value = ''
    return
  }

  const allowedExtensions = ['.pdf', '.jpg', '.jpeg', '.png']
  const ext = '.' + file.name.split('.').pop().toLowerCase()
  if (!allowedExtensions.includes(ext)) {
    fileError.value = 'Formato no válido. Solo se admiten documentos PDF o imágenes (JPG, PNG).'
    selectedFile.value = null
    fileName.value = ''
    fileSizeText.value = ''
    return
  }

  if (file.size > 10 * 1024 * 1024) {
    fileError.value = 'El archivo supera el tamaño máximo permitido de 10MB.'
    selectedFile.value = null
    fileName.value = ''
    fileSizeText.value = ''
    return
  }

  selectedFile.value = file
  fileName.value = file.name
  fileSizeText.value = (file.size / (1024 * 1024)).toFixed(2) + ' MB'
}

async function handleSubmit() {
  errorMessage.value = ''
  fileError.value = ''

  if (!props.isResubmit && !projectTitle.value.trim()) {
    errorMessage.value = 'Por favor indique el nombre del proyecto o solución acreditada en InnovaTecNM Nacional.'
    return
  }

  if (!selectedFile.value) {
    fileError.value = 'Debe adjuntar la constancia oficial de acreditación.'
    return
  }

  isSubmitting.value = true
  const formData = new FormData()
  formData.append('file', selectedFile.value)

  try {
    if (props.isResubmit) {
      await apiClient.post(`/v1/projects/${props.projectId}/accreditation/resubmit`, formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
    } else {
      formData.append('eventType', 'innovatec')
      formData.append('projectTitle', projectTitle.value.trim())
      await apiClient.post('/v1/projects/accreditation', formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
    }

    emit('success')
    emit('update:modelValue', false)
  } catch (err) {
    errorMessage.value =
      err.response?.data?.message || 'Error al procesar la solicitud de acreditación. Intente nuevamente.'
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <TecnmModal
    :model-value="modelValue"
    :title="modalTitle"
    max-width="620px"
    @close="closeModal"
  >
    <!-- Alerta de Observaciones Previas si es re-envío -->
    <div
      v-if="isResubmit && previousObservations"
      class="tecnm-alert tecnm-alert-warning tecnm-mb-3"
      role="alert"
    >
      <strong>Observaciones de la Jefatura de Carrera:</strong>
      <p class="tecnm-mt-1 tecnm-mb-0">{{ previousObservations }}</p>
    </div>

    <!-- Panel Informativo: Guía para el Estudiante -->
    <div v-if="!isResubmit" class="tecnm-alert tecnm-alert-info tecnm-mb-3" role="alert">
      <strong>Instrucciones para Acreditación Directa:</strong>
      <ul class="tecnm-mt-1 tecnm-mb-0" style="padding-left: 1.25rem; line-height: 1.45;">
        <li>Esta modalidad es exclusiva para estudiantes acreditados en la <strong>Cumbre Nacional InnovaTecNM a nivel nacional</strong>.</li>
        <li><strong>No requieres</strong> anteproyecto ordinario, asesor docente ni registro de objetivos.</li>
        <li>Sube tu constancia oficial nacional con sellos y firmas legibles (PDF o imagen, máx. 10MB).</li>
        <li>La Jefatura de Carrera o Administración validará el documento para <strong>liberar tu residencia con calificación de 100%</strong>.</li>
      </ul>
    </div>

    <!-- Error de Validación / Backend -->
    <div
      v-if="errorMessage"
      class="tecnm-alert tecnm-alert-danger tecnm-mb-3"
      role="alert"
    >
      <span>{{ errorMessage }}</span>
    </div>

    <form @submit.prevent="handleSubmit">
      <!-- Evento Oficial de Participación -->
      <div v-if="!isResubmit" class="tecnm-form-group">
        <label class="tecnm-label">
          Certamen Institucional de Acreditación
        </label>
        <div class="tecnm-field-value-box tecnm-field-value-emphasis">
          Cumbre Nacional de Desarrollo Tecnológico, Investigación e Innovación — InnovaTecNM Nacional
        </div>
      </div>

      <!-- Nombre del Proyecto / Solución -->
      <div v-if="!isResubmit" class="tecnm-form-group">
        <label class="tecnm-label" for="modalProjectTitle">
          Nombre del Proyecto o Solución <span class="tecnm-text-danger">*</span>
        </label>
        <input
          id="modalProjectTitle"
          v-model="projectTitle"
          type="text"
          class="tecnm-form-control"
          placeholder="Ej. Sistema Inteligente de Monitoreo Agropecuario"
          maxlength="250"
          :disabled="isSubmitting"
          required
        />
        <span class="tecnm-form-hint">Nombre oficial del proyecto con el que se acreditó en InnovaTecNM Nacional.</span>
      </div>

      <!-- Subida de Archivo de Constancia -->
      <div class="tecnm-form-group">
        <label class="tecnm-label" for="modalAccreditationFile">
          {{ isResubmit ? 'Nueva Constancia Oficial Corregida' : 'Constancia Oficial de InnovaTecNM Nacional' }}
          <span class="tecnm-text-danger">*</span>
        </label>
        <input
          id="modalAccreditationFile"
          type="file"
          class="tecnm-form-control"
          accept=".pdf,.jpg,.jpeg,.png"
          :disabled="isSubmitting"
          @change="handleFileChange"
        />
        <span v-if="fileName" class="tecnm-form-hint">
          Archivo seleccionado: <strong>{{ fileName }}</strong> ({{ fileSizeText }})
        </span>
        <span v-if="fileError" class="tecnm-form-error">
          {{ fileError }}
        </span>
        <span class="tecnm-form-hint">
          Formatos admitidos: PDF, JPG o PNG. Tamaño máximo: 10MB.
        </span>
      </div>
    </form>

    <template #footer>
      <button
        type="button"
        class="tecnm-btn tecnm-btn-secondary"
        :disabled="isSubmitting"
        @click="closeModal"
      >
        Cancelar
      </button>
      <button
        type="button"
        class="tecnm-btn tecnm-btn-primary"
        :disabled="isSubmitting"
        @click="handleSubmit"
      >
        <span v-if="isSubmitting">Enviando documento...</span>
        <span v-else>{{ isResubmit ? 'Re-enviar Constancia' : 'Enviar para Validación' }}</span>
      </button>
    </template>
  </TecnmModal>
</template>

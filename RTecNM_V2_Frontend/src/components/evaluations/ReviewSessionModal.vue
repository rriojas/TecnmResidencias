<script setup>
import { ref, watch } from 'vue'

const props = defineProps({
  isOpen: {
    type: Boolean,
    default: false
  },
  session: {
    type: Object,
    default: null
  },
  isSubmitting: {
    type: Boolean,
    default: false
  }
})

const emit = defineEmits(['close', 'submit'])

const form = ref({
  reviewStatus: 'approved',
  reviewNotes: ''
})

watch(
  () => props.session,
  (newVal) => {
    if (newVal) {
      form.value.reviewStatus = newVal.reviewStatus && newVal.reviewStatus !== 'pending'
        ? newVal.reviewStatus
        : 'approved'
      form.value.reviewNotes = newVal.reviewNotes || ''
    }
  },
  { immediate: true }
)

function handleSubmit() {
  emit('submit', {
    sessionId: props.session.id,
    reviewStatus: form.value.reviewStatus,
    reviewNotes: form.value.reviewNotes
  })
}
</script>

<template>
  <div
    class="modal-backdrop"
    :class="{ active: isOpen }"
    aria-modal="true"
    role="dialog"
  >
    <div class="modal-card" style="max-width: 540px;">
      <div class="tecnm-modal-header">
        <h3 class="tecnm-modal-title">Dictamen de Asesoría de Residencia</h3>
        <button
          type="button"
          class="tecnm-modal-close"
          aria-label="Cerrar"
          @click="emit('close')"
        >
          &times;
        </button>
      </div>

      <form @submit.prevent="handleSubmit">
        <div style="padding: 1.25rem;">
          <div v-if="session" style="background: #F8FAFC; border: 1px solid #E2E8F0; border-radius: 6px; padding: 0.75rem 1rem; margin-bottom: 1.25rem;">
            <div style="font-size: 0.75rem; color: #64748B; text-transform: uppercase; font-weight: 700;">Datos de la Sesión</div>
            <div style="font-weight: 600; color: #1E293B; font-size: 0.9rem; margin-top: 0.25rem;">
              Alumno: {{ session.studentName }} ({{ session.careerName }})
            </div>
            <div style="font-size: 0.85rem; color: #475569;">
              Asesor: {{ session.advisorName }}
            </div>
            <div style="font-size: 0.8rem; color: #64748B; margin-top: 0.25rem;">
              Proyecto: {{ session.projectTitle }}
            </div>
          </div>

          <!-- Selección de Dictamen -->
          <div class="tecnm-form-group">
            <label class="tecnm-label">Resolución del Dictamen *</label>
            <div style="display: flex; gap: 1rem; margin-top: 0.5rem;">
              <label style="display: flex; align-items: center; gap: 0.5rem; cursor: pointer; font-size: 0.875rem;">
                <input
                  v-model="form.reviewStatus"
                  type="radio"
                  value="approved"
                  name="reviewStatus"
                />
                <span class="tecnm-badge tecnm-badge-approved">Aprobar / Conforme</span>
              </label>

              <label style="display: flex; align-items: center; gap: 0.5rem; cursor: pointer; font-size: 0.875rem;">
                <input
                  v-model="form.reviewStatus"
                  type="radio"
                  value="observed"
                  name="reviewStatus"
                />
                <span class="tecnm-badge tecnm-badge-rejected">Emitir Observaciones</span>
              </label>
            </div>
          </div>

          <!-- Observaciones / Comentarios -->
          <div class="tecnm-form-group" style="margin-top: 1rem;">
            <label class="tecnm-label" for="reviewNotes">
              Observaciones o Instrucciones de Jefatura
              <span v-if="form.reviewStatus === 'observed'" style="color: #DC2626;">*</span>
            </label>
            <textarea
              id="reviewNotes"
              v-model="form.reviewNotes"
              class="tecnm-form-control"
              rows="3"
              style="height: auto;"
              placeholder="Indique las instrucciones, requerimientos de entrega o aclaraciones pertinentes..."
              :required="form.reviewStatus === 'observed'"
            ></textarea>
          </div>
        </div>

        <div class="tecnm-modal-footer">
          <button
            type="button"
            class="tecnm-btn tecnm-btn-secondary"
            :disabled="isSubmitting"
            @click="emit('close')"
          >
            Cancelar
          </button>
          <button
            type="submit"
            class="tecnm-btn tecnm-btn-primary"
            :disabled="isSubmitting"
          >
            {{ isSubmitting ? 'Guardando Dictamen...' : 'Guardar Resolución' }}
          </button>
        </div>
      </form>
    </div>
  </div>
</template>

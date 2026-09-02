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

const emit = defineEmits(['close', 'submit', 'delete'])

const note = ref('')

watch(
  () => props.session,
  (newVal) => {
    if (newVal) {
      note.value = newVal.supervisionNotes || ''
    } else {
      note.value = ''
    }
  },
  { immediate: true }
)

function handleSubmit() {
  emit('submit', {
    sessionId: props.session.id,
    notes: note.value.trim()
  })
}

function handleDelete() {
  emit('submit', {
    sessionId: props.session.id,
    notes: null
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
        <h3 class="tecnm-modal-title">Nota de Supervisión de Jefatura</h3>
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
            <div style="font-size: 0.75rem; color: #64748B; text-transform: uppercase; font-weight: 700;">Datos de la Asesoría</div>
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

          <!-- Observaciones / Comentarios -->
          <div class="tecnm-form-group">
            <label class="tecnm-label" for="supervisionNote">
              Instrucción, Recordatorio u Observación de Jefatura
            </label>
            <p style="font-size: 0.75rem; color: #64748B; margin: -0.25rem 0 0.5rem 0;">
              Esta nota se registrará como evidencia interna de supervisión y seguimiento docente.
            </p>
            <textarea
              id="supervisionNote"
              v-model="note"
              class="tecnm-form-control"
              rows="4"
              style="height: auto;"
              placeholder="Escriba aquí si detecta retrasos, falta de congruencia en acuerdos o instrucciones para el asesor..."
              required
            ></textarea>
          </div>
        </div>

        <div class="tecnm-modal-footer" style="display: flex; justify-content: space-between; align-items: center;">
          <div>
            <button
              v-if="session?.supervisionNotes"
              type="button"
              class="tecnm-btn tecnm-btn-outline-danger tecnm-btn-sm"
              :disabled="isSubmitting"
              @click="handleDelete"
            >
              Quitar Observación
            </button>
          </div>

          <div style="display: flex; gap: 0.5rem;">
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
              {{ isSubmitting ? 'Guardando...' : 'Guardar Nota' }}
            </button>
          </div>
        </div>
      </form>
    </div>
  </div>
</template>

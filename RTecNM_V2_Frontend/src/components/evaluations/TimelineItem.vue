<script setup>
import { computed } from 'vue'

const props = defineProps({
  session: {
    type: Object,
    required: true
  },
  canSupervise: {
    type: Boolean,
    default: false
  }
})

const emit = defineEmits(['add-note'])

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

const hasNote = computed(() => {
  return Boolean(props.session.supervisionNotes && props.session.supervisionNotes.trim())
})

const advisorInitials = computed(() => {
  const name = props.session.advisorName || 'Asesor'
  const parts = name.split(' ').filter(Boolean)
  if (parts.length >= 2) {
    return (parts[0][0] + parts[1][0]).toUpperCase()
  }
  return name.slice(0, 2).toUpperCase()
})

const studentInitials = computed(() => {
  const name = props.session.studentName || 'Estudiante'
  const parts = name.split(' ').filter(Boolean)
  if (parts.length >= 2) {
    return (parts[0][0] + parts[1][0]).toUpperCase()
  }
  return name.slice(0, 2).toUpperCase()
})
</script>

<template>
  <div class="tecnm-timeline-item">
    <!-- Nodo del Eje Cronológico -->
    <div
      class="tecnm-timeline-node"
      :class="{ observed: hasNote }"
    ></div>

    <!-- Tarjeta de Contenido del Evento -->
    <div class="tecnm-timeline-card">
      <!-- Cabecera de la Tarjeta -->
      <div class="tecnm-timeline-card-header">
        <div style="display: flex; align-items: center; gap: 0.75rem; flex-wrap: wrap;">
          <span class="tecnm-timeline-date-badge">
            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 0 1 2.25-2.25h13.5A2.25 2.25 0 0 1 21 7.5v11.25m-18 0A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75m-18 0v-7.5A2.25 2.25 0 0 1 5.25 9h13.5A2.25 2.25 0 0 1 21 11.25v7.5" />
            </svg>
            <span>Sesión: {{ formatTecNMDate(session.sessionDate) }}</span>
          </span>

          <span v-if="hasNote" class="tecnm-badge tecnm-badge-rejected">
            ⚠️ Con Observación de Jefatura
          </span>
          <span v-else class="tecnm-badge tecnm-badge-approved">
            Registrada
          </span>
        </div>

        <div style="font-size: var(--tecnm-font-size-xs, 0.75rem); color: var(--tecnm-text-secondary, #64748B);">
          Asesoría #{{ session.id }}
        </div>
      </div>

      <!-- Actores: Alumno y Asesor -->
      <div class="tecnm-timeline-actors">
        <!-- Asesor -->
        <div class="tecnm-timeline-actor-box">
          <div class="tecnm-timeline-actor-avatar" title="Asesor Académico">
            {{ advisorInitials }}
          </div>
          <div class="tecnm-timeline-actor-details">
            <div class="tecnm-timeline-actor-role">Asesor Académico</div>
            <div class="tecnm-timeline-actor-name" :title="session.advisorName">
              {{ session.advisorTitle ? `${session.advisorTitle} ` : '' }}{{ session.advisorName }}
            </div>
            <div class="tecnm-timeline-actor-meta">
              {{ session.advisorEmail || 'Docente TecNM' }}
            </div>
          </div>
        </div>

        <!-- Alumno -->
        <div class="tecnm-timeline-actor-box">
          <div class="tecnm-timeline-actor-avatar student" title="Estudiante Residente">
            {{ studentInitials }}
          </div>
          <div class="tecnm-timeline-actor-details">
            <div class="tecnm-timeline-actor-role">Estudiante Residente</div>
            <div class="tecnm-timeline-actor-name" :title="session.studentName">
              {{ session.studentName }}
            </div>
            <div class="tecnm-timeline-actor-meta">
              Ctrl: {{ session.studentControlNumber }} • {{ session.careerName }}
            </div>
          </div>
        </div>
      </div>

      <!-- Proyecto Vinculado -->
      <div style="margin-bottom: 0.75rem; background: #FAF5FF; border: 1px solid #E9D5FF; border-radius: 4px; padding: 0.5rem 0.75rem;">
        <span style="font-size: 0.7rem; font-weight: 700; text-transform: uppercase; color: #6B21A8; display: block;">Proyecto de Residencia:</span>
        <span style="font-size: 0.85rem; font-weight: 600; color: #3B0764;">{{ session.projectTitle }}</span>
      </div>

      <!-- Cuerpo: Temas y Acuerdos -->
      <div class="tecnm-timeline-body">
        <div>
          <div class="tecnm-timeline-section-title">Temas y Avances Abordados</div>
          <p style="margin: 0; line-height: 1.5; color: var(--tecnm-text-primary, #1E293B);">
            {{ session.topicsCovered }}
          </p>
        </div>

        <div v-if="session.studentAgreements">
          <div class="tecnm-timeline-section-title">Acuerdos y Compromisos del Estudiante</div>
          <p style="margin: 0; line-height: 1.5; color: var(--tecnm-text-secondary, #64748B); font-style: italic;">
            "{{ session.studentAgreements }}"
          </p>
        </div>

        <!-- Alerta de Observación de Supervisión si existe -->
        <div v-if="hasNote" class="tecnm-timeline-review-alert">
          <strong>Observación de Jefatura:</strong> {{ session.supervisionNotes }}
          <div v-if="session.supervisedAt" style="font-size: 0.7rem; margin-top: 0.25rem; opacity: 0.85;">
            Registrada el {{ formatTecNMDate(session.supervisedAt) }}
          </div>
        </div>
      </div>

      <!-- Pie de Tarjeta: Auditoría y Acción de Nota de Supervisión -->
      <div class="tecnm-timeline-card-footer">
        <div style="font-size: 0.75rem; color: var(--tecnm-text-secondary, #64748B);">
          <span>Registrada en bitácora: {{ formatTecNMDate(session.createdAt) }}</span>
        </div>

        <div>
          <button
            v-if="canSupervise"
            type="button"
            class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
            @click="emit('add-note', session)"
          >
            <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="m16.862 4.487 1.687-1.688a1.875 1.875 0 1 1 2.652 2.652L10.582 16.07a4.5 4.5 0 0 1-1.897 1.13L6 18l.8-2.685a4.5 4.5 0 0 1 1.13-1.897l8.932-8.931Zm0 0L19.5 7.125" />
            </svg>
            <span>{{ hasNote ? 'Editar Nota de Jefatura' : 'Agregar Nota de Jefatura' }}</span>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

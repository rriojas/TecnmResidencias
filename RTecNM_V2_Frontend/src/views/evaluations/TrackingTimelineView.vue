<script setup>
import { computed } from 'vue'
import { useAuthStore } from '@/stores/auth'
import AdvisorTrackingTimeline from '@/components/evaluations/AdvisorTrackingTimeline.vue'

const authStore = useAuthStore()

const careerLabel = computed(() => {
  if (authStore.isCareerHead && authStore.userCareerName) {
    return ` — ${authStore.userCareerName}`
  }
  return ''
})
</script>

<template>
  <div>
    <!-- Barra de Título Institucional -->
    <div class="tecnm-actions-bar">
      <div>
        <h1 class="tecnm-page-title">Supervisión de Asesorías{{ careerLabel }}</h1>
        <p class="tecnm-page-subtitle">
          Monitoreo continuo de seguimiento docente, ritmo de reuniones y detección temprana de inactividad
        </p>
      </div>
    </div>

    <!-- Centro de Supervisión y Línea de Tiempo de Asesores -->
    <AdvisorTrackingTimeline />
  </div>
</template>

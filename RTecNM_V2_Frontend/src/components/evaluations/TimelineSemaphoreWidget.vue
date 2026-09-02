<script setup>
defineProps({
  summary: {
    type: Object,
    default: () => ({
      healthyCount: 0,
      warningCount: 0,
      criticalCount: 0,
      irregularCount: 0,
      totalAdvisors: 0,
      totalSessions: 0,
      pendingReviewSessions: 0
    })
  },
  activeFilter: {
    type: String,
    default: 'all' // all | healthy | warning | critical | irregular
  }
})

const emit = defineEmits(['select-filter'])

function handleCardClick(status) {
  emit('select-filter', status)
}
</script>

<template>
  <div class="tecnm-semaphore-grid">
    <!-- Al Día (Verde) -->
    <div
      class="tecnm-semaphore-card tecnm-semaphore-healthy"
      :class="{ active: activeFilter === 'healthy' }"
      role="button"
      tabindex="0"
      @click="handleCardClick('healthy')"
      @keydown.enter="handleCardClick('healthy')"
    >
      <div class="tecnm-semaphore-header">
        <span class="tecnm-semaphore-title">Al Día (&le; 14 días)</span>
        <div class="tecnm-semaphore-icon" title="Seguimiento constante">
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
            <path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5" />
          </svg>
        </div>
      </div>
      <div class="tecnm-semaphore-count">{{ summary.healthyCount }}</div>
      <div class="tecnm-semaphore-desc">Asesores con avance regular y al corriente</div>
    </div>

    <!-- Alerta Preventiva (Amarillo) -->
    <div
      class="tecnm-semaphore-card tecnm-semaphore-warning"
      :class="{ active: activeFilter === 'warning' }"
      role="button"
      tabindex="0"
      @click="handleCardClick('warning')"
      @keydown.enter="handleCardClick('warning')"
    >
      <div class="tecnm-semaphore-header">
        <span class="tecnm-semaphore-title">En Alerta (15-21 días)</span>
        <div class="tecnm-semaphore-icon" title="Riesgo de retraso">
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m9-.75a9 9 0 1 1-18 0 9 9 0 0 1 18 0Zm-9 3.75h.008v.008H12v-.008Z" />
          </svg>
        </div>
      </div>
      <div class="tecnm-semaphore-count">{{ summary.warningCount }}</div>
      <div class="tecnm-semaphore-desc">Requieren recordatorio preventivo</div>
    </div>

    <!-- Inactividad Crítica (Rojo) -->
    <div
      class="tecnm-semaphore-card tecnm-semaphore-critical"
      :class="{ active: activeFilter === 'critical' }"
      role="button"
      tabindex="0"
      @click="handleCardClick('critical')"
      @keydown.enter="handleCardClick('critical')"
    >
      <div class="tecnm-semaphore-header">
        <span class="tecnm-semaphore-title">Inactividad Crítica (&gt; 21 días)</span>
        <div class="tecnm-semaphore-icon" title="Atención urgente">
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126ZM12 15.75h.007v.008H12v-.008Z" />
          </svg>
        </div>
      </div>
      <div class="tecnm-semaphore-count">{{ summary.criticalCount }}</div>
      <div class="tecnm-semaphore-desc">Sin asesorías recientes o 0 registradas</div>
    </div>

    <!-- Captura Irregular (Púrpura) -->
    <div
      class="tecnm-semaphore-card tecnm-semaphore-irregular"
      :class="{ active: activeFilter === 'irregular' }"
      role="button"
      tabindex="0"
      @click="handleCardClick('irregular')"
      @keydown.enter="handleCardClick('irregular')"
    >
      <div class="tecnm-semaphore-header">
        <span class="tecnm-semaphore-title">Seguimiento Atípico</span>
        <div class="tecnm-semaphore-icon" title="Registros masivos">
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 12c0-1.232-.046-2.453-.138-3.662a4.006 4.006 0 0 0-3.7-3.7 48.678 48.678 0 0 0-7.324 0 4.006 4.006 0 0 0-3.7 3.7c-.017.22-.032.441-.046.662M19.5 12l3-3m-3 3-3-3m-12 3c0 1.232.046 2.453.138 3.662a4.006 4.006 0 0 0 3.7 3.7 48.656 48.656 0 0 0 7.324 0 4.006 4.006 0 0 0 3.7-3.7c.017-.22.032-.441.046-.662M4.5 12l3 3m-3-3-3 3" />
          </svg>
        </div>
      </div>
      <div class="tecnm-semaphore-count">{{ summary.irregularCount }}</div>
      <div class="tecnm-semaphore-desc">Carga concentrada o en bloque</div>
    </div>
  </div>
</template>

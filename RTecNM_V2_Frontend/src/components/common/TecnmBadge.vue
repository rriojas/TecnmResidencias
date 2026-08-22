<script setup>
import { computed } from 'vue'

const props = defineProps({
  type: {
    type: String,
    default: 'neutral', // success | danger | warning | info | primary | neutral
  },
  status: {
    type: [String, Boolean, Number],
    default: '',
  },
})

const badgeClass = computed(() => {
  if (props.type && props.type !== 'neutral') {
    return `tecnm-badge-${props.type}`
  }

  // Mapeo automático de estados comunes
  const s = String(props.status).toLowerCase()
  if (s === 'active' || s === 'true' || s === 'aprobado' || s === 'vigente' || s === 'completado' || s === 'autorizado') {
    return 'tecnm-badge-success'
  }
  if (s === 'inactive' || s === 'false' || s === 'rechazado' || s === 'cancelado' || s === 'no_aprobado') {
    return 'tecnm-badge-danger'
  }
  if (s === 'pending' || s === 'en_revision' || s === 'en_evaluacion' || s === 'correcciones' || s === 'pendiente') {
    return 'tecnm-badge-warning'
  }
  if (s === 'draft' || s === 'borrador' || s === 'registrado') {
    return 'tecnm-badge-info'
  }
  return 'tecnm-badge-neutral'
})
</script>

<template>
  <span class="tecnm-badge" :class="badgeClass">
    <slot>{{ status }}</slot>
  </span>
</template>

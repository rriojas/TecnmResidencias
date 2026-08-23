<script setup>
import { computed } from 'vue'

const props = defineProps({
  type: {
    type: String,
    default: '', // approved | pending | rejected | neutral | info | warning
  },
  status: {
    type: [String, Boolean, Number],
    default: '',
  },
  label: {
    type: String,
    default: '',
  },
})

const parsed = computed(() => {
  if (props.status === true || props.status === 1) {
    return {
      cssClass: props.type ? `tecnm-badge-${props.type}` : 'tecnm-badge-approved',
      text: props.label || 'Activo',
    }
  }
  if (props.status === false || props.status === 0) {
    return {
      cssClass: props.type ? `tecnm-badge-${props.type}` : 'tecnm-badge-rejected',
      text: props.label || 'Inactivo',
    }
  }

  const s = String(props.status || '').trim().toLowerCase()

  // Mapeo 100% fiel a getBadgeHtml de ui.js en el frontend legacy
  if (s === 'draft' || s === 'borrador') {
    return {
      cssClass: props.type ? `tecnm-badge-${props.type}` : 'tecnm-badge-neutral',
      text: props.label || 'Borrador',
    }
  }
  if (s === 'approved' || s === 'aprobado' || s === 'autorizado' || s === 'vigente') {
    return {
      cssClass: props.type ? `tecnm-badge-${props.type}` : 'tecnm-badge-approved',
      text: props.label || 'Aprobado',
    }
  }
  if (s === 'in_progress' || s === 'inprogress' || s === 'en_progreso' || s === 'en progreso') {
    return {
      cssClass: props.type ? `tecnm-badge-${props.type}` : 'tecnm-badge-approved',
      text: props.label || 'En Progreso',
    }
  }
  if (s === 'completed' || s === 'completado' || s === 'finalizado') {
    return {
      cssClass: props.type ? `tecnm-badge-${props.type}` : 'tecnm-badge-approved',
      text: props.label || 'Completado',
    }
  }
  if (s === 'rejected' || s === 'rechazado' || s === 'correcciones' || s === 'correcciones requeridas') {
    return {
      cssClass: props.type ? `tecnm-badge-${props.type}` : 'tecnm-badge-rejected',
      text: props.label || 'Correcciones Requeridas',
    }
  }
  if (s === 'cancelled' || s === 'cancelado' || s === 'baja' || s === 'eliminado') {
    return {
      cssClass: props.type ? `tecnm-badge-${props.type}` : 'tecnm-badge-rejected',
      text: props.label || 'Cancelado',
    }
  }
  if (s === 'active' || s === 'activo') {
    return {
      cssClass: props.type ? `tecnm-badge-${props.type}` : 'tecnm-badge-approved',
      text: props.label || 'Activo',
    }
  }
  if (s === 'inactive' || s === 'inactivo') {
    return {
      cssClass: props.type ? `tecnm-badge-${props.type}` : 'tecnm-badge-rejected',
      text: props.label || 'Inactivo',
    }
  }

  // Por defecto (pending, en_revision, etc.)
  return {
    cssClass: props.type ? `tecnm-badge-${props.type}` : 'tecnm-badge-pending',
    text: props.label || 'En Revisión',
  }
})
</script>

<template>
  <span class="tecnm-badge" :class="parsed.cssClass">
    <slot>{{ parsed.text }}</slot>
  </span>
</template>

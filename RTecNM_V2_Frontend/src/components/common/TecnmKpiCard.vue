<script setup>
import { computed } from 'vue'

const props = defineProps({
  title: {
    type: String,
    required: true,
  },
  value: {
    type: [String, Number],
    default: 0,
  },
  variant: {
    type: String,
    default: 'navy',
    validator: (v) => ['navy', 'gold', 'emerald', 'indigo', 'slate', 'warning', 'default'].includes(v),
  },
  subtext: {
    type: String,
    default: '',
  },
  loading: {
    type: Boolean,
    default: false,
  },
  to: {
    type: [String, Object],
    default: null,
  },
})

const variantClass = computed(() => {
  return `tecnm-kpi-card--${props.variant || 'navy'}`
})
</script>

<template>
  <component
    :is="to ? 'router-link' : 'div'"
    :to="to"
    class="tecnm-kpi-card"
    :class="[variantClass, { 'tecnm-kpi-card--link': !!to }]"
  >
    <div class="tecnm-kpi-icon-wrapper">
      <slot name="icon">
        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" d="M3.75 3v11.25A2.25 2.25 0 0 0 6 16.5h2.25M3.75 3h-1.5m1.5 0h16.5m0 0h1.5m-1.5 0v11.25A2.25 2.25 0 0 1 18 16.5h-2.25m-7.5 0h7.5m-7.5 0-1 3m8.5-3 1 3m0 0 .5 1.5m-.5-1.5h-9.5m0 0-.5 1.5m.75-9 3-3 2.25 2.25L15 7.5" />
        </svg>
      </slot>
    </div>

    <div class="tecnm-kpi-content">
      <div class="tecnm-kpi-header">
        <span class="tecnm-kpi-title">{{ title }}</span>
        <slot name="badge" />
      </div>

      <div class="tecnm-kpi-value-row">
        <span v-if="loading" class="tecnm-kpi-loading">
          <span class="tecnm-kpi-skeleton"></span>
        </span>
        <span v-else class="tecnm-kpi-value">{{ value ?? 0 }}</span>
      </div>

      <div v-if="subtext || $slots.subtext" class="tecnm-kpi-subtext">
        <slot name="subtext">{{ subtext }}</slot>
      </div>
    </div>
  </component>
</template>

<style scoped>
.tecnm-kpi-card {
  display: flex;
  align-items: center;
  gap: 1rem;
  background-color: var(--tecnm-surface-white, #ffffff);
  border: 1px solid var(--tecnm-border-color, #e2e8f0);
  border-radius: var(--tecnm-radius-lg, 0.5rem);
  padding: 1.125rem 1.25rem;
  box-shadow: 0 1px 3px 0 rgba(0, 0, 0, 0.05), 0 1px 2px -1px rgba(0, 0, 0, 0.05);
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
  text-decoration: none;
  color: inherit;
  position: relative;
  overflow: hidden;
}

.tecnm-kpi-card::after {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 3px;
  background: transparent;
  transition: background-color 0.2s ease;
}

.tecnm-kpi-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px -2px rgba(15, 23, 42, 0.08), 0 2px 6px -2px rgba(15, 23, 42, 0.04);
  border-color: #cbd5e1;
}

.tecnm-kpi-card--link {
  cursor: pointer;
}

/* Icon Containers */
.tecnm-kpi-icon-wrapper {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 48px;
  height: 48px;
  flex-shrink: 0;
  border-radius: 10px;
  transition: transform 0.2s ease;
}

.tecnm-kpi-card:hover .tecnm-kpi-icon-wrapper {
  transform: scale(1.05);
}

.tecnm-kpi-icon-wrapper :deep(svg) {
  width: 24px;
  height: 24px;
}

/* Content */
.tecnm-kpi-content {
  display: flex;
  flex-direction: column;
  min-width: 0;
  flex: 1;
}

.tecnm-kpi-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
}

.tecnm-kpi-title {
  font-size: 0.725rem;
  font-weight: 600;
  color: var(--tecnm-text-secondary, #64748b);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  line-height: 1.2;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.tecnm-kpi-value-row {
  margin-top: 0.25rem;
  display: flex;
  align-items: baseline;
  gap: 0.5rem;
}

.tecnm-kpi-value {
  font-size: 1.625rem;
  font-weight: 700;
  line-height: 1.2;
  color: var(--tecnm-text-primary, #0f172a);
  font-feature-settings: 'cv02', 'cv03', 'cv04', 'cv11';
}

.tecnm-kpi-subtext {
  font-size: 0.75rem;
  color: var(--tecnm-text-muted, #94a3b8);
  margin-top: 0.2rem;
  line-height: 1.2;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

/* Skeleton Loading */
.tecnm-kpi-skeleton {
  display: inline-block;
  width: 50px;
  height: 24px;
  border-radius: 4px;
  background: linear-gradient(90deg, #e2e8f0 25%, #f1f5f9 50%, #e2e8f0 75%);
  background-size: 200% 100%;
  animation: tecnm-shimmer 1.5s infinite;
}

@keyframes tecnm-shimmer {
  0% { background-position: 200% 0; }
  100% { background-position: -200% 0; }
}

/* Variants */
.tecnm-kpi-card--navy .tecnm-kpi-icon-wrapper {
  background-color: rgba(27, 57, 106, 0.08);
  color: var(--tecnm-blue-primary, #1b396a);
}
.tecnm-kpi-card--navy::after {
  background-color: var(--tecnm-blue-primary, #1b396a);
}

.tecnm-kpi-card--gold .tecnm-kpi-icon-wrapper,
.tecnm-kpi-card--warning .tecnm-kpi-icon-wrapper {
  background-color: rgba(217, 119, 6, 0.1);
  color: #b45309;
}
.tecnm-kpi-card--gold::after,
.tecnm-kpi-card--warning::after {
  background-color: var(--tecnm-gold-accent, #d4a017);
}

.tecnm-kpi-card--emerald .tecnm-kpi-icon-wrapper {
  background-color: rgba(16, 185, 129, 0.1);
  color: #047857;
}
.tecnm-kpi-card--emerald::after {
  background-color: #10b981;
}

.tecnm-kpi-card--indigo .tecnm-kpi-icon-wrapper {
  background-color: rgba(79, 70, 229, 0.1);
  color: #4338ca;
}
.tecnm-kpi-card--indigo::after {
  background-color: #6366f1;
}

.tecnm-kpi-card--slate .tecnm-kpi-icon-wrapper,
.tecnm-kpi-card--default .tecnm-kpi-icon-wrapper {
  background-color: rgba(100, 116, 139, 0.1);
  color: #475569;
}
.tecnm-kpi-card--slate::after,
.tecnm-kpi-card--default::after {
  background-color: #64748b;
}
</style>

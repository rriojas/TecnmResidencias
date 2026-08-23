<script setup>
import { useAudit } from '@/composables/useAudit'

const { isVisible, auditTitle, auditRows, close } = useAudit()
</script>

<template>
  <Teleport to="body">
    <div
      v-if="isVisible"
      id="auditModal"
      class="modal-backdrop active"
      role="dialog"
      aria-modal="true"
      @click.self="close"
    >
      <div class="modal-card">
        <div class="tecnm-modal-header">
          <h3 id="auditModalTitle" class="tecnm-modal-title">{{ auditTitle || 'Auditoría del Registro' }}</h3>
          <button
            id="auditModalClose"
            type="button"
            class="tecnm-modal-close"
            aria-label="Cerrar"
            @click="close"
          >
            &times;
          </button>
        </div>
        <dl id="auditModalList" class="tecnm-audit-list">
          <div
            v-for="(row, idx) in auditRows"
            :key="idx"
            class="tecnm-audit-row"
          >
            <dt>{{ row.label }}</dt>
            <dd>{{ row.value || '—' }}</dd>
          </div>
        </dl>
        <div class="tecnm-modal-footer">
          <button
            id="auditModalOk"
            type="button"
            class="tecnm-btn tecnm-btn-secondary"
            @click="close"
          >
            Cerrar
          </button>
        </div>
      </div>
    </div>
  </Teleport>
</template>

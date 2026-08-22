<script setup>
import { useConfirm } from '@/composables/useConfirm'

const {
  isVisible,
  confirmTitle,
  confirmMessage,
  confirmOkText,
  confirmCancelText,
  handleOk,
  handleCancel,
} = useConfirm()
</script>

<template>
  <Teleport to="body">
    <div
      v-if="isVisible"
      class="modal-backdrop active"
      role="dialog"
      aria-modal="true"
      @click.self="handleCancel"
    >
      <div class="modal-card" style="max-width: 480px;">
        <div class="tecnm-modal-header">
          <h3 class="tecnm-modal-title">{{ confirmTitle }}</h3>
          <button
            type="button"
            class="tecnm-modal-close"
            aria-label="Cerrar"
            @click="handleCancel"
          >
            &times;
          </button>
        </div>
        <div class="tecnm-card-body" style="padding: 1.25rem 0;">
          <p style="margin: 0; color: var(--tecnm-text-primary); font-size: 1rem; line-height: 1.5;">
            {{ confirmMessage }}
          </p>
        </div>
        <div class="tecnm-modal-footer">
          <button
            type="button"
            class="tecnm-btn tecnm-btn-secondary"
            @click="handleCancel"
          >
            {{ confirmCancelText }}
          </button>
          <button
            type="button"
            class="tecnm-btn tecnm-btn-primary"
            @click="handleOk"
          >
            {{ confirmOkText }}
          </button>
        </div>
      </div>
    </div>
  </Teleport>
</template>

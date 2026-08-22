<script setup>
import { onMounted, onUnmounted, watch } from 'vue'

const props = defineProps({
  modelValue: {
    type: Boolean,
    default: false,
  },
  title: {
    type: String,
    default: '',
  },
  maxWidth: {
    type: String,
    default: '640px',
  },
  closable: {
    type: Boolean,
    default: true,
  },
})

const emit = defineEmits(['update:modelValue', 'close'])

function close() {
  if (!props.closable) return
  emit('update:modelValue', false)
  emit('close')
}

function handleKeyDown(e) {
  if (e.key === 'Escape' && props.modelValue && props.closable) {
    close()
  }
}

watch(
  () => props.modelValue,
  (val) => {
    if (val) {
      document.body.style.overflow = 'hidden'
    } else {
      document.body.style.overflow = ''
    }
  }
)

onMounted(() => {
  window.addEventListener('keydown', handleKeyDown)
})

onUnmounted(() => {
  window.removeEventListener('keydown', handleKeyDown)
  document.body.style.overflow = ''
})
</script>

<template>
  <Teleport to="body">
    <Transition name="modal-fade">
      <div
        v-if="modelValue"
        class="modal-backdrop active"
        role="dialog"
        aria-modal="true"
        @click.self="close"
      >
        <div class="modal-card" :style="{ maxWidth: maxWidth }">
          <div class="tecnm-modal-header">
            <h3 class="tecnm-modal-title">
              <slot name="title">{{ title }}</slot>
            </h3>
            <button
              v-if="closable"
              type="button"
              class="tecnm-modal-close"
              aria-label="Cerrar"
              @click="close"
            >
              &times;
            </button>
          </div>

          <div class="tecnm-card-body" style="padding: 1.25rem 0;">
            <slot />
          </div>

          <div v-if="$slots.footer" class="tecnm-modal-footer">
            <slot name="footer" />
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.modal-fade-enter-active,
.modal-fade-leave-active {
  transition: opacity 0.2s ease, transform 0.2s ease;
}

.modal-fade-enter-from,
.modal-fade-leave-to {
  opacity: 0;
}

.modal-fade-enter-from .modal-card,
.modal-fade-leave-to .modal-card {
  transform: scale(0.96) translateY(-10px);
}
</style>

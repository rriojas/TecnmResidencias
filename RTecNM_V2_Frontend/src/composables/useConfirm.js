import { ref } from 'vue'

const isVisible = ref(false)
const confirmTitle = ref('Confirmación')
const confirmMessage = ref('')
const confirmOkText = ref('Aceptar')
const confirmCancelText = ref('Cancelar')
let resolvePromise = null

export function useConfirm() {
  function confirm({
    title = 'Confirmación',
    message = '¿Está seguro de realizar esta acción?',
    okText = 'Aceptar',
    cancelText = 'Cancelar',
  } = {}) {
    confirmTitle.value = title
    confirmMessage.value = message
    confirmOkText.value = okText
    confirmCancelText.value = cancelText
    isVisible.value = true

    return new Promise((resolve) => {
      resolvePromise = resolve
    })
  }

  function handleOk() {
    isVisible.value = false
    if (resolvePromise) resolvePromise(true)
  }

  function handleCancel() {
    isVisible.value = false
    if (resolvePromise) resolvePromise(false)
  }

  return {
    isVisible,
    confirmTitle,
    confirmMessage,
    confirmOkText,
    confirmCancelText,
    confirm,
    handleOk,
    handleCancel,
  }
}

import { ref } from 'vue'

const isOpen = ref(false)
const initialSource = ref('')
const onSelectCallback = ref(null)

export function useGlobalSearch() {
  function open(options = {}) {
    initialSource.value = options.initialSource || ''
    onSelectCallback.value = typeof options.onSelect === 'function' ? options.onSelect : null
    isOpen.value = true
  }

  function close() {
    isOpen.value = false
    onSelectCallback.value = null
  }

  if (typeof window !== 'undefined') {
    window.openGlobalSearch = open
  }

  return {
    isOpen,
    initialSource,
    onSelectCallback,
    open,
    close,
  }
}

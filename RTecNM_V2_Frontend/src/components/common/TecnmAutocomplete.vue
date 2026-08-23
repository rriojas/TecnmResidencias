<script setup>
import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import { useGlobalSearch } from '@/composables/useGlobalSearch'
import apiClient from '@/services/api'

const props = defineProps({
  modelValue: {
    type: [String, Number, null],
    default: null,
  },
  placeholder: {
    type: String,
    default: 'Escriba para buscar...',
  },
  endpoint: {
    type: String,
    default: '',
  },
  searchParam: {
    type: String,
    default: 'search',
  },
  extraParams: {
    type: Object,
    default: () => ({}),
  },
  minChars: {
    type: Number,
    default: 2,
  },
  globalSearchSource: {
    type: String,
    default: '',
  },
  titleExtractor: {
    type: Function,
    default: (item) => {
      if (!item) return ''
      return (
        item.fullName ||
        item.full_name ||
        item.name ||
        item.title ||
        item.email ||
        (item.firstName ? `${item.firstName} ${item.lastName || ''}`.trim() : '') ||
        'Elemento seleccionado'
      )
    },
  },
  subtitleExtractor: {
    type: Function,
    default: (item) => {
      if (!item) return ''
      const controlNo = item.controlNumber || item.control_number
      if (controlNo) {
        const career = item.career || item.career_name || item.careerName
        return `No. Control: ${controlNo}${career ? ' • ' + career : ''}`
      }
      const rfc = item.rfc
      if (rfc) {
        const sector = item.sector
        return `RFC: ${rfc}${sector ? ' • ' + sector : ''}`
      }
      const depto = item.departmentName || item.department_name || item.department
      if (depto) return `Depto: ${depto}`
      const email = item.userEmail || item.user_email || item.email
      if (email) return email
      return ''
    },
  },
  valueExtractor: {
    type: Function,
    default: (item) => {
      if (!item) return null
      return item.id != null ? item.id : item.Id
    },
  },
  initialItem: {
    type: Object,
    default: null,
  },
})

const emit = defineEmits(['update:modelValue', 'select', 'clear'])

const { open: openGlobalSearch } = useGlobalSearch()

const query = ref('')
const selectedItem = ref(props.initialItem || null)
const results = ref([])
const isLoading = ref(false)
const isDropdownOpen = ref(false)
const focusedIndex = ref(-1)
const wrapperRef = ref(null)

let debounceTimer = null
let abortController = null

const selectedTitle = computed(() => {
  return selectedItem.value ? props.titleExtractor(selectedItem.value) : ''
})

const selectedSubtitle = computed(() => {
  return selectedItem.value ? props.subtitleExtractor(selectedItem.value) : ''
})

function escapeHtml(text) {
  if (!text) return ''
  return String(text)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;')
}

function highlightMatch(text) {
  if (!text) return ''
  const str = String(text)
  const q = query.value.trim()
  if (!q) return escapeHtml(str)
  const escapedQuery = q.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const regex = new RegExp(`(${escapedQuery})`, 'gi')
  return escapeHtml(str).replace(
    regex,
    '<span class="tecnm-autocomplete-highlight">$1</span>'
  )
}

async function search(val) {
  if (!val || val.length < props.minChars) {
    results.value = []
    isDropdownOpen.value = false
    return
  }

  if (abortController) {
    abortController.abort()
  }
  abortController = new AbortController()

  isLoading.value = true
  isDropdownOpen.value = true

  try {
    const params = {
      [props.searchParam]: val,
      ...props.extraParams,
    }
    const res = await apiClient.get(props.endpoint, {
      params,
      signal: abortController.signal,
    })
    results.value = Array.isArray(res.data) ? res.data : (res.data.items || [])
  } catch (err) {
    if (err.name !== 'CanceledError' && err.code !== 'ERR_CANCELED') {
      console.error('Error en autocomplete:', err)
      results.value = []
    }
  } finally {
    isLoading.value = false
  }
}

function handleInput() {
  clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => {
    search(query.value.trim())
  }, 250)
}

function selectItem(item) {
  selectedItem.value = item
  isDropdownOpen.value = false
  query.value = ''
  focusedIndex.value = -1
  const val = props.valueExtractor(item)
  emit('update:modelValue', val)
  emit('select', item)
}

function clearSelection() {
  selectedItem.value = null
  query.value = ''
  results.value = []
  isDropdownOpen.value = false
  emit('update:modelValue', null)
  emit('clear')
}

function openPicker() {
  if (!props.globalSearchSource) return
  openGlobalSearch({
    initialSource: props.globalSearchSource,
    onSelect: (item) => {
      selectItem(item)
    },
  })
}

function handleKeyDown(e) {
  if (!isDropdownOpen.value || results.value.length === 0) return

  if (e.key === 'ArrowDown') {
    e.preventDefault()
    focusedIndex.value = (focusedIndex.value + 1) % results.value.length
  } else if (e.key === 'ArrowUp') {
    e.preventDefault()
    focusedIndex.value = (focusedIndex.value - 1 + results.value.length) % results.value.length
  } else if (e.key === 'Enter') {
    e.preventDefault()
    if (focusedIndex.value >= 0 && focusedIndex.value < results.value.length) {
      selectItem(results.value[focusedIndex.value])
    }
  } else if (e.key === 'Escape') {
    isDropdownOpen.value = false
  }
}

function handleDocumentClick(e) {
  if (wrapperRef.value && !wrapperRef.value.contains(e.target)) {
    isDropdownOpen.value = false
  }
}

watch(
  () => props.initialItem,
  (val) => {
    selectedItem.value = val
  }
)

watch(
  () => props.modelValue,
  (val) => {
    if (val === null || val === undefined || val === '') {
      selectedItem.value = null
    }
  }
)

onMounted(() => {
  document.addEventListener('click', handleDocumentClick)
})

onUnmounted(() => {
  document.removeEventListener('click', handleDocumentClick)
  if (debounceTimer) clearTimeout(debounceTimer)
  if (abortController) abortController.abort()
})
</script>

<template>
  <div
    ref="wrapperRef"
    class="tecnm-autocomplete-wrapper"
    :class="{ 'is-loading': isLoading }"
  >
    <!-- Input Group (visible si no hay item seleccionado) -->
    <div
      class="tecnm-autocomplete-input-group"
      :class="{ 'has-selected': !!selectedItem }"
    >
      <div style="position: relative; width: 100%;">
        <input
          v-model="query"
          type="text"
          class="tecnm-autocomplete-input"
          :placeholder="placeholder"
          autocomplete="off"
          spellcheck="false"
          @input="handleInput"
          @keydown="handleKeyDown"
          @focus="query.length >= minChars && (isDropdownOpen = true)"
        />
        <div class="tecnm-autocomplete-spinner" aria-hidden="true"></div>
      </div>

      <button
        v-if="globalSearchSource"
        type="button"
        class="tecnm-btn tecnm-btn-outline tecnm-btn-sm tecnm-autocomplete-picker-btn"
        title="Buscar en tabla completa"
        @click="openPicker"
      >
        <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
        </svg>
        <span>Buscar</span>
      </button>
    </div>

    <!-- Selected Card (visible si hay item seleccionado) -->
    <div
      class="tecnm-autocomplete-selected"
      :class="{ active: !!selectedItem }"
    >
      <div class="tecnm-autocomplete-selected-info">
        <span class="tecnm-autocomplete-selected-title">{{ selectedTitle }}</span>
        <span v-if="selectedSubtitle" class="tecnm-autocomplete-selected-subtitle">{{ selectedSubtitle }}</span>
      </div>
      <button
        type="button"
        class="tecnm-autocomplete-clear-btn"
        title="Cambiar selección"
        aria-label="Cambiar selección"
        @click="clearSelection"
      >
        &times;
      </button>
    </div>

    <!-- Dropdown con resultados en tiempo real -->
    <div
      class="tecnm-autocomplete-dropdown"
      :class="{ active: isDropdownOpen }"
      role="listbox"
    >
      <div v-if="isLoading" class="tecnm-autocomplete-loading-msg">
        Buscando...
      </div>
      <div v-else-if="results.length === 0" class="tecnm-autocomplete-empty">
        No se encontraron coincidencias.
      </div>
      <div
        v-for="(item, idx) in results"
        v-else
        :key="props.valueExtractor(item) || idx"
        class="tecnm-autocomplete-item"
        :class="{ 'is-focused': idx === focusedIndex }"
        @click="selectItem(item)"
      >
        <span
          class="tecnm-autocomplete-item-title"
          v-html="highlightMatch(props.titleExtractor(item))"
        ></span>
        <span
          v-if="props.subtitleExtractor(item)"
          class="tecnm-autocomplete-item-subtitle"
          v-html="highlightMatch(props.subtitleExtractor(item))"
        ></span>
      </div>
    </div>
  </div>
</template>

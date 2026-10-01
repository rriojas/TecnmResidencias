<script setup>
import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import apiClient from '@/services/api'

const props = defineProps({
  modelValue: {
    type: [Number, String, null],
    default: '',
  },
  emptyValue: {
    type: [String, null],
    default: '',
  },
  placeholder: {
    type: String,
    default: 'Buscar asesor por nombre...',
  },
  advisors: {
    type: Array,
    default: null,
  },
  showLabel: {
    type: Boolean,
    default: true,
  },
  inputId: {
    type: String,
    default: () => `tecnm-advisor-filter-${Math.random().toString(36).substring(2, 9)}`,
  },
})

const emit = defineEmits(['update:modelValue', 'change', 'select', 'clear'])

let cachedAdvisors = null
let fetchPromise = null

async function getCachedAdvisors() {
  if (cachedAdvisors) return cachedAdvisors
  if (!fetchPromise) {
    fetchPromise = apiClient
      .get('/v1/advisors/options')
      .then((res) => {
        cachedAdvisors = res.data || []
        return cachedAdvisors
      })
      .catch((err) => {
        console.error('Error al cargar catálogo de asesores:', err)
        return []
      })
      .finally(() => {
        fetchPromise = null
      })
  }
  return fetchPromise
}

const localAdvisors = ref([])
const isLoading = ref(false)
const isOpen = ref(false)
const searchQuery = ref('')
const highlightedIndex = ref(-1)
const rootRef = ref(null)
const inputRef = ref(null)

const advisorsList = computed(() => {
  if (Array.isArray(props.advisors) && props.advisors.length > 0) {
    return props.advisors
  }
  return localAdvisors.value
})

const isSelected = computed(() => {
  const val = props.modelValue
  return val !== null && val !== undefined && val !== '' && val !== 'all'
})

const selectedAdvisor = computed(() => {
  if (!isSelected.value) return null
  const targetId = String(props.modelValue)
  return advisorsList.value.find((a) => String(a.id) === targetId) || null
})

function normalizeText(text) {
  return String(text || '')
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .trim()
}

const filteredAdvisors = computed(() => {
  const q = normalizeText(searchQuery.value)
  if (!q) {
    return advisorsList.value
  }
  return advisorsList.value.filter((adv) => {
    const name = normalizeText(adv.fullName || adv.name)
    const dept = normalizeText(adv.departmentName || adv.department)
    return name.includes(q) || dept.includes(q)
  })
})

function syncQueryFromSelected() {
  if (selectedAdvisor.value) {
    searchQuery.value = selectedAdvisor.value.fullName || selectedAdvisor.value.name || ''
  } else {
    searchQuery.value = ''
  }
}

watch(
  () => props.modelValue,
  () => {
    syncQueryFromSelected()
  },
  { immediate: true }
)

watch(
  () => advisorsList.value,
  () => {
    syncQueryFromSelected()
  }
)

function onFocus() {
  isOpen.value = true
  highlightedIndex.value = -1
}

function onInput() {
  isOpen.value = true
  highlightedIndex.value = 0
}

function selectAdvisor(adv) {
  const newId = adv.id
  searchQuery.value = adv.fullName || adv.name || ''
  isOpen.value = false
  highlightedIndex.value = -1
  emit('update:modelValue', newId)
  emit('change', newId)
  emit('select', adv)
}

function clearFilter() {
  searchQuery.value = ''
  isOpen.value = false
  highlightedIndex.value = -1
  emit('update:modelValue', props.emptyValue)
  emit('change', props.emptyValue)
  emit('clear')
}

function isAdvisorSelected(adv) {
  if (!isSelected.value) return false
  return String(adv.id) === String(props.modelValue)
}

function onKeyDown(e) {
  if (!isOpen.value) {
    if (e.key === 'ArrowDown' || e.key === 'Enter') {
      isOpen.value = true
      e.preventDefault()
    }
    return
  }

  if (e.key === 'ArrowDown') {
    e.preventDefault()
    if (filteredAdvisors.value.length === 0) return
    highlightedIndex.value = (highlightedIndex.value + 1) % filteredAdvisors.value.length
  } else if (e.key === 'ArrowUp') {
    e.preventDefault()
    if (filteredAdvisors.value.length === 0) return
    highlightedIndex.value =
      (highlightedIndex.value - 1 + filteredAdvisors.value.length) % filteredAdvisors.value.length
  } else if (e.key === 'Enter') {
    e.preventDefault()
    if (
      highlightedIndex.value >= 0 &&
      highlightedIndex.value < filteredAdvisors.value.length
    ) {
      selectAdvisor(filteredAdvisors.value[highlightedIndex.value])
    }
  } else if (e.key === 'Escape') {
    isOpen.value = false
    syncQueryFromSelected()
  }
}

function handleClickOutside(e) {
  if (rootRef.value && !rootRef.value.contains(e.target)) {
    isOpen.value = false
    syncQueryFromSelected()
  }
}

onMounted(async () => {
  document.addEventListener('click', handleClickOutside)
  if (!props.advisors || props.advisors.length === 0) {
    isLoading.value = true
    try {
      localAdvisors.value = await getCachedAdvisors()
      syncQueryFromSelected()
    } finally {
      isLoading.value = false
    }
  }
})

onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside)
})
</script>

<template>
  <div ref="rootRef" class="tecnm-advisor-filter">
    <label
      v-if="showLabel"
      :for="inputId"
      class="tecnm-field-label tecnm-advisor-filter-label"
    >
      Asesor:
    </label>

    <div
      class="tecnm-advisor-filter-box"
      :class="{ 'is-selected': isSelected, 'is-open': isOpen }"
    >
      <div class="tecnm-advisor-filter-input-wrap">
        <svg
          class="tecnm-advisor-filter-icon"
          xmlns="http://www.w3.org/2000/svg"
          width="14"
          height="14"
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
          stroke-width="2"
        >
          <path
            stroke-linecap="round"
            stroke-linejoin="round"
            d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z"
          />
        </svg>

        <input
          :id="inputId"
          ref="inputRef"
          v-model="searchQuery"
          type="text"
          class="tecnm-advisor-filter-input"
          :placeholder="placeholder"
          autocomplete="off"
          spellcheck="false"
          @focus="onFocus"
          @input="onInput"
          @keydown="onKeyDown"
        />

        <button
          v-if="isSelected || searchQuery"
          type="button"
          class="tecnm-advisor-filter-clear"
          title="Quitar filtro de asesor"
          aria-label="Quitar filtro de asesor"
          @click.stop="clearFilter"
        >
          &times;
        </button>
      </div>

      <!-- Menú Desplegable Flotante -->
      <div
        v-show="isOpen"
        class="tecnm-advisor-filter-dropdown"
        role="listbox"
      >
        <div v-if="isLoading" class="tecnm-advisor-filter-state">
          <span>Cargando catálogo de asesores...</span>
        </div>
        <div
          v-else-if="filteredAdvisors.length === 0"
          class="tecnm-advisor-filter-state"
        >
          <span>No se encontraron asesores.</span>
        </div>
        <div v-else class="tecnm-advisor-filter-list">
          <div
            v-if="isSelected"
            class="tecnm-advisor-filter-item is-all"
            @click="clearFilter"
          >
            <span>Ver todos los asesores (quitar filtro)</span>
          </div>
          <div
            v-for="(adv, index) in filteredAdvisors"
            :key="adv.id"
            class="tecnm-advisor-filter-item"
            :class="{
              'is-active': isAdvisorSelected(adv),
              'is-highlighted': index === highlightedIndex,
            }"
            @click="selectAdvisor(adv)"
            @mouseenter="highlightedIndex = index"
          >
            <span class="tecnm-advisor-filter-name">
              {{ adv.fullName || adv.name }}
            </span>
            <span
              v-if="adv.assignedStudentsCount !== undefined"
              class="tecnm-advisor-filter-count"
            >
              {{ adv.assignedStudentsCount }}
              {{ adv.assignedStudentsCount === 1 ? 'alumno' : 'alumnos' }}
            </span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.tecnm-advisor-filter {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.tecnm-advisor-filter-label {
  margin-bottom: 0;
  white-space: nowrap;
  font-size: 0.85rem;
  font-weight: 500;
  color: var(--text-color, #1e293b);
}

.tecnm-advisor-filter-box {
  position: relative;
  min-width: 220px;
  max-width: 280px;
}

.tecnm-advisor-filter-input-wrap {
  position: relative;
  display: flex;
  align-items: center;
  width: 100%;
}

.tecnm-advisor-filter-icon {
  position: absolute;
  left: 0.65rem;
  color: #94a3b8;
  pointer-events: none;
  z-index: 2;
}

.tecnm-advisor-filter-input {
  width: 100%;
  height: 38px;
  padding: 0.375rem 1.8rem 0.375rem 2rem;
  font-size: 0.85rem;
  color: var(--text-color, #1e293b);
  background-color: var(--input-bg, #ffffff);
  border: 1px solid var(--border-color, #cbd5e1);
  border-radius: 6px;
  outline: none;
  transition: border-color 0.15s ease-in-out, box-shadow 0.15s ease-in-out;
}

.tecnm-advisor-filter-input:focus {
  border-color: var(--primary-color, #1b396a);
  box-shadow: 0 0 0 3px rgba(27, 57, 106, 0.15);
}

.tecnm-advisor-filter-box.is-selected .tecnm-advisor-filter-input {
  border-color: var(--primary-color, #1b396a);
  background-color: #f8fafc;
  font-weight: 500;
}

.tecnm-advisor-filter-clear {
  position: absolute;
  right: 0.45rem;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 20px;
  height: 20px;
  padding: 0;
  font-size: 1rem;
  line-height: 1;
  color: #94a3b8;
  background: transparent;
  border: none;
  border-radius: 50%;
  cursor: pointer;
  z-index: 2;
  transition: color 0.15s, background-color 0.15s;
}

.tecnm-advisor-filter-clear:hover {
  color: #ef4444;
  background-color: #fee2e2;
}

.tecnm-advisor-filter-dropdown {
  position: absolute;
  top: calc(100% + 4px);
  left: 0;
  right: 0;
  min-width: 260px;
  max-height: 250px;
  overflow-y: auto;
  background-color: #ffffff;
  border: 1px solid #cbd5e1;
  border-radius: 6px;
  box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.1), 0 8px 10px -6px rgba(0, 0, 0, 0.1);
  z-index: 1050;
}

.tecnm-advisor-filter-state {
  padding: 0.75rem;
  font-size: 0.8rem;
  color: #64748b;
  text-align: center;
}

.tecnm-advisor-filter-list {
  padding: 0.25rem 0;
}

.tecnm-advisor-filter-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  padding: 0.5rem 0.75rem;
  font-size: 0.85rem;
  color: #334155;
  cursor: pointer;
  border-bottom: 1px solid #f1f5f9;
  transition: background-color 0.15s;
}

.tecnm-advisor-filter-item:last-child {
  border-bottom: none;
}

.tecnm-advisor-filter-item:hover,
.tecnm-advisor-filter-item.is-highlighted {
  background-color: #f1f5f9;
  color: var(--primary-color, #1b396a);
}

.tecnm-advisor-filter-item.is-active {
  background-color: #e0e7ff;
  color: #1e3a8a;
  font-weight: 600;
}

.tecnm-advisor-filter-item.is-all {
  font-style: italic;
  color: #64748b;
  background-color: #f8fafc;
}

.tecnm-advisor-filter-item.is-all:hover {
  background-color: #e2e8f0;
  color: #1e293b;
}

.tecnm-advisor-filter-name {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.tecnm-advisor-filter-count {
  font-size: 0.72rem;
  color: #64748b;
  background: #f1f5f9;
  padding: 0.1rem 0.4rem;
  border-radius: 4px;
  white-space: nowrap;
}
</style>

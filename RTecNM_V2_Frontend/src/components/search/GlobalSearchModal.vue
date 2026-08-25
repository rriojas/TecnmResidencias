<script setup>
import { ref, computed, watch, onMounted, onUnmounted, nextTick } from 'vue'
import { useRouter } from 'vue-router'
import { useGlobalSearch } from '@/composables/useGlobalSearch'
import { useAuthStore } from '@/stores/auth'
import apiClient from '@/services/api'

const router = useRouter()
const authStore = useAuthStore()
const { isOpen, initialSource, onSelectCallback, close } = useGlobalSearch()

const sources = ref([])
const activeSourceKey = ref('')
const selectedSource = computed(() =>
  sources.value.find((s) => s.key === activeSourceKey.value)
)

const searchText = ref('')
const searchColumn = ref('')
const matchOption = ref('Contains')
const statusFilter = ref('active')
const sortColumn = ref('id')
const sortDirection = ref('ASC')

const pageNumber = ref(1)
const pageSize = ref(10)
const totalCount = ref(0)
const totalPages = ref(0)
const items = ref([])
const isLoading = ref(false)
const selectedRow = ref(null)

const showColumnsMenu = ref(false)
const visibleColumns = ref({})
const currentColumns = ref([])
const searchInputRef = ref(null)

let debounceTimer = null

async function loadSources() {
  if (!authStore.isAuthenticated) return
  try {
    const res = await apiClient.get('/v1/searches/sources')
    sources.value = res.data || []
    if (sources.value.length) {
      const target = initialSource.value || activeSourceKey.value || sources.value[0].key
      setSource(target)
    }
  } catch (err) {
    console.error('Error al cargar fuentes de búsqueda:', err)
  }
}

function setSource(key) {
  const s = sources.value.find((x) => x.key === key) || sources.value[0]
  if (!s) return
  activeSourceKey.value = s.key
  searchColumn.value = s.columns?.[0]?.name || ''
  sortColumn.value = s.columns?.[0]?.name || s.keyColumn || 'id'
  pageNumber.value = 1
  selectedRow.value = null

  // Inicializar columnas visibles
  const map = {}
  ;(s.columns || []).forEach((c) => {
    map[c.name] = c.isDefaultVisible !== false
  })
  visibleColumns.value = map
  currentColumns.value = s.columns || []

  executeSearch()
}

const displayedColumns = computed(() => {
  return currentColumns.value.filter((c) => visibleColumns.value[c.name] !== false)
})

const visibleCount = computed(() => {
  return Object.values(visibleColumns.value).filter(Boolean).length
})

const searchColumnDisplayName = computed(() => {
  const c = currentColumns.value.find((x) => x.name === searchColumn.value)
  return c ? c.displayName : 'todas'
})

const startRecord = computed(() => {
  if (totalCount.value === 0) return 0
  return (pageNumber.value - 1) * pageSize.value + 1
})

const endRecord = computed(() => {
  return Math.min(pageNumber.value * pageSize.value, totalCount.value)
})

async function executeSearch() {
  if (!activeSourceKey.value || !authStore.isAuthenticated) return
  isLoading.value = true

  try {
    const payload = {
      sourceKey: activeSourceKey.value,
      pageNumber: pageNumber.value,
      pageSize: pageSize.value,
      searchText: searchText.value.trim(),
      searchColumn: searchColumn.value || '',
      matchOption: matchOption.value || 'Contains',
      sortColumn: sortColumn.value,
      sortDirection: sortDirection.value,
      statusFilter: statusFilter.value,
    }

    const res = await apiClient.post('/v1/searches/filter-paged', payload)
    const data = res.data || {}
    items.value = data.rows || data.pagination?.items || []
    totalCount.value = data.pagination?.totalCount ?? (data.rows?.length || 0)
    totalPages.value = data.pagination?.totalPages ?? (Math.ceil(totalCount.value / pageSize.value) || 1)
  } catch (err) {
    console.error('Error en búsqueda global:', err)
    items.value = []
    totalCount.value = 0
    totalPages.value = 0
  } finally {
    isLoading.value = false
  }
}

function onSearchInput() {
  clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => {
    pageNumber.value = 1
    executeSearch()
  }, 350)
}

function onSourceChange(key) {
  setSource(key)
}

function onFilterChange() {
  pageNumber.value = 1
  executeSearch()
}

function setStatusFilter(filter) {
  statusFilter.value = filter
  pageNumber.value = 1
  executeSearch()
}

function sortByColumn(colName) {
  if (sortColumn.value.toLowerCase() === colName.toLowerCase()) {
    sortDirection.value = sortDirection.value === 'ASC' ? 'DESC' : 'ASC'
  } else {
    sortColumn.value = colName
    sortDirection.value = 'ASC'
  }
  executeSearch()
}

function toggleColumnsMenu() {
  showColumnsMenu.value = !showColumnsMenu.value
}

function selectRow(row) {
  selectedRow.value = row
}

function handleAccept() {
  if (!selectedRow.value) return
  if (onSelectCallback.value) {
    onSelectCallback.value(selectedRow.value)
    close()
  } else {
    navigateToSelected()
  }
}

function handleDoubleClick(row) {
  selectedRow.value = row
  handleAccept()
}

function getRowValue(row, colName) {
  if (!row || !colName) return null
  if (row[colName] !== undefined) return row[colName]
  const lower = colName.toLowerCase()
  const key = Object.keys(row).find((k) => k.toLowerCase() === lower)
  return key !== undefined ? row[key] : null
}

function navigateToSelected() {
  if (!selectedRow.value) return
  const id = getRowValue(selectedRow.value, 'id')
  if (activeSourceKey.value === 'STUDENTS') {
    router.push(`/students/profile?id=${id}`)
  } else if (activeSourceKey.value === 'PROJECTS') {
    router.push(`/projects/proposal?id=${id}`)
  } else if (activeSourceKey.value === 'ADVISORS') {
    router.push(`/advisors?id=${id}`)
  } else if (activeSourceKey.value === 'COMPANIES') {
    router.push(`/companies?id=${id}`)
  }
  close()
}

function changePage(page) {
  if (page < 1 || page > totalPages.value) return
  pageNumber.value = page
  executeSearch()
}

function formatCellValue(val, dataType) {
  if (val == null || val === '') return '—'
  if (dataType === 'Date' || dataType === 'DateTime') {
    const d = new Date(val)
    if (isNaN(d.getTime())) return String(val)
    return d.toLocaleDateString('es-MX')
  }
  return String(val)
}

function handleGlobalClick(e) {
  if (showColumnsMenu.value && !e.target.closest('.tecnm-search-columns-popover-wrapper')) {
    showColumnsMenu.value = false
  }
}

function handleKeyDown(e) {
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
    if (authStore.currentRole === 'student') return
    e.preventDefault()
    if (isOpen.value) {
      close()
    } else {
      isOpen.value = true
    }
  } else if (e.key === 'Escape' && isOpen.value) {
    close()
  }
}

watch(isOpen, async (val) => {
  if (val) {
    await loadSources()
    nextTick(() => {
      if (searchInputRef.value) {
        searchInputRef.value.focus()
      }
    })
  } else {
    selectedRow.value = null
    showColumnsMenu.value = false
    searchText.value = ''
  }
})

onMounted(() => {
  window.addEventListener('keydown', handleKeyDown)
  document.addEventListener('click', handleGlobalClick)
})

onUnmounted(() => {
  window.removeEventListener('keydown', handleKeyDown)
  document.removeEventListener('click', handleGlobalClick)
})
</script>

<template>
  <Teleport to="body">
    <div
      v-if="isOpen"
      id="tecnmGlobalSearchModal"
      class="tecnm-search-modal-backdrop"
      aria-modal="true"
      role="dialog"
      style="display: flex;"
      @click.self="close"
    >
      <div class="tecnm-search-modal-container" role="dialog" aria-modal="true" aria-labelledby="globalSearchModalTitle">
        <!-- Header del Modal -->
        <div class="tecnm-search-modal-header">
          <div class="tecnm-search-header-info">
            <div class="tecnm-search-header-title-row">
              <h3 id="globalSearchModalTitle" class="tecnm-search-modal-title">Búsqueda</h3>
              <span id="globalSearchCountBadge" class="tecnm-search-count-badge">{{ totalCount }}</span>
            </div>
            <span id="globalSearchSubtitle" class="tecnm-search-modal-subtitle">
              por {{ searchColumnDisplayName }}
            </span>
          </div>

          <div class="tecnm-search-header-actions">
            <span class="tecnm-search-instruction">Selecciona un registro para continuar.</span>

            <button
              id="acceptGlobalSearchBtn"
              type="button"
              class="tecnm-btn tecnm-btn-success tecnm-btn-sm"
              :disabled="!selectedRow"
              @click="handleAccept"
            >
              Aceptar
            </button>

            <!-- Menu desplegable de Columnas -->
            <div class="tecnm-search-columns-popover-wrapper">
              <button
                id="toggleColumnsDropdownBtn"
                type="button"
                class="tecnm-btn tecnm-btn-outline tecnm-btn-sm"
                @click.stop="toggleColumnsMenu"
              >
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M9 4.5v15m6-15v15m-10.875 0h15.75c.621 0 1.125-.504 1.125-1.125V5.625c0-.621-.504-1.125-1.125-1.125H4.125C3.504 4.5 3 5.004 3 5.625v13.5c0 .621.504 1.125 1.125 1.125Z" />
                </svg>
                <span>Columnas</span>
                <span id="globalSearchVisibleColCount" class="tecnm-search-col-badge">{{ visibleCount }}</span>
                <span class="tecnm-search-chevron">▾</span>
              </button>

              <div
                v-show="showColumnsMenu"
                id="globalSearchColumnsMenu"
                class="tecnm-search-columns-dropdown"
                @click.stop
              >
                <div class="tecnm-search-dropdown-header">Mostrar / Ocultar Columnas</div>
                <div id="globalSearchColumnsChecklist" class="tecnm-search-dropdown-body">
                  <label
                    v-for="col in currentColumns"
                    :key="col.name"
                    class="tecnm-search-checkbox-label"
                  >
                    <input
                      v-model="visibleColumns[col.name]"
                      type="checkbox"
                    />
                    <span>{{ col.displayName }}</span>
                  </label>
                </div>
              </div>
            </div>

            <button
              id="closeGlobalSearchModalBtn"
              type="button"
              class="tecnm-search-modal-close"
              aria-label="Cerrar modal"
              @click="close"
            >
              &times;
            </button>
          </div>
        </div>

        <!-- Cuerpo del Modal -->
        <div class="tecnm-search-modal-body">
          <!-- Barra Horizontal de Filtros -->
          <div class="tecnm-search-toolbar">
            <div class="tecnm-search-filter-item flex-grow-2">
              <label for="globalSearchTextInput" class="tecnm-search-filter-label">Valor</label>
              <input
                id="globalSearchTextInput"
                ref="searchInputRef"
                v-model="searchText"
                type="text"
                class="tecnm-search-filter-input"
                placeholder="Escribe para buscar..."
                @input="onSearchInput"
              />
            </div>

            <div class="tecnm-search-filter-item">
              <label for="globalSearchSourceSelect" class="tecnm-search-filter-label">Fuente</label>
              <select
                id="globalSearchSourceSelect"
                :value="activeSourceKey"
                class="tecnm-search-filter-select"
                @change="onSourceChange($event.target.value)"
              >
                <option
                  v-for="s in sources"
                  :key="s.key"
                  :value="s.key"
                >
                  {{ s.displayName }}
                </option>
              </select>
            </div>

            <div class="tecnm-search-filter-item">
              <label for="globalSearchColumnSelect" class="tecnm-search-filter-label">Columna</label>
              <select
                id="globalSearchColumnSelect"
                v-model="searchColumn"
                class="tecnm-search-filter-select"
                @change="onFilterChange"
              >
                <option
                  v-for="col in currentColumns"
                  :key="col.name"
                  :value="col.name"
                >
                  {{ col.displayName }}
                </option>
              </select>
            </div>

            <div class="tecnm-search-filter-item">
              <label for="globalSearchMatchSelect" class="tecnm-search-filter-label">Coincidencia</label>
              <select
                id="globalSearchMatchSelect"
                v-model="matchOption"
                class="tecnm-search-filter-select"
                @change="onFilterChange"
              >
                <option value="Contains">Contiene</option>
                <option value="StartsWith">Inicia con</option>
                <option value="EndsWith">Finaliza con</option>
                <option value="Exact">Exacta</option>
              </select>
            </div>

            <div class="tecnm-search-filter-item">
              <label for="globalSearchSortDirSelect" class="tecnm-search-filter-label">Dirección</label>
              <select
                id="globalSearchSortDirSelect"
                v-model="sortDirection"
                class="tecnm-search-filter-select"
                @change="onFilterChange"
              >
                <option value="ASC">Ascendente</option>
                <option value="DESC">Descendente</option>
              </select>
            </div>

            <div class="tecnm-search-filter-item align-self-end">
              <button
                id="executeSearchBtn"
                type="button"
                class="tecnm-btn tecnm-btn-success"
                @click="executeSearch"
              >
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
                </svg>
                <span>Buscar</span>
              </button>
            </div>

            <div class="tecnm-search-filter-item align-self-end">
              <span class="tecnm-search-status-title">ESTATUS</span>
              <div class="tecnm-search-status-pills">
                <button
                  id="statusActiveBtn"
                  type="button"
                  class="tecnm-status-pill"
                  :class="{ active: statusFilter === 'active' }"
                  @click="setStatusFilter('active')"
                >
                  Activos
                </button>
                <button
                  id="statusInactiveBtn"
                  type="button"
                  class="tecnm-status-pill"
                  :class="{ active: statusFilter === 'inactive' }"
                  @click="setStatusFilter('inactive')"
                >
                  Inactivos
                </button>
              </div>
            </div>
          </div>

          <!-- Tabla Dinámica de Resultados -->
          <div class="tecnm-search-table-wrapper">
            <table id="globalSearchResultTable" class="tecnm-search-table">
              <thead id="globalSearchTableHead">
                <tr>
                  <th
                    v-for="col in displayedColumns"
                    :key="col.name"
                    class="tecnm-search-th-sortable"
                    :class="{ sorted: sortColumn.toLowerCase() === col.name.toLowerCase() }"
                    @click="sortByColumn(col.name)"
                  >
                    {{ col.displayName }}
                    <span v-if="sortColumn.toLowerCase() === col.name.toLowerCase()">
                      {{ sortDirection === 'ASC' ? ' ▲' : ' ▼' }}
                    </span>
                  </th>
                </tr>
              </thead>
              <tbody id="globalSearchTableBody">
                <tr v-if="isLoading">
                  <td :colspan="displayedColumns.length || 1" class="tecnm-search-empty">
                    Cargando registros...
                  </td>
                </tr>
                <tr v-else-if="items.length === 0">
                  <td :colspan="displayedColumns.length || 1" class="tecnm-search-empty">
                    No se encontraron registros que coincidan con los criterios.
                  </td>
                </tr>
                <tr
                  v-for="(row, rIdx) in items"
                  v-else
                  :key="getRowValue(row, 'id') || rIdx"
                  class="tecnm-search-row"
                  :class="{ selected: getRowValue(selectedRow, 'id') != null && getRowValue(selectedRow, 'id') === getRowValue(row, 'id') }"
                  @click="selectRow(row)"
                  @dblclick="handleDoubleClick(row)"
                >
                  <td
                    v-for="col in displayedColumns"
                    :key="col.name"
                  >
                    <template v-if="col.name.toLowerCase() === 'is_active' || col.name.toLowerCase() === 'isactive'">
                      <span
                        class="tecnm-badge"
                        :class="getRowValue(row, col.name) ? 'tecnm-badge-approved' : 'tecnm-badge-rejected'"
                      >
                        {{ getRowValue(row, col.name) ? 'Activo' : 'Inactivo' }}
                      </span>
                    </template>
                    <template v-else>
                      {{ formatCellValue(getRowValue(row, col.name), col.dataType) }}
                    </template>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- Footer Paginación -->
          <div class="tecnm-search-modal-footer">
            <button
              id="prevPageBtn"
              type="button"
              class="tecnm-btn tecnm-btn-outline tecnm-btn-sm"
              :disabled="pageNumber <= 1"
              @click="changePage(pageNumber - 1)"
            >
              Anterior
            </button>
            <span id="globalSearchPaginationInfo" class="tecnm-search-pagination-text">
              Página {{ pageNumber }} de {{ totalPages || 1 }} • Mostrando {{ startRecord }}-{{ endRecord }} de {{ totalCount }} registros
            </span>
            <button
              id="nextPageBtn"
              type="button"
              class="tecnm-btn tecnm-btn-outline tecnm-btn-sm"
              :disabled="pageNumber >= totalPages"
              @click="changePage(pageNumber + 1)"
            >
              Siguiente
            </button>
          </div>
        </div>
      </div>
    </div>
  </Teleport>
</template>

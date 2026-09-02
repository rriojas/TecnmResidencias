<script setup>
import { ref, onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import apiClient from '@/services/api'

const props = defineProps({
  filters: {
    type: Object,
    required: true
  }
})

const emit = defineEmits(['update:filters', 'apply', 'reset'])

const authStore = useAuthStore()

const advisors = ref([])
const careers = ref([])
const isLoadingCatalogs = ref(false)

async function loadCatalogs() {
  isLoadingCatalogs.value = true
  try {
    const [advRes, carRes] = await Promise.all([
      apiClient.get('/v1/advisors/options').catch(() => ({ data: [] })),
      apiClient.get('/v1/careers/all').catch(() => ({ data: [] }))
    ])

    advisors.value = Array.isArray(advRes.data) ? advRes.data : []
    careers.value = Array.isArray(carRes.data) ? carRes.data : []

    // Si es Jefe de Carrera, fijar automáticamente su carrera
    if (authStore.isCareerHead && authStore.userCareerId) {
      props.filters.careerId = authStore.userCareerId
    }
  } finally {
    isLoadingCatalogs.value = false
  }
}

function handleSearchInput() {
  emit('apply')
}

function handleFilterChange() {
  emit('apply')
}

function handleReset() {
  emit('reset')
}

onMounted(() => {
  loadCatalogs()
})
</script>

<template>
  <div class="tecnm-card tecnm-mb-4" style="margin-bottom: 1.5rem;">
    <div class="tecnm-card-body" style="padding: 1.25rem;">
      <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 1rem; align-items: flex-end;">
        <!-- Búsqueda General -->
        <div class="tecnm-form-group" style="margin-bottom: 0;">
          <label class="tecnm-label" for="filterSearch">Buscar</label>
          <input
            id="filterSearch"
            v-model="filters.search"
            type="text"
            class="tecnm-form-control"
            placeholder="Alumno, asesor o tema..."
            @input="handleSearchInput"
          />
        </div>

        <!-- Filtro por Asesor -->
        <div class="tecnm-form-group" style="margin-bottom: 0;">
          <label class="tecnm-label" for="filterAdvisor">Asesor Académico</label>
          <select
            id="filterAdvisor"
            v-model="filters.advisorId"
            class="tecnm-form-control"
            @change="handleFilterChange"
          >
            <option :value="null">Todos los Asesores</option>
            <option
              v-for="adv in advisors"
              :key="adv.id"
              :value="adv.id"
            >
              {{ adv.fullName }}
            </option>
          </select>
        </div>

        <!-- Filtro por Carrera -->
        <div class="tecnm-form-group" style="margin-bottom: 0;">
          <label class="tecnm-label" for="filterCareer">Carrera</label>
          <select
            id="filterCareer"
            v-model="filters.careerId"
            class="tecnm-form-control"
            :disabled="authStore.isCareerHead"
            @change="handleFilterChange"
          >
            <option v-if="!authStore.isCareerHead" :value="null">Todas las Carreras</option>
            <option
              v-for="car in careers"
              :key="car.id"
              :value="car.id"
            >
              {{ car.name }}
            </option>
          </select>
        </div>

        <!-- Filtro por Notas de Supervisión -->
        <div class="tecnm-form-group" style="margin-bottom: 0;">
          <label class="tecnm-label" for="filterObservation">Supervisión de Jefatura</label>
          <select
            id="filterObservation"
            v-model="filters.observationFilter"
            class="tecnm-form-control"
            @change="handleFilterChange"
          >
            <option value="all">Todas las Asesorías</option>
            <option value="with_notes">Con Observación de Jefatura</option>
            <option value="without_notes">Sin Observaciones</option>
          </select>
        </div>

        <!-- Rango de Fechas: Desde -->
        <div class="tecnm-form-group" style="margin-bottom: 0;">
          <label class="tecnm-label" for="filterStartDate">Fecha Desde</label>
          <input
            id="filterStartDate"
            v-model="filters.startDate"
            type="date"
            class="tecnm-form-control"
            @change="handleFilterChange"
          />
        </div>

        <!-- Rango de Fechas: Hasta -->
        <div class="tecnm-form-group" style="margin-bottom: 0;">
          <label class="tecnm-label" for="filterEndDate">Fecha Hasta</label>
          <input
            id="filterEndDate"
            v-model="filters.endDate"
            type="date"
            class="tecnm-form-control"
            @change="handleFilterChange"
          />
        </div>

        <!-- Acciones: Botón Limpiar -->
        <div style="display: flex; gap: 0.5rem;">
          <button
            type="button"
            class="tecnm-btn tecnm-btn-secondary"
            style="width: 100%; height: 2.5rem;"
            title="Restablecer filtros"
            @click="handleReset"
          >
            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0 3.181 3.183a8.25 8.25 0 0 0 13.803-3.7M4.031 9.865a8.25 8.25 0 0 1 13.803-3.7l3.181 3.182m0-4.991v4.99" />
            </svg>
            <span>Limpiar</span>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

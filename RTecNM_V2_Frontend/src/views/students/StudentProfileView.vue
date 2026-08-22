<script setup>
import { ref, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import apiClient from '@/services/api'
import TecnmBadge from '@/components/common/TecnmBadge.vue'

const route = useRoute()
const router = useRouter()

const student = ref(null)
const isLoading = ref(true)
const errorMessage = ref('')

const CAREERS = {
  1: 'Ingeniería Informática',
  2: 'Ingeniería Industrial',
  3: 'Ingeniería Mecatrónica',
  4: 'Ingeniería en Sistemas Computacionales',
}

const fullName = computed(() => {
  if (!student.value) return '—'
  return [student.value.firstName, student.value.lastName, student.value.lastName2]
    .filter(Boolean)
    .join(' ')
    .trim()
})

async function loadProfile() {
  isLoading.value = true
  errorMessage.value = ''
  try {
    const studentId = route.query.id
    const url = studentId ? `/v1/students/${studentId}` : '/v1/students/me'
    const res = await apiClient.get(url)
    student.value = res.data
  } catch (err) {
    errorMessage.value =
      err.response?.data?.message ||
      'No se pudo cargar el expediente del estudiante.'
  } finally {
    isLoading.value = false
  }
}

onMounted(() => {
  loadProfile()
})
</script>

<template>
  <div>
    <!-- Barra Superior -->
    <div class="tecnm-actions-bar">
      <div>
        <h1 class="tecnm-page-title">Perfil del Estudiante</h1>
        <p class="tecnm-page-subtitle">Información académica y expediente de residencia</p>
      </div>
      <div class="tecnm-page-actions">
        <button
          type="button"
          class="tecnm-btn tecnm-btn-secondary"
          @click="router.back()"
        >
          &larr; Volver
        </button>
      </div>
    </div>

    <!-- Error State -->
    <div
      v-if="errorMessage"
      class="tecnm-alert tecnm-alert-danger"
      role="alert"
    >
      <span>{{ errorMessage }}</span>
    </div>

    <!-- Loading State -->
    <div v-else-if="isLoading" class="tecnm-card" style="text-align: center; padding: 3rem;">
      <div class="tecnm-spinner" style="margin: 0 auto 1rem auto;"></div>
      <p>Cargando información del estudiante...</p>
    </div>

    <!-- Card de Perfil -->
    <div v-else-if="student" class="tecnm-card">
      <div class="tecnm-card-header">
        <div>
          <h3 class="tecnm-card-title">{{ fullName }}</h3>
          <p class="tecnm-card-subtitle">{{ CAREERS[student.careerId] || 'Carrera no asignada' }}</p>
        </div>
        <TecnmBadge :status="student.isActive ? 'Activo' : 'Inactivo'" />
      </div>

      <div class="tecnm-card-body">
        <div class="tecnm-profile-grid">
          <div class="tecnm-profile-item">
            <span class="tecnm-field-label">Número de Control</span>
            <strong class="tecnm-field-value tecnm-field-value-emphasis">{{ student.controlNumber || '—' }}</strong>
          </div>

          <div class="tecnm-profile-item">
            <span class="tecnm-field-label">Nombre Completo</span>
            <strong class="tecnm-field-value">{{ fullName }}</strong>
          </div>

          <div class="tecnm-profile-item">
            <span class="tecnm-field-label">Correo Institucional</span>
            <strong class="tecnm-field-value">{{ student.email || '—' }}</strong>
          </div>

          <div class="tecnm-profile-item">
            <span class="tecnm-field-label">Carrera</span>
            <strong class="tecnm-field-value">{{ CAREERS[student.careerId] || '—' }}</strong>
          </div>

          <div class="tecnm-profile-item">
            <span class="tecnm-field-label">Promedio General</span>
            <strong class="tecnm-field-value tecnm-field-value-emphasis">
              {{ student.gpa != null ? student.gpa.toFixed(1) : '—' }}
            </strong>
          </div>

          <div class="tecnm-profile-item">
            <span class="tecnm-field-label">Fecha de Registro</span>
            <strong class="tecnm-field-value">
              {{ student.createdAt ? new Date(student.createdAt).toLocaleDateString('es-MX') : '—' }}
            </strong>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.tecnm-profile-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
  gap: 1.5rem;
  padding: 0.5rem 0;
}
.tecnm-profile-item {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}
.tecnm-field-label {
  font-size: 0.85rem;
  color: var(--tecnm-text-secondary, #64748b);
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}
.tecnm-field-value {
  font-size: 1.05rem;
  color: var(--tecnm-text-primary, #1e293b);
}
.tecnm-field-value-emphasis {
  font-size: 1.25rem;
  color: var(--tecnm-blue-primary, #1b396a);
}
</style>

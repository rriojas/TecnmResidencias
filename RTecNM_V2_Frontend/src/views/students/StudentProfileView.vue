<script setup>
import { ref, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import apiClient from '@/services/api'
import { useAuthStore } from '@/stores/auth'
import TecnmBadge from '@/components/common/TecnmBadge.vue'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

const student = ref(null)
const project = ref(null)
const isLoading = ref(true)
const isLoadingProject = ref(false)
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

const initials = computed(() => {
  if (!student.value) return 'E'
  const first = student.value.firstName ? student.value.firstName[0].toUpperCase() : ''
  const last = student.value.lastName ? student.value.lastName[0].toUpperCase() : ''
  return `${first}${last}` || 'E'
})

const formattedCreatedAt = computed(() => {
  if (!student.value?.createdAt) return '—'
  try {
    return new Date(student.value.createdAt).toLocaleDateString('es-MX', {
      year: 'numeric',
      month: 'long',
      day: 'numeric'
    })
  } catch {
    return student.value.createdAt
  }
})

async function loadProfile() {
  isLoading.value = true
  errorMessage.value = ''
  try {
    const studentId = route.query.id
    const url = studentId ? `/v1/students/${studentId}` : '/v1/students/me'
    const res = await apiClient.get(url)
    student.value = res.data

    // Cargar proyecto de residencia si existe
    await loadStudentProject(student.value?.id)
  } catch (err) {
    errorMessage.value =
      err.response?.data?.message ||
      'No se pudo cargar el expediente del estudiante.'
  } finally {
    isLoading.value = false
  }
}

async function loadStudentProject(studentId) {
  if (!studentId) return
  isLoadingProject.value = true
  try {
    const queryStudentId = route.query.id
    if (queryStudentId) {
      const projRes = await apiClient.get(`/v1/projects/student/${queryStudentId}`, {
        params: { pageNumber: 1, pageSize: 1 }
      })
      const items = projRes.data?.items || (Array.isArray(projRes.data) ? projRes.data : [])
      project.value = items.length > 0 ? items[0] : null
    } else {
      const projRes = await apiClient.get('/v1/projects/me/current')
      project.value = projRes.data
    }
  } catch {
    // Si no tiene proyecto o no tiene permisos, no es bloqueante
    project.value = null
  } finally {
    isLoadingProject.value = false
  }
}

function handleGoBack() {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.push('/students')
  }
}

function goToProject() {
  if (!project.value?.id) return
  if (authStore.currentRole === 'student') {
    router.push('/projects/proposal')
  } else {
    router.push('/projects/review')
  }
}

onMounted(() => {
  loadProfile()
})
</script>

<template>
  <div class="tecnm-profile-page">
    <!-- Barra Superior de Acciones -->
    <div class="tecnm-actions-bar">
      <div>
        <h1 class="tecnm-page-title">Perfil del Estudiante</h1>
        <p class="tecnm-page-subtitle">Información académica y expediente de residencia profesional</p>
      </div>
      <div class="tecnm-page-actions">
        <button
          type="button"
          class="tecnm-btn tecnm-btn-secondary"
          @click="handleGoBack"
        >
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" style="margin-right: 0.35rem; display: inline-block; vertical-align: middle;">
            <path stroke-linecap="round" stroke-linejoin="round" d="M10.5 19.5L3 12m0 0l7.5-7.5M3 12h18" />
          </svg>
          Volver
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
      <p style="color: var(--tecnm-text-secondary); font-weight: 500;">Cargando información del estudiante...</p>
    </div>

    <!-- Contenido del Perfil -->
    <div v-else-if="student" class="tecnm-profile-container">
      <!-- Hero Header Card -->
      <div class="tecnm-card tecnm-profile-hero">
        <div class="tecnm-profile-hero-content">
          <div class="tecnm-profile-avatar-large">
            {{ initials }}
          </div>
          <div class="tecnm-profile-hero-details">
            <div class="tecnm-profile-hero-title-row">
              <h2 class="tecnm-profile-hero-name">{{ fullName }}</h2>
              <TecnmBadge :status="student.isActive ? 'Activo' : 'Inactivo'" />
            </div>
            <div class="tecnm-profile-hero-tags">
              <span class="tecnm-hero-tag">
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.75">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M4.26 10.147a60.436 60.436 0 00-.491 6.347A48.627 48.627 0 0112 20.904a48.627 48.627 0 018.232-4.41 60.46 60.46 0 00-.491-6.347m-15.482 0a50.57 50.57 0 00-2.658-.813A59.905 59.905 0 0112 3.493a59.902 59.902 0 0110.399 5.84c-.896.248-1.783.52-2.658.814m-15.482 0A50.697 50.697 0 0112 13.489a50.702 50.702 0 017.74-3.342M6.75 15a.75.75 0 100-1.5.75.75 0 000 1.5zm0 0v-3.675A55.378 55.378 0 0112 8.443m-7.007 11.55A5.981 5.981 0 006.75 15.75v-1.5" />
                </svg>
                {{ CAREERS[student.careerId] || 'Carrera no asignada' }}
              </span>
              <span class="tecnm-hero-tag tecnm-hero-tag-accent">
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.75">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M15 9h3.75M15 12h3.75M15 15h3.75M4.5 19.5h15a2.25 2.25 0 002.25-2.25V6.75A2.25 2.25 0 0019.5 4.5h-15a2.25 2.25 0 00-2.25 2.25v10.5A2.25 2.25 0 004.5 19.5zm6-10.125a1.875 1.875 0 11-3.75 0 1.875 1.875 0 013.75 0zm1.294 6.336a6.721 6.721 0 01-3.17.789 6.721 6.721 0 01-3.168-.789 3.376 3.376 0 016.338 0z" />
                </svg>
                No. Control: {{ student.controlNumber || '—' }}
              </span>
            </div>
          </div>
        </div>
      </div>

      <!-- Cuadrícula de Secciones -->
      <div class="tecnm-profile-sections-grid">
        <!-- Card 1: Información Académica y Personal -->
        <div class="tecnm-card">
          <div class="tecnm-card-header">
            <h3 class="tecnm-card-title">
              <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" class="tecnm-card-icon">
                <path stroke-linecap="round" stroke-linejoin="round" d="M15.75 6a3.75 3.75 0 11-7.5 0 3.75 3.75 0 017.5 0zM4.501 20.118a7.5 7.5 0 0114.998 0A17.933 17.933 0 0112 21.75c-2.676 0-5.216-.584-7.499-1.632z" />
              </svg>
              Datos Generales y Académicos
            </h3>
          </div>
          <div class="tecnm-card-body">
            <div class="tecnm-info-cards-grid">
              <!-- Número de Control -->
              <div class="tecnm-info-tile">
                <span class="tecnm-info-tile-label">Número de Control</span>
                <span class="tecnm-info-tile-value tecnm-info-tile-value--mono">{{ student.controlNumber || '—' }}</span>
              </div>

              <!-- Nombre Completo -->
              <div class="tecnm-info-tile">
                <span class="tecnm-info-tile-label">Nombre Completo</span>
                <span class="tecnm-info-tile-value">{{ fullName }}</span>
              </div>

              <!-- Correo Institucional -->
              <div class="tecnm-info-tile tecnm-info-tile--full">
                <span class="tecnm-info-tile-label">Correo Institucional</span>
                <a
                  v-if="student.email"
                  :href="`mailto:${student.email}`"
                  class="tecnm-info-tile-link"
                >
                  <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.75" style="flex-shrink: 0;">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M21.75 6.75v10.5a2.25 2.25 0 01-2.25 2.25h-15a2.25 2.25 0 01-2.25-2.25V6.75m19.5 0A2.25 2.25 0 0019.5 4.5h-15a2.25 2.25 0 00-2.25 2.25m19.5 0v.243a2.25 2.25 0 01-1.07 1.916l-7.5 4.615a2.25 2.25 0 01-2.36 0L3.32 8.91a2.25 2.25 0 01-1.07-1.916V6.75" />
                  </svg>
                  <span class="tecnm-email-text">{{ student.email }}</span>
                </a>
                <span v-else class="tecnm-info-tile-value">—</span>
              </div>

              <!-- Carrera -->
              <div class="tecnm-info-tile tecnm-info-tile--full">
                <span class="tecnm-info-tile-label">Carrera / Programa Académico</span>
                <span class="tecnm-info-tile-value">{{ CAREERS[student.careerId] || '—' }}</span>
              </div>

              <!-- Promedio General -->
              <div class="tecnm-info-tile">
                <span class="tecnm-info-tile-label">Promedio General</span>
                <div class="tecnm-gpa-badge">
                  <span class="tecnm-gpa-number">{{ student.gpa != null ? student.gpa.toFixed(1) : '—' }}</span>
                  <span class="tecnm-gpa-max">/ 100</span>
                </div>
              </div>

              <!-- Fecha de Registro -->
              <div class="tecnm-info-tile">
                <span class="tecnm-info-tile-label">Fecha de Registro</span>
                <span class="tecnm-info-tile-value">{{ formattedCreatedAt }}</span>
              </div>
            </div>
          </div>
        </div>

        <!-- Card 2: Expediente de Residencia Profesional -->
        <div class="tecnm-card">
          <div class="tecnm-card-header">
            <h3 class="tecnm-card-title">
              <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" class="tecnm-card-icon">
                <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 00-3.375-3.375h-1.5A1.125 1.125 0 0113.5 7.125v-1.5a3.375 3.375 0 00-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 00-9-9z" />
              </svg>
              Expediente de Residencia Profesional
            </h3>
          </div>
          <div class="tecnm-card-body">
            <!-- Loading Proyecto -->
            <div v-if="isLoadingProject" style="text-align: center; padding: 2rem;">
              <div class="tecnm-spinner" style="margin: 0 auto 0.75rem auto;"></div>
              <p style="font-size: 0.9rem; color: var(--tecnm-text-secondary);">Consultando anteproyecto...</p>
            </div>

            <!-- Proyecto Encontrado -->
            <div v-else-if="project" class="tecnm-project-summary">
              <div class="tecnm-project-header-box">
                <div class="tecnm-project-title-group">
                  <span class="tecnm-info-tile-label">Título del Anteproyecto</span>
                  <h4 class="tecnm-project-title">{{ project.title }}</h4>
                </div>
                <TecnmBadge :status="project.status" />
              </div>

              <div class="tecnm-info-cards-grid" style="margin-top: 1rem;">
                <div class="tecnm-info-tile">
                  <span class="tecnm-info-tile-label">Empresa Receptora</span>
                  <span class="tecnm-info-tile-value">{{ project.companyName || '—' }}</span>
                </div>

                <div class="tecnm-info-tile">
                  <span class="tecnm-info-tile-label">Asesor Interno</span>
                  <span class="tecnm-info-tile-value">{{ project.advisorName || '—' }}</span>
                </div>

                <div class="tecnm-info-tile">
                  <span class="tecnm-info-tile-label">Tipo de Proyecto</span>
                  <span class="tecnm-info-tile-value">{{ project.projectType || 'Desarrollo Tecnológico' }}</span>
                </div>

                <div class="tecnm-info-tile">
                  <span class="tecnm-info-tile-label">Fecha de Registro</span>
                  <span class="tecnm-info-tile-value">
                    {{ project.createdAt ? new Date(project.createdAt).toLocaleDateString('es-MX') : '—' }}
                  </span>
                </div>
              </div>

              <div class="tecnm-project-actions">
                <button
                  type="button"
                  class="tecnm-btn tecnm-btn-primary tecnm-btn-sm"
                  @click="goToProject"
                >
                  Ver Anteproyecto &rarr;
                </button>
              </div>
            </div>

            <!-- Sin Proyecto Registrado -->
            <div v-else class="tecnm-empty-project-box">
              <div class="tecnm-empty-icon">
                <svg xmlns="http://www.w3.org/2000/svg" width="36" height="36" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 00-3.375-3.375h-1.5A1.125 1.125 0 0113.5 7.125v-1.5a3.375 3.375 0 00-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 00-9-9z" />
                </svg>
              </div>
              <h4 class="tecnm-empty-title">Sin Anteproyecto Registrado</h4>
              <p class="tecnm-empty-desc">
                El estudiante aún no cuenta con un anteproyecto de residencia profesional registrado en el sistema.
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.tecnm-profile-page {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.tecnm-profile-container {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

/* Hero Header Card */
.tecnm-profile-hero {
  padding: 1.5rem 1.75rem;
  background: #ffffff;
  border-left: 5px solid var(--tecnm-blue-primary, #1b396a);
}

.tecnm-profile-hero-content {
  display: flex;
  align-items: center;
  gap: 1.5rem;
  flex-wrap: wrap;
}

.tecnm-profile-avatar-large {
  width: 64px;
  height: 64px;
  min-width: 64px;
  border-radius: var(--tecnm-radius-full, 9999px);
  background: linear-gradient(135deg, var(--tecnm-blue-dark, #102342) 0%, var(--tecnm-blue-primary, #1b396a) 100%);
  border: 3px solid var(--tecnm-gold-accent, #d4a017);
  color: #ffffff;
  font-size: 1.5rem;
  font-weight: 700;
  display: flex;
  align-items: center;
  justify-content: center;
  text-transform: uppercase;
  box-shadow: 0 4px 10px rgba(27, 57, 106, 0.15);
}

.tecnm-profile-hero-details {
  flex: 1;
  min-width: 260px;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.tecnm-profile-hero-title-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  flex-wrap: wrap;
}

.tecnm-profile-hero-name {
  margin: 0;
  font-size: 1.4rem;
  font-weight: 700;
  color: var(--tecnm-blue-primary, #1b396a);
  letter-spacing: -0.01em;
  word-break: break-word;
  overflow-wrap: anywhere;
}

.tecnm-profile-hero-tags {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.tecnm-hero-tag {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  font-size: 0.875rem;
  color: var(--tecnm-text-secondary, #475569);
  background-color: var(--tecnm-bg-light, #f1f5f9);
  padding: 0.3rem 0.75rem;
  border-radius: var(--tecnm-radius-md, 6px);
  font-weight: 500;
  word-break: break-word;
  overflow-wrap: anywhere;
}

.tecnm-hero-tag-accent {
  background-color: rgba(27, 57, 106, 0.08);
  color: var(--tecnm-blue-primary, #1b396a);
  font-weight: 600;
}

/* Sections Grid */
.tecnm-profile-sections-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(360px, 1fr));
  gap: 1.5rem;
  align-items: start;
}

.tecnm-card-icon {
  color: var(--tecnm-blue-primary, #1b396a);
  margin-right: 0.5rem;
  vertical-align: text-bottom;
}

/* Info Tiles Grid */
.tecnm-info-cards-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 1rem;
}

.tecnm-info-tile {
  background-color: var(--tecnm-bg-light, #f8fafc);
  border: 1px solid var(--tecnm-border-color, #e2e8f0);
  border-radius: var(--tecnm-radius-md, 8px);
  padding: 0.85rem 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  min-width: 0;
}

.tecnm-info-tile--full {
  grid-column: 1 / -1;
}

.tecnm-info-tile-label {
  font-size: 0.75rem;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: var(--tecnm-text-secondary, #64748b);
  font-weight: 600;
}

.tecnm-info-tile-value {
  font-size: 0.95rem;
  font-weight: 600;
  color: var(--tecnm-text-primary, #1e293b);
  word-break: break-word;
  overflow-wrap: anywhere;
  min-width: 0;
}

.tecnm-info-tile-value--mono {
  font-family: var(--tecnm-font-mono, monospace);
  font-size: 1.1rem;
  color: var(--tecnm-blue-primary, #1b396a);
}

.tecnm-info-tile-link {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  color: var(--tecnm-blue-primary, #1b396a);
  font-size: 0.95rem;
  font-weight: 600;
  text-decoration: none;
  word-break: break-word;
  overflow-wrap: anywhere;
  min-width: 0;
  transition: color 0.15s ease;
}

.tecnm-info-tile-link:hover {
  color: var(--tecnm-blue-hover, #102342);
  text-decoration: underline;
}

.tecnm-email-text {
  word-break: break-word;
  overflow-wrap: anywhere;
}

/* GPA Badge */
.tecnm-gpa-badge {
  display: inline-flex;
  align-items: baseline;
  gap: 0.25rem;
}

.tecnm-gpa-number {
  font-size: 1.35rem;
  font-weight: 700;
  color: var(--tecnm-blue-primary, #1b396a);
  line-height: 1;
}

.tecnm-gpa-max {
  font-size: 0.8rem;
  color: var(--tecnm-text-secondary, #64748b);
  font-weight: 500;
}

/* Project Summary */
.tecnm-project-summary {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.tecnm-project-header-box {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
  padding-bottom: 0.75rem;
  border-bottom: 1px solid var(--tecnm-border-color, #e2e8f0);
}

.tecnm-project-title-group {
  flex: 1;
  min-width: 0;
}

.tecnm-project-title {
  margin: 0.25rem 0 0 0;
  font-size: 1.05rem;
  font-weight: 700;
  color: var(--tecnm-blue-primary, #1b396a);
  line-height: 1.35;
  word-break: break-word;
  overflow-wrap: anywhere;
}

.tecnm-project-actions {
  margin-top: 0.5rem;
  display: flex;
  justify-content: flex-end;
}

/* Empty Project Box */
.tecnm-empty-project-box {
  text-align: center;
  padding: 2.5rem 1.5rem;
  background-color: var(--tecnm-bg-light, #f8fafc);
  border: 1px dashed var(--tecnm-border-color, #cbd5e1);
  border-radius: var(--tecnm-radius-md, 8px);
}

.tecnm-empty-icon {
  color: var(--tecnm-text-secondary, #94a3b8);
  margin-bottom: 0.75rem;
}

.tecnm-empty-title {
  margin: 0 0 0.35rem 0;
  font-size: 1rem;
  font-weight: 600;
  color: var(--tecnm-text-primary, #334155);
}

.tecnm-empty-desc {
  margin: 0;
  font-size: 0.875rem;
  color: var(--tecnm-text-secondary, #64748b);
  max-width: 380px;
  margin-left: auto;
  margin-right: auto;
  line-height: 1.4;
}

@media (max-width: 640px) {
  .tecnm-profile-sections-grid {
    grid-template-columns: 1fr;
  }
  .tecnm-profile-hero-content {
    flex-direction: column;
    align-items: flex-start;
  }
}
</style>


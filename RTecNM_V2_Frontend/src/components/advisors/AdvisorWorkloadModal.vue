<script setup>
import { ref, watch, computed } from 'vue'
import { useRouter } from 'vue-router'
import apiClient from '@/services/api'
import TecnmBadge from '@/components/common/TecnmBadge.vue'
import { useAuthStore } from '@/stores/auth'

const props = defineProps({
  modelValue: {
    type: Boolean,
    default: false,
  },
  advisorId: {
    type: [Number, String],
    default: null,
  },
})

const emit = defineEmits(['update:modelValue', 'close'])

const router = useRouter()
const authStore = useAuthStore()

const isLoading = ref(false)
const errorMessage = ref('')
const advisorData = ref(null)

const canManageAssignments = computed(() => {
  return authStore.isAdmin || authStore.isCareerHead || authStore.hasPermission('projects.advisor.assign')
})


const advisorInitials = computed(() => {
  if (!advisorData.value?.fullName) return 'DC'
  return advisorData.value.fullName
    .trim()
    .split(/\s+/)
    .map((p) => p[0])
    .slice(0, 2)
    .join('')
    .toUpperCase()
})

async function fetchAdvisorDetails(id) {
  if (!id) return
  isLoading.value = true
  errorMessage.value = ''
  advisorData.value = null

  try {
    const res = await apiClient.get(`/v1/advisors/${id}/residents`)
    advisorData.value = res.data
  } catch (err) {
    console.error('Error al cargar expediente del asesor:', err)
    errorMessage.value =
      err.response?.data?.message || 'No fue posible consultar los residentes asignados a este asesor.'
  } finally {
    isLoading.value = false
  }
}

watch(
  () => [props.modelValue, props.advisorId],
  ([isOpen, id]) => {
    if (isOpen && id) {
      fetchAdvisorDetails(id)
    } else if (!isOpen) {
      advisorData.value = null
      errorMessage.value = ''
    }
  },
  { immediate: true }
)

function closeModal() {
  emit('update:modelValue', false)
  emit('close')
}

function goToAssignments() {
  closeModal()
  router.push('/advisors/assignments')
}

function goToReview() {
  closeModal()
  router.push('/projects/review')
}
</script>

<template>
  <div v-if="modelValue" class="modal-backdrop" @click.self="closeModal">
    <div class="tecnm-modal-dialog" role="dialog" aria-modal="true">
      <!-- Modal Header -->
      <div class="tecnm-modal-header">
        <div class="tecnm-d-flex tecnm-align-center tecnm-gap-2">
          <svg xmlns="http://www.w3.org/2000/svg" class="tecnm-header-icon" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M15.75 6a3.75 3.75 0 1 1-7.5 0 3.75 3.75 0 0 1 7.5 0ZM4.501 20.118a7.5 7.5 0 0 1 14.998 0A17.933 17.933 0 0 1 12 21.75c-2.676 0-5.216-.584-7.499-1.632Z" />
          </svg>
          <h2 class="tecnm-modal-title">Expediente de Carga Docente</h2>
        </div>
        <button type="button" class="tecnm-modal-close" aria-label="Cerrar modal" @click="closeModal">
          &times;
        </button>
      </div>

      <!-- Loading State -->
      <div v-if="isLoading" class="modal-body-loading">
        <div class="spinner"></div>
        <p>Consultando residentes asignados y carga académica...</p>
      </div>

      <!-- Error State -->
      <div v-else-if="errorMessage" class="modal-body-content">
        <div class="tecnm-alert tecnm-alert-danger" style="margin: 1.5rem;">
          {{ errorMessage }}
        </div>
      </div>

      <!-- Main Content -->
      <div v-else-if="advisorData" class="modal-body-content">
        <!-- Ficha del Asesor -->
        <div class="advisor-summary-banner">
          <div class="advisor-avatar-circle">
            {{ advisorInitials }}
          </div>
          <div class="advisor-info-col">
            <div class="advisor-name-row">
              <h3 class="advisor-name">{{ advisorData.fullName }}</h3>
            </div>
            <div class="advisor-meta-row">
              <span class="advisor-title-badge">{{ advisorData.title || 'Docente TecNM' }}</span>
              <span class="meta-sep">•</span>
              <span class="advisor-dept">{{ advisorData.departmentName }}</span>
            </div>
            <div class="advisor-contact-row">
              <a v-if="advisorData.email" :href="`mailto:${advisorData.email}`" class="advisor-contact-link">
                <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                </svg>
                {{ advisorData.email }}
              </a>
              <span v-if="advisorData.phone" class="advisor-contact-phone">
                <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 5a2 2 0 012-2h3.28a1 1 0 01.948.684l1.498 4.493a1 1 0 01-.502 1.21l-2.257 1.13a11.042 11.042 0 005.516 5.516l1.13-2.257a1 1 0 011.21-.502l4.493 1.498a1 1 0 01.684.949V19a2 2 0 01-2 2h-1C9.716 21 3 14.284 3 6V5z" />
                </svg>
                {{ advisorData.phone }}
              </span>
            </div>
          </div>
        </div>

        <!-- Sección de Residentes Asignados -->
        <div class="residents-section">
          <div class="residents-section-header">
            <h4 class="residents-title">
              Residentes Asignados ({{ advisorData.residents.length }})
            </h4>
            <button
              v-if="canManageAssignments"
              type="button"
              class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
              @click="goToAssignments"
            >
              + Gestionar Asignación
            </button>
          </div>

          <!-- Tabla de Residentes si los hay -->
          <div v-if="advisorData.residents.length > 0" class="residents-list">
            <div
              v-for="res in advisorData.residents"
              :key="res.studentId"
              class="resident-card-row"
            >
              <div class="resident-left-col">
                <div class="resident-avatar">
                  {{ (res.fullName || 'E').charAt(0).toUpperCase() }}
                </div>
                <div>
                  <div class="resident-name">{{ res.fullName }}</div>
                  <div class="resident-meta">
                    <span>No. Control: <strong>{{ res.controlNumber }}</strong></span>
                    <span class="meta-sep">•</span>
                    <span>{{ res.careerName }}</span>
                  </div>
                  <div v-if="res.projectTitle" class="resident-project-line">
                    <span class="project-tag">Proyecto:</span>
                    <strong>{{ res.projectTitle }}</strong>
                    <span v-if="res.companyName" class="company-tag">({{ res.companyName }})</span>
                  </div>
                  <div v-else class="resident-no-project">
                    Sin anteproyecto registrado actualmente
                  </div>
                </div>
              </div>

              <div class="resident-right-col">
                <div class="resident-status-box">
                  <TecnmBadge v-if="res.projectStatus" :status="res.projectStatus" />
                  <span v-else class="tecnm-badge tecnm-badge-neutral">Pendiente</span>
                  <div class="advisory-count-badge" :title="`${res.advisoryCount} sesiones registradas`">
                    {{ res.advisoryCount }} {{ res.advisoryCount === 1 ? 'asesoría' : 'asesorías' }}
                  </div>
                </div>
                <button
                  v-if="res.projectId"
                  type="button"
                  class="tecnm-btn tecnm-btn-outline-primary tecnm-btn-sm"
                  @click="goToReview"
                >
                  Ver Proyecto &rarr;
                </button>
              </div>
            </div>
          </div>

          <!-- Empty State si no tiene residentes -->
          <div v-else class="empty-residents-box">
            <div class="empty-icon-circle">
              <svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
              </svg>
            </div>
            <h5 style="margin: 0.5rem 0 0.25rem; font-size: 1rem; color: var(--tecnm-text-primary, #0f172a);">
              Docente Disponible
            </h5>
            <p style="margin: 0; font-size: 0.85rem; color: var(--tecnm-text-secondary, #64748b);">
              Este asesor no tiene residentes asignados en este periodo académico.
            </p>
            <button
              v-if="canManageAssignments"
              type="button"
              class="tecnm-btn tecnm-btn-primary tecnm-btn-sm"
              style="margin-top: 1rem;"
              @click="goToAssignments"
            >
              Asignar Residentes a este Asesor &rarr;
            </button>
          </div>
        </div>
      </div>

      <!-- Modal Footer -->
      <div class="tecnm-modal-footer" style="display: flex; justify-content: space-between; align-items: center;">
        <button
          v-if="canManageAssignments"
          type="button"
          class="tecnm-btn tecnm-btn-outline-secondary tecnm-btn-sm"
          @click="goToAssignments"
        >
          Ir a Módulo de Asignaciones &rarr;
        </button>
        <div v-else></div>
        <button type="button" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" @click="closeModal">
          Cerrar
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.modal-backdrop {
  position: fixed;
  inset: 0;
  background-color: rgba(15, 23, 42, 0.6);
  backdrop-filter: blur(3px);
  z-index: 1100;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1rem;
}

.tecnm-modal-dialog {
  background: #ffffff;
  border-radius: 12px;
  box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.15), 0 8px 10px -6px rgba(0, 0, 0, 0.1);
  width: 100%;
  max-width: 780px;
  max-height: 90vh;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border-top: 4px solid var(--tecnm-gold-primary, #D4AF37);
  animation: modalEnter 0.2s ease-out;
}

@keyframes modalEnter {
  from {
    opacity: 0;
    transform: scale(0.96) translateY(8px);
  }
  to {
    opacity: 1;
    transform: scale(1) translateY(0);
  }
}

.tecnm-modal-header {
  padding: 1.25rem 1.5rem;
  border-bottom: 1px solid var(--tecnm-border-color, #e2e8f0);
  display: flex;
  justify-content: space-between;
  align-items: center;
  background-color: #ffffff;
}

.tecnm-modal-title {
  font-size: 1.125rem;
  font-weight: 700;
  color: var(--tecnm-blue-primary, #1B396A);
  margin: 0;
}

.tecnm-modal-close {
  background: transparent;
  border: none;
  font-size: 1.5rem;
  line-height: 1;
  color: var(--tecnm-text-secondary, #64748b);
  cursor: pointer;
  padding: 0.25rem 0.5rem;
  border-radius: 6px;
  transition: all 0.2s;
}

.tecnm-modal-close:hover {
  background-color: #f1f5f9;
  color: #0f172a;
}

.modal-body-loading {
  padding: 3rem 1.5rem;
  text-align: center;
  color: var(--tecnm-text-secondary, #64748b);
}

.spinner {
  width: 32px;
  height: 32px;
  border: 3px solid #e2e8f0;
  border-top-color: var(--tecnm-blue-primary, #1B396A);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  margin: 0 auto 1rem;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.modal-body-content {
  overflow-y: auto;
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.advisor-summary-banner {
  display: flex;
  gap: 1.25rem;
  padding: 1.25rem;
  background: linear-gradient(135deg, #f8fafc 0%, #f1f5f9 100%);
  border: 1px solid var(--tecnm-border-color, #e2e8f0);
  border-radius: 10px;
}

.advisor-avatar-circle {
  width: 60px;
  height: 60px;
  border-radius: 50%;
  background: linear-gradient(135deg, #1B396A 0%, #2563eb 100%);
  color: #ffffff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.25rem;
  font-weight: 700;
  flex-shrink: 0;
  box-shadow: 0 4px 6px -1px rgba(27, 57, 106, 0.2);
}

.advisor-info-col {
  flex: 1;
  min-width: 0;
}

.advisor-name-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-bottom: 0.25rem;
}

.advisor-name {
  font-size: 1.15rem;
  font-weight: 700;
  color: var(--tecnm-text-primary, #0f172a);
  margin: 0;
}

.advisor-meta-row {
  font-size: 0.825rem;
  color: var(--tecnm-text-secondary, #64748b);
  display: flex;
  align-items: center;
  gap: 0.4rem;
  margin-bottom: 0.5rem;
  flex-wrap: wrap;
}

.advisor-title-badge {
  font-weight: 600;
  color: var(--tecnm-blue-primary, #1B396A);
}

.meta-sep {
  color: #cbd5e1;
}

.advisor-contact-row {
  display: flex;
  gap: 1rem;
  font-size: 0.8rem;
  flex-wrap: wrap;
  margin-bottom: 0;
}

.advisor-contact-link,
.advisor-contact-phone {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  color: var(--tecnm-blue-primary, #1B396A);
  text-decoration: none;
}

.advisor-contact-link:hover {
  text-decoration: underline;
}

.residents-section {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.residents-section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.residents-title {
  font-size: 0.95rem;
  font-weight: 700;
  color: var(--tecnm-blue-primary, #1B396A);
  margin: 0;
}

.residents-list {
  display: flex;
  flex-direction: column;
  gap: 0.625rem;
}

.resident-card-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.875rem 1rem;
  border: 1px solid var(--tecnm-border-color, #e2e8f0);
  border-radius: 8px;
  background-color: #ffffff;
  gap: 1rem;
  transition: border-color 0.2s, box-shadow 0.2s;
}

.resident-card-row:hover {
  border-color: #cbd5e1;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.04);
}

.resident-left-col {
  display: flex;
  align-items: center;
  gap: 0.875rem;
  min-width: 0;
  flex: 1;
}

.resident-avatar {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  background: #f1f5f9;
  color: var(--tecnm-blue-primary, #1B396A);
  font-weight: 700;
  font-size: 0.9rem;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.resident-name {
  font-size: 0.925rem;
  font-weight: 600;
  color: var(--tecnm-text-primary, #0f172a);
}

.resident-meta {
  font-size: 0.775rem;
  color: var(--tecnm-text-secondary, #64748b);
  display: flex;
  align-items: center;
  gap: 0.4rem;
  margin-top: 0.1rem;
}

.resident-project-line {
  font-size: 0.8rem;
  color: var(--tecnm-text-primary, #0f172a);
  margin-top: 0.25rem;
}

.project-tag {
  color: var(--tecnm-text-secondary, #64748b);
  font-size: 0.75rem;
  margin-right: 0.25rem;
}

.company-tag {
  color: var(--tecnm-text-secondary, #64748b);
  font-size: 0.75rem;
  margin-left: 0.35rem;
}

.resident-no-project {
  font-size: 0.775rem;
  color: #94a3b8;
  font-style: italic;
  margin-top: 0.2rem;
}

.resident-right-col {
  display: flex;
  align-items: center;
  gap: 1rem;
  flex-shrink: 0;
}

.resident-status-box {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 0.25rem;
}

.advisory-count-badge {
  font-size: 0.725rem;
  color: var(--tecnm-text-secondary, #64748b);
  background: #f1f5f9;
  padding: 0.1rem 0.4rem;
  border-radius: 4px;
}

.empty-residents-box {
  text-align: center;
  padding: 2.5rem 1.5rem;
  background: #f8fafc;
  border: 1px dashed #cbd5e1;
  border-radius: 8px;
}

.empty-icon-circle {
  width: 52px;
  height: 52px;
  border-radius: 50%;
  background: #ecfdf5;
  color: #10b981;
  display: flex;
  align-items: center;
  justify-content: center;
  margin: 0 auto;
}

.tecnm-modal-footer {
  padding: 1rem 1.5rem;
  border-top: 1px solid var(--tecnm-border-color, #e2e8f0);
  background: #ffffff;
}
</style>

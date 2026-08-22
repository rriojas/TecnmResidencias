<script setup>
import { ref, onMounted, computed } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useConfirm } from '@/composables/useConfirm'
import { useAudit } from '@/composables/useAudit'
import apiClient from '@/services/api'
import TecnmBadge from '@/components/common/TecnmBadge.vue'

const authStore = useAuthStore()
const { confirm } = useConfirm()
const { showAudit } = useAudit()

// Estado
const companies = ref([])
const includeInactive = ref(false)
const isLoading = ref(false)

// Notificaciones
const alertMessage = ref('')
const alertType = ref('success')
let alertTimer = null

function showAlert(msg, type = 'success') {
  alertMessage.value = msg
  alertType.value = type
  clearTimeout(alertTimer)
  alertTimer = setTimeout(() => {
    alertMessage.value = ''
  }, 4500)
}

// Modal Form
const isModalOpen = ref(false)
const isEditMode = ref(false)
const editingCompanyId = ref(null)
const isSubmitting = ref(false)
const formError = ref('')

const form = ref({
  name: '',
  rfc: '',
  sector: '',
  address: '',
  contactName: '',
  contactEmail: '',
  contactPhone: '',
})

const canCreate = computed(() => {
  return (
    authStore.isAdmin ||
    authStore.hasPermission('companies.manage') ||
    authStore.hasRole('admin', 'vinculacion', 'departmenthead')
  )
})

async function loadCompanies() {
  isLoading.value = true
  try {
    const params = {
      includeInactive: includeInactive.value,
    }
    const res = await apiClient.get('/v1/companies', { params })
    companies.value = Array.isArray(res.data) ? res.data : (res.data.items || [])
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al cargar directorio de empresas.', 'danger')
    companies.value = []
  } finally {
    isLoading.value = false
  }
}

function openCreateModal() {
  isEditMode.value = false
  editingCompanyId.value = null
  form.value = {
    name: '',
    rfc: '',
    sector: '',
    address: '',
    contactName: '',
    contactEmail: '',
    contactPhone: '',
  }
  formError.value = ''
  isModalOpen.value = true
}

async function openEditModal(company) {
  try {
    const res = await apiClient.get(`/v1/companies/${company.id}`)
    const c = res.data
    isEditMode.value = true
    editingCompanyId.value = c.id
    form.value = {
      name: c.name || '',
      rfc: (c.rfc || '').toUpperCase(),
      sector: c.sector || '',
      address: c.address || '',
      contactName: c.contactName || '',
      contactEmail: c.contactEmail || '',
      contactPhone: c.contactPhone || '',
    }
    formError.value = ''
    isModalOpen.value = true
  } catch {
    showAlert('Error al cargar datos de la empresa.', 'danger')
  }
}

async function handleSubmit() {
  formError.value = ''

  if (!form.value.name.trim()) {
    formError.value = 'Ingrese el nombre o razón social de la empresa.'
    return
  }
  if (!form.value.rfc.trim()) {
    formError.value = 'Ingrese el RFC de la empresa.'
    return
  }
  if (!form.value.contactName.trim()) {
    formError.value = 'Ingrese el nombre del contacto principal.'
    return
  }
  if (!form.value.contactEmail.trim()) {
    formError.value = 'Ingrese el correo electrónico de contacto.'
    return
  }

  isSubmitting.value = true

  const payload = {
    name: form.value.name.trim(),
    rfc: form.value.rfc.trim().toUpperCase(),
    sector: form.value.sector.trim() || undefined,
    address: form.value.address.trim() || undefined,
    contactName: form.value.contactName.trim(),
    contactEmail: form.value.contactEmail.trim(),
    contactPhone: form.value.contactPhone.trim() || undefined,
  }

  try {
    if (isEditMode.value) {
      await apiClient.put(`/v1/companies/${editingCompanyId.value}`, payload)
      showAlert('Empresa receptora actualizada correctamente.', 'success')
    } else {
      await apiClient.post('/v1/companies', payload)
      showAlert('Empresa receptora registrada exitosamente.', 'success')
    }
    isModalOpen.value = false
    loadCompanies()
  } catch (err) {
    formError.value =
      err.response?.data?.message ||
      'Error al guardar la empresa. Verifique los datos ingresados.'
  } finally {
    isSubmitting.value = false
  }
}

async function handleDeactivate(company) {
  const confirmed = await confirm({
    title: 'Desactivar Empresa',
    message: `¿Está seguro de desactivar a la empresa "${company.name}"?`,
    okText: 'Desactivar',
    cancelText: 'Cancelar',
  })
  if (!confirmed) return

  try {
    await apiClient.delete(`/v1/companies/${company.id}`)
    showAlert('Empresa desactivada correctamente.', 'success')
    loadCompanies()
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al desactivar empresa.', 'danger')
  }
}

async function handleReactivate(company) {
  try {
    await apiClient.patch(`/v1/companies/${company.id}/activate`)
    showAlert('Empresa reactivada correctamente.', 'success')
    loadCompanies()
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al reactivar empresa.', 'danger')
  }
}

function handleAudit(company) {
  showAudit({
    title: `Auditoría — Empresa ${company.name}`,
    item: company,
  })
}

onMounted(() => {
  loadCompanies()
})
</script>

<template>
  <div>
    <!-- Notificación -->
    <div
      v-if="alertMessage"
      id="alertContainer"
      class="tecnm-alert"
      :class="`tecnm-alert-${alertType}`"
      role="alert"
    >
      <span>{{ alertMessage }}</span>
    </div>

    <!-- Barra de Acciones -->
    <div class="tecnm-actions-bar">
      <div>
        <h1 class="tecnm-page-title">Directorio de Empresas Receptoras</h1>
        <p class="tecnm-page-subtitle">Gestión y catálogo de instituciones y organizaciones vinculadas a residencias profesionales</p>
      </div>
      <div class="tecnm-page-actions">
        <button
          v-if="canCreate"
          id="openCompanyModalBtn"
          type="button"
          class="tecnm-btn tecnm-btn-primary"
          @click="openCreateModal"
        >
          + Registrar Nueva Empresa
        </button>
      </div>
    </div>

    <!-- Tarjeta Principal de Tabla -->
    <div class="tecnm-card">
      <div class="tecnm-card-header">
        <h3 class="tecnm-card-title">Empresas Registradas</h3>
      </div>
      <div class="tecnm-card-toolbar">
        <div class="tecnm-toolbar-actions">
          <label class="tecnm-switch-label">
            <span class="tecnm-switch">
              <input
                id="companyIncludeInactiveToggle"
                v-model="includeInactive"
                type="checkbox"
                @change="loadCompanies"
              />
              <span class="tecnm-switch-slider"></span>
            </span>
            Mostrar inactivas
          </label>
          <button
            id="refreshCompaniesBtn"
            type="button"
            class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
            @click="loadCompanies"
          >
            Recargar Lista
          </button>
        </div>
      </div>

      <div class="tecnm-card-body">
        <div class="tecnm-table-responsive">
          <table class="tecnm-table tecnm-table-striped">
            <thead>
              <tr>
                <th>Razón Social / Nombre</th>
                <th>RFC</th>
                <th>Sector</th>
                <th>Contacto Principal</th>
                <th>Correo / Teléfono</th>
                <th>Estado</th>
                <th>Acciones</th>
              </tr>
            </thead>
            <tbody id="companiesTableBody">
              <tr v-if="isLoading">
                <td colspan="7" class="tecnm-table-empty">
                  Cargando catálogo de empresas...
                </td>
              </tr>
              <tr v-else-if="companies.length === 0">
                <td colspan="7" class="tecnm-table-empty">
                  No hay empresas receptoras registradas.
                </td>
              </tr>
              <tr
                v-for="c in companies"
                v-else
                :key="c.id"
              >
                <td><strong>{{ c.name }}</strong></td>
                <td>{{ (c.rfc || '—').toUpperCase() }}</td>
                <td>{{ c.sector || '—' }}</td>
                <td>{{ c.contactName || '—' }}</td>
                <td>
                  <div>{{ c.contactEmail || '—' }}</div>
                  <small v-if="c.contactPhone" class="tecnm-text-muted">{{ c.contactPhone }}</small>
                </td>
                <td>
                  <TecnmBadge :status="c.isActive ? 'Activo' : 'Inactivo'" />
                </td>
                <td>
                  <div class="tecnm-row-actions">
                    <button
                      v-if="!authStore.isReadOnly && canCreate"
                      type="button"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                      @click="openEditModal(c)"
                    >
                      Editar
                    </button>
                    <button
                      v-if="authStore.canSeeAudit"
                      type="button"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                      @click="handleAudit(c)"
                    >
                      Auditoría
                    </button>
                    <template v-if="!authStore.isReadOnly && canCreate">
                      <button
                        v-if="c.isActive"
                        type="button"
                        class="tecnm-btn tecnm-btn-danger tecnm-btn-sm"
                        @click="handleDeactivate(c)"
                      >
                        Desactivar
                      </button>
                      <button
                        v-else
                        type="button"
                        class="tecnm-btn tecnm-btn-success tecnm-btn-sm"
                        @click="handleReactivate(c)"
                      >
                        Reactivar
                      </button>
                    </template>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- Modal Registrar / Editar Empresa -->
    <div
      v-if="isModalOpen"
      id="companyModal"
      class="modal-backdrop active"
      role="dialog"
      aria-modal="true"
      @click.self="isModalOpen = false"
    >
      <div class="modal-card">
        <div class="tecnm-modal-header">
          <h3 id="companyModalTitle" class="tecnm-modal-title">
            {{ isEditMode ? 'Editar Empresa Receptora' : 'Registrar Empresa Receptora' }}
          </h3>
          <button
            id="closeCompanyModalBtn"
            type="button"
            class="tecnm-modal-close"
            aria-label="Cerrar"
            @click="isModalOpen = false"
          >
            &times;
          </button>
        </div>

        <form id="companyForm" @submit.prevent="handleSubmit">
          <div
            v-if="formError"
            class="tecnm-alert tecnm-alert-danger"
            style="margin-bottom: 1rem;"
            role="alert"
          >
            <span>{{ formError }}</span>
          </div>

          <div class="tecnm-form-group">
            <label for="companyNameInput" class="tecnm-label">Nombre / Razón Social *</label>
            <input
              id="companyNameInput"
              v-model="form.name"
              type="text"
              class="tecnm-form-control"
              placeholder="Ej. Tecnología e Innovación S.A. de C.V."
              maxlength="200"
              :disabled="isSubmitting"
              required
            />
          </div>

          <div class="tecnm-form-group">
            <label for="companyRfcInput" class="tecnm-label">RFC *</label>
            <input
              id="companyRfcInput"
              v-model="form.rfc"
              type="text"
              class="tecnm-form-control"
              placeholder="Ej. TINO900101ABC"
              maxlength="13"
              style="text-transform: uppercase;"
              :disabled="isSubmitting"
              required
            />
          </div>

          <div class="tecnm-form-group">
            <label for="companySectorInput" class="tecnm-label">Sector Industrial / Servicios</label>
            <input
              id="companySectorInput"
              v-model="form.sector"
              type="text"
              class="tecnm-form-control"
              placeholder="Ej. Tecnológico, Industrial, Público, Privado"
              maxlength="100"
              :disabled="isSubmitting"
            />
          </div>

          <div class="tecnm-form-group">
            <label for="companyAddressInput" class="tecnm-label">Dirección Fiscal / Ubicación</label>
            <input
              id="companyAddressInput"
              v-model="form.address"
              type="text"
              class="tecnm-form-control"
              placeholder="Ej. Av. Tecnológico #123, Monclova, Coahuila"
              maxlength="300"
              :disabled="isSubmitting"
            />
          </div>

          <div class="tecnm-form-group">
            <label for="companyContactNameInput" class="tecnm-label">Nombre del Contacto Principal *</label>
            <input
              id="companyContactNameInput"
              v-model="form.contactName"
              type="text"
              class="tecnm-form-control"
              placeholder="Ej. Ing. Roberto García"
              maxlength="150"
              :disabled="isSubmitting"
              required
            />
          </div>

          <div class="tecnm-form-group">
            <label for="companyContactEmailInput" class="tecnm-label">Correo Electrónico de Contacto *</label>
            <input
              id="companyContactEmailInput"
              v-model="form.contactEmail"
              type="email"
              class="tecnm-form-control"
              placeholder="contacto@empresa.com"
              maxlength="150"
              :disabled="isSubmitting"
              required
            />
          </div>

          <div class="tecnm-form-group">
            <label for="companyContactPhoneInput" class="tecnm-label">Teléfono de Contacto</label>
            <input
              id="companyContactPhoneInput"
              v-model="form.contactPhone"
              type="tel"
              class="tecnm-form-control"
              placeholder="Ej. 866-123-4567"
              maxlength="30"
              :disabled="isSubmitting"
            />
          </div>

          <div class="tecnm-modal-footer">
            <button
              id="cancelCompanyModalBtn"
              type="button"
              class="tecnm-btn tecnm-btn-secondary"
              :disabled="isSubmitting"
              @click="isModalOpen = false"
            >
              Cancelar
            </button>
            <button
              id="submitCompanyBtn"
              type="submit"
              class="tecnm-btn tecnm-btn-primary"
              :disabled="isSubmitting"
            >
              <span v-if="!isSubmitting">Guardar Empresa</span>
              <span v-else class="login-spinner"></span>
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<style scoped>
.tecnm-row-actions {
  display: inline-flex;
  gap: 0.35rem;
}
</style>

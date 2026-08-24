<script setup>
import { ref, onMounted, computed } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useConfirm } from '@/composables/useConfirm'
import { useAudit } from '@/composables/useAudit'
import { useGlobalSearch } from '@/composables/useGlobalSearch'
import apiClient from '@/services/api'
import TecnmBadge from '@/components/common/TecnmBadge.vue'

const authStore = useAuthStore()
const { confirm } = useConfirm()
const { showAudit } = useAudit()
const { openSearch } = useGlobalSearch()

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

const canImport = computed(() => {
  return (
    authStore.isAdmin ||
    authStore.hasPermission('companies.import.excel') ||
    authStore.hasRole('admin', 'vinculacion')
  )
})

// Modal Importar Excel
const isImportModalOpen = ref(false)
const importFile = ref(null)
const isImporting = ref(false)
const importError = ref('')
const importResult = ref(null)

function openImportModal() {
  importFile.value = null
  isImporting.value = false
  importError.value = ''
  importResult.value = null
  isImportModalOpen.value = true
}

function handleFileChange(event) {
  const files = event.target.files
  if (files && files.length > 0) {
    importFile.value = files[0]
  } else {
    importFile.value = null
  }
}

async function handleImportSubmit() {
  importError.value = ''
  importResult.value = null

  if (!importFile.value) {
    importError.value = 'Seleccione un archivo Excel (.xlsx o .xls).'
    return
  }

  isImporting.value = true
  const formData = new FormData()
  formData.append('file', importFile.value)

  try {
    const res = await apiClient.post('/v1/companies/import-excel', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    importResult.value = res.data
    showAlert(`Importación finalizada. ${res.data.successCount} empresas creadas.`, 'success')
    loadCompanies()
  } catch (err) {
    importError.value =
      err.response?.data?.message ||
      'Error al procesar el archivo Excel. Verifique que cumpla con el formato y columnas requeridas.'
  } finally {
    isImporting.value = false
  }
}

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

async function handleDownloadCompanyTemplate() {
  try {
    const res = await apiClient.get('/v1/companies/import/template', { responseType: 'blob' })
    const blob = new Blob([res.data], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = 'Plantilla_Empresas.xlsx'
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    window.URL.revokeObjectURL(url)
  } catch {
    showAlert('Error al descargar la plantilla de empresas.', 'danger')
  }
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
          type="button"
          class="tecnm-btn tecnm-btn-secondary"
          @click="openSearch({ initialSource: 'COMPANIES' })"
        >
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
          </svg>
          <span>Abrir búsqueda</span>
        </button>
        <button
          v-if="canImport"
          id="openImportCompanyModalBtn"
          type="button"
          class="tecnm-btn tecnm-btn-secondary"
          @click="openImportModal"
        >
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" style="margin-right: 0.35rem; display: inline-block; vertical-align: middle;">
            <path stroke-linecap="round" stroke-linejoin="round" d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5m-13.5-9L12 3m0 0l4.5 4.5M12 3v13.5" />
          </svg>
          <span>Importar Excel</span>
        </button>
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

    <!-- Modal Importar Excel Empresas -->
    <div
      v-if="isImportModalOpen"
      id="importCompanyModal"
      class="modal-backdrop active"
      role="dialog"
      aria-modal="true"
      @click.self="isImportModalOpen = false"
    >
      <div class="modal-card" style="max-width: 600px;">
        <div class="tecnm-modal-header">
          <h3 class="tecnm-modal-title"> Carga Masiva de Empresas vía Excel</h3>
          <button
            type="button"
            class="tecnm-modal-close"
            aria-label="Cerrar"
            @click="isImportModalOpen = false"
          >
            &times;
          </button>
        </div>

        <form @submit.prevent="handleImportSubmit">
          <div class="tecnm-alert tecnm-alert-warning" style="margin-bottom: 1rem;">
            <div style="display: flex; justify-content: space-between; align-items: flex-start; gap: 0.75rem; flex-wrap: wrap;">
              <div>
                <strong>Requisito Estricto de Columnas:</strong><br />
                El archivo Excel debe contener exactamente las siguientes columnas en la primera fila:<br />
                <code>Nombre, RFC, Sector, Dirección, NombreContacto, CorreoContacto, TeléfonoContacto</code>
              </div>
              <button
                type="button"
                class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                style="margin-top: 0.25rem;"
                @click="handleDownloadCompanyTemplate"
              >
                Descargar Plantilla Excel
              </button>
            </div>
          </div>

          <div v-if="importError" class="tecnm-alert tecnm-alert-danger" style="margin-bottom: 1rem;" role="alert">
            <span>{{ importError }}</span>
          </div>

          <div v-if="importResult" class="tecnm-alert tecnm-alert-info" style="margin-bottom: 1rem;">
            <strong>Resumen de Importación:</strong>
            <ul>
              <li>Filas procesadas: {{ importResult.totalRows }}</li>
              <li>Empresas registradas: {{ importResult.successCount }}</li>
              <li>Omitidas (Duplicadas): {{ importResult.skippedCount }}</li>
              <li>Errores de fila: {{ importResult.errorCount }}</li>
            </ul>
            <div v-if="importResult.errors && importResult.errors.length > 0" style="margin-top: 0.5rem; max-height: 120px; overflow-y: auto;">
              <small class="tecnm-text-danger">
                <strong>Detalle de errores:</strong>
                <ul>
                  <li v-for="(e, idx) in importResult.errors" :key="idx">{{ e }}</li>
                </ul>
              </small>
            </div>
            <div v-if="importResult.skipped && importResult.skipped.length > 0" style="margin-top: 0.5rem; max-height: 100px; overflow-y: auto;">
              <small class="tecnm-text-muted">
                <strong>Omitidas:</strong>
                <ul>
                  <li v-for="(s, idx) in importResult.skipped" :key="idx">{{ s }}</li>
                </ul>
              </small>
            </div>
          </div>

          <div class="tecnm-form-group">
            <label for="companyExcelFile" class="tecnm-label">Seleccionar Archivo Excel (.xlsx / .xls) *</label>
            <input
              id="companyExcelFile"
              type="file"
              accept=".xlsx, .xls"
              class="tecnm-form-control"
              :disabled="isImporting"
              required
              @change="handleFileChange"
            />
          </div>

          <div class="tecnm-modal-footer">
            <button
              type="button"
              class="tecnm-btn tecnm-btn-secondary"
              :disabled="isImporting"
              @click="isImportModalOpen = false"
            >
              Cerrar
            </button>
            <button
              type="submit"
              class="tecnm-btn tecnm-btn-primary"
              :disabled="isImporting || !importFile"
            >
              <span v-if="!isImporting">Subir e Importar</span>
              <span v-else class="login-spinner">Procesando...</span>
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

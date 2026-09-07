<script setup>
import { ref, onMounted, computed, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useConfirm } from '@/composables/useConfirm'
import { useAudit } from '@/composables/useAudit'
import { useGlobalSearch } from '@/composables/useGlobalSearch'
import apiClient from '@/services/api'
import TecnmBadge from '@/components/common/TecnmBadge.vue'
import TecnmPagination from '@/components/common/TecnmPagination.vue'

const route = useRoute()
const authStore = useAuthStore()
const { confirm } = useConfirm()
const { showAudit } = useAudit()
const { open: openSearch } = useGlobalSearch()

// Pestañas
const activeTab = ref('companies') // 'companies' | 'agreements'

const canViewAgreements = computed(() => {
  return authStore.isAdmin || authStore.hasRole('vinculacion')
})

function switchTab(tab) {
  activeTab.value = tab
  if (tab === 'agreements') {
    loadAgreements()
  } else {
    loadCompanies()
  }
}

// ==========================================
// ESTADO: DIRECTORIO DE EMPRESAS
// ==========================================
const companies = ref([])
const isLoading = ref(false)

// Paginación y Filtros de Empresas
const pageNumber = ref(1)
const pageSize = ref(10)
const totalCount = ref(0)
const totalPages = ref(0)
const searchTerm = ref('')
const includeInactive = ref(false)
const sortBy = ref('Name')
const sortDir = ref('asc')

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

// Modal Formulario de Empresa
const isModalOpen = ref(false)
const isEditMode = ref(false)
const editingCompanyId = ref(null)
const isSubmitting = ref(false)
const formError = ref('')

const form = ref({
  name: '',
  legalName: '',
  tradeName: '',
  rfc: '',
  sector: '',
  street: '',
  number: '',
  colonia: '',
  city: '',
  state: '',
  postalCode: '',
  contactName: '',
  contactEmail: '',
  contactPhone: '',
})

const canCreate = computed(() => {
  return (
    authStore.isAdmin ||
    authStore.hasRole('admin', 'vinculacion', 'director')
  )
})

const canImport = computed(() => {
  return (
    authStore.isAdmin ||
    authStore.hasRole('admin', 'vinculacion', 'director')
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
    showAlert(`Importación finalizada. ${res.data.successCount} empresas procesadas exitosamente.`, 'success')
    pageNumber.value = 1
    loadCompanies()
  } catch (err) {
    importError.value =
      err.response?.data?.message ||
      'Error al procesar el archivo Excel. Verifique que cumpla con el formato y todas las columnas obligatorias.'
  } finally {
    isImporting.value = false
  }
}

function handleSort(field) {
  if (sortBy.value.toLowerCase() === field.toLowerCase()) {
    sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
  } else {
    sortBy.value = field
    sortDir.value = 'asc'
  }
  pageNumber.value = 1
  loadCompanies({ silent: true })
}

function handleOpenSearch() {
  openSearch({
    initialSource: 'COMPANIES',
    onSelect: (item) => {
      if (!item) return
      searchTerm.value = item.name || item.rfc || String(item.id || '')
      pageNumber.value = 1
      loadCompanies()
    },
  })
}

function handleReload() {
  searchTerm.value = ''
  pageNumber.value = 1
  loadCompanies()
}

function handleSearchSubmit() {
  pageNumber.value = 1
  loadCompanies()
}

function clearSearch() {
  searchTerm.value = ''
  pageNumber.value = 1
  loadCompanies()
}

function handlePageChange(newPage) {
  pageNumber.value = newPage
  loadCompanies()
}

function handleIncludeInactiveChange() {
  pageNumber.value = 1
  loadCompanies()
}

async function loadCompanies({ silent = false } = {}) {
  if (!silent) isLoading.value = true
  try {
    const params = {
      pageNumber: pageNumber.value,
      pageSize: pageSize.value,
      search: searchTerm.value.trim() || undefined,
      includeInactive: includeInactive.value,
      sortBy: sortBy.value,
      sortDir: sortDir.value,
    }
    const res = await apiClient.get('/v1/companies', { params })
    const data = res.data
    companies.value = Array.isArray(data) ? data : (data.items || [])
    totalCount.value = data.totalCount ?? companies.value.length
    totalPages.value = data.totalPages ?? Math.ceil(totalCount.value / pageSize.value) ?? 1
  } catch (err) {
    if (!silent) {
      showAlert(err.response?.data?.message || 'Error al cargar directorio de empresas.', 'danger')
      companies.value = []
    }
  } finally {
    if (!silent) isLoading.value = false
  }
}

function openCreateModal() {
  isEditMode.value = false
  editingCompanyId.value = null
  form.value = {
    name: '',
    legalName: '',
    tradeName: '',
    rfc: '',
    sector: '',
    street: '',
    number: '',
    colonia: '',
    city: '',
    state: '',
    postalCode: '',
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
      legalName: c.legalName || c.name || '',
      tradeName: c.tradeName || c.name || '',
      rfc: (c.rfc || '').toUpperCase(),
      sector: c.sector || '',
      street: c.street || '',
      number: c.number || '',
      colonia: c.colonia || '',
      city: c.city || '',
      state: c.state || '',
      postalCode: c.postalCode || '',
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

  const nameVal = form.value.name.trim() || form.value.legalName.trim() || form.value.tradeName.trim()
  if (!nameVal) {
    formError.value = 'Ingrese el nombre o razón social de la empresa.'
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
    name: nameVal,
    legalName: form.value.legalName.trim() || nameVal,
    tradeName: form.value.tradeName.trim() || nameVal,
    rfc: form.value.rfc.trim() ? form.value.rfc.trim().toUpperCase() : null,
    sector: form.value.sector.trim() || undefined,
    street: form.value.street.trim() || undefined,
    number: form.value.number.trim() || undefined,
    colonia: form.value.colonia.trim() || undefined,
    city: form.value.city.trim() || undefined,
    state: form.value.state.trim() || undefined,
    postalCode: form.value.postalCode.trim() || undefined,
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

// ==========================================
// ESTADO: SECCIÓN CONVENIOS INSTITUCIONALES
// ==========================================
const agreements = ref([])
const isAgreementsLoading = ref(false)
const agreementPageNumber = ref(1)
const agreementPageSize = ref(10)
const agreementTotalCount = ref(0)
const agreementTotalPages = ref(0)
const agreementSearchTerm = ref('')
const agreementStatusFilter = ref('all')

// Modal Convenio
const isAgreementModalOpen = ref(false)
const isAgreementSubmitting = ref(false)
const agreementFormError = ref('')
const selectedAgreementCompany = ref(null)

const agreementForm = ref({
  companyId: null,
  archiveId: '',
  status: '1 VIGENTE',
  pitCode: '5.1.2',
  ciaType: 'USO COMPARTIDO',
  agreementScope: 'GENERAL',
  sector: 'PRIVADO',
  businessLine: '',
  companySize: 'MEDIANAS EMPRESAS',
  geographicScope: 'NACIONAL',
  notes: '',
})

async function loadAgreements({ silent = false } = {}) {
  if (!canViewAgreements.value) return
  if (!silent) isAgreementsLoading.value = true
  try {
    const params = {
      pageNumber: agreementPageNumber.value,
      pageSize: agreementPageSize.value,
      search: agreementSearchTerm.value.trim() || undefined,
      status: agreementStatusFilter.value !== 'all' ? agreementStatusFilter.value : undefined,
    }
    const res = await apiClient.get('/v1/companies/agreements', { params })
    const data = res.data
    agreements.value = Array.isArray(data) ? data : (data.items || [])
    agreementTotalCount.value = data.totalCount ?? agreements.value.length
    agreementTotalPages.value = data.totalPages ?? Math.ceil(agreementTotalCount.value / agreementPageSize.value) ?? 1
  } catch (err) {
    if (!silent) {
      showAlert(err.response?.data?.message || 'Error al cargar convenios institucionales.', 'danger')
      agreements.value = []
    }
  } finally {
    if (!silent) isAgreementsLoading.value = false
  }
}

function handleAgreementSearchSubmit() {
  agreementPageNumber.value = 1
  loadAgreements()
}

function clearAgreementSearch() {
  agreementSearchTerm.value = ''
  agreementPageNumber.value = 1
  loadAgreements()
}

function handleAgreementPageChange(newPage) {
  agreementPageNumber.value = newPage
  loadAgreements()
}

function onAgreementStatusFilterChange() {
  agreementPageNumber.value = 1
  loadAgreements()
}

async function openAgreementModalForCompany(company) {
  selectedAgreementCompany.value = company
  agreementFormError.value = ''
  agreementForm.value = {
    companyId: company.id,
    archiveId: '',
    status: '1 VIGENTE',
    pitCode: '5.1.2',
    ciaType: 'USO COMPARTIDO',
    agreementScope: 'GENERAL',
    sector: company.sector || 'PRIVADO',
    businessLine: '',
    companySize: 'MEDIANAS EMPRESAS',
    geographicScope: 'NACIONAL',
    notes: '',
  }

  try {
    const res = await apiClient.get(`/v1/companies/${company.id}/agreement`)
    if (res.data) {
      const a = res.data
      agreementForm.value = {
        companyId: company.id,
        archiveId: a.archiveId || '',
        status: a.status || '1 VIGENTE',
        pitCode: a.pitCode || '5.1.2',
        ciaType: a.ciaType || 'USO COMPARTIDO',
        agreementScope: a.agreementScope || 'GENERAL',
        sector: a.sector || company.sector || 'PRIVADO',
        businessLine: a.businessLine || '',
        companySize: a.companySize || 'MEDIANAS EMPRESAS',
        geographicScope: a.geographicScope || 'NACIONAL',
        notes: a.notes || '',
      }
    }
  } catch {
    // Si no existe convenio aún, se usa el template por defecto
  }

  isAgreementModalOpen.value = true
}

function openEditAgreementModal(agreement) {
  selectedAgreementCompany.value = {
    id: agreement.companyId,
    name: agreement.companyName,
    legalName: agreement.companyLegalName,
    tradeName: agreement.companyTradeName,
    rfc: agreement.companyRfc,
  }
  agreementFormError.value = ''
  agreementForm.value = {
    companyId: agreement.companyId,
    archiveId: agreement.archiveId || '',
    status: agreement.status || '1 VIGENTE',
    pitCode: agreement.pitCode || '5.1.2',
    ciaType: agreement.ciaType || 'USO COMPARTIDO',
    agreementScope: agreement.agreementScope || 'GENERAL',
    sector: agreement.sector || 'PRIVADO',
    businessLine: agreement.businessLine || '',
    companySize: agreement.companySize || 'MEDIANAS EMPRESAS',
    geographicScope: agreement.geographicScope || 'NACIONAL',
    notes: agreement.notes || '',
  }
  isAgreementModalOpen.value = true
}

async function handleAgreementSubmit() {
  agreementFormError.value = ''
  if (!agreementForm.value.companyId) {
    agreementFormError.value = 'Empresa no seleccionada.'
    return
  }

  isAgreementSubmitting.value = true
  const payload = {
    archiveId: agreementForm.value.archiveId.trim() || undefined,
    status: agreementForm.value.status,
    pitCode: agreementForm.value.pitCode.trim() || undefined,
    ciaType: agreementForm.value.ciaType.trim() || undefined,
    agreementScope: agreementForm.value.agreementScope.trim() || undefined,
    sector: agreementForm.value.sector.trim() || undefined,
    businessLine: agreementForm.value.businessLine.trim() || undefined,
    companySize: agreementForm.value.companySize.trim() || undefined,
    geographicScope: agreementForm.value.geographicScope.trim() || undefined,
    notes: agreementForm.value.notes.trim() || undefined,
  }

  try {
    await apiClient.put(`/v1/companies/${agreementForm.value.companyId}/agreement`, payload)
    showAlert('Convenio institucional guardado exitosamente.', 'success')
    isAgreementModalOpen.value = false
    if (activeTab.value === 'agreements') {
      loadAgreements()
    } else {
      loadCompanies()
    }
  } catch (err) {
    agreementFormError.value =
      err.response?.data?.message || 'Error al guardar los datos del convenio.'
  } finally {
    isAgreementSubmitting.value = false
  }
}

function getAgreementBadgeClass(status) {
  const s = String(status || '').toUpperCase()
  if (s.includes('1') || s.includes('VIGENTE')) return 'tecnm-badge-success'
  if (s.includes('2') || s.includes('VENCIDO')) return 'tecnm-badge-danger'
  if (s.includes('3') || s.includes('STAND BY') || s.includes('STAND')) return 'tecnm-badge-warning'
  if (s.includes('4') || s.includes('RENOVAR')) return 'tecnm-badge-info'
  return 'tecnm-badge-secondary'
}

watch(
  () => route.query.id,
  async (newId) => {
    if (newId) {
      try {
        const res = await apiClient.get(`/v1/companies/${newId}`)
        if (res.data) {
          searchTerm.value = res.data.name || res.data.rfc || ''
          pageNumber.value = 1
          loadCompanies()
        }
      } catch {}
    }
  }
)

onMounted(() => {
  if (route.query.tab === 'agreements' && canViewAgreements.value) {
    activeTab.value = 'agreements'
    loadAgreements()
  } else {
    if (route.query.search) {
      searchTerm.value = String(route.query.search)
    }
    if (route.query.id) {
      apiClient.get(`/v1/companies/${route.query.id}`).then((res) => {
        if (res.data) {
          searchTerm.value = res.data.name || res.data.rfc || ''
        }
        loadCompanies()
      }).catch(() => loadCompanies())
    } else {
      loadCompanies()
    }
  }
})
</script>

<template>
  <div>
    <!-- Notificación Flotante / Encabezado -->
    <div
      v-if="alertMessage"
      id="alertContainer"
      class="tecnm-alert"
      :class="`tecnm-alert-${alertType}`"
      role="alert"
    >
      <span>{{ alertMessage }}</span>
    </div>

    <!-- Barra de Acciones Superior -->
    <div class="tecnm-actions-bar">
      <div>
        <h1 class="tecnm-page-title">
          {{ activeTab === 'companies' ? 'Directorio de Empresas Receptoras' : 'Convenios Institucionales con Empresas' }}
        </h1>
        <p class="tecnm-page-subtitle">
          {{ activeTab === 'companies' ? 'Gestión, consulta y catálogo de organizaciones vinculadas a residencias profesionales' : 'Módulo exclusivo de vinculación para control y seguimiento de convenios formales' }}
        </p>
      </div>
      <div class="tecnm-page-actions">
        <button
          type="button"
          class="tecnm-btn tecnm-btn-secondary"
          @click="handleOpenSearch"
        >
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
          </svg>
          <span>Abrir búsqueda</span>
        </button>
        <button
          v-if="canImport && activeTab === 'companies'"
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
          v-if="canCreate && activeTab === 'companies'"
          id="openCompanyModalBtn"
          type="button"
          class="tecnm-btn tecnm-btn-primary"
          @click="openCreateModal"
        >
          + Registrar Nueva Empresa
        </button>
      </div>
    </div>

    <!-- Pestañas Institucionales (Directorio / Convenios) -->
    <div v-if="canViewAgreements" class="tecnm-tabs" style="margin-bottom: 1.25rem;">
      <button
        id="tabCompaniesBtn"
        type="button"
        class="tecnm-btn tab-btn"
        :class="activeTab === 'companies' ? 'tecnm-btn-primary active' : 'tecnm-btn-secondary'"
        @click="switchTab('companies')"
      >
        Directorio de Empresas
      </button>
      <button
        id="tabAgreementsBtn"
        type="button"
        class="tecnm-btn tab-btn"
        :class="activeTab === 'agreements' ? 'tecnm-btn-primary active' : 'tecnm-btn-secondary'"
        @click="switchTab('agreements')"
      >
        Convenios de Vinculación
      </button>
    </div>

    <!-- ============================================================= -->
    <!-- PESTAÑA 1: DIRECTORIO DE EMPRESAS                            -->
    <!-- ============================================================= -->
    <div v-show="activeTab === 'companies'" class="tecnm-card">
      <div class="tecnm-card-header">
        <h3 class="tecnm-card-title">Empresas Registradas</h3>
      </div>

      <!-- Barra de herramientas: Búsqueda reactiva, Mostrar Inactivas, Recargar -->
      <div class="tecnm-card-toolbar">
        <div style="display: flex; align-items: center; gap: 0.5rem; flex: 1; max-width: 480px;">
          <input
            id="companySearchInput"
            v-model="searchTerm"
            type="text"
            class="tecnm-form-control"
            placeholder="Buscar por nombre, razón social, siglas o RFC..."
            @keyup.enter="handleSearchSubmit"
          />
          <button
            type="button"
            class="tecnm-btn tecnm-btn-primary tecnm-btn-sm"
            @click="handleSearchSubmit"
          >
            Buscar
          </button>
          <button
            v-if="searchTerm"
            type="button"
            class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
            @click="clearSearch"
          >
            Limpiar
          </button>
        </div>

        <div class="tecnm-toolbar-actions">
          <label v-if="!authStore.isCareerHead" class="tecnm-switch-label">
            <span class="tecnm-switch">
              <input
                id="companyIncludeInactiveToggle"
                v-model="includeInactive"
                type="checkbox"
                @change="handleIncludeInactiveChange"
              />
              <span class="tecnm-switch-slider"></span>
            </span>
            Mostrar inactivas
          </label>
          <button
            id="refreshCompaniesBtn"
            type="button"
            class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
            @click="handleReload"
          >
            Recargar Lista
          </button>
        </div>
      </div>

      <div class="tecnm-card-body">
        <div
          v-if="searchTerm"
          class="tecnm-alert tecnm-alert-info"
          style="margin-bottom: 1rem; display: flex; align-items: center; justify-content: space-between;"
        >
          <span>Filtro de búsqueda aplicado: <strong>{{ searchTerm }}</strong></span>
          <button
            type="button"
            class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
            @click="clearSearch"
          >
            Mostrar todas
          </button>
        </div>

        <div class="tecnm-table-responsive">
          <table class="tecnm-table tecnm-table-striped">
            <thead>
              <tr>
                <th class="tecnm-th-sortable" @click="handleSort('Name')">
                  Nombre de la Empresa
                  <span class="tecnm-sort-icon" :class="{ active: sortBy.toLowerCase() === 'name' }">
                    {{ sortBy.toLowerCase() === 'name' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th class="tecnm-th-sortable" @click="handleSort('LegalName')">
                  Razón Social
                  <span class="tecnm-sort-icon" :class="{ active: sortBy.toLowerCase() === 'legalname' }">
                    {{ sortBy.toLowerCase() === 'legalname' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th class="tecnm-th-sortable" @click="handleSort('TradeName')">
                  Nombre Comercial (Siglas)
                  <span class="tecnm-sort-icon" :class="{ active: sortBy.toLowerCase() === 'tradename' }">
                    {{ sortBy.toLowerCase() === 'tradename' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th class="tecnm-th-sortable" @click="handleSort('Rfc')">
                  RFC
                  <span class="tecnm-sort-icon" :class="{ active: sortBy.toLowerCase() === 'rfc' }">
                    {{ sortBy.toLowerCase() === 'rfc' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th>Ubicación</th>
                <th class="tecnm-th-sortable" @click="handleSort('ContactName')">
                  Contacto Principal
                  <span class="tecnm-sort-icon" :class="{ active: sortBy.toLowerCase() === 'contactname' }">
                    {{ sortBy.toLowerCase() === 'contactname' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th class="tecnm-th-sortable" @click="handleSort('IsActive')">
                  Estado
                  <span class="tecnm-sort-icon" :class="{ active: sortBy.toLowerCase() === 'isactive' }">
                    {{ sortBy.toLowerCase() === 'isactive' ? (sortDir === 'asc' ? '↑' : '↓') : '↕' }}
                  </span>
                </th>
                <th v-if="canViewAgreements">Convenio</th>
                <th v-if="!authStore.isStudent" class="tecnm-th-actions">Acciones</th>
              </tr>
            </thead>
            <tbody id="companiesTableBody">
              <tr v-if="isLoading">
                <td :colspan="canViewAgreements ? 9 : 8" class="tecnm-table-empty">
                  Cargando catálogo de empresas...
                </td>
              </tr>
              <tr v-else-if="companies.length === 0">
                <td :colspan="canViewAgreements ? 9 : 8" class="tecnm-table-empty">
                  <span v-if="searchTerm">No se encontraron empresas con el término "{{ searchTerm }}".</span>
                  <span v-else-if="includeInactive">No hay empresas registradas en el catálogo.</span>
                  <span v-else>No hay empresas receptoras registradas.</span>
                </td>
              </tr>
              <tr
                v-for="c in companies"
                v-else
                :key="c.id"
              >
                <td><strong>{{ c.name }}</strong></td>
                <td>{{ c.legalName || '—' }}</td>
                <td>
                  <span v-if="c.tradeName" class="tecnm-badge tecnm-badge-secondary" style="font-weight: 600;">
                    {{ c.tradeName }}
                  </span>
                  <span v-else>—</span>
                </td>
                <td>{{ (c.rfc || '—').toUpperCase() }}</td>
                <td>
                  <div v-if="c.city || c.state">
                    {{ [c.city, c.state].filter(Boolean).join(', ') }}
                  </div>
                  <small v-if="c.street || c.number" class="tecnm-text-muted">
                    {{ c.street }} {{ c.number ? '#' + c.number : '' }}
                  </small>
                  <span v-else-if="!c.city && !c.state">{{ c.address || '—' }}</span>
                </td>
                <td>
                  <div>{{ c.contactName || '—' }}</div>
                  <small class="tecnm-text-muted">{{ c.contactEmail }}</small>
                </td>
                <td>
                  <TecnmBadge :status="c.isActive ? 'Activo' : 'Inactivo'" />
                </td>
                <td v-if="canViewAgreements">
                  <button
                    type="button"
                    class="tecnm-btn tecnm-btn-sm"
                    :class="c.hasAgreement ? 'tecnm-btn-success' : 'tecnm-btn-secondary'"
                    style="font-size: 0.75rem; padding: 0.2rem 0.5rem;"
                    @click="openAgreementModalForCompany(c)"
                  >
                    {{ c.hasAgreement ? 'Con Convenio' : '+ Asignar Convenio' }}
                  </button>
                </td>
                <td v-if="!authStore.isStudent">
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

        <!-- Paginación de Empresas -->
        <div style="margin-top: 1rem;">
          <TecnmPagination
            v-if="totalCount > 0"
            :current-page="pageNumber"
            :page-size="pageSize"
            :total-count="totalCount"
            :total-pages="totalPages"
            @update:current-page="pageNumber = $event"
            @page-change="handlePageChange"
          />
        </div>
      </div>
    </div>

    <!-- ============================================================= -->
    <!-- PESTAÑA 2: CONVENIOS INSTITUCIONALES (VINCULACIÓN / ADMIN)   -->
    <!-- ============================================================= -->
    <div v-show="activeTab === 'agreements'" class="tecnm-card">
      <div class="tecnm-card-header">
        <h3 class="tecnm-card-title">Convenios de Vinculación Registrados</h3>
      </div>

      <!-- Barra de herramientas de Convenios -->
      <div class="tecnm-card-toolbar">
        <div style="display: flex; align-items: center; gap: 0.5rem; flex: 1; max-width: 420px;">
          <input
            id="agreementSearchInput"
            v-model="agreementSearchTerm"
            type="text"
            class="tecnm-form-control"
            placeholder="Buscar por archivo, empresa, giro, PIT..."
            @keyup.enter="handleAgreementSearchSubmit"
          />
          <button
            type="button"
            class="tecnm-btn tecnm-btn-primary tecnm-btn-sm"
            @click="handleAgreementSearchSubmit"
          >
            Buscar
          </button>
          <button
            v-if="agreementSearchTerm"
            type="button"
            class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
            @click="clearAgreementSearch"
          >
            Limpiar
          </button>
        </div>

        <div class="tecnm-toolbar-actions">
          <div style="display: flex; align-items: center; gap: 0.5rem;">
            <label for="agreementStatusFilterSelect" style="font-size: 0.85rem; font-weight: 500;">Status:</label>
            <select
              id="agreementStatusFilterSelect"
              v-model="agreementStatusFilter"
              class="tecnm-form-control"
              style="min-width: 150px; font-size: 0.85rem;"
              @change="onAgreementStatusFilterChange"
            >
              <option value="all">Todos los Status</option>
              <option value="1 VIGENTE">1 VIGENTE</option>
              <option value="2 VENCIDO">2 VENCIDO</option>
              <option value="3 STAND BY">3 STAND BY</option>
              <option value="4 RENOVAR">4 RENOVAR</option>
            </select>
          </div>
          <button
            type="button"
            class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
            @click="loadAgreements"
          >
            Recargar Convenios
          </button>
        </div>
      </div>

      <div class="tecnm-card-body">
        <div class="tecnm-table-responsive">
          <table class="tecnm-table tecnm-table-striped">
            <thead>
              <tr>
                <th>ID ARCHIVO</th>
                <th>EMPRESA</th>
                <th>STATUS</th>
                <th>PIT</th>
                <th>CIA</th>
                <th>TIPO 1</th>
                <th>SECTOR</th>
                <th>GIRO</th>
                <th>TAMAÑO</th>
                <th>TIPO 2</th>
                <th class="tecnm-th-actions">Acciones</th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="isAgreementsLoading">
                <td colspan="11" class="tecnm-table-empty">
                  Cargando convenios institucionales...
                </td>
              </tr>
              <tr v-else-if="agreements.length === 0">
                <td colspan="11" class="tecnm-table-empty">
                  <span v-if="agreementSearchTerm">No se encontraron convenios con "{{ agreementSearchTerm }}".</span>
                  <span v-else>No hay convenios registrados con los filtros seleccionados. Puede asignar convenios desde la pestaña Directorio de Empresas.</span>
                </td>
              </tr>
              <tr
                v-for="a in agreements"
                v-else
                :key="a.id"
              >
                <td><strong>{{ a.archiveId || '—' }}</strong></td>
                <td>
                  <div><strong>{{ a.companyName }}</strong></div>
                  <small v-if="a.companyLegalName && a.companyLegalName !== a.companyName" class="tecnm-text-muted">
                    {{ a.companyLegalName }}
                  </small>
                  <small v-if="a.companyRfc" class="tecnm-text-muted" style="display: block;">
                    RFC: {{ a.companyRfc }}
                  </small>
                </td>
                <td>
                  <span class="tecnm-badge" :class="getAgreementBadgeClass(a.status)">
                    {{ a.status }}
                  </span>
                </td>
                <td>{{ a.pitCode || '—' }}</td>
                <td>{{ a.ciaType || '—' }}</td>
                <td>{{ a.agreementScope || '—' }}</td>
                <td>{{ a.sector || '—' }}</td>
                <td>{{ a.businessLine || '—' }}</td>
                <td>{{ a.companySize || '—' }}</td>
                <td>{{ a.geographicScope || '—' }}</td>
                <td>
                  <button
                    type="button"
                    class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                    @click="openEditAgreementModal(a)"
                  >
                    Editar Convenio
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Paginación de Convenios -->
        <div style="margin-top: 1rem;">
          <TecnmPagination
            v-if="agreementTotalCount > 0"
            :current-page="agreementPageNumber"
            :page-size="agreementPageSize"
            :total-count="agreementTotalCount"
            :total-pages="agreementTotalPages"
            @update:current-page="agreementPageNumber = $event"
            @page-change="handleAgreementPageChange"
          />
        </div>
      </div>
    </div>

    <!-- ============================================================= -->
    <!-- MODAL REGISTRAR / EDITAR EMPRESA                              -->
    <!-- ============================================================= -->
    <div
      v-if="isModalOpen"
      id="companyModal"
      class="modal-backdrop active"
      role="dialog"
      aria-modal="true"
      @click.self="isModalOpen = false"
    >
      <div class="modal-card" style="max-width: 680px;">
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

          <!-- Bloque Identificación de Empresa -->
          <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem;">
            <div class="tecnm-form-group">
              <label for="companyNameInput" class="tecnm-label">Nombre de la Empresa *</label>
              <input
                id="companyNameInput"
                v-model="form.name"
                type="text"
                class="tecnm-form-control"
                placeholder="Ej. Tecnología e Innovación"
                maxlength="200"
                :disabled="isSubmitting"
                required
              />
            </div>

            <div class="tecnm-form-group">
              <label for="companyTradeNameInput" class="tecnm-label">Nombre Comercial / Siglas</label>
              <input
                id="companyTradeNameInput"
                v-model="form.tradeName"
                type="text"
                class="tecnm-form-control"
                placeholder="Ej. TECNOINNOVA o TINO"
                maxlength="150"
                :disabled="isSubmitting"
              />
            </div>
          </div>

          <div style="display: grid; grid-template-columns: 2fr 1fr; gap: 0.75rem;">
            <div class="tecnm-form-group">
              <label for="companyLegalNameInput" class="tecnm-label">Razón Social</label>
              <input
                id="companyLegalNameInput"
                v-model="form.legalName"
                type="text"
                class="tecnm-form-control"
                placeholder="Ej. Tecnología e Innovación de México S.A. de C.V."
                maxlength="250"
                :disabled="isSubmitting"
              />
            </div>

            <div class="tecnm-form-group">
              <label for="companyRfcInput" class="tecnm-label">RFC (Opcional)</label>
              <input
                id="companyRfcInput"
                v-model="form.rfc"
                type="text"
                class="tecnm-form-control"
                placeholder="Ej. TINO900101ABC"
                maxlength="13"
                style="text-transform: uppercase;"
                :disabled="isSubmitting"
              />
            </div>
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

          <!-- Bloque Dirección Dividida -->
          <div style="margin-top: 0.5rem; margin-bottom: 0.75rem; border-top: 1px solid var(--tecnm-border-color, #e2e8f0); padding-top: 0.75rem;">
            <strong style="display: block; margin-bottom: 0.5rem; color: var(--tecnm-text-primary, #0f172a); font-size: 0.9rem;">
              Dirección de la Empresa (Desglosada)
            </strong>

            <div style="display: grid; grid-template-columns: 2fr 1fr; gap: 0.75rem;">
              <div class="tecnm-form-group">
                <label for="companyStreetInput" class="tecnm-label">Calle</label>
                <input
                  id="companyStreetInput"
                  v-model="form.street"
                  type="text"
                  class="tecnm-form-control"
                  placeholder="Ej. Av. Tecnológico"
                  maxlength="150"
                  :disabled="isSubmitting"
                />
              </div>
              <div class="tecnm-form-group">
                <label for="companyNumberInput" class="tecnm-label">Número (Ext / Int)</label>
                <input
                  id="companyNumberInput"
                  v-model="form.number"
                  type="text"
                  class="tecnm-form-control"
                  placeholder="Ej. 123 Int. 4"
                  maxlength="50"
                  :disabled="isSubmitting"
                />
              </div>
            </div>

            <div style="display: grid; grid-template-columns: 2fr 1fr; gap: 0.75rem;">
              <div class="tecnm-form-group">
                <label for="companyColoniaInput" class="tecnm-label">Colonia</label>
                <input
                  id="companyColoniaInput"
                  v-model="form.colonia"
                  type="text"
                  class="tecnm-form-control"
                  placeholder="Ej. Fracc. Los Laureles"
                  maxlength="150"
                  :disabled="isSubmitting"
                />
              </div>
              <div class="tecnm-form-group">
                <label for="companyPostalCodeInput" class="tecnm-label">Código Postal</label>
                <input
                  id="companyPostalCodeInput"
                  v-model="form.postalCode"
                  type="text"
                  class="tecnm-form-control"
                  placeholder="Ej. 25700"
                  maxlength="10"
                  :disabled="isSubmitting"
                />
              </div>
            </div>

            <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem;">
              <div class="tecnm-form-group">
                <label for="companyCityInput" class="tecnm-label">Ciudad</label>
                <input
                  id="companyCityInput"
                  v-model="form.city"
                  type="text"
                  class="tecnm-form-control"
                  placeholder="Ej. Monclova"
                  maxlength="100"
                  :disabled="isSubmitting"
                />
              </div>
              <div class="tecnm-form-group">
                <label for="companyStateInput" class="tecnm-label">Estado</label>
                <input
                  id="companyStateInput"
                  v-model="form.state"
                  type="text"
                  class="tecnm-form-control"
                  placeholder="Ej. Coahuila"
                  maxlength="100"
                  :disabled="isSubmitting"
                />
              </div>
            </div>
          </div>

          <!-- Bloque Contacto Principal -->
          <div style="margin-top: 0.5rem; border-top: 1px solid var(--tecnm-border-color, #e2e8f0); padding-top: 0.75rem;">
            <strong style="display: block; margin-bottom: 0.5rem; color: var(--tecnm-text-primary, #0f172a); font-size: 0.9rem;">
              Contacto Principal
            </strong>

            <div class="tecnm-form-group">
              <label for="companyContactNameInput" class="tecnm-label">Nombre del Contacto *</label>
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

            <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem;">
              <div class="tecnm-form-group">
                <label for="companyContactEmailInput" class="tecnm-label">Correo Electrónico *</label>
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
            </div>
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

    <!-- ============================================================= -->
    <!-- MODAL EDITAR / ASIGNAR CONVENIO (VINCULACIÓN)                -->
    <!-- ============================================================= -->
    <div
      v-if="isAgreementModalOpen"
      id="agreementModal"
      class="modal-backdrop active"
      role="dialog"
      aria-modal="true"
      @click.self="isAgreementModalOpen = false"
    >
      <div class="modal-card" style="max-width: 720px;">
        <div class="tecnm-modal-header">
          <h3 class="tecnm-modal-title">
            Ficha de Convenio Institucional
          </h3>
          <button
            type="button"
            class="tecnm-modal-close"
            aria-label="Cerrar"
            @click="isAgreementModalOpen = false"
          >
            &times;
          </button>
        </div>

        <form @submit.prevent="handleAgreementSubmit">
          <div
            v-if="agreementFormError"
            class="tecnm-alert tecnm-alert-danger"
            style="margin-bottom: 1rem;"
            role="alert"
          >
            <span>{{ agreementFormError }}</span>
          </div>

          <div
            v-if="selectedAgreementCompany"
            class="tecnm-alert tecnm-alert-info"
            style="margin-bottom: 1rem;"
          >
            <strong>Empresa:</strong> {{ selectedAgreementCompany.name }}
            <span v-if="selectedAgreementCompany.rfc">({{ selectedAgreementCompany.rfc }})</span>
          </div>

          <!-- Campos de la Ficha de Convenios -->
          <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem;">
            <div class="tecnm-form-group">
              <label for="agreementArchiveIdInput" class="tecnm-label">ID ARCHIVO</label>
              <input
                id="agreementArchiveIdInput"
                v-model="agreementForm.archiveId"
                type="text"
                class="tecnm-form-control"
                placeholder="Ej. CV-2025-01 / Carpeta 4"
                maxlength="50"
                :disabled="isAgreementSubmitting"
              />
            </div>

            <div class="tecnm-form-group">
              <label for="agreementStatusSelect" class="tecnm-label">STATUS *</label>
              <select
                id="agreementStatusSelect"
                v-model="agreementForm.status"
                class="tecnm-form-control"
                :disabled="isAgreementSubmitting"
                required
              >
                <option value="1 VIGENTE">1 VIGENTE</option>
                <option value="2 VENCIDO">2 VENCIDO</option>
                <option value="3 STAND BY">3 STAND BY</option>
                <option value="4 RENOVAR">4 RENOVAR</option>
              </select>
            </div>
          </div>

          <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem;">
            <div class="tecnm-form-group">
              <label for="agreementPitCodeSelect" class="tecnm-label">PIT 2025-2026</label>
              <input
                id="agreementPitCodeSelect"
                v-model="agreementForm.pitCode"
                type="text"
                class="tecnm-form-control"
                placeholder="Ej. 5.1.2, 5.1.3, 5.1.4"
                maxlength="20"
                :disabled="isAgreementSubmitting"
              />
            </div>

            <div class="tecnm-form-group">
              <label for="agreementCiaTypeSelect" class="tecnm-label">CIA</label>
              <select
                id="agreementCiaTypeSelect"
                v-model="agreementForm.ciaType"
                class="tecnm-form-control"
                :disabled="isAgreementSubmitting"
              >
                <option value="USO COMPARTIDO">USO COMPARTIDO</option>
                <option value="DE VINCULACION ENTRE INSTITUTOS">DE VINCULACION ENTRE INSTITUTOS</option>
                <option value="CON IES">CON IES</option>
              </select>
            </div>
          </div>

          <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem;">
            <div class="tecnm-form-group">
              <label for="agreementScopeSelect" class="tecnm-label">TIPO 1 (Alcance)</label>
              <select
                id="agreementScopeSelect"
                v-model="agreementForm.agreementScope"
                class="tecnm-form-control"
                :disabled="isAgreementSubmitting"
              >
                <option value="GENERAL">GENERAL</option>
                <option value="ESPECÍFICOS">ESPECÍFICOS</option>
              </select>
            </div>

            <div class="tecnm-form-group">
              <label for="agreementSectorSelect" class="tecnm-label">SECTOR</label>
              <select
                id="agreementSectorSelect"
                v-model="agreementForm.sector"
                class="tecnm-form-control"
                :disabled="isAgreementSubmitting"
              >
                <option value="PÚBLICO">PÚBLICO</option>
                <option value="SOCIAL">SOCIAL</option>
                <option value="PRIVADO">PRIVADO</option>
                <option value="EDUCATIVO">EDUCATIVO</option>
              </select>
            </div>
          </div>

          <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem;">
            <div class="tecnm-form-group">
              <label for="agreementBusinessLineInput" class="tecnm-label">GIRO</label>
              <input
                id="agreementBusinessLineInput"
                v-model="agreementForm.businessLine"
                type="text"
                class="tecnm-form-control"
                placeholder="Ej. Automotriz, Metalmecánico, Software..."
                maxlength="150"
                :disabled="isAgreementSubmitting"
              />
            </div>

            <div class="tecnm-form-group">
              <label for="agreementCompanySizeSelect" class="tecnm-label">TAMAÑO</label>
              <select
                id="agreementCompanySizeSelect"
                v-model="agreementForm.companySize"
                class="tecnm-form-control"
                :disabled="isAgreementSubmitting"
              >
                <option value="MICROEMPRESA">MICROEMPRESA</option>
                <option value="PEQUEÑAS EMPRESAS">PEQUEÑAS EMPRESAS</option>
                <option value="MEDIANAS EMPRESAS">MEDIANAS EMPRESAS</option>
                <option value="GRANDES EMPRESAS">GRANDES EMPRESAS</option>
              </select>
            </div>
          </div>

          <div class="tecnm-form-group">
            <label for="agreementGeographicScopeSelect" class="tecnm-label">TIPO 2 (Cobertura Geográfica)</label>
            <select
              id="agreementGeographicScopeSelect"
              v-model="agreementForm.geographicScope"
              class="tecnm-form-control"
              :disabled="isAgreementSubmitting"
            >
              <option value="NACIONAL">NACIONAL</option>
              <option value="INTERNACIONAL">INTERNACIONAL</option>
            </select>
          </div>

          <div class="tecnm-form-group">
            <label for="agreementNotesInput" class="tecnm-label">Observaciones / Notas Adicionales</label>
            <textarea
              id="agreementNotesInput"
              v-model="agreementForm.notes"
              class="tecnm-form-control"
              rows="3"
              placeholder="Detalles sobre vigencia, cláusulas o seguimiento..."
              :disabled="isAgreementSubmitting"
            ></textarea>
          </div>

          <div class="tecnm-modal-footer">
            <button
              type="button"
              class="tecnm-btn tecnm-btn-secondary"
              :disabled="isAgreementSubmitting"
              @click="isAgreementModalOpen = false"
            >
              Cancelar
            </button>
            <button
              type="submit"
              class="tecnm-btn tecnm-btn-primary"
              :disabled="isAgreementSubmitting"
            >
              <span v-if="!isAgreementSubmitting">Guardar Convenio</span>
              <span v-else class="login-spinner"></span>
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- ============================================================= -->
    <!-- MODAL IMPORTAR EXCEL                                          -->
    <!-- ============================================================= -->
    <div
      v-if="isImportModalOpen"
      id="importCompanyModal"
      class="modal-backdrop active"
      role="dialog"
      aria-modal="true"
      @click.self="isImportModalOpen = false"
    >
      <div class="modal-card">
        <div class="tecnm-modal-header">
          <h3 class="tecnm-modal-title">Importar Empresas desde Archivo Excel</h3>
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
          <div
            v-if="importError"
            class="tecnm-alert tecnm-alert-danger"
            style="margin-bottom: 1rem;"
            role="alert"
          >
            <span>{{ importError }}</span>
          </div>

          <div
            v-if="importResult"
            class="tecnm-alert tecnm-alert-success"
            style="margin-bottom: 1rem;"
          >
            <p><strong>Resultado del Procesamiento:</strong></p>
            <ul style="margin: 0.5rem 0 0 1.25rem; font-size: 0.875rem;">
              <li>Filas leídas: {{ importResult.totalRows }}</li>
              <li>Empresas procesadas: {{ importResult.successCount }}</li>
              <li>Omitidas (sin cambios): {{ importResult.skippedCount }}</li>
            </ul>
          </div>

          <p class="tecnm-text-muted" style="margin-bottom: 1rem; font-size: 0.9rem;">
            Descargue la plantilla oficial con columnas desglosadas (Calle, Número, Colonia, etc.). Si sube empresas ya existentes, se completarán únicamente los campos vacíos sin sobreescribir datos existentes. Si alguna fila tiene campos requeridos vacíos, se detendrá el proceso para evitar datos incompletos.
          </p>

          <div style="margin-bottom: 1.25rem;">
            <button
              type="button"
              class="tecnm-btn tecnm-btn-secondary"
              @click="handleDownloadCompanyTemplate"
            >
              Descargar Plantilla (.xlsx)
            </button>
          </div>

          <div class="tecnm-form-group">
            <label for="companyExcelFileInput" class="tecnm-label">Archivo Excel (.xlsx, .xls) *</label>
            <input
              id="companyExcelFileInput"
              type="file"
              class="tecnm-form-control"
              accept=".xlsx,.xls"
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
              <span v-if="!isImporting">Subir y Procesar</span>
              <span v-else class="login-spinner"></span>
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

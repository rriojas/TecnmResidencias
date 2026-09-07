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

const tableColspan = computed(() => {
  let count = 7
  if (canViewAgreements.value) count++
  if (!authStore.isStudent) count++
  return count
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
    loadCompanyOptions()
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
      legalName: c.legalName || '',
      tradeName: c.tradeName || '',
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
    formError.value = 'Ingrese el nombre de la empresa.'
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
    legalName: form.value.legalName.trim() || undefined,
    tradeName: form.value.tradeName.trim() || undefined,
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
    loadCompanyOptions()
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
    loadCompanyOptions()
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al desactivar empresa.', 'danger')
  }
}

async function handleReactivate(company) {
  try {
    await apiClient.patch(`/v1/companies/${company.id}/activate`)
    showAlert('Empresa reactivada correctamente.', 'success')
    loadCompanies()
    loadCompanyOptions()
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
// ESTADO: CONVENIOS INSTITUCIONALES (N:M)
// ==========================================
const agreements = ref([])
const isAgreementsLoading = ref(false)
const agreementPageNumber = ref(1)
const agreementPageSize = ref(10)
const agreementTotalCount = ref(0)
const agreementTotalPages = ref(0)
const agreementSearchTerm = ref('')
const agreementStatusFilter = ref('all')

// Opciones de empresas para selector múltiple
const allCompanyOptions = ref([])
const companyPickerSearch = ref('')

async function loadCompanyOptions() {
  try {
    const res = await apiClient.get('/v1/companies/options')
    allCompanyOptions.value = res.data || []
  } catch {
    allCompanyOptions.value = []
  }
}

const filteredCompanyOptions = computed(() => {
  const term = companyPickerSearch.value.trim().toLowerCase()
  if (!term) return allCompanyOptions.value
  return allCompanyOptions.value.filter((c) =>
    (c.name || '').toLowerCase().includes(term) || (c.rfc || '').toLowerCase().includes(term)
  )
})

// Modal Convenio (N:M)
const isAgreementModalOpen = ref(false)
const isAgreementEditMode = ref(false)
const editingAgreementId = ref(null)
const isAgreementSubmitting = ref(false)
const agreementFormError = ref('')

const agreementForm = ref({
  archiveId: '',
  hasExpirationDate: false,
  expirationDate: '', // Formato DD/MM/YYYY
  processStatus: '', // '' | 'EN_RENOVACION' | 'CANCELADO'
  pitCode: '5.1.2',
  ciaType: 'USO COMPARTIDO',
  companies: [],
})

function formatDateDDMMYYYY(dateVal) {
  if (!dateVal) return '—'
  const s = String(dateVal).trim()
  if (/^\d{1,2}\/\d{1,2}\/\d{4}$/.test(s)) return s
  try {
    const d = new Date(s)
    if (isNaN(d.getTime())) return s
    const day = String(d.getUTCDate()).padStart(2, '0')
    const month = String(d.getUTCMonth() + 1).padStart(2, '0')
    const year = d.getUTCFullYear()
    return `${day}/${month}/${year}`
  } catch {
    return s
  }
}

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

function openCreateAgreementModal() {
  isAgreementEditMode.value = false
  editingAgreementId.value = null
  companyPickerSearch.value = ''
  agreementFormError.value = ''
  agreementForm.value = {
    archiveId: '',
    hasExpirationDate: false,
    expirationDate: '',
    processStatus: '',
    pitCode: '5.1.2',
    ciaType: 'USO COMPARTIDO',
    companies: [],
  }
  loadCompanyOptions()
  isAgreementModalOpen.value = true
}

function openEditAgreementModal(agreement) {
  isAgreementEditMode.value = true
  editingAgreementId.value = agreement.id
  companyPickerSearch.value = ''
  agreementFormError.value = ''
  const hasExp = Boolean(agreement.expirationDate)
  agreementForm.value = {
    archiveId: agreement.archiveId || '',
    hasExpirationDate: hasExp,
    expirationDate: agreement.expirationDateFormatted || (hasExp ? formatDateDDMMYYYY(agreement.expirationDate) : ''),
    processStatus: agreement.processStatus || '',
    pitCode: agreement.pitCode || '5.1.2',
    ciaType: agreement.ciaType || 'USO COMPARTIDO',
    companies: (agreement.companies || []).map((c) => ({
      companyId: c.companyId || c.id,
      agreementScope: c.agreementScope || 'GENERAL',
    })),
  }
  loadCompanyOptions()
  isAgreementModalOpen.value = true
}

function openAgreementModalForCompany(company) {
  isAgreementEditMode.value = false
  editingAgreementId.value = null
  companyPickerSearch.value = ''
  agreementFormError.value = ''
  agreementForm.value = {
    archiveId: '',
    hasExpirationDate: false,
    expirationDate: '',
    processStatus: '',
    pitCode: '5.1.2',
    ciaType: 'USO COMPARTIDO',
    companies: [
      {
        companyId: company.id,
        agreementScope: 'GENERAL',
      },
    ],
  }
  loadCompanyOptions()
  isAgreementModalOpen.value = true
}

function toggleCompanyInAgreement(companyId) {
  const index = agreementForm.value.companies.findIndex((c) => c.companyId === companyId)
  if (index >= 0) {
    agreementForm.value.companies.splice(index, 1)
  } else {
    agreementForm.value.companies.push({
      companyId,
      agreementScope: 'GENERAL',
    })
  }
}

function removeCompanyFromAgreement(companyId) {
  agreementForm.value.companies = agreementForm.value.companies.filter((c) => c.companyId !== companyId)
}

function getCompanyDetailsById(id) {
  const found = allCompanyOptions.value.find((c) => c.id === id)
  return found || { id, name: `Empresa #${id}`, sector: '—', tradeName: null, rfc: '' }
}

function isCompanySelectedInAgreement(companyId) {
  return agreementForm.value.companies.some((c) => c.companyId === companyId)
}

function getCompanyScopeInAgreement(companyId) {
  const item = agreementForm.value.companies.find((c) => c.companyId === companyId)
  return item ? item.agreementScope : 'GENERAL'
}

function setCompanyScopeInAgreement(companyId, scope) {
  const item = agreementForm.value.companies.find((c) => c.companyId === companyId)
  if (item) {
    item.agreementScope = scope
  }
}

function filterAgreementsByCompany(company) {
  activeTab.value = 'agreements'
  agreementSearchTerm.value = company.name || company.rfc || ''
  agreementPageNumber.value = 1
  loadAgreements()
}

function viewCompanyAgreements(company) {
  filterAgreementsByCompany(company)
}

async function handleAgreementSubmit() {
  agreementFormError.value = ''
  if (!agreementForm.value.companies || agreementForm.value.companies.length === 0) {
    agreementFormError.value = 'Debe seleccionar al menos una empresa para vincular al convenio.'
    return
  }

  let expirationDatePayload = null
  if (agreementForm.value.hasExpirationDate) {
    const raw = (agreementForm.value.expirationDate || '').trim()
    if (!raw) {
      agreementFormError.value = 'Debe ingresar la fecha de caducidad en formato día/mes/año (ej. 25/12/2026) o desactivar la fecha de caducidad.'
      return
    }
    const match = raw.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})$/)
    if (!match) {
      agreementFormError.value = 'El formato de fecha de caducidad debe ser día/mes/año (ej. 25/12/2026).'
      return
    }
    const day = parseInt(match[1], 10)
    const month = parseInt(match[2], 10)
    const year = parseInt(match[3], 10)
    if (month < 1 || month > 12 || day < 1 || day > 31 || year < 1900 || year > 2100) {
      agreementFormError.value = 'La fecha de caducidad ingresada no es válida.'
      return
    }
    expirationDatePayload = `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`
  }

  isAgreementSubmitting.value = true
  const payload = {
    archiveId: agreementForm.value.archiveId.trim() || undefined,
    expirationDate: expirationDatePayload,
    processStatus: isAgreementEditMode.value ? (agreementForm.value.processStatus || null) : null,
    pitCode: agreementForm.value.pitCode.trim() || undefined,
    ciaType: agreementForm.value.ciaType.trim() || undefined,
    companies: agreementForm.value.companies.map((c) => ({
      companyId: c.companyId,
      agreementScope: c.agreementScope || 'GENERAL',
    })),
  }

  try {
    if (isAgreementEditMode.value) {
      await apiClient.put(`/v1/companies/agreements/${editingAgreementId.value}`, payload)
      showAlert('Convenio institucional actualizado exitosamente.', 'success')
    } else {
      await apiClient.post('/v1/companies/agreements', payload)
      showAlert('Convenio institucional registrado exitosamente.', 'success')
    }
    isAgreementModalOpen.value = false
    loadAgreements()
    loadCompanies()
  } catch (err) {
    agreementFormError.value =
      err.response?.data?.message || 'Error al guardar los datos del convenio.'
  } finally {
    isAgreementSubmitting.value = false
  }
}

async function handleDeleteAgreement(agreement) {
  const confirmed = await confirm({
    title: 'Desactivar Convenio',
    message: `¿Está seguro de desactivar el convenio "${agreement.archiveId || 'Convenio #' + agreement.id}"?`,
    okText: 'Desactivar',
    cancelText: 'Cancelar',
  })
  if (!confirmed) return

  try {
    await apiClient.delete(`/v1/companies/agreements/${agreement.id}`)
    showAlert('Convenio desactivado correctamente.', 'success')
    loadAgreements()
    loadCompanies()
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al desactivar convenio.', 'danger')
  }
}

function getAgreementBadgeClass(status) {
  const s = String(status || '').toUpperCase()
  if (s.includes('VIGENTE')) return 'tecnm-badge-success'
  if (s.includes('VENCIDO')) return 'tecnm-badge-danger'
  if (s.includes('RENOV') || s.includes('STAND')) return 'tecnm-badge-warning'
  if (s.includes('CANCEL')) return 'tecnm-badge-secondary'
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
  loadCompanyOptions()
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

        <template v-if="activeTab === 'companies'">
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
        </template>

        <template v-else-if="activeTab === 'agreements' && canViewAgreements">
          <button
            type="button"
            class="tecnm-btn tecnm-btn-primary"
            @click="openCreateAgreementModal"
          >
            + Registrar Nuevo Convenio
          </button>
        </template>
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
                <td :colspan="tableColspan" class="tecnm-table-empty">
                  Cargando catálogo de empresas...
                </td>
              </tr>
              <tr v-else-if="companies.length === 0">
                <td :colspan="tableColspan" class="tecnm-table-empty">
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
                  <span
                    v-if="c.hasAgreement"
                    class="tecnm-badge tecnm-badge-success"
                    style="cursor: pointer;"
                    title="Ver convenios asociados a esta empresa"
                    @click="filterAgreementsByCompany(c)"
                  >
                    Con Convenio
                  </span>
                  <button
                    v-else
                    type="button"
                    class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                    style="font-size: 0.75rem; padding: 0.2rem 0.5rem;"
                    @click="openAgreementModalForCompany(c)"
                  >
                    + Asignar a Convenio
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
    <!-- PESTAÑA 2: CONVENIOS INSTITUCIONALES (N:M)                    -->
    <!-- ============================================================= -->
    <div v-if="canViewAgreements" v-show="activeTab === 'agreements'" class="tecnm-card">
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
              <option value="all">Todos los Convenios</option>
              <option value="VIGENTE">Vigentes (Activos / Indefinidos)</option>
              <option value="EN_RENOVACION">En Proceso de Renovación</option>
              <option value="VENCIDO">Vencidos (Caducados)</option>
              <option value="CANCELADO">Cancelados</option>
            </select>
          </div>
          <button
            type="button"
            class="tecnm-btn tecnm-btn-primary tecnm-btn-sm"
            @click="openCreateAgreementModal"
          >
            + Registrar Convenio
          </button>
          <button
            type="button"
            class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
            @click="loadAgreements"
          >
            Recargar
          </button>
        </div>
      </div>

      <div class="tecnm-card-body">
        <div
          v-if="agreementSearchTerm"
          class="tecnm-alert tecnm-alert-info"
          style="margin-bottom: 1rem; display: flex; align-items: center; justify-content: space-between;"
        >
          <span>Filtro de convenios aplicado: <strong>{{ agreementSearchTerm }}</strong></span>
          <button
            type="button"
            class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
            @click="clearAgreementSearch"
          >
            Mostrar todos
          </button>
        </div>

        <div class="tecnm-table-responsive">
          <table class="tecnm-table tecnm-table-striped">
            <thead>
              <tr>
                <th>ID ARCHIVO</th>
                <th>ESTATUS</th>
                <th>VIGENCIA</th>
                <th>PROCESO</th>
                <th>PIT</th>
                <th>CIA</th>
                <th>EMPRESAS VINCULADAS (SECTOR / ALCANCE TIPO 1)</th>
                <th class="tecnm-th-actions">Acciones</th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="isAgreementsLoading">
                <td colspan="8" class="tecnm-table-empty">
                  Cargando convenios institucionales...
                </td>
              </tr>
              <tr v-else-if="agreements.length === 0">
                <td colspan="8" class="tecnm-table-empty">
                  <span v-if="agreementSearchTerm">No se encontraron convenios con "{{ agreementSearchTerm }}".</span>
                  <span v-else>No hay convenios registrados. Puede hacer clic en "+ Registrar Nuevo Convenio" para crear uno y vincular múltiples empresas.</span>
                </td>
              </tr>
              <tr
                v-for="a in agreements"
                v-else
                :key="a.id"
              >
                <td><strong>{{ a.archiveId || '—' }}</strong></td>
                <td>
                  <span class="tecnm-badge" :class="getAgreementBadgeClass(a.status)">
                    {{ a.status }}
                  </span>
                </td>
                <td>
                  <span v-if="!a.expirationDate" class="tecnm-badge" style="font-size: 0.75rem; background: #e0f2fe; color: #0369a1; border: 1px solid #bae6fd;">
                    Vigencia Indefinida
                  </span>
                  <span v-else style="font-weight: 600; font-size: 0.85rem; color: #1e293b;">
                    {{ a.expirationDateFormatted || formatDateDDMMYYYY(a.expirationDate) }}
                  </span>
                </td>
                <td>
                  <span v-if="a.processStatus === 'EN_RENOVACION'" class="tecnm-badge tecnm-badge-warning" style="font-size: 0.75rem;">
                    En Renovación
                  </span>
                  <span v-else-if="a.processStatus === 'CANCELADO'" class="tecnm-badge tecnm-badge-danger" style="font-size: 0.75rem;">
                    Cancelado
                  </span>
                  <span v-else class="tecnm-text-muted" style="font-size: 0.8rem;">—</span>
                </td>
                <td>{{ a.pitCode || '—' }}</td>
                <td>{{ a.ciaType || '—' }}</td>
                <td>
                  <div v-if="a.companies && a.companies.length > 0" style="display: flex; flex-direction: column; gap: 0.35rem;">
                    <div
                      v-for="comp in a.companies"
                      :key="comp.companyId"
                      style="display: flex; align-items: center; gap: 0.4rem; flex-wrap: wrap; background: #f8fafc; padding: 0.25rem 0.5rem; border-radius: 4px; border: 1px solid #e2e8f0;"
                    >
                      <strong style="color: #0f172a; font-size: 0.85rem;">{{ comp.tradeName || comp.companyName }}</strong>
                      <span v-if="comp.sector" class="tecnm-badge tecnm-badge-secondary" style="font-size: 0.7rem;">
                        {{ comp.sector }}
                      </span>
                      <span class="tecnm-badge" :class="comp.agreementScope === 'ESPECÍFICOS' ? 'tecnm-badge-warning' : 'tecnm-badge-info'" style="font-size: 0.7rem;">
                        {{ comp.agreementScope || 'GENERAL' }}
                      </span>
                    </div>
                  </div>
                  <span v-else class="tecnm-text-muted">Sin empresas vinculadas</span>
                </td>
                <td>
                  <div class="tecnm-row-actions">
                    <button
                      type="button"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                      @click="openEditAgreementModal(a)"
                    >
                      Editar
                    </button>
                    <button
                      type="button"
                      class="tecnm-btn tecnm-btn-danger tecnm-btn-sm"
                      @click="handleDeleteAgreement(a)"
                    >
                      Desactivar
                    </button>
                  </div>
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
    <!-- MODAL REGISTRAR / EDITAR CONVENIO N:M (VINCULACIÓN)          -->
    <!-- ============================================================= -->
    <div
      v-if="canViewAgreements && isAgreementModalOpen"
      id="agreementModal"
      class="modal-backdrop active"
      role="dialog"
      aria-modal="true"
      @click.self="isAgreementModalOpen = false"
    >
      <div class="modal-card" style="max-width: 760px;">
        <div class="tecnm-modal-header">
          <h3 class="tecnm-modal-title">
            {{ isAgreementEditMode ? 'Editar Convenio Institucional' : 'Registrar Nuevo Convenio Institucional' }}
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

          <!-- Datos Principales del Convenio -->
          <div :style="isAgreementEditMode ? 'display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem;' : 'display: block; margin-bottom: 1rem;'">
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

            <div v-if="isAgreementEditMode" class="tecnm-form-group">
              <label for="agreementProcessStatusSelect" class="tecnm-label">PROCESO DEL CONVENIO</label>
              <select
                id="agreementProcessStatusSelect"
                v-model="agreementForm.processStatus"
                class="tecnm-form-control"
                :disabled="isAgreementSubmitting"
              >
                <option value="">Ninguno (Vigencia Normal)</option>
                <option value="EN_RENOVACION">En Proceso de Renovación</option>
                <option value="CANCELADO">Cancelado / Revocado</option>
              </select>
            </div>
          </div>

          <!-- Vigencia y Fecha de Caducidad (Día/Mes/Año) -->
          <div class="tecnm-card" style="margin-bottom: 1rem; border: 1px solid var(--tecnm-border-color, #cbd5e1); padding: 0.85rem; border-radius: 8px; background: #f8fafc;">
            <div style="display: flex; align-items: center; justify-content: space-between; gap: 1rem; flex-wrap: wrap;">
              <div>
                <label class="tecnm-switch-label" style="font-weight: 600; cursor: pointer; display: flex; align-items: center; gap: 0.5rem; margin: 0;">
                  <span class="tecnm-switch">
                    <input
                      id="agreementHasExpirationToggle"
                      v-model="agreementForm.hasExpirationDate"
                      type="checkbox"
                      :disabled="isAgreementSubmitting"
                    />
                    <span class="tecnm-switch-slider"></span>
                  </span>
                  <span>Activar fecha de caducidad / vencimiento</span>
                </label>
                <small class="tecnm-text-muted" style="display: block; margin-top: 0.35rem;">
                  {{ agreementForm.hasExpirationDate ? 'El convenio vencerá automáticamente en la fecha indicada.' : 'Convenio de vigencia indefinida (nunca caduca).' }}
                </small>
              </div>

              <div v-if="agreementForm.hasExpirationDate" style="min-width: 220px;">
                <label for="agreementExpirationDateInput" class="tecnm-label" style="margin-bottom: 0.25rem; font-size: 0.85rem; font-weight: 600;">
                  Fecha de Caducidad (Día/Mes/Año) *
                </label>
                <input
                  id="agreementExpirationDateInput"
                  v-model="agreementForm.expirationDate"
                  type="text"
                  class="tecnm-form-control"
                  placeholder="DD/MM/AAAA (ej. 25/12/2026)"
                  maxlength="10"
                  required
                  :disabled="isAgreementSubmitting"
                />
              </div>
            </div>
          </div>

          <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; margin-bottom: 1rem;">
            <div class="tecnm-form-group">
              <label for="agreementPitCodeSelect" class="tecnm-label">PIT</label>
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

          <!-- Empresas Vinculadas con Tipo 1 Individual -->
          <div class="tecnm-form-group" style="border: 1px solid var(--tecnm-border-color, #e2e8f0); border-radius: 8px; padding: 1rem; background: var(--tecnm-surface-neutral, #f8fafc);">
            <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.5rem;">
              <label class="tecnm-label" style="font-weight: 600; margin-bottom: 0;">
                Empresas Vinculadas al Convenio ({{ agreementForm.companies.length }}) *
              </label>
              <small class="tecnm-text-muted">Cada empresa mantiene su Sector y define su Tipo 1 (Alcance)</small>
            </div>

            <!-- Lista de empresas ya vinculadas -->
            <div v-if="agreementForm.companies.length > 0" style="display: flex; flex-direction: column; gap: 0.5rem; margin-bottom: 0.85rem;">
              <div
                v-for="item in agreementForm.companies"
                :key="item.companyId"
                style="display: flex; align-items: center; justify-content: space-between; gap: 0.75rem; background: #fff; padding: 0.5rem 0.75rem; border-radius: 6px; border: 1px solid #cbd5e1;"
              >
                <div style="flex: 1; min-width: 0;">
                  <div style="font-weight: 600; color: #0f172a; font-size: 0.9rem; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;">
                    {{ getCompanyDetailsById(item.companyId).tradeName || getCompanyDetailsById(item.companyId).name }}
                  </div>
                  <div style="display: flex; gap: 0.4rem; align-items: center; margin-top: 0.2rem;">
                    <span v-if="getCompanyDetailsById(item.companyId).sector" class="tecnm-badge tecnm-badge-secondary" style="font-size: 0.7rem;">
                      Sector: {{ getCompanyDetailsById(item.companyId).sector }}
                    </span>
                    <small v-if="getCompanyDetailsById(item.companyId).rfc" class="tecnm-text-muted" style="font-size: 0.75rem;">
                      RFC: {{ getCompanyDetailsById(item.companyId).rfc }}
                    </small>
                  </div>
                </div>

                <div style="display: flex; align-items: center; gap: 0.5rem;">
                  <div>
                    <label :for="`scope_${item.companyId}`" style="font-size: 0.75rem; font-weight: 500; display: block; margin-bottom: 0.15rem;">
                      Tipo 1:
                    </label>
                    <select
                      :id="`scope_${item.companyId}`"
                      v-model="item.agreementScope"
                      class="tecnm-form-control"
                      style="font-size: 0.8rem; padding: 0.25rem 0.5rem; height: auto;"
                      :disabled="isAgreementSubmitting"
                    >
                      <option value="GENERAL">GENERAL</option>
                      <option value="ESPECÍFICOS">ESPECÍFICOS</option>
                    </select>
                  </div>

                  <button
                    type="button"
                    class="tecnm-btn tecnm-btn-danger tecnm-btn-sm"
                    style="margin-top: 1rem; padding: 0.25rem 0.5rem;"
                    title="Quitar empresa de este convenio"
                    :disabled="isAgreementSubmitting"
                    @click="removeCompanyFromAgreement(item.companyId)"
                  >
                    &times;
                  </button>
                </div>
              </div>
            </div>
            <div v-else class="tecnm-alert tecnm-alert-warning" style="margin-bottom: 0.75rem; font-size: 0.85rem; padding: 0.5rem 0.75rem;">
              No hay empresas vinculadas. Agregue una o más empresas usando el buscador a continuación.
            </div>

            <!-- Buscador para agregar empresas -->
            <div>
              <input
                v-model="companyPickerSearch"
                type="text"
                class="tecnm-form-control"
                placeholder="Escriba el nombre o RFC para buscar empresas y vincularlas..."
                style="margin-bottom: 0.5rem; font-size: 0.85rem;"
              />

              <div style="max-height: 140px; overflow-y: auto; background: #fff; border: 1px solid var(--tecnm-border-color, #e2e8f0); border-radius: 4px; padding: 0.35rem;">
                <div
                  v-for="opt in filteredCompanyOptions"
                  :key="opt.id"
                  style="display: flex; align-items: center; justify-content: space-between; padding: 0.3rem 0.5rem; border-bottom: 1px solid #f1f5f9;"
                >
                  <div style="flex: 1; font-size: 0.85rem;">
                    <strong>{{ opt.name }}</strong>
                    <span v-if="opt.sector" style="color: #64748b; font-size: 0.75rem; margin-left: 0.35rem;">({{ opt.sector }})</span>
                    <small v-if="opt.rfc" class="tecnm-text-muted" style="margin-left: 0.35rem;">[{{ opt.rfc }}]</small>
                  </div>
                  <button
                    type="button"
                    class="tecnm-btn tecnm-btn-sm"
                    :class="isCompanySelectedInAgreement(opt.id) ? 'tecnm-btn-secondary' : 'tecnm-btn-primary'"
                    style="font-size: 0.75rem; padding: 0.2rem 0.5rem;"
                    :disabled="isAgreementSubmitting"
                    @click="toggleCompanyInAgreement(opt.id)"
                  >
                    {{ isCompanySelectedInAgreement(opt.id) ? '✓ Vinculada' : '+ Vincular' }}
                  </button>
                </div>
                <div v-if="filteredCompanyOptions.length === 0" class="tecnm-text-muted" style="font-size: 0.85rem; padding: 0.4rem; text-align: center;">
                  No se encontraron empresas con ese término.
                </div>
              </div>
            </div>
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
              <span v-if="!isAgreementSubmitting">{{ isAgreementEditMode ? 'Actualizar Convenio' : 'Guardar Convenio' }}</span>
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
      <div class="modal-card modal-card-wide" style="max-width: 880px;">
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

          <!-- Cabecera de Descarga -->
          <div style="display: flex; align-items: center; justify-content: space-between; gap: 1rem; flex-wrap: wrap; margin-bottom: 1rem; padding: 0.75rem 1rem; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px;">
            <div>
              <strong style="color: #0f172a; font-size: 0.95rem; display: block;">Plantilla Oficial de Importación (.xlsx)</strong>
              <span class="tecnm-text-muted" style="font-size: 0.82rem;">
                Descargue el formato oficial con los 20 encabezados exactos para empresas y convenios vinculados.
              </span>
            </div>
            <button
              type="button"
              class="tecnm-btn tecnm-btn-primary tecnm-btn-sm"
              @click="handleDownloadCompanyTemplate"
            >
              <svg xmlns="http://www.w3.org/2000/svg" width="15" height="15" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" style="margin-right: 0.35rem;">
                <path stroke-linecap="round" stroke-linejoin="round" d="M3 16.5v2.25A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75V16.5M7.5 12 12 16.5m0 0L16.5 12M12 16.5V3" />
              </svg>
              Descargar Plantilla Oficial
            </button>
          </div>

          <!-- Guía Interactiva de Columnas y Tipos de Datos Aceptados -->
          <div style="margin-bottom: 1.25rem; border: 1px solid #cbd5e1; border-radius: 8px; background: #ffffff; padding: 0.85rem;">
            <div style="display: flex; align-items: center; justify-content: space-between; margin-bottom: 0.5rem;">
              <strong style="font-size: 0.88rem; color: #1e293b; display: flex; align-items: center; gap: 0.35rem;">
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" style="color: #0284c7;">
                  <path stroke-linecap="round" stroke-linejoin="round" d="m11.25 11.25.041-.02a.75.75 0 0 1 1.063.852l-.708 2.836a.75.75 0 0 0 1.063.853l.041-.021M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Zm-9-3.75h.008v.008H12V8.25Z" />
                </svg>
                Guía de Datos Esperados por Columna
              </strong>
              <span class="tecnm-badge tecnm-badge-info" style="font-size: 0.7rem;">Reglas de Validación</span>
            </div>

            <div style="font-size: 0.8rem; color: #475569; margin-bottom: 0.65rem; line-height: 1.45;">
              • <strong>Empresa (14 columnas)</strong>: Todos los datos son obligatorios salvo <em>RazonSocial</em>. El RFC debe tener 12 o 13 caracteres alfanuméricos.<br />
              • <strong>Convenio (6 columnas)</strong>: Si escribe <em>NumeroConvenio</em>, son requeridos <em>AlcanceConvenio</em> (GENERAL o ESPECIFICOS), <em>ClavePIT</em> y <em>TipoCIA</em>.<br />
              • <strong>Vigencia</strong>: <em>FechaCaducidad</em> en <code>DD/MM/AAAA</code> (ej. 31/12/2026). Si se deja vacía = <strong>Vigencia Indefinida</strong>.<br />
              • <strong>Proceso</strong>: <em>EstadoProceso</em> solo acepta <code>EN_RENOVACION</code>, <code>CANCELADO</code>, o vacío (normal).
            </div>

            <div style="max-height: 220px; overflow-y: auto; border: 1px solid #e2e8f0; border-radius: 6px;">
              <table class="tecnm-table tecnm-table-striped" style="font-size: 0.78rem; margin: 0;">
                <thead>
                  <tr style="background: #0f172a; color: #ffffff;">
                    <th style="padding: 0.35rem 0.5rem; position: sticky; top: 0; z-index: 2;">Columna en Excel</th>
                    <th style="padding: 0.35rem 0.5rem; position: sticky; top: 0; z-index: 2;">Obligatorio</th>
                    <th style="padding: 0.35rem 0.5rem; position: sticky; top: 0; z-index: 2;">Tipo de Dato / Formato</th>
                    <th style="padding: 0.35rem 0.5rem; position: sticky; top: 0; z-index: 2;">Ejemplo Válido</th>
                  </tr>
                </thead>
                <tbody>
                  <tr>
                    <td><strong>Nombre</strong></td>
                    <td><span class="tecnm-badge tecnm-badge-danger" style="font-size: 0.7rem;">Sí</span></td>
                    <td>Texto (Nombre público u oficial)</td>
                    <td>Altos Hornos de México</td>
                  </tr>
                  <tr>
                    <td><strong>RazonSocial</strong></td>
                    <td><span class="tecnm-badge tecnm-badge-secondary" style="font-size: 0.7rem;">Opcional</span></td>
                    <td>Texto (Razón fiscal completa)</td>
                    <td>Altos Hornos de México S.A.B. de C.V.</td>
                  </tr>
                  <tr>
                    <td><strong>NombreComercial</strong></td>
                    <td><span class="tecnm-badge tecnm-badge-danger" style="font-size: 0.7rem;">Sí</span></td>
                    <td>Texto (Siglas o marca)</td>
                    <td>AHMSA</td>
                  </tr>
                  <tr>
                    <td><strong>RFC</strong></td>
                    <td><span class="tecnm-badge tecnm-badge-danger" style="font-size: 0.7rem;">Sí</span></td>
                    <td>12 chars (moral) o 13 chars (física)</td>
                    <td>AHM441231AB1</td>
                  </tr>
                  <tr>
                    <td><strong>Sector</strong></td>
                    <td><span class="tecnm-badge tecnm-badge-danger" style="font-size: 0.7rem;">Sí</span></td>
                    <td>Industrial, Servicios, Público, etc.</td>
                    <td>Siderúrgico / Metalmecánico</td>
                  </tr>
                  <tr>
                    <td><strong>Calle</strong></td>
                    <td><span class="tecnm-badge tecnm-badge-danger" style="font-size: 0.7rem;">Sí</span></td>
                    <td>Nombre de la vialidad</td>
                    <td>Prolongación Juárez</td>
                  </tr>
                  <tr>
                    <td><strong>Numero</strong></td>
                    <td><span class="tecnm-badge tecnm-badge-danger" style="font-size: 0.7rem;">Sí</span></td>
                    <td>Texto o número (exterior/int)</td>
                    <td>s/n o 1200</td>
                  </tr>
                  <tr>
                    <td><strong>Colonia</strong></td>
                    <td><span class="tecnm-badge tecnm-badge-danger" style="font-size: 0.7rem;">Sí</span></td>
                    <td>Colonia o parque industrial</td>
                    <td>Parque Industrial Monclova</td>
                  </tr>
                  <tr>
                    <td><strong>Ciudad</strong></td>
                    <td><span class="tecnm-badge tecnm-badge-danger" style="font-size: 0.7rem;">Sí</span></td>
                    <td>Municipio / Ciudad</td>
                    <td>Monclova</td>
                  </tr>
                  <tr>
                    <td><strong>Estado</strong></td>
                    <td><span class="tecnm-badge tecnm-badge-danger" style="font-size: 0.7rem;">Sí</span></td>
                    <td>Entidad federativa</td>
                    <td>Coahuila</td>
                  </tr>
                  <tr>
                    <td><strong>CodigoPostal</strong></td>
                    <td><span class="tecnm-badge tecnm-badge-danger" style="font-size: 0.7rem;">Sí</span></td>
                    <td>5 dígitos numéricos</td>
                    <td>25700</td>
                  </tr>
                  <tr>
                    <td><strong>NombreContacto</strong></td>
                    <td><span class="tecnm-badge tecnm-badge-danger" style="font-size: 0.7rem;">Sí</span></td>
                    <td>Contacto oficial de vinculación</td>
                    <td>Ing. Carlos Mendoza Silva</td>
                  </tr>
                  <tr>
                    <td><strong>CorreoContacto</strong></td>
                    <td><span class="tecnm-badge tecnm-badge-danger" style="font-size: 0.7rem;">Sí</span></td>
                    <td>Email válido con @ y punto</td>
                    <td>cmendoza@ahmsa.com</td>
                  </tr>
                  <tr>
                    <td><strong>TeléfonoContacto</strong></td>
                    <td><span class="tecnm-badge tecnm-badge-danger" style="font-size: 0.7rem;">Sí</span></td>
                    <td>Teléfono con lada</td>
                    <td>866-649-3000</td>
                  </tr>
                  <tr style="background: #eff6ff;">
                    <td><strong>NumeroConvenio</strong></td>
                    <td><span class="tecnm-badge tecnm-badge-warning" style="font-size: 0.7rem;">Condicional</span></td>
                    <td>ID archivo / carpeta (vacío = sin convenio)</td>
                    <td>CV-2025-01</td>
                  </tr>
                  <tr style="background: #eff6ff;">
                    <td><strong>FechaCaducidad</strong></td>
                    <td><span class="tecnm-badge tecnm-badge-secondary" style="font-size: 0.7rem;">Opcional</span></td>
                    <td>DD/MM/AAAA (vacío = Indefinido)</td>
                    <td>31/12/2026</td>
                  </tr>
                  <tr style="background: #eff6ff;">
                    <td><strong>EstadoProceso</strong></td>
                    <td><span class="tecnm-badge tecnm-badge-secondary" style="font-size: 0.7rem;">Opcional</span></td>
                    <td>EN_RENOVACION, CANCELADO, o vacío</td>
                    <td>EN_RENOVACION</td>
                  </tr>
                  <tr style="background: #eff6ff;">
                    <td><strong>AlcanceConvenio</strong></td>
                    <td><span class="tecnm-badge tecnm-badge-warning" style="font-size: 0.7rem;">Condicional</span></td>
                    <td>GENERAL o ESPECIFICOS</td>
                    <td>GENERAL</td>
                  </tr>
                  <tr style="background: #eff6ff;">
                    <td><strong>ClavePIT</strong></td>
                    <td><span class="tecnm-badge tecnm-badge-warning" style="font-size: 0.7rem;">Condicional</span></td>
                    <td>Código PIT oficial</td>
                    <td>5.1.2</td>
                  </tr>
                  <tr style="background: #eff6ff;">
                    <td><strong>TipoCIA</strong></td>
                    <td><span class="tecnm-badge tecnm-badge-warning" style="font-size: 0.7rem;">Condicional</span></td>
                    <td>USO COMPARTIDO, CON IES, etc.</td>
                    <td>USO COMPARTIDO</td>
                  </tr>
                </tbody>
              </table>
            </div>
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

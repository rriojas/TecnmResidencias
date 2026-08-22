import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import apiClient from '@/services/api'

export const useAuthStore = defineStore('auth', () => {
  const token = ref(sessionStorage.getItem('authToken') || null)
  const user = ref(null)

  // Inicializar usuario desde sessionStorage si existe
  try {
    const cachedUser = sessionStorage.getItem('authUser')
    if (cachedUser) {
      user.value = JSON.parse(cachedUser)
    }
  } catch {
    sessionStorage.removeItem('authUser')
  }

  // --- Getters / Computeds ---
  const isAuthenticated = computed(() => Boolean(token.value && user.value))

  const currentRole = computed(() => {
    return (user.value?.role || '').toLowerCase().replace('_', '')
  })

  const permissions = computed(() => {
    return Array.isArray(user.value?.permissions) ? user.value.permissions : []
  })

  const isAdmin = computed(() => {
    if (!user.value) return false
    return (
      user.value.isAdmin === true ||
      user.value.is_admin === true ||
      currentRole.value === 'admin'
    )
  })

  const isReadOnly = computed(() => {
    return currentRole.value === 'director'
  })

  const roleLabel = computed(() => {
    const map = {
      admin: 'Administrador',
      departmenthead: 'Jefe de División',
      advisor: 'Asesor Académico',
      student: 'Estudiante',
      vinculacion: 'Vinculación',
      director: 'Director (Solo Lectura)',
      academic: 'Académico',
    }
    return map[currentRole.value] || user.value?.role || 'Usuario'
  })

  const userDisplayName = computed(() => {
    if (!user.value?.email) return 'Usuario'
    const local = user.value.email.split('@')[0] || ''
    const words = local.split(/[._\-\d]+/).filter(Boolean)
    return words.length
      ? words.map((w) => w.charAt(0).toUpperCase() + w.slice(1)).join(' ')
      : local
  })

  const userAvatarInitials = computed(() => {
    const name = userDisplayName.value
    if (!name) return '··'
    const parts = name.split(' ').filter(Boolean)
    if (parts.length >= 2) {
      return (parts[0][0] + parts[1][0]).toUpperCase()
    }
    return name.slice(0, 2).toUpperCase()
  })

  // --- Helpers de Autorización (RBAC) ---
  function hasRole(...roles) {
    if (!currentRole.value) return false
    return roles.some(
      (r) => (r || '').toLowerCase().replace('_', '') === currentRole.value
    )
  }

  function hasPermission(permission) {
    if (!permission) return true
    if (isAdmin.value) return true
    return permissions.value.some(
      (p) =>
        p === permission ||
        permission.startsWith(p + '.') ||
        p.startsWith(permission + '.')
    )
  }

  const canSeeAudit = computed(() => {
    return (
      !isReadOnly.value &&
      (isAdmin.value ||
        hasPermission('admin.reports') ||
        hasRole('admin', 'departmenthead'))
    )
  })

  const canManageRegistry = computed(() => {
    return (
      !isReadOnly.value &&
      (isAdmin.value ||
        hasPermission('projects.delete') ||
        hasPermission('students.manage') ||
        hasRole('admin', 'departmenthead'))
    )
  })

  const canGrade = computed(() => {
    return (
      !isReadOnly.value &&
      (isAdmin.value ||
        hasPermission('evaluations.grading') ||
        hasRole('admin', 'departmenthead', 'advisor'))
    )
  })

  const canCreateProposal = computed(() => {
    return !isReadOnly.value && (isAdmin.value || currentRole.value === 'student')
  })

  // --- Acciones ---
  async function login(email, password) {
    const response = await apiClient.post('/v1/auth/login', { email, password })
    const data = response.data

    token.value = data.token
    user.value = data.user

    sessionStorage.setItem('authToken', data.token)
    sessionStorage.setItem('authUser', JSON.stringify(data.user))

    return data
  }

  function logout() {
    token.value = null
    user.value = null
    sessionStorage.removeItem('authToken')
    sessionStorage.removeItem('authUser')
  }

  return {
    token,
    user,
    isAuthenticated,
    currentRole,
    permissions,
    isAdmin,
    isReadOnly,
    roleLabel,
    userDisplayName,
    userAvatarInitials,
    hasRole,
    hasPermission,
    canSeeAudit,
    canManageRegistry,
    canGrade,
    canCreateProposal,
    login,
    logout,
  }
})

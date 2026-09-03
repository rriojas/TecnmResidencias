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

  const isCareerHead = computed(() => {
    return currentRole.value === 'jefecarrera' || currentRole.value === 'careerhead'
  })

  const userCareerId = computed(() => {
    return user.value?.careerId ?? user.value?.career_id ?? null
  })

  const roleLabel = computed(() => {
    const map = {
      admin: 'Administrador',
      departmenthead: 'Jefe de División',
      jefecarrera: 'Jefe de Carrera',
      careerhead: 'Jefe de Carrera',
      advisor: 'Asesor Académico',
      student: 'Estudiante',
      vinculacion: 'Vinculación',
      director: 'Director (Solo Lectura)',
      academic: 'Académico',
    }
    return map[currentRole.value] || user.value?.role || 'Usuario'
  })

  const userDisplayName = computed(() => {
    if (user.value?.fullName?.trim()) return user.value.fullName.trim()
    if (user.value?.name?.trim()) return user.value.name.trim()
    if (!user.value?.email) return 'Usuario'
    const local = user.value.email.split('@')[0] || ''
    const words = local.split(/[._\-\d]+/).filter(Boolean)
    return words.length
      ? words.map((w) => w.charAt(0).toUpperCase() + w.slice(1)).join(' ')
      : (user.value.controlNumber ? `Estudiante ${user.value.controlNumber}` : local)
  })

  const userAvatarInitials = computed(() => {
    const name = userDisplayName.value
    if (!name || name === '··') return '··'
    const parts = name.split(' ').filter(Boolean)
    if (parts.length >= 2) {
      const p0 = parts[0][0] || ''
      const p1 = parts[1][0] || ''
      return (p0 + p1).toUpperCase()
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

  const DEFAULT_ROLE_PERMISSIONS = {
    vinculacion: [
      'companies.view', 'companies.create', 'companies.manage', 'companies.import.excel',
      'documents.digital', 'documents.verify', 'documents.letters.generate',
      'students.profile.view', 'students.profile.update', 'students.manage', 'students.eligibility.verify', 'students.import.excel',
      'advisors.view', 'projects.view'
    ],
    departmenthead: [
      'students.manage', 'students.profile.view', 'students.profile.update', 'students.eligibility.verify',
      'advisors.manage',
      'companies.view', 'companies.manage',
      'projects.proposals', 'projects.review', 'projects.delete',
      'activities.schedule',
      'evaluations.advisories', 'evaluations.grading',
      'documents.digital', 'documents.verify',
      'admin.reports'
    ],
    director: [
      'students.profile.view', 'advisors.manage', 'projects.proposals', 'activities.schedule',
      'advisories.session.view', 'evaluations.summary.view', 'documents.digital', 'companies.view',
      'admin.reports', 'reports.export.excel', 'admin.roles'
    ],
    advisor: [
      'projects.review', 'projects.advisor',
      'activities.schedule', 'activities.progress.validate',
      'evaluations.advisories', 'advisories.session.record', 'advisories.session.view',
      'evaluations.grading', 'evaluations.grade.partial', 'evaluations.grade.final', 'evaluations.summary.view',
      'documents.digital', 'documents.verify', 'companies.view'
    ],
    student: [
      'students.profile.view', 'students.profile.update',
      'projects.proposals', 'projects.proposal.create', 'projects.proposal.update', 'projects.my',
      'activities.schedule', 'activities.progress.report',
      'evaluations.advisories', 'advisories.session.view', 'advisories.evidence.upload',
      'documents.digital', 'documents.upload', 'documents.my',
      'companies.view'
    ],
    academic: [
      'companies.view', 'companies.manage', 'documents.digital', 'documents.verify', 'projects.proposals', 'projects.review'
    ],
    jefecarrera: [
      'students.profile.view',
      'projects.review',
      'projects.advisor.assign',
      'activities.schedule',
      'evaluations.advisories', 'advisories.session.view',
      'evaluations.summary.view',
      'documents.digital'
    ],
    careerhead: [
      'students.profile.view',
      'projects.review',
      'projects.advisor.assign',
      'activities.schedule',
      'evaluations.advisories', 'advisories.session.view',
      'evaluations.summary.view',
      'documents.digital'
    ]
  }

  function hasPermission(permission) {
    if (!permission) return true
    if (isAdmin.value) return true

    // 1. Permisos cargados dinámicamente del backend
    if (Array.isArray(permissions.value) && permissions.value.length > 0) {
      if (
        permissions.value.some(
          (p) =>
            p === permission ||
            permission.startsWith(p + '.') ||
            p.startsWith(permission + '.')
        )
      ) {
        return true
      }
    }

    // 2. Respaldo por matriz oficial de rol si el token en sesión no contiene aún todos los permisos
    const defaultPerms = DEFAULT_ROLE_PERMISSIONS[currentRole.value] || []
    return defaultPerms.some(
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
  async function fetchUserProfile() {
    if (!token.value || !user.value) return
    if (user.value.fullName) return

    try {
      if (currentRole.value === 'student') {
        const response = await apiClient.get('/v1/students/me')
        const data = response.data
        if (data) {
          const full = [data.firstName, data.lastName, data.lastName2]
            .filter(Boolean)
            .join(' ')
            .trim()
          if (full) {
            user.value = {
              ...user.value,
              fullName: full,
              controlNumber: data.controlNumber,
              careerId: data.careerId
            }
            sessionStorage.setItem('authUser', JSON.stringify(user.value))
          }
        }
      } else if (currentRole.value === 'advisor') {
        const response = await apiClient.get('/v1/advisors/me')
        const data = response.data
        if (data?.fullName) {
          user.value = {
            ...user.value,
            fullName: data.fullName
          }
          sessionStorage.setItem('authUser', JSON.stringify(user.value))
        }
      }
    } catch {
      // Si falla, mantiene el fallback
    }
  }

  async function login(email, password) {
    const response = await apiClient.post('/v1/auth/login', { email, password })
    const data = response.data

    token.value = data.token
    user.value = data.user

    sessionStorage.setItem('authToken', data.token)
    sessionStorage.setItem('authUser', JSON.stringify(data.user))

    if (!data.user?.fullName) {
      await fetchUserProfile()
    }

    return data
  }

  function logout() {
    token.value = null
    user.value = null
    sessionStorage.removeItem('authToken')
    sessionStorage.removeItem('authUser')
  }

  // Hidratar nombre de usuario en inicio si falta
  if (token.value && user.value && !user.value.fullName) {
    fetchUserProfile()
  }

  return {
    token,
    user,
    isAuthenticated,
    currentRole,
    permissions,
    isAdmin,
    isReadOnly,
    isCareerHead,
    userCareerId,
    roleLabel,
    userDisplayName,
    userAvatarInitials,
    hasRole,
    hasPermission,
    canSeeAudit,
    canManageRegistry,
    canGrade,
    canCreateProposal,
    fetchUserProfile,
    login,
    logout,
  }
})

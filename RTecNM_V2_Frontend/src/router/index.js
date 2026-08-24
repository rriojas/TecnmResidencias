import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const routes = [
  {
    path: '/',
    redirect: '/dashboard',
  },
  {
    path: '/auth/login',
    name: 'Login',
    component: () => import('@/views/auth/LoginView.vue'),
    meta: { isPublic: true, title: 'Iniciar Sesión - Sistema de Residencias' },
  },
  {
    path: '/dashboard',
    name: 'Dashboard',
    component: () => import('@/views/dashboard/DashboardView.vue'),
    meta: {
      requiresAuth: true,
      title: 'Panel Principal - Sistema de Residencias',
      navActive: 'dashboard',
    },
  },
  {
    path: '/students',
    name: 'Students',
    component: () => import('@/views/students/StudentsView.vue'),
    meta: {
      requiresAuth: true,
      title: 'Estudiantes - Sistema de Residencias',
      permission: 'students.manage',
      navActive: 'students',
    },
  },
  {
    path: '/students/profile',
    name: 'StudentProfile',
    component: () => import('@/views/students/StudentProfileView.vue'),
    meta: {
      requiresAuth: true,
      title: 'Perfil de Estudiante - Sistema de Residencias',
      navActive: 'students',
    },
  },
  {
    path: '/advisors',
    name: 'Advisors',
    component: () => import('@/views/advisors/AdvisorsView.vue'),
    meta: {
      requiresAuth: true,
      title: 'Asesores - Sistema de Residencias',
      permission: 'advisors.manage',
      navActive: 'advisors',
    },
  },
  {
    path: '/companies',
    name: 'Companies',
    component: () => import('@/views/companies/CompaniesView.vue'),
    meta: {
      requiresAuth: true,
      title: 'Empresas Receptoras - Sistema de Residencias',
      permission: 'companies.view',
      navActive: 'companies',
    },
  },
  {
    path: '/projects/proposal',
    name: 'ProjectProposal',
    component: () => import('@/views/projects/ProposalView.vue'),
    meta: {
      requiresAuth: true,
      title: 'Solicitud de Anteproyecto - Sistema de Residencias',
      permission: 'projects.proposals',
      navActive: 'proposal',
    },
  },
  {
    path: '/projects/review',
    name: 'ProjectReview',
    component: () => import('@/views/projects/ReviewView.vue'),
    meta: {
      requiresAuth: true,
      title: 'Dictamen de División - Sistema de Residencias',
      permission: 'projects.review',
      navActive: 'review',
    },
  },
  {
    path: '/activities/schedule',
    name: 'ActivitySchedule',
    component: () => import('@/views/activities/ScheduleView.vue'),
    meta: {
      requiresAuth: true,
      title: 'Cronograma de Actividades - Sistema de Residencias',
      permission: 'activities.schedule',
      navActive: 'schedule',
    },
  },
  {
    path: '/evaluations',
    name: 'AdvisorySessions',
    component: () => import('@/views/evaluations/AdvisorySessionsView.vue'),
    meta: {
      requiresAuth: true,
      title: 'Bitácora de Asesorías - Sistema de Residencias',
      permission: 'evaluations.advisories',
      navActive: 'evaluations',
    },
  },
  {
    path: '/evaluations/grading',
    name: 'Grading',
    component: () => import('@/views/evaluations/GradingView.vue'),
    meta: {
      requiresAuth: true,
      title: 'Evaluaciones - Sistema de Residencias',
      permission: 'evaluations.grading',
      navActive: 'grading',
    },
  },
  {
    path: '/documents',
    name: 'Documents',
    component: () => import('@/views/documents/DocumentsView.vue'),
    meta: {
      requiresAuth: true,
      title: 'Expediente Digital - Sistema de Residencias',
      permission: 'documents.digital',
      navActive: 'documents',
    },
  },
  {
    path: '/admin/reports',
    name: 'AdminReports',
    component: () => import('@/views/admin/ReportsView.vue'),
    meta: {
      requiresAuth: true,
      title: 'Reportes y Liberación - Sistema de Residencias',
      permission: 'admin.reports',
      navActive: 'reports',
    },
  },
  {
    path: '/admin/roles',
    name: 'AdminRoles',
    component: () => import('@/views/admin/RolesView.vue'),
    meta: {
      requiresAuth: true,
      title: 'Usuarios y Roles - Sistema de Residencias',
      permission: 'admin.roles',
      navActive: 'roles',
    },
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: '/dashboard',
  },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
  scrollBehavior() {
    return { top: 0 }
  },
})

// Navigation Guards: Autenticación y RBAC
router.beforeEach((to, from, next) => {
  const authStore = useAuthStore()

  // Si ya está autenticado e intenta ir al login, redirigir al dashboard
  if (to.meta.isPublic && authStore.isAuthenticated) {
    return next({ path: '/dashboard' })
  }

  // Si la ruta requiere autenticación y el usuario no está logueado
  if (to.meta.requiresAuth && !authStore.isAuthenticated) {
    return next({ path: '/auth/login', query: { redirect: to.fullPath } })
  }

  // Si la ruta requiere roles específicos
  if (to.meta.roles && Array.isArray(to.meta.roles)) {
    if (!authStore.isAdmin && !authStore.hasRole(...to.meta.roles)) {
      return next({ path: '/dashboard' })
    }
  }

  // Si la ruta requiere permisos específicos
  if (to.meta.permission && !authStore.hasPermission(to.meta.permission)) {
    return next({ path: '/dashboard' })
  }

  next()
})

// Actualizar el título de la pestaña del navegador
router.afterEach((to) => {
  document.title = to.meta.title || 'Sistema de Residencias Profesionales - TecNM'
})

export default router

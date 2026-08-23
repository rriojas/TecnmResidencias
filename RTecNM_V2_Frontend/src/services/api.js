import axios from 'axios'

export const apiClient = axios.create({
  baseURL: '/api',
  headers: {
    'Content-Type': 'application/json',
  },
})

// Interceptor de Request: inyecta el token Bearer en cada petición
apiClient.interceptors.request.use(
  (config) => {
    const token = sessionStorage.getItem('authToken')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  (error) => Promise.reject(error)
)

// Interceptor de Response: captura 401 Unauthorized y redirige a login
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    const isAuthRequest = error.config?.url?.includes('/api/v1/auth/login')
    if (error.response?.status === 401 && !isAuthRequest) {
      sessionStorage.removeItem('authToken')
      sessionStorage.removeItem('authUser')
      if (window.location.pathname !== '/auth/login') {
        window.location.href = '/auth/login'
      }
    }
    return Promise.reject(error)
  }
)

export default apiClient

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
    if (config.data instanceof FormData && config.headers) {
      delete config.headers['Content-Type']
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

/**
 * Procesa errores de subida de archivo para dar una razón clara, minimalista
 * y sin revelar rutas, trazas ni datos sensibles del servidor.
 */
export function getUploadErrorMessage(err, defaultAction = 'subir el archivo') {
  if (!err) return `No se pudo ${defaultAction}: error desconocido.`
  const status = err.response?.status
  if (status === 413) {
    return `No se pudo ${defaultAction}: el archivo supera el límite máximo permitido de 5MB.`
  }
  if (status === 415) {
    return `No se pudo ${defaultAction}: formato de archivo no permitido (solo PDF, JPG o PNG).`
  }
  if (status === 403) {
    return `No se pudo ${defaultAction}: no cuenta con permisos para adjuntar este documento.`
  }
  if (status === 404) {
    return `No se pudo ${defaultAction}: el registro o expediente no fue encontrado.`
  }
  if (status === 400 || status === 409 || status === 422) {
    const raw = err.response?.data?.message || err.response?.data?.title
    if (raw && typeof raw === 'string') {
      const clean = raw.trim()
      const hasSensitive =
        clean.includes('/') ||
        clean.includes('\\') ||
        clean.includes('Exception') ||
        clean.includes('at ') ||
        clean.includes('SELECT') ||
        clean.includes('INSERT') ||
        clean.includes('Stack') ||
        clean.length > 150
      if (!hasSensitive) {
        return `No se pudo ${defaultAction}: ${clean}`
      }
    }
    return `No se pudo ${defaultAction}: los datos o el formato del archivo no son válidos.`
  }
  if (status >= 500) {
    return `No se pudo ${defaultAction}: servicio temporalmente no disponible. Intente más tarde.`
  }
  if (!err.response && (err.code === 'ECONNABORTED' || err.message?.toLowerCase().includes('network'))) {
    return `No se pudo ${defaultAction}: problema de conexión con el servidor.`
  }
  return `No se pudo ${defaultAction}. Intente nuevamente.`
}

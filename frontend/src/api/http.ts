import axios, { AxiosError } from 'axios'

import type { ApiError, ProblemDetails } from '@/types/api'

const baseURL = import.meta.env.VITE_API_BASE_URL ?? '/api/v1'
const csrfClient = axios.create({ baseURL, withCredentials: true })
let csrfToken: string | null = null

export const http = axios.create({
  baseURL,
  withCredentials: true,
  timeout: 15_000,
  headers: { Accept: 'application/json' },
})

http.interceptors.request.use(async (config) => {
  const method = config.method?.toLowerCase()
  if (method && !['get', 'head', 'options'].includes(method)) {
    if (!csrfToken) {
      const response = await csrfClient.get<{ token: string }>('/auth/csrf-token')
      csrfToken = response.data.token
    }
    config.headers.set('X-CSRF-TOKEN', csrfToken)
  }
  return config
})

http.interceptors.response.use(
  (response) => response,
  (error: AxiosError<ProblemDetails>) => {
    const data = error.response?.data
    const apiError: ApiError = {
      status: error.response?.status ?? 0,
      message: data?.detail ?? data?.title ?? (error.response ? '操作失敗，請稍後再試。' : '無法連線至伺服器。'),
      validationErrors: data?.errors,
    }
    return Promise.reject(apiError)
  },
)

export function clearCsrfToken(): void {
  csrfToken = null
}
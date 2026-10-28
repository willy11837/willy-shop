import axios, { AxiosError, type AxiosResponse, type InternalAxiosRequestConfig } from 'axios'

import type { ApiError, ApiResponse } from '@/types/api'
import type { AuthResponse } from '@/types/auth'

const baseURL = import.meta.env.VITE_API_BASE_URL ?? '/api/v1'
const csrfClient = axios.create({ baseURL, withCredentials: true, timeout: 15_000 })
const refreshClient = axios.create({ baseURL, withCredentials: true, timeout: 15_000 })
let anonymousCsrfToken: string | null = null
let anonymousCsrfTokenPromise: Promise<string> | null = null
let authenticatedCsrfToken: { accessToken: string, token: string } | null = null
let authenticatedCsrfTokenPromise: { accessToken: string, promise: Promise<string> } | null = null
let accessToken: string | null = null
let refreshPromise: Promise<AuthResponse> | null = null

interface RetryableRequestConfig extends InternalAxiosRequestConfig {
  _authRetry?: boolean
  _csrfRetry?: boolean
}

export const http = axios.create({
  baseURL,
  withCredentials: true,
  timeout: 15_000,
  headers: { Accept: 'application/json' },
})

export function setAccessToken(token: string | null): void {
  if (accessToken !== token) clearCsrfToken()
  accessToken = token
}

export function clearCsrfToken(): void {
  anonymousCsrfToken = null
  anonymousCsrfTokenPromise = null
  authenticatedCsrfToken = null
  authenticatedCsrfTokenPromise = null
}

async function getCsrfToken(requestAccessToken: string | null = accessToken): Promise<string> {
  if (!requestAccessToken) {
    if (anonymousCsrfToken) return anonymousCsrfToken
    if (!anonymousCsrfTokenPromise) {
      anonymousCsrfTokenPromise = requestCsrfToken(null)
        .then((token) => {
          anonymousCsrfToken = token
          return token
        })
        .finally(() => { anonymousCsrfTokenPromise = null })
    }
    return await anonymousCsrfTokenPromise
  }

  if (authenticatedCsrfToken?.accessToken === requestAccessToken)
    return authenticatedCsrfToken.token

  if (authenticatedCsrfTokenPromise?.accessToken !== requestAccessToken) {
    const promise = requestCsrfToken(requestAccessToken)
      .then((token) => {
        authenticatedCsrfToken = { accessToken: requestAccessToken, token }
        return token
      })
      .finally(() => {
        if (authenticatedCsrfTokenPromise?.accessToken === requestAccessToken)
          authenticatedCsrfTokenPromise = null
      })
    authenticatedCsrfTokenPromise = { accessToken: requestAccessToken, promise }
  }
  return await authenticatedCsrfTokenPromise.promise
}

async function requestCsrfToken(requestAccessToken: string | null): Promise<string> {
  try {
    const response = await csrfClient.get<ApiResponse<{ token: string }>>('/auth/csrf-token', {
      headers: requestAccessToken ? { Authorization: `Bearer ${requestAccessToken}` } : undefined,
    })
    return requireApiData(response).token
  } catch (error) {
    throw normalizeApiError(error)
  }
}

export async function refreshAccessToken(): Promise<AuthResponse> {
  if (!refreshPromise) {
    refreshPromise = (async () => {
      const token = await getCsrfToken(null)
      try {
        const response = await refreshClient.post<ApiResponse<AuthResponse>>('/auth/refresh', undefined, {
          headers: { 'X-CSRF-TOKEN': token },
        })
        const data = requireApiData(response)
        setAccessToken(data.accessToken)
        return data
      } catch (error) {
        throw normalizeApiError(error)
      }
    })().finally(() => { refreshPromise = null })
  }
  return refreshPromise
}

http.interceptors.request.use(async (config) => {
  if (accessToken) config.headers.set('Authorization', `Bearer ${accessToken}`)

  const method = config.method?.toLowerCase()
  if (method && !['get', 'head', 'options'].includes(method))
    config.headers.set('X-CSRF-TOKEN', await getCsrfToken())

  return config
})

http.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ApiResponse<unknown>>) => {
    const config = error.config as RetryableRequestConfig | undefined
    const requestPath = config?.url ?? ''
    const data = error.response?.data
    const canRetryCsrf = error.response?.status === 400
      && data?.message === '要求缺少有效的 CSRF Token。'
      && config
      && !config._csrfRetry

    if (canRetryCsrf) {
      config._csrfRetry = true
      clearCsrfToken()
      config.headers.delete('X-CSRF-TOKEN')
      return await http(config)
    }

    const canRefresh = error.response?.status === 401
      && config
      && !config._authRetry
      && !requestPath.includes('/auth/login')
      && !requestPath.includes('/auth/register')
      && !requestPath.includes('/auth/refresh')

    if (canRefresh) {
      config._authRetry = true
      try {
        const response = await refreshAccessToken()
        config.headers.set('Authorization', `Bearer ${response.accessToken}`)
        return await http(config)
      } catch {
        setAccessToken(null)
      }
    }

    return Promise.reject(normalizeApiError(error))
  },
)

export function requireApiData<T>(response: AxiosResponse<ApiResponse<T>>): T {
  const envelope = response.data
  if (!envelope.success)
    throw { status: envelope.code, message: envelope.message } satisfies ApiError
  if (envelope.data === null)
    throw { status: envelope.code, message: '伺服器回應缺少資料。' } satisfies ApiError
  return envelope.data
}

function normalizeApiError(error: unknown): ApiError {
  if (axios.isAxiosError<ApiResponse<unknown>>(error)) {
    return {
      status: error.response?.status ?? 0,
      message: error.response?.data?.message
        ?? (error.response ? `伺服器回傳 HTTP ${error.response.status}，但未提供錯誤訊息。` : '無法連線至伺服器。'),
    }
  }

  const apiError = error as Partial<ApiError>
  return {
    status: apiError.status ?? 0,
    message: apiError.message ?? '無法取得伺服器回應。',
  }
}
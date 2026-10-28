import type { ApiError } from '@/types/api'

export function errorMessage(error: unknown): string {
  const apiError = error as Partial<ApiError>
  return apiError.message ?? '無法取得伺服器回應。'
}
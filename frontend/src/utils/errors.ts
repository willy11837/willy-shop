import type { ApiError } from '@/types/api'

export function errorMessage(error: unknown): string {
  const apiError = error as Partial<ApiError>
  if (apiError.validationErrors) {
    const messages = Object.values(apiError.validationErrors).flat()
    if (messages.length > 0) return messages.join('、')
  }
  return apiError.message ?? '操作失敗，請稍後再試。'
}
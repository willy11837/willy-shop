export interface ApiResponse<T> {
  success: boolean
  code: number
  message: string
  data: T | null
}

export interface ApiValidationErrorData {
  errors: Record<string, string[]>
}

export interface ApiError {
  status: number
  message: string
}
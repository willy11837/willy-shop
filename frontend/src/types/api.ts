export interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
  errors?: Record<string, string[]>
}

export interface ApiError {
  status: number
  message: string
  validationErrors?: Record<string, string[]>
}
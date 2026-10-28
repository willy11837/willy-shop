import { clearCsrfToken, http, refreshAccessToken, requireApiData, setAccessToken } from './http'
import type { ApiResponse } from '@/types/api'
import type { AuthResponse, CurrentUser, LoginRequest, RegisterRequest } from '@/types/auth'

export async function restoreSession(): Promise<AuthResponse> {
  return await refreshAccessToken()
}

export async function getCurrentUser(): Promise<CurrentUser> {
  return requireApiData(await http.get<ApiResponse<CurrentUser>>('/auth/me'))
}

export async function login(request: LoginRequest): Promise<AuthResponse> {
  const response = requireApiData(await http.post<ApiResponse<AuthResponse>>('/auth/login', request))
  setAccessToken(response.accessToken)
  clearCsrfToken()
  return response
}

export async function register(request: RegisterRequest): Promise<AuthResponse> {
  const response = requireApiData(await http.post<ApiResponse<AuthResponse>>('/auth/register', request))
  setAccessToken(response.accessToken)
  clearCsrfToken()
  return response
}

export async function logout(): Promise<void> {
  await http.post('/auth/logout')
  setAccessToken(null)
  clearCsrfToken()
}
import { clearCsrfToken, http } from './http'
import type { CurrentUser, LoginRequest, RegisterRequest } from '@/types/auth'

export async function getCurrentUser(): Promise<CurrentUser> {
  return (await http.get<CurrentUser>('/auth/me')).data
}

export async function login(request: LoginRequest): Promise<CurrentUser> {
  const user = (await http.post<CurrentUser>('/auth/login', request)).data
  clearCsrfToken()
  return user
}

export async function register(request: RegisterRequest): Promise<CurrentUser> {
  const user = (await http.post<CurrentUser>('/auth/register', request)).data
  clearCsrfToken()
  return user
}

export async function logout(): Promise<void> {
  await http.post('/auth/logout')
  clearCsrfToken()
}
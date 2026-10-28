export const roles = {
  system: 'System',
  admin: 'Admin',
  store: 'Store',
  normal: 'Normal',
  guest: 'Guest',
} as const

export type Role = (typeof roles)[keyof typeof roles]

export interface CurrentUser {
  id: string
  account: string
  displayName: string | null
  email: string | null
  roles: Role[]
}

export interface AuthResponse {
  accessToken: string
  expiresAt: string
  user: CurrentUser
}

export interface LoginRequest {
  account: string
  password: string
}

export interface RegisterRequest {
  account: string
  displayName: string
  email: string
  password: string
  confirmPassword: string
}
export const roles = {
  system: 'System',
  admin: 'Admin',
  store: 'Store',
  normal: 'Normal',
  guest: 'Guest',
} as const

export type Role = (typeof roles)[keyof typeof roles]

export interface CurrentUser {
  isAuthenticated: boolean
  id: string | null
  account: string | null
  displayName: string | null
  email: string | null
  roles: Role[]
}

export interface LoginRequest {
  account: string
  password: string
  rememberMe: boolean
}

export interface RegisterRequest {
  account: string
  displayName: string
  email: string
  password: string
  confirmPassword: string
}
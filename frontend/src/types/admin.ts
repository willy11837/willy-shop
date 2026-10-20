export interface AdminUser {
  id: string
  account: string
  displayName: string | null
  email: string | null
  role: string
  createdAt: string
  modifiedBy: string | null
  modifiedAt: string | null
}

export interface CreateUserRequest {
  account: string
  displayName: string
  email: string
  password: string
  role: string
}

export interface UpdateUserRequest {
  displayName: string
  email: string
  newPassword?: string
  role: string
}
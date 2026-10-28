export interface Profile {
  id: string
  account: string
  displayName: string
  email: string
  roles: string[]
  createdAt: string
  modifiedAt: string | null
}

export interface UpdateProfileRequest {
  displayName: string
  email: string
}

export interface ChangePasswordRequest {
  currentPassword: string
  newPassword: string
  confirmPassword: string
}
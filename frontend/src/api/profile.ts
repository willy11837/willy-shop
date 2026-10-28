import { http, requireApiData } from './http'
import type { ApiResponse } from '@/types/api'
import type { ChangePasswordRequest, Profile, UpdateProfileRequest } from '@/types/profile'

export async function getProfile(): Promise<Profile> {
  return requireApiData(await http.get<ApiResponse<Profile>>('/profile'))
}

export async function updateProfile(request: UpdateProfileRequest): Promise<Profile> {
  return requireApiData(await http.put<ApiResponse<Profile>>('/profile', request))
}

export async function changePassword(request: ChangePasswordRequest): Promise<void> {
  await http.post<ApiResponse<null>>('/profile/change-password', request)
}
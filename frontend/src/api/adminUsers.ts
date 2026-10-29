import { http, requireApiData } from './http'
import type { ApiResponse } from '@/types/api'
import type { AdminUser, AdminUserQuery, CreateUserRequest, PagedAdminUsers, UpdateUserRequest } from '@/types/admin'

export async function getAdminUsers(query: AdminUserQuery = {}): Promise<PagedAdminUsers> {
  return requireApiData(await http.get<ApiResponse<PagedAdminUsers>>('/admin/users', { params: query }))
}

export async function getAssignableRoles(): Promise<string[]> {
  return requireApiData(await http.get<ApiResponse<string[]>>('/admin/users/roles'))
}

export async function createAdminUser(request: CreateUserRequest): Promise<AdminUser> {
  return requireApiData(await http.post<ApiResponse<AdminUser>>('/admin/users', request))
}

export async function updateAdminUser(id: string, request: UpdateUserRequest): Promise<AdminUser> {
  return requireApiData(await http.put<ApiResponse<AdminUser>>(`/admin/users/${id}`, request))
}

export async function deleteAdminUser(id: string): Promise<void> {
  await http.delete(`/admin/users/${id}`)
}
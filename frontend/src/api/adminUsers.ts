import { http } from './http'
import type { AdminUser, CreateUserRequest, UpdateUserRequest } from '@/types/admin'

export async function getAdminUsers(): Promise<AdminUser[]> {
  return (await http.get<AdminUser[]>('/admin/users')).data
}

export async function getAssignableRoles(): Promise<string[]> {
  return (await http.get<string[]>('/admin/users/roles')).data
}

export async function createAdminUser(request: CreateUserRequest): Promise<AdminUser> {
  return (await http.post<AdminUser>('/admin/users', request)).data
}

export async function updateAdminUser(id: string, request: UpdateUserRequest): Promise<AdminUser> {
  return (await http.put<AdminUser>(`/admin/users/${id}`, request)).data
}

export async function deleteAdminUser(id: string): Promise<void> {
  await http.delete(`/admin/users/${id}`)
}
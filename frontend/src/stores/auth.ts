import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

import * as authApi from '@/api/auth'
import type { LoginRequest, RegisterRequest, Role, CurrentUser } from '@/types/auth'

export const useAuthStore = defineStore('auth', () => {
  const user = ref<CurrentUser | null>(null)
  const initialized = ref(false)
  const isAuthenticated = computed(() => user.value !== null)

  async function initialize(): Promise<void> {
    if (initialized.value) return
    try { user.value = (await authApi.restoreSession()).user }
    catch { user.value = null }
    finally { initialized.value = true }
  }

  async function login(request: LoginRequest): Promise<void> {
    user.value = (await authApi.login(request)).user
    initialized.value = true
  }

  async function register(request: RegisterRequest): Promise<void> {
    user.value = (await authApi.register(request)).user
    initialized.value = true
  }

  async function logout(): Promise<void> {
    await authApi.logout()
    user.value = null
    initialized.value = true
  }

  async function reloadUser(): Promise<void> {
    user.value = await authApi.getCurrentUser()
  }

  function hasAnyRole(required: Role[]): boolean {
    return required.some((role) => user.value?.roles.includes(role))
  }

  return { user, initialized, isAuthenticated, initialize, login, register, logout, reloadUser, hasAnyRole }
})
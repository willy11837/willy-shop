<script setup lang="ts">
import { computed, onMounted, onUnmounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useCartStore } from '@/stores/cart'
import { useNotificationStore } from '@/stores/notifications'
import { roles } from '@/types/auth'

const router = useRouter()
const auth = useAuthStore()
const cart = useCartStore()
const notifications = useNotificationStore()
const canAccessAdmin = computed(() => auth.hasAnyRole([roles.system, roles.admin, roles.store]))

onMounted(() => cart.load())
watch(
  () => auth.isAuthenticated,
  (isAuthenticated) => { if (isAuthenticated) notifications.start(); else notifications.stop() },
  { immediate: true },
)
onUnmounted(() => notifications.stop())

async function signOut(): Promise<void> {
  await auth.logout()
  await cart.load()
  await router.push('/')
}
</script>

<template>
  <div class="app-shell">
    <header class="site-header">
      <div class="container nav-row">
        <RouterLink class="brand" to="/">Willy Shop</RouterLink>
        <nav class="nav-links" aria-label="主要導覽">
          <RouterLink to="/">商品</RouterLink>
          <RouterLink to="/cart">購物車 <span class="badge">{{ cart.totalQuantity }}</span></RouterLink>
          <RouterLink v-if="auth.isAuthenticated" to="/orders">訂單</RouterLink>
          <RouterLink v-if="auth.isAuthenticated" to="/notifications">
            通知 <span v-if="notifications.unreadCount" class="badge">{{ notifications.unreadCount }}</span>
          </RouterLink>
          <RouterLink v-if="auth.isAuthenticated" to="/profile">個人資料</RouterLink>
          <RouterLink v-if="canAccessAdmin" :to="{ name: 'admin' }">管理後台</RouterLink>
          <RouterLink v-if="!auth.isAuthenticated" to="/login">登入</RouterLink>
          <RouterLink v-if="!auth.isAuthenticated" to="/register">註冊</RouterLink>
          <button v-if="auth.isAuthenticated" class="link-button" type="button" @click="signOut">
            登出（{{ auth.user?.displayName || auth.user?.account }}）
          </button>
        </nav>
      </div>
    </header>
    <main class="container page-content"><RouterView /></main>
  </div>
</template>
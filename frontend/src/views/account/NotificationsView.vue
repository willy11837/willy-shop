<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import * as notificationApi from '@/api/notifications'
import { useNotificationStore } from '@/stores/notifications'
import type { UserNotification } from '@/types/notification'
import { errorMessage } from '@/utils/errors'

const router = useRouter()
const notificationStore = useNotificationStore()
const notifications = ref<UserNotification[]>([])
const loading = ref(true)
const error = ref('')

onMounted(async () => {
  try { notifications.value = (await notificationApi.getNotifications(1, 100)).items }
  catch (reason) { error.value = errorMessage(reason) }
  finally { loading.value = false }
})

async function openNotification(notification: UserNotification): Promise<void> {
  try {
    if (!notification.readAt) {
      await notificationApi.markNotificationRead(notification.id)
      notification.readAt = new Date().toISOString()
      notificationStore.decrementUnreadCount()
    }
    if (notification.targetUrl) await router.push(notification.targetUrl)
  } catch (reason) { error.value = errorMessage(reason) }
}

async function markAllRead(): Promise<void> {
  try {
    await notificationApi.markAllNotificationsRead()
    const readAt = new Date().toISOString()
    notifications.value.forEach((notification) => { notification.readAt ??= readAt })
    notificationStore.clearUnreadCount()
  } catch (reason) { error.value = errorMessage(reason) }
}
</script>

<template>
  <section>
    <div class="page-header">
      <h1>通知中心</h1>
      <button
        v-if="notifications.some((notification) => !notification.readAt)"
        class="button secondary"
        type="button"
        @click="markAllRead"
      >
        全部標示為已讀
      </button>
    </div>
    <p v-if="error" class="alert">{{ error }}</p>
    <p v-if="loading" class="card">通知載入中...</p>
    <div v-else-if="notifications.length" class="grid">
      <button
        v-for="notification in notifications"
        :key="notification.id"
        class="card notification-card"
        :class="{ unread: !notification.readAt }"
        type="button"
        @click="openNotification(notification)"
      >
        <span class="page-header">
          <strong>{{ notification.title }}</strong>
          <span class="muted">{{ new Date(notification.createdAt).toLocaleString() }}</span>
        </span>
        <span>{{ notification.message }}</span>
        <span v-if="!notification.readAt" class="muted">未讀</span>
      </button>
    </div>
    <p v-else class="card">目前沒有通知。</p>
  </section>
</template>

<style scoped>
.notification-card { width: 100%; color: inherit; text-align: left; cursor: pointer; border: 1px solid transparent; }
.notification-card.unread { border-color: #2563eb; background: #eff6ff; }
</style>
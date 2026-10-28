import { ref } from 'vue'
import { defineStore } from 'pinia'

import * as notificationApi from '@/api/notifications'

const refreshIntervalMilliseconds = 60_000

export const useNotificationStore = defineStore('notifications', () => {
  const unreadCount = ref(0)
  let refreshTimer: ReturnType<typeof setInterval> | null = null

  async function refreshUnreadCount(): Promise<void> {
    try { unreadCount.value = await notificationApi.getUnreadNotificationCount() }
    catch { /* 導覽列更新失敗不應中斷目前頁面操作。 */ }
  }

  function handleWindowFocus(): void {
    void refreshUnreadCount()
  }

  function start(): void {
    if (refreshTimer) return
    void refreshUnreadCount()
    window.addEventListener('focus', handleWindowFocus)
    refreshTimer = setInterval(() => { void refreshUnreadCount() }, refreshIntervalMilliseconds)
  }

  function stop(): void {
    if (refreshTimer) clearInterval(refreshTimer)
    refreshTimer = null
    window.removeEventListener('focus', handleWindowFocus)
    unreadCount.value = 0
  }

  function decrementUnreadCount(): void {
    unreadCount.value = Math.max(0, unreadCount.value - 1)
  }

  function clearUnreadCount(): void {
    unreadCount.value = 0
  }

  return { unreadCount, refreshUnreadCount, start, stop, decrementUnreadCount, clearUnreadCount }
})
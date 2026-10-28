import { http, requireApiData } from './http'
import type { ApiResponse } from '@/types/api'
import type {
  BackInStockSubscription,
  PagedNotifications,
  UnreadNotificationCount,
} from '@/types/notification'

const subscriptionUrl = (productCode: string): string =>
  `/products/${encodeURIComponent(productCode)}/back-in-stock-subscription`

export async function getBackInStockSubscription(productCode: string): Promise<BackInStockSubscription> {
  return requireApiData(await http.get<ApiResponse<BackInStockSubscription>>(subscriptionUrl(productCode)))
}

export async function subscribeBackInStock(productCode: string): Promise<BackInStockSubscription> {
  return requireApiData(await http.post<ApiResponse<BackInStockSubscription>>(subscriptionUrl(productCode)))
}

export async function unsubscribeBackInStock(productCode: string): Promise<void> {
  await http.delete(subscriptionUrl(productCode))
}

export async function getNotifications(page = 1, pageSize = 20): Promise<PagedNotifications> {
  return requireApiData(await http.get<ApiResponse<PagedNotifications>>('/notifications', {
    params: { page, pageSize },
  }))
}

export async function getUnreadNotificationCount(): Promise<number> {
  const result = requireApiData(
    await http.get<ApiResponse<UnreadNotificationCount>>('/notifications/unread-count'),
  )
  return result.count
}

export async function markNotificationRead(notificationId: number): Promise<void> {
  await http.put(`/notifications/${notificationId}/read`)
}

export async function markAllNotificationsRead(): Promise<void> {
  await http.put('/notifications/read-all')
}
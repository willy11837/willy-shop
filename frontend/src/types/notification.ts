export const notificationTypes = {
  backInStock: 0,
} as const

export interface BackInStockSubscription {
  isSubscribed: boolean
  createdAt: string | null
}

export interface UserNotification {
  id: number
  type: number
  typeName: string
  title: string
  message: string
  productCode: string | null
  targetUrl: string | null
  createdAt: string
  readAt: string | null
}

export interface PagedNotifications {
  items: UserNotification[]
  page: number
  pageSize: number
  totalCount: number
}

export interface UnreadNotificationCount {
  count: number
}
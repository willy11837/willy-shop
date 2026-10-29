export interface OrderItem {
  productCode: string
  productName: string
  unitPrice: number
  quantity: number
  subtotal: number
}

export interface Order {
  id: number
  totalAmount: number
  createdAt: string
  status: string
  statusName: string
  modifiedAt: string | null
  shippedAt: string | null
  completedAt: string | null
  cancelledAt: string | null
  returnRequestedAt: string | null
  returnReason: string | null
  returnReviewedAt: string | null
  returnReviewNote: string | null
  refundCompletedAt: string | null
  refundAmount: number | null
  refundNote: string | null
  items: OrderItem[]
}

export const orderStatuses = {
  pending: 'Pending',
  shipped: 'Shipped',
  completed: 'Completed',
  cancelled: 'Cancelled',
  returnRequested: 'ReturnRequested',
  returnApproved: 'ReturnApproved',
  refunded: 'Refunded',
} as const

export type OrderStatus = (typeof orderStatuses)[keyof typeof orderStatuses]

export const orderStatusOptions: ReadonlyArray<{ value: OrderStatus; label: string }> = [
  { value: orderStatuses.pending, label: '待處理' },
  { value: orderStatuses.shipped, label: '寄送中' },
  { value: orderStatuses.completed, label: '已完成' },
  { value: orderStatuses.returnRequested, label: '退貨申請中' },
  { value: orderStatuses.returnApproved, label: '退貨已核准' },
  { value: orderStatuses.refunded, label: '已退款' },
  { value: orderStatuses.cancelled, label: '已取消' },
]

export interface AdminOrder extends Order {
  userId: string
  account: string
  displayName: string | null
  email: string | null
  returnReviewedBy: string | null
  refundProcessedBy: string | null
}

export interface AdminOrderQuery {
  keyword?: string
  status?: string
  createdFrom?: string
  createdTo?: string
  sortBy?: 'createdAt' | 'totalAmount'
  sortDirection?: 'asc' | 'desc'
  page?: number
  pageSize?: number
}

export interface PagedAdminOrders {
  items: AdminOrder[]
  page: number
  pageSize: number
  totalCount: number
}
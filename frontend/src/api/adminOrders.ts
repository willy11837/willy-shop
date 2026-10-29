import { http, requireApiData } from './http'
import type { ApiResponse } from '@/types/api'
import type { AdminOrder, AdminOrderQuery, Order, PagedAdminOrders } from '@/types/order'

export async function getAdminOrders(query: AdminOrderQuery = {}): Promise<PagedAdminOrders> {
  return requireApiData(await http.get<ApiResponse<PagedAdminOrders>>('/admin/orders', { params: query }))
}

export async function getAdminOrder(orderId: number): Promise<AdminOrder> {
  return requireApiData(await http.get<ApiResponse<AdminOrder>>(`/admin/orders/${orderId}`))
}

export async function shipOrder(orderId: number): Promise<Order> {
  return requireApiData(await http.post<ApiResponse<Order>>(`/admin/orders/${orderId}/ship`))
}

export async function cancelAdminOrder(orderId: number): Promise<Order> {
  return requireApiData(await http.post<ApiResponse<Order>>(`/admin/orders/${orderId}/cancel`))
}

export async function approveReturn(orderId: number, note?: string): Promise<Order> {
  return requireApiData(await http.post<ApiResponse<Order>>(`/admin/orders/${orderId}/approve-return`, { note }))
}

export async function rejectReturn(orderId: number, note?: string): Promise<Order> {
  return requireApiData(await http.post<ApiResponse<Order>>(`/admin/orders/${orderId}/reject-return`, { note }))
}

export async function completeRefund(orderId: number, note?: string): Promise<Order> {
  return requireApiData(await http.post<ApiResponse<Order>>(`/admin/orders/${orderId}/complete-refund`, { note }))
}
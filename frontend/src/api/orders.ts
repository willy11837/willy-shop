import { http, requireApiData } from './http'
import type { ApiResponse } from '@/types/api'
import type { Order } from '@/types/order'

export async function getOrders(): Promise<Order[]> {
  return requireApiData(await http.get<ApiResponse<Order[]>>('/orders'))
}

export async function getOrder(orderId: number): Promise<Order> {
  return requireApiData(await http.get<ApiResponse<Order>>(`/orders/${orderId}`))
}

export async function checkout(): Promise<Order> {
  return requireApiData(await http.post<ApiResponse<Order>>('/orders/checkout'))
}
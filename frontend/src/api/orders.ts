import { http } from './http'
import type { Order } from '@/types/order'

export async function getOrders(): Promise<Order[]> {
  return (await http.get<Order[]>('/orders')).data
}

export async function checkout(): Promise<Order> {
  return (await http.post<Order>('/orders/checkout')).data
}
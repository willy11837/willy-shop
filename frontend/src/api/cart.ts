import { http, requireApiData } from './http'
import type { ApiResponse } from '@/types/api'
import type { Cart } from '@/types/cart'

export async function getCart(): Promise<Cart> {
  return requireApiData(await http.get<ApiResponse<Cart>>('/cart'))
}

export async function addCartItem(productCode: string, quantity: number): Promise<Cart> {
  return requireApiData(await http.post<ApiResponse<Cart>>(`/cart/items/${encodeURIComponent(productCode)}`, { quantity }))
}

export async function updateCartItem(productCode: string, quantity: number): Promise<Cart> {
  return requireApiData(await http.put<ApiResponse<Cart>>(`/cart/items/${encodeURIComponent(productCode)}`, { quantity }))
}

export async function removeCartItem(productCode: string): Promise<Cart> {
  return requireApiData(await http.delete<ApiResponse<Cart>>(`/cart/items/${encodeURIComponent(productCode)}`))
}
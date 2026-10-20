import { http } from './http'
import type { Cart } from '@/types/cart'

export async function getCart(): Promise<Cart> {
  return (await http.get<Cart>('/cart')).data
}

export async function addCartItem(productCode: string, quantity: number): Promise<Cart> {
  return (await http.post<Cart>(`/cart/items/${encodeURIComponent(productCode)}`, { quantity })).data
}

export async function updateCartItem(productCode: string, quantity: number): Promise<Cart> {
  return (await http.put<Cart>(`/cart/items/${encodeURIComponent(productCode)}`, { quantity })).data
}

export async function removeCartItem(productCode: string): Promise<Cart> {
  return (await http.delete<Cart>(`/cart/items/${encodeURIComponent(productCode)}`)).data
}
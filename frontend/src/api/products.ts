import { http } from './http'
import type { Product, SaveProductRequest } from '@/types/product'

export async function getProducts(search = '', category = ''): Promise<Product[]> {
  return (await http.get<Product[]>('/products', { params: { search: search || undefined, category: category || undefined } })).data
}

export async function getProduct(productCode: string): Promise<Product> {
  return (await http.get<Product>(`/products/${encodeURIComponent(productCode)}`)).data
}

export async function getAdminProducts(): Promise<Product[]> {
  return (await http.get<Product[]>('/admin/products')).data
}

export async function createProduct(request: SaveProductRequest): Promise<Product> {
  return (await http.post<Product>('/admin/products', request)).data
}

export async function updateProduct(code: string, request: SaveProductRequest): Promise<Product> {
  return (await http.put<Product>(`/admin/products/${encodeURIComponent(code)}`, request)).data
}

export async function deleteProduct(code: string): Promise<void> {
  await http.delete(`/admin/products/${encodeURIComponent(code)}`)
}
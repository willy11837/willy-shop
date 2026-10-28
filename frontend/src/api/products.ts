import { http, requireApiData } from './http'
import type { ApiResponse } from '@/types/api'
import type { PagedProducts, Product, ProductQuery, SaveProductRequest } from '@/types/product'

export async function getProducts(query: ProductQuery = {}): Promise<PagedProducts> {
  return requireApiData(await http.get<ApiResponse<PagedProducts>>('/products', { params: query }))
}

export async function getProduct(productCode: string): Promise<Product> {
  return requireApiData(await http.get<ApiResponse<Product>>(`/products/${encodeURIComponent(productCode)}`))
}

export async function getOnSaleProducts(): Promise<Product[]> {
  return requireApiData(await http.get<ApiResponse<Product[]>>('/products/on-sale'))
}

export async function getAdminProducts(): Promise<Product[]> {
  return requireApiData(await http.get<ApiResponse<Product[]>>('/admin/products'))
}

export async function createProduct(request: SaveProductRequest): Promise<Product> {
  return requireApiData(await http.post<ApiResponse<Product>>('/admin/products', request))
}

export async function updateProduct(code: string, request: SaveProductRequest): Promise<Product> {
  return requireApiData(await http.put<ApiResponse<Product>>(`/admin/products/${encodeURIComponent(code)}`, request))
}

export async function deleteProduct(code: string): Promise<void> {
  await http.delete(`/admin/products/${encodeURIComponent(code)}`)
}
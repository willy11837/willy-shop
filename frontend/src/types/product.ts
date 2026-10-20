export const productStatuses = {
  available: 0,
  frozen: 1,
  outOfStock: 2,
} as const

export interface Product {
  productCode: string
  name: string
  price: number
  category: string | null
  stockQuantity: number
  status: number
  statusName: string
  imageUrl: string | null
  createdAt: string
  modifiedBy: string | null
  modifiedAt: string | null
}

export interface SaveProductRequest {
  productCode: string
  name: string
  price: number
  category: string | null
  stockQuantity: number
  status: number
  imagePath: string | null
}
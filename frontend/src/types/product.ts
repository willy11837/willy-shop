export const productStatuses = {
  available: 0,
  frozen: 1,
  outOfStock: 2,
} as const

export interface Product {
  productCode: string
  name: string
  price: number
  isOnSale: boolean
  salePrice: number | null
  effectivePrice: number
  priceStatusName: string
  category: string | null
  stockQuantity: number
  status: number
  statusName: string
  imageUrl: string | null
  createdAt: string
  modifiedBy: string | null
  modifiedAt: string | null
}

export interface ProductQuery {
  productCode?: string
  productName?: string
  category?: string
  modifiedFrom?: string
  modifiedTo?: string
  sortBy?: 'productCode' | 'productName' | 'price' | 'modifiedAt'
  sortDirection?: 'asc' | 'desc'
  page?: number
  pageSize?: number
}

export interface AdminProductQuery extends ProductQuery {
  status?: number
}

export interface PagedProducts {
  items: Product[]
  page: number
  pageSize: number
  totalCount: number
}

export interface SaveProductRequest {
  productCode: string
  name: string
  price: number
  isOnSale: boolean
  salePrice: number | null
  category: string | null
  stockQuantity: number
  status: number
  imagePath: string | null
}
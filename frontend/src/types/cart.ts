export interface CartItem {
  id: number | null
  productCode: string
  name: string
  unitPrice: number
  quantity: number
  subtotal: number
  stockQuantity: number
  imageUrl: string | null
}

export interface Cart {
  items: CartItem[]
  totalQuantity: number
  totalAmount: number
}
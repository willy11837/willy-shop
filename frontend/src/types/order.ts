export interface OrderItem {
  productCode: string
  productName: string
  unitPrice: number
  quantity: number
  subtotal: number
}

export interface Order {
  id: number
  totalAmount: number
  createdAt: string
  status: string
  items: OrderItem[]
}
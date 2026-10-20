import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

import * as cartApi from '@/api/cart'
import type { Cart } from '@/types/cart'

const emptyCart = (): Cart => ({ items: [], totalQuantity: 0, totalAmount: 0 })

export const useCartStore = defineStore('cart', () => {
  const cart = ref<Cart>(emptyCart())
  const loading = ref(false)
  const totalQuantity = computed(() => cart.value.totalQuantity)

  async function load(): Promise<void> {
    loading.value = true
    try { cart.value = await cartApi.getCart() }
    finally { loading.value = false }
  }

  async function add(productCode: string, quantity = 1): Promise<void> {
    cart.value = await cartApi.addCartItem(productCode, quantity)
  }

  async function update(productCode: string, quantity: number): Promise<void> {
    cart.value = await cartApi.updateCartItem(productCode, quantity)
  }

  async function remove(productCode: string): Promise<void> {
    cart.value = await cartApi.removeCartItem(productCode)
  }

  return { cart, loading, totalQuantity, load, add, update, remove }
})
<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { getProduct } from '@/api/products'
import { useCartStore } from '@/stores/cart'
import type { Product } from '@/types/product'
import { errorMessage } from '@/utils/errors'

const route = useRoute()
const cart = useCartStore()
const product = ref<Product | null>(null)
const quantity = ref(1)
const error = ref('')
const message = ref('')

onMounted(async () => {
  try { product.value = await getProduct(String(route.params.productCode)) }
  catch (reason) { error.value = errorMessage(reason) }
})

async function add(): Promise<void> {
  if (!product.value) return
  try {
    await cart.add(product.value.productCode, quantity.value)
    message.value = '商品已加入購物車。'
    error.value = ''
  } catch (reason) { error.value = errorMessage(reason) }
}
</script>

<template>
  <p v-if="error" class="alert">{{ error }}</p>
  <p v-if="message" class="alert success">{{ message }}</p>
  <article v-if="product" class="card product-detail">
    <img class="product-image" :src="product.imageUrl || '/vite.svg'" :alt="product.name" />
    <div>
      <p class="muted">{{ product.productCode }} · {{ product.category || '未分類' }}</p>
      <h1>{{ product.name }}</h1>
      <p class="price">NT$ {{ product.price.toLocaleString() }}</p>
      <p>庫存：{{ product.stockQuantity }}</p>
      <div class="actions">
        <input v-model.number="quantity" type="number" min="1" :max="product.stockQuantity" aria-label="數量" />
        <button class="button" type="button" @click="add">加入購物車</button>
        <RouterLink class="button secondary" to="/">返回列表</RouterLink>
      </div>
    </div>
  </article>
</template>

<style scoped>.product-detail { display: grid; grid-template-columns: minmax(260px, 480px) 1fr; gap: 32px; } input { width: 90px; padding: 9px; } @media(max-width:720px){.product-detail{grid-template-columns:1fr}}</style>
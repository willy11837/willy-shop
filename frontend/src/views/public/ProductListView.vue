<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { getProducts } from '@/api/products'
import { useCartStore } from '@/stores/cart'
import type { Product } from '@/types/product'
import { errorMessage } from '@/utils/errors'

const cart = useCartStore()
const products = ref<Product[]>([])
const search = ref('')
const loading = ref(false)
const message = ref('')
const error = ref('')

async function load(): Promise<void> {
  loading.value = true
  error.value = ''
  try { products.value = await getProducts(search.value) }
  catch (reason) { error.value = errorMessage(reason) }
  finally { loading.value = false }
}

async function add(product: Product): Promise<void> {
  error.value = ''
  message.value = ''
  try {
    await cart.add(product.productCode)
    message.value = `已將「${product.name}」加入購物車。`
  } catch (reason) { error.value = errorMessage(reason) }
}

onMounted(load)
</script>

<template>
  <section>
    <div class="page-header">
      <div><h1>商品列表</h1><p class="muted">瀏覽目前可銷售且有庫存的商品。</p></div>
      <form class="actions" @submit.prevent="load">
        <input v-model.trim="search" aria-label="搜尋商品" placeholder="商品代碼或名稱" />
        <button class="button" type="submit">搜尋</button>
      </form>
    </div>
    <p v-if="error" class="alert">{{ error }}</p>
    <p v-if="message" class="alert success">{{ message }}</p>
    <p v-if="loading">載入中...</p>
    <div v-else-if="products.length" class="grid product-grid">
      <article v-for="product in products" :key="product.productCode" class="card product-card">
        <img class="product-image" :src="product.imageUrl || '/vite.svg'" :alt="product.name" />
        <div><strong>{{ product.name }}</strong><div class="muted">{{ product.productCode }} · {{ product.category || '未分類' }}</div></div>
        <div class="price">NT$ {{ product.price.toLocaleString() }}</div>
        <div class="muted">庫存 {{ product.stockQuantity }}</div>
        <div class="actions">
          <RouterLink class="button secondary" :to="`/products/${product.productCode}`">查看</RouterLink>
          <button class="button" type="button" @click="add(product)">加入購物車</button>
        </div>
      </article>
    </div>
    <div v-else class="card">目前沒有符合條件的商品。</div>
  </section>
</template>
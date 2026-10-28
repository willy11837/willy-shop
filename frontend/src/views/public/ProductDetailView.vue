<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import * as notificationApi from '@/api/notifications'
import { getProduct } from '@/api/products'
import { useAuthStore } from '@/stores/auth'
import { useCartStore } from '@/stores/cart'
import { productStatuses, type Product } from '@/types/product'
import { errorMessage } from '@/utils/errors'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const cart = useCartStore()
const product = ref<Product | null>(null)
const quantity = ref(1)
const error = ref('')
const message = ref('')
const isSubscribed = ref(false)
const subscriptionLoading = ref(false)
const isPurchasable = computed(() => product.value?.status === productStatuses.available
  && product.value.stockQuantity > 0)
const isOutOfStock = computed(() => product.value?.status === productStatuses.outOfStock
  || (product.value?.stockQuantity ?? 0) <= 0)

onMounted(async () => {
  try {
    product.value = await getProduct(String(route.params.productCode))
    if (auth.isAuthenticated && isOutOfStock.value)
      isSubscribed.value = (await notificationApi.getBackInStockSubscription(product.value.productCode)).isSubscribed
  }
  catch (reason) { error.value = errorMessage(reason) }
})

async function add(): Promise<void> {
  if (!product.value || !isPurchasable.value) return
  try {
    await cart.add(product.value.productCode, quantity.value)
    message.value = '商品已加入購物車。'
    error.value = ''
  } catch (reason) { error.value = errorMessage(reason) }
}

async function toggleSubscription(): Promise<void> {
  if (!product.value) return
  if (!auth.isAuthenticated) {
    await router.push({ name: 'login', query: { redirect: route.fullPath } })
    return
  }

  subscriptionLoading.value = true
  try {
    if (isSubscribed.value) {
      await notificationApi.unsubscribeBackInStock(product.value.productCode)
      isSubscribed.value = false
      message.value = '已取消到貨通知。'
    } else {
      await notificationApi.subscribeBackInStock(product.value.productCode)
      isSubscribed.value = true
      message.value = '商品恢復有貨時會在通知中心提醒您。'
    }
    error.value = ''
  } catch (reason) { error.value = errorMessage(reason) }
  finally { subscriptionLoading.value = false }
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
      <p><span class="price-status">{{ product.priceStatusName }}</span></p>
      <div v-if="product.isOnSale" class="price-row">
        <span class="original-price">NT$ {{ product.price.toLocaleString() }}</span>
        <span class="price sale-price">NT$ {{ product.effectivePrice.toLocaleString() }}</span>
      </div>
      <p v-else class="price">NT$ {{ product.effectivePrice.toLocaleString() }}</p>
      <p>庫存：{{ product.stockQuantity }}</p>
      <p>狀態：{{ product.statusName }}</p>
      <div v-if="isPurchasable" class="actions">
        <input v-model.number="quantity" type="number" min="1" :max="product.stockQuantity" aria-label="數量" />
        <button class="button" type="button" @click="add">加入購物車</button>
        <RouterLink class="button secondary" to="/">返回列表</RouterLink>
      </div>
      <div v-else class="actions">
        <button
          v-if="isOutOfStock"
          class="button"
          type="button"
          :disabled="subscriptionLoading"
          @click="toggleSubscription"
        >
          {{ isSubscribed ? '取消到貨通知' : '有貨通知我' }}
        </button>
        <RouterLink class="button secondary" to="/">返回列表</RouterLink>
      </div>
    </div>
  </article>
</template>

<style scoped>.product-detail { display: grid; grid-template-columns: minmax(260px, 480px) 1fr; gap: 32px; } input { width: 90px; padding: 9px; } @media(max-width:720px){.product-detail{grid-template-columns:1fr}}</style>
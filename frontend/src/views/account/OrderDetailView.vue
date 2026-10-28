<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'

import { getOrder } from '@/api/orders'
import type { Order } from '@/types/order'
import { errorMessage } from '@/utils/errors'

const route = useRoute()
const order = ref<Order | null>(null)
const loading = ref(true)
const error = ref('')

onMounted(async () => {
  const orderId = Number(route.params.orderId)
  if (!Number.isInteger(orderId) || orderId <= 0) {
    error.value = '訂單編號無效。'
    loading.value = false
    return
  }
  try { order.value = await getOrder(orderId) }
  catch (reason) { error.value = errorMessage(reason) }
  finally { loading.value = false }
})
</script>

<template>
  <section><div class="page-header"><h1>訂單明細</h1><RouterLink class="button secondary" to="/orders">返回訂單</RouterLink></div>
    <p v-if="error" class="alert">{{ error }}</p><p v-if="loading">載入中...</p>
    <article v-else-if="order" class="card"><div class="page-header"><div><h2>訂單 #{{ order.id }}</h2><div class="muted">{{ new Date(order.createdAt).toLocaleString() }} · {{ order.status }}</div></div><strong>NT$ {{ order.totalAmount.toLocaleString() }}</strong></div>
      <div class="table-wrap"><table><thead><tr><th>商品</th><th>單價</th><th>數量</th><th>小計</th></tr></thead><tbody><tr v-for="item in order.items" :key="item.productCode"><td>{{ item.productName }}（{{ item.productCode }}）</td><td>NT$ {{ item.unitPrice.toLocaleString() }}</td><td>{{ item.quantity }}</td><td>NT$ {{ item.subtotal.toLocaleString() }}</td></tr></tbody></table></div>
    </article>
  </section>
</template>
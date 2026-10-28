<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'

import { getOrder } from '@/api/orders'
import type { Order } from '@/types/order'
import { errorMessage } from '@/utils/errors'

const route = useRoute()
const order = ref<Order | null>(null)
const error = ref('')

onMounted(async () => {
  const orderId = Number(route.params.orderId)
  if (!Number.isInteger(orderId) || orderId <= 0) { error.value = '訂單編號無效。'; return }
  try { order.value = await getOrder(orderId) }
  catch (reason) { error.value = errorMessage(reason) }
})
</script>

<template><section class="card"><h1>訂單已建立</h1><p v-if="error" class="alert">{{ error }}</p><template v-else-if="order"><p class="alert success">感謝您的訂購，訂單 #{{ order.id }} 已成功建立。</p><p>訂單金額：<strong>NT$ {{ order.totalAmount.toLocaleString() }}</strong></p><div class="actions"><RouterLink class="button" :to="`/orders/${order.id}`">查看訂單明細</RouterLink><RouterLink class="button secondary" to="/">繼續購物</RouterLink></div></template><p v-else>載入訂單中...</p></section></template>
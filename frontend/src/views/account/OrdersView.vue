<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { getOrders } from '@/api/orders'
import type { Order } from '@/types/order'
import { errorMessage } from '@/utils/errors'

const route = useRoute(); const orders = ref<Order[]>([]); const error = ref('')
onMounted(async () => { try { orders.value = await getOrders() } catch (reason) { error.value = errorMessage(reason) } })
</script>

<template><section><div class="page-header"><h1>我的訂單</h1></div><p v-if="route.query.created" class="alert success">訂單 #{{ route.query.created }} 已成功建立。</p><p v-if="error" class="alert">{{ error }}</p><div class="grid"><article v-for="order in orders" :key="order.id" class="card"><div class="page-header"><div><h2>訂單 #{{ order.id }}</h2><div class="muted">{{ new Date(order.createdAt).toLocaleString() }} · {{ order.status }}</div></div><strong>NT$ {{ order.totalAmount.toLocaleString() }}</strong></div><ul><li v-for="item in order.items" :key="item.productCode">{{ item.productName }} × {{ item.quantity }} — NT$ {{ item.subtotal.toLocaleString() }}</li></ul></article><div v-if="!orders.length" class="card">目前沒有訂單。</div></div></section></template>
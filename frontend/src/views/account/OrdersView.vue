<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { getOrders } from '@/api/orders'
import { orderStatusOptions, type Order, type OrderStatus } from '@/types/order'
import { errorMessage } from '@/utils/errors'

const orders = ref<Order[]>([]); const error = ref(''); const loading = ref(true)
const selectedStatus = ref<'' | OrderStatus>('')
const statusCounts = computed(() => orderStatusOptions.map((status) => ({ ...status, count: orders.value.filter((order) => order.status === status.value).length })))
const filteredOrders = computed(() => selectedStatus.value ? orders.value.filter((order) => order.status === selectedStatus.value) : orders.value)
onMounted(async () => { try { orders.value = await getOrders() } catch (reason) { error.value = errorMessage(reason) } finally { loading.value = false } })
</script>

<template><section><div class="page-header"><h1>我的訂單</h1></div><p v-if="error" class="alert">{{ error }}</p><p v-if="loading">載入中...</p><template v-else-if="!error"><ul class="status-summary" aria-label="各狀態訂單數量"><li v-for="status in statusCounts" :key="status.value" class="status-count"><span>{{ status.label }}</span><strong>{{ status.count }}</strong></li></ul><div class="card status-filter"><label class="form-row">訂單狀態<select v-model="selectedStatus"><option value="">全部</option><option v-for="status in orderStatusOptions" :key="status.value" :value="status.value">{{ status.label }}</option></select></label></div><div class="grid"><article v-for="order in filteredOrders" :key="order.id" class="card"><div class="page-header"><div><h2>訂單 #{{ order.id }}</h2><div class="muted">{{ new Date(order.createdAt).toLocaleString() }} · {{ order.statusName }}</div></div><strong>NT$ {{ order.totalAmount.toLocaleString() }}</strong></div><div class="actions"><RouterLink class="button secondary" :to="`/orders/${order.id}`">查看明細與操作</RouterLink></div></article><div v-if="!orders.length" class="card">目前沒有訂單。</div><div v-else-if="!filteredOrders.length" class="card">目前沒有符合此狀態的訂單。</div></div></template></section></template>

<style scoped>.status-summary{display:grid;grid-template-columns:repeat(auto-fit,minmax(130px,1fr));gap:12px;margin:0 0 24px;padding:0;list-style:none}.status-count{display:flex;align-items:center;justify-content:space-between;gap:12px;padding:16px;border:1px solid #dfe4ea;border-radius:12px;background:white}.status-count strong{font-size:1.4rem;color:#1769aa}.status-filter{max-width:320px;margin-bottom:24px}</style>
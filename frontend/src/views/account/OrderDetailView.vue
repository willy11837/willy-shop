<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { cancelOrder, completeOrder, getOrder, requestOrderReturn } from '@/api/orders'
import { orderStatuses, type Order } from '@/types/order'
import { errorMessage } from '@/utils/errors'

const route = useRoute(); const order = ref<Order | null>(null); const loading = ref(true); const submitting = ref(false); const error = ref(''); const message = ref(''); const returnReason = ref('')
const canCancel = computed(() => order.value?.status === orderStatuses.pending)
const canComplete = computed(() => order.value?.status === orderStatuses.shipped)
const canReturn = computed(() => order.value?.status === orderStatuses.completed)

async function load(): Promise<void> {
  const orderId = Number(route.params.orderId)
  if (!Number.isInteger(orderId) || orderId <= 0) { error.value = '訂單編號無效。'; loading.value = false; return }
  try { order.value = await getOrder(orderId) } catch (reason) { error.value = errorMessage(reason) } finally { loading.value = false }
}
async function execute(operation: () => Promise<Order>, successMessage: string): Promise<void> { if (submitting.value) return; submitting.value = true; error.value = ''; message.value = ''; try { order.value = await operation(); message.value = successMessage } catch (reason) { error.value = errorMessage(reason) } finally { submitting.value = false } }
async function cancel(): Promise<void> { if (!order.value || !confirm('確定取消此訂單？取消後商品庫存將回補。')) return; await execute(() => cancelOrder(order.value!.id), '訂單已取消。') }
async function complete(): Promise<void> { if (!order.value || !confirm('確認已收到商品並完成訂單？')) return; await execute(() => completeOrder(order.value!.id), '訂單已完成。') }
async function requestReturn(): Promise<void> { const reason = returnReason.value.trim(); if (!order.value || reason.length < 5) { error.value = '退貨原因至少需要 5 個字。'; return }; if (!confirm(`確定要申請退貨？\n\n退貨原因：${reason}`)) return; await execute(() => requestOrderReturn(order.value!.id, reason), '退貨申請已送出。'); returnReason.value = '' }
onMounted(load)
</script>

<template><section><div class="page-header"><h1>訂單明細</h1><RouterLink class="button secondary" to="/orders">返回訂單</RouterLink></div><p v-if="error" class="alert">{{ error }}</p><p v-if="message" class="alert success">{{ message }}</p><p v-if="loading">載入中...</p>
  <article v-else-if="order" class="card"><div class="page-header"><div><h2>訂單 #{{ order.id }}</h2><div class="muted">{{ new Date(order.createdAt).toLocaleString() }} · {{ order.statusName }}</div></div><strong>NT$ {{ order.totalAmount.toLocaleString() }}</strong></div>
    <div class="table-wrap"><table><thead><tr><th>商品</th><th>單價</th><th>數量</th><th>小計</th></tr></thead><tbody><tr v-for="item in order.items" :key="item.productCode"><td>{{ item.productName }}（{{ item.productCode }}）</td><td>NT$ {{ item.unitPrice.toLocaleString() }}</td><td>{{ item.quantity }}</td><td>NT$ {{ item.subtotal.toLocaleString() }}</td></tr></tbody></table></div>
    <dl class="status-details"><template v-if="order.shippedAt"><dt>寄送時間</dt><dd>{{ new Date(order.shippedAt).toLocaleString() }}</dd></template><template v-if="order.completedAt"><dt>完成時間</dt><dd>{{ new Date(order.completedAt).toLocaleString() }}</dd></template><template v-if="order.cancelledAt"><dt>取消時間</dt><dd>{{ new Date(order.cancelledAt).toLocaleString() }}</dd></template><template v-if="order.returnReason"><dt>退貨原因</dt><dd>{{ order.returnReason }}</dd></template><template v-if="order.returnReviewNote"><dt>退貨審核備註</dt><dd>{{ order.returnReviewNote }}</dd></template><template v-if="order.refundCompletedAt"><dt>退款完成時間</dt><dd>{{ new Date(order.refundCompletedAt).toLocaleString() }}</dd><dt>退款金額</dt><dd>NT$ {{ (order.refundAmount ?? 0).toLocaleString() }}</dd></template><template v-if="order.refundNote"><dt>退款備註</dt><dd>{{ order.refundNote }}</dd></template></dl>
    <div class="actions operation-area"><button v-if="canCancel" class="button danger" :disabled="submitting" @click="cancel">取消訂單</button><button v-if="canComplete" class="button" :disabled="submitting" @click="complete">確認完成訂單</button></div>
    <form v-if="canReturn" class="return-form" @submit.prevent="requestReturn"><label class="form-row">退貨原因<textarea v-model.trim="returnReason" rows="4" maxlength="500" required /></label><button class="button danger" :disabled="submitting">申請退貨</button></form>
    <p v-if="order.status === orderStatuses.returnRequested" class="alert">退貨申請審核中。</p><p v-if="order.status === orderStatuses.returnApproved" class="alert">退貨已核准，等待管理人員完成退款紀錄。</p>
  </article>
</section></template>

<style scoped>.status-details{display:grid;grid-template-columns:max-content 1fr;gap:8px 16px;margin-top:24px}.status-details dt{font-weight:700}.status-details dd{margin:0}.operation-area,.return-form{margin-top:24px}.return-form{display:grid;gap:12px}.return-form textarea{width:100%;padding:10px 12px;border:1px solid #cbd2da;border-radius:8px}</style>
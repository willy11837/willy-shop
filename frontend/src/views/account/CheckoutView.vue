<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { checkout } from '@/api/orders'
import { useCartStore } from '@/stores/cart'
import { errorMessage } from '@/utils/errors'

const cart = useCartStore(); const router = useRouter(); const error = ref(''); const submitting = ref(false)
onMounted(cart.load)
async function submit(): Promise<void> {
  submitting.value = true; error.value = ''
  try { const order = await checkout(); await cart.load(); await router.push({ name: 'orders', query: { created: order.id } }) }
  catch (reason) { error.value = errorMessage(reason) }
  finally { submitting.value = false }
}
</script>

<template><section><div class="page-header"><h1>確認結帳</h1></div><p v-if="error" class="alert">{{ error }}</p><div v-if="cart.cart.items.length" class="card"><ul><li v-for="item in cart.cart.items" :key="item.productCode">{{ item.name }} × {{ item.quantity }} — NT$ {{ item.subtotal.toLocaleString() }}</li></ul><div class="summary"><strong>總計：NT$ {{ cart.cart.totalAmount.toLocaleString() }}</strong></div><div class="actions" style="justify-content:flex-end;margin-top:20px"><RouterLink class="button secondary" to="/cart">返回購物車</RouterLink><button class="button" :disabled="submitting" @click="submit">{{ submitting ? '處理中...' : '確認下單' }}</button></div></div><div v-else class="card">購物車是空的。</div></section></template>
<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useCartStore } from '@/stores/cart'
import { useAuthStore } from '@/stores/auth'
import { errorMessage } from '@/utils/errors'

const cart = useCartStore()
const auth = useAuthStore()
const error = ref('')

async function update(code: string, quantity: number): Promise<void> {
  try { await cart.update(code, quantity) }
  catch (reason) { error.value = errorMessage(reason); await cart.load() }
}

async function remove(code: string): Promise<void> {
  try { await cart.remove(code) }
  catch (reason) { error.value = errorMessage(reason) }
}

onMounted(cart.load)
</script>

<template>
  <section>
    <div class="page-header"><h1>購物車</h1><RouterLink class="button secondary" to="/">繼續購物</RouterLink></div>
    <p v-if="error" class="alert">{{ error }}</p>
    <div v-if="cart.loading">載入中...</div>
    <div v-else-if="cart.cart.items.length" class="card table-wrap">
      <table>
        <thead><tr><th>商品</th><th>單價</th><th>數量</th><th>小計</th><th></th></tr></thead>
        <tbody>
          <tr v-for="item in cart.cart.items" :key="item.productCode">
            <td>{{ item.name }}<div class="muted">{{ item.productCode }}</div></td>
            <td>NT$ {{ item.unitPrice.toLocaleString() }}</td>
            <td><input :value="item.quantity" type="number" min="1" :max="item.stockQuantity" @change="update(item.productCode, Number(($event.target as HTMLInputElement).value))" /></td>
            <td>NT$ {{ item.subtotal.toLocaleString() }}</td>
            <td><button class="button danger" type="button" @click="remove(item.productCode)">移除</button></td>
          </tr>
        </tbody>
      </table>
      <div class="summary"><strong>總計：NT$ {{ cart.cart.totalAmount.toLocaleString() }}</strong></div>
      <div class="actions" style="justify-content:flex-end;margin-top:16px">
        <RouterLink v-if="auth.isAuthenticated" class="button" to="/checkout">前往結帳</RouterLink>
        <RouterLink v-else class="button" :to="{ name: 'login', query: { redirect: '/checkout' } }">登入後結帳</RouterLink>
      </div>
    </div>
    <div v-else class="card">購物車目前是空的。</div>
  </section>
</template>

<style scoped>input{width:80px;padding:8px}</style>
<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useCartStore } from '@/stores/cart'
import { errorMessage } from '@/utils/errors'

const auth = useAuthStore(); const cart = useCartStore(); const route = useRoute(); const router = useRouter()
const form = reactive({ account: '', password: '' })
const error = ref(''); const submitting = ref(false)
async function submit(): Promise<void> {
  submitting.value = true; error.value = ''
  try {
    await auth.login(form); await cart.load()
    const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : '/'
    await router.push(redirect)
  } catch (reason) { error.value = errorMessage(reason) }
  finally { submitting.value = false }
}
</script>

<template><section class="card auth-card"><h1>會員登入</h1><p v-if="error" class="alert">{{ error }}</p><form class="form-grid" @submit.prevent="submit"><label class="form-row">帳號<input v-model.trim="form.account" autocomplete="username" required /></label><label class="form-row">密碼<input v-model="form.password" type="password" autocomplete="current-password" required /></label><button class="button" :disabled="submitting">{{ submitting ? '登入中...' : '登入' }}</button><RouterLink to="/register">尚未註冊？建立帳號</RouterLink></form></section></template>
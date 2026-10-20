<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { errorMessage } from '@/utils/errors'

const auth = useAuthStore(); const router = useRouter()
const form = reactive({ account: '', displayName: '', email: '', password: '', confirmPassword: '' })
const error = ref(''); const submitting = ref(false)
async function submit(): Promise<void> {
  if (form.password !== form.confirmPassword) { error.value = '密碼與確認密碼不相符。'; return }
  submitting.value = true; error.value = ''
  try { await auth.register(form); await router.push('/') }
  catch (reason) { error.value = errorMessage(reason) }
  finally { submitting.value = false }
}
</script>

<template><section class="card auth-card"><h1>會員註冊</h1><p v-if="error" class="alert">{{ error }}</p><form class="form-grid" @submit.prevent="submit"><label class="form-row">帳號<input v-model.trim="form.account" minlength="3" maxlength="30" required /></label><label class="form-row">顯示名稱<input v-model.trim="form.displayName" maxlength="50" required /></label><label class="form-row">Email<input v-model.trim="form.email" type="email" required /></label><label class="form-row">密碼<input v-model="form.password" type="password" minlength="8" required /></label><label class="form-row">確認密碼<input v-model="form.confirmPassword" type="password" minlength="8" required /></label><button class="button" :disabled="submitting">{{ submitting ? '建立中...' : '建立帳號' }}</button></form></section></template>
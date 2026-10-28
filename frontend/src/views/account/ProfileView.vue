<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'

import { changePassword, getProfile, updateProfile } from '@/api/profile'
import { useAuthStore } from '@/stores/auth'
import type { Profile } from '@/types/profile'
import { errorMessage } from '@/utils/errors'

const auth = useAuthStore()
const profile = ref<Profile | null>(null)
const form = reactive({ displayName: '', email: '' })
const passwordForm = reactive({ currentPassword: '', newPassword: '', confirmPassword: '' })
const loading = ref(true)
const submitting = ref(false)
const changingPassword = ref(false)
const error = ref('')
const message = ref('')
const passwordError = ref('')
const passwordMessage = ref('')

async function load(): Promise<void> {
  loading.value = true
  error.value = ''
  try {
    profile.value = await getProfile()
    form.displayName = profile.value.displayName
    form.email = profile.value.email
  } catch (reason) { error.value = errorMessage(reason) }
  finally { loading.value = false }
}

async function submit(): Promise<void> {
  submitting.value = true
  error.value = ''
  message.value = ''
  try {
    profile.value = await updateProfile(form)
    await auth.reloadUser()
    message.value = '個人資料已更新。'
  } catch (reason) { error.value = errorMessage(reason) }
  finally { submitting.value = false }
}

async function submitPassword(): Promise<void> {
  passwordError.value = ''
  passwordMessage.value = ''
  if (passwordForm.newPassword !== passwordForm.confirmPassword) {
    passwordError.value = '新密碼與確認密碼不相符。'
    return
  }

  changingPassword.value = true
  try {
    await changePassword(passwordForm)
    passwordForm.currentPassword = ''
    passwordForm.newPassword = ''
    passwordForm.confirmPassword = ''
    passwordMessage.value = '密碼已修改。'
  } catch (reason) { passwordError.value = errorMessage(reason) }
  finally { changingPassword.value = false }
}

onMounted(load)
</script>

<template>
  <section>
    <div class="page-header"><div><h1>個人資料</h1><p class="muted">管理顯示名稱、電子郵件與登入密碼。</p></div></div>
    <p v-if="error" class="alert">{{ error }}</p><p v-if="message" class="alert success">{{ message }}</p>
    <p v-if="loading">載入中...</p>
    <form v-else-if="profile" class="card form-grid" @submit.prevent="submit">
      <h2>基本資料</h2>
      <label class="form-row">帳號<input :value="profile.account" disabled /></label>
      <label class="form-row">顯示名稱<input v-model.trim="form.displayName" maxlength="50" required /></label>
      <label class="form-row">Email<input v-model.trim="form.email" type="email" maxlength="256" required /></label>
      <div><strong>角色：</strong>{{ profile.roles.join('、') }}</div>
      <div class="muted">建立時間：{{ new Date(profile.createdAt).toLocaleString() }}</div>
      <div class="actions"><button class="button" :disabled="submitting">{{ submitting ? '儲存中...' : '儲存變更' }}</button></div>
    </form>
    <form v-if="profile" class="card form-grid profile-section" @submit.prevent="submitPassword">
      <div>
        <h2>修改密碼</h2>
        <p class="muted">新密碼至少 8 個字元，並須包含英文大小寫與數字。</p>
      </div>
      <p v-if="passwordError" class="alert">{{ passwordError }}</p>
      <p v-if="passwordMessage" class="alert success">{{ passwordMessage }}</p>
      <label class="form-row">目前密碼<input v-model="passwordForm.currentPassword" type="password" autocomplete="current-password" required /></label>
      <label class="form-row">新密碼<input v-model="passwordForm.newPassword" type="password" autocomplete="new-password" minlength="8" maxlength="100" required /></label>
      <label class="form-row">確認新密碼<input v-model="passwordForm.confirmPassword" type="password" autocomplete="new-password" minlength="8" maxlength="100" required /></label>
      <div class="actions"><button class="button" :disabled="changingPassword">{{ changingPassword ? '修改中...' : '修改密碼' }}</button></div>
    </form>
  </section>
</template>

<style scoped>
.profile-section { margin-top: 24px; }
</style>
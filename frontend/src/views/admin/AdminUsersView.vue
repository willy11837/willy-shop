<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { createAdminUser, deleteAdminUser, getAdminUsers, getAssignableRoles, updateAdminUser } from '@/api/adminUsers'
import type { AdminUser } from '@/types/admin'
import { useAuthStore } from '@/stores/auth'
import { roles as systemRoles } from '@/types/auth'
import { errorMessage } from '@/utils/errors'

const auth = useAuthStore(); const users = ref<AdminUser[]>([]); const roles = ref<string[]>([]); const error = ref(''); const editingId = ref<string | null>(null)
const blank = () => ({ account: '', displayName: '', email: '', password: '', newPassword: '', role: 'Normal' })
const form = reactive(blank())
async function load(): Promise<void> { try { [users.value, roles.value] = await Promise.all([getAdminUsers(), getAssignableRoles()]) } catch (reason) { error.value = errorMessage(reason) } }
function reset(): void { Object.assign(form, blank()); editingId.value = null }
function edit(user: AdminUser): void { editingId.value = user.id; Object.assign(form, { account: user.account, displayName: user.displayName ?? '', email: user.email ?? '', password: '', newPassword: '', role: user.role }); window.scrollTo({ top: 0, behavior: 'smooth' }) }
async function save(): Promise<void> { try { if (editingId.value) await updateAdminUser(editingId.value, { displayName: form.displayName, email: form.email, newPassword: form.newPassword || undefined, role: form.role }); else await createAdminUser({ account: form.account, displayName: form.displayName, email: form.email, password: form.password, role: form.role }); reset(); await load() } catch (reason) { error.value = errorMessage(reason) } }
async function remove(user: AdminUser): Promise<void> { if (!confirm(`確定刪除帳號「${user.account}」？`)) return; try { await deleteAdminUser(user.id); await load() } catch (reason) { error.value = errorMessage(reason) } }
onMounted(load)
</script>

<template><section><div class="page-header"><h1>帳號管理</h1><RouterLink class="button secondary" to="/admin">管理首頁</RouterLink></div><p v-if="error" class="alert">{{ error }}</p><form class="card form-grid" @submit.prevent="save"><h2>{{ editingId ? '編輯帳號' : '新增帳號' }}</h2><div class="admin-form"><label class="form-row">帳號<input v-model.trim="form.account" :disabled="Boolean(editingId)" required /></label><label class="form-row">顯示名稱<input v-model.trim="form.displayName" required /></label><label class="form-row">Email<input v-model.trim="form.email" type="email" required /></label><label v-if="!editingId" class="form-row">密碼<input v-model="form.password" type="password" required /></label><label v-else class="form-row">新密碼（選填）<input v-model="form.newPassword" type="password" /></label><label class="form-row">角色<select v-model="form.role"><option v-for="role in roles" :key="role">{{ role }}</option></select></label></div><div class="actions"><button class="button">{{ editingId ? '儲存修改' : '新增帳號' }}</button><button v-if="editingId" class="button secondary" type="button" @click="reset">取消</button></div></form><div class="card table-wrap" style="margin-top:24px"><table><thead><tr><th>帳號</th><th>名稱</th><th>Email</th><th>角色</th><th></th></tr></thead><tbody><tr v-for="user in users" :key="user.id"><td>{{ user.account }}</td><td>{{ user.displayName }}</td><td>{{ user.email }}</td><td>{{ user.role }}</td><td class="actions"><button class="button secondary" @click="edit(user)">編輯</button><button v-if="auth.user?.roles.includes(systemRoles.system)" class="button danger" @click="remove(user)">刪除</button></td></tr></tbody></table></div></section></template>

<style scoped>.admin-form{display:grid;grid-template-columns:repeat(auto-fit,minmax(210px,1fr));gap:16px}</style>
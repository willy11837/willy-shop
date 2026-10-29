<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { createAdminUser, deleteAdminUser, getAdminUsers, getAssignableRoles, updateAdminUser } from '@/api/adminUsers'
import type { AdminUser } from '@/types/admin'
import { useAuthStore } from '@/stores/auth'
import { roles as systemRoles } from '@/types/auth'
import { errorMessage } from '@/utils/errors'

const auth = useAuthStore(); const users = ref<AdminUser[]>([]); const roles = ref<string[]>([]); const error = ref(''); const loading = ref(false); const editingId = ref<string | null>(null)
const query = reactive({ keyword: '', page: 1, pageSize: 20 }); const totalCount = ref(0); const pageCount = computed(() => Math.max(1, Math.ceil(totalCount.value / query.pageSize)))
const blank = () => ({ account: '', displayName: '', email: '', password: '', newPassword: '', role: 'Normal' }); const form = reactive(blank())
async function load(): Promise<void> { loading.value = true; error.value = ''; try { const [result, assignableRoles] = await Promise.all([getAdminUsers({ keyword: query.keyword || undefined, page: query.page, pageSize: query.pageSize }), getAssignableRoles()]); users.value = result.items; totalCount.value = result.totalCount; roles.value = assignableRoles } catch (reason) { error.value = errorMessage(reason) } finally { loading.value = false } }
async function search(): Promise<void> { query.page = 1; await load() }
async function clearSearch(): Promise<void> { query.keyword = ''; query.page = 1; await load() }
async function changePage(page: number): Promise<void> { query.page = Math.min(Math.max(page, 1), pageCount.value); await load() }
function reset(): void { Object.assign(form, blank()); editingId.value = null }
function edit(user: AdminUser): void { editingId.value = user.id; Object.assign(form, { account: user.account, displayName: user.displayName ?? '', email: user.email ?? '', password: '', newPassword: '', role: user.role }); window.scrollTo({ top: 0, behavior: 'smooth' }) }
async function save(): Promise<void> { try { if (editingId.value) await updateAdminUser(editingId.value, { displayName: form.displayName, email: form.email, newPassword: form.newPassword || undefined, role: form.role }); else await createAdminUser({ account: form.account, displayName: form.displayName, email: form.email, password: form.password, role: form.role }); reset(); await load() } catch (reason) { error.value = errorMessage(reason) } }
async function remove(user: AdminUser): Promise<void> { if (!confirm(`確定刪除帳號「${user.account}」？`)) return; try { await deleteAdminUser(user.id); await load() } catch (reason) { error.value = errorMessage(reason) } }
onMounted(load)
</script>

<template><section><div class="page-header"><h1>帳號管理</h1><RouterLink class="button secondary" to="/admin">管理首頁</RouterLink></div><p v-if="error" class="alert">{{ error }}</p>
  <form class="card form-grid" @submit.prevent="save"><h2>{{ editingId ? '編輯帳號' : '新增帳號' }}</h2><div class="admin-form"><label class="form-row">帳號<input v-model.trim="form.account" :disabled="Boolean(editingId)" required /></label><label class="form-row">顯示名稱<input v-model.trim="form.displayName" required /></label><label class="form-row">Email<input v-model.trim="form.email" type="email" required /></label><label v-if="!editingId" class="form-row">密碼<input v-model="form.password" type="password" required /></label><label v-else class="form-row">新密碼（選填）<input v-model="form.newPassword" type="password" /></label><label class="form-row">角色<select v-model="form.role"><option v-for="role in roles" :key="role">{{ role }}</option></select></label></div><div class="actions"><button class="button">{{ editingId ? '儲存修改' : '新增帳號' }}</button><button v-if="editingId" class="button secondary" type="button" @click="reset">取消</button></div></form>
  <form class="card search-card" @submit.prevent="search"><h2>帳號查詢</h2><div class="actions"><input v-model.trim="query.keyword" aria-label="搜尋帳號、名稱或 Email" placeholder="帳號、顯示名稱或 Email" /><button class="button" :disabled="loading">搜尋</button><button class="button secondary" type="button" :disabled="loading" @click="clearSearch">清除</button></div></form>
  <p v-if="loading">載入中...</p><div v-else class="card table-wrap user-table"><table><thead><tr><th>帳號</th><th>名稱</th><th>Email</th><th>角色</th><th></th></tr></thead><tbody><tr v-for="user in users" :key="user.id"><td>{{ user.account }}</td><td>{{ user.displayName }}</td><td>{{ user.email }}</td><td>{{ user.role }}</td><td class="actions"><button class="button secondary" @click="edit(user)">編輯</button><button v-if="auth.user?.roles.includes(systemRoles.system)" class="button danger" @click="remove(user)">刪除</button></td></tr><tr v-if="!users.length"><td colspan="5">目前沒有符合條件的帳號。</td></tr></tbody></table></div>
  <nav v-if="totalCount" class="actions pagination"><button class="button secondary" :disabled="query.page <= 1 || loading" @click="changePage(query.page - 1)">上一頁</button><span>第 {{ query.page }} / {{ pageCount }} 頁，共 {{ totalCount }} 筆</span><button class="button secondary" :disabled="query.page >= pageCount || loading" @click="changePage(query.page + 1)">下一頁</button></nav>
</section></template>

<style scoped>.admin-form{display:grid;grid-template-columns:repeat(auto-fit,minmax(210px,1fr));gap:16px}.search-card,.user-table{margin-top:24px}.search-card input{min-width:min(420px,100%);padding:10px 12px;border:1px solid #cbd2da;border-radius:8px}.pagination{justify-content:center;margin-top:20px}</style>
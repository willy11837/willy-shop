<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { createProduct, deleteProduct, getAdminProducts, updateProduct } from '@/api/products'
import type { Product, SaveProductRequest } from '@/types/product'
import { errorMessage } from '@/utils/errors'

const products = ref<Product[]>([]); const error = ref(''); const editingCode = ref<string | null>(null)
const blank = (): SaveProductRequest => ({ productCode: '', name: '', price: 0, category: '', stockQuantity: 0, status: 0, imagePath: '' })
const form = reactive<SaveProductRequest>(blank())
async function load(): Promise<void> { try { products.value = await getAdminProducts() } catch (reason) { error.value = errorMessage(reason) } }
function reset(): void { Object.assign(form, blank()); editingCode.value = null }
function edit(product: Product): void { editingCode.value = product.productCode; Object.assign(form, { productCode: product.productCode, name: product.name, price: product.price, category: product.category, stockQuantity: product.stockQuantity, status: product.status, imagePath: product.imageUrl ? `~${product.imageUrl}` : '' }); window.scrollTo({ top: 0, behavior: 'smooth' }) }
async function save(): Promise<void> { try { if (editingCode.value) await updateProduct(editingCode.value, form); else await createProduct(form); reset(); await load() } catch (reason) { error.value = errorMessage(reason) } }
async function remove(product: Product): Promise<void> { if (!confirm(`確定刪除「${product.name}」？`)) return; try { await deleteProduct(product.productCode); await load() } catch (reason) { error.value = errorMessage(reason) } }
onMounted(load)
</script>

<template><section><div class="page-header"><h1>商品管理</h1><RouterLink class="button secondary" to="/admin">管理首頁</RouterLink></div><p v-if="error" class="alert">{{ error }}</p><form class="card form-grid" @submit.prevent="save"><h2>{{ editingCode ? '編輯商品' : '新增商品' }}</h2><div class="admin-form"><label class="form-row">商品代碼<input v-model.trim="form.productCode" :disabled="Boolean(editingCode)" required /></label><label class="form-row">名稱<input v-model.trim="form.name" required /></label><label class="form-row">分類<input v-model.trim="form.category" /></label><label class="form-row">價格<input v-model.number="form.price" type="number" min="0" step="0.01" required /></label><label class="form-row">庫存<input v-model.number="form.stockQuantity" type="number" min="0" required /></label><label class="form-row">狀態<select v-model.number="form.status"><option :value="0">可銷售</option><option :value="1">凍結</option><option :value="2">數量不足</option></select></label><label class="form-row">圖片路徑<input v-model.trim="form.imagePath" placeholder="~/images/example.jpg" /></label></div><div class="actions"><button class="button">{{ editingCode ? '儲存修改' : '新增商品' }}</button><button v-if="editingCode" class="button secondary" type="button" @click="reset">取消</button></div></form><div class="card table-wrap" style="margin-top:24px"><table><thead><tr><th>代碼</th><th>名稱</th><th>價格</th><th>庫存</th><th>狀態</th><th></th></tr></thead><tbody><tr v-for="product in products" :key="product.productCode"><td>{{ product.productCode }}</td><td>{{ product.name }}</td><td>NT$ {{ product.price.toLocaleString() }}</td><td>{{ product.stockQuantity }}</td><td>{{ product.statusName }}</td><td class="actions"><button class="button secondary" @click="edit(product)">編輯</button><button class="button danger" @click="remove(product)">刪除</button></td></tr></tbody></table></div></section></template>

<style scoped>.admin-form{display:grid;grid-template-columns:repeat(auto-fit,minmax(210px,1fr));gap:16px}</style>
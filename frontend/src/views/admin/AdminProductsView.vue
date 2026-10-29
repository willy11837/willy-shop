<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { createProduct, deleteProduct, getAdminProducts, updateProduct } from '@/api/products'
import { productStatuses, type Product, type SaveProductRequest } from '@/types/product'
import { errorMessage } from '@/utils/errors'

const products = ref<Product[]>([])
const totalCount = ref(0)
const loading = ref(false)
const error = ref('')
const editingCode = ref<string | null>(null)
const saving = ref(false)
const query = reactive({ productCode: '', productName: '', category: '', status: '', modifiedFrom: '', modifiedTo: '', sortBy: 'productCode' as const, sortDirection: 'asc' as const, page: 1, pageSize: 20 })
const pageCount = computed(() => Math.max(1, Math.ceil(totalCount.value / query.pageSize)))

const blank = (): SaveProductRequest => ({ productCode: '', name: '', price: 0, isOnSale: false, salePrice: null, category: '', stockQuantity: 0, status: productStatuses.outOfStock, imagePath: '' })
const form = reactive<SaveProductRequest>(blank())

watch(() => form.stockQuantity, (quantity, previousQuantity) => {
  if (quantity <= 0) form.status = productStatuses.outOfStock
  else if (previousQuantity <= 0 && form.status === productStatuses.outOfStock) form.status = productStatuses.available
})

async function load(): Promise<void> {
  if (query.modifiedFrom && query.modifiedTo && query.modifiedFrom > query.modifiedTo) { error.value = '修改迄日不得早於修改起日。'; return }
  loading.value = true; error.value = ''
  try {
    const result = await getAdminProducts({
      productCode: query.productCode || undefined, productName: query.productName || undefined,
      category: query.category || undefined, status: query.status === '' ? undefined : Number(query.status),
      modifiedFrom: query.modifiedFrom || undefined, modifiedTo: query.modifiedTo ? `${query.modifiedTo}T23:59:59` : undefined,
      sortBy: query.sortBy, sortDirection: query.sortDirection, page: query.page, pageSize: query.pageSize,
    })
    products.value = result.items; totalCount.value = result.totalCount
  } catch (reason) { error.value = errorMessage(reason) }
  finally { loading.value = false }
}

async function search(): Promise<void> { query.page = 1; await load() }
async function clearSearch(): Promise<void> { Object.assign(query, { productCode: '', productName: '', category: '', status: '', modifiedFrom: '', modifiedTo: '', sortBy: 'productCode', sortDirection: 'asc', page: 1 }); await load() }
async function changePage(page: number): Promise<void> { query.page = Math.min(Math.max(page, 1), pageCount.value); await load() }
function reset(): void { Object.assign(form, blank()); editingCode.value = null }
function edit(product: Product): void { editingCode.value = product.productCode; Object.assign(form, { productCode: product.productCode, name: product.name, price: product.price, isOnSale: product.isOnSale, salePrice: product.salePrice, category: product.category, stockQuantity: product.stockQuantity, status: product.status, imagePath: product.imageUrl ? `~${product.imageUrl}` : '' }); window.scrollTo({ top: 0, behavior: 'smooth' }) }
async function save(): Promise<void> {
  if (saving.value) return
  error.value = ''
  if (form.isOnSale && (form.salePrice === null || form.salePrice <= 0 || form.salePrice >= form.price)) { error.value = '特價價格必須大於 0 且低於原價。'; return }
  saving.value = true
  try { if (editingCode.value) await updateProduct(editingCode.value, form); else await createProduct(form); reset(); await load() }
  catch (reason) { error.value = errorMessage(reason) }
  finally { saving.value = false }
}
async function remove(product: Product): Promise<void> { if (!confirm(`確定刪除「${product.name}」？`)) return; try { await deleteProduct(product.productCode); await load() } catch (reason) { error.value = errorMessage(reason) } }
onMounted(load)
</script>

<template>
  <section>
    <div class="page-header"><h1>商品管理</h1><RouterLink class="button secondary" to="/admin">管理首頁</RouterLink></div>
    <p v-if="error" class="alert">{{ error }}</p>
    <form class="card form-grid" @submit.prevent="save">
      <h2>{{ editingCode ? '編輯商品' : '新增商品' }}</h2>
      <div class="admin-form">
        <label class="form-row">商品代碼<input v-model.trim="form.productCode" :disabled="Boolean(editingCode)" required /></label>
        <label class="form-row">名稱<input v-model.trim="form.name" required /></label>
        <label class="form-row">分類<input v-model.trim="form.category" /></label>
        <label class="form-row">原價<input v-model.number="form.price" type="number" min="0" step="0.01" required /></label>
        <label class="form-row">價格狀態<select v-model="form.isOnSale"><option :value="false">原價</option><option :value="true">特價中</option></select></label>
        <label class="form-row">特價價格<input v-model.number="form.salePrice" type="number" min="0.01" step="0.01" :required="form.isOnSale" :disabled="!form.isOnSale" /></label>
        <label class="form-row">庫存<input v-model.number="form.stockQuantity" type="number" min="0" required /></label>
        <label class="form-row">商品狀態<select v-model.number="form.status" :disabled="form.stockQuantity <= 0"><option :value="0">可銷售</option><option :value="1">凍結</option><option :value="2">數量不足</option></select></label>
        <label class="form-row">圖片路徑<input v-model.trim="form.imagePath" placeholder="~/images/example.webp" /></label>
      </div>
      <div class="actions"><button class="button" :disabled="saving">{{ saving ? '儲存中...' : editingCode ? '儲存修改' : '新增商品' }}</button><button v-if="editingCode" class="button secondary" type="button" :disabled="saving" @click="reset">取消</button></div>
    </form>
    <form class="card search-card" @submit.prevent="search">
      <h2>商品查詢</h2>
      <div class="admin-form">
        <label class="form-row">商品代碼<input v-model.trim="query.productCode" /></label><label class="form-row">商品名稱<input v-model.trim="query.productName" /></label><label class="form-row">分類<input v-model.trim="query.category" /></label>
        <label class="form-row">狀態<select v-model="query.status"><option value="">全部</option><option value="0">可銷售</option><option value="1">凍結</option><option value="2">數量不足</option></select></label>
        <label class="form-row">修改起日<input v-model="query.modifiedFrom" type="date" /></label><label class="form-row">修改迄日<input v-model="query.modifiedTo" type="date" /></label>
        <label class="form-row">排序<select v-model="query.sortBy"><option value="productCode">商品代碼</option><option value="productName">商品名稱</option><option value="price">價格</option><option value="modifiedAt">修改時間</option></select></label>
        <label class="form-row">方向<select v-model="query.sortDirection"><option value="asc">升冪</option><option value="desc">降冪</option></select></label>
      </div>
      <div class="actions"><button class="button" :disabled="loading">搜尋</button><button class="button secondary" type="button" :disabled="loading" @click="clearSearch">清除</button></div>
    </form>
    <p v-if="loading">載入中...</p>
    <div v-else class="card table-wrap product-table"><table><thead><tr><th>代碼</th><th>名稱</th><th>原價</th><th>目前售價</th><th>庫存</th><th>狀態</th><th>修改時間</th><th></th></tr></thead><tbody><tr v-for="product in products" :key="product.productCode"><td>{{ product.productCode }}</td><td>{{ product.name }}</td><td>NT$ {{ product.price.toLocaleString() }}</td><td>NT$ {{ product.effectivePrice.toLocaleString() }}</td><td>{{ product.stockQuantity }}</td><td>{{ product.statusName }}</td><td>{{ product.modifiedAt ? new Date(product.modifiedAt).toLocaleString() : '-' }}</td><td class="actions"><button class="button secondary" type="button" @click="edit(product)">編輯</button><button class="button danger" type="button" @click="remove(product)">刪除</button></td></tr><tr v-if="!products.length"><td colspan="8">目前沒有符合條件的商品。</td></tr></tbody></table></div>
    <nav v-if="totalCount" class="actions pagination"><button class="button secondary" :disabled="query.page <= 1 || loading" @click="changePage(query.page - 1)">上一頁</button><span>第 {{ query.page }} / {{ pageCount }} 頁，共 {{ totalCount }} 筆</span><button class="button secondary" :disabled="query.page >= pageCount || loading" @click="changePage(query.page + 1)">下一頁</button></nav>
  </section>
</template>

<style scoped>.admin-form{display:grid;grid-template-columns:repeat(auto-fit,minmax(190px,1fr));gap:16px}.search-card,.product-table{margin-top:24px}.pagination{justify-content:center;margin-top:20px}</style>
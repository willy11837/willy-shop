<script setup lang="ts">
import { onMounted, reactive, ref, watch } from 'vue'
import { createProduct, deleteProduct, getAdminProducts, updateProduct } from '@/api/products'
import { productStatuses, type Product, type SaveProductRequest } from '@/types/product'
import { errorMessage } from '@/utils/errors'

const products = ref<Product[]>([])
const error = ref('')
const editingCode = ref<string | null>(null)
const saving = ref(false)

const blank = (): SaveProductRequest => ({
  productCode: '',
  name: '',
  price: 0,
  isOnSale: false,
  salePrice: null,
  category: '',
  stockQuantity: 0,
  status: productStatuses.outOfStock,
  imagePath: '',
})
const form = reactive<SaveProductRequest>(blank())

watch(() => form.stockQuantity, (quantity, previousQuantity) => {
  if (quantity <= 0) {
    form.status = productStatuses.outOfStock
  } else if (previousQuantity <= 0 && form.status === productStatuses.outOfStock) {
    form.status = productStatuses.available
  }
})

async function load(): Promise<void> {
  error.value = ''
  try { products.value = await getAdminProducts() }
  catch (reason) { error.value = errorMessage(reason) }
}

function reset(): void {
  Object.assign(form, blank())
  editingCode.value = null
}

function edit(product: Product): void {
  editingCode.value = product.productCode
  Object.assign(form, {
    productCode: product.productCode,
    name: product.name,
    price: product.price,
    isOnSale: product.isOnSale,
    salePrice: product.salePrice,
    category: product.category,
    stockQuantity: product.stockQuantity,
    status: product.status,
    imagePath: product.imageUrl ? `~${product.imageUrl}` : '',
  })
  window.scrollTo({ top: 0, behavior: 'smooth' })
}

async function save(): Promise<void> {
  if (saving.value) return
  error.value = ''
  if (form.isOnSale && (form.salePrice === null || form.salePrice <= 0 || form.salePrice >= form.price)) {
    error.value = '特價價格必須大於 0 且低於原價。'
    return
  }

  saving.value = true
  try {
    if (editingCode.value) await updateProduct(editingCode.value, form)
    else await createProduct(form)
    reset()
    await load()
  } catch (reason) { error.value = errorMessage(reason) }
  finally { saving.value = false }
}

async function remove(product: Product): Promise<void> {
  if (!confirm(`確定刪除「${product.name}」？`)) return
  try {
    await deleteProduct(product.productCode)
    await load()
  } catch (reason) { error.value = errorMessage(reason) }
}

onMounted(load)
</script>

<template>
  <section>
    <div class="page-header">
      <h1>商品管理</h1>
      <RouterLink class="button secondary" to="/admin">管理首頁</RouterLink>
    </div>
    <p v-if="error" class="alert">{{ error }}</p>
    <form class="card form-grid" @submit.prevent="save">
      <h2>{{ editingCode ? '編輯商品' : '新增商品' }}</h2>
      <div class="admin-form">
        <label class="form-row">商品代碼<input v-model.trim="form.productCode" :disabled="Boolean(editingCode)" required /></label>
        <label class="form-row">名稱<input v-model.trim="form.name" required /></label>
        <label class="form-row">分類<input v-model.trim="form.category" /></label>
        <label class="form-row">原價<input v-model.number="form.price" type="number" min="0" step="0.01" required /></label>
        <label class="form-row">價格狀態
          <select v-model="form.isOnSale">
            <option :value="false">原價</option>
            <option :value="true">特價中</option>
          </select>
        </label>
        <label class="form-row">特價價格
          <input v-model.number="form.salePrice" type="number" min="0.01" step="0.01" :required="form.isOnSale" :disabled="!form.isOnSale" />
        </label>
        <label class="form-row">庫存<input v-model.number="form.stockQuantity" type="number" min="0" required /></label>
        <label class="form-row">商品狀態
          <select v-model.number="form.status" :disabled="form.stockQuantity <= 0">
            <option :value="0">可銷售</option>
            <option :value="1">凍結</option>
            <option :value="2">數量不足</option>
          </select>
          <small v-if="form.stockQuantity <= 0" class="muted">庫存為 0 時會強制設為數量不足。</small>
        </label>
        <label class="form-row">圖片路徑
          <input v-model.trim="form.imagePath" placeholder="~/images/example.webp" />
          <small class="muted">支援 WebP、JPG、PNG、GIF 與 SVG 圖片。</small>
        </label>
      </div>
      <div class="actions">
        <button class="button" :disabled="saving">{{ saving ? '儲存中...' : editingCode ? '儲存修改' : '新增商品' }}</button>
        <button v-if="editingCode" class="button secondary" type="button" :disabled="saving" @click="reset">取消</button>
      </div>
    </form>
    <div class="card table-wrap product-table">
      <table>
        <thead><tr><th>代碼</th><th>名稱</th><th>原價</th><th>價格狀態</th><th>目前售價</th><th>庫存</th><th>商品狀態</th><th></th></tr></thead>
        <tbody>
          <tr v-for="product in products" :key="product.productCode">
            <td>{{ product.productCode }}</td>
            <td>{{ product.name }}</td>
            <td>NT$ {{ product.price.toLocaleString() }}</td>
            <td><span class="price-status">{{ product.priceStatusName }}</span></td>
            <td :class="{ 'sale-price': product.isOnSale }">NT$ {{ product.effectivePrice.toLocaleString() }}</td>
            <td>{{ product.stockQuantity }}</td>
            <td>{{ product.statusName }}</td>
            <td class="actions">
              <button class="button secondary" type="button" @click="edit(product)">編輯</button>
              <button class="button danger" type="button" @click="remove(product)">刪除</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>

<style scoped>
.admin-form{display:grid;grid-template-columns:repeat(auto-fit,minmax(210px,1fr));gap:16px}
.product-table{margin-top:24px}
.sale-price{color:#b42318;font-weight:800}
</style>
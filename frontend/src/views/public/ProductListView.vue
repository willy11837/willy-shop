<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { getOnSaleProducts, getProducts } from '@/api/products'
import { useCartStore } from '@/stores/cart'
import { productStatuses, type Product } from '@/types/product'
import { errorMessage } from '@/utils/errors'

const cart = useCartStore()
const products = ref<Product[]>([])
const onSaleProducts = ref<Product[]>([])
const query = reactive({ productCode: '', productName: '', category: '', sortBy: 'productCode' as const, sortDirection: 'asc' as const, page: 1, pageSize: 12 })
const totalCount = ref(0)
const loading = ref(false)
const message = ref('')
const error = ref('')

async function load(): Promise<void> {
  loading.value = true
  error.value = ''
  try {
    const result = await getProducts({
      ...query,
      productCode: query.productCode || undefined,
      productName: query.productName || undefined,
      category: query.category || undefined,
    })
    products.value = result.items
    totalCount.value = result.totalCount
  }
  catch (reason) { error.value = errorMessage(reason) }
  finally { loading.value = false }
}

async function loadOnSaleProducts(): Promise<void> {
  try { onSaleProducts.value = await getOnSaleProducts() }
  catch { onSaleProducts.value = [] }
}

const pageCount = computed(() => Math.max(1, Math.ceil(totalCount.value / query.pageSize)))

function isPurchasable(product: Product): boolean {
  return product.status === productStatuses.available && product.stockQuantity > 0
}

async function searchProducts(): Promise<void> {
  query.page = 1
  await load()
}

async function changePage(page: number): Promise<void> {
  query.page = Math.min(Math.max(page, 1), pageCount.value)
  await load()
}

async function add(product: Product): Promise<void> {
  if (!isPurchasable(product)) return
  error.value = ''
  message.value = ''
  try {
    await cart.add(product.productCode)
    message.value = `已將「${product.name}」加入購物車。`
  } catch (reason) { error.value = errorMessage(reason) }
}

onMounted(() => {
  void load()
  void loadOnSaleProducts()
})
</script>

<template>
  <section>
    <div v-if="onSaleProducts.length" class="sale-marquee" aria-label="特價商品快訊">
      <div class="sale-marquee-label">特價快訊</div>
      <div class="sale-marquee-viewport">
        <div class="sale-marquee-track">
          <RouterLink
            v-for="product in onSaleProducts"
            :key="product.productCode"
            class="sale-marquee-item"
            :to="{ name: 'product-details', params: { productCode: product.productCode } }"
          >
            {{ product.name }} NT$ {{ product.effectivePrice.toLocaleString() }}
          </RouterLink>
        </div>
      </div>
    </div>
    <div class="page-header">
      <div><h1>商品列表</h1><p class="muted">瀏覽可銷售商品，缺貨商品仍可查看並訂閱到貨通知。</p></div>
      <form class="actions" @submit.prevent="searchProducts">
        <input v-model.trim="query.productCode" aria-label="商品代碼" placeholder="商品代碼" />
        <input v-model.trim="query.productName" aria-label="商品名稱" placeholder="商品名稱" />
        <input v-model.trim="query.category" aria-label="商品分類" placeholder="分類" />
        <select v-model="query.sortBy" aria-label="排序欄位">
          <option value="productCode">商品代碼</option><option value="productName">商品名稱</option><option value="price">價格</option><option value="modifiedAt">修改時間</option>
        </select>
        <select v-model="query.sortDirection" aria-label="排序方向"><option value="asc">升冪</option><option value="desc">降冪</option></select>
        <button class="button" type="submit">搜尋</button>
      </form>
    </div>
    <p v-if="error" class="alert">{{ error }}</p>
    <p v-if="message" class="alert success">{{ message }}</p>
    <p v-if="loading">載入中...</p>
    <div v-else-if="products.length" class="grid product-grid">
      <article v-for="product in products" :key="product.productCode" class="card product-card">
        <img class="product-image" :src="product.imageUrl || '/vite.svg'" :alt="product.name" />
        <div><strong>{{ product.name }}</strong><div class="muted">{{ product.category || '未分類' }}</div></div>
        <div class="product-availability">
          <span>庫存：{{ product.stockQuantity }}</span>
          <span :class="{ 'out-of-stock': !isPurchasable(product) }">{{ product.statusName }}</span>
        </div>
        <div>
          <span class="price-status">{{ product.priceStatusName }}</span>
          <div v-if="product.isOnSale" class="price-row">
            <span class="original-price">NT$ {{ product.price.toLocaleString() }}</span>
            <span class="price sale-price">NT$ {{ product.effectivePrice.toLocaleString() }}</span>
          </div>
          <div v-else class="price">NT$ {{ product.effectivePrice.toLocaleString() }}</div>
        </div>
        <div class="actions">
          <RouterLink class="button secondary" :to="`/products/${product.productCode}`">查看</RouterLink>
          <button class="button" type="button" :disabled="!isPurchasable(product)" @click="add(product)">
            {{ isPurchasable(product) ? '加入購物車' : '數量不足' }}
          </button>
        </div>
      </article>
    </div>
    <div v-else class="card">目前沒有符合條件的商品。</div>
    <nav v-if="totalCount" class="actions" aria-label="商品分頁" style="justify-content:center;margin-top:24px">
      <button class="button secondary" type="button" :disabled="query.page <= 1 || loading" @click="changePage(query.page - 1)">上一頁</button>
      <span>第 {{ query.page }} / {{ pageCount }} 頁，共 {{ totalCount }} 筆</span>
      <button class="button secondary" type="button" :disabled="query.page >= pageCount || loading" @click="changePage(query.page + 1)">下一頁</button>
    </nav>
  </section>
</template>

<style scoped>
.sale-marquee{display:flex;align-items:center;margin-bottom:24px;overflow:hidden;border-radius:10px;color:#7a271a;background:#ffebe8}
.sale-marquee-label{z-index:1;flex:0 0 auto;padding:12px 16px;color:white;background:#b42318;font-weight:800}
.sale-marquee-viewport{flex:1;overflow:hidden;white-space:nowrap}
.sale-marquee-track{display:inline-flex;min-width:max-content;padding-left:100%;animation:marquee 24s linear infinite}
.sale-marquee-item{padding:0 28px;color:inherit;font-weight:700;text-decoration:none}
.sale-marquee-item:hover{text-decoration:underline}
.sale-marquee-item:focus-visible{border-radius:4px;outline:2px solid currentColor;outline-offset:2px}
.sale-marquee:hover .sale-marquee-track,.sale-marquee:focus-within .sale-marquee-track{animation-play-state:paused}
.product-availability{display:flex;justify-content:space-between;gap:12px;color:#475467}
.out-of-stock{color:#b42318;font-weight:700}
@keyframes marquee{from{transform:translateX(0)}to{transform:translateX(-100%)}}
@media (prefers-reduced-motion:reduce){.sale-marquee-track{animation:none;white-space:normal;flex-wrap:wrap;gap:8px;padding:10px}.sale-marquee-item{padding:0 8px}}
</style>
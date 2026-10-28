import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import type { Role } from '@/types/auth'

declare module 'vue-router' {
  interface RouteMeta {
    title?: string
    requiresAuth?: boolean
    roles?: Role[]
  }
}

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/', name: 'products', component: () => import('@/views/public/ProductListView.vue'), meta: { title: '商品列表' } },
    { path: '/products/:productCode', name: 'product-details', component: () => import('@/views/public/ProductDetailView.vue'), meta: { title: '商品詳情' } },
    { path: '/cart', name: 'cart', component: () => import('@/views/public/CartView.vue'), meta: { title: '購物車' } },
    { path: '/login', name: 'login', component: () => import('@/views/account/LoginView.vue'), meta: { title: '登入' } },
    { path: '/register', name: 'register', component: () => import('@/views/account/RegisterView.vue'), meta: { title: '註冊' } },
    { path: '/orders', name: 'orders', component: () => import('@/views/account/OrdersView.vue'), meta: { title: '我的訂單', requiresAuth: true } },
    { path: '/orders/:orderId(\\d+)', name: 'order-detail', component: () => import('@/views/account/OrderDetailView.vue'), meta: { title: '訂單明細', requiresAuth: true } },
    { path: '/checkout', name: 'checkout', component: () => import('@/views/account/CheckoutView.vue'), meta: { title: '結帳', requiresAuth: true } },
    { path: '/checkout/complete/:orderId(\\d+)', name: 'checkout-complete', component: () => import('@/views/account/CheckoutCompleteView.vue'), meta: { title: '訂單完成', requiresAuth: true } },
    { path: '/profile', name: 'profile', component: () => import('@/views/account/ProfileView.vue'), meta: { title: '個人資料', requiresAuth: true } },
    { path: '/notifications', name: 'notifications', component: () => import('@/views/account/NotificationsView.vue'), meta: { title: '通知中心', requiresAuth: true } },
    { path: '/admin', name: 'admin', component: () => import('@/views/admin/AdminHomeView.vue'), meta: { title: '管理後台', requiresAuth: true, roles: ['System', 'Admin', 'Store'] } },
    { path: '/admin/products', name: 'admin-products', component: () => import('@/views/admin/AdminProductsView.vue'), meta: { title: '商品管理', requiresAuth: true, roles: ['System', 'Admin', 'Store'] } },
    { path: '/admin/users', name: 'admin-users', component: () => import('@/views/admin/AdminUsersView.vue'), meta: { title: '帳號管理', requiresAuth: true, roles: ['System', 'Admin'] } },
    { path: '/forbidden', name: 'forbidden', component: () => import('@/views/ForbiddenView.vue'), meta: { title: '沒有權限' } },
    { path: '/:pathMatch(.*)*', name: 'not-found', component: () => import('@/views/NotFoundView.vue'), meta: { title: '找不到頁面' } },
  ],
  scrollBehavior: () => ({ top: 0 }),
})

router.beforeEach(async (to) => {
  const auth = useAuthStore()
  await auth.initialize()
  if (to.meta.requiresAuth && !auth.isAuthenticated)
    return { name: 'login', query: { redirect: to.fullPath } }
  if (to.meta.roles?.length && !auth.hasAnyRole(to.meta.roles)) return { name: 'forbidden' }
})

router.afterEach((to) => {
  const appTitle = import.meta.env.VITE_APP_TITLE ?? 'Willy Shop'
  document.title = to.meta.title ? `${to.meta.title}｜${appTitle}` : appTitle
})

export default router
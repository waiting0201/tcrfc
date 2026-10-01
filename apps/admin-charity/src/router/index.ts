import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { isAuthenticated, isBootstrapped, markBootstrapped } from '@/auth/session'
import { loadProfile, refreshAccessToken } from '@/api/auth'

const AdminLayout = () => import('@/layouts/AdminLayout.vue')
const LoginView = () => import('@/views/LoginView.vue')
const NotFoundView = () => import('@/views/NotFoundView.vue')

const StoreListView = () => import('@/views/stores/StoreListView.vue')
const StoreEditView = () => import('@/views/stores/StoreEditView.vue')
const ProjectListView = () => import('@/views/projects/ProjectListView.vue')
const ProjectEditView = () => import('@/views/projects/ProjectEditView.vue')
const DonationListView = () => import('@/views/donations/DonationListView.vue')
const SettlementView = () => import('@/views/settlements/SettlementView.vue')
const InvoiceListView = () => import('@/views/invoices/InvoiceListView.vue')
const ReportView = () => import('@/views/reports/ReportView.vue')
const SiteSettingView = () => import('@/views/settings/SiteSettingView.vue')

const routes: RouteRecordRaw[] = [
  { path: '/login', name: 'login', component: LoginView, meta: { label: '登入' } },
  {
    path: '/',
    component: AdminLayout,
    children: [
      { path: '', redirect: '/stores' },
      { path: 'stores', name: 'stores', component: StoreListView, meta: { label: '店家與 QR Code', code: 'N1' } },
      { path: 'stores/new', name: 'store-new', component: StoreEditView, meta: { label: '新增店家', code: 'N1' } },
      {
        path: 'stores/:storeKey/edit',
        name: 'store-edit',
        component: StoreEditView,
        props: true,
        meta: { label: '編輯店家', code: 'N1' },
      },
      { path: 'projects', name: 'projects', component: ProjectListView, meta: { label: '捐款項目管理', code: 'N2' } },
      {
        path: 'projects/new',
        name: 'project-new',
        component: ProjectEditView,
        meta: { label: '新增捐款項目', code: 'N2' },
      },
      {
        path: 'projects/:projectKey/edit',
        name: 'project-edit',
        component: ProjectEditView,
        props: true,
        meta: { label: '編輯捐款項目', code: 'N2' },
      },
      { path: 'donations', name: 'donations', component: DonationListView, meta: { label: '捐款紀錄', code: 'N3' } },
      // N4–N7 的後端尚未提供（回饋金結算、發票管理、報表、站台設定屬後續批次），畫面維持示範資料，
      // 並由 AdminLayout 依 meta.demo 顯示「示範畫面」提示，避免被誤認為真實資料。
      { path: 'settlements', name: 'settlements', component: SettlementView, meta: { label: '回饋金結算', code: 'N4', demo: true } },
      { path: 'invoices', name: 'invoices', component: InvoiceListView, meta: { label: '發票與收據管理', code: 'N5', demo: true } },
      { path: 'reports', name: 'reports', component: ReportView, meta: { label: '捐款報表', code: 'N6', demo: true } },
      { path: 'settings', name: 'settings', component: SiteSettingView, meta: { label: '站台設定', code: 'N7', demo: true } },
    ],
  },
  { path: '/:pathMatch(.*)*', name: 'not-found', component: NotFoundView },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

// 登入守衛：開機時（重新整理頁面）先用更新權杖 Cookie 靜默換一次存取權杖，成功才載入個人檔案；
// 沒有有效登入就導向登入頁（帶 redirect 以便登入後回到原本要去的頁面）。
router.beforeEach(async (to) => {
  if (!isBootstrapped()) {
    markBootstrapped()
    if (await refreshAccessToken()) {
      try { await loadProfile() } catch { /* 個人檔案讀不到時，下一個請求會處理 */ }
    }
  }
  if (to.name === 'login') return isAuthenticated.value ? { path: '/' } : true
  if (!isAuthenticated.value) return { name: 'login', query: to.fullPath === '/' ? undefined : { redirect: to.fullPath } }
  return true
})

export default router

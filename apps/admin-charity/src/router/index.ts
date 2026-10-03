import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { authUser, isAuthenticated, isBootstrapped, markBootstrapped } from '@/auth/session'
import { loadProfile, refreshAccessToken } from '@/api/auth'
import { NAV_ITEMS, isNavItemVisible } from '@/data/nav'
import { ElMessage } from 'element-plus'

const AdminLayout = () => import('@/layouts/AdminLayout.vue')
const LoginView = () => import('@/views/LoginView.vue')
const NotFoundView = () => import('@/views/NotFoundView.vue')

const StoreListView = () => import('@/views/stores/StoreListView.vue')
const StoreEditView = () => import('@/views/stores/StoreEditView.vue')
const ProjectListView = () => import('@/views/projects/ProjectListView.vue')
const ProjectEditView = () => import('@/views/projects/ProjectEditView.vue')
const ProjectContentView = () => import('@/views/projects/ProjectContentView.vue')
const DonationListView = () => import('@/views/donations/DonationListView.vue')
const SettlementView = () => import('@/views/settlements/SettlementView.vue')
const InvoiceListView = () => import('@/views/invoices/InvoiceListView.vue')
const ReportView = () => import('@/views/reports/ReportView.vue')
const AccountListView = () => import('@/views/system/AccountListView.vue')
const AccountEditView = () => import('@/views/system/AccountEditView.vue')
const RoleListView = () => import('@/views/system/RoleListView.vue')
const RoleEditView = () => import('@/views/system/RoleEditView.vue')
const SiteSettingView = () => import('@/views/settings/SiteSettingView.vue')

const routes: RouteRecordRaw[] = [
  { path: '/login', name: 'login', component: LoginView, meta: { label: '登入' } },
  {
    path: '/',
    component: AdminLayout,
    children: [
      // 登入後落地在「第一個看得到的模組」：客服／行政沒有店家檢視權限，不能一律導去店家頁。
      { path: '', redirect: () => NAV_ITEMS.find(isNavItemVisible)?.path ?? '/stores' },
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
      {
        path: 'projects/:projectKey/content',
        name: 'project-content',
        component: ProjectContentView,
        props: true,
        meta: { label: '編輯項目內文', code: 'N2' },
      },
      { path: 'donations', name: 'donations', component: DonationListView, meta: { label: '捐款紀錄', code: 'N3' } },
      { path: 'settlements', name: 'settlements', component: SettlementView, meta: { label: '回饋金結算', code: 'N4' } },
      { path: 'invoices', name: 'invoices', component: InvoiceListView, meta: { label: '發票與收據管理', code: 'N5' } },
      { path: 'reports', name: 'reports', component: ReportView, meta: { label: '捐款報表', code: 'N6' } },
      { path: 'settings', name: 'settings', component: SiteSettingView, meta: { label: '站台設定', code: 'N7' } },
      // 帳號與角色管理：只有系統管理員（`sysadminOnly`）；真正的授權邊界在後端，守衛只是提前導頁。
      { path: 'system/accounts', name: 'system-account-list', component: AccountListView, meta: { label: '帳號', sysadminOnly: true } },
      { path: 'system/accounts/new', name: 'system-account-new', component: AccountEditView, meta: { label: '新增帳號', sysadminOnly: true } },
      { path: 'system/accounts/:id/edit', name: 'system-account-edit', component: AccountEditView, props: true, meta: { label: '編輯帳號', sysadminOnly: true } },
      { path: 'system/roles', name: 'system-role-list', component: RoleListView, meta: { label: '角色與權限', sysadminOnly: true } },
      { path: 'system/roles/new', name: 'system-role-new', component: RoleEditView, meta: { label: '新增角色', sysadminOnly: true } },
      { path: 'system/roles/:id/edit', name: 'system-role-edit', component: RoleEditView, props: true, meta: { label: '編輯角色', sysadminOnly: true } },
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
  if (to.meta.sysadminOnly && !authUser.value?.isSuperAdmin) {
    ElMessage.error('你的帳號沒有權限進入這個頁面。')
    return { path: '/' }
  }
  return true
})

export default router

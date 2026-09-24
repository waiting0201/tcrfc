import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { ElMessage } from 'element-plus'
import { ALL_NAV_ITEMS } from '@/data/nav'
import { authUser, isAuthenticated, isBootstrapped, markBootstrapped, needsForcedOnboarding } from '@/auth/session'
import { refreshAccessToken } from '@/api/adminAuth'
import { ensureClubsLoaded } from '@/auth/clubAccess'

const AdminLayout = () => import('@/layouts/AdminLayout.vue')
const DashboardView = () => import('@/views/DashboardView.vue')
const PageListView = () => import('@/views/pages/PageListView.vue')
const PageEditView = () => import('@/views/pages/PageEditView.vue')
const NewsListView = () => import('@/views/news/NewsListView.vue')
const NewsEditView = () => import('@/views/news/NewsEditView.vue')
const PlaceholderView = () => import('@/views/PlaceholderView.vue')
const NotFoundView = () => import('@/views/NotFoundView.vue')
const LoginView = () => import('@/views/auth/LoginView.vue')
const AccountSecurityView = () => import('@/views/account/AccountSecurityView.vue')
const AccountListView = () => import('@/views/system/AccountListView.vue')
const AccountEditView = () => import('@/views/system/AccountEditView.vue')
const RoleListView = () => import('@/views/system/RoleListView.vue')
const RoleEditView = () => import('@/views/system/RoleEditView.vue')
const ClubListView = () => import('@/views/system/ClubListView.vue')
const ClubEditView = () => import('@/views/system/ClubEditView.vue')
const CompetitionListView = () => import('@/views/teams/CompetitionListView.vue')
const CompetitionEditView = () => import('@/views/teams/CompetitionEditView.vue')

/**
 * 已經真的做出功能的路徑，優先於「還沒做」的通用佔位路由。
 * 對照 docs/21-admin-ui.md §10：外殼＋儀表板＋新聞與故事列表／編輯頁，
 * 本輪新增 J1／J2／J4（帳號、角色與權限、俱樂部與授權）與 C4 底下的賽事系列維護。
 *
 * `meta.sysadminOnly`：只有系統管理員能看到與進入（規劃書 §6 權限矩陣「系統」欄只有系統管理員），
 * 對應的後端端點全部是 `sysadmin_only` 權限碼，這裡的守衛只是提前導頁、不是真正的邊界
 * （見 `router.beforeEach` 與 apps/api/README.md「型別強制的三層防線」）。
 */
const IMPLEMENTED_ROUTES: RouteRecordRaw[] = [
  { path: '/dashboard', name: 'dashboard', component: DashboardView, meta: { label: '儀表板', code: 'A' } },
  { path: '/content/pages', name: 'page-list', component: PageListView, meta: { label: '頁面管理', code: 'B1' } },
  {
    path: '/content/pages/new',
    name: 'page-new',
    component: PageEditView,
    meta: { label: '新增頁面', code: 'B1' },
  },
  {
    path: '/content/pages/:id/edit',
    name: 'page-edit',
    component: PageEditView,
    props: true,
    meta: { label: '編輯頁面', code: 'B1' },
  },
  { path: '/content/news', name: 'news-list', component: NewsListView, meta: { label: '新聞與故事', code: 'B2' } },
  {
    path: '/content/news/new',
    name: 'news-new',
    component: NewsEditView,
    meta: { label: '新增文章', code: 'B2' },
  },
  {
    path: '/content/news/:id/edit',
    name: 'news-edit',
    component: NewsEditView,
    props: true,
    meta: { label: '編輯文章', code: 'B2' },
  },
  {
    path: '/teams/competitions',
    name: 'competition-list',
    component: CompetitionListView,
    meta: { label: '賽事系列', code: 'C4' },
  },
  {
    path: '/teams/competitions/new',
    name: 'competition-new',
    component: CompetitionEditView,
    meta: { label: '新增賽事系列', code: 'C4' },
  },
  {
    path: '/teams/competitions/:id/edit',
    name: 'competition-edit',
    component: CompetitionEditView,
    props: true,
    meta: { label: '編輯賽事系列', code: 'C4' },
  },
  {
    path: '/system/accounts',
    name: 'system-account-list',
    component: AccountListView,
    meta: { label: '帳號', code: 'J1', sysadminOnly: true },
  },
  {
    path: '/system/accounts/new',
    name: 'system-account-new',
    component: AccountEditView,
    meta: { label: '新增帳號', code: 'J1', sysadminOnly: true },
  },
  {
    path: '/system/accounts/:id/edit',
    name: 'system-account-edit',
    component: AccountEditView,
    props: true,
    meta: { label: '編輯帳號', code: 'J1', sysadminOnly: true },
  },
  {
    path: '/system/roles',
    name: 'system-role-list',
    component: RoleListView,
    meta: { label: '角色與權限', code: 'J2', sysadminOnly: true },
  },
  {
    path: '/system/roles/new',
    name: 'system-role-new',
    component: RoleEditView,
    meta: { label: '新增角色', code: 'J2', sysadminOnly: true },
  },
  {
    path: '/system/roles/:id/edit',
    name: 'system-role-edit',
    component: RoleEditView,
    props: true,
    meta: { label: '編輯角色', code: 'J2', sysadminOnly: true },
  },
  {
    path: '/system/clubs',
    name: 'system-club-list',
    component: ClubListView,
    meta: { label: '俱樂部與授權管理', code: 'J4', sysadminOnly: true },
  },
  {
    path: '/system/clubs/new',
    name: 'system-club-new',
    component: ClubEditView,
    meta: { label: '新增俱樂部', code: 'J4', sysadminOnly: true },
  },
  {
    path: '/system/clubs/:id/edit',
    name: 'system-club-edit',
    component: ClubEditView,
    props: true,
    meta: { label: '編輯俱樂部', code: 'J4', sysadminOnly: true },
  },
]

const implementedPaths = new Set(IMPLEMENTED_ROUTES.map((route) => route.path))

/** 側欄裡「尚未建置」的模組，全部指到同一個 PlaceholderView，帶上模組名稱與代號 */
const PLACEHOLDER_ROUTES: RouteRecordRaw[] = ALL_NAV_ITEMS.filter(
  (item) => !item.implemented && !implementedPaths.has(item.path),
).map((item) => ({
  path: item.path,
  name: `placeholder-${item.code}`,
  component: PlaceholderView,
  meta: { label: item.label, code: item.code },
}))

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/login', name: 'login', component: LoginView, meta: { public: true } },
    { path: '/account/security', name: 'account-security', component: AccountSecurityView },
    {
      path: '/',
      component: AdminLayout,
      children: [
        { path: '', redirect: '/dashboard' },
        ...IMPLEMENTED_ROUTES,
        ...PLACEHOLDER_ROUTES,
      ],
    },
    { path: '/:pathMatch(.*)*', name: 'not-found', component: NotFoundView },
  ],
})

/**
 * 開機時的靜默換權杖：頁面重新整理後，記憶體裡的存取權杖會不見（刻意的存放策略，見
 * `@/auth/session` 檔頭說明），但 `__Host-tcrfc-admin-rt` 更新權杖 Cookie 還在，用它換一次
 * 新的存取權杖就能恢復工作階段，使用者感受不到差異。只嘗試一次，不論成功或失敗都標記完成，
 * 避免每次路由跳轉都重打一次 `/auth/refresh`。
 */
let bootstrapPromise: Promise<void> | null = null
function ensureBootstrapped(): Promise<void> {
  if (isBootstrapped()) return Promise.resolve()
  if (!bootstrapPromise) {
    bootstrapPromise = refreshAccessToken()
      .catch(() => false)
      .finally(() => markBootstrapped())
      .then(() => undefined)
  }
  return bootstrapPromise
}

router.beforeEach(async (to) => {
  await ensureBootstrapped()

  if (to.meta.public) {
    // 已經登入卻又想進登入頁：直接送去後台首頁，不必再看一次登入表單。
    if (to.name === 'login' && isAuthenticated.value) return '/dashboard'
    return true
  }

  if (!isAuthenticated.value) {
    return { name: 'login', query: to.fullPath !== '/' ? { redirect: to.fullPath } : undefined }
  }

  // 首次登入強制改密／尚未啟用兩階段驗證：導去帳號安全設定頁把它做完（伺服器端仍是最終把關，
  // 見 apps/api/README.md「強制密碼更換與強制 2FA 在哪裡擋」）。
  if (needsForcedOnboarding.value && to.name !== 'account-security') {
    const reason = authUser.value?.mustChangePassword ? 'password' : 'totp'
    return { name: 'account-security', query: { forced: reason } }
  }

  // 僅系統管理員可見的模組（J1／J2／J4），非系統管理員即使直接改網址也導回儀表板——
  // 側欄本身也不會顯示這些項目給非系統管理員（見 AppSidebar.vue），這裡是第二層提醒，
  // 真正的把關永遠在後端（每個端點的權限碼皆為 sysadmin_only）。
  if (to.meta.sysadminOnly && !authUser.value?.isSuperAdmin) {
    ElMessage.error('你的帳號沒有權限進入這個模組。')
    return '/dashboard'
  }

  // 進了後台外殼就順手把俱樂部清單準備好，站台切換器與各俱樂部範圍頁面都用得到。
  ensureClubsLoaded()

  return true
})

export default router

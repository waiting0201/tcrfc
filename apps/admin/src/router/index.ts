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
const HomeLayoutView = () => import('@/views/home/HomeLayoutView.vue')
const FaqListView = () => import('@/views/faq/FaqListView.vue')
const FaqEditView = () => import('@/views/faq/FaqEditView.vue')
const TeamListView = () => import('@/views/teams/TeamListView.vue')
const TeamEditView = () => import('@/views/teams/TeamEditView.vue')
const PlayerListView = () => import('@/views/teams/PlayerListView.vue')
const PlayerEditView = () => import('@/views/teams/PlayerEditView.vue')
const StaffListView = () => import('@/views/teams/StaffListView.vue')
const StaffEditView = () => import('@/views/teams/StaffEditView.vue')
const MatchListView = () => import('@/views/teams/MatchListView.vue')
const MatchEditView = () => import('@/views/teams/MatchEditView.vue')
const StandingListView = () => import('@/views/teams/StandingListView.vue')
const ProgramItemListView = () => import('@/views/programs/ProgramItemListView.vue')
const ProgramItemEditView = () => import('@/views/programs/ProgramItemEditView.vue')
const ProgramSessionListView = () => import('@/views/programs/ProgramSessionListView.vue')
const ProgramSessionEditView = () => import('@/views/programs/ProgramSessionEditView.vue')
const RegistrationListView = () => import('@/views/programs/RegistrationListView.vue')
const RegistrationEditView = () => import('@/views/programs/RegistrationEditView.vue')
const FormListView = () => import('@/views/forms/FormListView.vue')
const FormEditView = () => import('@/views/forms/FormEditView.vue')
const EnquiryInboxView = () => import('@/views/forms/EnquiryInboxView.vue')
const EnquiryEditView = () => import('@/views/forms/EnquiryEditView.vue')
const CalendarOverviewView = () => import('@/views/calendar/CalendarOverviewView.vue')
const CalendarEventListView = () => import('@/views/calendar/CalendarEventListView.vue')
const CalendarEventEditView = () => import('@/views/calendar/CalendarEventEditView.vue')
const SeoSettingsView = () => import('@/views/seo/SeoSettingsView.vue')
const RedirectListView = () => import('@/views/seo/RedirectListView.vue')
const OrphanPagesReportView = () => import('@/views/seo/OrphanPagesReportView.vue')
const LlmsContentView = () => import('@/views/seo/LlmsContentView.vue')
const AiCrawlerView = () => import('@/views/seo/AiCrawlerView.vue')

/**
 * 已經真的做出功能的路徑，優先於「還沒做」的通用佔位路由。
 * 對照 docs/21-admin-ui.md §10：外殼＋儀表板＋新聞與故事列表／編輯頁，
 * 逐輪疊加 J1／J2／J4（帳號、角色與權限、俱樂部與授權）、C4 底下的賽事系列維護，
 * 本輪（S1-8）補上 C4 真正的主體：賽程與賽果（賽事、比分、進球者、卡牌、出賽名單）與積分榜。
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
  { path: '/content/homepage', name: 'home-layout', component: HomeLayoutView, meta: { label: '首頁編排', code: 'B3' } },
  { path: '/content/faq', name: 'faq-list', component: FaqListView, meta: { label: '常見問題', code: 'B4' } },
  { path: '/content/faq/new', name: 'faq-new', component: FaqEditView, meta: { label: '新增題目', code: 'B4' } },
  {
    path: '/content/faq/:id/edit',
    name: 'faq-edit',
    component: FaqEditView,
    props: true,
    meta: { label: '編輯題目', code: 'B4' },
  },
  { path: '/teams/clubs', name: 'team-list', component: TeamListView, meta: { label: '球隊', code: 'C1' } },
  { path: '/teams/clubs/new', name: 'team-new', component: TeamEditView, meta: { label: '新增球隊', code: 'C1' } },
  {
    path: '/teams/clubs/:id/edit',
    name: 'team-edit',
    component: TeamEditView,
    props: true,
    meta: { label: '編輯球隊', code: 'C1' },
  },
  { path: '/teams/players', name: 'player-list', component: PlayerListView, meta: { label: '球員', code: 'C2' } },
  { path: '/teams/players/new', name: 'player-new', component: PlayerEditView, meta: { label: '新增球員', code: 'C2' } },
  {
    path: '/teams/players/:id/edit',
    name: 'player-edit',
    component: PlayerEditView,
    props: true,
    meta: { label: '編輯球員', code: 'C2' },
  },
  {
    path: '/teams/staff',
    name: 'staff-list',
    component: StaffListView,
    meta: { label: '教練與團隊成員', code: 'C3' },
  },
  {
    path: '/teams/staff/new',
    name: 'staff-new',
    component: StaffEditView,
    meta: { label: '新增教練與團隊成員', code: 'C3' },
  },
  {
    path: '/teams/staff/:id/edit',
    name: 'staff-edit',
    component: StaffEditView,
    props: true,
    meta: { label: '編輯教練與團隊成員', code: 'C3' },
  },
  { path: '/teams/matches', name: 'match-list', component: MatchListView, meta: { label: '賽程與賽果', code: 'C4' } },
  { path: '/teams/matches/new', name: 'match-new', component: MatchEditView, meta: { label: '新增賽事', code: 'C4' } },
  {
    path: '/teams/matches/:id/edit',
    name: 'match-edit',
    component: MatchEditView,
    props: true,
    meta: { label: '編輯賽事', code: 'C4' },
  },
  { path: '/teams/standings', name: 'standing-list', component: StandingListView, meta: { label: '積分榜', code: 'C4' } },
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
  { path: '/programs/items', name: 'program-item-list', component: ProgramItemListView, meta: { label: '項目', code: 'P1' } },
  {
    path: '/programs/items/new',
    name: 'program-item-new',
    component: ProgramItemEditView,
    meta: { label: '新增項目', code: 'P1' },
  },
  {
    path: '/programs/items/:id/edit',
    name: 'program-item-edit',
    component: ProgramItemEditView,
    props: true,
    meta: { label: '編輯項目', code: 'P1' },
  },
  { path: '/programs/sessions', name: 'program-session-list', component: ProgramSessionListView, meta: { label: '梯次', code: 'P2' } },
  {
    path: '/programs/sessions/new',
    name: 'program-session-new',
    component: ProgramSessionEditView,
    meta: { label: '新增梯次', code: 'P2' },
  },
  {
    path: '/programs/sessions/:id/edit',
    name: 'program-session-edit',
    component: ProgramSessionEditView,
    props: true,
    meta: { label: '編輯梯次', code: 'P2' },
  },
  {
    path: '/programs/enrollments',
    name: 'registration-list',
    component: RegistrationListView,
    meta: { label: '報名', code: 'P3' },
  },
  {
    path: '/programs/enrollments/new',
    name: 'registration-new',
    component: RegistrationEditView,
    meta: { label: '新增報名', code: 'P3' },
  },
  {
    path: '/programs/enrollments/:id/edit',
    name: 'registration-edit',
    component: RegistrationEditView,
    props: true,
    meta: { label: '處理報名', code: 'P3' },
  },
  { path: '/inquiries/builder', name: 'form-list', component: FormListView, meta: { label: '設計器', code: 'G1' } },
  {
    path: '/inquiries/builder/:id/edit',
    name: 'form-edit',
    component: FormEditView,
    props: true,
    meta: { label: '編輯表單', code: 'G1' },
  },
  { path: '/inquiries/inbox', name: 'enquiry-inbox-list', component: EnquiryInboxView, meta: { label: '收件匣', code: 'G2' } },
  {
    path: '/inquiries/inbox/:id/edit',
    name: 'enquiry-edit',
    component: EnquiryEditView,
    props: true,
    meta: { label: '處理詢問', code: 'G2' },
  },
  { path: '/calendar/overview', name: 'calendar-overview', component: CalendarOverviewView, meta: { label: '總覽', code: 'L1' } },
  { path: '/calendar/events', name: 'calendar-event-list', component: CalendarEventListView, meta: { label: '自建事件', code: 'L2' } },
  {
    path: '/calendar/events/new',
    name: 'calendar-event-new',
    component: CalendarEventEditView,
    meta: { label: '新增自建事件', code: 'L2' },
  },
  {
    path: '/calendar/events/:id/edit',
    name: 'calendar-event-edit',
    component: CalendarEventEditView,
    props: true,
    meta: { label: '編輯自建事件', code: 'L2' },
  },
  {
    path: '/seo/settings',
    name: 'seo-settings',
    component: SeoSettingsView,
    meta: { label: '全站設定', code: 'H1', sysadminOnly: true },
  },
  {
    path: '/seo/redirects',
    name: 'seo-redirect-list',
    component: RedirectListView,
    meta: { label: '301 轉址', code: 'H2', sysadminOnly: true },
  },
  {
    path: '/seo/orphan-pages',
    name: 'seo-orphan-pages',
    component: OrphanPagesReportView,
    meta: { label: '孤立頁面偵測', code: 'H3', sysadminOnly: true },
  },
  {
    path: '/seo/llms-content',
    name: 'seo-llms-content',
    component: LlmsContentView,
    meta: { label: 'AI 摘要資料', code: 'H4', sysadminOnly: true },
  },
  {
    path: '/seo/crawler-settings',
    name: 'seo-crawler-settings',
    component: AiCrawlerView,
    meta: { label: 'AI 爬蟲授權', code: 'H5', sysadminOnly: true },
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

  // 進了後台外殼前先把俱樂部清單準備好（含這個帳號的權限碼），站台切換器與各俱樂部範圍頁面都
  // 用得到。🔴 **這裡一定要 `await`**（2026-09-25 修正既有 bug，見 docs/18-work-errors.md）：
  // 先前是 fire-and-forget，`@/auth/clubAccess` 的 `activeClubId` 預設值固定是 `'tcrfc'`，只有
  // 這支函式 resolve 後才會被訂正成這個帳號實際被授權的俱樂部——對俱樂部範圍頁面整頁重新載入
  // （重新整理、或直接貼網址在新分頁打開）時，若沒有 `await`，目標頁面元件掛載當下第一次資料
  // 請求會先送出還沒被訂正過的預設值 `tcrfc`，沒有 `tcrfc` 授權的帳號（例如只授權 `bw` 的
  // `academy.login`）會先閃一次「你沒有被授權存取俱樂部」的錯誤畫面才自我修復。`ensureClubsLoaded`
  // 內部本來就有 `state.loaded` 短路（見該檔案），`await` 只有第一次導頁會真的等網路來回，之後
  // 每次導頁都是立即 resolve，不會拖慢整體導覽速度。
  await ensureClubsLoaded()

  return true
})

export default router

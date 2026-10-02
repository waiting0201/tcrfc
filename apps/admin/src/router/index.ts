import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { ElMessage } from 'element-plus'
import { authUser, isAuthenticated, isBootstrapped, markBootstrapped } from '@/auth/session'
import { refreshAccessToken } from '@/api/adminAuth'
import { activeClubId, ensureClubsLoaded } from '@/auth/clubAccess'
import { DEFAULT_CLUB_CODE, applyClubTheme } from '@/auth/clubTheme'

const AdminLayout = () => import('@/layouts/AdminLayout.vue')
const DashboardView = () => import('@/views/DashboardView.vue')
const PageListView = () => import('@/views/pages/PageListView.vue')
const PageEditView = () => import('@/views/pages/PageEditView.vue')
const NewsListView = () => import('@/views/news/NewsListView.vue')
const NewsEditView = () => import('@/views/news/NewsEditView.vue')
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
const SchemaCompletenessView = () => import('@/views/seo/SchemaCompletenessView.vue')
const SiteFactsView = () => import('@/views/settings/SiteFactsView.vue')
const MenuSettingsView = () => import('@/views/settings/MenuSettingsView.vue')
const GlobalSettingsView = () => import('@/views/settings/GlobalSettingsView.vue')
const LocaleSettingsView = () => import('@/views/settings/LocaleSettingsView.vue')
const VenueListView = () => import('@/views/settings/VenueListView.vue')
const VenueEditView = () => import('@/views/settings/VenueEditView.vue')
const EdmSettingsView = () => import('@/views/settings/EdmSettingsView.vue')
const PartnerListView = () => import('@/views/business/PartnerListView.vue')
const PartnerEditView = () => import('@/views/business/PartnerEditView.vue')
const SponsorshipListView = () => import('@/views/business/SponsorshipListView.vue')
const SponsorEditView = () => import('@/views/business/SponsorEditView.vue')
const SponsorPackageEditView = () => import('@/views/business/SponsorPackageEditView.vue')
const ProposalListView = () => import('@/views/business/ProposalListView.vue')
const ProposalEditView = () => import('@/views/business/ProposalEditView.vue')
const CharityView = () => import('@/views/charity/CharityView.vue')
const CharityOrgEditView = () => import('@/views/charity/CharityOrgEditView.vue')
const CharityProgramEditView = () => import('@/views/charity/CharityProgramEditView.vue')
const CharityRecordEditView = () => import('@/views/charity/CharityRecordEditView.vue')
const MediaListView = () => import('@/views/media/MediaListView.vue')
const MediaEditView = () => import('@/views/media/MediaEditView.vue')
const HonoursView = () => import('@/views/honours/HonoursView.vue')
const MilestoneEditView = () => import('@/views/honours/MilestoneEditView.vue')

const TrialListView = () => import('@/views/programs/TrialListView.vue')
const TrialEditView = () => import('@/views/programs/TrialEditView.vue')
const TrialRegistrationListView = () => import('@/views/programs/TrialRegistrationListView.vue')
const TrialRegistrationEditView = () => import('@/views/programs/TrialRegistrationEditView.vue')
const TrialSignInSheetView = () => import('@/views/programs/TrialSignInSheetView.vue')
const WaitlistReminderView = () => import('@/views/programs/WaitlistReminderView.vue')
const RegistrationSignInSheetView = () => import('@/views/programs/RegistrationSignInSheetView.vue')
const MemberListView = () => import('@/views/members/MemberListView.vue')
const MemberDetailView = () => import('@/views/members/MemberDetailView.vue')
const MemberEditView = () => import('@/views/members/MemberEditView.vue')
const MemberDuplicatesView = () => import('@/views/members/MemberDuplicatesView.vue')
const MembershipPlanView = () => import('@/views/members/MembershipPlanView.vue')
const MembershipPlanEditView = () => import('@/views/members/MembershipPlanEditView.vue')
const MembershipDetailView = () => import('@/views/members/MembershipDetailView.vue')
const JerseyListView = () => import('@/views/members/JerseyListView.vue')
const PartnerStoreView = () => import('@/views/members/PartnerStoreView.vue')
const PartnerStoreEditView = () => import('@/views/members/PartnerStoreEditView.vue')
const MangaView = () => import('@/views/culture/MangaView.vue')
const MangaEpisodeEditView = () => import('@/views/culture/MangaEpisodeEditView.vue')
const FanEventListView = () => import('@/views/culture/FanEventListView.vue')
const FanEventEditView = () => import('@/views/culture/FanEventEditView.vue')
const ProductListView = () => import('@/views/shop/ProductListView.vue')
const ProductEditView = () => import('@/views/shop/ProductEditView.vue')
const InventoryView = () => import('@/views/shop/InventoryView.vue')
const OrderListView = () => import('@/views/shop/OrderListView.vue')
const OrderCreateView = () => import('@/views/shop/OrderCreateView.vue')
const OrderDetailView = () => import('@/views/shop/OrderDetailView.vue')
const ShippingView = () => import('@/views/shop/ShippingView.vue')
const ReturnListView = () => import('@/views/shop/ReturnListView.vue')
const ReturnDetailView = () => import('@/views/shop/ReturnDetailView.vue')
const ShopSettingsView = () => import('@/views/shop/ShopSettingsView.vue')
const DrawListView = () => import('@/views/members/DrawListView.vue')
const DrawEditView = () => import('@/views/members/DrawEditView.vue')
const DrawDetailView = () => import('@/views/members/DrawDetailView.vue')
const CalendarCategoriesView = () => import('@/views/calendar/CalendarCategoriesView.vue')
const CalendarSubscriptionsView = () => import('@/views/calendar/CalendarSubscriptionsView.vue')
const NewsletterView = () => import('@/views/forms/NewsletterView.vue')
const AdvertiserView = () => import('@/views/ads/AdvertiserView.vue')
const CampaignListView = () => import('@/views/ads/CampaignListView.vue')
const CampaignDetailView = () => import('@/views/ads/CampaignDetailView.vue')
const AdReportView = () => import('@/views/ads/AdReportView.vue')
const ReleaseView = () => import('@/views/app/ReleaseView.vue')
const AppContentView = () => import('@/views/app/AppContentView.vue')
const PushView = () => import('@/views/app/PushView.vue')
const PushEditView = () => import('@/views/app/PushEditView.vue')
const PushDetailView = () => import('@/views/app/PushDetailView.vue')
const DeviceView = () => import('@/views/app/DeviceView.vue')
const AppSettingsView = () => import('@/views/app/AppSettingsView.vue')
const AuditView = () => import('@/views/system/AuditView.vue')

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
    path: '/seo/schema-completeness',
    name: 'seo-schema-completeness',
    component: SchemaCompletenessView,
    meta: { label: '結構化資料完整性檢查', code: 'H6', sysadminOnly: true },
  },
  {
    path: '/settings/site',
    name: 'settings-site-facts',
    component: SiteFactsView,
    meta: { label: '基本資料與聯絡方式', code: 'I1', sysadminOnly: true },
  },
  // H 批：其餘網站設定子畫面。不加 sysadminOnly——多語系的字串翻譯表開放給翻譯人員；
  // 各畫面進入後由後端權限碼把關（403 會顯示在畫面上），選單可見度見 AppSidebar 的 CHILD_VISIBILITY。
  { path: '/settings/menus', name: 'settings-menus', component: MenuSettingsView, meta: { label: '選單管理', code: 'I2' } },
  { path: '/settings/global', name: 'settings-global', component: GlobalSettingsView, meta: { label: '全域設定', code: 'I3' } },
  { path: '/settings/locales', name: 'settings-locales', component: LocaleSettingsView, meta: { label: '多語系', code: 'I4' } },
  { path: '/settings/venues', name: 'settings-venues', component: VenueListView, meta: { label: '場地管理', code: 'I5' } },
  { path: '/settings/venues/new', name: 'settings-venue-new', component: VenueEditView, meta: { label: '新增場地', code: 'I5' } },
  { path: '/settings/venues/:id/edit', name: 'settings-venue-edit', component: VenueEditView, props: true, meta: { label: '編輯場地', code: 'I5' } },
  { path: '/settings/edm', name: 'settings-edm', component: EdmSettingsView, meta: { label: '電子報平台', code: 'I6' } },
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
  { path: '/business/partners', name: 'partner-list', component: PartnerListView, meta: { label: '夥伴', code: 'E1' } },
  { path: '/business/partners/new', name: 'partner-new', component: PartnerEditView, meta: { label: '新增夥伴', code: 'E1' } },
  { path: '/business/partners/:id/edit', name: 'partner-edit', component: PartnerEditView, props: true, meta: { label: '編輯夥伴', code: 'E1' } },
  { path: '/business/sponsorships', name: 'sponsorship-list', component: SponsorshipListView, meta: { label: '贊助', code: 'E2' } },
  { path: '/business/sponsorships/sponsors/new', name: 'sponsor-new', component: SponsorEditView, meta: { label: '新增贊助商', code: 'E2' } },
  { path: '/business/sponsorships/sponsors/:id/edit', name: 'sponsor-edit', component: SponsorEditView, props: true, meta: { label: '編輯贊助商', code: 'E2' } },
  { path: '/business/sponsorships/packages/new', name: 'sponsor-package-new', component: SponsorPackageEditView, meta: { label: '新增贊助方案', code: 'E2' } },
  { path: '/business/sponsorships/packages/:id/edit', name: 'sponsor-package-edit', component: SponsorPackageEditView, props: true, meta: { label: '編輯贊助方案', code: 'E2' } },
  { path: '/business/proposals', name: 'proposal-list', component: ProposalListView, meta: { label: '提案下載', code: 'E3' } },
  { path: '/business/proposals/new', name: 'proposal-new', component: ProposalEditView, meta: { label: '新增提案', code: 'E3' } },
  { path: '/business/proposals/:id/edit', name: 'proposal-edit', component: ProposalEditView, props: true, meta: { label: '編輯提案', code: 'E3' } },
  { path: '/content/charity', name: 'charity-list', component: CharityView, meta: { label: '慈善與社會影響', code: 'B5' } },
  { path: '/content/charity/organizations/new', name: 'charity-org-new', component: CharityOrgEditView, meta: { label: '新增公益團體', code: 'B5' } },
  { path: '/content/charity/organizations/:id/edit', name: 'charity-org-edit', component: CharityOrgEditView, props: true, meta: { label: '編輯公益團體', code: 'B5' } },
  { path: '/content/charity/programs/new', name: 'charity-program-new', component: CharityProgramEditView, meta: { label: '新增慈善計畫', code: 'B5' } },
  { path: '/content/charity/programs/:id/edit', name: 'charity-program-edit', component: CharityProgramEditView, props: true, meta: { label: '編輯慈善計畫', code: 'B5' } },
  { path: '/content/charity/records/new', name: 'charity-record-new', component: CharityRecordEditView, meta: { label: '新增事蹟紀錄', code: 'B5' } },
  { path: '/content/charity/records/:id/edit', name: 'charity-record-edit', component: CharityRecordEditView, props: true, meta: { label: '編輯事蹟紀錄', code: 'B5' } },
  { path: '/content/media', name: 'media-list', component: MediaListView, meta: { label: '媒體專區', code: 'B6' } },
  { path: '/content/media/new', name: 'media-new', component: MediaEditView, meta: { label: '新增媒體資源', code: 'B6' } },
  { path: '/content/media/:id/edit', name: 'media-edit', component: MediaEditView, props: true, meta: { label: '編輯媒體資源', code: 'B6' } },
  { path: '/teams/honours', name: 'honours', component: HonoursView, meta: { label: '榮譽與里程碑', code: 'C5' } },
  { path: '/teams/honours/milestones/new', name: 'milestone-new', component: MilestoneEditView, meta: { label: '新增里程碑', code: 'C5' } },
  { path: '/teams/honours/milestones/:id/edit', name: 'milestone-edit', component: MilestoneEditView, props: true, meta: { label: '編輯里程碑', code: 'C5' } },
  // ── B1 批（S2-4～S2-6）：P4 試訓與 P3 進階、K1–K4 會員系統、L3／L4 行事曆進階 ──
  { path: '/programs/trials', name: 'trial-list', component: TrialListView, meta: { label: '試訓場次', code: 'P4' } },
  { path: '/programs/trials/new', name: 'trial-new', component: TrialEditView, meta: { label: '新增試訓場次', code: 'P4' } },
  { path: '/programs/trials/:id/edit', name: 'trial-edit', component: TrialEditView, props: true, meta: { label: '編輯試訓場次', code: 'P4' } },
  { path: '/programs/trials/:id/registrations', name: 'trial-registration-list', component: TrialRegistrationListView, props: true, meta: { label: '試訓報名名單', code: 'P4' } },
  { path: '/programs/trials/:id/registrations/new', name: 'trial-registration-new', component: TrialRegistrationEditView, props: true, meta: { label: '新增試訓報名', code: 'P4' } },
  { path: '/programs/trials/:id/registrations/:regId/edit', name: 'trial-registration-edit', component: TrialRegistrationEditView, props: true, meta: { label: '處理試訓報名', code: 'P4' } },
  { path: '/programs/trials/:id/sign-in', name: 'trial-sign-in', component: TrialSignInSheetView, props: true, meta: { label: '試訓簽到表', code: 'P4' } },
  { path: '/programs/enrollments/waitlist', name: 'registration-waitlist', component: WaitlistReminderView, meta: { label: '候補遞補提醒', code: 'P3' } },
  { path: '/programs/enrollments/sign-in', name: 'registration-sign-in', component: RegistrationSignInSheetView, meta: { label: '課程簽到表', code: 'P3' } },
  { path: '/members/list', name: 'member-list', component: MemberListView, meta: { label: '會員名單', code: 'K1' } },
  { path: '/members/list/new', name: 'member-new', component: MemberEditView, meta: { label: '現場建立會員', code: 'K1' } },
  { path: '/members/list/duplicates', name: 'member-duplicates', component: MemberDuplicatesView, meta: { label: '重複帳號比對', code: 'K1' } },
  { path: '/members/list/:id', name: 'member-detail', component: MemberDetailView, props: true, meta: { label: '會員詳情', code: 'K1' } },
  { path: '/members/plans', name: 'membership-plan-list', component: MembershipPlanView, meta: { label: '會籍與方案', code: 'K2' } },
  { path: '/members/plans/new', name: 'membership-plan-new', component: MembershipPlanEditView, meta: { label: '新增方案', code: 'K2' } },
  { path: '/members/plans/memberships/:id', name: 'membership-detail', component: MembershipDetailView, props: true, meta: { label: '會籍詳情', code: 'K2' } },
  { path: '/members/plans/:id/edit', name: 'membership-plan-edit', component: MembershipPlanEditView, props: true, meta: { label: '編輯方案', code: 'K2' } },
  { path: '/members/jerseys', name: 'jersey-list', component: JerseyListView, meta: { label: '球衣發放', code: 'K3' } },
  { path: '/members/partner-stores', name: 'partner-store-list', component: PartnerStoreView, meta: { label: '特約店家與權益', code: 'K4' } },
  { path: '/members/partner-stores/new', name: 'partner-store-new', component: PartnerStoreEditView, meta: { label: '新增特約店家', code: 'K4' } },
  { path: '/members/partner-stores/:id/edit', name: 'partner-store-edit', component: PartnerStoreEditView, props: true, meta: { label: '編輯特約店家', code: 'K4' } },
  { path: '/calendar/categories', name: 'calendar-categories', component: CalendarCategoriesView, meta: { label: '分類設定', code: 'L3' } },
  { path: '/calendar/subscriptions', name: 'calendar-subscriptions', component: CalendarSubscriptionsView, meta: { label: '訂閱與匯出', code: 'L4' } },
  // ── C1 批（S3-1／S3-3／S3-4／S3-8）：F1 漫畫、F2 球迷會活動、S1–S6 站內商店、K5 抽獎名單 ──
  { path: '/culture/manga', name: 'manga', component: MangaView, meta: { label: '漫畫', code: 'F1' } },
  { path: '/culture/manga/episodes/new', name: 'manga-episode-new', component: MangaEpisodeEditView, meta: { label: '新增集數', code: 'F1' } },
  { path: '/culture/manga/episodes/:id/edit', name: 'manga-episode-edit', component: MangaEpisodeEditView, props: true, meta: { label: '編輯集數', code: 'F1' } },
  { path: '/culture/fan-events', name: 'fan-event-list', component: FanEventListView, meta: { label: '球迷會活動', code: 'F2' } },
  { path: '/culture/fan-events/new', name: 'fan-event-new', component: FanEventEditView, meta: { label: '新增活動', code: 'F2' } },
  { path: '/culture/fan-events/:id/edit', name: 'fan-event-edit', component: FanEventEditView, props: true, meta: { label: '編輯活動', code: 'F2' } },
  { path: '/shop/products', name: 'shop-product-list', component: ProductListView, meta: { label: '商品與規格', code: 'S1' } },
  { path: '/shop/products/new', name: 'shop-product-new', component: ProductEditView, meta: { label: '新增商品', code: 'S1' } },
  { path: '/shop/products/:id/edit', name: 'shop-product-edit', component: ProductEditView, props: true, meta: { label: '編輯商品', code: 'S1' } },
  { path: '/shop/inventory', name: 'shop-inventory', component: InventoryView, meta: { label: '庫存', code: 'S2' } },
  { path: '/shop/orders', name: 'shop-order-list', component: OrderListView, meta: { label: '訂單', code: 'S3' } },
  { path: '/shop/orders/new', name: 'shop-order-new', component: OrderCreateView, meta: { label: '手動建單', code: 'S3' } },
  { path: '/shop/orders/:id', name: 'shop-order-detail', component: OrderDetailView, props: true, meta: { label: '訂單詳情', code: 'S3' } },
  { path: '/shop/shipping', name: 'shop-shipping', component: ShippingView, meta: { label: '出貨與物流', code: 'S4' } },
  { path: '/shop/returns', name: 'shop-return-list', component: ReturnListView, meta: { label: '退貨與退款', code: 'S5' } },
  { path: '/shop/returns/:id', name: 'shop-return-detail', component: ReturnDetailView, props: true, meta: { label: '退貨案件', code: 'S5' } },
  { path: '/shop/settings', name: 'shop-settings', component: ShopSettingsView, meta: { label: '設定與報表', code: 'S6' } },
  { path: '/members/lottery', name: 'draw-list', component: DrawListView, meta: { label: '抽獎名單管理', code: 'K5' } },
  { path: '/members/lottery/new', name: 'draw-new', component: DrawEditView, meta: { label: '新增抽獎活動', code: 'K5' } },
  { path: '/members/lottery/:id', name: 'draw-detail', component: DrawDetailView, props: true, meta: { label: '抽獎活動管理', code: 'K5' } },
  { path: '/members/lottery/:id/edit', name: 'draw-edit', component: DrawEditView, props: true, meta: { label: '編輯抽獎活動', code: 'K5' } },
  // ── D 批：G3 電子報、E4–E6 App 廣告（兩隊共用，不分俱樂部）、M1–M5 行動 App 後台、J3 稽核與備份 ──
  { path: '/inquiries/newsletter', name: 'newsletter', component: NewsletterView, meta: { label: '電子報', code: 'G3' } },
  { path: '/business/advertisers', name: 'ad-advertisers', component: AdvertiserView, meta: { label: '廣告主與版位', code: 'E4' } },
  { path: '/business/campaigns', name: 'ad-campaign-list', component: CampaignListView, meta: { label: '投放檔期', code: 'E5' } },
  { path: '/business/campaigns/:id', name: 'ad-campaign-detail', component: CampaignDetailView, props: true, meta: { label: '檔期詳情', code: 'E5' } },
  { path: '/business/ad-reports', name: 'ad-reports', component: AdReportView, meta: { label: '成效報表', code: 'E6' } },
  { path: '/app/releases', name: 'app-releases', component: ReleaseView, meta: { label: '版本發布', code: 'M1' } },
  { path: '/app/content', name: 'app-content', component: AppContentView, meta: { label: '內容編排', code: 'M2' } },
  { path: '/app/push', name: 'app-push', component: PushView, meta: { label: '推播', code: 'M3' } },
  { path: '/app/push/new', name: 'app-push-new', component: PushEditView, meta: { label: '新增推播', code: 'M3' } },
  { path: '/app/push/:id', name: 'app-push-detail', component: PushDetailView, props: true, meta: { label: '推播詳情', code: 'M3' } },
  { path: '/app/push/:id/edit', name: 'app-push-edit', component: PushEditView, props: true, meta: { label: '編輯推播', code: 'M3' } },
  { path: '/app/devices', name: 'app-devices', component: DeviceView, meta: { label: '推播裝置', code: 'M4' } },
  { path: '/app/settings', name: 'app-settings', component: AppSettingsView, meta: { label: 'App 設定與連線檢查', code: 'M5' } },
  { path: '/system/audit', name: 'system-audit', component: AuditView, meta: { label: '稽核與備份', code: 'J3', sysadminOnly: true } },
]

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
    // 登入頁（尚未選擇俱樂部）固定用磐石預設配色；登入後 ensureClubsLoaded 會依俱樂部換色
    applyClubTheme(DEFAULT_CLUB_CODE)
    // 已經登入卻又想進登入頁：直接送去後台首頁，不必再看一次登入表單。
    if (to.name === 'login' && isAuthenticated.value) return '/dashboard'
    return true
  }

  if (!isAuthenticated.value) {
    return { name: 'login', query: to.fullPath !== '/' ? { redirect: to.fullPath } : undefined }
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
  // 從登入頁進來時 ensureClubsLoaded 可能已短路（state.loaded），這裡再對齊一次配色
  applyClubTheme(activeClubId.value)

  return true
})

export default router

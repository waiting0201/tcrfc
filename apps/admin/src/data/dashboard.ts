/**
 * 假資料：儀表板（docs/03-admin-spec.md §2 A 儀表板、docs/21-admin-ui.md §10）。
 *
 * 本輪（後台新聞接真實 API）刻意沒有動這個檔案：核對過畫面上實際渲染出來的數字
 * （`DashboardView.vue` 只用到 `CONTENT_OVERVIEW.untranslatedCount`），沒有一個是「新聞與故事」
 * 專屬的統計（`totalPublished` 定義了但目前沒有任何畫面在用），所以接上新聞真實資料後不會出現
 * 「首頁數字跟新聞列表對不上」的矛盾。⚠️ 之後如果要在儀表板加一個「已發布新聞篇數」之類的卡片，
 * 那張卡片要接真實 API（後台新聞清單 `status=published` 的 `totalCount`），不能沿用這裡的假資料，
 * 否則就會製造出本輪任務要求排除的那種「說謊的數字」。
 */

export interface TodoReminder {
  id: string
  label: string
  count: number
  /** 路由路徑，點擊可直接跳轉到對應模組 */
  to: string
}

export interface UpcomingEvent {
  id: string
  date: string
  title: string
  type: '賽事' | '課程' | '行銷活動'
}

export interface QuickEntry {
  id: string
  label: string
  to: string
}

export const TODO_REMINDERS: TodoReminder[] = [
  { id: 'inquiry', label: '未處理的詢問', count: 5, to: '/inquiries/inbox' },
  { id: 'enrollment', label: '待審核的報名', count: 3, to: '/programs/enrollments' },
  { id: 'camp-closing', label: '即將截止的營隊', count: 2, to: '/programs/sessions' },
  { id: 'sponsor-expiring', label: '即將到期的贊助合約', count: 1, to: '/business/sponsorships' },
]

export const UPCOMING_EVENTS: UpcomingEvent[] = [
  { id: 'e1', date: '2026-09-25', title: 'U15 青訓營開放報名截止', type: '課程' },
  { id: 'e2', date: '2026-09-27', title: '台中磐石 vs 高雄成功', type: '賽事' },
  { id: 'e3', date: '2026-10-03', title: '球迷見面會', type: '行銷活動' },
  { id: 'e4', date: '2026-10-11', title: '台中磐石 vs 台南港明', type: '賽事' },
]

export const QUICK_ENTRIES: QuickEntry[] = [
  { id: 'qe1', label: '新增文章', to: '/content/news/new' },
  { id: 'qe2', label: '新增賽程', to: '/teams/matches' },
  { id: 'qe3', label: '處理詢問', to: '/inquiries/inbox' },
  { id: 'qe4', label: '會員名單', to: '/members/list' },
]

export const CONTENT_OVERVIEW = {
  untranslatedCount: 12,
  totalPublished: 96,
}

export const MEMBERSHIP_OVERVIEW = {
  newMembersThisMonth: 18,
  newPaidMemberships: 6,
  renewals: 21,
  expiringSoon: 4,
}

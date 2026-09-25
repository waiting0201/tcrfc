import type { NavGroup, NavChild, NavModule } from '@/types/nav'

/**
 * 側欄導覽資料（docs/21-admin-ui.md §1）：14 個一級模組、6 組視覺分組。
 * 有子模組的模組渲染成 el-sub-menu（手風琴 unique-opened）；沒有子模組的（A／I）是葉節點。
 * `implemented: false` 的項目點進去會看到 PlaceholderView（「這個模組還沒做」），不是死連結。
 */
export const NAV_GROUPS: NavGroup[] = [
  {
    groupLabel: '總覽',
    modules: [{ code: 'A', label: '儀表板', path: '/dashboard', implemented: true }],
  },
  {
    groupLabel: '內容與網站',
    modules: [
      {
        code: 'B',
        label: '內容管理',
        children: [
          { code: 'B1', label: '頁面管理', path: '/content/pages', implemented: true },
          { code: 'B2', label: '新聞與故事', path: '/content/news', implemented: true },
          { code: 'B3', label: '首頁編排', path: '/content/homepage', implemented: true },
          { code: 'B4', label: '常見問題', path: '/content/faq', implemented: true },
          { code: 'B5', label: '慈善與社會影響', path: '/content/charity', implemented: false },
          { code: 'B6', label: '媒體專區', path: '/content/media', implemented: false },
        ],
      },
      {
        code: 'H',
        label: '搜尋與 AI 能見度',
        // ⚠️ 這整組只有系統管理員看得到（AppSidebar.vue 的 SYSADMIN_ONLY_MODULE_CODES）——
        // 五個子模組的權限碼（`seo.setting.*`／`seo.redirect.*`／`seo.report.view`／`seo.llms.*`／
        // `seo.crawler.*`）皆為 `sysadmin_only`（apps/api/README.md「S1-12」「S1-12a」「S1-12b」
        // 各節「權限碼」），比照 J 系統管理整組的既有做法。
        children: [
          { code: 'H1', label: '全站設定', path: '/seo/settings', implemented: true },
          { code: 'H2', label: '301 轉址', path: '/seo/redirects', implemented: true },
          { code: 'H3', label: '孤立頁面偵測', path: '/seo/orphan-pages', implemented: true },
          { code: 'H4', label: 'AI 摘要資料', path: '/seo/llms-content', implemented: true },
          { code: 'H5', label: 'AI 爬蟲授權', path: '/seo/crawler-settings', implemented: true },
        ],
      },
      { code: 'I', label: '網站設定', path: '/settings/site', implemented: false },
      {
        code: 'L',
        label: '行事曆管理',
        children: [
          { code: 'L1', label: '總覽', path: '/calendar/overview', implemented: true },
          { code: 'L2', label: '自建事件', path: '/calendar/events', implemented: true },
          { code: 'L3', label: '分類設定', path: '/calendar/categories', implemented: false },
          { code: 'L4', label: '訂閱與匯出', path: '/calendar/subscriptions', implemented: false },
        ],
      },
    ],
  },
  {
    groupLabel: '球隊與活動',
    modules: [
      {
        code: 'C',
        label: '球隊管理',
        children: [
          { code: 'C1', label: '球隊', path: '/teams/clubs', implemented: true },
          { code: 'C2', label: '球員', path: '/teams/players', implemented: true },
          { code: 'C3', label: '教練與團隊成員', path: '/teams/staff', implemented: true },
          // C4「賽程與賽果」（S1-8）：賽事本身（日期、比分、進球者、卡牌、出賽名單）＋積分榜，
          // 逐字對照主站規劃書 §4.3 C4。「賽事系列」是這個模組底下的支援型別（賽季分類，
          // 例如企業甲級聯賽），S1-4 就先做出來，這裡沿用同一個模組代號但分成三個獨立畫面，
          // 不硬塞進同一頁——三者的操作頻率與資料形狀差異太大（前者逐場維護、後者整季表格、
          // 支援型別偶爾才新增一筆）。
          { code: 'C4', label: '賽程與賽果', path: '/teams/matches', implemented: true },
          { code: 'C4', label: '積分榜', path: '/teams/standings', implemented: true },
          { code: 'C4', label: '賽事系列', path: '/teams/competitions', implemented: true },
          { code: 'C5', label: '榮譽與里程碑', path: '/teams/honours', implemented: false },
        ],
      },
      {
        code: 'P',
        label: '課程與活動',
        children: [
          { code: 'P1', label: '項目', path: '/programs/items', implemented: true },
          { code: 'P2', label: '梯次', path: '/programs/sessions', implemented: true },
          { code: 'P3', label: '報名', path: '/programs/enrollments', implemented: true },
          { code: 'P4', label: '試訓場次', path: '/programs/trials', implemented: false },
        ],
      },
    ],
  },
  {
    groupLabel: '會員與商店',
    modules: [
      {
        code: 'K',
        label: '會員管理',
        children: [
          { code: 'K1', label: '名單', path: '/members/list', implemented: false },
          { code: 'K2', label: '會籍與方案', path: '/members/plans', implemented: false },
          { code: 'K3', label: '球衣發放', path: '/members/jerseys', implemented: false },
          { code: 'K4', label: '特約店家與權益', path: '/members/partner-stores', implemented: false },
          { code: 'K5', label: '抽獎名單管理', path: '/members/lottery', implemented: false },
        ],
      },
      {
        code: 'S',
        label: '商店',
        children: [
          { code: 'S1', label: '商品與規格', path: '/shop/products', implemented: false },
          { code: 'S2', label: '庫存', path: '/shop/inventory', implemented: false },
          { code: 'S3', label: '訂單', path: '/shop/orders', implemented: false },
          { code: 'S4', label: '出貨與物流', path: '/shop/shipping', implemented: false },
          { code: 'S5', label: '退貨與退款', path: '/shop/returns', implemented: false },
          { code: 'S6', label: '設定與報表', path: '/shop/settings', implemented: false },
        ],
      },
    ],
  },
  {
    groupLabel: '商業與社群',
    modules: [
      {
        code: 'E',
        label: '商業模組',
        children: [
          { code: 'E1', label: '夥伴', path: '/business/partners', implemented: false },
          { code: 'E2', label: '贊助', path: '/business/sponsorships', implemented: false },
          { code: 'E3', label: '提案下載', path: '/business/proposals', implemented: false },
          { code: 'E4', label: '廣告主與版位', path: '/business/advertisers', implemented: false },
          { code: 'E5', label: '投放檔期', path: '/business/campaigns', implemented: false },
          { code: 'E6', label: '成效報表', path: '/business/ad-reports', implemented: false },
        ],
      },
      {
        code: 'F',
        label: '文化模組',
        children: [
          { code: 'F1', label: '漫畫', path: '/culture/manga', implemented: false },
          { code: 'F2', label: '球迷會活動', path: '/culture/fan-events', implemented: false },
        ],
      },
      {
        code: 'G',
        label: '表單與詢問',
        children: [
          { code: 'G1', label: '設計器', path: '/inquiries/builder', implemented: true },
          { code: 'G2', label: '收件匣', path: '/inquiries/inbox', implemented: true },
          { code: 'G3', label: '電子報', path: '/inquiries/newsletter', implemented: false },
        ],
      },
    ],
  },
  {
    groupLabel: 'App 與系統',
    modules: [
      {
        code: 'M',
        label: '行動 App',
        children: [
          { code: 'M1', label: '版本發布', path: '/app/releases', implemented: false },
          { code: 'M2', label: '內容編排', path: '/app/content', implemented: false },
          { code: 'M3', label: '推播', path: '/app/push', implemented: false },
          { code: 'M4', label: '推播裝置', path: '/app/devices', implemented: false },
          { code: 'M5', label: 'App 設定與連線檢查', path: '/app/settings', implemented: false },
        ],
      },
      {
        code: 'J',
        label: '系統管理',
        // ⚠️ 這整組只有系統管理員看得到——AppSidebar.vue 會依登入者的 isSuperAdmin 整組濾掉，
        // 不是靠這裡的 implemented 旗標控制可見度（那個旗標只管「做了沒」）。
        children: [
          { code: 'J1', label: '帳號', path: '/system/accounts', implemented: true },
          { code: 'J2', label: '角色與權限', path: '/system/roles', implemented: true },
          { code: 'J3', label: '稽核與備份', path: '/system/audit', implemented: false },
          { code: 'J4', label: '俱樂部與授權管理', path: '/system/clubs', implemented: true },
        ],
      },
    ],
  },
]

/** 攤平所有子模組（路由設定用），沒有子模組的模組視同自己是一筆「子模組」 */
export const ALL_NAV_ITEMS: NavChild[] = NAV_GROUPS.flatMap((group) =>
  group.modules.flatMap((mod: NavModule) =>
    mod.children ?? [{ code: mod.code, label: mod.label, path: mod.path!, implemented: mod.implemented ?? false }],
  ),
)

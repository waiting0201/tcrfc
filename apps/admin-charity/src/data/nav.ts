import type { NavItem } from '@/types/nav'

/**
 * 側欄導覽（docs/22-charity-ui.md §3.3）：單列頂欄 ＋ 平鋪清單側欄，7 個一級模組不分組、
 * 不做手風琴子選單——與 apps/admin 的 14 模組／6 分組刻意不同，理由見 docs/22 §3.2。
 */
export const NAV_ITEMS: NavItem[] = [
  { code: 'N1', label: '店家與 QR', path: '/stores', frontendUnit: '掃碼落地頁' },
  { code: 'N2', label: '捐款項目', path: '/projects', frontendUnit: '項目詳情頁' },
  { code: 'N3', label: '捐款紀錄', path: '/donations', frontendUnit: '捐款表單／結果頁' },
  { code: 'N4', label: '回饋金結算', path: '/settlements', frontendUnit: '（無對應前台頁面，內部帳務作業）' },
  { code: 'N5', label: '發票與收據', path: '/invoices', frontendUnit: '結果頁（發票／收據）' },
  { code: 'N6', label: '捐款報表', path: '/reports', frontendUnit: '（無對應前台頁面，內部報表）' },
  { code: 'N7', label: '站台設定', path: '/settings', frontendUnit: '站台文案、系統信、稽核紀錄' },
]

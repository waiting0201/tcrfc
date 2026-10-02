import type { NavItem } from '@/types/nav'

/**
 * 側欄導覽（docs/22-charity-ui.md §3.3）：單列頂欄 ＋ 平鋪清單側欄，7 個一級模組不分組、
 * 不做手風琴子選單——與 apps/admin 的 14 模組／6 分組刻意不同，理由見 docs/22 §3.2。
 * `anyOf` 是各模組「檢視」端點的權限碼：沒有任一個就不顯示該選單項目（系統管理員全部持有）。
 */
export const NAV_ITEMS: NavItem[] = [
  { code: 'N1', label: '店家與 QR', path: '/stores', frontendUnit: '掃碼落地頁', anyOf: ['n1.donation_store.view'] },
  { code: 'N2', label: '捐款項目', path: '/projects', frontendUnit: '項目詳情頁', anyOf: ['n2.donation_project.view'] },
  { code: 'N3', label: '捐款紀錄', path: '/donations', frontendUnit: '捐款表單／結果頁', anyOf: ['n3.donation.view'] },
  { code: 'N4', label: '回饋金結算', path: '/settlements', frontendUnit: '（無對應前台頁面，內部帳務作業）', anyOf: ['n4.settlement.view'] },
  { code: 'N5', label: '發票與收據', path: '/invoices', frontendUnit: '結果頁（發票／收據）', anyOf: ['n5.donation_invoice.view'] },
  { code: 'N6', label: '捐款報表', path: '/reports', frontendUnit: '（無對應前台頁面，內部報表）', anyOf: ['n6.report.view'] },
  {
    code: 'N7',
    label: '站台設定',
    path: '/settings',
    frontendUnit: '站台文案、系統信、徵信名單',
    anyOf: ['n7.setting.view', 'n7.payment_channel.manage', 'n7.audit_log.view'],
  },
]

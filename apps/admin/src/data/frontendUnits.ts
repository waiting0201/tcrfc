import type { FrontendUnitInfo } from '@/types/frontendUnit'

/**
 * 模組代號 → 前台單元對照，逐字取自規劃書（output/TCRFC_前後台功能規劃書.md）
 * §4.0「前後台對照表（v3.7 新增）」。**只有這份對照表是真實來源**，不得自行另外編。
 *
 * key 是模組代號，只作程式內部查表用，不會顯示在畫面上（畫面只會顯示 label）。
 */
export const FRONTEND_UNIT_MAP: Record<string, FrontendUnitInfo> = {
  A: { linkType: 'none', label: '無前台產出（後台自用）' },

  B1: { linkType: 'multi', label: '多個靜態頁面', url: '/zh/sitemap/' },
  B2: { linkType: 'link', label: '新聞與故事', url: '/zh/news/' },
  B3: { linkType: 'link', label: '首頁', url: '/zh/' },
  B4: { linkType: 'link', label: '常見問題', url: '/zh/faq/' },
  B5: { linkType: 'link', label: '慈善與社會影響', url: '/zh/charity/' },
  B6: { linkType: 'link', label: '媒體專區', url: '/zh/news/media/' },

  C1: { linkType: 'link', label: '一線隊／學院梯隊', url: '/zh/club/' },
  C2: { linkType: 'link', label: '一線隊／學院梯隊的球員頁', url: '/zh/club/players/' },
  C3: { linkType: 'link', label: '一線隊／學院梯隊的教練與團隊介紹', url: '/zh/club/staff/' },
  C4: { linkType: 'link', label: '賽事行事曆', url: '/zh/schedule/' },
  C5: { linkType: 'link', label: '關於台中磐石', url: '/zh/about/' },

  P1: { linkType: 'link', label: '課程與活動', url: '/zh/programs/' },
  P2: { linkType: 'link', label: '課程與活動', url: '/zh/programs/' },
  P3: { linkType: 'app', label: '課程與活動的報名（資料進後台，前台只負責送出）' },
  P4: { linkType: 'app', label: '試訓（資料進後台，前台只負責送出）' },

  E1: { linkType: 'link', label: '合作夥伴與贊助、首頁夥伴標誌牆、頁尾', url: '/zh/partners/' },
  E2: { linkType: 'link', label: '合作夥伴與贊助', url: '/zh/partners/' },
  E3: { linkType: 'link', label: '合作夥伴與贊助', url: '/zh/partners/' },
  E4: { linkType: 'app', label: '行動 App 的廣告版位（網頁前台不設固定版位）' },
  E5: { linkType: 'app', label: '行動 App 的廣告版位（網頁前台不設固定版位）' },
  E6: { linkType: 'app', label: '行動 App 的廣告版位（網頁前台不設固定版位）' },

  F1: { linkType: 'link', label: '台中磐石文化', url: '/zh/culture/manga/' },
  F2: { linkType: 'link', label: '台中磐石文化', url: '/zh/culture/' },

  G1: { linkType: 'app', label: '加入／聯絡我們的表單（資料進後台，前台只負責送出）' },
  G2: { linkType: 'app', label: '加入／聯絡我們的表單（資料進後台，前台只負責送出）' },
  G3: { linkType: 'link', label: '頁尾訂閱區塊', url: '/zh/' },

  H: { linkType: 'multi', label: '全站（不對應單一頁面）', url: '/zh/sitemap/' },
  I: { linkType: 'multi', label: '全站導覽、頁尾、聯絡資訊', url: '/zh/' },

  J1: { linkType: 'none', label: '無前台產出（後台自用）' },
  J2: { linkType: 'none', label: '無前台產出（後台自用）' },
  J3: { linkType: 'none', label: '無前台產出（後台自用）' },
  J4: { linkType: 'none', label: '無前台產出（後台自用）' },

  K1: { linkType: 'link', label: '會員中心、電子會員卡與公開驗證頁', url: '/zh/member/' },
  K2: { linkType: 'link', label: '會員中心、電子會員卡與公開驗證頁', url: '/zh/member/' },
  K3: { linkType: 'link', label: '會員中心、電子會員卡與公開驗證頁', url: '/zh/member/' },
  K4: { linkType: 'link', label: '特約店家清單、會員權益對照表', url: '/zh/culture/partner-stores/' },
  K5: { linkType: 'link', label: '會員中心、電子會員卡與公開驗證頁', url: '/zh/member/' },

  L1: { linkType: 'link', label: '賽事行事曆（訂閱與匯出）', url: '/zh/schedule/' },
  L2: { linkType: 'link', label: '賽事行事曆（訂閱與匯出）', url: '/zh/schedule/' },
  L3: { linkType: 'link', label: '賽事行事曆（訂閱與匯出）', url: '/zh/schedule/' },
  L4: { linkType: 'link', label: '賽事行事曆（訂閱與匯出）', url: '/zh/schedule/' },

  M1: { linkType: 'app', label: '行動 App（非網頁前台）' },
  M2: { linkType: 'app', label: '行動 App（非網頁前台）' },
  M3: { linkType: 'app', label: '行動 App（非網頁前台）' },
  M4: { linkType: 'app', label: '行動 App（非網頁前台）' },
  M5: { linkType: 'app', label: '行動 App（非網頁前台）' },

  S1: { linkType: 'link', label: '站內商店、會員中心的我的訂單', url: '/zh/shop/' },
  S2: { linkType: 'link', label: '站內商店', url: '/zh/shop/' },
  S3: { linkType: 'link', label: '站內商店、會員中心的我的訂單', url: '/zh/member/orders/' },
  S4: { linkType: 'link', label: '站內商店、會員中心的我的訂單', url: '/zh/member/orders/' },
  S5: { linkType: 'link', label: '站內商店、會員中心的我的訂單', url: '/zh/member/orders/' },
  S6: { linkType: 'link', label: '站內商店', url: '/zh/shop/' },
}

export function getFrontendUnit(moduleCode: string): FrontendUnitInfo {
  return FRONTEND_UNIT_MAP[moduleCode] ?? { linkType: 'none', label: '無前台產出（後台自用）' }
}

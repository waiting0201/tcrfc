/**
 * B3 首頁編排的畫面型別。對照 `apps/api` 的 `Features/AdminBanners`／`Features/AdminHomeSections`
 * （見 apps/api/README.md「S1-6」「S1-7a」）。
 */

/** 🔴 本輪只允許圖片——影片的格式、大小上限與是否轉碼待裁決（apps/api/README.md「我的判斷」第 6 點），
 * 畫面上固定顯示為圖片，不提供切換到影片的選項（送出也一律被後端拒絕）。 */
export type BannerMediaType = 'image' | 'video'

export interface BannerLocaleContent {
  title: string
  subtitle: string
  imageAlt: string
  cta1Label: string
  cta1Url: string
  cta2Label: string
  cta2Url: string
}

export interface Banner {
  id: string
  mediaType: BannerMediaType
  imageKey: string
  imageWidth: number | null
  imageHeight: number | null
  videoKey: string | null
  startAt: string | null
  endAt: string | null
  sortOrder: number
  zh: BannerLocaleContent
  en: BannerLocaleContent
  updatedAt: string
}

/**
 * 首頁九大區塊（`HomeSectionCatalog`，主站規劃書 §3.1）。**固定列舉，不能新增或刪除**——
 * 九個代碼是前台首頁固定的九個位置，見 apps/api/README.md「Banners」段落。
 */
export const HOME_SECTION_CODE_ORDER = [
  'hero',
  'core_values',
  'ecosystem_nav',
  'upcoming_match',
  'recent_fixtures',
  'latest_news',
  'partner_logos',
  'shop_entry',
  'bottom_cta',
] as const
export type HomeSectionCode = (typeof HOME_SECTION_CODE_ORDER)[number]

export interface HomeSection {
  id: string
  sectionCode: HomeSectionCode | string
  nameZh: string
  nameEn: string
  isEnabled: boolean
  sortOrder: number
  featuredBannerId: string | null
  updatedAt: string
}

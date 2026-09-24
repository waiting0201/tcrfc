/**
 * B3 首頁編排的畫面型別。對照 `apps/api` 的 `Features/AdminBanners`／`Features/AdminHomeSections`
 * （見 apps/api/README.md「S1-6」「S1-7a」）。
 */

/** v3.14 起圖片、影片皆可（apps/api/README.md「S1-7b」）。影片模式仍必須搭配一張圖片
 * 作為海報格（`<video poster>`），格式僅收 MP4、上限 50 MB，見 `docs/17-deployment.md` §6。 */
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

/** 草稿／發布（v3.14）。新增一律是 `draft`；`published` 且落在 `startAt`／`endAt` 上架期間內
 * 才會出現在公開端點——期間判斷在後端，畫面上只做提示用途。 */
export type BannerStatus = 'draft' | 'published'

export interface Banner {
  id: string
  mediaType: BannerMediaType
  imageKey: string
  imageWidth: number | null
  imageHeight: number | null
  videoKey: string | null
  status: BannerStatus
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

import type { Bilingual, ContentStatus } from './common'

/**
 * 分類代碼與中文名稱逐字取自 `db/seed/generate-club-seed-sql.py` 的 `CATEGORIES`
 * （article_categories 的真實種子資料）與規劃書 §3.7「07 NEWS & STORIES」7.1–7.8。
 * ⚠️ v1 mockup 原本寫的是另一組憑感覺編的分類（match／academy／business／charity／club／
 * culture／general），跟資料庫實際的 `article_categories.code` 完全對不起來——接上真實 API 後
 * 送這組值會被 `AdminArticleValidationException` 擋下（400「找不到分類代碼」），本輪已改為
 * 與資料庫一致的 8 個真實代碼。
 */
export type NewsCategory =
  | 'club' // 7.1 俱樂部新聞
  | 'match' // 7.2 比賽報導
  | 'academy' // 7.3 學院新聞
  | 'player-stories' // 7.4 球員故事
  | 'international' // 7.5 國際動態
  | 'camps-events' // 7.6 營隊與活動
  | 'community' // 7.7 社區活動
  | 'media' // 7.8 媒體專區

export const NEWS_CATEGORY_LABEL: Record<NewsCategory, string> = {
  club: '俱樂部新聞',
  match: '比賽報導',
  academy: '學院新聞',
  'player-stories': '球員故事',
  international: '國際動態',
  'camps-events': '營隊與活動',
  community: '社區活動',
  media: '媒體專區',
}

/**
 * 後台新聞編輯頁目前不暴露編輯介面、但 API 會整份取代的欄位（`AdminArticleLocaleContent`
 * 的 `Summary`／`SeoTitle`／`SeoDescription`）。畫面上沒有對應輸入框，讀回來就原封存著、
 * 存檔時原封送回去，避免「畫面沒有這個欄位＝存檔時把它清空」——docs/21 的編輯頁 wireframe
 * 目前只定義了基本資訊／內容／封面圖片／發布設定四段，沒有摘要與 SEO 欄位的畫面規格，
 * 這裡刻意不自行加規格外的輸入框（CLAUDE.md 全域規定 2），只做到「不遺失資料」。
 */
export interface NewsArticle {
  id: string
  title: Bilingual
  /** 網址名稱（畫面翻譯自 API 的 slug，見 docs/06 §1） */
  urlName: string
  category: NewsCategory
  /** 目前恆為 null（見 api/adminNews.ts 的 detailDtoToArticle 說明），保留欄位給日後補上
   * 「用 coverKey 換可顯示網址」的機制時使用，ImageUploader.vue 的 existingPreviewUrl 已經接好。 */
  coverImageUrl: string | null
  /** 對應 API 的 coverKey，圖片上傳共用元件（S0-8）的物件鍵，見 apps/api/README.md */
  coverKey: string | null
  /** 置頂精選（規劃書§3.7、docs/03-admin-spec.md「置頂精選（限 3）」），逐俱樂部最多 3 篇 */
  isFeatured: boolean
  status: ContentStatus
  statusAt?: string
  statusBy?: string
  /** 是否屬於兩隊共用內容（club_id 為空），docs/21 §5。共用內容一律唯讀 */
  isSharedContent: boolean
  /** API 回傳的原始 ISO 8601 時間字串，做為下一次寫入的並行權杖，不做任何格式轉換 */
  updatedAt: string
  content: Bilingual
  /** 畫面上沒有對應輸入框，見上方說明 */
  summary: Bilingual
  seoTitle: Bilingual
  seoDescription: Bilingual
}

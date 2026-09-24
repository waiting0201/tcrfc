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
 * 標籤（S1-5，apps/api/README.md「B2 新聞與故事後端補完」）。`slug` 是資料庫的識別碼
 * （小寫英文字母＋連字號），畫面上不直接要求使用者輸入這串——使用者只看得到／打得出中文名稱，
 * `slug` 由畫面自動產生或沿用既有標籤的值，見 `NewsEditView.vue` 的 `resolveTagInput`。
 */
export interface NewsTag {
  slug: string
  nameZh?: string | null
  nameEn?: string | null
}

/**
 * 核心價值標籤（S1-5）。值域逐字對應後端 `value_tag_links.value_tag` 的 CHECK 約束
 * （`AdminArticlesRepository.AllowedCoreValueTags`），中文名稱取自規劃書 §1.2／
 * docs/06-conventions.md §1「五大核心價值」。
 */
export type CoreValueTag = 'players_first' | 'excellence' | 'global_pathways' | 'community' | 'integrity'

export const CORE_VALUE_TAG_ORDER: CoreValueTag[] = [
  'players_first',
  'excellence',
  'global_pathways',
  'community',
  'integrity',
]

export const CORE_VALUE_TAG_LABEL: Record<CoreValueTag, string> = {
  players_first: '以球員為本',
  excellence: '追求卓越',
  global_pathways: '國際發展',
  community: '社區共好',
  integrity: '誠信專業',
}

/**
 * 關聯目標型別（S1-5，規劃書 B2「關聯（球員／球隊／賽事／課程／夥伴）」，逐字對應後端
 * `AdminArticlesRepository.AllowedRelationTargetTypes`）。
 */
export type RelationTargetType = 'player' | 'team' | 'match' | 'program' | 'partner'

export const RELATION_TARGET_TYPE_ORDER: RelationTargetType[] = ['player', 'team', 'match', 'program', 'partner']

export const RELATION_TARGET_TYPE_LABEL: Record<RelationTargetType, string> = {
  player: '球員',
  team: '球隊',
  match: '賽事',
  program: '課程',
  partner: '夥伴',
}

/**
 * ⚠️ 這裡列出「畫面上真的查得到清單」的類型——**課程**與**夥伴**目前沒有一份這個帳號能查詢的
 * 唯讀清單可以拿來做選擇器（後端根本還沒有對應模組，沒有 `Features/Programs`／
 * `Features/Partners`），見 apps/admin/README.md「已知的 API 缺口」與 apps/api/README.md
 * 「S1-5」。**球隊**已於 S1-7 續作解除：`GET /api/v1/admin/{club}/teams`（權限碼
 * `team.team.view`，C1 俱樂部範圍端點）授予了寫新聞的唯讀角色（見
 * `src/api/adminRelationTargets.ts` 檔頭的完整說明），不再是先前回報的
 * `system.team_grant.view` 系統管理員限定端點。**不要因為想讓功能看起來完整就自己拼一份
 * 清單或改用假資料**——未列在這裡的類型，畫面上顯示為停用選項並附說明文字，不假裝有得選。
 */
export const RELATION_TARGET_TYPES_AVAILABLE: RelationTargetType[] = ['player', 'team', 'match']

/**
 * 單筆關聯。`targetLabel` 純粹是畫面顯示用的暫存欄位，**不送給 API**（後端的
 * `AdminArticleRelationInput` 只有 `targetType`／`targetId` 兩個欄位，沒有名稱）——
 * 儲存讀回後，名稱要重新從對應的清單（球員／賽事）用 `targetId` 反查回來顯示，
 * 見 `NewsEditView.vue` 的 `resolveRelationLabel`。
 */
export interface NewsRelation {
  targetType: RelationTargetType
  targetId: string
  targetLabel?: string
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
  /** 標籤（S1-5） */
  tags: NewsTag[]
  /** 核心價值標籤（S1-5） */
  coreValueTags: CoreValueTag[]
  /** 關聯（S1-5） */
  relations: NewsRelation[]
  /** 瀏覽數（S1-5，唯讀，只由公開端點遞增，見 apps/api/README.md） */
  viewCount: number
}

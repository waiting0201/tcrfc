/**
 * B1 頁面管理的區塊化編輯器——12 種區塊型別的畫面狀態，對照
 * `apps/api/README.md`「12 種區塊的欄位與驗證摘要」與 `Features/AdminPages/PageBlockTypes.cs`／
 * `PageBlockContentProcessor.cs`。逐字數過規劃書 §4.2 B1（約行 1013）列出的區塊名稱只有 12 個，
 * 本檔只做這 12 種，不自創第 13 種（apps/api 那邊已回報過同一個落差，見上述 README）。
 *
 * ⚠️ 這裡的型別是「畫面狀態」，不是直接送給後端的 JSON 形狀——差異集中在圖片欄位
 * （見 `ImageSlotState`）：後端只認得 `{pendingUpload:true,...}` 或 `{key,width,height,...}`
 * 兩種形狀之一，畫面上需要同時記得「這次瀏覽階段選的新檔案」與「既有的物件鍵」才能實作
 * 「選檔不上傳、儲存才上傳」，序列化與還原邏輯見 `@/utils/pageBlockSerializer.ts`。
 */

export type PageBlockType =
  | 'text'
  | 'text_image'
  | 'gallery'
  | 'video_embed'
  | 'quote'
  | 'cta'
  | 'accordion_faq'
  | 'timeline'
  | 'steps'
  | 'stat_cards'
  | 'table'
  | 'file_download'

/** 12 種區塊型別的中文名稱——逐字取自規劃書 §4.2 B1，畫面上只顯示這個，不顯示英文代碼
 * （規劃書 §4.0「代號不進介面」；`CTA`／`FAQ` 兩詞是規劃書原文用字，不是英文技術詞，
 * 見 `docs/06-conventions.md` §1 對照表左欄——那份清單沒有這兩個詞）。 */
export const PAGE_BLOCK_TYPE_LABEL: Record<PageBlockType, string> = {
  text: '文字',
  text_image: '圖文左右',
  gallery: '圖片藝廊',
  video_embed: '影音嵌入',
  quote: '引言',
  cta: 'CTA',
  accordion_faq: '手風琴 FAQ',
  timeline: '時間軸',
  steps: '步驟條',
  stat_cards: '數據卡',
  table: '表格',
  file_download: '檔案下載',
}

export const PAGE_BLOCK_TYPES: PageBlockType[] = [
  'text', 'text_image', 'gallery', 'video_embed', 'quote', 'cta',
  'accordion_faq', 'timeline', 'steps', 'stat_cards', 'table', 'file_download',
]

/** 雙語文字，`en` 可空但欄位必須存在（CLAUDE.md 全域規定 4）。畫面上永遠是字串（空字串代表未填），
 * 序列化時才決定要不要送出 `en`（見 `pageBlockSerializer.ts`）。 */
export interface BilingualText {
  zh: string
  en: string
}

export function emptyBilingualText(): BilingualText {
  return { zh: '', en: '' }
}

/**
 * 單一圖片欄位的畫面狀態，對照 `PageBlockContentProcessor.ResolveSingleImageAsync`：
 * - `existingKey` 有值且 `cleared` 為否、`file` 為空 ＝ 沿用既有圖片（後端形狀 `{key,width,height,...}`）。
 * - `file` 有值 ＝ 這次瀏覽階段選了新檔案，尚未上傳（後端形狀 `{pendingUpload:true,...}`）。
 * - 兩者皆無（含使用者按下移除既有圖片、`cleared=true` 但還沒選新檔案）＝ 存檔前必須擋下，
 *   提示使用者上傳一張圖片（image／gallery 每張圖都是必填，不像新聞封面圖可以整個留空）。
 *
 * 沿用既有 `ImageUploader.vue` 的欄位命名慣例（`file`／`removeCover`）方便直接重用該元件：
 * `hasExistingImage = !!existingKey`、`v-model:file="slot.file"`、`v-model:remove-cover="slot.cleared"`。
 */
export interface ImageSlotState {
  existingKey: string | null
  existingWidth: number | null
  existingHeight: number | null
  file: File | null
  cleared: boolean
  altZh: string
  altEn: string
}

export function emptyImageSlot(): ImageSlotState {
  return { existingKey: null, existingWidth: null, existingHeight: null, file: null, cleared: false, altZh: '', altEn: '' }
}

export interface TextBlockContent {
  body: BilingualText
}

export interface TextImageBlockContent {
  body: BilingualText
  imagePosition: 'left' | 'right'
  image: ImageSlotState
}

export interface GalleryBlockContent {
  images: ImageSlotState[]
}

export interface VideoEmbedBlockContent {
  provider: 'youtube' | 'vimeo'
  videoId: string
  /** 可選——整個欄位可以留空不送出，見 `pageBlockSerializer.ts` 的 `serializeOptionalBilingual`。 */
  caption: BilingualText
}

export interface QuoteBlockContent {
  text: BilingualText
  attribution: BilingualText
}

export interface CtaBlockContent {
  text: BilingualText
  buttonLabel: BilingualText
  buttonUrl: string
}

export interface AccordionFaqItem {
  question: BilingualText
  answer: BilingualText
}

export interface AccordionFaqBlockContent {
  items: AccordionFaqItem[]
}

export interface TimelineItem {
  /** 日期本身不分語言，維持單一純值（CLAUDE.md 全域規定 4 的例外：網址、識別碼、日期、數值）。 */
  date: string
  title: BilingualText
  description: BilingualText
}

export interface TimelineBlockContent {
  items: TimelineItem[]
}

export interface StepsItem {
  title: BilingualText
  description: BilingualText
}

export interface StepsBlockContent {
  items: StepsItem[]
}

export interface StatCardItem {
  /** 數據值本身不分語言。 */
  value: string
  label: BilingualText
}

export interface StatCardsBlockContent {
  items: StatCardItem[]
}

export interface TableBlockContent {
  headers: BilingualText[]
  rows: string[][]
}

export interface FileDownloadBlockContent {
  label: BilingualText
  fileUrl: string
}

export type PageBlockContent =
  | TextBlockContent
  | TextImageBlockContent
  | GalleryBlockContent
  | VideoEmbedBlockContent
  | QuoteBlockContent
  | CtaBlockContent
  | AccordionFaqBlockContent
  | TimelineBlockContent
  | StepsBlockContent
  | StatCardsBlockContent
  | TableBlockContent
  | FileDownloadBlockContent

/** 畫面上一個區塊的完整狀態。`localKey` 只給 `v-for` 當 `:key` 用，不會送給後端
 * （後端的區塊沒有穩定 id 可以在建立前先取得，新增／排序／刪除全部靠整份陣列送出，
 * 見 apps/api/README.md「我的判斷」）。 */
export interface PageBlockState {
  localKey: string
  blockType: PageBlockType
  content: PageBlockContent
}

function randomLocalKey(): string {
  return typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : `local-${Date.now()}-${Math.random().toString(36).slice(2)}`
}

export function createEmptyBlockContent(type: PageBlockType): PageBlockContent {
  switch (type) {
    case 'text':
      return { body: emptyBilingualText() } satisfies TextBlockContent
    case 'text_image':
      return { body: emptyBilingualText(), imagePosition: 'left', image: emptyImageSlot() } satisfies TextImageBlockContent
    case 'gallery':
      return { images: [emptyImageSlot()] } satisfies GalleryBlockContent
    case 'video_embed':
      return { provider: 'youtube', videoId: '', caption: emptyBilingualText() } satisfies VideoEmbedBlockContent
    case 'quote':
      return { text: emptyBilingualText(), attribution: emptyBilingualText() } satisfies QuoteBlockContent
    case 'cta':
      return { text: emptyBilingualText(), buttonLabel: emptyBilingualText(), buttonUrl: '' } satisfies CtaBlockContent
    case 'accordion_faq':
      return { items: [{ question: emptyBilingualText(), answer: emptyBilingualText() }] } satisfies AccordionFaqBlockContent
    case 'timeline':
      return { items: [{ date: '', title: emptyBilingualText(), description: emptyBilingualText() }] } satisfies TimelineBlockContent
    case 'steps':
      return { items: [{ title: emptyBilingualText(), description: emptyBilingualText() }] } satisfies StepsBlockContent
    case 'stat_cards':
      return { items: [{ value: '', label: emptyBilingualText() }] } satisfies StatCardsBlockContent
    case 'table':
      return { headers: [emptyBilingualText()], rows: [['']] } satisfies TableBlockContent
    case 'file_download':
      return { label: emptyBilingualText(), fileUrl: '' } satisfies FileDownloadBlockContent
  }
}

export function createEmptyBlock(type: PageBlockType): PageBlockState {
  return { localKey: randomLocalKey(), blockType: type, content: createEmptyBlockContent(type) }
}

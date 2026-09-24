import type { Bilingual } from './common'

/**
 * B4 常見問題的畫面型別。對照 `apps/api` 的 `Features/AdminFaqs`（見 apps/api/README.md
 * 「S1-6」「S1-6 續作」「S1-7a」）。
 */

export interface FaqCategoryRef {
  id: string
  slug: string
  name: Bilingual
}

export interface FaqCategory {
  id: string
  slug: string
  sortOrder: number
  /** 軟停用（S1-7a）。停用只讓分類從公開導覽消失，既有題目與關聯不受影響，可隨時重新啟用。
   * 🔴 這已經不是「刪除＝停用」——刪除是真的刪除，見清單頁的刪除按鈕。 */
  isEnabled: boolean
  name: Bilingual
  /** 目前掛在這個分類底下的題目筆數，僅供刪除前提醒用。 */
  faqCount: number
  updatedAt: string
}

/** G-12 快捷區塊掛載點（S1-7a）：固定 4 筆字典，只做「額外指定」的那一半——
 * 「由分類自動對應」是前台頁面元件的固定路由決定，後端沒有資料可查，見
 * apps/api/README.md「FAQ 嵌入設定」段。 */
export interface FaqEmbedSlot {
  id: string
  code: string
  name: string
}

export type FaqStatus = 'draft' | 'published'

export interface Faq {
  id: string
  slug: string
  sortOrder: number
  status: FaqStatus
  isShared: boolean
  viewCount: number
  helpfulCount: number
  unhelpfulCount: number
  categories: FaqCategoryRef[]
  embedSlots: FaqEmbedSlot[]
  question: Bilingual
  answer: Bilingual
  updatedAt: string
}

export interface BatchOperationSkipped {
  id: string
  reason: string
}

export interface BatchOperationResult {
  updatedCount: number
  skipped: BatchOperationSkipped[]
}

export interface FaqCsvImportRowError {
  rowNumber: number
  reason: string
}

export interface FaqCsvImportResult {
  importedCount: number
  errors: FaqCsvImportRowError[]
}

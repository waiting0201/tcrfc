import type { Bilingual, ContentStatus } from './common'

export type NewsCategory =
  | 'match' // 賽事
  | 'academy' // 學院
  | 'business' // 商業
  | 'charity' // 慈善
  | 'club' // 俱樂部
  | 'culture' // 文化
  | 'general' // 一般

export interface NewsArticle {
  id: string
  title: Bilingual
  /** 網址名稱（畫面翻譯自 slug，見 docs/06 §1） */
  urlName: string
  category: NewsCategory
  coverImageUrl: string | null
  status: ContentStatus
  statusAt?: string
  statusBy?: string
  /** 是否屬於兩隊共用內容（club_id 為空），docs/21 §5 */
  isSharedContent: boolean
  updatedAt: string
  content: Bilingual
  /** 圖片替代文字（畫面翻譯自 alt） */
  coverImageAlt: Bilingual
  /** 不讓搜尋引擎收錄 */
  noIndex: boolean
  /** 正規網址（畫面翻譯自 canonical） */
  canonicalUrl: string
}

export const NEWS_CATEGORY_LABEL: Record<NewsCategory, string> = {
  match: '賽事',
  academy: '學院',
  business: '商業',
  charity: '慈善',
  club: '俱樂部',
  culture: '文化',
  general: '一般',
}

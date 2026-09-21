import { reactive } from 'vue'
import { NEWS_ARTICLES } from './news'
import type { NewsArticle } from '@/types/news'

/**
 * 假資料的「單一真實來源」，讓列表頁與編輯頁共用同一份記憶體資料——
 * 在編輯頁按「儲存」之後，回到列表頁能看到剛剛的變更，不用重新整理頁面。
 * 這不是真正的狀態管理框架（沒有引入 Pinia），純粹是一個模組層級的 reactive 單例，
 * 因為本階段範圍只有一個資料模組，用不到跨模組的狀態管理需求。
 */
export const newsStore = reactive({
  articles: structuredClone(NEWS_ARTICLES) as NewsArticle[],
})

export function getNewsById(id: string): NewsArticle | undefined {
  return newsStore.articles.find((a) => a.id === id)
}

export function upsertNews(article: NewsArticle): void {
  const index = newsStore.articles.findIndex((a) => a.id === article.id)
  if (index >= 0) {
    newsStore.articles[index] = article
  } else {
    newsStore.articles.unshift(article)
  }
}

export function removeNews(id: string): void {
  newsStore.articles = newsStore.articles.filter((a) => a.id !== id)
}

export function createEmptyArticle(): NewsArticle {
  return {
    id: `n-${Date.now()}`,
    title: { zh: '', en: '' },
    urlName: '',
    category: 'general',
    coverImageUrl: null,
    coverImageAlt: { zh: '', en: '' },
    status: 'draft',
    isSharedContent: false,
    updatedAt: new Date().toISOString(),
    content: { zh: '', en: '' },
    noIndex: false,
    canonicalUrl: '',
  }
}

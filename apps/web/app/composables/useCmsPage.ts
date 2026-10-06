// app/composables/useCmsPage.ts — B1 頁面管理「靜態頁讀 CMS」的共用模式（稽核 B-1／B-2，2026-10-06）
//
// 規劃書對照表：頁面管理產出 02 關於、03.2–03.5、9.3、06 入口、11.1 等靜態頁。
// 讀 `GET /api/v1/{club}/pages/{slug}?lang=`（經 BFF `/api/backend`）：
//   · 有「已發布且含可渲染區塊」的頁面 → `active` 為 true，頁面以 `<ContentCmsPageBand>` 取代寫死的主內文；
//   · 404、API 失敗、區塊全是不認得的型別 → `active` 為 false，頁面維持原本寫死內容（fallback，不刪）。
// 後台頁面沒有標題欄位，h1／麵包屑／hero／CTA 一律維持前台固定版面；只換「主內文」。
//
// SEO：CMS 有值才覆寫（title／description／keywords／canonical／OG 圖文）；`isNoindex` 只會讓頁面「更」noindex，
// 全站 noindex（X-Robots-Tag，nuxt.config.ts routeRules）與此互不取代，也不會被取消。
// `slug` 可傳候選清單（依序嘗試，取第一個有可渲染區塊者），用於種子頁 slug 與前台網址不同的情形。
import type { PageBlockNode, RawPageBlock } from '#shared/utils/page-blocks'
import type { MaybeRefOrGetter } from 'vue'

interface RawCmsPage {
  slug?: string
  seoTitle?: string | null
  seoDescription?: string | null
  seoKeywords?: string | null
  canonicalPath?: string | null
  isNoindex?: boolean
  ogImageUrl?: string | null
  ogImageWidth?: number | null
  ogImageHeight?: number | null
  ogImageAlt?: string | null
  blocks?: RawPageBlock[]
}

export interface CmsPageData {
  slug: string
  seoTitle: string | null
  seoDescription: string | null
  seoKeywords: string | null
  canonicalPath: string | null
  isNoindex: boolean
  ogImageUrl: string | null
  ogImageWidth: number | null
  ogImageHeight: number | null
  ogImageAlt: string | null
  blocks: PageBlockNode[]
}

export async function useCmsPage(slug: string | string[]) {
  const config = useRuntimeConfig()
  const club = config.public.club as string
  const mediaBaseUrl = config.public.mediaBaseUrl as string
  const { locale, isEn } = useLocale()
  const slugs = Array.isArray(slug) ? slug : [slug]

  // `transform` 之前就把區塊正規化成安全的純文字節點（原始 JSON 不進頁面 payload）
  const { data } = await useAsyncData<CmsPageData | null>(
    `cms-page-${club}-${slugs.join('|')}-${locale.value}`,
    async () => {
      for (const s of slugs) {
        const raw = await $fetch<RawCmsPage>(`/api/backend/${club}/pages/${s}`, { query: { lang: locale.value } }).catch(() => null)
        if (!raw) continue
        const blocks = normalizePageBlocks(raw.blocks, { locale: locale.value, mediaBaseUrl })
        if (!blocks.length) continue
        return {
          slug: s,
          seoTitle: raw.seoTitle || null,
          seoDescription: raw.seoDescription || null,
          seoKeywords: raw.seoKeywords || null,
          canonicalPath: raw.canonicalPath || null,
          isNoindex: raw.isNoindex === true,
          ogImageUrl: raw.ogImageUrl || null,
          ogImageWidth: raw.ogImageWidth ?? null,
          ogImageHeight: raw.ogImageHeight ?? null,
          ogImageAlt: raw.ogImageAlt || null,
          blocks,
        }
      }
      return null
    },
    { default: () => null },
  )

  const page = computed(() => data.value)
  const active = computed(() => (page.value?.blocks.length ?? 0) > 0)
  const blocks = computed(() => page.value?.blocks ?? [])
  // 英文版：該語系尚無內容時後端回退繁中區塊，含中日文字時提示（normalize 已丟掉備援旗標）
  const hasZhFallback = computed(() => isEn.value && active.value && /[㐀-鿿]/.test(JSON.stringify(blocks.value)))

  /** 套用 SEO：CMS 有值用 CMS，沒值沿用呼叫端傳入的頁面既有設定。 */
  function applySeo(fallback: { title: MaybeRefOrGetter<string>, description: MaybeRefOrGetter<string> }) {
    const cms = <K extends keyof CmsPageData>(k: K) => (active.value ? (page.value?.[k] ?? undefined) : undefined)
    useSeoMeta({
      title: computed(() => (cms('seoTitle') as string | undefined) || toValue(fallback.title)),
      description: computed(() => (cms('seoDescription') as string | undefined) || toValue(fallback.description)),
      ogImage: computed(() => cms('ogImageUrl') as string | undefined),
      ogImageWidth: computed(() => cms('ogImageWidth') as number | undefined),
      ogImageHeight: computed(() => cms('ogImageHeight') as number | undefined),
      ogImageAlt: computed(() => cms('ogImageAlt') as string | undefined),
      // 只能更 noindex：true 才輸出，false／無值不輸出也不取消全站 noindex
      robots: computed(() => (active.value && page.value?.isNoindex ? 'noindex' : undefined)),
    })
    const siteConfig = useSiteConfig()
    useHead({
      // Meta Keywords：useSeoMeta 的型別沒有 keywords，走 useHead 的 meta
      meta: computed(() => {
        const k = cms('seoKeywords') as string | undefined
        return k ? [{ name: 'keywords', content: k }] : []
      }),
      link: computed(() => {
        const path = active.value ? page.value?.canonicalPath : null
        if (!path) return []
        return [{ rel: 'canonical', href: `${(siteConfig.url ?? '').replace(/\/$/, '')}${path}` }]
      }),
    })
  }

  return { page, active, blocks, hasZhFallback, applySeo }
}

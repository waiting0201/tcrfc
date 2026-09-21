<script setup lang="ts">
// app/components/news/NewsListBody.vue — 「全部新聞區塊」的清單本體
// （result-count／news-list-grid／empty state／load more），6 頁共用同一份行為
// （原 mockup 59 行 client script，docs/13-blue-whale-site.md §6 紀律：務必做成
// 一個元件不要複製六次）：news/index（全部 83 篇 + 分類鈕）與 5 個分類頁
// （club／camps-events／community／international／match，年月＋關鍵字篩選，
// 分類已由父層 API 呼叫時過濾好，這裡的 activeCat 固定 'all'）。
//
// 🔴 SSR 一律先渲染「未篩選、未分頁」的完整清單（match mockup 在 JS 執行前的
// 原始 HTML：所有卡片皆可見、result-count 顯示未篩選總數），篩選與「載入更多」
// 的分頁狀態只在 client 掛載後套用（onMounted），避免 SSR 輸出與 site/dist
// 的 <main> 內容不一致（docs/14-invariants.md「Nuxt SSR 輸出必須與 site/dist
// 逐頁正規化零差異」）。
interface NewsListArticle {
  slug: string
  categoryCode: string
  categoryName: string | null
  title: string | null
  publishedAt: string | null
}

const props = withDefaults(
  defineProps<{
    articles: NewsListArticle[]
    /** 'all' = 不依分類篩選（分類已在 API 呼叫時過濾，或 news/index 顯示全部） */
    activeCat: string
    year: string
    month: string
    search: string
    /** news/index 在深色 band 裡，result-count／empty 需要額外的行內樣式；5 個分類頁沒有 */
    dark?: boolean
  }>(),
  { dark: false },
)

// 兩種文案：news/index 有分類鈕（提示可換分類），5 個分類頁沒有分類鈕（提示換年月）——
// 逐字比對 mockup 原文，不是隨意精簡。
const emptyText = computed(() =>
  props.dark ? '這個篩選條件目前沒有符合的文章，換個分類或關鍵字看看。' : '這個篩選條件目前沒有符合的文章，換個年月或關鍵字看看。',
)
// :style="undefined" 在 Vue SSR 仍會印出空字串 style=""（mockup 沒有分類鈕的
// 5 個分類頁完全沒有這個屬性），改用 v-bind 物件展開，物件沒有 style 鍵時才會
// 真的不渲染這個屬性。
const resultCountAttrs = computed(() => (props.dark ? { style: 'color:var(--muted-dark)' } : {}))
const emptyAttrs = computed(() => (props.dark ? { style: 'background:transparent;color:var(--muted-dark)' } : {}))

const PAGE_SIZE = 9
const shown = ref(PAGE_SIZE)
const mounted = ref(false)

watch([() => props.activeCat, () => props.year, () => props.month, () => props.search], () => {
  shown.value = PAGE_SIZE
})

function matches(a: NewsListArticle): boolean {
  if (props.activeCat !== 'all' && a.categoryCode !== props.activeCat) return false
  if (props.year && newsYearAttr(a.publishedAt) !== props.year) return false
  if (props.month && newsMonthAttr(a.publishedAt) !== props.month) return false
  const q = props.search.trim().toLowerCase()
  if (q && !newsTitleAttr(a.title).includes(q)) return false
  return true
}

// SSR／掛載前：完整清單、不套用篩選（比照 mockup 的 JS 執行前狀態）。
const filtered = computed(() => (mounted.value ? props.articles.filter(matches) : props.articles))
const visible = computed(() => (mounted.value ? filtered.value.slice(0, shown.value) : filtered.value))
const visibleSlugs = computed(() => new Set(visible.value.map((a) => a.slug)))
const hasMore = computed(() => mounted.value && filtered.value.length > shown.value)
const isEmpty = computed(() => mounted.value && filtered.value.length === 0)
const resultCount = computed(() => filtered.value.length)

function loadMore() {
  shown.value += PAGE_SIZE
}

onMounted(() => {
  mounted.value = true
})
</script>

<template>
  <p id="result-count" class="result-count" v-bind="resultCountAttrs" aria-live="polite">共 {{ resultCount }} 篇</p>

  <div class="news-list-grid">
    <NewsCard
      v-for="a in articles"
      :key="a.slug"
      :article="a"
      :hidden="mounted && !visibleSlugs.has(a.slug)"
    />
  </div>

  <div id="news-empty" class="news-empty" :hidden="!isEmpty" v-bind="emptyAttrs">{{ emptyText }}</div>

  <div class="load-more-row">
    <button id="load-more" type="button" class="btn btn--light" :hidden="!hasMore" @click="loadMore">載入更多</button>
  </div>
</template>

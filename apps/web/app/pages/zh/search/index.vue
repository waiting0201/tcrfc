<script setup lang="ts">
// app/pages/zh/search/index.vue — G-02 全站搜尋結果頁（H 批，2026-10-02）
//
// 規格：主站規劃書 §3.0 G-02（跨新聞、球員、教練、課程、FAQ、慈善事蹟）。對應 apps/api
// `GET /api/v1/{club}/search?q=&type=&lang=&page=&pageSize=`（apps/api/README.md「H 批」§4，只讀不寫、
// 限流 `public-search` 每 IP 每分鐘 30 次 → 429）。
//
// 🔴 **不得 v-html**：標題與摘錄是純文字，高亮用 `tokens`（伺服器已正規化、拆好）在前端切成文字片段，
// 命中的片段用 `<mark>` 包起來，其餘照文字節點輸出（shared/utils/plain-text.ts `highlightSegments`）。
// 🔴 搜尋頁本身 `noindex`（搜尋結果頁不該被收錄），也不在 sitemap（沒有 `unit` 宣告，SITE_UNITS 不含）。
// 零結果：後端回 `isEmpty` 時，**前台**呼叫既有的 `POST faqs/search-misses` 記錄（搜尋端點是 GET，不寫入）；
// 同一個關鍵字在同一個頁面載入只記一次。只在瀏覽器端記錄（SSR 不寫入，也避免爬蟲造成假資料）。
definePageMeta({ nav: '' })

interface SearchItem {
  type: string
  subType: string | null
  id: string
  slug: string | null
  title: string
  snippet: string | null
  date: string | null
  categoryCode: string | null
  teamCode: string | null
  imageUrl: string | null
  isFallbackLocale: boolean
}
interface SearchResponse {
  query: string
  tokens: string[]
  items: SearchItem[]
  page: number
  pageSize: number
  totalCount: number
  facets: Array<{ type: string, label: string, count: number }>
  truncated: boolean
  isEmpty: boolean
}
type SearchOutcome = { ok: true, res: SearchResponse } | { ok: false, message: string }

const PAGE_SIZE = 20

const route = useRoute()
const { lp, locale } = useLocale()
const config = useRuntimeConfig()
const club = config.public.club
const clubNameZh = computed(() => getClubAssets(club).nameZh)

const q = computed(() => (typeof route.query.q === 'string' ? route.query.q.trim() : ''))
const type = computed(() => (typeof route.query.type === 'string' ? route.query.type : ''))
const page = computed(() => Math.max(1, Number.parseInt(String(route.query.page ?? '1'), 10) || 1))
const input = ref(q.value)
watch(q, (v) => { input.value = v })

useSeoMeta({
  title: computed(() => `${q.value ? `搜尋：${q.value}` : '搜尋'}｜${clubNameZh.value}`),
  description: computed(() => `${clubNameZh.value}官方網站全站搜尋。`),
  robots: 'noindex, follow',
})

const { data: outcome, status } = await useAsyncData<SearchOutcome | null>(
  () => `search-${club}-${locale.value}-${q.value}-${type.value}-${page.value}`,
  async () => {
    if (!q.value) return null
    try {
      const res = await $fetch<SearchResponse>(`/api/backend/${club}/search`, {
        query: { q: q.value, type: type.value || undefined, lang: locale.value, page: page.value, pageSize: PAGE_SIZE },
      })
      return { ok: true, res }
    }
    catch (err: unknown) {
      const code = (err as { statusCode?: number, status?: number } | null)?.statusCode ?? (err as { status?: number } | null)?.status
      const message = code === 429
        ? '搜尋次數過多，請稍候一分鐘再試。'
        : (code === 400 ? (extractErrorMessage(err) ?? '搜尋條件不正確，請調整關鍵字後再試。') : '搜尋暫時無法使用，請稍後再試。')
      return { ok: false, message }
    }
  },
  { watch: [q, type, page] },
)

const res = computed(() => (outcome.value?.ok ? outcome.value.res : null))
const errorMessage = computed(() => (outcome.value && !outcome.value.ok ? outcome.value.message : ''))
const tokens = computed(() => res.value?.tokens ?? [])

// 藍鯨不設慈善（單元 11）：即使後端有資料也不顯示該分類，避免連到不存在的頁面
const charityEnabled = isUnitEnabledForClub('11', club)
const items = computed(() => (res.value?.items ?? []).filter((i) => i.type !== 'charity' || charityEnabled))
const facets = computed(() => (res.value?.facets ?? []).filter((f) => f.type !== 'charity' || charityEnabled))
const totalHits = computed(() => facets.value.reduce((sum, f) => sum + f.count, 0))
const totalPages = computed(() => Math.max(1, Math.ceil((res.value?.totalCount ?? 0) / PAGE_SIZE)))

const TYPE_LABEL: Record<string, string> = { news: '新聞', faq: '常見問題', program: '課程', player: '球員', coach: '教練與團隊', charity: '慈善' }

/** 結果 → 前台詳情連結（內部路徑，不含語系前綴，交給 lp()）。 */
function resultPath(item: SearchItem): string {
  switch (item.type) {
    case 'news': return `/zh/news/${item.slug}/`
    case 'faq': return item.slug ? `/zh/faq/#q-${item.slug}` : '/zh/faq/'
    case 'program': return '/zh/programs/'
    case 'player': return `/zh/club/first-team/player/${item.id}/`
    case 'coach': return '/zh/club/first-team/'
    case 'charity': return item.subType === 'program' && item.slug ? `/zh/charity/programs/${item.slug}/` : '/zh/charity/impact-stories/'
    default: return '/zh/'
  }
}

function dateText(d: string | null): string {
  return d ? d.slice(0, 10).replaceAll('-', '/') : ''
}

function pageLink(next: { type?: string, page?: number }) {
  const query: Record<string, string> = { q: q.value }
  const t = next.type !== undefined ? next.type : type.value
  if (t) query.type = t
  if (next.page && next.page > 1) query.page = String(next.page)
  return { path: route.path, query }
}

function submit() {
  const next = input.value.trim()
  if (!next) return
  return navigateTo({ path: route.path, query: { q: next } })
}

// 零結果記錄：只在瀏覽器端、同一關鍵字一次
let lastMissRecorded = ''
watch(
  () => (outcome.value?.ok ? outcome.value.res : null),
  (r) => {
    if (!import.meta.client || !r || !r.isEmpty || r.query === lastMissRecorded) return
    lastMissRecorded = r.query
    $fetch(`/api/backend/${club}/faqs/search-misses`, { method: 'POST', body: { keyword: r.query } }).catch(() => {})
  },
  { immediate: true },
)
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li aria-current="page">搜尋</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">Search</p>
    <h1>搜尋<span class="en">Search</span></h1>
    <p class="page-hero__lede">搜尋新聞、常見問題、課程、球員、教練與慈善事蹟。多個關鍵字請用空白分隔，結果必須包含全部關鍵字。</p>
  </div>
</section>

<section class="band search-band" aria-labelledby="search-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="search-title">搜尋結果</h2>

    <form class="search-form" role="search" aria-label="站內搜尋" @submit.prevent="submit">
      <label class="visually-hidden" for="search-page-input">搜尋關鍵字</label>
      <input id="search-page-input" v-model="input" type="search" name="q" maxlength="100" placeholder="輸入關鍵字" autocomplete="off" enterkeyhint="search">
      <button type="submit" class="btn btn--primary">搜尋</button>
    </form>

    <p v-if="!q" class="search-hint">請輸入關鍵字開始搜尋。</p>

    <div v-else-if="errorMessage" class="form-status form-status--error" role="alert"><p>{{ errorMessage }}</p></div>

    <template v-else-if="res">
      <p class="search-summary" role="status">
        <template v-if="res.isEmpty || totalHits === 0">找不到與「{{ res.query }}」相關的內容。</template>
        <template v-else>
          「{{ res.query }}」共找到 {{ totalHits }} 筆結果<template v-if="res.truncated">（每個分類只列出最前面的 100 筆，請加入更多關鍵字縮小範圍）</template>。
        </template>
      </p>

      <nav v-if="facets.length > 0 && totalHits > 0" class="search-facets" aria-label="結果分類">
        <ul>
          <li>
            <NuxtLink :to="pageLink({ type: '' })" :aria-current="type === '' ? 'true' : undefined">全部（{{ totalHits }}）</NuxtLink>
          </li>
          <li v-for="f in facets" :key="f.type">
            <NuxtLink :to="pageLink({ type: f.type })" :aria-current="type === f.type ? 'true' : undefined">{{ f.label || TYPE_LABEL[f.type] }}（{{ f.count }}）</NuxtLink>
          </li>
        </ul>
      </nav>

      <div v-if="res.isEmpty || totalHits === 0" class="search-empty">
        <p>建議您：</p>
        <ul>
          <li>檢查關鍵字是否有錯字，或改用較短、較通用的詞。</li>
          <li>減少關鍵字的數量（所有關鍵字都必須出現才算符合）。</li>
          <li>前往<a :href="lp('/zh/faq/')">常見問題</a>瀏覽，或<a :href="lp('/zh/join/general/')">聯絡我們</a>。</li>
        </ul>
      </div>

      <ol v-else class="search-results" :class="{ 'is-loading': status === 'pending' }">
        <li v-for="item in items" :key="`${item.type}-${item.id}`" class="search-item">
          <img v-if="item.imageUrl" class="search-item__img" :src="item.imageUrl" alt="" width="96" height="72" loading="lazy">
          <div class="search-item__body">
            <p class="search-item__meta">
              <span class="search-item__type">{{ TYPE_LABEL[item.type] ?? item.type }}</span>
              <span v-if="item.date">{{ dateText(item.date) }}</span>
              <span v-if="item.isFallbackLocale">（尚無此語系版本，顯示繁體中文）</span>
            </p>
            <h3 class="search-item__title">
              <a :href="lp(resultPath(item))"><template v-for="(seg, i) in highlightSegments(item.title, tokens)" :key="i"><mark v-if="seg.hit">{{ seg.text }}</mark><template v-else>{{ seg.text }}</template></template></a>
            </h3>
            <p v-if="item.snippet" class="search-item__snippet"><template v-for="(seg, i) in highlightSegments(item.snippet, tokens)" :key="i"><mark v-if="seg.hit">{{ seg.text }}</mark><template v-else>{{ seg.text }}</template></template></p>
          </div>
        </li>
      </ol>

      <nav v-if="totalPages > 1" class="search-pager" aria-label="搜尋結果分頁">
        <NuxtLink v-if="page > 1" :to="pageLink({ page: page - 1 })" rel="prev">上一頁</NuxtLink>
        <span aria-current="page">第 {{ page }} / {{ totalPages }} 頁</span>
        <NuxtLink v-if="page < totalPages" :to="pageLink({ page: page + 1 })" rel="next">下一頁</NuxtLink>
      </nav>
    </template>

    <p v-else-if="status === 'pending'" class="search-hint" role="status">搜尋中…</p>
  </div>
</section>
</template>

<style>
.search-band{ padding-block:clamp(2.5rem,5vw,4.5rem); }
.search-form{ display:flex; gap:.75rem; max-width:720px; }
.search-form input[type="search"]{ flex:1 1 auto; min-width:0; min-height:48px; padding:.7rem 1rem; border:1px solid var(--rule); background:#fff; font:inherit; }
.search-form .btn{ flex:0 0 auto; }
.search-hint, .search-summary{ margin-top:1.25rem; color:var(--muted); }
.search-summary{ color:var(--text); font-weight:700; }
.search-facets{ margin-top:1.25rem; }
.search-facets ul{ display:flex; flex-wrap:wrap; gap:.5rem; list-style:none; margin:0; padding:0; }
.search-facets a{ display:inline-block; padding:.45rem .9rem; border:1px solid var(--rule); font-size:.85rem; font-weight:700; background:var(--paper); }
.search-facets a[aria-current="true"]{ background:var(--ink); color:#fff; border-color:var(--ink); }
.search-empty{ margin-top:1.5rem; max-width:62ch; color:var(--muted); }
.search-empty ul{ margin:.5rem 0 0 1.2rem; list-style:disc; }
.search-results{ list-style:none; margin:1.5rem 0 0; padding:0; display:flex; flex-direction:column; }
.search-results.is-loading{ opacity:.55; }
.search-item{ display:flex; gap:1rem; padding:1.25rem 0; border-bottom:1px solid var(--rule); min-width:0; }
.search-item__img{ flex:0 0 auto; width:96px; height:72px; object-fit:cover; background:var(--paper-2); }
.search-item__body{ min-width:0; flex:1 1 auto; }
.search-item__meta{ display:flex; flex-wrap:wrap; gap:.25rem .75rem; font-size:.75rem; color:var(--muted); }
.search-item__type{ font-weight:800; letter-spacing:.04em; color:var(--brand-aa); }
.search-item__title{ margin:.25rem 0 0; font-size:1.05rem; font-weight:800; overflow-wrap:anywhere; }
.search-item__title a:hover{ text-decoration:underline; }
.search-item__snippet{ margin:.4rem 0 0; font-size:.88rem; line-height:1.7; color:var(--muted); overflow-wrap:anywhere; }
.search-item mark{ background:color-mix(in srgb, var(--brand) 18%, transparent); color:inherit; padding:0 .1em; }
.search-pager{ display:flex; align-items:center; justify-content:center; gap:1.25rem; margin-top:2rem; font-weight:700; }
@media (max-width:560px){
  .search-form{ flex-wrap:wrap; }
  .search-form input[type="search"]{ flex-basis:100%; }
  .search-item__img{ width:72px; height:54px; }
}
</style>

<script setup lang="ts">
// app/pages/zh/news/[slug]/index.vue — 新聞逐篇網址（S0-9e，取代 article.vue 的範本頁）
//
// 由 app/pages/zh/news/article.vue 改寫而來，版面（DOM 結構、class）逐字沿用該頁
// （docs/13-blue-whale-site.md §6 紀律 9／10：不得改寫版型），只把「寫死一篇」
// 換成「依網址打 GET /api/v1/{club}/news/{slug} 取那一篇」。
//
// 🔴 article.vue 本身已刪除：它是 mockup 時代「文章詳情頁範本」（固定示範
// 2026-05-24-match-001 那一篇），template-banner 明文寫「正式站將由 CMS 為每篇
// 文章產生獨立網址」——這句話現在成立了，範本页存在的理由隨之消失。留著不刪的話，
// 那篇文章會同時有兩個網址（/zh/news/article/ 與 /zh/news/2026-05-24-match-001/），
// 正好是本頁下面要處理的 canonical／Article Schema 想避免的重複內容問題，自己先犯一次。
// 已用 grep 確認全站（apps/web 範圍內）改完 NewsCard.vue／app/pages/zh/index.vue 之後
// 沒有任何地方還連著 /zh/news/article/（見 STATUS.md S0-9e 驗收紀錄）。
//
// ⚠️ 路由優先順序（碰撞防呆，任務指示要求的部分）：07 單元底下已有 8 個分類頁
// （club/match/academy/player-stories/international/camps-events/community/media，
// 皆是與本檔同層的 app/pages/zh/news/<name>.vue 靜態頁），Nuxt／vue-router 的路由
// 排序規則是「靜態路徑一律優先於動態區段」，不需要在這裡另外寫規則去擋——
// 已用 curl 實測 /zh/news/club/ 等分類頁仍是分類頁、不會被本頁吃掉（見驗收紀錄）。
// 若編輯把文章 slug 取名剛好撞上這 8 個分類代碼（或 'article'，見下方保留字清單），
// 該篇文章會變成打不到、永遠顯示分類頁——這組保留字的建立端驗證由 apps/api
// 另一位 agent 同步處理，不在本頁範圍，這裡只確保「靜態贏動態」這個路由層的前提成立。
definePageMeta({ nav: 'news', unit: '07', enReady: true, enReadyBw: true })

const route = useRoute()
const config = useRuntimeConfig()
const club = config.public.club

// S1-13：lang 跟隨目前路由語系（/zh/news/{slug} 或 /en/news/{slug}，兩者是同一個
// component 檔案複製出來的孿生路由，見 nuxt.config.ts 的 pages:extend），不再寫死 'zh'。
const { locale, lp, isEn, tx } = useLocale()

// slug 用函式形式傳給 useFetch key／URL，確保「從一篇相關文章點到另一篇」這種
// client-side 導覽（同一個路由元件、只有 params.slug 變化）會重新打 API，
// 不會沿用上一篇的快取資料。
const { data: article } = await useFetch(() => `/api/backend/${club}/news/${route.params.slug}`, {
  query: { lang: locale.value },
})

// 🔴 找不到的 slug（不存在／草稿／排程中——公開 API 本來就只回已發布文章）→ 302 回新聞列表，
// **不回 200 配空版面**（那會讓搜尋引擎收錄一堆空頁），也**不顯示 404 錯誤頁**：App 規劃書 §2.3
// 規定未安裝 App 的裝置點到 `tcrfc://news/{slug}` 的回退網址「不得顯示錯誤頁」（docs/19 §2）。
// 2026-10 之前這裡是 createError 404，改為轉址的取捨：下架文章的舊網址不再給搜尋引擎 404 訊號，
// 但站台上線前本來就全站 noindex，上線後下架文章的收斂交給後台 H 的 301 轉址管理。
if (!article.value) {
  await navigateTo(lp('/zh/news/'), { redirectCode: 302, replace: true })
}

// 相關文章：同分類、依 API 既有排序（發布時間新到舊）找目前這篇之後的兩篇。
// 邏輯沿用 article.vue 原本的寫法，差別只在分類改成「這篇文章自己的分類」
// （article.value.categoryCode），不是寫死 'match'。
const { data: categoryList } = await useFetch(`/api/backend/${club}/news`, {
  query: { category: article.value?.categoryCode, pageSize: 200, lang: locale.value },
})

const related = computed(() => {
  const items = categoryList.value?.items ?? []
  const idx = items.findIndex((a: { slug: string }) => a.slug === article.value?.slug)
  if (idx === -1) return []
  return items.slice(idx + 1, idx + 3)
})

// ── 賽事資訊（規劃書 3.7：7.2 比賽報導的特殊欄位「關聯賽事、比分」）────────────────
// 資料來源是文章自己的關聯賽事（ArticleDetailDto.relations 裡 targetType='match' 的那筆，
// 後台 B2「關聯」欄位選的），不再用正規表示式從標題硬拆。關聯只帶賽事 id，名稱／比分
// 要回頭查 GET /{club}/schedule（公開端點）依 id 找出那一場——有關聯才發請求。
// 查不到（沒關聯、賽事不在回傳範圍內）就整塊不顯示，不留空白列或示範文字。
// 「出賽名單」目前沒有公開讀取端點（apps/api 只有後台 C4 有 match_lineups），該列不顯示。
interface RelatedMatch {
  id: string
  matchOn: string
  homeAway: string | null
  matchNo: number | null
  opponent: string | null
  competitionName: string | null
  status: string | null
  scoreHome: number | null
  scoreAway: number | null
}
// useFetch 在本頁沒有帶回應型別（既有慣例，見檔內其他欄位的存取），relations／bodyJson 先轉成最小型別再用，
// 不在這一輪替整頁補型別（前台 typecheck 既有錯誤不處理，但不新增）。
type ArticleExtras = { relations?: { targetType: string, targetId: string }[], bodyJson?: string | null }
const articleExtras = computed(() => article.value as unknown as ArticleExtras | null)
const bodyJson = computed(() => articleExtras.value?.bodyJson ?? null)
const relatedMatchId = computed(() => {
  const rel = (articleExtras.value?.relations ?? []).find((r) => r.targetType === 'match')
  return rel ? String(rel.targetId).toLowerCase() : null
})
const { data: scheduleData } = await useFetch<{ items: RelatedMatch[] }>(`/api/backend/${club}/schedule`, {
  query: { pageSize: 100, lang: locale.value },
  immediate: relatedMatchId.value !== null,
})
const matchFields = computed(() => {
  const id = relatedMatchId.value
  if (!id) return null
  const m = (scheduleData.value?.items ?? []).find((x) => x.id.toLowerCase() === id)
  if (!m) return null
  const self = isEn.value ? clubNameEn(clubKey) : getClubAssets(club).nameZh
  const opp = m.opponent ?? '—'
  const finished = m.scoreHome != null && m.scoreAway != null
  return {
    competition: m.competitionName ?? '—',
    fixture: m.homeAway === 'HOME' ? `${self} vs ${opp}` : `${opp} vs ${self}`,
    score: finished ? `${m.scoreHome} : ${m.scoreAway}` : null,
    scheduleHref: `${lp('/zh/schedule/')}#${fixtureId(m.matchOn, m.homeAway, m.matchNo)}`,
    matchOn: m.matchOn,
  }
})

// ── B-15：核心價值標籤與關聯球員 ──────────────────────────────────────────────
// 核心價值標籤：`coreValueTags` 是五個固定代碼（與首頁核心價值同一組），名稱取 shared/utils/core-values.ts 的對照；
// 認不得的代碼不顯示（不輸出原始代碼給訪客看）。
type ArticleExtras2 = { coreValueTags?: string[] }
const coreValueLabels = computed(() => {
  const codes = (article.value as unknown as ArticleExtras2 | null)?.coreValueTags ?? []
  return codes
    .map((c) => CORE_VALUES_FALLBACK.find((v) => v.code === c))
    .filter((v): v is NonNullable<typeof v> => Boolean(v))
    .map((v) => (isEn.value ? v.nameEn : v.nameZh))
})
// 關聯球員：關聯只帶球員 id，名稱與網址代稱回頭查公開球員列表（只含現役；查不到＝已離隊或不公開，不顯示）。
// 球員頁網址本身接受 id，但這裡仍用 slug 組網址，與球員列表一致。
const relatedPlayerIds = computed(() => (articleExtras.value?.relations ?? [])
  .filter((r) => r.targetType === 'player')
  .map((r) => String(r.targetId).toLowerCase()))
const { data: playersData } = await useFetch<{ items: { id: string, slug: string, name: string | null, shirtNo: number | null, position: string | null }[] }>(`/api/backend/${club}/players`, {
  query: { pageSize: 200, lang: locale.value },
  immediate: relatedPlayerIds.value.length > 0,
})
const relatedPlayers = computed(() => {
  const ids = relatedPlayerIds.value
  if (!ids.length) return []
  return (playersData.value?.items ?? []).filter((p) => p.name && ids.includes(p.id.toLowerCase()))
})

// ── 社群分享（規劃書 3.7 詳情頁「社群分享」）──────────────────────────────────────
// Facebook／LINE 用官方分享網址（不需任何金鑰），複製連結用 Clipboard API。分享的網址是
// 這一站目前的絕對網址（siteConfig.url 為 runtime 可覆寫的站台網址，見檔內 canonical 說明）。
const shareUrl = computed(() => `${(siteConfig.url ?? '').replace(/\/$/, '')}${route.path}`)
const facebookShareHref = computed(() => `https://www.facebook.com/sharer/sharer.php?u=${encodeURIComponent(shareUrl.value)}`)
const lineShareHref = computed(() => `https://social-plugins.line.me/lineit/share?url=${encodeURIComponent(shareUrl.value)}`)
const copyState = ref<'idle' | 'done' | 'failed'>('idle')
async function copyShareLink() {
  try {
    await navigator.clipboard.writeText(shareUrl.value)
    copyState.value = 'done'
  } catch {
    copyState.value = 'failed'
  }
  setTimeout(() => { copyState.value = 'idle' }, 2500)
}

const eyebrow = computed(() => (isEn.value ? newsCategoryTabLabelEn(article.value?.categoryCode ?? '', clubKey) : newsEyebrowText(article.value?.categoryCode ?? '')))
const categoryBilingual = computed(() => (isEn.value
  ? newsCategoryLabelEn(article.value?.categoryCode ?? '', article.value?.categoryName, clubKey)
  : newsCategoryBilingualLabel(article.value?.categoryCode ?? '', article.value?.categoryName)))
/** 分類名稱（麵包屑、「查看所有…」按鈕用）：英文版取對照表，避免 API 回退成繁中時混語。 */
const categoryName = computed(() => (isEn.value ? newsCategoryLabelEn(article.value?.categoryCode ?? '', article.value?.categoryName, clubKey) : article.value?.categoryName))
function relatedCategoryName(r: { categoryCode: string, categoryName: string | null }): string | null {
  return isEn.value ? newsCategoryLabelEn(r.categoryCode, r.categoryName, clubKey) : r.categoryName
}
/** 日期顯示：zh `2026/08/10`、en `10 Aug 2026`。 */
function dateText(iso: string | null | undefined): string {
  return isEn.value ? newsDateEn(iso) : newsSlashDate(iso)
}
const clubKey: 'tcrfc' | 'bw' = club === 'bw' ? 'bw' : 'tcrfc'
// S0-7h：封面來源／寬高／替代文字集中在 newsCoverImg()（utils/news.ts）
const cover = computed(() => (article.value ? newsCoverImg(article.value, clubKey) : null))

// ── SEO／GEO ──────────────────────────────────────────────────────────────
// canonical：交給 @nuxtjs/seo（nuxt-seo-utils）依 site.url ＋ 目前路徑自動產生，
// 這裡不手動疊加 <link rel="canonical">——其餘 80 頁走的是同一套機制
// （S0-9b 已實測 NUXT_PUBLIC_SITE_URL 能 runtime 覆寫，本頁沿用、不另開一套）。
//
// 🔴 共用文章（IsShared＝club_id 為空）的 canonical 該掛哪一站，規劃書列為
// 「待客戶確認」（規劃書第 10 章第 38 點／docs/05-i18n-seo.md §4b①明文「⚠️ 待客戶
// 確認」），不是已定案的規則。這裡刻意不替它預先決定「掛主站」：沿用上面那套
// nuxt-seo-utils 的預設行為（canonical 指向目前這一站自己的網址）——這是對任何
// 頁面都成立的中性預設值，不是在替共用文章的站別站隊。等客戶拍板後，若決議是
// 「掛主站、藍鯨站只連結不重複陳述」（docs/05 的建議案），要在這裡加條件判斷：
// article.value.isShared 為 true 且目前站不是主站時，改用明確的 useSeoMeta({ ogUrl })
// ／useHead 疊加 <link rel="canonical"> 指到主站網址（GEO-09）。目前 83 篇種子
// 資料沒有任何一篇 club_id 為空（apps/api/README.md 驗收紀錄），這個分支目前
// 不會被觸發，也還沒有真實資料可以驗證。
//
// 站名：用 getClubAssets(club).nameZh（既有工具，llms.txt.ts 已是同一種用法），
// 不是把「台中磐石足球俱樂部」寫死——這點跟站內其餘既有頁面不同（那些是搬遷時
// 沿用 mockup 逐字內容的既有落差，見回報），但本頁是全新頁面且兩站都會用到，
// 寫死磐石名稱會讓藍鯨站的文章頁 SEO 標題與 Schema 都掛錯品牌，沒有理由沿用
// 那個已知落差。
const siteName = computed(() => (isEn.value ? clubNameEn(clubKey) : getClubAssets(club).nameZh))
// Schema 的 author／publisher：藍鯨英文用全名（B-5），其餘與 siteName 相同。
const schemaOrgName = computed(() => (isEn.value && clubKey === 'bw' ? BW_FULL_NAME_EN : siteName.value))

useSeoMeta({
  // B-15：後台填了 SEO 標題就用它（頁面自己的設定優先於全站標題樣板）
  title: computed(() =>
    article.value?.seoTitle?.trim() ? article.value.seoTitle.trim() : article.value ? (isEn.value ? `${article.value.title} | News | ${siteName.value}` : `${article.value.title}｜新聞 News｜${siteName.value}`) : '',
  ),
  description: computed(() => {
    const a = article.value
    if (!a) return ''
    return a.seoDescription || a.summary || (isEn.value ? `${a.title ?? ''} — ${siteName.value} news centre` : `${a.title ?? ''} — ${siteName.value}新聞中心`)
  }),
  // ── S1-12 驗收退回後補做：Meta Keywords／OG 圖文／noindex 真的要輸出到 HTML ──────────
  // 上一輪只把這些欄位加進 apps/api 的 DTO，沒有接到任何前台頁面消費，這裡是第一個（也是
  // 目前唯一一個）真的有動態內容可以渲染的公開頁面（其餘 79 頁是靜態 mockup 搬遷頁，
  // 沒有對應的後端 SEO 資料可讀，見 apps/api/README.md「S1-12」段「Sitemap 只涵蓋 Article」
  // 同一個理由）。
  keywords: computed(() => article.value?.seoKeywords ?? undefined),
  ogTitle: computed(() => (article.value?.seoTitle?.trim() ? article.value.seoTitle.trim() : article.value ? `${article.value.title}${isEn.value ? ' | ' : '｜'}${siteName.value}` : undefined)),
  ogDescription: computed(() => article.value?.seoDescription || article.value?.summary || undefined),
  // ogImage 已經是 apps/api 算好優先序（這篇文章專屬 > 全站預設 > 這篇文章的封面圖片）之後
  // 的完整網址，這裡直接用，不在前台重新判斷一次優先序（單一真實來源）。
  ogImage: computed(() => article.value?.ogImageUrl ?? undefined),
  ogImageWidth: computed(() => article.value?.ogImageWidth ?? undefined),
  ogImageHeight: computed(() => article.value?.ogImageHeight ?? undefined),
  ogImageAlt: computed(() => article.value?.ogImageAlt ?? undefined),
  // 🔴 noindex 是「這篇文章」層級的開關（後台可以個別設定），跟全站上線前的 noindex
  // （nuxt.config.ts 的 routeRules，CLAUDE.md 全域規定第 5 條）是兩個機制、互不取代——全站
  // noindex 由 X-Robots-Tag 標頭無條件蓋過，這裡的 <meta name="robots"> 是「萬一全站
  // noindex 之後解除了，這篇文章本身該不該被收錄」這件事的獨立設定，兩者同時存在不衝突
  // （搜尋引擎對「有任何一處說 noindex」一律視為 noindex，不會互相抵消）。
  robots: computed(() => (article.value?.isNoindex ? 'noindex' : undefined)),
})

// canonical 覆寫（S1-12 驗收退回後補做）：只有後台明確設定 canonicalPath 時才疊加，
// 省略時維持上面既有註解說明的預設行為（nuxt-seo-utils 依 site.url ＋ 目前路徑自動產生）——
// 不因為新增這個欄位就改變其餘 80 頁沒有這個資料可用時的既有行為。siteConfig.url 用法
// 逐字比照既有 app/pages/zh/schedule.vue 的既有先例（nuxt-site-config 的 priority-stack，
// 已實測 NUXT_PUBLIC_SITE_URL 能在 runtime 正確覆寫）。
const siteConfig = useSiteConfig()
useHead({
  link: computed(() => {
    const canonicalPath = article.value?.canonicalPath
    if (!canonicalPath) return []
    const siteUrl = (siteConfig.url ?? '').replace(/\/$/, '')
    return [{ rel: 'canonical', href: `${siteUrl}${canonicalPath}` }]
  }),
})

// Article Schema（GEO-05／GEO-08：canonical、發布與更新時間、語言、作者或署名單位）。
// 本站沒有個別記者／作者欄位，署名單位固定用俱樂部本身（siteName，即下方 JSON-LD
// 的 author／publisher 同一個值）——S1-12e 之前這裡的可見「作者」欄只有標籤沒有值
// （article.vue 原本留白），GEO-08 明文「文章型內容另輸出作者或署名單位」不只是
// 結構化資料，可見文字也要輸出，S1-12e 把樣板裡的「作者」欄位補上這個值
// （見下方 template 的 `article-meta-row`），讓明文與 JSON-LD 兩處數值一致（GEO-04）。
//
// 🔴 已知缺口（回報，不在本頁修補，因為不在 apps/web 範圍內）：apps/api 的
// ArticleDetailDto 沒有 updatedAt／dateModified 可用的欄位——DB 的
// articles.updated_at 只在後台寫入端點當樂觀並行的權杖用，沒有經公開 API 的 DTO
// 輸出（apps/api/README.md）。GEO-08 明文「更新時間要真的更新，不是發布時間複製
// 一份」，這裡沒有真實資料就不輸出 dateModified，不用 publishedAt 頂替一份假的。
//
// GEO-05（S1-12c）：輸不輸出改讀後端算好的 a.schemaEligible（標題／發布時間／圖片
// 三個必填欄位是否齊全，見 apps/api Features/Seo/SchemaRequiredFields），不再只看
// publishedAt 一個欄位——判斷條件的單一來源只在 apps/api 宣告一次（E-39）。
watchEffect(() => {
  const a = article.value
  if (!a?.schemaEligible) return
  useSchemaOrg([
    defineArticle({
      headline: a.title ?? undefined,
      datePublished: a.publishedAt,
      // S1-13：inLanguage 跟隨目前路由語系（GEO-08「語言」要求），不再寫死
      // zh-Hant——本頁的 /en/... 孿生路由現在真的存在，繼續寫死會讓 en 頁面的
      // Article Schema 自稱是中文內容，自相矛盾。
      inLanguage: HREFLANG_MAP[locale.value],
      // GEO-05（S1-12c）：用 a.ogImageUrl（後端已算好「這篇專屬 > 全站預設 > 封面圖」優先序
      // 的完整網址），不是本地 mockup 靜態檔案的 hasNewsCover() 判斷——schemaEligible 判斷
      // 「這篇文章有沒有圖片」時用的就是 ogImageUrl，這裡要用同一份值，兩者才不會互相矛盾
      // （schemaEligible 說有圖，這裡卻因為 mockup 沒有那個檔案而輸出 undefined）。
      // S0-7h：寬高都有時輸出 ImageObject（url＋width＋height），否則維持單純網址字串（不發明尺寸）
      image: a.ogImageUrl
        ? (a.ogImageWidth && a.ogImageHeight
            ? { '@type': 'ImageObject', url: a.ogImageUrl, width: a.ogImageWidth, height: a.ogImageHeight }
            : a.ogImageUrl)
        : undefined,
      articleSection: a.categoryName ?? undefined,
      author: { '@type': 'Organization', name: schemaOrgName.value },
      publisher: { '@type': 'Organization', name: schemaOrgName.value },
    }),
  ])
})

// BreadcrumbList JSON-LD（GEO-05／S1-12f）：這是目前唯一有 BreadcrumbSchemaEligible 可讀的頁面
// （本專案沒有頁面階層資料表，其餘 79 頁的麵包屑是靜態手寫、沒有對應的 DB 記錄可供 GEO-05
// 判斷「缺不缺」，見 apps/api ArticleDetailDto.BreadcrumbSchemaEligible 檔頭說明與
// apps/web/README.md「S1-12f」節，本輪不擴大到其餘靜態頁）。第三層「分類」節點用文章本身的
// 分類頁網址，最後一層（文章標題本身）依 schema.org 慣例不需要 item。
watchEffect(() => {
  const a = article.value
  if (!a?.breadcrumbSchemaEligible) return
  const siteUrl = (siteConfig.url ?? '').replace(/\/$/, '')
  useSchemaOrg([
    defineBreadcrumb({
      itemListElement: [
        { name: tx('首頁', 'Home'), item: `${siteUrl}${lp('/zh/')}` },
        { name: tx('新聞 News', 'News'), item: `${siteUrl}${lp('/zh/news/')}` },
        { name: a.categoryName ?? undefined, item: `${siteUrl}${lp(`/zh/news/${a.categoryCode}/`)}` },
        { name: a.title ?? undefined },
      ],
    }),
  ])
})

// A-8：瀏覽數 +1。只在瀏覽器端、每個工作階段每篇最多一次（見 utils/report-view.ts）。
// 從相關文章 client-side 導覽到另一篇時元件不重掛載，所以用 watch 追 slug。
watch(() => article.value?.slug, (slug) => { if (slug) reportView(club, 'news', slug) }, { immediate: true, flush: 'post' })
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/news/')">{{ tx('新聞 News', 'News') }}</a></li>
      <li><a :href="lp(`/zh/news/${article?.categoryCode}/`)">{{ categoryName }}</a></li>
      <li aria-current="page">{{ article?.title }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <img
    class="page-hero__bg"
    :src="cover ? cover.src : newsFallbackMarkSrc(clubKey)"
    alt=""
    :width="cover ? (cover.width ?? undefined) : 1600"
    :height="cover ? (cover.height ?? undefined) : 1067"
  >
  <div class="container">
    <p class="page-hero__eyebrow">{{ eyebrow }}</p>
    <h1>{{ article?.title }}</h1>
    <p class="page-hero__lede">{{ isEn ? `Published ${newsDateEn(article?.publishedAt)}` : `${newsSlashDate(article?.publishedAt)} 發布` }}</p>
  </div>
</section>

<LocaleFallbackNotice v-if="isEn && (hasFallbackLocale(article) || hasFallbackLocale(related))" partial />

<section class="band" aria-labelledby="article-body-title">
  <div class="band-inner container article-layout">
    <article class="prose">
      <h2 class="visually-hidden" id="article-body-title">{{ tx('文章內容', 'Article content') }}</h2>

      <div class="article-meta-row">
        <div><span class="article-meta-row__label">{{ tx('發布日期', 'Published') }}</span><time :datetime="newsIsoDate(article?.publishedAt)">{{ dateText(article?.publishedAt) }}</time></div>
        <div><span class="article-meta-row__label">{{ tx('分類', 'Category') }}</span><a :href="lp(`/zh/news/${article?.categoryCode}/`)">{{ categoryBilingual }}</a></div>
        <div><span class="article-meta-row__label">{{ tx('作者', 'Author') }}</span>{{ siteName }}</div>
        <!-- S1-17 新增：標籤（規劃書 3.7「詳情頁：…標籤…」，ArticleDetailDto.tags 是 S1-5
             就已回傳的既有欄位，先前沒有前台頁面消費）。沒有標籤時整格不顯示，不留空欄位。 -->
        <div v-if="coreValueLabels.length"><span class="article-meta-row__label">{{ tx('核心價值', 'Core values') }}</span><span v-for="l in coreValueLabels" :key="l" class="article-value-tag">{{ l }}</span></div>
        <div v-if="article?.tags?.length"><span class="article-meta-row__label">{{ tx('標籤', 'Tags') }}</span>{{ article.tags.map((t) => t.name).filter(Boolean).join(isEn ? ', ' : '、') }}</div>
      </div>

      <!-- 內文（bodyJson：純文字或區塊 JSON，解析與消毒見 utils/news-body.ts；沒有內文就不顯示） -->
      <NewsBody :body-json="bodyJson" />

      <template v-if="matchFields">
        <h3>{{ tx('賽事資訊', 'Match information') }}</h3>
        <table class="match-fields">
          <tbody>
            <tr><th scope="row">{{ tx('賽事', 'Competition') }}</th><td>{{ matchFields.competition }}</td></tr>
            <tr><th scope="row">{{ tx('對戰組合', 'Fixture') }}</th><td>{{ matchFields.fixture }}</td></tr>
            <tr v-if="matchFields.score"><th scope="row">{{ tx('比分', 'Score') }}</th><td>{{ matchFields.score }}</td></tr>
            <tr><th scope="row">{{ tx('關聯賽事（賽程連結）', 'Related match (schedule link)') }}</th><td><a :href="matchFields.scheduleHref">{{ isEn ? `Schedule: ${newsDateEn(matchFields.matchOn)}` : `${newsSlashDate(matchFields.matchOn)} 賽程` }}</a></td></tr>
          </tbody>
        </table>
      </template>

      <template v-if="cover">
        <h3>{{ tx('圖集', 'Gallery') }}</h3>
        <div class="article-gallery">
          <figure>
            <!-- 主要內容圖：替代文字在這裡（hero 背景是裝飾，alt 留空避免同一張圖念兩次） -->
            <img :src="cover.src" :alt="cover.alt" loading="lazy" :width="cover.width ?? undefined" :height="cover.height ?? undefined">          </figure>
        </div>
      </template>

      <template v-if="relatedPlayers.length">
        <h3>{{ tx('相關球員', 'Related players') }}</h3>
        <ul class="article-players">
          <li v-for="p in relatedPlayers" :key="p.id">
            <a :href="lp(playerPath(p.slug))">{{ p.shirtNo != null ? `#${p.shirtNo} ` : '' }}{{ p.name }}{{ p.position ? `${tx('　', ' · ')}${p.position}` : '' }}</a>
          </li>
        </ul>
      </template>

      <h3>{{ tx('社群分享', 'Share') }}</h3>
      <!-- Facebook／LINE 為官方分享網址（新分頁開啟，不需金鑰、不載入第三方腳本）；複製連結用 Clipboard API。 -->
      <div class="share-row" role="group" :aria-label="tx('分享這篇文章', 'Share this article')">
        <a class="btn btn--light btn--sm" :href="facebookShareHref" target="_blank" rel="noopener noreferrer">{{ tx('分享至 Facebook', 'Share on Facebook') }}</a>
        <a class="btn btn--light btn--sm" :href="lineShareHref" target="_blank" rel="noopener noreferrer">{{ tx('分享至 LINE', 'Share on LINE') }}</a>
        <button type="button" class="btn btn--light btn--sm" @click="copyShareLink">{{ tx('複製連結', 'Copy link') }}</button>
        <span class="share-row__status" role="status" aria-live="polite">{{ copyState === 'done' ? tx('已複製連結', 'Link copied') : copyState === 'failed' ? tx('無法自動複製，請手動複製網址列', 'Could not copy automatically. Please copy the address from your browser bar.') : '' }}</span>
      </div>
    </article>

    <aside class="article-aside" aria-labelledby="related-title">
      <h3 id="related-title">{{ tx('相關文章', 'Related articles') }}</h3>
      <div class="article-aside__list">
        <a v-for="r in related" :key="r.slug" class="news-card clip-card" :href="lp(`/zh/news/${r.slug}/`)" :data-title="newsTitleAttr(r.title)">
          <div :class="['news-card__media', { 'news-card__media--noimg': !newsCoverImg(r, clubKey) }]">
            <span class="news-card__tag">{{ relatedCategoryName(r) }}</span>
            <img v-if="newsCoverImg(r, clubKey)" :src="newsCoverImg(r, clubKey)!.src" :alt="newsCoverImg(r, clubKey)!.alt" loading="lazy" :width="newsCoverImg(r, clubKey)!.width ?? undefined" :height="newsCoverImg(r, clubKey)!.height ?? undefined">
            <img v-else class="news-card__media-mark" :src="newsFallbackMarkSrc('bw')" alt="" loading="lazy" width="64" height="63">
          </div>
          <div class="news-card__body">
            <p class="news-card__meta"><time :datetime="newsIsoDate(r.publishedAt)">{{ dateText(r.publishedAt) }}</time></p>
            <p class="news-card__title">{{ r.title }}</p>
          </div>
        </a>
      </div>
      <a class="btn btn--dark btn--block" :href="lp(`/zh/news/${article?.categoryCode}/`)" style="margin-top:1.5rem">{{ isEn ? `View all ${categoryName}` : `查看所有${article?.categoryName}` }}</a>
    </aside>
  </div>
</section>
</template>

<style>
/* Article detail page — 07 NEWS 專用版型，逐字沿用原 article.vue 的樣式（規則本身
   未變動，只是隨檔案一起搬到新路徑）。若日後正式文章頁沿用相同結構，建議收進共用 CSS。 */
.article-layout{ display:grid; grid-template-columns:1fr 320px; gap:3rem; align-items:start; }
.article-meta-row{ display:flex; gap:2rem; flex-wrap:wrap; padding-bottom:1.5rem; margin-bottom:1.5rem; border-bottom:1px solid var(--rule); font-size:.85rem; }
.article-meta-row__label{ display:block; font-size:.68rem; font-weight:800; letter-spacing:.06em; text-transform:uppercase; color:var(--muted); margin-bottom:.25rem; }
.match-fields{ width:100%; border-collapse:collapse; font-size:.9rem; }
.match-fields th, .match-fields td{ text-align:left; padding:.65rem .75rem; border-bottom:1px solid var(--rule); vertical-align:top; }
.match-fields th{ width:11em; color:var(--muted); font-weight:700; }
.article-gallery{ display:grid; grid-template-columns:repeat(auto-fit,minmax(220px,1fr)); gap:1rem; }
.article-gallery img{ width:100%; aspect-ratio:3/2; object-fit:cover; }
.article-gallery figcaption{ font-size:.78rem; color:var(--muted); margin-top:.4rem; }
.article-value-tag{ display:inline-block; margin-right:.5rem; padding:.1rem .55rem; border:1px solid var(--rule); font-size:.78rem; font-weight:700; }
.article-players{ list-style:none; padding:0; margin:0 0 1.5rem; display:flex; flex-wrap:wrap; gap:.5rem 1.25rem; }
.article-players a{ font-weight:700; color:var(--brand-aa); }
.share-row{ display:flex; gap:.75rem; flex-wrap:wrap; align-items:center; }
.share-row__status{ font-size:.82rem; color:var(--muted); }
.article-aside{ position:sticky; top:1.5rem; }
.article-aside h3{ font-size:1rem; font-weight:800; margin-bottom:1rem; }
.article-aside__list{ display:flex; flex-direction:column; gap:1rem; }
.article-aside .news-card__media{ aspect-ratio:3/2; }
@media (max-width:900px){ .article-layout{ grid-template-columns:1fr; } .article-aside{ position:static; } }
</style>

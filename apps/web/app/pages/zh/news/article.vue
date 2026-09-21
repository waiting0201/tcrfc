<script setup lang="ts">
// app/pages/zh/news/article.vue — 由 site/src/pages/zh/news/article/index.html 轉來（S0-9 資料驅動頁搬遷）
//
// 🔴 這頁在 mockup 裡明文是「文章詳情頁範本」（template-banner：「正式站將由 CMS 為每篇
// 文章產生獨立網址」），固定示範同一篇真實文章（企甲聯賽 台中磐石 3-0 銘傳大學，
// slug 2026-05-24-match-001），不是動態 [slug] 路由——這是 mockup 既有的刻意設計，
// 不是本次搬遷簡化掉的功能。搬遷後改為 SSR 打真實 API 抓「這篇」文章的真實資料，
// 而不是 build.mjs 時期寫死在 HTML 裡的靜態文字；「賽事資訊」表格的對戰組合／比分
// 一樣是從標題文字解析（原 mockup 本身也是人工從標題抄的，見原檔註解「比分／對戰組合
// 取自真實標題文字」），相關文章改成真的抓同分類下一時間序的下兩篇（比對後與
// mockup 原本手動挑選的兩篇完全一致：2026-05-17-match-002／2026-05-10-match-003）。
definePageMeta({ nav: 'news', unit: '07' })

const ARTICLE_SLUG = '2026-05-24-match-001'

const config = useRuntimeConfig()
const club = config.public.club

const { data: article } = await useFetch(`/api/backend/${club}/news/${ARTICLE_SLUG}`, {
  query: { lang: 'zh' },
})

const { data: categoryList } = await useFetch(`/api/backend/${club}/news`, {
  query: { category: 'match', pageSize: 200, lang: 'zh' },
})

const related = computed(() => {
  const items = categoryList.value?.items ?? []
  const idx = items.findIndex((a) => a.slug === ARTICLE_SLUG)
  if (idx === -1) return []
  return items.slice(idx + 1, idx + 3)
})

// 對戰組合／比分：從標題解析，格式「{賽事} {主隊} {比分} {客隊}」
// （mockup 原本就是人工抄標題填表格，這裡用同樣的來源、改成即時解析）。
const matchFields = computed(() => {
  const title = article.value?.title ?? ''
  const m = title.match(/^(\S+)\s+(.+?)\s+(\d+-\d+)\s+(.+)$/)
  if (!m) return { competition: title, fixture: '—', score: '—' }
  return { competition: m[1], fixture: `${m[2]} vs ${m[4]}`, score: m[3] }
})

useSeoMeta({
  title: computed(() => `${article.value?.title ?? ''}（文章詳情頁範本）｜新聞 News｜台中磐石足球俱樂部`),
  description:
    '文章詳情頁版型範本，以真實文章「企甲聯賽 台中磐石 3-0 銘傳大學」（2026/05/24）示範封面、發布資訊、內文結構與相關文章；正式站由 CMS 為每篇文章產生獨立網址。',
})
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a href="/zh/">首頁</a></li>
      <li><a href="/zh/news/">新聞 News</a></li>
      <li><a href="/zh/news/match/">比賽報導</a></li>
      <li aria-current="page">文章範本</li>
    </ol>
  </div>
</nav>

<div class="template-banner">
  <span class="mock-flag">範本頁 Template — 正式站將由 CMS 為每篇文章產生獨立網址</span>
</div>

<section class="page-hero page-hero--media">
  <img class="page-hero__bg" :src="newsCoverSrc(ARTICLE_SLUG)" alt="" width="1600" height="1067">
  <div class="container">
    <p class="page-hero__eyebrow">7.2 Match Reports · 比賽報導</p>
    <h1>{{ article?.title }}</h1>
    <p class="page-hero__lede">{{ newsSlashDate(article?.publishedAt) }} 發布</p>
  </div>
</section>

<section class="band" aria-labelledby="article-body-title">
  <div class="band-inner container article-layout">
    <article class="prose">
      <h2 class="visually-hidden" id="article-body-title">文章內容</h2>

      <div class="article-meta-row">
        <div><span class="article-meta-row__label">發布日期</span><time :datetime="newsIsoDate(article?.publishedAt)">{{ newsSlashDate(article?.publishedAt) }}</time></div>
        <div><span class="article-meta-row__label">分類</span><a href="/zh/news/match/">比賽報導 Match Reports</a></div>
        <div><span class="article-meta-row__label">作者</span></div>
      </div>

      <h3>賽事資訊</h3>
      <table class="match-fields">
        <tbody>
          <tr><th scope="row">賽事</th><td>{{ matchFields.competition }}</td></tr>
          <tr><th scope="row">對戰組合</th><td>{{ matchFields.fixture }}</td></tr>
          <tr><th scope="row">比分</th><td>{{ matchFields.score }}</td></tr>
          <tr><th scope="row">關聯賽事（賽程連結）</th><td>—</td></tr>
          <tr><th scope="row">出賽名單</th><td>—</td></tr>
        </tbody>
      </table>
      <p style="font-size:.82rem;color:var(--muted)">上表示範 7.2 比賽報導分類的特殊欄位（規劃書 3.7 節）；比分／對戰組合取自真實標題文字，其餘欄位將依個別文章內容填寫。</p>

      <h3>圖集</h3>
      <div class="article-gallery">
        <figure>
          <img :src="newsCoverSrc(ARTICLE_SLUG)" alt="" loading="lazy" width="1600" height="1067">
          <figcaption>封面照片（已轉檔為網頁用尺寸）</figcaption>
        </figure>
      </div>

      <h3>社群分享</h3>
      <!-- 分享按鈕為結構性 UI，尚未串接真實分享 intent；正式文章網址產生後可接上 FB／LINE／複製連結功能 -->
      <div class="share-row" aria-label="分享文章（範本，尚未啟用）">
        <button type="button" class="btn btn--light btn--sm" disabled>分享至 Facebook</button>
        <button type="button" class="btn btn--light btn--sm" disabled>分享至 LINE</button>
        <button type="button" class="btn btn--light btn--sm" disabled>複製連結</button>
      </div>
    </article>

    <aside class="article-aside" aria-labelledby="related-title">
      <h3 id="related-title">相關文章</h3>
      <div class="article-aside__list">
        <a v-for="r in related" :key="r.slug" class="news-card clip-card" href="/zh/news/article/" :data-title="newsTitleAttr(r.title)">
          <div class="news-card__media">
            <span class="news-card__tag">{{ r.categoryName }}</span>
            <img :src="newsCoverSrc(r.slug)" alt="" loading="lazy" width="1600" height="1067">
          </div>
          <div class="news-card__body">
            <p class="news-card__meta"><time :datetime="newsIsoDate(r.publishedAt)">{{ newsSlashDate(r.publishedAt) }}</time></p>
            <p class="news-card__title">{{ r.title }}</p>
          </div>
        </a>
      </div>
      <a class="btn btn--dark btn--block" href="/zh/news/match/" style="margin-top:1.5rem">查看所有比賽報導</a>
    </aside>
  </div>
</section>
</template>

<style>
/* Article detail template — 07 NEWS 專用版型，若日後正式文章頁沿用相同結構，建議收進共用 CSS */
.template-banner{ background:var(--paper-2); border-bottom:1px solid var(--rule); padding:.85rem var(--edge); text-align:center; }
.article-layout{ display:grid; grid-template-columns:1fr 320px; gap:3rem; align-items:start; }
.article-meta-row{ display:flex; gap:2rem; flex-wrap:wrap; padding-bottom:1.5rem; margin-bottom:1.5rem; border-bottom:1px solid var(--rule); font-size:.85rem; }
.article-meta-row__label{ display:block; font-size:.68rem; font-weight:800; letter-spacing:.06em; text-transform:uppercase; color:var(--muted); margin-bottom:.25rem; }
.match-fields{ width:100%; border-collapse:collapse; font-size:.9rem; }
.match-fields th, .match-fields td{ text-align:left; padding:.65rem .75rem; border-bottom:1px solid var(--rule); vertical-align:top; }
.match-fields th{ width:11em; color:var(--muted); font-weight:700; }
.article-gallery{ display:grid; grid-template-columns:repeat(auto-fit,minmax(220px,1fr)); gap:1rem; }
.article-gallery img{ width:100%; aspect-ratio:3/2; object-fit:cover; }
.article-gallery figcaption{ font-size:.78rem; color:var(--muted); margin-top:.4rem; }
.share-row{ display:flex; gap:.75rem; flex-wrap:wrap; }
.article-aside{ position:sticky; top:1.5rem; }
.article-aside h3{ font-size:1rem; font-weight:800; margin-bottom:1rem; }
.article-aside__list{ display:flex; flex-direction:column; gap:1rem; }
.article-aside .news-card__media{ aspect-ratio:3/2; }
@media (max-width:900px){ .article-layout{ grid-template-columns:1fr; } .article-aside{ position:static; } }
</style>

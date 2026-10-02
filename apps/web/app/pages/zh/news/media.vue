<script setup lang="ts">
// app/pages/zh/news/media.vue — 由 site/src/pages/zh/news/media/index.html 轉來（S0-9 靜態頁搬遷）
import type { PressResource, PressResourceType } from '#shared/utils/press'
import type { PagedResponse } from '#shared/utils/api-types'

definePageMeta({ nav: "news", unit: "07" })

const config = useRuntimeConfig()
const club = config.public.club
const isTcrfc = computed(() => club !== 'bw')
const { lp, locale } = useLocale()

// BW-C1（品牌外洩全站盤點）：title／description 原本固定寫死「台中磐石足球俱樂部」；
// 分類導覽改用共用元件 NewsCategoryTabs（理由同 academy.vue／player-stories.vue）；
// 「品牌識別包」整區塊下載的 SVG／PNG 檔案（`/assets/brand/svg/tcrfc-*`）是磐石專屬
// 的實際向量檔案，藍鯨目前只有隊徽點陣主檔、沒有向量（CLAUDE.md 品牌資產列「向量
// 原始檔仍未提供」），不得把磐石的向量檔案路徑掛在藍鯨站上假裝藍鯨也有一整包可下載
// 的品牌識別包，改為只對 tcrfc 顯示，bw 顯示誠實的「尚未提供」空狀態。
// S2-12（7.8 媒體專區）：新聞稿／品牌識別包／高解析圖庫三類資源讀 `GET /api/backend/{club}/press`（後台 B6，只回
// 已發布；共同列一併回、專屬優先，所以磐石與藍鯨各自只會看到自己的＋共用的資源）。
// 🔴 下載一律經同源代理 `/api/backend/{club}/press/{slug}/download`（API 累計下載次數後 302 到檔案），不直接輸出
// 檔案網址，這樣後台的「累計下載次數」才準。
// 🔴 磐石的「靜態品牌識別包」（隊徽 SVG／PNG、社群分享圖，直接由 logo 主檔向量萃取）是真實可下載檔案，保留為過渡內容；
// 後台 B6 建立的「品牌識別包」資源另列在其下，兩者並存、不互相取代。藍鯨沒有向量主檔（CLAUDE.md 品牌資產列），
// 只顯示後台 B6 建立的資源，沒有就誠實空狀態。
// 🔴 媒體聯絡窗口（7.8.4）導 10.6 媒體聯絡表單（規劃書 §3.7），不在本頁另放聯絡資料。
async function fetchPress(type: PressResourceType) {
  const { data } = await useFetch<PagedResponse<PressResource>>(`/api/backend/${club}/press`, {
    query: { type, lang: locale.value, pageSize: 100 },
    key: `press-${club}-${type}-${locale.value}`,
  })
  return computed(() => data.value?.items ?? [])
}
const [pressReleases, brandKits, hiresImages] = await Promise.all([
  fetchPress('press_release'),
  fetchPress('brand_kit'),
  fetchPress('hires_image'),
])

useSeoMeta({
  title: computed(() => `媒體專區 Media｜新聞 News｜${getClubAssets(club).nameZh}`),
  description: computed(() => (isTcrfc.value
    ? '台中磐石媒體專區：品牌識別包下載（隊徽 SVG／PNG、社群分享圖），新聞稿與高解析圖庫、媒體聯絡窗口建置中。'
    : '台中藍鯨媒體專區：新聞稿與高解析圖庫、媒體聯絡窗口建置中。')),
})
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/news/')">新聞 News</a></li>
      <li aria-current="page">媒體專區</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <ClubHeroBg :src="siteImg('/assets/img/nav-news.jpg')" width="1920" height="1279" />
  <div class="container">
    <p class="page-hero__eyebrow">7.8 Media</p>
    <h1>媒體專區<span class="en">Media</span></h1>
    <p class="page-hero__lede">提供媒體夥伴新聞稿下載、品牌識別包（Logo／CIS）、高解析圖庫與媒體聯絡窗口。</p>
  </div>
</section>

<section class="band" aria-labelledby="media-tabs-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="media-tabs-title">新聞分類導覽</h2>
    <div class="news-toolbar">
      <NewsCategoryTabs active="media" />
    </div>
  </div>
</section>

<section v-if="isTcrfc" class="band grain grain--2" aria-labelledby="brandkit-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker" style="color:var(--brand)">7.8.1</p>
        <h2 class="section-title" id="brandkit-title" style="color:#fff">品牌識別包</h2>
      </div>
      <p class="section-lede" style="color:var(--muted-dark)">直接向量萃取自 logo 主檔（<code>reference/TCR_logo_CMYK.ai</code>），可下載使用；請勿重繪或改動標誌造型。</p>
    </div>

    <div class="grid grid--3" style="margin-top:2.5rem">
      <div class="value-card">
        <p class="value-card__num">MARK</p>
        <p class="value-card__zh">隊徽（飛鳥）</p>
        <p class="value-card__desc">單獨隊徽圖形，適合小尺寸應用（社群大頭貼、favicon 等）。</p>
        <p style="display:flex;gap:.75rem;flex-wrap:wrap;margin-top:.5rem">
          <a class="btn btn--dark btn--sm" href="/assets/brand/svg/tcrfc-mark-pink.svg" download>桃紅 SVG</a>
          <a class="btn btn--dark btn--sm" href="/assets/brand/svg/tcrfc-mark-black.svg" download>黑 SVG</a>
          <a class="btn btn--dark btn--sm" href="/assets/brand/svg/tcrfc-mark-white.svg" download>白 SVG</a>
          <a class="btn btn--dark btn--sm" href="/assets/brand/png/tcrfc-mark-pink-512.png" download>桃紅 PNG</a>
          <a class="btn btn--dark btn--sm" href="/assets/brand/png/tcrfc-mark-white-512.png" download>白 PNG</a>
        </p>
      </div>
      <div class="value-card">
        <p class="value-card__num">STACKED</p>
        <p class="value-card__zh">隊徽＋TCRFC</p>
        <p class="value-card__desc">隊徽疊加英文簡稱的直式組合，適合方形版位。</p>
        <p style="display:flex;gap:.75rem;flex-wrap:wrap;margin-top:.5rem">
          <a class="btn btn--dark btn--sm" href="/assets/brand/svg/tcrfc-stacked-pink.svg" download>桃紅 SVG</a>
          <a class="btn btn--dark btn--sm" href="/assets/brand/svg/tcrfc-stacked-black.svg" download>黑 SVG</a>
          <a class="btn btn--dark btn--sm" href="/assets/brand/svg/tcrfc-stacked-white.svg" download>白 SVG</a>
        </p>
      </div>
      <div class="value-card">
        <p class="value-card__num">FULL</p>
        <p class="value-card__zh">完整組合標誌</p>
        <p class="value-card__desc">隊徽＋TCRFC＋「台中磐石足球俱樂部」全稱，適合正式文件與新聞稿封面。</p>
        <p style="display:flex;gap:.75rem;flex-wrap:wrap;margin-top:.5rem">
          <a class="btn btn--dark btn--sm" href="/assets/brand/svg/tcrfc-full-pink.svg" download>桃紅 SVG</a>
          <a class="btn btn--dark btn--sm" href="/assets/brand/svg/tcrfc-full-black.svg" download>黑 SVG</a>
          <a class="btn btn--dark btn--sm" href="/assets/brand/svg/tcrfc-full-white.svg" download>白 SVG</a>
          <a class="btn btn--dark btn--sm" href="/assets/brand/png/tcrfc-full-black-2048.png" download>黑 PNG</a>
          <a class="btn btn--dark btn--sm" href="/assets/brand/png/tcrfc-full-white-2048.png" download>白 PNG</a>
        </p>
      </div>
    </div>

    <p style="margin-top:2.5rem;color:var(--muted-dark);font-size:.9rem">另可下載社群分享圖（<span class="en">1200×630</span>，品牌黑底＋反白標誌＋雙語主張）：
      <a class="btn btn--primary btn--sm" href="/assets/brand/social/og-image.png" download style="margin-left:.75rem">下載分享圖 PNG</a>
    </p>
  </div>
</section>
<section v-else class="band grain grain--2" aria-labelledby="brandkit-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker" style="color:var(--brand)">7.8.1</p>
        <h2 class="section-title" id="brandkit-title" style="color:#fff">品牌識別包</h2>
      </div>
    </div>
    <p v-if="!brandKits.length" style="margin-top:1.5rem;color:var(--muted-dark)">品牌識別包（隊徽向量檔、社群分享圖）尚未提供，稍後將於本頁公布。</p>
    <ul v-else class="press-list press-list--dark">
      <li v-for="r in brandKits" :key="r.id" class="press-item">
        <div class="press-item__main">
          <p class="press-item__title">{{ r.title }}</p>
          <p v-if="r.description" class="press-item__desc">{{ r.description }}</p>
          <p class="press-item__meta">{{ [slashDate(r.publishedOn), fileKindLabel(r.fileExtension), formatFileSize(r.fileBytes)].filter(Boolean).join(' · ') }}</p>
        </div>
        <a v-if="pressDownloadHref(club, r.slug)" class="btn btn--primary btn--sm" :href="pressDownloadHref(club, r.slug) ?? undefined" :aria-label="`下載：${r.title}`">下載</a>
      </li>
    </ul>
  </div>
</section>

<!-- 磐石：後台 B6 另外建立的品牌識別包資源，列在靜態識別包之後（兩者並存） -->
<section v-if="isTcrfc && brandKits.length" class="band grain grain--2" aria-labelledby="brandkit-more-title">
  <div class="band-inner container">
    <h2 class="section-title" id="brandkit-more-title" style="color:#fff">更多識別素材</h2>
    <ul class="press-list press-list--dark">
      <li v-for="r in brandKits" :key="r.id" class="press-item">
        <div class="press-item__main">
          <p class="press-item__title">{{ r.title }}</p>
          <p v-if="r.description" class="press-item__desc">{{ r.description }}</p>
          <p class="press-item__meta">{{ [slashDate(r.publishedOn), fileKindLabel(r.fileExtension), formatFileSize(r.fileBytes)].filter(Boolean).join(' · ') }}</p>
        </div>
        <a v-if="pressDownloadHref(club, r.slug)" class="btn btn--primary btn--sm" :href="pressDownloadHref(club, r.slug) ?? undefined" :aria-label="`下載：${r.title}`">下載</a>
      </li>
    </ul>
  </div>
</section>

<section class="band" aria-labelledby="press-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">7.8.2</p>
        <h2 class="section-title" id="press-title">新聞稿下載</h2>
      </div>
    </div>
    <ul v-if="pressReleases.length" class="press-list">
      <li v-for="r in pressReleases" :key="r.id" class="press-item">
        <div class="press-item__main">
          <p class="press-item__title">{{ r.title }}</p>
          <p v-if="r.description" class="press-item__desc">{{ r.description }}</p>
          <p class="press-item__meta">{{ [slashDate(r.publishedOn), fileKindLabel(r.fileExtension), formatFileSize(r.fileBytes)].filter(Boolean).join(' · ') }}</p>
        </div>
        <a v-if="pressDownloadHref(club, r.slug)" class="btn btn--dark btn--sm" :href="pressDownloadHref(club, r.slug) ?? undefined" :aria-label="`下載：${r.title}`">下載</a>
      </li>
    </ul>
    <p v-else class="press-empty">新聞稿整理中，稍後將於本頁公布。</p>
  </div>
</section>

<section class="band" aria-labelledby="gallery-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">7.8.3</p>
        <h2 class="section-title" id="gallery-title">高解析圖庫</h2>
      </div>
    </div>
    <ul v-if="hiresImages.length" class="press-gallery">
      <li v-for="r in hiresImages" :key="r.id">
        <a v-if="pressDownloadHref(club, r.slug)" class="press-gallery__item" :href="pressDownloadHref(club, r.slug) ?? undefined" :aria-label="`下載高解析圖：${r.title}`">
          <img v-if="r.coverUrl" :src="r.coverUrl" :alt="r.description ?? r.title ?? ''" loading="lazy" width="640" height="427">
          <span class="press-gallery__cap">{{ r.title }}<small v-if="formatFileSize(r.fileBytes)"> · {{ formatFileSize(r.fileBytes) }}</small></span>
        </a>
      </li>
    </ul>
    <p v-else class="press-empty">高解析圖庫整理中，稍後將於本頁公布。</p>
  </div>
</section>

<section class="band" aria-labelledby="mediacontact-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">7.8.4 · 10.6</p>
        <h2 class="section-title" id="mediacontact-title">媒體聯絡窗口</h2>
      </div>
    </div>
    <p class="press-empty">媒體採訪、轉載授權與資料索取，請透過媒體聯絡表單與我們聯繫。</p>
    <p><a class="btn btn--dark btn--sm" :href="lp('/zh/join/media/')">前往媒體聯絡表單</a></p>
  </div>
</section>
</template>

<style>
/* ============================================================
   News listing components (07 NEWS & STORIES)
   Used on: news/ hub + all 7.x category pages + 7.8 media.
   Repeats identically across 9 pages — candidate for promotion
   into shared tcrfc.css, see build report.
   ============================================================ */
.news-toolbar{ display:flex; flex-direction:column; gap:1.25rem; margin-bottom:1.75rem; }

/* 7.8 媒體資源清單與圖庫（S2-12） */
.press-list{ list-style:none; padding:0; margin:1.5rem 0 0; display:flex; flex-direction:column; border-top:1px solid var(--rule); }
.press-item{ display:flex; gap:1.25rem; align-items:center; justify-content:space-between; padding:1.1rem 0; border-bottom:1px solid var(--rule); }
.press-item__main{ min-width:0; }
.press-item__title{ font-weight:800; color:var(--heading); line-height:1.5; }
.press-item__desc{ font-size:.88rem; line-height:1.7; color:var(--text); margin-top:.25rem; white-space:pre-line; }
.press-item__meta{ font-size:.78rem; font-weight:700; color:var(--muted); margin-top:.3rem; }
.press-list--dark{ border-top-color:rgba(255,255,255,.15); }
.press-list--dark .press-item{ border-bottom-color:rgba(255,255,255,.15); }
.press-list--dark .press-item__title{ color:#fff; }
.press-list--dark .press-item__desc, .press-list--dark .press-item__meta{ color:var(--muted-dark); }
.press-gallery{ list-style:none; padding:0; margin:1.5rem 0 0; display:grid; grid-template-columns:repeat(auto-fill,minmax(220px,1fr)); gap:1rem; }
.press-gallery__item{ display:block; text-decoration:none; color:inherit; }
.press-gallery__item img{ width:100%; aspect-ratio:3/2; object-fit:cover; display:block; background:var(--paper-2); }
.press-gallery__cap{ display:block; font-size:.82rem; font-weight:700; margin-top:.4rem; color:var(--heading); }
.press-gallery__cap small{ font-weight:500; color:var(--muted); }
.press-empty{ margin:1.25rem 0; color:var(--muted); font-size:.9rem; }
@media (max-width:560px){ .press-item{ flex-direction:column; align-items:flex-start; } }
</style>

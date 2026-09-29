<script setup lang="ts">
// app/pages/zh/news/media.vue — 由 site/src/pages/zh/news/media/index.html 轉來（S0-9 靜態頁搬遷）
definePageMeta({ nav: "news", unit: "07" })

const config = useRuntimeConfig()
const club = config.public.club
const isTcrfc = computed(() => club !== 'bw')
const { lp } = useLocale()

// BW-C1（品牌外洩全站盤點）：title／description 原本固定寫死「台中磐石足球俱樂部」；
// 分類導覽改用共用元件 NewsCategoryTabs（理由同 academy.vue／player-stories.vue）；
// 「品牌識別包」整區塊下載的 SVG／PNG 檔案（`/assets/brand/svg/tcrfc-*`）是磐石專屬
// 的實際向量檔案，藍鯨目前只有隊徽點陣主檔、沒有向量（CLAUDE.md 品牌資產列「向量
// 原始檔仍未提供」），不得把磐石的向量檔案路徑掛在藍鯨站上假裝藍鯨也有一整包可下載
// 的品牌識別包，改為只對 tcrfc 顯示，bw 顯示誠實的「尚未提供」空狀態。
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
  <img class="page-hero__bg" src="/assets/img/nav-news.jpg" alt="" width="1920" height="1279">
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
    <p style="margin-top:1.5rem;color:var(--muted-dark)">品牌識別包（隊徽向量檔、社群分享圖）尚未提供，稍後將於本頁公布。</p>
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
</style>

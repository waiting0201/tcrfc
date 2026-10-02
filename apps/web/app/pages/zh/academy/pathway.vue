<script setup lang="ts">
// app/pages/zh/academy/pathway.vue — 由 site/src/pages/zh/academy/pathway/index.html 轉來（S0-9 靜態頁搬遷）
//
// S2-8（2026-09-29）：改為細粒度 unit '4.3'（原本粗粒度 '04' 零俱樂部分支，藍鯨容器
// 會直接顯示磐石內容，屬於既有缺口，見 units.ts 檔頭說明）。本頁全部既有內容都是
// 「準備中」通用佔位文字，沒有磐石專屬真實事實，改為兩俱樂部共用同一份文案（只換
// 抬頭與 CTA 連結），不需要臆造新內容。
definePageMeta({ nav: "academy", unit: "4.3" })

const { lp } = useLocale()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')
const identity = computed(() => getClubIdentity(clubKey.value))

// S1-12d 收尾：梯隊代碼改讀 useSiteFacts(clubKey)（後端公開端點）。
const { academyLabel } = useSiteFacts(clubKey.value)

useSeoMeta({
  title: computed(() => getAcademyPathwaySeo(clubKey.value).title),
  description: computed(() => getAcademyPathwaySeo(clubKey.value).description),
})
const hero = computed(() => getAcademyPathwayHero(clubKey.value))
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/academy/')">{{ identity.academyLabelZh }}</a></li>
      <li aria-current="page">{{ identity.academyShortLabelZh }}發展路徑</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <!-- 藍鯨無青年隊訓練照片可用（客戶尚未提供，肖像同意狀態未知），不沿用磐石學院照片頂替 -->
  <img v-if="isTcrfc" class="page-hero__bg" :src="siteImg('/assets/img/academy/life-08.jpg')" alt="" width="1600" height="900">
  <div v-else class="page-hero__bg page-hero__bg--pending" aria-hidden="true" />
  <div class="container">
    <p class="page-hero__eyebrow">{{ isTcrfc ? '4.3 Academy Pathway' : '4.3' }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<section class="band">
  <div class="container">

    <ol class="path-track">
      <li class="path-step">
        <details>
          <summary>
            <span class="path-step__num" aria-hidden="true">01</span>
            <span class="path-step__body">
              <span class="path-step__en">Stage 1 · U12</span>
              <span class="path-step__zh">起步階段</span>
            </span>
            <span class="path-step__chevron" aria-hidden="true">＋</span>
          </summary>
          <div class="path-step__detail">
            <p>詳細說明準備中，稍後將於本頁公布。</p>
          </div>
        </details>
      </li>
      <li class="path-step">
        <details>
          <summary>
            <span class="path-step__num" aria-hidden="true">02</span>
            <span class="path-step__body">
              <span class="path-step__en">Stage 2 · U15</span>
              <span class="path-step__zh">深化階段</span>
            </span>
            <span class="path-step__chevron" aria-hidden="true">＋</span>
          </summary>
          <div class="path-step__detail">
            <p>詳細說明準備中，稍後將於本頁公布。</p>
          </div>
        </details>
      </li>
      <li class="path-step">
        <details>
          <summary>
            <span class="path-step__num" aria-hidden="true">03</span>
            <span class="path-step__body">
              <span class="path-step__en">Stage 3 · First Team / Overseas</span>
              <span class="path-step__zh">一線隊／海外</span>
            </span>
            <span class="path-step__chevron" aria-hidden="true">＋</span>
          </summary>
          <div class="path-step__detail">
            <p>詳細說明準備中，稍後將於本頁公布。</p>
          </div>
        </details>
      </li>
    </ol>

  </div>
</section>

<section class="band grain cta-band">
  <div class="container">
    <div class="cta-grid">
      <a class="cta-card" :href="lp('/zh/academy/teams/')">
        <span class="cta-card__num">4.2</span>
        <span class="cta-card__title">{{ identity.academyShortLabelZh }}隊伍</span>
        <p class="cta-card__desc">查看 {{ academyLabel() }} 各梯隊</p>
      </a>
      <a class="cta-card" :href="lp('/zh/academy/curriculum/')">
        <span class="cta-card__num">4.4</span>
        <span class="cta-card__title">訓練課程與課綱</span>
        <p class="cta-card__desc">五大訓練面向與週期規劃</p>
      </a>
      <!-- 4.7 加入學院對藍鯨已整頁關閉（既有 units.ts，S1-15），不連結一個會 404 的頁面。 -->
      <a v-if="isTcrfc" class="cta-card" :href="lp('/zh/academy/join/')">
        <span class="cta-card__num">4.7</span>
        <span class="cta-card__title">加入學院</span>
        <p class="cta-card__desc">招生對象與遴選流程</p>
      </a>
    </div>
  </div>
</section>
</template>

<style>
/* ── 4.3 專屬元件：階梯路徑（<details> 逐階展開，無需 JS）──
   3.4 國際發展通道也需要「路徑圖，各階段可點擊」，建議把 .path-track 收進 tcrfc.css 共用。 */
.path-track{ list-style:none; max-width:60rem; position:relative; }
.path-track::before{
  content:""; position:absolute; left:1.85rem; top:2.5rem; bottom:2.5rem; width:2px; background:var(--rule);
}
.path-step{ position:relative; margin-bottom:1.25rem; }
.path-step details{ background:var(--paper-2); border:1px solid var(--rule); }
.path-step summary{
  list-style:none; cursor:pointer; display:flex; align-items:center; gap:1.25rem;
  padding:1.5rem 1.75rem; position:relative; z-index:1;
}
.path-step summary::-webkit-details-marker{ display:none; }
.path-step__num{
  flex:0 0 auto; width:3.7rem; height:3.7rem; border-radius:50%; background:var(--ink); color:#fff;
  display:flex; align-items:center; justify-content:center; font-weight:900; font-size:1.1rem;
}
.path-step__body{ display:flex; flex-direction:column; gap:.2rem; flex:1 1 auto; min-width:0; }
.path-step__en{ font-size:.72rem; font-weight:800; letter-spacing:.08em; text-transform:uppercase; color:var(--brand-aa); }
.path-step__zh{ font-size:1.25rem; font-weight:900; color:var(--heading); }
.path-step__chevron{ flex:0 0 auto; font-size:1.4rem; font-weight:300; color:var(--muted); transition:transform var(--dur-fast) var(--ease); }
.path-step details[open] .path-step__chevron{ transform:rotate(45deg); }
.path-step details[open] summary{ border-bottom:1px solid var(--rule); }
.path-step__detail{ padding:1.5rem 1.75rem 1.75rem calc(1.75rem + 3.7rem + 1.25rem); }
@media (max-width:640px){
  .path-track::before{ display:none; }
  .path-step__detail{ padding-left:1.75rem; }
}
</style>

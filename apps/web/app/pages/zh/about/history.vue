<script setup lang="ts">
// app/pages/zh/about/history.vue — 由 site/src/pages/zh/about/history/index.html 轉來（S0-9 靜態頁搬遷）
//
// 文案依俱樂部切換：藍鯨版逐字節錄 content/blue-whale/club-profile.md §4
// 「沿革 HISTORY（2014–2025，原文照錄）」，見 club-copy.ts 的 HISTORY_YEARS_BW。
definePageMeta({ nav: "about", unit: "02" })

const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const identity = computed(() => getClubIdentity(clubKey.value))
const hero = computed(() => HISTORY_HERO[clubKey.value])

useSeoMeta({
  title: computed(() => HISTORY_SEO[clubKey.value].title),
  description: computed(() => HISTORY_SEO[clubKey.value].description),
})
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a href="/zh/">首頁</a></li>
      <li><a href="/zh/about/">{{ identity.aboutLabelZh }}</a></li>
      <li aria-current="page">俱樂部歷程</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <img class="page-hero__bg" src="/assets/img/nav-about.jpg" alt="" width="1600" height="900">
  <div class="container">
    <p class="page-hero__eyebrow">{{ aboutEyebrow('2.7', clubKey) }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <!-- tcrfc 版 lede 含既有 mockup 的內嵌連結標記（見 club-copy.ts HISTORY_HERO 註解），故用 v-html；
         bw 版是純文字，v-html 對它是安全的 no-op（沒有標記可解析）。內容全部來自本頁資料層，非使用者輸入。 -->
    <p class="page-hero__lede" v-html="hero.lede"></p>
  </div>
</section>

<section class="band history-band" aria-labelledby="history-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="history-title">俱樂部歷程</h2>
    <div v-if="clubKey === 'tcrfc'" class="prose">
      <p>台中磐石足球俱樂部（Taichung Rock FC）於 <strong>2024 年</strong>在台中成立，成立當年即拿下<strong>全國乙級聯賽冠軍</strong>，並持續擴展一線隊、學院與國際交流網絡。</p>
    </div>
    <div v-else class="prose history-years">
      <div v-for="y in HISTORY_YEARS_BW" :key="y.year" class="history-year">
        <h3 class="history-year__title">{{ y.year }} 年</h3>
        <ul class="history-year__list">
          <li v-for="(item, i) in y.itemsZh" :key="i">{{ item }}</li>
        </ul>
      </div>
      <p class="field-hint">上列沿革整理自舊官網公開內容，部分年度屆數標示與其他資料有出入，正式版本將於俱樂部確認後更新。</p>
    </div>
  </div>
</section>
</template>

<style>
/* 【2.7 藍鯨版沿革】逐年清單——磐石本頁沒有對等內容，這份樣式只影響藍鯨站 */
.history-years{ display:flex; flex-direction:column; gap:2.25rem; }
.history-year__title{ font-size:1.3rem; font-weight:900; color:var(--brand-aa); margin-bottom:.6rem; }
.history-year__list{ margin:0; padding-left:1.25rem; display:flex; flex-direction:column; gap:.35rem; }
.history-year__list li{ line-height:1.6; }
</style>

<style>
.history-band{ background:var(--paper); padding-block:clamp(3.5rem,6vw,6rem); }
</style>

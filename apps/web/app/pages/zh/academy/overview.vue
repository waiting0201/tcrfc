<script setup lang="ts">
// app/pages/zh/academy/overview.vue — 由 site/src/pages/zh/academy/overview/index.html 轉來
// （S0-9 靜態頁搬遷），S1-15 補上俱樂部分支文案。
//
// 🔴 S1-15（2026-09-29）之前本頁對兩俱樂部零分支，字面寫死「台中磐石足球學院」——
// bw 容器直接顯示磐石學院內容，是既有缺口（不是本輪新增的迴歸）。藍鯨規劃書 §3.4
// 「04 青年隊沿用主站 04 的梯隊版型」（本頁屬於 04 單元的總覽子頁，適用同一句），
// 本輪改為讀 shared/utils/club-copy.ts 的 ACADEMY_OVERVIEW_*／ACADEMY_POSITIONING，
// 藍鯨文案逐句節錄自 content/blue-whale/squad/youth-teams.md（紀律 11）。
//
// unit 由粗粒度 '04' 改為 '4.1'，讓 units.ts 之後如果要單獨關閉某個 04 子頁
// （比照本輪 4.7 的做法）不必牽動本頁——見 shared/utils/units.ts 說明。
definePageMeta({ nav: "academy", unit: "4.1" })

const { lp } = useLocale()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')
const identity = computed(() => getClubIdentity(clubKey.value))
const hero = computed(() => ACADEMY_OVERVIEW_HERO[clubKey.value])

useSeoMeta({
  title: computed(() => ACADEMY_OVERVIEW_SEO[clubKey.value].title),
  description: computed(() => ACADEMY_OVERVIEW_SEO[clubKey.value].description),
})
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/academy/')">{{ identity.academyLabelZh }}</a></li>
      <li aria-current="page">{{ isTcrfc ? '學院總覽' : '青年隊總覽' }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <!-- 藍鯨無青年隊訓練照片可用（客戶尚未提供，肖像同意狀態未知），不沿用磐石學院照片頂替 -->
  <img v-if="isTcrfc" class="page-hero__bg" src="/assets/img/academy/life-13.jpg" alt="" width="1600" height="900">
  <div v-else class="page-hero__bg page-hero__bg--pending" aria-hidden="true"></div>
  <div class="container">
    <p class="page-hero__eyebrow">{{ isTcrfc ? '4.1 Academy Overview' : '4.1' }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<section class="band">
  <div class="container">
    <div class="prose">
      <h2>{{ isTcrfc ? '學院定位' : '青年隊定位' }}</h2>
      <p>{{ ACADEMY_POSITIONING[clubKey] }}</p>
      <p v-if="isTcrfc">詳見 <a :href="lp('/zh/academy/pathway/')">4.3 學院發展路徑</a>。</p>
    </div>

    <h2 class="section-title" style="margin-top:3.5rem;">數據亮點</h2>
    <p class="section-lede" style="margin-top:.5rem;">學員數、教練數、升學率等數據，未來將於本頁公布。</p>
  </div>
</section>

<section class="band grain cta-band">
  <div class="container">
    <div class="cta-grid">
      <a class="cta-card" :href="lp('/zh/academy/teams/')">
        <span class="cta-card__num">4.2</span>
        <span class="cta-card__title">{{ isTcrfc ? '學院隊伍' : '青年隊' }}</span>
        <p class="cta-card__desc">{{ isTcrfc ? `查看 ${academyTeamCodesLabel('tcrfc')} 各梯隊` : `查看 ${academyTeamCodesLabel('bw')} 兩個梯隊` }}</p>
      </a>
      <template v-if="isTcrfc">
        <a class="cta-card" :href="lp('/zh/academy/coaches/')">
          <span class="cta-card__num">4.5</span>
          <span class="cta-card__title">學院教練團</span>
          <p class="cta-card__desc">認識帶領各梯隊的教練</p>
        </a>
        <a class="cta-card" :href="lp('/zh/academy/join/')">
          <span class="cta-card__num">4.7</span>
          <span class="cta-card__title">加入學院</span>
          <p class="cta-card__desc">招生對象與遴選流程</p>
        </a>
      </template>
      <a v-else class="cta-card" :href="lp('/zh/schedule/')">
        <span class="cta-card__num">13</span>
        <span class="cta-card__title">賽事行事曆</span>
        <p class="cta-card__desc">查看俱樂部完整賽事時程</p>
      </a>
    </div>
  </div>
</section>
</template>

<style>
/* 藍鯨無青年隊訓練照片時的純色回退，只用既有 token（比照 club/first-team/index.vue 既有寫法） */
.page-hero__bg--pending{ background:linear-gradient(160deg, var(--ink) 0%, var(--brand-deep) 100%); }
</style>

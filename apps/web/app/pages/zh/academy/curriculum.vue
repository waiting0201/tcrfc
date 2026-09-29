<script setup lang="ts">
// app/pages/zh/academy/curriculum.vue — 由 site/src/pages/zh/academy/curriculum/index.html 轉來（S0-9 靜態頁搬遷）
//
// S2-8（2026-09-29）：改為細粒度 unit '4.4'（原本粗粒度 '04' 零俱樂部分支，藍鯨容器
// 會直接顯示磐石內容，屬於既有缺口，見 units.ts 檔頭說明）。本頁全部既有內容都是
// 「準備中」通用佔位文字，沒有磐石專屬真實事實，改為兩俱樂部共用同一份文案（只換
// 抬頭與 CTA 連結），不需要臆造新內容。
definePageMeta({ nav: "academy", unit: "4.4" })

const { lp } = useLocale()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')
const identity = computed(() => getClubIdentity(clubKey.value))

useSeoMeta({
  title: computed(() => getAcademyCurriculumSeo(clubKey.value).title),
  description: computed(() => getAcademyCurriculumSeo(clubKey.value).description),
})
const hero = computed(() => getAcademyCurriculumHero(clubKey.value))
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/academy/')">{{ identity.academyLabelZh }}</a></li>
      <li aria-current="page">訓練課程與課綱</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <!-- 藍鯨無青年隊訓練照片可用（客戶尚未提供，肖像同意狀態未知），不沿用磐石學院照片頂替 -->
  <img v-if="isTcrfc" class="page-hero__bg" src="/assets/img/academy/life-06.jpg" alt="" width="1600" height="900">
  <div v-else class="page-hero__bg page-hero__bg--pending" aria-hidden="true" />
  <div class="container">
    <p class="page-hero__eyebrow">{{ isTcrfc ? '4.4 Training &amp; Curriculum' : '4.4' }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<section class="band values-band">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">Five Pillars</p>
        <h2 class="section-title">五大訓練面向</h2>
      </div>
    </div>
    <div class="values-grid">
      <div class="value-card clip-card">
        <span class="value-card__num">01</span>
        <span class="value-card__en">Technical</span>
        <span class="value-card__zh">技術</span>
        <p class="value-card__desc">球感、控球、傳接與射門等基本功，是戰術與比賽表現的基礎。</p>
      </div>
      <div class="value-card clip-card">
        <span class="value-card__num">02</span>
        <span class="value-card__en">Tactical</span>
        <span class="value-card__zh">戰術</span>
        <p class="value-card__desc">個人與團隊的攻守觀念、站位與空間判讀。</p>
      </div>
      <div class="value-card clip-card">
        <span class="value-card__num">03</span>
        <span class="value-card__en">Physical</span>
        <span class="value-card__zh">體能</span>
        <p class="value-card__desc">速度、爆發力、耐力與協調性，支撐高強度比賽節奏。</p>
      </div>
      <div class="value-card clip-card">
        <span class="value-card__num">04</span>
        <span class="value-card__en">Game Reading</span>
        <span class="value-card__zh">比賽判讀</span>
        <p class="value-card__desc">閱讀場上局勢、即時做出決策的能力。</p>
      </div>
      <div class="value-card clip-card">
        <span class="value-card__num">05</span>
        <span class="value-card__en">Character</span>
        <span class="value-card__zh">品格</span>
        <p class="value-card__desc">團隊精神、紀律與運動家精神的培養。</p>
      </div>
    </div>
  </div>
</section>

<section class="band">
  <div class="container">
    <h2 class="section-title">課綱與週期規劃表</h2>
    <p class="section-lede" style="margin-top:.5rem;">各面向的具體課綱內容與週期（年度／學期／週）訓練配比，待客戶資料到位後填入下表。</p>
    <div class="data-table-wrap" style="margin-top:2rem;">
      <table class="data-table">
        <caption class="visually-hidden">五大訓練面向課綱與週期規劃表</caption>
        <thead>
          <tr>
            <th scope="col">面向</th>
            <th scope="col">課綱重點</th>
            <th scope="col">週期規劃</th>
          </tr>
        </thead>
        <tbody>
          <tr><th scope="row">技術</th><td class="pending-cell">內容準備中</td><td class="pending-cell">內容準備中</td></tr>
          <tr><th scope="row">戰術</th><td class="pending-cell">內容準備中</td><td class="pending-cell">內容準備中</td></tr>
          <tr><th scope="row">體能</th><td class="pending-cell">內容準備中</td><td class="pending-cell">內容準備中</td></tr>
          <tr><th scope="row">比賽判讀</th><td class="pending-cell">內容準備中</td><td class="pending-cell">內容準備中</td></tr>
          <tr><th scope="row">品格</th><td class="pending-cell">內容準備中</td><td class="pending-cell">內容準備中</td></tr>
        </tbody>
      </table>
    </div>
  </div>
</section>

<section class="band grain cta-band">
  <div class="container">
    <div class="cta-grid">
      <a class="cta-card" :href="lp('/zh/academy/pathway/')">
        <span class="cta-card__num">4.3</span>
        <span class="cta-card__title">{{ identity.academyShortLabelZh }}發展路徑</span>
        <p class="cta-card__desc">{{ isTcrfc ? '從 U12 到一線隊／海外的成長路徑' : '從 U12 到一線隊的成長路徑' }}</p>
      </a>
      <!-- BW-C1：4.5 已重開（見 shared/utils/units.ts 檔頭），移除既有的 isTcrfc 隱藏。 -->
      <a class="cta-card" :href="lp('/zh/academy/coaches/')">
        <span class="cta-card__num">4.5</span>
        <span class="cta-card__title">{{ identity.academyShortLabelZh }}教練團</span>
        <p class="cta-card__desc">認識帶領各梯隊的教練</p>
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
/* ── 4.4 專屬元件：資料表格 ──────────────────────────
   全站目前沒有共用表格樣式；4.7 費用表、5.x 課表也會用到，
   建議把 .data-table 收進 tcrfc.css 共用。 */
.data-table-wrap{ overflow-x:auto; border:1px solid var(--rule); }
.data-table{ width:100%; border-collapse:collapse; min-width:480px; }
.data-table th, .data-table td{ padding:.9rem 1.1rem; text-align:left; border-bottom:1px solid var(--rule); font-size:.9rem; }
.data-table thead th{ background:var(--paper-2); font-weight:800; color:var(--heading); }
.data-table tbody th{ font-weight:700; color:var(--heading); white-space:nowrap; }
.data-table tbody tr:last-child th, .data-table tbody tr:last-child td{ border-bottom:0; }
.pending-cell{ color:var(--brand-deep); font-weight:700; }
</style>

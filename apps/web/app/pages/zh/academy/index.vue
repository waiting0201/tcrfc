<script setup lang="ts">
// app/pages/zh/academy/index.vue — 由 site/src/pages/zh/academy/index/index.html 轉來（S0-9 靜態頁搬遷）
//
// BW-C1（品牌外洩全站盤點）：本頁原本整頁固定磐石內容（SEO／H1／七張導覽卡／底部
// CTA 全部沒有俱樂部分支），是本輪全站掃描才發現的既有缺口。4.7（加入學院）對藍鯨
// 是 units.ts 明文關閉的單元（藍鯨規劃書 §3.4「04 不沿用招生與課程報名架構」），
// bw 版導覽卡與底部 CTA 對應移除，不連到會 404 的頁面。改讀 club-copy.ts 的
// getAcademyHubSeo()／getAcademyHubHero()／getAcademyHubCards()／getAcademyHubCtaTitle()。
definePageMeta({ nav: "academy", unit: "04" })

const { lp } = useLocale()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')
const identity = computed(() => getClubIdentity(clubKey.value))

const { facts } = useSiteFacts(clubKey.value)

useSeoMeta({
  title: computed(() => getAcademyHubSeo(clubKey.value, facts.value).title),
  description: computed(() => getAcademyHubSeo(clubKey.value, facts.value).description),
})
const hero = computed(() => getAcademyHubHero(clubKey.value, facts.value))
const cards = computed(() => getAcademyHubCards(clubKey.value, facts.value))
const ctaTitle = computed(() => getAcademyHubCtaTitle(clubKey.value))
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li aria-current="page">{{ identity.academyLabelZh }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <ClubHeroBg :src="siteImg('/assets/img/academy/life-05.jpg')" width="1600" height="900" />
  <div class="container">
    <p class="page-hero__eyebrow">{{ isTcrfc ? '04 Academy' : '04' }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<section class="band unit-nav-band">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">{{ isTcrfc ? 'Explore the Academy' : 'Explore the Youth Team' }}</p>
        <h2 class="section-title">{{ identity.academyShortLabelZh }}導覽</h2>
      </div>
      <p v-if="isTcrfc" class="section-lede">從總覽、隊伍到加入方式，七個子單元帶你認識台中磐石足球學院。</p>
      <p v-else class="section-lede">從總覽、隊伍到隊伍日常，六個子單元帶你認識台中藍鯨青年隊。</p>
    </div>

    <div class="unit-nav-grid">
      <a v-for="(c, i) in cards" :key="c.num" class="unit-nav-card clip-card" :class="{ 'unit-nav-card--cta': isTcrfc && i === cards.length - 1 }" :href="lp(c.href)">
        <span class="unit-nav-card__num">{{ c.num }}</span>
        <span class="unit-nav-card__en">{{ c.titleEn }}</span>
        <span class="unit-nav-card__zh">{{ c.titleZh }}</span>
        <span class="unit-nav-card__desc">{{ c.descZh }}</span>
        <span class="unit-nav-card__arrow" aria-hidden="true">→</span>
      </a>
    </div>
  </div>
</section>

<section class="band grain cta-strip">
  <div class="container cta-strip__inner">
    <div>
      <p class="kicker kicker--on-dark">Ready to join?</p>
      <h2 class="section-title" style="color:#fff;">{{ ctaTitle }}</h2>
    </div>
    <div class="cta-strip__actions">
      <a class="btn btn--primary" :href="lp('/zh/join/academy/')">線上申請</a>
      <a v-if="isTcrfc" class="btn btn--light" :href="lp('/zh/academy/join/')">查看招生資訊</a>
    </div>
  </div>
</section>
</template>

<style>
/* ── 4.x Academy 專屬樣式 ──────────────────────────────
   unit-nav-* 與 cta-strip 若三頁以上共用，建議收進 tcrfc.css（見交付回報） */
.unit-nav-band{ padding-block:clamp(4rem,7vw,6.5rem); background:var(--paper); }
.unit-nav-grid{
  display:grid; grid-template-columns:repeat(auto-fit,minmax(260px,1fr)); gap:1.5rem;
}
.unit-nav-card{
  position:relative; display:flex; flex-direction:column; gap:.4rem;
  background:var(--paper-2); padding:1.85rem 1.75rem 2.1rem;
  border:1px solid var(--rule); min-height:190px;
  transition:transform var(--dur) var(--ease), border-color var(--dur) var(--ease), background var(--dur) var(--ease);
}
.unit-nav-card:hover{ transform:translateY(-4px); border-color:var(--brand); background:var(--paper); }
.unit-nav-card--cta{ background:var(--ink); border-color:var(--ink); }
.unit-nav-card--cta .unit-nav-card__num,
.unit-nav-card--cta .unit-nav-card__zh{ color:#fff; }
.unit-nav-card--cta .unit-nav-card__desc{ color:var(--muted-dark); }
.unit-nav-card--cta:hover{ border-color:var(--brand-bright); background:var(--ink-2); }
.unit-nav-card__num{ font-size:.72rem; font-weight:800; letter-spacing:.08em; color:var(--muted); }
.unit-nav-card__en{ font-size:.78rem; font-weight:800; letter-spacing:.06em; text-transform:uppercase; color:var(--brand-aa); }
.unit-nav-card__zh{ font-size:1.2rem; font-weight:900; letter-spacing:-.01em; color:var(--heading); }
.unit-nav-card__desc{ font-size:.85rem; color:var(--muted); line-height:1.6; margin-top:.15rem; }
.unit-nav-card__arrow{ margin-top:auto; align-self:flex-end; font-weight:800; color:var(--brand-aa); transition:transform var(--dur-fast) var(--ease); }
.unit-nav-card:hover .unit-nav-card__arrow{ transform:translateX(4px); }

.cta-strip{ background-color:var(--ink); padding-block:clamp(3rem,5vw,4.5rem); }
.cta-strip__inner{ display:flex; align-items:center; justify-content:space-between; gap:1.75rem; flex-wrap:wrap; }
.cta-strip__actions{ display:flex; gap:1rem; flex-wrap:wrap; }
</style>

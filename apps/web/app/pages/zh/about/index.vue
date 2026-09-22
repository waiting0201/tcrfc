<script setup lang="ts">
// app/pages/zh/about/index.vue — 由 site/src/pages/zh/about/index/index.html 轉來（S0-9 靜態頁搬遷）
//
// 文案依俱樂部切換（docs/13-blue-whale-site.md §6 紀律 11）：本頁 SEO／頁首／
// 導覽卡描述一律取自 shared/utils/club-copy.ts，不在頁面內硬編碼俱樂部名稱。
definePageMeta({ nav: "about", unit: "02" })

const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const identity = computed(() => getClubIdentity(clubKey.value))
const assets = computed(() => getClubAssets(clubKey.value))
const hero = computed(() => ABOUT_INDEX_HERO[clubKey.value])
const navDesc = computed(() => ABOUT_NAV_DESC[clubKey.value])

useSeoMeta({
  title: computed(() => ABOUT_INDEX_SEO[clubKey.value].title),
  description: computed(() => ABOUT_INDEX_SEO[clubKey.value].description),
})
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a href="/zh/">首頁</a></li>
      <li aria-current="page">{{ identity.aboutLabelZh }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <img class="page-hero__bg" src="/assets/img/nav-about.jpg" alt="" width="1600" height="900">
  <div class="container">
    <p class="page-hero__eyebrow">{{ aboutEyebrow('02', clubKey) }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<section class="band about-landing" aria-labelledby="about-nav-title">
  <span class="ghost-num ghost-num--light" aria-hidden="true">02</span>
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">EIGHT CHAPTERS</p>
        <h2 class="section-title" id="about-nav-title">認識{{ assets.nameZh }}</h2>
      </div>
      <p class="section-lede">從故事、理念到治理，逐篇了解{{ assets.nameZh }}。</p>
    </div>

    <div class="about-nav-grid">
      <a class="about-nav-card clip-card" href="/zh/about/our-story/">
        <p class="about-nav-card__num">2.1</p>
        <p class="about-nav-card__en">Our Story</p>
        <p class="about-nav-card__zh">我們的故事</p>
        <p class="about-nav-card__desc">{{ navDesc.ourStory }}</p>
        <span class="about-nav-card__link">閱讀故事 <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg></span>
      </a>
      <a class="about-nav-card clip-card" href="/zh/about/vision-mission/">
        <p class="about-nav-card__num">2.2</p>
        <p class="about-nav-card__en">Vision &amp; Mission</p>
        <p class="about-nav-card__zh">願景與使命</p>
        <p class="about-nav-card__desc">{{ navDesc.visionMission }}</p>
        <span class="about-nav-card__link">了解願景 <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg></span>
      </a>
      <a class="about-nav-card clip-card" href="/zh/about/philosophy/">
        <p class="about-nav-card__num">2.3</p>
        <p class="about-nav-card__en">Our Philosophy</p>
        <p class="about-nav-card__zh">足球理念</p>
        <p class="about-nav-card__desc">{{ navDesc.philosophy }}</p>
        <span class="about-nav-card__link">認識理念 <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg></span>
      </a>
      <a class="about-nav-card clip-card" href="/zh/about/our-people/">
        <p class="about-nav-card__num">2.4</p>
        <p class="about-nav-card__en">Our People</p>
        <p class="about-nav-card__zh">團隊成員</p>
        <p class="about-nav-card__desc">{{ navDesc.ourPeople }}</p>
        <span class="about-nav-card__link">查看團隊 <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg></span>
      </a>
      <a class="about-nav-card clip-card" href="/zh/about/governance/">
        <p class="about-nav-card__num">2.5</p>
        <p class="about-nav-card__en">Governance</p>
        <p class="about-nav-card__zh">治理與管理</p>
        <p class="about-nav-card__desc">組織架構、治理原則與公開文件。</p>
        <span class="about-nav-card__link">了解治理 <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg></span>
      </a>
      <a class="about-nav-card clip-card" href="/zh/about/ecosystem/">
        <p class="about-nav-card__num">2.6</p>
        <p v-if="identity.brandTagEn" class="about-nav-card__en">{{ identity.brandTagEn }} Ecosystem</p>
        <p v-else class="about-nav-card__en">Ecosystem</p>
        <p class="about-nav-card__zh">生態系</p>
        <p class="about-nav-card__desc">{{ navDesc.ecosystem }}</p>
        <span class="about-nav-card__link">查看生態系 <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg></span>
      </a>
      <a class="about-nav-card clip-card" href="/zh/about/history/">
        <p class="about-nav-card__num">2.7</p>
        <p class="about-nav-card__en">Club History</p>
        <p class="about-nav-card__zh">俱樂部歷程</p>
        <p class="about-nav-card__desc">{{ navDesc.history }}</p>
        <span class="about-nav-card__link">回顧歷程 <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg></span>
      </a>
      <a class="about-nav-card clip-card" href="/zh/about/milestones/">
        <p class="about-nav-card__num">2.8</p>
        <p class="about-nav-card__en">Key Milestones</p>
        <p class="about-nav-card__zh">重要里程碑</p>
        <p class="about-nav-card__desc">按年份檢視俱樂部的重要大事記。</p>
        <span class="about-nav-card__link">查看時間軸 <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg></span>
      </a>
    </div>
  </div>
</section>
</template>

<style>
/* 【02 ABOUT landing】導覽卡格線 —— 若其他單元 landing 頁也採同款卡片，建議收進共用 CSS */
.about-landing{ background:var(--paper); padding-block:clamp(4rem,7vw,6.5rem); }
.about-nav-grid{
  display:grid; gap:clamp(1rem,2.5vw,1.75rem);
  grid-template-columns:repeat(auto-fit,minmax(260px,1fr));
}
.about-nav-card{
  display:flex; flex-direction:column; gap:.4rem;
  background:var(--paper-2); padding:1.75rem 1.6rem 2rem;
  border:1px solid var(--rule); transition:transform var(--dur-fast) var(--ease), border-color var(--dur-fast) var(--ease);
}
.about-nav-card:hover{ transform:translateY(-4px); border-color:var(--brand-aa); }
.about-nav-card__num{ font-size:.78rem; font-weight:800; letter-spacing:.06em; color:var(--brand-aa); }
.about-nav-card__en{ font-size:.72rem; font-weight:700; letter-spacing:.08em; text-transform:uppercase; color:var(--muted); margin-top:.35rem; }
.about-nav-card__zh{ font-size:1.25rem; font-weight:900; letter-spacing:-.01em; color:var(--heading); }
.about-nav-card__desc{ font-size:.86rem; color:var(--muted); line-height:1.6; flex:1; margin-top:.15rem; }
.about-nav-card__link{ font-size:.8rem; font-weight:700; color:var(--brand-aa); display:inline-flex; align-items:center; gap:.35em; margin-top:.4rem; }
.about-nav-card__link svg{ width:14px; height:14px; transition:transform var(--dur-fast) var(--ease); }
.about-nav-card:hover .about-nav-card__link svg{ transform:translateX(4px); }
</style>

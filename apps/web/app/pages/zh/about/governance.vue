<script setup lang="ts">
// app/pages/zh/about/governance.vue — 由 site/src/pages/zh/about/governance/index.html 轉來（S0-9 靜態頁搬遷）
definePageMeta({ nav: "about", unit: "02" })

const { lp } = useLocale()

const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const identity = computed(() => getClubIdentity(clubKey.value))
const hero = computed(() => GOVERNANCE_HERO[clubKey.value])

useSeoMeta({
  title: computed(() => GOVERNANCE_SEO[clubKey.value].title),
  description: computed(() => GOVERNANCE_SEO[clubKey.value].description),
})
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/about/')">{{ identity.aboutLabelZh }}</a></li>
      <li aria-current="page">治理與管理</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <img class="page-hero__bg" src="/assets/img/nav-about.jpg" alt="" width="1600" height="900">
  <div class="container">
    <p class="page-hero__eyebrow">{{ aboutEyebrow('2.5', clubKey) }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<section class="band gov-band" aria-labelledby="gov-docs-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">DOWNLOADS</p>
        <h2 class="section-title" id="gov-docs-title">公開文件</h2>
      </div>
    </div>

    <p class="section-lede" style="margin-top:1rem;">目前尚無可供下載的公開文件。</p>
  </div>
</section>
</template>

<style>
/* 【2.5 Governance】組織架構圖佔位、治理原則、可下載文件清單 */
.gov-band{ background:var(--paper); padding-block:clamp(3rem,5vw,4.5rem); }

.doc-list{ display:flex; flex-direction:column; gap:1rem; max-width:640px; }
.doc-list__item{
  display:flex; gap:1.1rem; align-items:flex-start;
  background:var(--paper-2); border:1px solid var(--rule); padding:1.25rem 1.4rem;
}
.doc-list__icon{
  flex:none; width:44px; height:52px; display:flex; align-items:center; justify-content:center;
  background:var(--ink); color:#fff; font-size:.66rem; font-weight:900; letter-spacing:.04em;
}
.doc-list__title{ font-weight:800; color:var(--heading); }
.doc-list__meta{ font-size:.82rem; color:var(--muted); }
</style>

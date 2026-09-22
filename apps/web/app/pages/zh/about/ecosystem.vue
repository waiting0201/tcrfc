<script setup lang="ts">
// app/pages/zh/about/ecosystem.vue — 由 site/src/pages/zh/about/ecosystem/index.html 轉來（S0-9 靜態頁搬遷）
//
// 文案依俱樂部切換：節點清單改用 ECOSYSTEM_NODES（藍鯨移除自我指涉的女子足球
// 節點，04 依 docs/13-blue-whale-site.md §3 改為青年隊——這是規劃書已定的單元
// 取捨，不是本頁自行決定的版型差異）。
definePageMeta({ nav: "about", unit: "02" })

const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const identity = computed(() => getClubIdentity(clubKey.value))
const assets = computed(() => getClubAssets(clubKey.value))
const hero = computed(() => ECOSYSTEM_HERO[clubKey.value])
const nodes = computed(() => ECOSYSTEM_NODES[clubKey.value])

useSeoMeta({
  title: computed(() => ECOSYSTEM_SEO[clubKey.value].title),
  description: computed(() => ECOSYSTEM_SEO[clubKey.value].description),
})
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a href="/zh/">首頁</a></li>
      <li><a href="/zh/about/">{{ identity.aboutLabelZh }}</a></li>
      <li aria-current="page">生態系</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <img class="page-hero__bg" src="/assets/img/nav-about.jpg" alt="" width="1600" height="900">
  <div class="container">
    <p class="page-hero__eyebrow">{{ aboutEyebrow('2.6', clubKey) }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<section class="band eco-band" aria-labelledby="eco-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="eco-title">體系圖解</h2>

    <div class="eco-diagram">
      <div class="eco-hub">
        <img :src="assets.headerMark.src" alt="" width="40" height="42" loading="lazy">
        <span v-if="identity.brandTagEn">{{ identity.brandTagEn }}</span>
      </div>

      <a v-for="node in nodes" :key="node.num" class="eco-node" :class="`eco-node--${node.num}`" :href="node.href">
        <span class="eco-node__num">{{ node.num }}</span>
        <span class="eco-node__en">{{ node.enLabel }}</span>
        <span class="eco-node__zh">{{ node.zhLabel }}</span>
        <span class="eco-node__desc">{{ node.descZh }}</span>
      </a>
    </div>

  </div>
</section>
</template>

<style>
/* 【2.6 Ecosystem】互動式生態系圖解 —— hub-and-spoke 版型，桌機置中放射、行動版收合為清單 */
.eco-band{ background:var(--paper); padding-block:clamp(3.5rem,6vw,6rem); }

.eco-diagram{
  display:grid; gap:1.25rem;
  grid-template-columns:repeat(auto-fit,minmax(230px,1fr));
  position:relative;
}
.eco-hub{
  grid-column:1/-1; justify-self:center;
  display:flex; flex-direction:column; align-items:center; gap:.5rem;
  width:120px; height:120px; border-radius:50%;
  background:var(--ink); color:#fff;
  margin-bottom:.5rem;
  justify-content:center;
}
.eco-hub span{ font-size:.72rem; font-weight:900; letter-spacing:.08em; }

.eco-node{
  display:flex; flex-direction:column; gap:.35rem;
  background:var(--paper-2); border:1px solid var(--rule); border-top:4px solid var(--brand-aa);
  padding:1.5rem 1.4rem; transition:transform var(--dur-fast) var(--ease), border-color var(--dur-fast) var(--ease);
}
.eco-node:hover, .eco-node:focus-visible{ transform:translateY(-4px); border-top-color:var(--brand-deep); }
.eco-node__num{ font-size:.72rem; font-weight:800; color:var(--muted); }
.eco-node__en{ font-size:.7rem; font-weight:800; letter-spacing:.08em; text-transform:uppercase; color:var(--brand-aa); }
.eco-node__zh{ font-size:1.2rem; font-weight:900; color:var(--heading); }
.eco-node__desc{ font-size:.84rem; color:var(--muted); line-height:1.6; margin-top:.15rem; }
.eco-node__desc .badge{ margin-top:.5rem; }

@media (min-width:860px){
  .eco-diagram{ grid-template-columns:repeat(4,1fr); }
}
</style>

<script setup lang="ts">
// app/pages/zh/charity/commitment.vue — 由 site/src/pages/zh/charity/commitment/index.html 轉來（S0-9 靜態頁搬遷）
definePageMeta({ nav: "charity", unit: "11", enReady: true })

const { lp, isEn, tx, locale } = useLocale()

// 本頁僅磐石有內容（藍鯨規劃書不設「11 慈善與社會影響」），不寫死站別，仍讀 config.public.club 的設定由路由層處理。
// 稽核 B-1：11.1 慈善理念讀 B1 頁面管理的 `charity/commitment`（規劃書 §3.11 11.1「區塊編輯器排版」），
// 模式抽成共用的 `useCmsPage`：有已發布且含可渲染區塊的頁面就用後台內容，否則（含 404、API 打不到）
// 以客戶 11.1 原文備援（shared/utils/charity-commitment.ts，同時是種子來源，不再於此內嵌文案）。
// 後台頁面沒有標題欄位，h1 維持固定標題；區塊只渲染純文字型，不 v-html（見 shared/utils/page-blocks.ts）。
const cms = await useCmsPage('charity/commitment')

const fallbackBlocks = computed(() => CHARITY_COMMITMENT_BLOCKS.flatMap((b, i) =>
  normalizePageBlocks([{ blockType: b.type, content: b.content, sortOrder: i }], { locale: locale.value })))

cms.applySeo({
  title: computed(() => (isEn.value ? 'Our Commitment and Focus Areas | Charity & Impact | Taichung Rock FC' : "慈善理念與投入領域 Our Commitment｜慈善與社會影響｜台中磐石足球俱樂部")),
  description: computed(() => CHARITY_COMMITMENT_SEO_DESCRIPTION[isEn.value ? 'en' : 'zh']),
})
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/charity/')">{{ tx('慈善與社會影響', 'Charity & Impact') }}</a></li>
      <li aria-current="page">{{ tx('慈善理念', 'Our Commitment') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <img class="page-hero__bg" :src="siteImg('/assets/img/trencin-03.jpg')" alt="" width="1920" height="1280">
  <div class="container">
    <p class="page-hero__eyebrow">11.1 Our Commitment</p>
    <h1><template v-if="isEn">Our Commitment</template><template v-else>{{ tx('慈善理念', 'Our Commitment') }}<span class="en">Our Commitment</span></template></h1>
    <p class="page-hero__lede"><template v-if="isEn">Football is more than competition; it is also a way to connect with the community. Guided by our core value of Local pride, we take action in four areas of public benefit.</template><template v-else>足球不只是競技，也是連結社區的方式。我們以「Local pride 在地驕傲」核心價值出發，投入四大領域的公益行動。</template></p>
  </div>
</section>

<ContentCmsPageBand :blocks="cms.active.value ? cms.blocks.value : fallbackBlocks" :label="tx('慈善理念說明', 'About our charitable philosophy')" :zh-fallback="cms.active.value && cms.hasZhFallback.value" />

<section class="band grain cta-band" aria-labelledby="commitment-cta-title">
  <span class="ghost-num" aria-hidden="true" style="left:var(--edge);bottom:-1.5rem;color:rgba(255,255,255,.06);">11.1</span>
  <div class="band-inner container">
    <h2 id="commitment-cta-title" class="section-title">{{ tx('看看理念如何落實', 'See how our commitment is put into practice') }}</h2>
    <div class="cta-grid">
      <a class="cta-card" :href="lp('/zh/charity/programs/')">
        <span class="cta-card__num">11.2</span>
        <span class="cta-card__title">{{ tx('慈善計畫', 'Charity Programs') }}</span>
        <p class="cta-card__desc">{{ tx('正在進行與已完成的公益計畫', 'Ongoing and completed charity programs') }}</p>
      </a>
      <a class="cta-card" :href="lp('/zh/charity/impact-stories/')">
        <span class="cta-card__num">11.3</span>
        <span class="cta-card__title">{{ tx('慈善事蹟', 'Impact Stories') }}</span>
        <p class="cta-card__desc">{{ tx('已落地的公益行動紀錄', 'Records of the charity actions we have carried out') }}</p>
      </a>
      <a class="cta-card" :href="lp('/zh/charity/#donate')">
        <span class="cta-card__num">01</span>
        <span class="cta-card__title">{{ tx('支持我們', 'Support us') }}</span>
        <p class="cta-card__desc">{{ tx('企業合作與球迷捐款兩種參與方式', 'Two ways to take part: corporate partnerships and fan donations') }}</p>
      </a>
    </div>
  </div>
</section>
</template>

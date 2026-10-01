<script setup lang="ts">
// app/pages/zh/charity/commitment.vue — 由 site/src/pages/zh/charity/commitment/index.html 轉來（S0-9 靜態頁搬遷）
import type { PageBlockNode, RawPageBlock } from '#shared/utils/page-blocks'

definePageMeta({ nav: "charity", unit: "11" })

const { lp } = useLocale()

// S1-12d 收尾：成立年份改讀 useSiteFacts('tcrfc')（後端公開端點）。本頁 unit '11'
// 對藍鯨已整頁 404（藍鯨規劃書不設「11 慈善與社會影響」），固定讀 tcrfc 即可。
const { facts: tcrfcFacts } = useSiteFacts('tcrfc')

// S3-5（順手）：11.1 慈善理念改讀 B1 頁面管理的 `charity/commitment`（`GET /api/v1/tcrfc/pages/charity/commitment`，
// 規劃書 §3.11 11.1「區塊編輯器排版」）。**後端有已發布且含可渲染區塊的頁面就用後台內容，否則（含 404、API 打不到、
// 區塊全是本輪不渲染的型別）維持下方既有的靜態內容**——種子目前沒有這一頁。後台頁面沒有標題欄位，h1 維持固定標題。
// 區塊只渲染純文字型（見 shared/utils/page-blocks.ts），不 v-html。
const { locale } = useLocale()
interface RawCommitmentPage {
  seoTitle: string | null
  seoDescription: string | null
  blocks: RawPageBlock[]
}
// `transform`：在進入頁面 payload 之前就把區塊正規化成安全的純文字節點（原始 JSON 不進 payload）
const { data: cmsPage } = await useFetch<{ seoTitle: string | null, seoDescription: string | null, blocks: PageBlockNode[] } | null>('/api/backend/tcrfc/pages/charity/commitment', {
  query: { lang: locale.value },
  key: `charity-commitment-${locale.value}`,
  default: () => null,
  transform: (p: RawCommitmentPage) => ({ seoTitle: p.seoTitle, seoDescription: p.seoDescription, blocks: normalizePageBlocks(p.blocks) }),
})
const cmsBlocks = computed(() => cmsPage.value?.blocks ?? [])
const useCms = computed(() => cmsBlocks.value.length > 0)

useSeoMeta({
  title: computed(() => (useCms.value && cmsPage.value?.seoTitle) || "慈善理念與投入領域 Our Commitment｜慈善與社會影響｜台中磐石足球俱樂部"),
  description: computed(() => (useCms.value && cmsPage.value?.seoDescription) || "台中磐石足球俱樂部的慈善理念與四大投入領域：青少年扶助、偏鄉足球、弱勢家庭與公益義賽，實踐 Community 社區共好核心價值。"),
})
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/charity/')">慈善與社會影響</a></li>
      <li aria-current="page">慈善理念</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <img class="page-hero__bg" src="/assets/img/trencin-03.jpg" alt="" width="1920" height="1280">
  <div class="container">
    <p class="page-hero__eyebrow">11.1 Our Commitment</p>
    <h1>慈善理念<span class="en">Our Commitment</span></h1>
    <p class="page-hero__lede">足球不只是競技，也是連結社區的方式。我們以「Community 社區共好」核心價值出發，投入四大領域的公益行動。</p>
  </div>
</section>

<section v-if="useCms" class="band commitment-band" aria-labelledby="commitment-title">
  <div class="band-inner container">
    <h2 id="commitment-title" class="visually-hidden">慈善理念說明</h2>
    <ContentPageBlocks :blocks="cmsBlocks" />
  </div>
</section>

<section v-else class="band commitment-band" aria-labelledby="commitment-title">
  <div class="band-inner container">
    <div class="prose">
      <h2 id="commitment-title" class="visually-hidden">慈善理念說明</h2>
      <!-- GEO-03（S1-12d）：成立年份為單一來源 site-facts.ts，不在此重複寫死字面值（本頁僅磐石有內容，慈善單元藍鯨不設）。 -->
      <p>台中磐石足球俱樂部自 {{ tcrfcFacts.foundedYear }} 年成立以來，將「<strong>Community 社區共好</strong>」列為五大核心價值之一，相信足球能為社區帶來的影響不只在球場上。我們相信優質的足球資源不該只集中在少數人身上，因此持續尋找機會，把訓練、場地與人才帶到需要的地方。</p>

    </div>

    <h2 class="section-title" style="margin-top:3rem;">投入領域</h2>
    <p class="section-lede" style="margin-bottom:1.5rem;">以下四大領域為規劃書明列的公益投入方向，各領域詳細計畫請見 <a :href="lp('/zh/charity/programs/')" style="color:var(--brand-aa);">11.2 慈善計畫</a>。</p>

    <div class="values-grid">
      <div class="value-card">
        <p class="value-card__num">01</p>
        <p class="value-card__en">Youth Support</p>
        <p class="value-card__zh">青少年扶助</p>
        <p class="value-card__desc">支持經濟弱勢或資源不足地區的青少年參與足球運動，降低參與門檻。</p>
      </div>
      <div class="value-card">
        <p class="value-card__num">02</p>
        <p class="value-card__en">Rural Football</p>
        <p class="value-card__zh">偏鄉足球</p>
        <p class="value-card__desc">將教練資源與訓練機會帶到偏遠地區的學校與社區，拉近城鄉足球資源落差。</p>
      </div>
      <div class="value-card">
        <p class="value-card__num">03</p>
        <p class="value-card__en">Family Support</p>
        <p class="value-card__zh">弱勢家庭</p>
        <p class="value-card__desc">與在地社福單位合作，提供弱勢家庭孩子參與足球活動的機會與必要物資。</p>
      </div>
      <div class="value-card">
        <p class="value-card__num">04</p>
        <p class="value-card__en">Charity Matches</p>
        <p class="value-card__zh">公益義賽</p>
        <p class="value-card__desc">舉辦或參與公益義賽，將活動收益或關注度轉化為對特定公益團體的實質支持。</p>
      </div>
    </div>
  </div>
</section>

<section class="band grain cta-band" aria-labelledby="commitment-cta-title">
  <span class="ghost-num" aria-hidden="true" style="left:var(--edge);bottom:-1.5rem;color:rgba(255,255,255,.06);">11.1</span>
  <div class="band-inner container">
    <h2 id="commitment-cta-title" class="section-title">看看理念如何落實</h2>
    <div class="cta-grid">
      <a class="cta-card" :href="lp('/zh/charity/programs/')">
        <span class="cta-card__num">11.2</span>
        <span class="cta-card__title">慈善計畫</span>
        <p class="cta-card__desc">正在進行與已完成的公益計畫</p>
      </a>
      <a class="cta-card" :href="lp('/zh/charity/impact-stories/')">
        <span class="cta-card__num">11.3</span>
        <span class="cta-card__title">慈善事蹟</span>
        <p class="cta-card__desc">已落地的公益行動紀錄</p>
      </a>
      <a class="cta-card" :href="lp('/zh/charity/#donate')">
        <span class="cta-card__num">01</span>
        <span class="cta-card__title">支持我們</span>
        <p class="cta-card__desc">企業合作與球迷捐款兩種參與方式</p>
      </a>
    </div>
  </div>
</section>
</template>

<style>
.commitment-band{ padding-block:clamp(3.5rem,6vw,6rem); }

/* .values-grid 共用元件是為首頁「五大核心價值」寫死 repeat(5,...)，
   這裡的四大投入領域只有 4 項，沿用會多一格空白，故覆寫為 4 欄。 */
.commitment-band .values-grid{ grid-template-columns:repeat(4,minmax(0,1fr)); }
@media (max-width:900px){ .commitment-band .values-grid{ grid-template-columns:repeat(2,minmax(0,1fr)); } }
@media (max-width:520px){ .commitment-band .values-grid{ grid-template-columns:1fr; } }
</style>

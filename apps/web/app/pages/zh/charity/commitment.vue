<script setup lang="ts">
// app/pages/zh/charity/commitment.vue — 由 site/src/pages/zh/charity/commitment/index.html 轉來（S0-9 靜態頁搬遷）
definePageMeta({ nav: "charity", unit: "11", enReady: true })

const { lp, isEn, tx } = useLocale()

// S1-12d 收尾：成立年份改讀 useSiteFacts（後端公開端點）。本頁 unit '11'
// 對藍鯨已整頁 404（藍鯨規劃書不設「11 慈善與社會影響」），實務上只有磐石走到；C-1 起不寫死站別，一律讀 config.public.club。
const club = useRuntimeConfig().public.club
const { facts: tcrfcFacts } = useSiteFacts(club)

// 稽核 B-1：11.1 慈善理念讀 B1 頁面管理的 `charity/commitment`（規劃書 §3.11 11.1「區塊編輯器排版」），
// 模式抽成共用的 `useCmsPage`：有已發布且含可渲染區塊的頁面就用後台內容，否則（含 404、API 打不到）維持下方既有靜態內容。
// 後台頁面沒有標題欄位，h1 維持固定標題；區塊只渲染純文字型，不 v-html（見 shared/utils/page-blocks.ts）。
const cms = await useCmsPage('charity/commitment')

cms.applySeo({
  title: computed(() => (isEn.value ? 'Our Commitment and Focus Areas | Charity & Impact | Taichung Rock FC' : "慈善理念與投入領域 Our Commitment｜慈善與社會影響｜台中磐石足球俱樂部")),
  description: computed(() => (isEn.value ? 'The charitable philosophy and four focus areas of Taichung Rock FC: youth support, rural football, disadvantaged families and charity matches, putting our core value of Community into practice.' : "台中磐石足球俱樂部的慈善理念與四大投入領域：青少年扶助、偏鄉足球、弱勢家庭與公益義賽，實踐 Community 社區共好核心價值。")),
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
    <p class="page-hero__lede"><template v-if="isEn">Football is more than competition; it is also a way to connect with the community. Guided by our core value of Community, we take action in four areas of public benefit.</template><template v-else>足球不只是競技，也是連結社區的方式。我們以「Community 社區共好」核心價值出發，投入四大領域的公益行動。</template></p>
  </div>
</section>

<ContentCmsPageBand v-if="cms.active.value" :blocks="cms.blocks.value" :label="tx('慈善理念說明', 'About our charitable philosophy')" :zh-fallback="cms.hasZhFallback.value" />

<section v-else class="band commitment-band" aria-labelledby="commitment-title">
  <div class="band-inner container">
    <div class="prose">
      <h2 id="commitment-title" class="visually-hidden">{{ tx('慈善理念說明', 'About our charitable philosophy') }}</h2>
      <!-- GEO-03（S1-12d）：成立年份為單一來源 site-facts.ts，不在此重複寫死字面值（本頁僅磐石有內容，慈善單元藍鯨不設）。 -->
      <p><template v-if="isEn">Since Taichung Rock FC was founded in {{ tcrfcFacts.foundedYear }}, <strong>Community</strong> has been one of our five core values. We believe the impact football can have on a community goes well beyond the pitch. We also believe that good football resources should not be concentrated in the hands of a few, so we keep looking for opportunities to bring training, facilities and talent to the places that need them.</template><template v-else>台中磐石足球俱樂部自 {{ tcrfcFacts.foundedYear }} 年成立以來，將「<strong>Community 社區共好</strong>」列為五大核心價值之一，相信足球能為社區帶來的影響不只在球場上。我們相信優質的足球資源不該只集中在少數人身上，因此持續尋找機會，把訓練、場地與人才帶到需要的地方。</template></p>

    </div>

    <h2 class="section-title" style="margin-top:3rem;">{{ tx('投入領域', 'Focus areas') }}</h2>
    <p class="section-lede" style="margin-bottom:1.5rem;"><template v-if="isEn">The four areas below are our public-interest priorities. For the programs in each area, see <a :href="lp('/zh/charity/programs/')" style="color:var(--brand-aa);">11.2 Charity Programs</a>.</template><template v-else>以下四大領域為規劃書明列的公益投入方向，各領域詳細計畫請見 <a :href="lp('/zh/charity/programs/')" style="color:var(--brand-aa);">11.2 {{ tx('慈善計畫', 'Charity Programs') }}</a>。</template></p>

    <div class="values-grid">
      <div class="value-card">
        <p class="value-card__num">01</p>
        <p v-if="!isEn" class="value-card__en">Youth Support</p>
        <p class="value-card__zh">{{ tx('青少年扶助', 'Youth Support') }}</p>
        <p class="value-card__desc">{{ tx('支持經濟弱勢或資源不足地區的青少年參與足球運動，降低參與門檻。', 'Supporting young people from economically disadvantaged or under-resourced areas to take part in football, and lowering the barriers to participation.') }}</p>
      </div>
      <div class="value-card">
        <p class="value-card__num">02</p>
        <p v-if="!isEn" class="value-card__en">Rural Football</p>
        <p class="value-card__zh">{{ tx('偏鄉足球', 'Rural Football') }}</p>
        <p class="value-card__desc">{{ tx('將教練資源與訓練機會帶到偏遠地區的學校與社區，拉近城鄉足球資源落差。', 'Bringing coaching resources and training opportunities to schools and communities in remote areas, narrowing the gap in football resources between cities and the countryside.') }}</p>
      </div>
      <div class="value-card">
        <p class="value-card__num">03</p>
        <p v-if="!isEn" class="value-card__en">Family Support</p>
        <p class="value-card__zh">{{ tx('弱勢家庭', 'Family Support') }}</p>
        <p class="value-card__desc">{{ tx('與在地社福單位合作，提供弱勢家庭孩子參與足球活動的機會與必要物資。', 'Working with local social welfare organisations to give children from disadvantaged families the chance to take part in football activities, along with the necessary supplies.') }}</p>
      </div>
      <div class="value-card">
        <p class="value-card__num">04</p>
        <p v-if="!isEn" class="value-card__en">Charity Matches</p>
        <p class="value-card__zh">{{ tx('公益義賽', 'Charity Matches') }}</p>
        <p class="value-card__desc">{{ tx('舉辦或參與公益義賽，將活動收益或關注度轉化為對特定公益團體的實質支持。', 'Organising or taking part in charity matches, turning event proceeds or attention into real support for specific charities.') }}</p>
      </div>
    </div>
  </div>
</section>

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

<style>
.commitment-band{ padding-block:clamp(3.5rem,6vw,6rem); }

/* .values-grid 共用元件是為首頁「五大核心價值」寫死 repeat(5,...)，
   這裡的四大投入領域只有 4 項，沿用會多一格空白，故覆寫為 4 欄。 */
.commitment-band .values-grid{ grid-template-columns:repeat(4,minmax(0,1fr)); }
@media (max-width:900px){ .commitment-band .values-grid{ grid-template-columns:repeat(2,minmax(0,1fr)); } }
@media (max-width:520px){ .commitment-band .values-grid{ grid-template-columns:1fr; } }
</style>

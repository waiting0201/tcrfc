<script setup lang="ts">
// app/pages/zh/partners/index.vue — 由 site/src/pages/zh/partners/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
definePageMeta({ nav: 'partners', unit: '09', enReady: true })

const { lp, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const isTcrfc = computed(() => config.public.club !== 'bw')
// BW-C1（品牌外洩全站盤點）：改讀既有的 getClubAssets()。
const clubAssets = computed(() => getClubAssets(config.public.club))
// 藍鯨規劃書 v1.9 §2.1：藍鯨不設 8.1 漫畫，贊助方案少一項（見 partners/opportunities）。
// S2-7：後台已發布任何贊助方案時，方案數改讀實際張數（與 9.4 同一份資料、同一個換算）；沒有才用規劃書固定的九／八。
const { packages } = await useSponsorPackages()
const CN_NUM = ['', '一', '兩', '三', '四', '五', '六', '七', '八', '九', '十']
const CN_NUM_EN = ['', 'one', 'two', 'three', 'four', 'five', 'six', 'seven', 'eight', 'nine', 'ten']
const planCountEn = computed(() => {
  const n = packages.value.length
  if (n === 0) return isUnitEnabledForClub('8.1', config.public.club) ? 'nine' : 'eight'
  return n <= 10 ? (CN_NUM_EN[n] ?? String(n)) : String(n)
})
const planCountZh = computed(() => {
  const n = packages.value.length
  if (n === 0) return isUnitEnabledForClub('8.1', config.public.club) ? '九' : '八'
  return n <= 10 ? (CN_NUM[n] ?? String(n)) : String(n)
})

useSeoMeta({
  title: computed(() => (isEn.value ? 'Partners & Sponsors | Taichung Rock FC' : `合作夥伴與贊助 Partners & Sponsors｜${clubAssets.value.nameZh}`)),
  description: computed(() => (isEn.value ? `The partners and sponsors of Taichung Rock FC, the value of becoming a partner, and an overview of our ${planCountEn.value} sponsorship packages.` : `${clubAssets.value.nameZh}的合作夥伴、贊助商、成為合作夥伴的價值主張，以及${planCountZh.value}種贊助方案總覽。`)),
})
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li aria-current="page">{{ tx('夥伴', 'Partners') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <ClubHeroBg :src="siteImg('/assets/img/nav-partners.jpg')" width="1600" height="900" />
  <div class="container">
    <p class="page-hero__eyebrow">09 Partners & Sponsors</p>
    <h1><template v-if="isEn">Partners &amp; Sponsors</template><template v-else>合作夥伴與贊助<span class="en">Partners &amp; Sponsors</span></template></h1>
    <p v-if="isTcrfc" class="page-hero__lede"><template v-if="isEn">Join forces with Taichung Rock FC to reach the local community and the international football network through a professional football platform, creating value for your brand and the community alike.</template><template v-else>攜手台中磐石，透過職業足球平台觸及在地社群與國際足球網絡，共創品牌與社區的雙贏價值。</template></p>
    <p v-else class="page-hero__lede">攜手台中藍鯨，透過女子足球平台觸及在地社群與國際足球網絡，共創品牌與社區的雙贏價值。</p>
  </div>
</section>

<!-- SPEC 3.9 — 單元 landing：導覽卡連往 9.1–9.4 -->
<section class="band" aria-labelledby="partners-hub-title">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">EXPLORE</p>
        <h2 class="section-title" id="partners-hub-title">{{ tx('合作夥伴與贊助四大單元', 'The four sections of Partners & Sponsors') }}</h2>
      </div>
    </div>

    <div class="unit-grid">
      <a class="unit-card" :href="lp('/zh/partners/our-partners/')">
        <div class="unit-card__scrim" aria-hidden="true"></div>
        <div class="unit-card__body">
          <p class="unit-card__num">9.1</p>
          <p class="unit-card__title"><template v-if="isEn">Our Partners</template><template v-else>合作夥伴<span class="en">Our Partners</span></template></p>
          <p class="unit-card__desc">{{ tx('依策略、國際、訓練、教育、品牌五大類型分區的夥伴牆。', 'A partner wall organised into five types: strategic, international, training, education and brand.') }}</p>
          <span class="unit-card__link">{{ tx('查看夥伴', 'View partners') }} <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg></span>
        </div>
      </a>
      <a class="unit-card" :href="lp('/zh/partners/our-sponsors/')">
        <div class="unit-card__scrim" aria-hidden="true"></div>
        <div class="unit-card__body">
          <p class="unit-card__num">9.2</p>
          <p class="unit-card__title"><template v-if="isEn">Our Sponsors</template><template v-else>贊助商<span class="en">Our Sponsors</span></template></p>
          <p class="unit-card__desc">{{ tx('依主贊助、官方、支持等級分區，贊助故事與活動紀錄。', 'Sponsors organised into Title, Official and Supporting tiers, with sponsor stories and activation records.') }}</p>
          <span class="unit-card__link">{{ tx('查看贊助商', 'View sponsors') }} <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg></span>
        </div>
      </a>
      <a class="unit-card" :href="lp('/zh/partners/become-a-partner/')">
        <div class="unit-card__scrim" aria-hidden="true"></div>
        <div class="unit-card__body">
          <p class="unit-card__num">9.3</p>
          <p class="unit-card__title"><template v-if="isEn">Become a Partner</template><template v-else>成為合作夥伴<span class="en">Become a Partner</span></template></p>
          <p class="unit-card__desc"><template v-if="isEn">Six value propositions and audience data: the reasons to partner with Taichung Rock FC.</template><template v-else>六大價值論述與受眾數據，了解與{{ clubAssets.shortNameZh }}合作的理由。</template></p>
          <span class="unit-card__link">{{ tx('了解價值', 'Learn more') }} <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg></span>
        </div>
      </a>
      <a class="unit-card" :href="lp('/zh/partners/opportunities/')">
        <div class="unit-card__scrim" aria-hidden="true"></div>
        <div class="unit-card__body">
          <p class="unit-card__num">9.4</p>
          <p class="unit-card__title"><template v-if="isEn">Sponsorship Opportunities</template><template v-else>贊助方案<span class="en">Sponsorship Opportunities</span></template></p>
          <p class="unit-card__desc"><template v-if="isEn">{{ planCountEn }} sponsorship packages to help you find the partnership scale that suits you best.</template><template v-else>{{ planCountZh }}種贊助方案卡片，找到最適合的合作規模。</template></p>
          <span class="unit-card__link">{{ tx('查看方案', 'View packages') }} <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg></span>
        </div>
      </a>
    </div>
  </div>
</section>

<!-- SPEC 3.9 — 頁尾 CTA：提案簡介下載／聯絡我們 -->
<section class="band grain cta-band" aria-labelledby="partners-cta-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="partners-cta-title">{{ tx('洽談合作或下載提案簡介', 'Discuss a partnership or download the sponsorship deck') }}</h2>
    <div class="cta-grid" style="grid-template-columns:repeat(2,minmax(0,1fr))">
      <div class="cta-card">
        <p class="cta-card__num">DOWNLOAD</p>
        <p class="cta-card__title">{{ tx('下載提案簡介', 'Download the sponsorship deck') }}</p>
        <p class="cta-card__desc">{{ tx('留下公司與聯絡資訊，取得完整贊助提案簡介 PDF。', 'Leave your company and contact details to get the full sponsorship deck (PDF).') }}</p>
        <a class="btn btn--primary" :href="lp('/zh/partners/opportunities/#deck-download')">{{ tx('前往下載', 'Go to download') }}</a>
      </div>
      <div class="cta-card">
        <p class="cta-card__num">CONTACT</p>
        <p class="cta-card__title">{{ tx('聯絡我們', 'Contact us') }}</p>
        <p class="cta-card__desc">{{ tx('想進一步討論合作或贊助方案？商務部將盡快與您聯繫。', 'Want to discuss a partnership or sponsorship package in more detail? Our Partnerships Department will get back to you shortly.') }}</p>
        <a class="btn btn--primary" :href="lp('/zh/join/partnership/')">{{ tx('洽談合作', 'Discuss a partnership') }}</a>
      </div>
    </div>
  </div>
</section>
</template>

<style>
/* 單元 landing 導覽卡（unit-card）：與 culture/index.html 相同寫法，建議收進共用 CSS */
.unit-grid{
  display:grid; grid-template-columns:repeat(auto-fit,minmax(280px,1fr)); gap:1.5rem; margin-top:2.5rem;
}
.unit-card{
  position:relative; display:flex; flex-direction:column; justify-content:flex-end;
  min-height:300px; background:var(--ink-2); color:#fff; overflow:hidden; text-decoration:none;
  clip-path:polygon(0 0,100% 0,100% calc(100% - 28px),calc(100% - 28px) 100%,0 100%);
  transition:transform var(--dur) var(--ease);
}
.unit-card:hover{ transform:translateY(-4px); }
.unit-card::after{
  content:""; position:absolute; right:0; bottom:0; width:34px; height:34px;
  background:var(--ink-trim-dk); clip-path:polygon(100% 0,100% 100%,0 100%); z-index:2;
}
.unit-card img{ position:absolute; inset:0; width:100%; height:100%; object-fit:cover; opacity:.6; transition:transform .6s var(--ease); }
.unit-card:hover img{ transform:scale(1.08); }
.unit-card__scrim{ position:absolute; inset:0; background:linear-gradient(0deg,rgba(35,25,22,.96) 0%,rgba(35,25,22,.6) 55%,rgba(35,25,22,.2) 100%); }
.unit-card__body{ position:relative; z-index:1; padding:1.85rem 1.6rem; }
.unit-card__num{ font-size:.72rem; font-weight:800; letter-spacing:.08em; color:var(--brand); text-transform:uppercase; }
.unit-card__title{ font-size:1.4rem; font-weight:900; margin-top:.5rem; letter-spacing:-.01em; }
.unit-card__title .en{ display:block; font-size:.6em; font-weight:700; letter-spacing:.1em; text-transform:uppercase; color:var(--muted-dark); margin-top:.25em; }
.unit-card__desc{ font-size:.85rem; color:var(--muted-dark); margin-top:.6rem; line-height:1.6; }
.unit-card__link{ display:inline-flex; align-items:center; gap:.4em; margin-top:1.1rem; font-weight:700; font-size:.85rem; color:#fff; }
.unit-card__link svg{ width:14px; height:14px; transition:transform var(--dur-fast) var(--ease); }
.unit-card:hover .unit-card__link svg{ transform:translateX(4px); }
</style>

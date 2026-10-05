<script setup lang="ts">
// app/pages/zh/partners/become-a-partner/index.vue — 由 site/src/pages/zh/partners/become-a-partner/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
definePageMeta({ nav: 'partners', unit: '9.3', enReady: true, enReadyBw: true })

const { lp, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const isTcrfc = computed(() => config.public.club !== 'bw')
// BW-C1（品牌外洩全站盤點）：改讀既有的 getClubAssets()。六張價值卡有幾張含磐石
// 專屬事實（企甲聯賽定位、學院、慈善活動——11 慈善單元藍鯨不設，見 docs/13 §3），
// 不是單純換名稱就能通用，逐張改為 isTcrfc 分支，不臆造藍鯨對應敘述。
const clubAssets = computed(() => getClubAssets(config.public.club))
const clubEn = computed(() => bizClubNameEn(config.public.club))

useSeoMeta({
  title: computed(() => (isEn.value ? `Become a Partner | Partners & Sponsors | ${clubEn.value}` : `成為合作夥伴 Become a Partner｜合作夥伴與贊助｜${clubAssets.value.nameZh}`)),
  description: computed(() => (isEn.value ? `Learn about the six values of partnering with ${clubEn.value}: brand exposure, audience reach, social impact and an international development network.` : `了解與${clubAssets.value.nameZh}合作的六大價值：品牌曝光、受眾觸及、社會影響力與國際發展網絡。`)),
})
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/partners/')">{{ tx('夥伴', 'Partners') }}</a></li>
      <li aria-current="page">{{ tx('成為合作夥伴', 'Become a Partner') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">9.3 Become a Partner</p>
    <h1><template v-if="isEn">Become a Partner</template><template v-else>{{ tx('成為合作夥伴', 'Become a Partner') }}<span class="en">Become a Partner</span></template></h1>
    <p v-if="isTcrfc" class="page-hero__lede"><template v-if="isEn">Through Taichung Rock FC's professional football platform, reach the local community, youth-development families and the international football network, and let your brand grow together with the club.</template><template v-else>透過台中磐石的職業足球平台，觸及在地社群、青訓家庭與國際足球網絡，讓品牌與台中磐石一起成長。</template></p>
    <p v-else class="page-hero__lede"><template v-if="isEn">{{ PARTNERS_BECOME_HERO_EN_BW }}</template><template v-else>透過台中藍鯨的女子足球平台，觸及在地社群、青訓家庭與國際足球網絡，讓品牌與台中藍鯨一起成長。</template></p>
  </div>
</section>

<!-- SPEC 3.9 §9.3 — 六大價值論述 -->
<section class="band" aria-labelledby="values-title">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">WHY PARTNER WITH US</p>
        <h2 class="section-title" id="values-title">{{ tx('六大合作價值', 'Six Partnership Values') }}</h2>
      </div>
    </div>

    <div class="grid grid--3 value-prop-grid">
      <div class="value-prop-card">
        <p class="value-prop-card__num">01</p>
        <p class="value-prop-card__title"><template v-if="isEn">Why Partner</template><template v-else>為何合作<span class="en">Why Partner</span></template></p>
        <p v-if="isTcrfc" class="value-prop-card__desc"><template v-if="isEn">Taichung Rock FC is the only club in Taichung competing in the top-tier corporate league, giving brands a direct route into sport and the local community.</template><template v-else>台中磐石是台中在地唯一的企業甲級足球俱樂部，是品牌走進運動與社區場域的直接管道。</template></p>
        <p v-else class="value-prop-card__desc"><template v-if="isEn">{{ PARTNERS_BECOME_VALUES_EN_BW.why }}</template><template v-else>台中藍鯨是台灣木蘭聯賽的球隊之一，是品牌走進女子足球與社區場域的直接管道。</template></p>
      </div>
      <div class="value-prop-card">
        <p class="value-prop-card__num">02</p>
        <p class="value-prop-card__title"><template v-if="isEn">Audience Analysis</template><template v-else>受眾分析<span class="en">Audience Analysis</span></template></p>
        <p v-if="isTcrfc" class="value-prop-card__desc"><template v-if="isEn">Covers First Team fans, Academy student families and matchday crowds. Detailed figures are below.</template><template v-else>涵蓋一線隊球迷、學院學員家庭與賽事現場觀眾，詳細數據見下方。</template></p>
        <p v-else class="value-prop-card__desc"><template v-if="isEn">{{ PARTNERS_BECOME_VALUES_EN_BW.audience }}</template><template v-else>涵蓋一線隊球迷、青年隊學員家庭與賽事現場觀眾，詳細數據見下方。</template></p>
      </div>
      <div class="value-prop-card">
        <p class="value-prop-card__num">03</p>
        <p class="value-prop-card__title"><template v-if="isEn">Brand Exposure</template><template v-else>品牌曝光<span class="en">Brand Exposure</span></template></p>
        <p class="value-prop-card__desc">{{ tx('球衣、場邊看板、官網與社群通路曝光，讓品牌與職業足球賽事同框。', 'Exposure on the kit, pitchside boards, our website and social media channels puts your brand alongside professional football fixtures.') }}</p>
      </div>
      <div class="value-prop-card">
        <p class="value-prop-card__num">04</p>
        <p class="value-prop-card__title"><template v-if="isEn">Social Impact</template><template v-else>社會影響力<span class="en">Social Impact</span></template></p>
        <p v-if="isTcrfc" class="value-prop-card__desc"><template v-if="isEn">Community football days and charity activities let your brand take part in meaningful local social action.</template><template v-else>結合社區足球日與慈善活動，讓品牌參與有意義的在地社會實踐。</template></p>
        <p v-else class="value-prop-card__desc"><template v-if="isEn">{{ PARTNERS_BECOME_VALUES_EN_BW.social }}</template><template v-else>結合社區足球推廣活動，讓品牌參與有意義的在地社會實踐。</template></p>
      </div>
      <div class="value-prop-card">
        <p class="value-prop-card__num">05</p>
        <p class="value-prop-card__title"><template v-if="isEn">Community Reach</template><template v-else>中介影響力<span class="en">Community Reach</span></template></p>
        <p v-if="isTcrfc" class="value-prop-card__desc"><template v-if="isEn">Through the Academy and our programs, build long-term trust with families and schools and widen your brand's word-of-mouth reach.</template><template v-else>透過學院與課程與家庭、學校建立長期信任關係，擴大品牌口碑觸及。</template></p>
        <p v-else class="value-prop-card__desc"><template v-if="isEn">{{ PARTNERS_BECOME_VALUES_EN_BW.community }}</template><template v-else>透過青年隊與推廣活動與家庭、學校建立長期信任關係，擴大品牌口碑觸及。</template></p>
      </div>
      <div class="value-prop-card">
        <p class="value-prop-card__num">06</p>
        <p class="value-prop-card__title"><template v-if="isEn">International Impact</template><template v-else>國際影響力<span class="en">International Impact</span></template></p>
        <p v-if="isTcrfc" class="value-prop-card__desc"><template v-if="isEn">Through international exchange and overseas expansion programs, let your brand travel the world with Taichung Rock FC's players.</template><template v-else>藉由國際交流與海外拓展計畫，讓品牌隨台中磐石球員的腳步走向世界。</template></p>
        <p v-else class="value-prop-card__desc"><template v-if="isEn">{{ PARTNERS_BECOME_VALUES_EN_BW.international }}</template><template v-else>藉由國際交流與海外拓展計畫，讓品牌隨台中藍鯨球員的腳步走向世界。</template></p>
      </div>
    </div>
  </div>
</section>

<!-- SPEC 3.9 §9.3 — 受眾數據視覺化 -->
<section class="band grain grain--2" id="audience-data" aria-labelledby="audience-data-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker kicker--on-dark">AUDIENCE ANALYSIS</p>
        <h2 class="section-title" id="audience-data-title" style="color:#fff">{{ tx('受眾數據', 'Audience data') }}</h2>
      </div>
    </div>

    <p class="audience-note">{{ tx('完整受眾與社群數據報告可於洽談合作時提供，歡迎聯絡商務部索取最新資料。', 'The full audience and social media data report is available when we discuss a partnership. Please contact our Partnerships Department for the latest figures.') }}</p>
  </div>
</section>

<section class="band" aria-labelledby="become-cta-title">
  <div class="container">
    <h2 class="visually-hidden" id="become-cta-title">{{ tx('查看贊助方案並聯絡我們', 'View sponsorship packages and contact us') }}</h2>
    <div class="cta-grid" style="grid-template-columns:repeat(2,minmax(0,1fr))">
      <div class="cta-card" style="background:var(--ink)">
        <p class="cta-card__num">9.4</p>
        <p class="cta-card__title">{{ tx('查看贊助方案', 'View sponsorship packages') }}</p>
        <p class="cta-card__desc">{{ tx('從俱樂部到場館冠名，找到最適合的合作規模。', 'From club sponsorship to venue naming rights, find the partnership scale that suits you best.') }}</p>
        <a class="btn btn--primary" :href="lp('/zh/partners/opportunities/')">{{ tx('贊助方案', 'Sponsorship packages') }}</a>
      </div>
      <div class="cta-card" style="background:var(--ink)">
        <p class="cta-card__num">CONTACT</p>
        <p class="cta-card__title">{{ tx('聯絡商務部', 'Contact our Partnerships Department') }}</p>
        <p class="cta-card__desc">{{ tx('想進一步討論合作內容？我們期待與您對話。', 'Want to discuss a partnership in more detail? We look forward to hearing from you.') }}</p>
        <a class="btn btn--primary" :href="lp('/zh/join/partnership/')">{{ tx('洽談合作', 'Discuss a partnership') }}</a>
      </div>
    </div>
  </div>
</section>
</template>

<style>
/* 價值主張卡（value-prop-card）：建議收進共用 CSS，可與首頁 value-card 合併 */
.value-prop-grid{ margin-top:1rem; }
.value-prop-card{ background:var(--paper-2); border:1px solid var(--rule); padding:1.75rem 1.5rem; display:flex; flex-direction:column; gap:.65rem; }
.value-prop-card__num{ font-size:.72rem; font-weight:800; color:var(--muted); letter-spacing:.08em; }
.value-prop-card__title{ font-size:1.05rem; font-weight:800; color:var(--heading); }
.value-prop-card__title .en{ display:block; font-size:.68em; font-weight:700; letter-spacing:.06em; text-transform:uppercase; color:var(--brand-aa); margin-top:.3em; }
.value-prop-card__desc{ font-size:.85rem; color:var(--muted); line-height:1.65; }

.audience-note{ margin-top:1.5rem; font-size:.92rem; color:var(--muted-dark); max-width:56ch; line-height:1.75; }
</style>

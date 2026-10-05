<script setup lang="ts">
// app/pages/zh/academy/overview.vue — 由 site/src/pages/zh/academy/overview/index.html 轉來
// （S0-9 靜態頁搬遷），S1-15 補上俱樂部分支文案。
//
// 🔴 S1-15（2026-09-29）之前本頁對兩俱樂部零分支，字面寫死「台中磐石足球學院」——
// bw 容器直接顯示磐石學院內容，是既有缺口（不是本輪新增的迴歸）。藍鯨規劃書 §3.4
// 「04 青年隊沿用主站 04 的梯隊版型」（本頁屬於 04 單元的總覽子頁，適用同一句），
// 本輪改為讀 shared/utils/club-copy.ts 的 ACADEMY_OVERVIEW_*／ACADEMY_POSITIONING，
// 藍鯨文案逐句節錄自 content/blue-whale/squad/youth-teams.md（紀律 11）。
//
// unit 由粗粒度 '04' 改為 '4.1'，讓 units.ts 之後如果要單獨關閉某個 04 子頁
// （比照本輪 4.7 的做法）不必牽動本頁——見 shared/utils/units.ts 說明。
definePageMeta({ nav: "academy", unit: "4.1", enReady: true, enReadyBw: true })

const { lp, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')
const identity = computed(() => (isEn.value ? getClubIdentityEn() : getClubIdentity(clubKey.value)))
const academyCrumb = computed(() => (isEn.value ? getAcademyUnitLabelEn(clubKey.value) : identity.value.academyLabelZh))

// S1-12d 收尾：梯隊代碼改讀 useSiteFacts（後端公開端點）。本頁兩俱樂部皆會渲染，
// 兩邊各自的梯隊代碼都要能取得，故各自呼叫一次。
// S1-12d 收尾第二輪：hero／SEO／ACADEMY_POSITIONING 三者含成立年份／梯隊代碼事實，
// club-copy.ts 已改為工廠函式，一併從既有的兩次 useSiteFacts() 呼叫多取 facts，
// 不新增額外的 fetch。
// 🔴 F2（2026-10-03）：原本兩邊各呼叫一次，藍鯨站因此把磐石事實（場地、地址、梯隊代碼）
// 序列化進 SSR payload。改為只依當前站台呼叫一次。
const { facts: activeFacts, academyLabel } = useSiteFacts(clubKey.value)
// C-6／S2-13：主站 /en/ 讀 shared/utils/club-copy-en-acad.ts（英文版文案）。
const hero = computed(() => (isEn.value ? getAcademyOverviewHeroEn(clubKey.value, activeFacts.value) : getAcademyOverviewHero(clubKey.value, activeFacts.value)))
const positioning = computed(() => (isEn.value ? getAcademyPositioningEn(activeFacts.value, clubKey.value) : getAcademyPositioning(clubKey.value, activeFacts.value)))
const seo = computed(() => (isEn.value ? getAcademyOverviewSeoEn(clubKey.value, activeFacts.value) : getAcademyOverviewSeo(clubKey.value, activeFacts.value)))

useSeoMeta({
  title: computed(() => seo.value.title),
  description: computed(() => seo.value.description),
})
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/academy/')">{{ academyCrumb }}</a></li>
      <li aria-current="page">{{ tx(isTcrfc ? '學院總覽' : '青年隊總覽', isTcrfc ? 'Academy Overview' : 'Youth Teams Overview') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <!-- 藍鯨無青年隊訓練照片可用（客戶尚未提供，肖像同意狀態未知），不沿用磐石學院照片頂替 -->
  <img v-if="isTcrfc" class="page-hero__bg" :src="siteImg('/assets/img/academy/life-13.jpg')" alt="" width="1600" height="900">
  <div v-else class="page-hero__bg page-hero__bg--pending" aria-hidden="true"></div>
  <div class="container">
    <p class="page-hero__eyebrow">{{ isTcrfc ? '4.1 Academy Overview' : '4.1' }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<section class="band">
  <div class="container">
    <div class="prose">
      <h2>{{ tx(isTcrfc ? '學院定位' : '青年隊定位', isTcrfc ? 'Academy Positioning' : 'Youth Teams Positioning') }}</h2>
      <p>{{ positioning }}</p>
      <p v-if="isTcrfc">{{ tx('詳見', 'See') }} <a :href="lp('/zh/academy/pathway/')">{{ tx('4.3 學院發展路徑', '4.3 Academy Pathway') }}</a>{{ tx('。', '.') }}</p>
    </div>

    <h2 class="section-title" style="margin-top:3.5rem;">{{ tx('數據亮點', 'Highlights by the Numbers') }}</h2>
    <p class="section-lede" style="margin-top:.5rem;">{{ tx('學員數、教練數、升學率等數據，未來將於本頁公布。', 'Figures such as player numbers, coach numbers and school-progression rates will be published on this page in future.') }}</p>
  </div>
</section>

<section class="band grain cta-band">
  <div class="container">
    <div class="cta-grid">
      <a class="cta-card" :href="lp('/zh/academy/teams/')">
        <span class="cta-card__num">4.2</span>
        <span class="cta-card__title">{{ tx(isTcrfc ? '學院隊伍' : '青年隊', 'Our Teams') }}</span>
        <p class="cta-card__desc">{{ tx(isTcrfc ? `查看 ${academyLabel()} 各梯隊` : `查看 ${academyLabel()} 兩個梯隊`, `View the ${academyLabel(', ')} squads`) }}</p>
      </a>
      <!-- BW-C1：4.5 已重開（見 shared/utils/units.ts 檔頭），從原本綁在一起的
           isTcrfc 區塊拆出來，兩俱樂部都顯示；4.7（招生架構）維持關閉，只有磐石顯示。 -->
      <a class="cta-card" :href="lp('/zh/academy/coaches/')">
        <span class="cta-card__num">4.5</span>
        <span class="cta-card__title">{{ tx(isTcrfc ? '學院教練團' : '青年隊教練團', 'Coaches') }}</span>
        <p class="cta-card__desc">{{ tx('認識帶領各梯隊的教練', 'Meet the coaches who lead each squad') }}</p>
      </a>
      <a v-if="isTcrfc" class="cta-card" :href="lp('/zh/academy/join/')">
        <span class="cta-card__num">4.7</span>
        <span class="cta-card__title">{{ tx('加入學院', 'Join the Academy') }}</span>
        <p class="cta-card__desc">{{ tx('招生對象與遴選流程', 'Who we recruit and how selection works') }}</p>
      </a>
      <a v-else class="cta-card" :href="lp('/zh/schedule/')">
        <span class="cta-card__num">13</span>
        <span class="cta-card__title">{{ tx('賽事行事曆', 'Schedule') }}</span>
        <p class="cta-card__desc">{{ tx('查看俱樂部完整賽事時程', 'See the full match schedule') }}</p>
      </a>
    </div>
  </div>
</section>
</template>

<style>
/* 藍鯨無青年隊訓練照片時的純色回退，只用既有 token（比照 club/first-team/index.vue 既有寫法） */
.page-hero__bg--pending{ background:linear-gradient(160deg, var(--ink) 0%, var(--brand-deep) 100%); }
</style>

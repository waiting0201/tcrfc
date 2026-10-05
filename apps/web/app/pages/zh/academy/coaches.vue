<script setup lang="ts">
// app/pages/zh/academy/coaches.vue — 由 site/src/pages/zh/academy/coaches/index.html 轉來（S0-9 靜態頁搬遷）
//
// BW-C1（2026-09-29）：本頁對藍鯨重開（04 的 4.5 並不在藍鯨規劃書 §1.3 總則的例外之列）。
// 標題對藍鯨改「青年隊教練團」（不用「學院」字樣，見 check-club-brand-leak.mjs 詞表；
// 04 對藍鯨依 docs/13 §3 一律稱「青年隊」）。
//
// 2026-10-02：教練名單改接 `GET /{club}/staff`（C3 教練與職員，公開端點，照片已套肖像同意
// fail-closed）。原本寫死 3 位磐石教練（含 1 張教練照）已移除。青年隊教練＝`teamCodes` 與
// 該俱樂部青年梯隊代碼（getAcademyTeamTabs 的有效 teamCode）有交集的人員，不依賴 `team` 單值
// 查詢參數（同一位教練可兼任多個梯隊）。沒有任何教練資料（例：藍鯨名單未建）時顯示既有
// 「整理中」空狀態，不沿用磐石資料頂替。
definePageMeta({ nav: "academy", unit: "4.5", enReady: true, enReadyBw: true })

const { lp, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')
const identity = computed(() => (isEn.value ? getClubIdentityEn() : getClubIdentity(clubKey.value)))
const academyCrumb = computed(() => (isEn.value ? getAcademyUnitLabelEn(clubKey.value) : identity.value.academyLabelZh))

// S1-12d 收尾：梯隊代碼改讀 useSiteFacts(clubKey)（後端公開端點）。
const { facts, academyLabel } = useSiteFacts(clubKey.value)

// ---- 教練名單（C3 教練與職員）----
interface CoachStaff {
  id: string
  name: string | null
  title: string | null
  licence: string | null
  photoUrl: string | null
  teamCodes: string[]
}
const { locale } = useLocale()
const { data: staffData } = await useFetch<{ items: CoachStaff[] }>(`/api/backend/${config.public.club}/staff`, {
  query: { pageSize: 200, lang: locale.value },
})
const youthTeamCodes = computed(() =>
  getAcademyTeamTabs(clubKey.value, facts.value).map((t) => t.teamCode).filter((c): c is string => c !== null),
)
const coaches = computed(() =>
  (staffData.value?.items ?? []).filter((s) => s.name && s.teamCodes?.some((c) => youthTeamCodes.value.includes(c))),
)
/** 「證照、專長與負責梯隊」區塊：至少有一位教練填了證照或負責梯隊才顯示列表，否則維持準備中說明。 */
const coachDetails = computed(() => coaches.value.filter((c) => c.licence || c.teamCodes?.length))

useSeoMeta({
  title: computed(() => (isEn.value ? getYouthCoachesSeoEn(clubKey.value) : getYouthCoachesSeo(clubKey.value)).title),
  description: computed(() => (isEn.value ? getYouthCoachesSeoEn(clubKey.value) : getYouthCoachesSeo(clubKey.value)).description),
})
const hero = computed(() => (isEn.value ? getYouthCoachesHeroEn(clubKey.value) : getYouthCoachesHero(clubKey.value)))
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/academy/')">{{ academyCrumb }}</a></li>
      <li aria-current="page">{{ tx(identity.academyShortLabelZh + '教練團', 'Coaches') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <!-- 藍鯨無已核實、非過期的教練照片可用，不沿用磐石照片頂替 -->
  <img v-if="isTcrfc" class="page-hero__bg" :src="siteImg('/assets/img/academy/life-09.jpg')" alt="" width="1600" height="900">
  <div v-else class="page-hero__bg page-hero__bg--pending" aria-hidden="true" />
  <div class="container">
    <p class="page-hero__eyebrow">{{ isTcrfc ? '4.5 Coaches' : '4.5' }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<LocaleFallbackNotice v-if="isEn && hasFallbackLocale(staffData)" partial />
<section class="band" aria-labelledby="coaches-list-title">
  <div class="container">
    <!-- S1-12e（GEO-07）：這裡原本直接從 H1 跳到教練卡片的 h3（人名），中間沒有 h2，
         違反「H2/H3 不跳階」。加一個視覺隱藏的 h2 補上大綱層級，比照本站既有的
         `.visually-hidden` 標題慣例（見 zh/member/index.vue「member-title」、
         zh/news/[slug]/index.vue「article-body-title」），不影響版面。 -->
    <h2 id="coaches-list-title" class="visually-hidden">{{ tx('教練名單', 'Coaching staff') }}</h2>

    <div v-if="coaches.length > 0" class="grid grid--3 person-grid">
      <article v-for="c in coaches" :key="c.id" class="person-card clip-card clip-card--outlined">
        <div v-if="c.photoUrl" class="person-card__photo">
          <img :src="c.photoUrl" :alt="isEn ? `${c.title ?? 'Coach'} ${c.name}` : `${c.title ?? '教練'}${c.name}`" width="800" height="800" loading="lazy">
        </div>
        <div v-else class="person-card__photo person-card__photo--empty">
          <svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="8" r="4"/><path d="M4 20c0-4.4 3.6-8 8-8s8 3.6 8 8"/></svg>
        </div>
        <div class="person-card__body">
          <p class="person-card__role">{{ c.title ?? tx('教練團成員', 'Coaching staff member') }}</p>
          <h3 class="person-card__name">{{ c.name }}</h3>
        </div>
      </article>
    </div>
    <p v-else class="section-lede is-pending">{{ tx('教練名單整理中，待資料到位後將公布於本頁。', 'The coaching list is being compiled and will be published here once the information is ready.') }}</p>

    <h2 class="section-title" style="margin-top:4rem;">{{ tx('證照、專長與負責梯隊', 'Licences, Specialisms and Squads') }}</h2>
    <ul v-if="coachDetails.length > 0" class="coach-detail-list">
      <li v-for="c in coachDetails" :key="c.id">
        <strong>{{ c.name }}</strong>
        <span v-if="c.licence">{{ tx('　證照：', ' | Licence: ') }}{{ c.licence }}</span>
        <span v-if="c.teamCodes?.length">{{ tx('　負責梯隊：', ' | Squads: ') }}{{ c.teamCodes.join(tx('、', ', ')) }}</span>
      </li>
    </ul>
    <p v-else class="section-lede" style="margin-top:.5rem;">{{ tx('相關資料準備中，稍後將於本頁公布。', 'Details are being prepared and will be published on this page soon.') }}</p>
  </div>
</section>

<section class="band grain cta-band">
  <div class="container">
    <div class="cta-grid">
      <a class="cta-card" :href="lp('/zh/academy/teams/')">
        <span class="cta-card__num">4.2</span>
        <span class="cta-card__title">{{ tx(identity.academyShortLabelZh + '隊伍', 'Our Teams') }}</span>
        <p class="cta-card__desc">{{ tx(`查看 ${academyLabel()} 各梯隊`, `View the ${academyLabel(', ')} squads`) }}</p>
      </a>
      <a class="cta-card" :href="lp('/zh/academy/curriculum/')">
        <span class="cta-card__num">4.4</span>
        <span class="cta-card__title">{{ tx('訓練課程與課綱', 'Training & Curriculum') }}</span>
        <p class="cta-card__desc">{{ tx('五大訓練面向與週期規劃', 'Five training pillars and how the training cycle is planned') }}</p>
      </a>
      <a class="cta-card" :href="lp('/zh/academy/life/')">
        <span class="cta-card__num">4.6</span>
        <span class="cta-card__title">{{ tx(identity.academyShortLabelZh + '生活', isTcrfc ? 'Academy Life' : 'Youth Life') }}</span>
        <p class="cta-card__desc">{{ tx('訓練與比賽的日常紀錄', 'A day-to-day record of training and matches') }}</p>
      </a>
    </div>
  </div>
</section>
</template>

<style>
/* 藍鯨無對應照片時的頁首佔位漸層（BW-C1 新增，沿用 academy/pathway.vue 等既有樣式） */
.page-hero__bg--pending{ background:linear-gradient(160deg, var(--ink) 0%, var(--brand-deep) 100%); }
.is-pending{ color:var(--muted); font-style:italic; }

/* ── 4.5 專屬元件：人物卡 person-card ─────────────────
   2.4 團隊成員、3.1 一線隊教練團等頁面都需要同款「照片＋職稱＋姓名＋簡介」卡片，
   強烈建議把 .person-card 收進 tcrfc.css 成為全站共用元件。 */
.person-grid{ align-items:start; }
.person-card{ --clip-fill:var(--paper-2); }
.person-card__photo{ aspect-ratio:1/1; overflow:hidden; background:var(--paper-3); }
.person-card__photo img{ width:100%; height:100%; object-fit:cover; object-position:top center; }
.person-card__photo--empty{ display:flex; align-items:center; justify-content:center; }
.person-card__photo--empty svg{ width:34%; height:34%; fill:none; stroke:var(--rule); stroke-width:1.4; }
.person-card__body{ padding:1.5rem 1.5rem 1.75rem; }
.person-card__role{ font-size:.72rem; font-weight:800; letter-spacing:.08em; text-transform:uppercase; color:var(--brand-aa); }
.person-card__name{ font-size:1.3rem; font-weight:900; color:var(--heading); margin-top:.25rem; }
.person-card__pending{ margin-top:1rem; font-size:.82rem; }
.coach-detail-list{ list-style:none; margin:1rem 0 0; padding:0; display:grid; gap:.5rem; line-height:1.7; }
</style>

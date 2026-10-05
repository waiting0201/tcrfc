<script setup lang="ts">
// app/pages/zh/charity/our-impact.vue — 11.4 影響力數據（S2-9 起接上真實 API）
//
// 資料來源：`GET /api/backend/{club}/charity/impact?lang=`（後台 B5）：
//   - `metrics`：後台標為「公開」的統計項目（**金額類預設不公開**，公開端點只輸出 is_public=1，docs/14 E1a 規則④；
//     前台不得自行補算或顯示任何金額）；
//   - `charityCount`／`donationItemCount`（＝事蹟筆數）／`regions`（＝事蹟地點）：系統自動彙整；
//   - `charities`：合作公益團體（Logo 牆／名稱列表）。
// 🔴 計數為 0 一律顯示「—」（尚未公開），不顯示「0」：0 代表資料還沒建，不是「確實沒有合作團體」。
// 🔴 後端完全沒有資料（含 API 打不到）時，退回 mockup 時代人工整理的三個團體名稱與「3」筆已公開事蹟（真實，
// 有俱樂部新聞報導為憑據，S0-9 搬遷保留）；後台一旦有任何事蹟或公開統計，整頁換成後台資料，不混搭。
// 🔴 單元 11 對藍鯨整頁 404（藍鯨規劃書 §2.1）。
import type { ImpactSummary } from '#shared/utils/charity'

definePageMeta({ nav: 'charity', unit: '11', enReady: true })

const { lp, locale, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const club = config.public.club
const { donateLink } = await useCharityCta()

useSeoMeta({
  title: computed(() => (isEn.value ? 'Our Impact | Charity & Impact | Taichung Rock FC' : '影響力數據 Our Impact｜慈善與社會影響｜台中磐石足球俱樂部')),
  description: computed(() => (isEn.value
    ? 'Cumulative statistics on the charity work of Taichung Rock FC and a list of partner charities. Amount-based data is generally not made public.'
    : '台中磐石足球俱樂部公益投入的累計統計數據與合作公益團體列表。金額類數據原則上不公開。')),
})

const { data } = await useFetch<ImpactSummary>(`/api/backend/${club}/charity/impact`, {
  query: { lang: locale.value },
  key: `charity-impact-${club}-${locale.value}`,
})

const summary = computed(() => data.value ?? null)
const usingApi = computed(() => Boolean(summary.value && (summary.value.charityCount > 0 || summary.value.donationItemCount > 0 || summary.value.metrics.length > 0)))

interface StatCell { label: string, text: string | null }
const STATIC_ORGS = ['潭秀非營利幼兒園', '台中磐石盃少年足球隊伍', '潭秀國中暨嶺東高中聯隊']

const stats = computed<StatCell[]>(() => {
  const s = summary.value
  if (!usingApi.value || !s) {
    return [
      { label: tx('合作公益團體數', 'Partner charities'), text: null },
      { label: tx('累計捐助項次', 'Donation items to date'), text: null },
      { label: tx('服務地區數', 'Regions served'), text: null },
      { label: tx('已公開紀錄的慈善事蹟', 'Published charity impact records'), text: '3' },
    ]
  }
  const count = (n: number) => (n > 0 ? String(n) : null)
  return [
    { label: tx('合作公益團體數', 'Partner charities'), text: count(s.charityCount) },
    { label: tx('累計捐助項次', 'Donation items to date'), text: count(s.donationItemCount) },
    { label: tx('服務地區數', 'Regions served'), text: count(s.regions.length) },
    ...s.metrics
      .filter((m) => m.name)
      .map((m) => ({ label: m.name as string, text: m.value == null ? null : `${m.value.toLocaleString('en-US')}${m.unit ?? ''}` })),
  ]
})
const regions = computed(() => (usingApi.value ? summary.value?.regions ?? [] : []))
const charities = computed(() => (usingApi.value ? summary.value?.charities ?? [] : []))
const anyLogo = computed(() => charities.value.some((c) => c.logoUrl))
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/charity/')">{{ tx('慈善與社會影響', 'Charity & Impact') }}</a></li>
      <li aria-current="page">{{ tx('影響力數據', 'Our Impact') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <img class="page-hero__bg" :src="siteImg('/assets/img/news/2025-06-11-community-050.jpg')" alt="" width="1600" height="1068">
  <div class="container">
    <p class="page-hero__eyebrow">11.4 Our Impact</p>
    <h1><template v-if="isEn">Our Impact</template><template v-else>{{ tx('影響力數據', 'Our Impact') }}<span class="en">Our Impact</span></template></h1>
    <p class="page-hero__lede"><template v-if="isEn">Cumulative statistics and partner charities show the club's track record in charity work. <strong>Amount-based data is generally not made public</strong>, and each item can be shown or hidden individually.</template><template v-else>以累計統計與合作公益團體，呈現俱樂部投入公益的軌跡。<strong>金額類數據原則上不公開</strong>，後台可逐項決定是否顯示。</template></p>
  </div>
</section>

<LocaleFallbackNotice v-if="isEn && !usingApi" partial />

<section class="band grain grain--2 stats-band" aria-labelledby="impact-stats-title">
  <span class="ghost-num ghost-num--dark" aria-hidden="true" style="left:var(--edge);bottom:-1.5rem;">11.4</span>
  <div class="band-inner container">
    <h2 class="section-title" id="impact-stats-title" style="color:#fff;">{{ tx('累計統計', 'Cumulative statistics') }}</h2>
    <div class="stats-grid" style="margin-top:2rem;">
      <div v-for="st in stats" :key="st.label" class="stat">
        <p v-if="st.text" class="stat__num">{{ st.text }}</p>
        <p v-else class="stat__num" :aria-label="tx('尚未公開', 'Not yet announced')">—</p>
        <p class="stat__label">{{ st.label }}</p>
      </div>
    </div>
    <p v-if="regions.length" class="impact-regions">{{ tx('服務地區：', 'Regions served: ') }}{{ regions.join(tx('、', ', ')) }}</p>
  </div>
</section>

<section class="band sponsor-band" aria-labelledby="impact-partners-title">
  <div class="band-inner container">
    <h2 class="section-title" id="impact-partners-title">{{ tx('合作公益團體', 'Partner charities') }}</h2>
    <template v-if="usingApi">
      <div v-if="anyLogo" class="sponsor-grid">
        <PartnerLogoTile
          v-for="c in charities"
          :key="c.slug"
          :name="c.name"
          :logo-url="c.logoUrl"
          :href="safeExternalUrl(c.websiteUrl)"
          external
        />
      </div>
      <ul v-else-if="charities.length" class="partner-list">
        <li v-for="c in charities" :key="c.slug" class="partner-list__item">{{ c.name }}</li>
      </ul>
      <p v-else class="section-lede">{{ tx('合作公益團體名單整理中，稍後將於本頁公布。', 'The list of partner charities is being compiled and will be published here soon.') }}</p>
    </template>
    <template v-else>
      <p class="section-lede" style="margin-bottom:2rem;"><template v-if="isEn">Charities are listed by name until their logos are available. Once the official logos arrive, this will become a logo wall.</template><template v-else>依規劃書，尚無 Logo 圖檔時以名稱列表呈現；正式 Logo 到齊後將改為 Logo 牆。</template></p>
      <ul class="partner-list">
        <li v-for="n in STATIC_ORGS" :key="n" class="partner-list__item">{{ n }}</li>
      </ul>
    </template>
  </div>
</section>

<section class="band grain cta-band" aria-labelledby="impact-cta-title">
  <span class="ghost-num" aria-hidden="true" style="left:var(--edge);bottom:-1.5rem;color:rgba(255,255,255,.06);">11.4</span>
  <div class="band-inner container">
    <h2 class="section-title" id="impact-cta-title">{{ tx('相關內容', 'Related content') }}</h2>
    <div class="cta-grid">
      <a class="cta-card" :href="lp('/zh/charity/impact-stories/')">
        <span class="cta-card__num">11.3</span>
        <span class="cta-card__title">{{ tx('慈善事蹟', 'Impact Stories') }}</span>
        <p class="cta-card__desc">{{ tx('逐筆紀錄的時間軸', 'A timeline of every record') }}</p>
      </a>
      <a class="cta-card" :href="donateLink.href" :target="donateLink.external ? '_blank' : undefined" :rel="donateLink.external ? 'noopener noreferrer' : undefined">
        <span class="cta-card__num">01</span>
        <span class="cta-card__title">{{ tx('球迷捐款', 'Fan donations') }}</span>
        <p class="cta-card__desc">{{ tx('由台灣足球策略發展協會的慈善捐款平台承接；具名者列於徵信名單（僅顯示姓名）', 'Handled by the charity donation platform of the Taiwan Football Strategic Development Association; named donors appear on the donor acknowledgement list (name only)') }}</p>
      </a>
      <a class="cta-card" :href="lp('/zh/partners/opportunities/')">
        <span class="cta-card__num">9.4</span>
        <span class="cta-card__title">{{ tx('企業合作', 'Corporate partnerships') }}</span>
        <p class="cta-card__desc">{{ tx('成為合作公益團體或贊助夥伴', 'Become a partner charity or a sponsorship partner') }}</p>
      </a>
    </div>
  </div>
</section>
</template>

<style>
.partner-list{ display:flex; flex-direction:column; gap:.75rem; max-width:640px; }
.partner-list__item{
  padding:1rem 1.25rem; background:var(--paper); border:1px solid var(--rule);
  font-weight:700; color:var(--heading); font-size:.95rem;
}

.impact-regions{ margin-top:1.75rem; font-size:.9rem; color:var(--muted-dark); }
</style>

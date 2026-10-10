<script setup lang="ts">
// app/pages/zh/partners/our-sponsors/index.vue — 9.2 贊助商（S2-7 起接上真實 API）
//
// 資料來源：`GET /api/backend/{club}/sponsors?lang=`（apps/api Features/Sponsors，後台 E2）。
// 後端已依等級（主贊助→官方贊助→支持夥伴）排序、已排除合約結束者，並**不輸出**聯絡窗口、合約日期與到期
// 提醒（docs/14 E1a 規則⑤）——本頁不得另外去要這些欄位。
// 🔴 分區：端點以 club_id 分區，只讀目前容器所屬俱樂部（見 usePartners.ts 檔頭）。
// 🔴 贊助故事＝後台關聯的「已發布文章」（slug／title／summary），連到 07 新聞逐篇網址 `/zh/news/{slug}/`；
// 贊助活動紀錄＝每位贊助商底下的活動（日期、標題、成效摘要、圖集），本頁彙整成一張依日期新到舊的表。
// 🔴 空狀態：該等級沒有贊助商維持既有「尚未公開」虛線格；故事／活動沒有資料維持既有「準備中」文字。
// 🔴 等級字面值：後端是「主贊助／官方贊助／支持夥伴」，mockup 第三區標題沿用「支持贊助」——
// 用 `match` 對後端字面值、`title` 放頁面標題，兩者刻意分開（SPONSOR_TIER_SECTIONS）。
import type { PublicSponsor } from '#shared/utils/partners'
import { SPONSOR_TIER_SECTIONS, groupByKnownType, pickLogoProps } from '#shared/utils/partners'

definePageMeta({ nav: 'partners', unit: '9.2', enReady: true, enReadyBw: true })

const { lp, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const clubAssets = computed(() => getClubAssets(config.public.club))
const clubEn = computed(() => bizClubNameEn(config.public.club))
const charityEnabled = isUnitEnabledForClub('11', config.public.club)

const { sponsors } = await useSponsorList()

const tiers = computed(() => groupByKnownType(
  sponsors.value,
  (s) => s.tier,
  SPONSOR_TIER_SECTIONS.map((t) => ({ key: t.key, match: t.tier, title: t.heading, en: t.en })),
))
/** 有任何詳情欄位的贊助商才進詳情清單（只有名稱的不重複列一次）。 */
function rosterItems(items: PublicSponsor[]): PublicSponsor[] {
  return items.filter((s) => s.content || (charityEnabled && s.charityPrograms.length))
}
const BAND_CLASSES = ['band grain', 'band', 'band grain grain--2'] as const
function bandClass(i: number): string {
  return BAND_CLASSES[i] ?? (i % 2 === 0 ? 'band grain' : 'band')
}
function isDark(i: number): boolean {
  return bandClass(i).includes('grain')
}

const stories = computed(() => sponsors.value.flatMap((s) => s.stories.map((st) => ({ ...st, sponsorName: s.name }))))
const activations = computed(() => sponsors.value
  .flatMap((s) => s.activations.map((a) => ({ ...a, sponsorName: s.name })))
  .sort((a, b) => (b.happenedOn ?? '').localeCompare(a.happenedOn ?? '')))

useSeoMeta({
  title: computed(() => (isEn.value ? `Our Sponsors | Partners & Sponsors | ${clubEn.value}` : `贊助商 Our Sponsors｜合作夥伴與贊助｜${clubAssets.value.nameZh}`)),
  description: computed(() => (isEn.value ? `The sponsors of ${clubEn.value}, introduced by three tiers (Title, Official and Supporting), with sponsor stories and activation records.` : `${clubAssets.value.nameZh}的贊助商，依主贊助、官方、支持三個等級介紹，並收錄贊助故事與活動紀錄。`)),
})
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/partners/')">{{ tx('夥伴', 'Partners') }}</a></li>
      <li aria-current="page">{{ tx('贊助商', 'Our Sponsors') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">9.2 Our Sponsors</p>
    <h1><template v-if="isEn">Our Sponsors</template><template v-else>{{ tx('贊助商', 'Our Sponsors') }}<span class="en">Our Sponsors</span></template></h1>
    <p class="page-hero__lede"><template v-if="isEn">Thank you to every sponsor who supports {{ clubEn }}. Our sponsors are introduced below by tier.</template><template v-else>感謝每一位支持{{ clubAssets.shortNameZh }}的贊助夥伴，以下依贊助等級分區介紹。</template></p>
  </div>
</section>

<!-- SPEC 3.9 §9.2 — 依等級：主贊助／官方／支持（＋俱樂部自訂等級） -->
<section v-for="(tier, i) in tiers" :id="tier.key" :key="tier.key" :class="bandClass(i)" :aria-labelledby="`${tier.key}-title`">
  <div :class="isDark(i) ? 'band-inner container' : 'container'">
    <h2 :id="`${tier.key}-title`" class="section-title partner-type-title" :style="isDark(i) ? 'color:#fff' : undefined"><template v-if="isEn">{{ tier.en ?? tier.title }}</template><template v-else>{{ tier.title }}<span v-if="tier.en" class="en">{{ tier.en }}</span></template></h2>

    <div v-if="tier.items.length" class="sponsor-grid" :style="tier.key === 'title-sponsors' ? 'grid-template-columns:repeat(3,minmax(0,1fr))' : undefined">
      <PartnerLogoTile v-for="s in tier.items" :key="s.id" :name="s.name" v-bind="pickLogoProps(s)" />
    </div>
    <div v-else class="sponsor-grid" :style="tier.key === 'title-sponsors' ? 'grid-template-columns:repeat(3,minmax(0,1fr))' : undefined">
      <div v-for="n in (tier.key === 'title-sponsors' ? 3 : 5)" :key="n" class="sponsor-tile sponsor-tile--empty" :style="tier.key === 'title-sponsors' ? 'aspect-ratio:16/9' : undefined"><span>{{ tx('尚未公開', 'Not yet announced') }}</span></div>
    </div>

    <ul v-if="rosterItems(tier.items).length" class="partner-roster">
      <li v-for="s in rosterItems(tier.items)" :key="s.id" class="partner-roster__item">
        <h3 class="partner-roster__name">{{ s.name }}</h3>
        <p v-if="s.content" class="partner-roster__content">{{ s.content }}</p>
        <p v-if="charityEnabled && s.charityPrograms.length" class="partner-roster__link">
          {{ tx('共同參與的公益計畫：', 'Charity programs we take part in together:') }}
          <template v-for="(c, ci) in s.charityPrograms" :key="c.slug"><span v-if="ci">{{ tx('、', ', ') }}</span><a :href="lp(`/zh/charity/programs/${c.slug}/`)">{{ c.name ?? c.slug }}</a></template>
        </p>
      </li>
    </ul>
  </div>
</section>

<!-- SPEC 3.9 §9.2 — 贊助故事 -->
<section class="band" id="sponsor-stories" aria-labelledby="sponsor-stories-title">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">SPONSOR STORIES</p>
        <h2 class="section-title" id="sponsor-stories-title">{{ tx('贊助故事', 'Sponsor Stories') }}</h2>
      </div>
    </div>
    <div class="grid grid--3">
      <a v-for="st in stories" :key="`${st.sponsorName}-${st.slug}`" class="story-card story-card--link" :href="lp(`/zh/news/${st.slug}/`)">
        <span class="story-card__body">
          <span v-if="st.sponsorName" class="story-card__kicker">{{ st.sponsorName }}</span>
          <strong class="story-card__title">{{ st.title }}</strong>
          <span v-if="st.summary" class="story-card__summary">{{ st.summary }}</span>
        </span>
      </a>
      <article v-if="!stories.length" class="story-card">
        <p>{{ tx('贊助故事準備中，稍後將於本頁公布。', 'Sponsor stories are being prepared and will be published here soon.') }}</p>
      </article>
    </div>
  </div>
</section>

<!-- SPEC 3.9 §9.2 — 贊助活動紀錄 -->
<section class="band grain" id="sponsor-activity" aria-labelledby="sponsor-activity-title">
  <div class="band-inner container">
    <h2 class="section-title" id="sponsor-activity-title" style="color:#fff">{{ tx('贊助活動紀錄', 'Sponsor activations') }}</h2>
    <div class="table-scroll" style="margin-top:1.75rem">
      <table class="benefit-table">
        <thead>
          <tr><th scope="col">{{ tx('日期', 'Date') }}</th><th scope="col">{{ tx('活動', 'Activity') }}</th><th scope="col">{{ tx('合作贊助商', 'Sponsor') }}</th></tr>
        </thead>
        <tbody>
          <tr v-for="a in activations" :key="a.id">
            <td>{{ a.happenedOn ? a.happenedOn.replaceAll('-', '/') : '—' }}</td>
            <td>
              <strong class="activity-title">{{ a.title }}</strong>
              <span v-if="a.resultSummary" class="activity-summary">{{ a.resultSummary }}</span>
              <ImageGalleryStrip v-if="a.images.length" :images="a.images" :label="a.title" />
            </td>
            <td>{{ a.sponsorName }}</td>
          </tr>
          <tr v-if="!activations.length"><td colspan="3">{{ tx('活動紀錄準備中，稍後將於本頁公布。', 'Activation records are being prepared and will be published here soon.') }}</td></tr>
        </tbody>
      </table>
    </div>
  </div>
</section>
</template>

<style>
.partner-type-title{ font-size:var(--fs-h3); margin-bottom:1.75rem; }
.partner-type-title .en{ display:inline-block; margin-left:.6em; font-size:.5em; font-weight:700; letter-spacing:.08em; text-transform:uppercase; color:var(--brand-aa); vertical-align:middle; }
.grain .partner-type-title .en, .grain--2 .partner-type-title .en{ color:var(--brand); }

.sponsor-tile--empty{ border:1px dashed var(--rule); background:transparent; }
.grain .sponsor-tile--empty, .grain--2 .sponsor-tile--empty{ border-color:rgba(255,255,255,.2); }
.sponsor-tile--empty span{ font-size:.72rem; font-weight:700; color:var(--muted); letter-spacing:.04em; }
.grain .sponsor-tile--empty span, .grain--2 .sponsor-tile--empty span{ color:var(--muted-dark); }

.story-card{ background:var(--paper-2); border:1px solid var(--rule); padding:1.75rem; min-height:180px; display:flex; align-items:center; }

.table-scroll{ overflow-x:auto; }
.benefit-table{ width:100%; min-width:520px; border-collapse:collapse; background:var(--ink); color:#fff; }
.benefit-table th, .benefit-table td{ padding:.85rem 1.1rem; border:1px solid rgba(255,255,255,.12); font-size:.88rem; text-align:left; }
.benefit-table thead th{ background:rgba(255,255,255,.06); font-weight:800; }
.benefit-table td{ color:var(--muted-dark); }

/* 夥伴／贊助商詳情清單（與 9.1 同一份寫法，S2-7） */
.partner-roster{ list-style:none; padding:0; margin:2rem 0 0; display:grid; grid-template-columns:repeat(auto-fit,minmax(260px,1fr)); gap:1.5rem; }
.partner-roster__name{ font-size:1rem; font-weight:800; color:var(--heading); margin:0 0 .35rem; }
.partner-roster__content{ font-size:.88rem; line-height:1.7; color:var(--text); margin:0 0 .5rem; white-space:pre-line; }
.partner-roster__link{ font-size:.82rem; margin:0 0 .25rem; color:var(--muted); }
.partner-roster__link a{ color:var(--brand-aa); text-decoration:underline; }
.grain .partner-roster__name, .grain--2 .partner-roster__name{ color:#fff; }
.grain .partner-roster__content, .grain--2 .partner-roster__content, .grain .partner-roster__link, .grain--2 .partner-roster__link{ color:var(--muted-dark); }
.grain .partner-roster__link a, .grain--2 .partner-roster__link a{ color:var(--brand); }

.story-card--link{ text-decoration:none; color:inherit; transition:border-color var(--dur-fast) var(--ease); }
.story-card--link:hover{ border-color:var(--brand-aa); }
.story-card__body{ display:flex; flex-direction:column; gap:.4rem; }
.story-card__kicker{ font-size:.68rem; font-weight:800; letter-spacing:.06em; color:var(--brand-aa); }
.story-card__title{ font-size:1rem; line-height:1.5; color:var(--heading); }
.story-card__summary{ font-size:.85rem; line-height:1.6; color:var(--muted); }

.activity-title{ display:block; color:#fff; font-weight:800; }
.activity-summary{ display:block; margin-top:.3rem; line-height:1.6; }
</style>

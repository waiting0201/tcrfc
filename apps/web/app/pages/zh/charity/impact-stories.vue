<script setup lang="ts">
// app/pages/zh/charity/impact-stories.vue — 11.3 慈善事蹟紀錄（S2-9 起接上真實 API）
//
// 資料來源：`GET /api/backend/{club}/charity/records?lang=&pageSize=50`＋`.../records/years`（後台 B5「事蹟紀錄」）。
// 每筆三項核心資料：公益團體名稱（charityName）、捐助內容（donationContent）、活動圖片（imageUrl＋images）。
// 時間軸依年份分組、年份篩選鈕沿用 useYearChips（與 about/milestones 共用）。
// 🔴 後端單次最多 50 筆（`maxPageSize`）；超過 50 筆時本頁只顯示最新 50 筆並附「更多紀錄」提示——前台目前
// 沒有分頁 UI（年份篩選需要整批資料在 client），見 README「S2-9」節範圍縮減。
// 🔴 後端沒有任何事蹟紀錄（含 API 打不到）時，退回 mockup 時代人工整理的三筆真實事蹟（俱樂部既有新聞報導
// 為憑據，S0-9 搬遷保留）；後台一旦建立任何一筆事蹟，整頁換成後台資料（不混搭兩個來源）——這三筆應該由
// 內容人員補登進後台 B5（回報「待內容補登」）。
// 🔴 單元 11 對藍鯨整頁 404（藍鯨規劃書 §2.1）。
import type { ImpactRecord, PublicCharityImage } from '#shared/utils/charity'
import type { PagedResponse } from '#shared/utils/api-types'

definePageMeta({ nav: 'charity', unit: '11', enReady: true })

const { lp, locale, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const club = config.public.club
const { donateLink } = await useCharityCta()

useSeoMeta({
  title: computed(() => (isEn.value ? 'Impact Stories | Charity & Impact | Taichung Rock FC' : '慈善事蹟紀錄 Impact Stories｜慈善與社會影響｜台中磐石足球俱樂部')),
  description: computed(() => (isEn.value
    ? 'A timeline of the charity impact stories of Taichung Rock FC: beneficiary charities, what was donated and photo records, with a year filter.'
    : '台中磐石足球俱樂部的慈善事蹟時間軸：受贈公益團體、捐助內容與活動圖片紀錄，支援年份篩選。')),
})

const { data } = await useFetch<PagedResponse<ImpactRecord>>(`/api/backend/${club}/charity/records`, {
  query: { lang: locale.value, pageSize: 50 },
  key: `charity-records-${club}-${locale.value}`,
})


interface TimelineFact { label: string, text: string, href?: string }
interface TimelineItem {
  key: string
  date: string
  imageUrl: string | null
  imageAlt: string
  imageWidth: number | null
  imageHeight: number | null
  /** B-21：全部活動圖片（縮圖列＋燈箱），不再截斷成前 2 張。 */
  gallery: PublicCharityImage[]
  /** B-21：公益團體 Logo（後台有上傳才有值）。 */
  logoUrl: string | null
  logoWidth?: number | null
  logoHeight?: number | null
  logoAlt?: string
  title: string
  facts: TimelineFact[]
}
interface TimelineYear { key: string, label: string, items: TimelineItem[] }

/** 後端沒有任何事蹟時的過渡內容（見檔頭）：三筆都有對應的俱樂部新聞報導。 */
const STATIC_YEARS: TimelineYear[] = [
  { key: '2026', label: '2026', items: [{
    key: 's-2026-01-12', date: '2026-01-12', imageUrl: siteImg('/assets/img/news/2026-01-12-community-017.jpg'),
    imageAlt: tx('台中磐石攜手 Subkarma 捐贈英語書籍走進潭秀非營利幼兒園活動現場', 'Taichung Rock FC and Subkarma donating English books at a non-profit kindergarten'), imageWidth: 640, imageHeight: 427, gallery: [], logoUrl: null,
    title: '潭秀非營利幼兒園',
    facts: [
      { label: tx('捐助內容', 'Donated'), text: tx('英語書籍（與 Subkarma 合作捐贈）', 'English-language books (donated in partnership with Subkarma)') },
      { label: tx('相關報導', 'Related news'), text: tx('台中磐石攜手 Subkarma 深耕在地公益，捐贈英語書籍走進潭秀非營利幼兒園', 'Taichung Rock FC and Subkarma deepen local charity work, donating English books to a non-profit kindergarten'), href: '/zh/news/community/' },
    ],
  }] },
  { key: '2025', label: '2025', items: [{
    key: 's-2025-05-03', date: '2025-05-03', imageUrl: siteImg('/assets/img/news/2025-05-03-camps-056.jpg'),
    imageAlt: tx('2025台中磐石盃足球邀請賽活動現場', 'The 2025 Taichung Rock FC Cup football invitational'), imageWidth: 640, imageHeight: 480, gallery: [], logoUrl: null,
    title: '台中磐石盃少年足球隊伍',
    facts: [{ label: tx('相關活動', 'Related event'), text: tx('2025 台中磐石盃足球邀請賽', '2025 Taichung Rock FC Cup football invitational') }],
  }] },
  { key: '2024', label: '2024', items: [{
    key: 's-2024-07-23', date: '2024-07-23', imageUrl: null, imageAlt: '', imageWidth: 640, imageHeight: 427, gallery: [], logoUrl: null,
    title: '潭秀國中暨嶺東高中聯隊', facts: [],
  }] },
]

const apiRecords = computed(() => data.value?.items ?? [])
const usingApi = computed(() => apiRecords.value.length > 0)
const totalCount = computed(() => data.value?.totalCount ?? 0)

const years = computed<TimelineYear[]>(() => {
  if (!usingApi.value) return STATIC_YEARS
  const groups = new Map<string, TimelineYear>()
  for (const r of apiRecords.value) {
    const y = recordYear(r)
    const key = y == null ? 'undated' : String(y)
    let g = groups.get(key)
    if (!g) {
      g = { key, label: y == null ? tx('未標日期', 'Undated') : String(y), items: [] }
      groups.set(key, g)
    }
    const facts: TimelineFact[] = []
    if (r.donationContent) facts.push({ label: tx('捐助內容', 'Donated'), text: r.donationContent })
    if (r.location) facts.push({ label: tx('地點', 'Location'), text: r.location })
    if (r.briefDescription) facts.push({ label: tx('說明', 'Description'), text: r.briefDescription })
    if (r.programSlug) facts.push({ label: tx('所屬計畫', 'Program'), text: r.programName ?? r.programSlug, href: `/zh/charity/programs/${r.programSlug}/` })
    g.items.push({
      key: r.id,
      date: r.happenedOn ?? '',
      imageUrl: r.imageUrl,
      imageAlt: imgAlt(r.imageAlt, r.charityName ? (isEn.value ? `${r.charityName} activity photo` : `${r.charityName} 活動照片`) : ''),
      imageWidth: r.imageWidth ?? null,
      imageHeight: r.imageHeight ?? null,
      gallery: r.images,
      logoUrl: safeImageUrl(r.charityLogoUrl),
      logoWidth: r.charityLogoWidth ?? null,
      logoHeight: r.charityLogoHeight ?? null,
      logoAlt: imgAlt(r.charityLogoAlt, r.charityName, ''),
      title: r.charityName ?? '',
      facts,
    })
  }
  // 後端排序＝置頂優先、排序值、日期新到舊：年份區塊改依年份新到舊，區塊內保留後端順序；未標日期殿後。
  return [...groups.values()].sort((a, b) => (a.key === 'undated' ? 1 : b.key === 'undated' ? -1 : Number(b.key) - Number(a.key)))
})

const { activeYear, isPressed, isPanelHidden } = useYearChips()
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/charity/')">{{ tx('慈善與社會影響', 'Charity & Impact') }}</a></li>
      <li aria-current="page">{{ tx('慈善事蹟', 'Impact Stories') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <img class="page-hero__bg" :src="siteImg('/assets/img/news/2026-01-12-community-017.jpg')" alt="" width="1600" height="1068">
  <div class="container">
    <p class="page-hero__eyebrow">11.3 Impact Stories</p>
    <h1><template v-if="isEn">Impact Stories</template><template v-else>{{ tx('慈善事蹟', 'Impact Stories') }}<span class="en">Impact Stories</span></template></h1>
    <p class="page-hero__lede"><template v-if="isEn">Each record presents three key pieces of information: <strong>the name of the charity, what was donated and photos of the activity</strong>, giving a faithful account of the charity work the club has carried out.</template><template v-else>每一筆紀錄呈現三項核心資料：<strong>公益團體名稱、捐助內容與活動圖片</strong>，忠實呈現俱樂部已落地的公益行動。</template></p>
  </div>
</section>

<LocaleFallbackNotice v-if="isEn && (!usingApi || hasFallbackLocale(data))" partial />

<section class="band impact-band" aria-labelledby="impact-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="impact-title">{{ tx('慈善事蹟時間軸', 'Impact stories timeline') }}</h2>

    <div class="year-filter" role="group" :aria-label="tx('選擇年份', 'Select a year')">
      <button class="year-chip" type="button" data-year="all" :aria-pressed="isPressed('all')" @click="activeYear = 'all'">{{ tx('全部', 'All') }}</button>
      <button v-for="y in years" :key="y.key" class="year-chip" type="button" :data-year="y.key" :aria-pressed="isPressed(y.key)" @click="activeYear = y.key">{{ y.label }}</button>
    </div>

    <div class="timeline">
      <section v-for="y in years" :id="`impact-${y.key}`" :key="y.key" class="timeline-year" :data-year-panel="y.key" :hidden="isPanelHidden(y.key)">
        <h3 class="timeline-year__anchor">{{ y.label }}</h3>
        <ol class="timeline-list">
          <li v-for="it in y.items" :key="it.key" :class="['timeline-item', { 'timeline-item--no-media': !it.imageUrl }]">
            <p v-if="it.date" class="timeline-item__date">{{ it.date }}</p>
            <div v-if="it.imageUrl" class="timeline-item__media"><img :src="it.imageUrl" :alt="it.imageAlt" loading="lazy" v-bind="imgAttrs(it.imageWidth, it.imageHeight)"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">{{ tx('公益團體 Beneficiary', 'Beneficiary') }}</p>
              <h4 class="timeline-item__title">
                <img v-if="it.logoUrl" class="impact-logo" :src="it.logoUrl" :alt="imgAlt(it.logoAlt, tx(`${it.title} Logo`, `${it.title} logo`))" loading="lazy" :height="it.logoWidth && it.logoHeight ? it.logoHeight : undefined" :width="it.logoWidth && it.logoHeight ? it.logoWidth : undefined">
                {{ it.title }}
              </h4>
              <dl v-if="it.facts.length" class="impact-facts">
                <div v-for="f in it.facts" :key="f.label">
                  <dt>{{ f.label }}</dt>
                  <dd><a v-if="f.href" :href="lp(f.href)">{{ f.text }}</a><template v-else>{{ f.text }}</template></dd>
                </div>
              </dl>
              <ImageGalleryStrip v-if="it.gallery.length" :images="it.gallery" :label="it.title || tx('慈善事蹟', 'Impact story')" />
            </div>
          </li>
        </ol>
      </section>
    </div>
    <p v-if="usingApi && totalCount > apiRecords.length" class="impact-more"><template v-if="isEn">Showing the latest {{ apiRecords.length }} of {{ totalCount }} records.</template><template v-else>目前顯示最新 {{ apiRecords.length }} 筆，共 {{ totalCount }} 筆紀錄。</template></p>
  </div>
</section>

<section class="band grain cta-band" aria-labelledby="impact-cta-title">
  <span class="ghost-num" aria-hidden="true" style="left:var(--edge);bottom:-1.5rem;color:rgba(255,255,255,.06);">11.3</span>
  <div class="band-inner container">
    <h2 class="section-title" id="impact-cta-title">{{ tx('相關內容', 'Related content') }}</h2>
    <div class="cta-grid">
      <a class="cta-card" :href="lp('/zh/charity/our-impact/')">
        <span class="cta-card__num">11.4</span>
        <span class="cta-card__title">{{ tx('影響力數據', 'Our Impact') }}</span>
        <p class="cta-card__desc">{{ tx('累計統計與夥伴團體列表', 'Cumulative statistics and a list of partner organisations') }}</p>
      </a>
      <a class="cta-card" :href="lp('/zh/charity/programs/')">
        <span class="cta-card__num">11.2</span>
        <span class="cta-card__title">{{ tx('慈善計畫', 'Charity Programs') }}</span>
        <p class="cta-card__desc">{{ tx('正在進行與已完成的公益計畫', 'Ongoing and completed charity programs') }}</p>
      </a>
      <a class="cta-card" :href="donateLink.href" :target="donateLink.external ? '_blank' : undefined" :rel="donateLink.external ? 'noopener noreferrer' : undefined">
        <span class="cta-card__num">01</span>
        <span class="cta-card__title">{{ tx('支持我們', 'Support us') }}</span>
        <p class="cta-card__desc"><template v-if="isEn">Two ways to take part: corporate partnerships and fan donations. Fan donations are handled by the charity donation platform of the {{ CHARITY_RECIPIENT_EN }}.</template><template v-else>企業合作與球迷捐款兩種參與方式；球迷捐款由{{ CHARITY_RECIPIENT }}的慈善捐款平台承接</template></p>
      </a>
    </div>
  </div>
</section>
</template>

<style>
/* ── 11.3 沿用 2.8 Key Milestones 已建立的 .timeline／.year-filter 元件（見 about/milestones/index.html），
   並擴充 impact-facts 呈現「公益團體名稱／捐助內容」兩項核心資料。
   建議將 .timeline 系列元件收進共用 tcrfc.css（見 AGENT_BRIEF §6），屆時本頁與 2.8 可共用同一套樣式。 */
.impact-band{ padding-block:clamp(3.5rem,6vw,6rem); }

.year-filter{ display:flex; gap:.6rem; flex-wrap:wrap; margin-bottom:3rem; }
.year-chip{
  min-height:44px; padding:0 1.3rem; font-weight:800; font-size:.88rem;
  border:2px solid var(--rule); color:var(--muted);
  transition:background var(--dur-fast) var(--ease), color var(--dur-fast) var(--ease), border-color var(--dur-fast) var(--ease);
}
.year-chip:hover{ border-color:var(--brand-aa); color:var(--brand-aa); }
.year-chip[aria-pressed="true"]{ background:var(--brand-aa); border-color:var(--brand-aa); color:#fff; }

.timeline{ position:relative; max-width:800px; }
.timeline-year{ margin-bottom:3.5rem; }
.timeline-year:last-child{ margin-bottom:0; }
.timeline-year__anchor{
  font-size:2.4rem; font-weight:900; color:var(--brand-aa); letter-spacing:-.02em;
  margin-bottom:1.5rem; scroll-margin-top:110px;
}

.timeline-list{ position:relative; list-style:none; margin:0; padding:0; }
.timeline-list::before{
  content:""; position:absolute; left:7px; top:.4rem; bottom:.4rem; width:2px; background:var(--rule);
}
.timeline-item{
  position:relative; padding-left:2.5rem; padding-bottom:2rem;
  display:grid; grid-template-columns:auto 1fr; gap:0 1.25rem; align-items:start;
}
.timeline-item:last-child{ padding-bottom:0; }
.timeline-item--no-media{ grid-template-columns:1fr; gap:0; }
.timeline-item::before{
  content:""; position:absolute; left:0; top:.35rem; width:16px; height:16px; border-radius:50%;
  background:var(--paper); border:3px solid var(--brand-aa);
}
.timeline-item__date{
  grid-column:1/-1; font-size:.82rem; font-weight:800; color:var(--muted); letter-spacing:.03em;
  display:flex; align-items:center; flex-wrap:wrap; gap:.4rem; margin-bottom:.6rem;
}
.timeline-item__media{ width:140px; flex:none; aspect-ratio:3/2; overflow:hidden; }
.timeline-item__media img{ width:100%; height:100%; object-fit:cover; }
.timeline-item__body{ min-width:0; }
.timeline-item__tag{ font-size:.68rem; font-weight:800; letter-spacing:.06em; text-transform:uppercase; color:var(--brand-aa); margin-bottom:.35rem; }
.timeline-item__title{ font-size:1.1rem; font-weight:800; color:var(--heading); line-height:1.5; margin-bottom:.6rem; }

.impact-facts{ display:flex; flex-direction:column; gap:.35rem; }
.impact-facts div{ display:flex; gap:.5rem; font-size:.85rem; }
.impact-facts dt{ font-weight:700; color:var(--muted); flex:none; min-width:5.5em; }
.impact-facts dd{ color:var(--text); }
.impact-facts a{ color:var(--brand-aa); text-decoration:underline; }

@media (max-width:520px){
  .timeline-item{ grid-template-columns:1fr; }
  .timeline-item__media{ width:100%; aspect-ratio:16/9; margin-bottom:.75rem; }
}

.impact-logo{ display:inline-block; height:32px; width:auto; max-width:96px; object-fit:contain; vertical-align:middle; margin-right:.5rem; }
.impact-more{ margin-top:2rem; font-size:.85rem; color:var(--muted); }
</style>

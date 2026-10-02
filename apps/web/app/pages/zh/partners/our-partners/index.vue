<script setup lang="ts">
// app/pages/zh/partners/our-partners/index.vue — 9.1 合作夥伴（S2-7 起接上真實 API）
//
// 資料來源：`GET /api/backend/{club}/partners?lang=`（apps/api Features/Partners，後台 E1）。
// 🔴 分區：端點以 club_id 分區，藍鯨容器只會讀到藍鯨自己的夥伴，磐石同理（docs/14 五種商業對象、
// 藍鯨規劃書 §2.1「09 須與磐石分區」）；本頁不做任何跨俱樂部合併。
// 🔴 合作期間：後端只回「合作期間涵蓋今天」的夥伴（沒填起訖視為進行中），前台不重複篩一次。
// 🔴 空狀態：某類型沒有夥伴就維持既有「尚未公開」虛線格；API 打不到時同樣落回空狀態，不出 500。
// 🔴 國際夥伴靜態三個隊徽（Hellas Verona／Rayo Alcobendas／Rot-Weiss Ahlen）是磐石一線隊的真實海外
// 合作對象（S0-9 搬遷保留），**只在磐石、且後台「國際夥伴」類型一筆都還沒建時**作為過渡顯示；後台一
// 旦建立任何國際夥伴就整批換成後台資料（不混搭兩個來源），藍鯨一律不顯示（E-83）。
// 🔴 類型值是中文字面值（標準五類＋俱樂部自訂，如藍鯨的「指導單位」），不隨語系翻譯；標準五類區塊
// 標題的英文副標是版型固定文字，自訂類型沒有英文副標，只顯示後台填的類型名稱。
import type { PublicPartner } from '#shared/utils/partners'
import { PARTNER_TYPE_SECTIONS, groupByKnownType, pickLogoUrl, safeExternalUrl, formatPartnerPeriod } from '#shared/utils/partners'

definePageMeta({ nav: 'partners', unit: '9.1' })

const { lp } = useLocale()
const config = useRuntimeConfig()
const isTcrfc = computed(() => config.public.club !== 'bw')
const clubAssets = computed(() => getClubAssets(config.public.club))
// 夥伴詳情的「共同參與的公益計畫」連結只在有 11 慈善單元的俱樂部顯示（藍鯨不設 11，連過去會 404）。
const charityEnabled = isUnitEnabledForClub('11', config.public.club)

const { partners } = await usePartnerList()

const sections = computed(() => groupByKnownType(
  partners.value,
  (p) => p.partnerType,
  PARTNER_TYPE_SECTIONS.map((s) => ({ key: s.key, match: s.type, title: s.type, en: s.en })),
))
const hasAnyPartner = computed(() => partners.value.length > 0)

/** 有任何詳情欄位的夥伴才進詳情清單（只有名稱的不重複列一次，名稱已在 Logo 牆上）。 */
function rosterItems(items: PublicPartner[]): PublicPartner[] {
  return items.filter((p) => p.content || p.startOn || p.endOn || p.country || safeExternalUrl(p.websiteUrl) || (charityEnabled && p.charityPrograms.length))
}

/** 既有版型的底色輪替：標準五類固定如 mockup（深／淺／較深／淺／深），自訂類型接著淺、深交替。 */
const BAND_CLASSES = ['band grain', 'band', 'band grain grain--2', 'band', 'band grain'] as const
function bandClass(i: number): string {
  return BAND_CLASSES[i] ?? (i % 2 === 0 ? 'band grain' : 'band')
}
function isDark(i: number): boolean {
  return bandClass(i).includes('grain')
}

useSeoMeta({
  title: computed(() => `合作夥伴 Our Partners｜合作夥伴與贊助｜${clubAssets.value.nameZh}`),
  description: computed(() => `${clubAssets.value.nameZh}的合作夥伴，依策略、國際、訓練、教育、品牌五大類型分區介紹。`),
})
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/partners/')">夥伴</a></li>
      <li aria-current="page">合作夥伴</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">9.1 Our Partners</p>
    <h1>合作夥伴<span class="en">Our Partners</span></h1>
    <p v-if="hasAnyPartner" class="page-hero__lede">與{{ clubAssets.shortNameZh }}攜手的合作夥伴，依策略、國際、訓練、教育、品牌等類型分區介紹。</p>
    <p v-else class="page-hero__lede">以下為合作夥伴分類架構，實際夥伴名單建置中。</p>
  </div>
</section>

<!-- SPEC 3.9 §9.1 — 依類型分區：策略／國際／訓練／教育／品牌（＋俱樂部自訂類型）。Logo 牆＋夥伴詳情（合作內容、期間、連結） -->
<section v-for="(sec, i) in sections" :id="sec.key" :key="sec.key" :class="bandClass(i)" :aria-labelledby="`${sec.key}-title`">
  <div :class="isDark(i) ? 'band-inner container' : 'container'">
    <h2 :id="`${sec.key}-title`" class="section-title partner-type-title" :style="isDark(i) ? 'color:#fff' : undefined">{{ sec.title }}<span v-if="sec.en" class="en">{{ sec.en }}</span></h2>

    <div v-if="sec.items.length" class="sponsor-grid">
      <PartnerLogoTile
        v-for="p in sec.items"
        :key="p.id"
        :name="p.name"
        :logo-url="pickLogoUrl(p)"
        :href="safeExternalUrl(p.websiteUrl)"
        external
      />
    </div>

    <!-- 國際夥伴：後台尚未建立任何一筆時，磐石沿用既有三個海外合作隊徽（見檔頭說明），藍鯨顯示空格 -->
    <div v-else-if="sec.key === 'international' && isTcrfc" class="sponsor-grid">
      <div class="sponsor-tile">
        <img :src="siteImg('/assets/img/partners-intl/partner-intl-01-hellas-verona.webp')" alt="義大利 Hellas Verona FC 隊徽" loading="lazy" width="200" height="200">
      </div>
      <div class="sponsor-tile">
        <img :src="siteImg('/assets/img/partners-intl/partner-intl-02-rayo-alcobendas.png')" alt="西班牙 Rayo Ciudad Alcobendas CF 隊徽" loading="lazy" width="200" height="200">
      </div>
      <div class="sponsor-tile">
        <img :src="siteImg('/assets/img/partners-intl/partner-intl-03-rot-weiss-ahlen.webp')" alt="德國 Rot-Weiss Ahlen 隊徽" loading="lazy" width="200" height="200">
      </div>
      <div class="sponsor-tile sponsor-tile--empty"><span>尚未公開</span></div>
    </div>
    <div v-else class="sponsor-grid">
      <div v-for="n in 4" :key="n" class="sponsor-tile sponsor-tile--empty"><span>尚未公開</span></div>
    </div>

    <!-- 夥伴詳情：合作內容、合作期間、國家、官方網站、共同參與的公益計畫；都沒有的欄位整列不顯示 -->
    <ul v-if="rosterItems(sec.items).length" class="partner-roster">
      <li v-for="p in rosterItems(sec.items)" :key="p.id" class="partner-roster__item">
        <h3 class="partner-roster__name">{{ p.name }}</h3>
        <p v-if="p.country || formatPartnerPeriod(p.startOn, p.endOn)" class="partner-roster__meta">
          <span v-if="p.country">{{ p.country }}</span>
          <span v-if="formatPartnerPeriod(p.startOn, p.endOn)">合作期間 {{ formatPartnerPeriod(p.startOn, p.endOn) }}</span>
        </p>
        <p v-if="p.content" class="partner-roster__content">{{ p.content }}</p>
        <p v-if="safeExternalUrl(p.websiteUrl)" class="partner-roster__link"><a :href="safeExternalUrl(p.websiteUrl) ?? undefined" target="_blank" rel="noopener noreferrer">官方網站（另開新分頁）</a></p>
        <p v-if="charityEnabled && p.charityPrograms.length" class="partner-roster__link">
          共同參與的公益計畫：
          <template v-for="(c, ci) in p.charityPrograms" :key="c.slug"><span v-if="ci">、</span><a :href="lp(`/zh/charity/programs/${c.slug}/`)">{{ c.name ?? c.slug }}</a></template>
        </p>
      </li>
    </ul>
  </div>
</section>

<section class="band" aria-labelledby="op-cta-title">
  <div class="container">
    <h2 class="visually-hidden" id="op-cta-title">成為合作夥伴</h2>
    <div class="cta-card" style="background:var(--ink);max-width:640px">
      <p class="cta-card__num">9.3</p>
      <p class="cta-card__title">想成為{{ clubAssets.shortNameZh }}的合作夥伴？</p>
      <p class="cta-card__desc">了解與{{ clubAssets.shortNameZh }}合作的六大價值，以及受眾數據概況。</p>
      <a class="btn btn--primary" :href="lp('/zh/partners/become-a-partner/')">成為合作夥伴</a>
    </div>
  </div>
</section>
</template>

<style>
/* 夥伴分類標題（partner-type-title）：小節內的次級標題，非跨頁 section-title */
.partner-type-title{ font-size:var(--fs-h3); margin-bottom:1.75rem; }
.partner-type-title .en{ display:inline-block; margin-left:.6em; font-size:.5em; font-weight:700; letter-spacing:.08em; text-transform:uppercase; color:var(--brand-aa); vertical-align:middle; }
.grain .partner-type-title .en, .grain--2 .partner-type-title .en{ color:var(--brand); }

/* 空的 Logo 牆格位：sponsor-tile 的尚未公開變體，建議收進共用 CSS */
.sponsor-tile--empty{
  border:1px dashed var(--rule); background:transparent;
}
.grain .sponsor-tile--empty, .grain--2 .sponsor-tile--empty{ border-color:rgba(255,255,255,.2); }
.sponsor-tile--empty span{ font-size:.72rem; font-weight:700; color:var(--muted); letter-spacing:.04em; }
.grain .sponsor-tile--empty span, .grain--2 .sponsor-tile--empty span{ color:var(--muted-dark); }

/* 夥伴詳情清單（S2-7）：Logo 牆下方的文字說明，深色底區塊與淺色底區塊各自換字色 */
.partner-roster{ list-style:none; padding:0; margin:2rem 0 0; display:grid; grid-template-columns:repeat(auto-fit,minmax(260px,1fr)); gap:1.5rem; }
.partner-roster__name{ font-size:1rem; font-weight:800; color:var(--heading); margin:0 0 .35rem; }
.partner-roster__meta{ display:flex; flex-wrap:wrap; gap:.25rem .9rem; font-size:.78rem; font-weight:700; color:var(--muted); margin:0 0 .5rem; }
.partner-roster__content{ font-size:.88rem; line-height:1.7; color:var(--text); margin:0 0 .5rem; white-space:pre-line; }
.partner-roster__link{ font-size:.82rem; margin:0 0 .25rem; color:var(--muted); }
.partner-roster__link a{ color:var(--brand-aa); text-decoration:underline; }
.grain .partner-roster__name, .grain--2 .partner-roster__name{ color:#fff; }
.grain .partner-roster__meta, .grain--2 .partner-roster__meta,
.grain .partner-roster__link, .grain--2 .partner-roster__link{ color:var(--muted-dark); }
.grain .partner-roster__content, .grain--2 .partner-roster__content{ color:var(--muted-dark); }
.grain .partner-roster__link a, .grain--2 .partner-roster__link a{ color:var(--brand); }
</style>

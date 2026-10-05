<script setup lang="ts">
// app/pages/zh/club/international-pathways/index.vue — 由 site/src/pages/zh/club/international-pathways/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
// ⛔ 原頁 <script>（地區分頁 tablist 切換）改寫為 onMounted，行為逐字等價。
//
// BW-C1（2026-09-29）：本頁對藍鯨重開（S2-8 曾以「沒有海外合作俱樂部或旅外案例」為由
// 整頁 404，是誤用——藍鯨規劃書 §1.3 總則例外只有四項，不含 3.4，見
// shared/utils/units.ts 檔頭）。S2-8 當時只查了「有沒有合作俱樂部 Logo」，沒有查
// club-profile.md §4 沿革——沿革其實有三筆真實旅外事實（蔡明容／程思瑜／蘇育萱旅外
// 日本，蘇育萱另旅外中國），改為讀 getInternationalPathwaysSeo()／
// getInternationalPathwaysHero() 與 INTL_PATHWAY_JAPAN_NOTES_BW／
// INTL_PATHWAY_CHINA_NOTE_BW（club-copy.ts）。地區頁籤對藍鯨改為 Japan／China（球員
// 實際旅外地區），合作俱樂部 Logo 牆藍鯨維持空狀態（沒有可公開授權使用的海外合作
// 俱樂部 Logo，不得挪用磐石三個海外俱樂部 Logo 充數）。
//
// 2026-10-02：海外合作俱樂部 Logo 牆（分區頁籤內的 Logo 與下方「合作俱樂部」牆）改接
// E1 `GET /{club}/partners`（夥伴類型「國際夥伴」，與 9.1 our-partners 同一份資料、同一個
// 過渡規則）：後台一旦建立任何國際夥伴就整批換成後台資料；磐石在後台一筆都還沒建時，沿用
// 既有三個海外合作隊徽作過渡顯示（FALLBACK_INTL_TILES，S0-9 搬遷保留）；藍鯨不使用該備援
// （E-83）。分區頁籤依夥伴的 `country` 欄位歸類（日本／香港／中國，其餘歸歐洲）。
// ⚠️ 「分區資訊」的旅外球員案例（楊朝景、INTL_PATHWAY_*_NOTES_BW）與各地區說明文字仍是
// 寫死內容，後端沒有對應型別（待決，見 apps/web/README.md）。
import { PARTNER_TYPE_SECTIONS, pickLogoUrl, safeExternalUrl } from '#shared/utils/partners'

definePageMeta({ nav: 'club', unit: '3.4', enReady: true })

const { lp, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')

// ---- 海外合作俱樂部（E1 夥伴，類型＝國際夥伴）----
interface IntlTile {
  key: string
  name: string
  country: string | null
  logo: string | null
  alt: string
  region: 'europe' | 'japan' | 'hk' | 'china'
  href: string | null
}
const FALLBACK_INTL_TILES: IntlTile[] = [
  { key: 'verona', name: 'Hellas Verona FC', country: '義大利 Italy', logo: siteImg('/assets/img/partners-intl/partner-intl-01-hellas-verona.webp'), alt: 'Hellas Verona FC 標誌', region: 'europe', href: null },
  { key: 'rayo', name: 'Rayo Ciudad Alcobendas CF', country: '西班牙 Spain', logo: siteImg('/assets/img/partners-intl/partner-intl-02-rayo-alcobendas.png'), alt: 'Rayo Ciudad Alcobendas CF 標誌', region: 'europe', href: null },
  { key: 'ahlen', name: 'Rot-Weiss Ahlen', country: '德國 Germany', logo: siteImg('/assets/img/partners-intl/partner-intl-03-rot-weiss-ahlen.webp'), alt: 'Rot-Weiss Ahlen 標誌', region: 'europe', href: null },
]
function regionOfCountry(country: string | null): IntlTile['region'] {
  const c = (country ?? '').toLowerCase()
  if (/日本|japan/.test(c)) return 'japan'
  if (/香港|hong\s*kong/.test(c)) return 'hk'
  if (/中國|中国|china/.test(c)) return 'china'
  return 'europe'
}
const { partners } = await usePartnerList()
const apiIntlTiles = computed<IntlTile[]>(() =>
  partners.value
    .filter((p) => p.partnerType === PARTNER_TYPE_SECTIONS[1].type && p.name)
    .map((p) => ({
      key: p.id,
      name: p.name!,
      country: p.country,
      logo: pickLogoUrl(p),
      alt: isEn.value ? `${p.name} logo` : `${p.name} 標誌`,
      region: regionOfCountry(p.country),
      href: safeExternalUrl(p.websiteUrl),
    })),
)
const intlTiles = computed<IntlTile[]>(() => {
  if (apiIntlTiles.value.length) return apiIntlTiles.value
  return isTcrfc.value ? FALLBACK_INTL_TILES : []
})
const tilesIn = (region: IntlTile['region']) => intlTiles.value.filter((t) => t.region === region)
function tileSrc(t: IntlTile): string | null {
  return t.logo
}

useSeoMeta({
  title: computed(() => (isEn.value ? getInternationalPathwaysSeoEn() : getInternationalPathwaysSeo(clubKey.value)).title),
  description: computed(() => (isEn.value ? getInternationalPathwaysSeoEn() : getInternationalPathwaysSeo(clubKey.value)).description),
})
const hero = computed(() => (isEn.value ? getInternationalPathwaysHeroEn() : getInternationalPathwaysHero(clubKey.value)))
/** 國際夥伴 `country` 欄在英文版的顯示（可能含中文）。 */
function countryText(t: IntlTile): string | null {
  return isEn.value ? clubCountryLabelEn(t.country) : t.country
}

onMounted(() => {
  const tabs = document.querySelectorAll<HTMLButtonElement>('.region-tab')
  const panels = document.querySelectorAll<HTMLElement>('.region-panel')
  tabs.forEach((tab) => {
    tab.addEventListener('click', () => {
      tabs.forEach((t) => { t.setAttribute('aria-selected', 'false'); t.tabIndex = -1 })
      tab.setAttribute('aria-selected', 'true')
      tab.tabIndex = 0
      const region = tab.dataset.region
      panels.forEach((p) => { p.hidden = p.dataset.regionPanel !== region })
    })
  })
})
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/club/')">{{ tx('俱樂部', 'Football Club') }}</a></li>
      <li aria-current="page">{{ tx('國際發展通道', 'International Pathways') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <!-- 藍鯨無已核實可用的海外交流照片可用，不沿用磐石照片頂替（比照 academy/pathway.vue 既有做法） -->
  <img v-if="isTcrfc" class="page-hero__bg" :src="siteImg('/assets/img/trencin-01.jpg')" alt="" width="1920" height="1279">
  <div v-else class="page-hero__bg page-hero__bg--pending" aria-hidden="true" />
  <div class="container">
    <p class="page-hero__eyebrow">{{ isTcrfc ? '3.4 International Pathways' : '3.4' }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<section class="band" id="pathway-overview" aria-labelledby="pathway-overview-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">THE PATHWAY</p>
        <h2 class="section-title" id="pathway-overview-title">{{ tx('路徑總覽', 'Pathway overview') }}</h2>
      </div>
    </div>
    <ol class="pathway-flow">
      <li class="pathway-step">
        <span class="pathway-step__num">01</span>
        <p class="pathway-step__zh">{{ tx('在地', 'Local Roots') }}</p>
        <p v-if="!isEn" class="pathway-step__en">Local Roots</p>
      </li>
      <li class="pathway-step__arrow" aria-hidden="true">→</li>
      <li class="pathway-step">
        <span class="pathway-step__num">02</span>
        <p class="pathway-step__zh">{{ tx('國內職業', 'Domestic Pro') }}</p>
        <p v-if="!isEn" class="pathway-step__en">Domestic Pro</p>
      </li>
      <li class="pathway-step__arrow" aria-hidden="true">→</li>
      <li class="pathway-step">
        <span class="pathway-step__num">03</span>
        <p class="pathway-step__zh">{{ tx('海外訓練', 'Overseas Training') }}</p>
        <p v-if="!isEn" class="pathway-step__en">Overseas Training</p>
      </li>
      <li class="pathway-step__arrow" aria-hidden="true">→</li>
      <li class="pathway-step">
        <span class="pathway-step__num">04</span>
        <p class="pathway-step__zh">{{ tx('海外俱樂部', 'Clubs Abroad') }}</p>
        <p v-if="!isEn" class="pathway-step__en">Clubs Abroad</p>
      </li>
    </ol>
  </div>
</section>

<LocaleFallbackNotice v-if="isEn && hasFallbackLocale(partners)" partial />
<section class="band grain regions-band" id="regions" aria-labelledby="regions-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker kicker--on-dark">BY REGION</p>
        <h2 class="section-title" id="regions-title">{{ tx('分區資訊', 'Regional information') }}</h2>
      </div>
    </div>

    <!-- 磐石：Europe／Japan／HK 三分區（有合作俱樂部與旅外球員）。
         藍鯨：Japan／China 兩分區（真實旅外球員案例，見 club-copy.ts
         INTL_PATHWAY_JAPAN_NOTES_BW／INTL_PATHWAY_CHINA_NOTE_BW），沒有可公開的
         海外合作俱樂部 Logo，故不設 Europe 分區。 -->
    <div v-if="isTcrfc" class="region-tabs" role="tablist" :aria-label="tx('選擇地區', 'Select a region')">
      <button type="button" class="region-tab" role="tab" id="tab-europe" aria-controls="panel-europe" aria-selected="true" data-region="europe">{{ tx('Europe 歐洲', 'Europe') }}</button>
      <button type="button" class="region-tab" role="tab" id="tab-japan" aria-controls="panel-japan" aria-selected="false" data-region="japan" tabindex="-1">{{ tx('Japan 日本', 'Japan') }}</button>
      <button type="button" class="region-tab" role="tab" id="tab-hk" aria-controls="panel-hk" aria-selected="false" data-region="hk" tabindex="-1">{{ tx('Hong Kong 香港', 'Hong Kong') }}</button>
    </div>
    <div v-else class="region-tabs" role="tablist" aria-label="選擇地區">
      <button id="tab-japan" type="button" class="region-tab" role="tab" aria-controls="panel-japan" aria-selected="true" data-region="japan">Japan 日本</button>
      <button id="tab-china" type="button" class="region-tab" role="tab" aria-controls="panel-china" aria-selected="false" data-region="china" tabindex="-1">China 中國</button>
    </div>

    <template v-if="isTcrfc">
      <div class="region-panel" id="panel-europe" role="tabpanel" aria-labelledby="tab-europe" data-region-panel="europe">
        <p class="region-panel__desc">{{ tilesIn('europe').length ? tx('目前已建立聯繫的歐洲俱樂部，詳細合作內容與申請流程整理中。', 'European clubs we are currently in contact with. Details of the partnership and the application process are being prepared.') : tx('歐洲尚無正式合作俱樂部可公開，相關進展將更新於本頁。', 'There are no formal partner clubs in Europe that can be made public yet. Updates will be posted on this page.') }}</p>
        <div v-if="tilesIn('europe').length" class="region-partners">
          <component :is="t.href ? 'a' : 'div'" v-for="t in tilesIn('europe')" :key="t.key" class="region-partner-tile" :href="t.href || undefined" :target="t.href ? '_blank' : undefined" :rel="t.href ? 'noopener noreferrer' : undefined">
            <img v-if="tileSrc(t)" :src="tileSrc(t)!" :alt="isEn ? `${t.name} logo` : t.alt" loading="lazy" width="200" height="200">
            <span>{{ t.name }}<template v-if="t.country"><br><small>{{ countryText(t) }}</small></template></span>
          </component>
        </div>
      </div>

      <div class="region-panel" id="panel-japan" role="tabpanel" aria-labelledby="tab-japan" data-region-panel="japan" hidden>
        <p class="region-panel__desc">{{ tilesIn('japan').length ? tx('日本的合作俱樂部：', 'Partner clubs in Japan:') : tx('日本尚無正式合作俱樂部或協議可公開，相關進展將更新於本頁。', 'There are no formal partner clubs or agreements in Japan that can be made public yet. Updates will be posted on this page.') }}</p>
        <div v-if="tilesIn('japan').length" class="region-partners">
          <component :is="t.href ? 'a' : 'div'" v-for="t in tilesIn('japan')" :key="t.key" class="region-partner-tile" :href="t.href || undefined" :target="t.href ? '_blank' : undefined" :rel="t.href ? 'noopener noreferrer' : undefined">
            <img v-if="tileSrc(t)" :src="tileSrc(t)!" :alt="isEn ? `${t.name} logo` : t.alt" loading="lazy" width="200" height="200">
            <span>{{ t.name }}<template v-if="t.country"><br><small>{{ countryText(t) }}</small></template></span>
          </component>
        </div>
      </div>

      <div class="region-panel" id="panel-hk" role="tabpanel" aria-labelledby="tab-hk" data-region-panel="hk" hidden>
        <p class="region-panel__desc">{{ tilesIn('hk').length ? tx('香港的合作俱樂部，以及一線隊球員實際旅外案例：', 'Partner clubs in Hong Kong, and a real case of a First Team player going abroad:') : tx('香港暫無正式合作俱樂部標誌或協議文件，但已有一線隊球員實際旅外案例：', 'There is no formal partner club logo or agreement in Hong Kong yet, but a First Team player has already gone abroad:') }}</p>
        <div v-if="tilesIn('hk').length" class="region-partners">
          <component :is="t.href ? 'a' : 'div'" v-for="t in tilesIn('hk')" :key="t.key" class="region-partner-tile" :href="t.href || undefined" :target="t.href ? '_blank' : undefined" :rel="t.href ? 'noopener noreferrer' : undefined">
            <img v-if="tileSrc(t)" :src="tileSrc(t)!" :alt="isEn ? `${t.name} logo` : t.alt" loading="lazy" width="200" height="200">
            <span>{{ t.name }}<template v-if="t.country"><br><small>{{ countryText(t) }}</small></template></span>
          </component>
        </div>
        <div class="hk-player-note clip-card clip-card--on-dark">
          <div class="hk-player-note__visual">
            <img src="/assets/brand/svg/tcrfc-mark-white.svg" alt="" width="48" height="50" aria-hidden="true">
            <span>11</span>
          </div>
          <div>
            <p class="hk-player-note__name">楊朝景<span class="badge badge--on-dark">{{ tx('旅外', 'Abroad') }}</span></p>
            <p class="hk-player-note__desc">{{ tx('一線隊 11 號中場，目前效力於香港九龍城（Kowloon City）。', 'First Team No. 11, a midfielder, currently playing for Kowloon City in Hong Kong.') }}</p>
            <a :href="lp('/zh/club/player-stories/')">{{ tx('查看球員故事 →', 'View Player Stories →') }}</a>
          </div>
        </div>
      </div>
    </template>
    <template v-else>
      <div id="panel-japan" class="region-panel" role="tabpanel" aria-labelledby="tab-japan" data-region-panel="japan">
        <p class="region-panel__desc">{{ tilesIn('japan').length ? '日本的合作俱樂部，以及球員實際旅外案例：' : '日本尚無正式合作俱樂部或協議可公開，但已有球員實際旅外案例：' }}</p>
        <div v-if="tilesIn('japan').length" class="region-partners">
          <component :is="t.href ? 'a' : 'div'" v-for="t in tilesIn('japan')" :key="t.key" class="region-partner-tile" :href="t.href || undefined" :target="t.href ? '_blank' : undefined" :rel="t.href ? 'noopener noreferrer' : undefined">
            <img v-if="tileSrc(t)" :src="tileSrc(t)!" :alt="isEn ? `${t.name} logo` : t.alt" loading="lazy" width="200" height="200">
            <span>{{ t.name }}<template v-if="t.country"><br><small>{{ countryText(t) }}</small></template></span>
          </component>
        </div>
        <div v-for="note in INTL_PATHWAY_JAPAN_NOTES_BW" :key="note.nameZh" class="hk-player-note clip-card clip-card--on-dark" style="margin-bottom:1rem;">
          <div class="hk-player-note__visual"><span aria-hidden="true">✈</span></div>
          <div>
            <p class="hk-player-note__name">{{ note.nameZh }}<span class="badge badge--on-dark">旅外</span></p>
            <p class="hk-player-note__desc">{{ note.descZh }}</p>
          </div>
        </div>
      </div>

      <div id="panel-china" class="region-panel" role="tabpanel" aria-labelledby="tab-china" data-region-panel="china" hidden>
        <p class="region-panel__desc">{{ tilesIn('china').length ? '中國的合作俱樂部，以及球員實際旅外案例：' : '中國尚無正式合作俱樂部或協議可公開，但已有球員實際旅外案例：' }}</p>
        <div v-if="tilesIn('china').length" class="region-partners">
          <component :is="t.href ? 'a' : 'div'" v-for="t in tilesIn('china')" :key="t.key" class="region-partner-tile" :href="t.href || undefined" :target="t.href ? '_blank' : undefined" :rel="t.href ? 'noopener noreferrer' : undefined">
            <img v-if="tileSrc(t)" :src="tileSrc(t)!" :alt="isEn ? `${t.name} logo` : t.alt" loading="lazy" width="200" height="200">
            <span>{{ t.name }}<template v-if="t.country"><br><small>{{ countryText(t) }}</small></template></span>
          </component>
        </div>
        <div class="hk-player-note clip-card clip-card--on-dark">
          <div class="hk-player-note__visual"><span aria-hidden="true">✈</span></div>
          <div>
            <p class="hk-player-note__name">{{ INTL_PATHWAY_CHINA_NOTE_BW.nameZh }}<span class="badge badge--on-dark">旅外</span></p>
            <p class="hk-player-note__desc">{{ INTL_PATHWAY_CHINA_NOTE_BW.descZh }}</p>
          </div>
        </div>
      </div>
    </template>
  </div>
</section>

<section class="band sponsor-band" id="intl-partners" aria-labelledby="intl-partners-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">INTERNATIONAL PARTNERS</p>
        <h2 class="section-title" id="intl-partners-title">{{ tx('合作俱樂部', 'Partner clubs') }}</h2>
      </div>
      <p class="section-lede">{{ intlTiles.length ? tx('目前已取得標誌授權的合作俱樂部如下，更多合作內容持續更新中。', 'The partner clubs whose logos we are authorised to show are listed below, with more partnership content being added.') : tx('目前尚無可公開的海外合作俱樂部，相關進展將更新於本頁。', 'There are no overseas partner clubs that can be made public yet. Updates will be posted on this page.') }}</p>
    </div>
    <div v-if="intlTiles.length" class="sponsor-grid" style="grid-template-columns:repeat(auto-fit,minmax(200px,1fr))">
      <PartnerLogoTile v-for="t in intlTiles" :key="t.key" :name="t.name" :logo-url="tileSrc(t)" :href="t.href" external />
    </div>
  </div>
</section>

<section class="band paper-2-band" id="scouting" aria-labelledby="scouting-title">
  <div class="band-inner container">
    <div class="grid grid--2">
      <div class="prose">
        <h2 id="scouting-title">{{ tx('試訓球探 Trials & Scouting', 'Trials & Scouting') }}</h2>
        <p>{{ tx('透過一線隊賽事表現與訓練紀錄，尋找具備海外試訓潛力的球員。', 'We look for players with the potential to trial overseas, based on First Team match performances and training records.') }}</p>

      </div>
      <div class="prose">
        <h2>{{ tx('海外俱樂部媒合 Finding Clubs Abroad', 'Finding Clubs Abroad') }}</h2>
        <p>{{ tx('提供有意海外發展的球員諮詢服務，協助釐清方向與所需準備。', 'We offer a consultation service for players interested in developing overseas, helping to clarify direction and the preparation needed.') }}</p>
        <a class="btn btn--primary" :href="lp('/zh/join/international-player/')">{{ tx('預約海外媒合諮詢', 'Book an overseas club-matching consultation') }}</a>
      </div>
    </div>
  </div>
</section>
</template>

<style>
/* 藍鯨無對應照片時的頁首佔位漸層（BW-C1 新增，沿用 academy/pathway.vue 等既有樣式） */
.page-hero__bg--pending{ background:linear-gradient(160deg, var(--ink) 0%, var(--brand-deep) 100%); }

/* 3.4 國際發展通道 — pathway-flow / region-tabs / hk-player-note
   .sponsor-grid、.sponsor-tile 為既有共用 CSS，此頁直接沿用未新增 */
.pathway-flow{
  list-style:none; display:flex; align-items:center; gap:1rem; flex-wrap:wrap; margin-top:2rem;
}
.pathway-step{
  background:var(--paper-2); border:1px solid var(--rule); padding:1.5rem 1.75rem;
  min-width:150px; text-align:center; flex:1 1 150px;
}
.pathway-step__num{ font-size:.68rem; font-weight:800; color:var(--brand-aa); letter-spacing:.08em; }
.pathway-step__zh{ font-size:1.1rem; font-weight:900; color:var(--heading); margin-top:.4rem; }
.pathway-step__en{ font-size:.72rem; font-weight:700; color:var(--muted); text-transform:uppercase; letter-spacing:.04em; margin-top:.15rem; }
.pathway-step__arrow{ font-size:1.5rem; font-weight:900; color:var(--ghost); flex:0 0 auto; }
@media (max-width:760px){
  .pathway-flow{ flex-direction:column; align-items:stretch; }
  .pathway-step__arrow{ text-align:center; transform:rotate(90deg); }
}

.regions-band{ color:#fff; padding-block:clamp(4rem,7vw,6.5rem); }
.regions-band .section-title{ color:#fff; }
.region-tabs{ display:flex; gap:.6rem; flex-wrap:wrap; margin:2rem 0 2.25rem; }
.region-tab{
  min-height:44px; padding:0 1.4rem; font-weight:800; font-size:.85rem;
  border:2px solid rgba(255,255,255,.18); color:var(--muted-dark); background:transparent;
  transition:all var(--dur-fast) var(--ease);
}
.region-tab:hover{ border-color:rgba(255,255,255,.45); color:#fff; }
.region-tab[aria-selected="true"]{ background:var(--brand-aa); border-color:var(--brand-aa); color:#fff; }
.region-panel[hidden]{ display:none; }
.region-panel__desc{ color:var(--muted-dark); max-width:60ch; margin-bottom:1.5rem; }
.region-panel .pending{ background:rgba(255,255,255,.06); border-color:rgba(255,255,255,.35); color:var(--muted-dark); }

.region-partners{ display:grid; grid-template-columns:repeat(auto-fit,minmax(180px,1fr)); gap:1.25rem; margin-bottom:1.5rem; }
.region-partner-tile{
  background:rgba(255,255,255,.04); border:1px solid rgba(255,255,255,.1);
  padding:1.5rem; display:flex; flex-direction:column; align-items:center; gap:.85rem; text-align:center;
}
.region-partner-tile img{ height:64px; width:auto; max-width:100%; object-fit:contain; filter:grayscale(1) brightness(1.6); }
.region-partner-tile span{ font-size:.82rem; font-weight:700; color:#fff; line-height:1.5; }
.region-partner-tile small{ font-weight:600; color:var(--muted-dark); }

.hk-player-note{
  display:flex; gap:1.25rem; align-items:flex-start; background:var(--ink-2);
  padding:1.5rem 1.75rem; margin-bottom:1.5rem;
}
.hk-player-note__visual{
  position:relative; width:64px; height:64px; flex:none; background:var(--ink);
  display:flex; align-items:center; justify-content:center;
}
.hk-player-note__visual img{ position:absolute; inset:0; margin:auto; width:60%; height:60%; opacity:.15; }
.hk-player-note__visual span{ position:relative; font-size:1.5rem; font-weight:900; color:var(--brand-bright); }
.hk-player-note__name{ font-weight:800; color:#fff; display:flex; align-items:center; gap:.6rem; }
.hk-player-note__desc{ font-size:.88rem; color:var(--muted-dark); margin-top:.4rem; }
.hk-player-note a{ display:inline-block; margin-top:.6rem; font-size:.82rem; font-weight:700; color:var(--brand-bright); }
</style>

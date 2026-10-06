<script setup lang="ts">
// app/pages/zh/womens/index.vue — 由 site/src/pages/zh/womens/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class 大致不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
//
// S1-16（2026-09-29）：規劃書 §3.6「單元定位」——本頁是台中藍鯨女足官網的入口頁，
// 藍鯨球隊資料建於本資料庫（`club_id=TCBW`），但名單／賽程／積分榜一律由藍鯨官網
// 呈現，本頁不重複建置。本輪把原本留白／寫死的兩處補上：
//   1. ②介紹文與事實面板改讀 `club-copy.ts`／`site-facts.ts` 既有的已核實藍鯨事實，
//      不再是空白段落或籠統占位字串（原檔 <h2> 底下沒有任何文字）。
//   2. 不放藍鯨賽果：規劃書 §3.6「不含功能」明文排除「藍鯨賽程與比賽結果」，
//      一律由藍鯨官網呈現（見 docs/18-work-errors.md E-67）。
//   3. ④外連藍鯨官網的按鈕原本寫死舊站網址 `https://www.tcbw2014.com/`——那是
//      「既有 Google Sites 站台」，藍鯨規劃書 §1.4／主站規劃書 §3.6 明文「新站上線後
//      301 轉址」，不是本頁的永久連結目標。
//
// S1-12d 收尾第二輪（2026-09-29）：④外連按鈕改讀後端 site-facts 公開端點的
// `blueWhaleSiteUrl`（後台 `I` 網站設定可維護，見 apps/api/README.md「S1-12d」節
// 「後續補完」）——單一維護處改到後台後，藍鯨正式網域定案只需要後台改一個值，
// 不必再改環境變數或重新部署容器。API 打不到或這個欄位尚未設定（`null`／空字串）時，
// 退回既有 `nuxt.config.ts` 的 `runtimeConfig.public.blueWhaleSiteUrl`（staging 網域
// `https://bw-stg.tcrfc.tw` 預設值，見 apps/web/README.md 環境變數表），維持原本的
// 「staging 預設值＋容器啟動時可覆寫」降級行為，不顯示空連結。
//
// 本頁只會在 tcrfc 容器渲染（bw 容器對單元 '06' 全站 404，見
// shared/utils/units.ts BLUE_WHALE_DISABLED_UNITS），因此下面所有藍鯨資料一律
// 明確帶 club='bw'，不是讀 config.public.club（那永遠是 'tcrfc'）。
definePageMeta({ nav: 'womens', unit: '06', enReady: true })

const { lp, isEn, tx } = useLocale()
const config = useRuntimeConfig()

const bwAssets = getClubAssets('bw')
// S1-12d 收尾：事實面板改讀 useSiteFacts('bw')（後端公開端點），不再是
// shared/utils/site-facts.ts 的靜態快照——見 apps/api/README.md「S1-12d」節。
// 本頁只會在 tcrfc 容器渲染（見上方檔頭說明），底部 CTA 卡另外需要磐石自己的
// 梯隊代碼，故 tcrfc／bw 兩邊各呼叫一次；`facts: tcrfcFacts` 同一次呼叫多取一個值，
// 不新增 fetch，供下方 blueWhaleSiteUrl 使用（S1-12d 收尾第二輪）。
const { facts: bwFacts } = useSiteFacts('bw')
const { academyLabel: tcrfcAcademyLabel, facts: tcrfcFacts } = useSiteFacts('tcrfc')

// S1-12d 收尾第二輪：後端欄位為 null（尚未設定）或 API 打不到（降級快照裡固定是
// null，見 shared/utils/site-facts.ts）時，退回既有環境變數預設值，不顯示空連結。
const blueWhaleSiteUrl = computed(
  () => tcrfcFacts.value.blueWhaleSiteUrl || config.public.blueWhaleSiteUrl,
)

// 稽核 B-1：06 藍鯨官網入口的簡介段改讀頁面管理 `womens`（規劃書 B1「06 藍鯨官網入口頁」）；有已發布區塊用 CMS，否則維持寫死簡介。
// 事實面板（單一來源 site-facts，GEO-03）與前往官網按鈕不屬頁面管理，一律保留。
const cms = await useCmsPage('womens')
cms.applySeo({
  title: computed(() => (isEn.value ? CLUB_WOMENS_SEO_EN.title : '女子足球 Women\'s Football｜台中磐石足球俱樂部')),
  description: computed(() => (isEn.value
    ? CLUB_WOMENS_SEO_EN.description
    : '台中藍鯨女子隊的介紹與官網入口。完整球員名單、教練陣容、賽程與成績請至台中藍鯨官方網站。')),
})

// 英文版：藍鯨的成立日期、聯賽、場地、梯隊體系都由 facts 取值；facts 沒有英文值時
// 聯賽用對照表的 Taiwan Mulan Football League、場地維持中文原名（不自創譯名）。
const bwFoundedText = computed(() => (isEn.value
  ? clubFoundedLabelEn(bwFacts.value.foundingDateIso, bwFacts.value.foundedYear)
  : bwFacts.value.foundedDisplayZh))
const bwLeagueText = computed(() => (isEn.value
  ? (bwFacts.value.league.nameEn ?? CLUB_WOMENS_LEAGUE_EN)
  : bwFacts.value.league.nameZh))
const bwVenueText = computed(() => (isEn.value
  ? bwFacts.value.venues.map((v) => v.nameEn ?? v.nameZh).join(', ')
  : bwFacts.value.venues.map((v) => v.nameZh).join('、')))
const bwSquadText = computed(() => (isEn.value
  ? clubWomensSquadStructureEn(bwFacts.value.squadCodes)
  : bwFacts.value.squadStructureZh))
const academySquadsEn = computed(() => tcrfcFacts.value.squadCodes.join('/'))
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li aria-current="page">{{ tx('女子足球', "Women's Football") }}</li>
    </ol>
  </div>
</nav>

<!-- ① 主視覺與標題 -->
<section class="page-hero">
  <span class="ghost-num" aria-hidden="true" style="left:var(--edge);bottom:-1.8rem;color:rgba(255,255,255,.06);">06</span>
  <div class="container">
    <p class="page-hero__eyebrow">06 Women's Football</p>
    <h1>{{ tx('女子足球', "Women's Football") }}<span v-if="!isEn" class="en">Women's Football</span></h1>
    <p class="page-hero__lede">{{ tx('台中藍鯨女子隊是台中磐石支持的女子足球隊伍。球員名單、賽程與成績等詳細資訊，請至女足官方網站查詢。', CLUB_WOMENS_PAGE_EN.lede) }}</p>
  </div>
</section>

<!-- ② 台中藍鯨女子隊介紹（文字節錄自 club-copy.ts OUR_STORY_BODY_BW，已核實既有文案，
     不新寫文案；事實面板改讀 site-facts.ts 單一事實來源，GEO-03） -->
<section class="band">
  <div class="container">
    <div v-if="cms.active.value" class="women-intro women-intro--cms">
      <ContentPageBlocks :blocks="cms.blocks.value" />
      <figure class="women-visual">
        <img :src="bwAssets.headerMark.src" width="140" height="138" loading="lazy" :alt="tx('台中藍鯨隊徽', CLUB_WOMENS_PAGE_EN.crestAlt)">
      </figure>
    </div>
    <div v-else class="women-intro">
      <div class="prose">
        <h2>{{ tx('台中藍鯨女子隊', CLUB_WOMENS_PAGE_EN.introHeading) }}</h2>
        <p>{{ isEn ? WOMENS_STORY_BODY_EN : OUR_STORY_BODY_BW }}</p>
      </div>
      <figure class="women-visual">
        <img :src="bwAssets.headerMark.src" width="140" height="138" loading="lazy" :alt="tx('台中藍鯨隊徽', CLUB_WOMENS_PAGE_EN.crestAlt)">
      </figure>
    </div>
    <LocaleFallbackNotice v-if="cms.hasZhFallback.value" partial />

    <div class="fact-panel" style="margin-top:2rem;max-width:520px;">
      <dl style="margin:0;">
        <dt>{{ tx('隊伍名稱', 'Team name') }}</dt>
        <dd>{{ tx('台中藍鯨女子足球隊', CLUB_WOMENS_PAGE_EN.teamName) }}</dd>
        <dt>{{ tx('成立', 'Founded') }}</dt>
        <dd>{{ bwFoundedText }}</dd>
        <dt>{{ tx('所屬聯賽', 'League') }}</dt>
        <dd>{{ bwLeagueText }}</dd>
        <dt>{{ tx('主場', 'Home grounds') }}</dt>
        <dd>{{ bwVenueText }}</dd>
        <dt>{{ tx('梯隊體系', 'Squad structure') }}</dt>
        <dd>{{ bwSquadText }}</dd>
        <dt>{{ tx('完整資訊', 'Full information') }}</dt>
        <dd>{{ tx('球員名單、教練陣容、賽程與成績請至台中藍鯨官方網站', CLUB_WOMENS_PAGE_EN.fullInfo) }}</dd>
      </dl>
    </div>
  </div>
</section>

<!-- ④ 前往女足官網按鈕 -->
<section class="band grain" aria-labelledby="women-official-title">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">TCBW</span>
  <div class="band-inner container" style="text-align:center;">
    <p class="kicker kicker--on-dark" style="justify-content:center;">OFFICIAL SITE</p>
    <h2 class="section-title" id="women-official-title" style="color:#fff;">{{ tx('完整名單、賽程與成績請至台中藍鯨官網', CLUB_WOMENS_PAGE_EN.officialHeading) }}</h2>
    <p class="section-lede on-dark" style="margin-inline:auto;">{{ tx('球員名單、教練陣容、賽程與比賽成績等資訊，皆由台中藍鯨官方網站呈現。', CLUB_WOMENS_PAGE_EN.officialLede) }}</p>
    <div class="hero__ctas" style="margin-top:2rem;justify-content:center;">
      <a class="btn btn--primary" :href="blueWhaleSiteUrl" target="_blank" rel="noopener">
        {{ tx('前往台中藍鯨官方網站', CLUB_WOMENS_PAGE_EN.officialCta) }}
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" aria-hidden="true" style="margin-left:.4em;vertical-align:-2px;"><path d="M7 17L17 7M9 7h8v8"/></svg>
        <span class="visually-hidden">{{ tx('（新分頁開啟）', '(opens in a new tab)') }}</span>
      </a>
    </div>
  </div>
</section>

<!-- ⑤ 底部 CTA -->
<section class="band cta-band" aria-labelledby="women-cta-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">JOIN TCRFC</p>
        <h2 class="section-title" id="women-cta-title" style="color:var(--heading);">{{ tx('更多方式參與台中磐石', 'More ways to get involved with Taichung Rock') }}</h2>
      </div>
    </div>
    <div class="cta-grid">
      <div class="cta-card clip-card" style="background:var(--paper-2);color:var(--heading);">
        <p class="cta-card__num" style="color:var(--brand-aa);">{{ tx('台中磐石學院', 'TCRFC Academy') }}</p>
        <p class="cta-card__title">{{ tx('加入足球學院', 'Join the Academy') }}</p>
        <p class="cta-card__desc" style="color:var(--muted);">{{ isEn ? `${academySquadsEn} squads, developing the next generation of players.` : `${tcrfcAcademyLabel()} 梯隊，培育下一代球員。` }}</p>
        <a class="btn btn--dark btn--sm" :href="lp('/zh/academy/join/')">{{ tx('了解更多', 'Learn more') }}</a>
      </div>
      <div class="cta-card clip-card" style="background:var(--paper-2);color:var(--heading);">
        <p class="cta-card__num" style="color:var(--brand-aa);">{{ tx('課程與活動', 'Programs') }}</p>
        <p class="cta-card__title">{{ tx('兒童足球訓練', "Children's Training") }}</p>
        <p class="cta-card__desc" style="color:var(--muted);">{{ tx('分齡分級課程，適合初次接觸足球的孩子。', 'Age-grouped and level-based classes, suited to children trying football for the first time.') }}</p>
        <a class="btn btn--dark btn--sm" :href="lp('/zh/programs/childrens-training/')">{{ tx('了解更多', 'Learn more') }}</a>
      </div>
      <div class="cta-card clip-card" style="background:var(--paper-2);color:var(--heading);">
        <p class="cta-card__num" style="color:var(--brand-aa);">{{ tx('聯絡我們', 'Contact us') }}</p>
        <p class="cta-card__title">{{ tx('有其他問題？', 'Other questions?') }}</p>
        <p class="cta-card__desc" style="color:var(--muted);">{{ tx('歡迎直接與台中磐石團隊聯繫。', 'Feel free to contact the Taichung Rock team directly.') }}</p>
        <a class="btn btn--primary btn--sm" :href="lp('/zh/join/general/')">{{ tx('聯絡我們', 'Contact us') }}</a>
      </div>
    </div>
  </div>
</section>
</template>

<style>
/* WOMEN'S FOOTBALL 頁面局部元件 */
.women-intro{ display:flex; gap:2rem; align-items:flex-start; flex-wrap:wrap; }
.women-intro .prose{ flex:1 1 320px; min-width:0; }
.women-intro--cms .pb{ flex:1 1 320px; min-width:0; }
.women-visual{ flex:none; margin:0; }
.women-visual img{ display:block; width:140px; height:138px; object-fit:contain; }
.fact-panel{ background:var(--paper-2); border-left:4px solid var(--brand-aa); padding:1.5rem 1.75rem; }
.fact-panel dt{ font-size:.72rem; font-weight:800; text-transform:uppercase; letter-spacing:.06em; color:var(--muted); margin-top:1rem; }
.fact-panel dt:first-child{ margin-top:0; }
.fact-panel dd{ font-size:.95rem; color:var(--heading); font-weight:600; margin-left:0; }
</style>

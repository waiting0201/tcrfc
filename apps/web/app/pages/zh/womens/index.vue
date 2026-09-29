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
//      301 轉址」，不是本頁的永久連結目標。藍鯨正式網域尚未定案（STATUS.md 阻塞
//      清單），改讀 `nuxt.config.ts` 的 `runtimeConfig.public.blueWhaleSiteUrl`
//      （預設 staging 網域 `https://bw-stg.tcrfc.tw`，比照 `NUXT_PUBLIC_SITE_URL`／
//      `NUXT_PUBLIC_SITE_NAME` 既有的「staging 預設值＋容器啟動時可覆寫」做法，
//      見 apps/web/README.md 環境變數表），正式網域定案後改 env 即可、不必動程式碼。
//
// 本頁只會在 tcrfc 容器渲染（bw 容器對單元 '06' 全站 404，見
// shared/utils/units.ts BLUE_WHALE_DISABLED_UNITS），因此下面所有藍鯨資料一律
// 明確帶 club='bw'，不是讀 config.public.club（那永遠是 'tcrfc'）。
definePageMeta({ nav: 'womens', unit: '06' })

const { lp } = useLocale()
const config = useRuntimeConfig()

const bwAssets = getClubAssets('bw')
const bwFacts = getSiteFacts('bw')

useSeoMeta({
  title: '女子足球 Women\'s Football｜台中磐石足球俱樂部',
  description:
    '台中藍鯨女子隊的介紹與官網入口。完整球員名單、教練陣容、賽程與成績請至台中藍鯨官方網站。',
})
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li aria-current="page">女子足球</li>
    </ol>
  </div>
</nav>

<!-- ① 主視覺與標題 -->
<section class="page-hero">
  <span class="ghost-num" aria-hidden="true" style="left:var(--edge);bottom:-1.8rem;color:rgba(255,255,255,.06);">06</span>
  <div class="container">
    <p class="page-hero__eyebrow">06 Women's Football</p>
    <h1>女子足球<span class="en">Women's Football</span></h1>
    <p class="page-hero__lede">台中藍鯨女子隊是台中磐石支持的女子足球隊伍。球員名單、賽程與成績等詳細資訊，請至女足官方網站查詢。</p>
  </div>
</section>

<!-- ② 台中藍鯨女子隊介紹（文字節錄自 club-copy.ts OUR_STORY_BODY_BW，已核實既有文案，
     不新寫文案；事實面板改讀 site-facts.ts 單一事實來源，GEO-03） -->
<section class="band">
  <div class="container">
    <div class="women-intro">
      <div class="prose">
        <h2>台中藍鯨女子隊</h2>
        <p>{{ OUR_STORY_BODY_BW }}</p>
      </div>
      <figure class="women-visual">
        <img :src="bwAssets.headerMark.src" width="140" height="138" loading="lazy" alt="台中藍鯨隊徽">
      </figure>
    </div>

    <div class="fact-panel" style="margin-top:2rem;max-width:520px;">
      <dl style="margin:0;">
        <dt>隊伍名稱</dt>
        <dd>台中藍鯨女子足球隊</dd>
        <dt>成立</dt>
        <dd>{{ bwFacts.foundedDisplayZh }}</dd>
        <dt>所屬聯賽</dt>
        <dd>{{ bwFacts.league.nameZh }}</dd>
        <dt>主場</dt>
        <dd>{{ bwFacts.venues.map((v) => v.nameZh).join('、') }}</dd>
        <dt>梯隊體系</dt>
        <dd>{{ bwFacts.squadStructureZh }}</dd>
        <dt>完整資訊</dt>
        <dd>球員名單、教練陣容、賽程與成績請至台中藍鯨官方網站</dd>
      </dl>
    </div>
  </div>
</section>

<!-- ④ 前往女足官網按鈕 -->
<section class="band grain" aria-labelledby="women-official-title">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">TCBW</span>
  <div class="band-inner container" style="text-align:center;">
    <p class="kicker kicker--on-dark" style="justify-content:center;">OFFICIAL SITE</p>
    <h2 class="section-title" id="women-official-title" style="color:#fff;">完整名單、賽程與成績請至台中藍鯨官網</h2>
    <p class="section-lede on-dark" style="margin-inline:auto;">球員名單、教練陣容、賽程與比賽成績等資訊，皆由台中藍鯨官方網站呈現。</p>
    <div class="hero__ctas" style="margin-top:2rem;justify-content:center;">
      <a class="btn btn--primary" :href="config.public.blueWhaleSiteUrl" target="_blank" rel="noopener">
        前往台中藍鯨官方網站
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" aria-hidden="true" style="margin-left:.4em;vertical-align:-2px;"><path d="M7 17L17 7M9 7h8v8"/></svg>
        <span class="visually-hidden">（新分頁開啟）</span>
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
        <h2 class="section-title" id="women-cta-title" style="color:var(--heading);">更多方式參與台中磐石</h2>
      </div>
    </div>
    <div class="cta-grid">
      <div class="cta-card clip-card" style="background:var(--paper-2);color:var(--heading);">
        <p class="cta-card__num" style="color:var(--brand-aa);">台中磐石學院</p>
        <p class="cta-card__title">加入足球學院</p>
        <p class="cta-card__desc" style="color:var(--muted);">{{ academyTeamCodesLabel('tcrfc') }} 梯隊，培育下一代球員。</p>
        <a class="btn btn--dark btn--sm" :href="lp('/zh/academy/join/')">了解更多</a>
      </div>
      <div class="cta-card clip-card" style="background:var(--paper-2);color:var(--heading);">
        <p class="cta-card__num" style="color:var(--brand-aa);">課程與活動</p>
        <p class="cta-card__title">兒童足球訓練</p>
        <p class="cta-card__desc" style="color:var(--muted);">分齡分級課程，適合初次接觸足球的孩子。</p>
        <a class="btn btn--dark btn--sm" :href="lp('/zh/programs/childrens-training/')">了解更多</a>
      </div>
      <div class="cta-card clip-card" style="background:var(--paper-2);color:var(--heading);">
        <p class="cta-card__num" style="color:var(--brand-aa);">聯絡我們</p>
        <p class="cta-card__title">有其他問題？</p>
        <p class="cta-card__desc" style="color:var(--muted);">歡迎直接與台中磐石團隊聯繫。</p>
        <a class="btn btn--primary btn--sm" :href="lp('/zh/join/general/')">聯絡我們</a>
      </div>
    </div>
  </div>
</section>
</template>

<style>
/* WOMEN'S FOOTBALL 頁面局部元件 */
.women-intro{ display:flex; gap:2rem; align-items:flex-start; flex-wrap:wrap; }
.women-intro .prose{ flex:1 1 320px; min-width:0; }
.women-visual{ flex:none; margin:0; }
.women-visual img{ display:block; width:140px; height:138px; object-fit:contain; }
.fact-panel{ background:var(--paper-2); border-left:4px solid var(--brand-aa); padding:1.5rem 1.75rem; }
.fact-panel dt{ font-size:.72rem; font-weight:800; text-transform:uppercase; letter-spacing:.06em; color:var(--muted); margin-top:1rem; }
.fact-panel dt:first-child{ margin-top:0; }
.fact-panel dd{ font-size:.95rem; color:var(--heading); font-weight:600; margin-left:0; }
</style>

<script setup lang="ts">
// app/pages/zh/club/opportunities/index.vue — 由 site/src/pages/zh/club/opportunities/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
//
// S2-8（2026-09-29）：修正既有品牌外洩缺口——本頁改動前固定呼叫 `useSiteFacts('tcrfc')`，
// 藍鯨容器（單元 '3.3' 本來就沒關閉）會直接顯示磐石的聯賽名稱與文案，是本輪盤點發現的
// 既有缺口（apps/web/README.md「S2-8」節）。改為依 `clubKey` 動態抓取，並把「加入」／
// 「外籍球員招募」兩段正文改為 `club-copy.ts` 工廠函式（`getJoinFirstTeamBody`／
// `getForeignPlayerBody`），依規劃書 §1.3「四項以外不得另行設計」維持本頁對藍鯨開放。
// 試訓場次表格兩俱樂部共用同一份通用空白狀態文字（無俱樂部專屬事實），不需要分支。
definePageMeta({ nav: 'club', unit: '3.3', enReady: true, enReadyBw: true })

const { lp, locale, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')

const { facts } = useSiteFacts(clubKey.value)
// P4 試訓場次（H 批）：未結束的公開場次，API 失敗＝空陣列
const { trials } = await usePublicTrials()
// 英文版：磐石用 `*En`，藍鯨用 `*EnBw`（club-copy-en-club.ts 檔頭）。
const hero = computed(() => (isEn.value ? (isTcrfc.value ? getPlayerOpportunitiesHeroEn() : getPlayerOpportunitiesHeroEnBw()) : getPlayerOpportunitiesHero(clubKey.value)))
const joinBody = computed(() => (isEn.value ? (isTcrfc.value ? getJoinFirstTeamBodyEn(facts.value) : getJoinFirstTeamBodyEnBw(facts.value)) : getJoinFirstTeamBody(clubKey.value, facts.value)))
const foreignBody = computed(() => (isEn.value ? (isTcrfc.value ? getForeignPlayerBodyEn(facts.value) : getForeignPlayerBodyEnBw(facts.value)) : getForeignPlayerBody(clubKey.value, facts.value)))

// 稽核 B-1：主內文改讀頁面管理（有已發布區塊用 CMS，否則維持下方寫死內容；hero／麵包屑／CTA 不變）
const cms = await useCmsPage('club/opportunities')
cms.applySeo({
  title: computed(() => (isEn.value ? (isTcrfc.value ? getPlayerOpportunitiesSeoEn() : getPlayerOpportunitiesSeoEnBw()) : getPlayerOpportunitiesSeo(clubKey.value)).title),
  description: computed(() => (isEn.value ? (isTcrfc.value ? getPlayerOpportunitiesSeoEn() : getPlayerOpportunitiesSeoEnBw()) : getPlayerOpportunitiesSeo(clubKey.value)).description),
})

// G-12 常見問題快捷區塊：trials 掛載點（db/seed FAQ_EMBED_SLOTS「試訓頁（3.3）」），
// 四個固定掛載點之一，前三個掛載點已在 S1-15／本輪陸續消費，理由見 useFaqEmbed.ts 檔頭。
const { faqs } = useFaqEmbed(config.public.club, 'trials', locale.value)
useFaqPageSchema(faqs)
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/club/')">{{ tx('俱樂部', 'Football Club') }}</a></li>
      <li aria-current="page">{{ tx('球員機會', 'Player Opportunities') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <ClubHeroBg :src="siteImg('/assets/img/trencin-04.jpg')" width="1920" height="1279" />
  <div class="container">
    <p class="page-hero__eyebrow">3.3 Player Opportunities</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<template v-if="cms.active.value">
<ContentCmsPageBand :blocks="cms.blocks.value" :label="tx('球員機會', 'Player Opportunities')" :zh-fallback="cms.hasZhFallback.value" />
<section class="band" aria-label="registration">
  <div class="container">
    <a class="btn btn--primary" :href="lp('/zh/join/player/')">{{ tx('填寫加入球隊報名表', 'Fill in the squad registration form') }}</a>
  </div>
</section>
</template>
<template v-else>
<section class="band" id="join" aria-labelledby="join-title">
  <div class="band-inner container">
    <div class="prose">
      <h2 id="join-title">{{ isEn ? (isTcrfc ? 'Join Taichung Rock' : CLUB_JOIN_HEADING_EN_BW) : (isTcrfc ? '加入台中磐石 Join TCRFC' : '加入台中藍鯨') }}</h2>
      <!-- GEO-03（S1-12d）：聯賽名稱為單一來源 site-facts.ts，不在此重複寫死字面值。 -->
      <p>{{ joinBody }}</p>
    </div>
    <a class="btn btn--primary" :href="lp('/zh/join/player/')" style="margin-top:1.5rem">{{ tx('填寫加入球隊報名表', 'Fill in the squad registration form') }}</a>
  </div>
</section>
</template>

<section class="band paper-2-band" id="trials" aria-labelledby="trials-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">TRIALS</p>
        <h2 class="section-title" id="trials-title">{{ tx('試訓場次', 'Trial sessions') }}</h2>
      </div>
      <p class="section-lede">{{ tx('日期、地點、對象、名額與報名截止，開放場次將公告於此。', 'Dates, venues, who each session is for, places available and registration deadlines. Open sessions will be announced here.') }}</p>
    </div>

    <!-- 試訓場次列表＋線上報名（P4，H 批）：資料來自 GET trials；沒有場次時元件自己顯示原本的「目前尚無公告中的試訓場次」。 -->
    <TrialSchedule :trials="trials" />
    <a class="btn btn--dark btn--sm" :href="lp('/zh/join/player/')" style="margin-top:1.5rem">{{ tx('登記試訓意願', 'Register your interest in a trial') }}</a>
  </div>
</section>

<LocaleFallbackNotice v-if="isEn && hasFallbackLocale(faqs)" partial />
<section id="trials-faq" class="band" aria-labelledby="trials-faq-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">FAQ</p>
        <h2 id="trials-faq-title" class="section-title">{{ tx('球員機會常見問題', 'Player Opportunities FAQ') }}</h2>
      </div>
      <a :href="lp('/zh/faq/')">{{ tx('查看全部常見問題 →', 'View all FAQs →') }}</a>
    </div>
    <p v-if="faqs.length === 0" class="is-pending" style="margin-top:1.5rem;">{{ tx('常見問題收錄中，稍後將於本頁公布。', 'FAQs are being compiled and will be published on this page soon.') }}</p>
    <dl v-else class="faq-embed-list">
      <div v-for="f in faqs" :key="f.id" class="faq-embed-item">
        <dt>{{ f.question }}</dt>
        <dd>{{ f.answer }}</dd>
      </div>
    </dl>
  </div>
</section>

<section class="band foreign-band" id="foreign-players" aria-labelledby="foreign-players-title" lang="en">
  <div class="band-inner container">
    <div class="prose">
      <p class="kicker">FOR INTERNATIONAL PLAYERS</p>
      <h2 id="foreign-players-title">Foreign Player Recruitment</h2>
      <p>{{ foreignBody }}</p>
    </div>
    <a class="btn btn--primary" :href="lp('/zh/join/international-player/')" style="margin-top:1.5rem">International Player Enquiry</a>
  </div>
</section>

<section class="band grain cta-band" id="opp-cta" aria-labelledby="opp-cta-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="opp-cta-title">{{ tx('相關頁面', 'Related pages') }}</h2>
    <div class="cta-grid" style="grid-template-columns:repeat(2,minmax(0,1fr))">
      <div class="cta-card">
        <p class="cta-card__num">3.1</p>
        <p class="cta-card__title">{{ tx('認識一線隊', 'Meet the First Team') }}</p>
        <p class="cta-card__desc">{{ tx('加入前，先了解一線隊陣容、教練團與賽程。', 'Before you join, get to know the First Team squad, the coaching staff and the fixtures.') }}</p>
        <a class="btn btn--primary" :href="lp('/zh/club/first-team/')">{{ tx('前往一線隊', 'Go to the First Team') }}</a>
      </div>
      <!-- BW-C1：3.4 已重開（見 shared/utils/units.ts 檔頭），移除既有的 isTcrfc 隱藏。 -->
      <div class="cta-card">
        <p class="cta-card__num">3.4</p>
        <p class="cta-card__title">{{ tx('國際發展通道', 'International Pathways') }}</p>
        <p class="cta-card__desc">
          <template v-if="isEn && isTcrfc">See how players can reach stages in Europe, Japan and Hong Kong through Taichung Rock.</template>
          <template v-else-if="isEn">{{ CLUB_OPPORTUNITIES_INTL_CARD_DESC_EN_BW }}</template>
          <template v-else-if="isTcrfc">了解球員如何透過台中磐石通往歐洲、日本、香港的舞台。</template>
          <template v-else>了解球員如何透過台中藍鯨旅外日本、中國。</template>
        </p>
        <a class="btn btn--primary" :href="lp('/zh/club/international-pathways/')">{{ tx('查看國際通道', 'View International Pathways') }}</a>
      </div>
    </div>
  </div>
</section>
</template>

<style>
/* 3.3 球員機會 — 試訓表格 pending 狀態、英文優先段落
   .trial-table 與 3.1 .sched-table 結構相近（H 批起表格本體移到 components/TrialSchedule.vue，樣式仍留在這裡），未來若試訓資料到位可考慮合併樣式收進共用 CSS */
.paper-2-band{ background:var(--paper-2); }
.table-wrap{ overflow-x:auto; }
.trial-table{ width:100%; min-width:640px; border-collapse:collapse; font-size:.86rem; }
.trial-table th{
  padding:.85rem 1rem; text-align:left; font-size:.68rem; font-weight:800;
  letter-spacing:.06em; text-transform:uppercase; color:var(--muted); background:var(--paper); border-bottom:1px solid var(--rule);
}
.trial-table__pending-row td{ padding:1.5rem 1rem; }

.foreign-band{ background:var(--ink); color:#fff; padding-block:clamp(4rem,7vw,6rem); }
.foreign-band h2{ color:#fff; font-size:var(--fs-h3); font-weight:900; }
.foreign-band .kicker{ color:var(--brand-bright); }
.foreign-band p{ color:var(--muted-dark); }
.foreign-band .pending{ background:rgba(255,255,255,.06); border-color:rgba(255,255,255,.35); color:var(--muted-dark); }

/* G-12 常見問題快捷區塊（S2-8 新增，樣式沿用 programs/childrens-training 既有慣例，
   之後若要收共用 CSS 可與該頁一併處理）。 */
.is-pending{ color:var(--muted); font-style:italic; }
.faq-embed-list{ margin-top:1.5rem; display:flex; flex-direction:column; gap:1.25rem; }
.faq-embed-item dt{ font-weight:800; color:var(--heading); }
.faq-embed-item dd{ margin:.4rem 0 0; color:var(--muted); font-size:.9rem; line-height:1.7; }
</style>

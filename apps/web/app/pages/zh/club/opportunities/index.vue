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
definePageMeta({ nav: 'club', unit: '3.3' })

const { lp, locale } = useLocale()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')

const { facts } = useSiteFacts(clubKey.value)
const hero = computed(() => getPlayerOpportunitiesHero(clubKey.value))
const joinBody = computed(() => getJoinFirstTeamBody(clubKey.value, facts.value))
const foreignBody = computed(() => getForeignPlayerBody(clubKey.value, facts.value))

useSeoMeta({
  title: computed(() => getPlayerOpportunitiesSeo(clubKey.value).title),
  description: computed(() => getPlayerOpportunitiesSeo(clubKey.value).description),
})

// G-12 常見問題快捷區塊：trials 掛載點（db/seed FAQ_EMBED_SLOTS「試訓頁（3.3）」），
// 四個固定掛載點之一，前三個掛載點已在 S1-15／本輪陸續消費，理由見 useFaqEmbed.ts 檔頭。
const { faqs } = useFaqEmbed(config.public.club, 'trials', locale.value)
useFaqPageSchema(faqs)
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/club/')">俱樂部</a></li>
      <li aria-current="page">球員機會</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <ClubHeroBg src="/assets/img/trencin-04.jpg" width="1920" height="1279" />
  <div class="container">
    <p class="page-hero__eyebrow">3.3 Player Opportunities</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<section class="band" id="join" aria-labelledby="join-title">
  <div class="band-inner container">
    <div class="prose">
      <h2 id="join-title">{{ isTcrfc ? '加入台中磐石 Join TCRFC' : '加入台中藍鯨' }}</h2>
      <!-- GEO-03（S1-12d）：聯賽名稱為單一來源 site-facts.ts，不在此重複寫死字面值。 -->
      <p>{{ joinBody }}</p>
    </div>
    <a class="btn btn--primary" :href="lp('/zh/join/player/')" style="margin-top:1.5rem">填寫加入球隊報名表</a>
  </div>
</section>

<section class="band paper-2-band" id="trials" aria-labelledby="trials-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">TRIALS</p>
        <h2 class="section-title" id="trials-title">試訓場次</h2>
      </div>
      <p class="section-lede">日期、地點、對象、名額與報名截止，開放場次將公告於此。</p>
    </div>

    <div class="table-wrap">
      <table class="trial-table">
        <caption class="visually-hidden">試訓場次列表</caption>
        <thead>
          <tr>
            <th scope="col">日期</th>
            <th scope="col">地點</th>
            <th scope="col">對象</th>
            <th scope="col">名額</th>
            <th scope="col">報名截止</th>
            <th scope="col">狀態</th>
          </tr>
        </thead>
        <tbody>
          <tr class="trial-table__pending-row">
            <td colspan="6">目前尚無公告中的試訓場次，請關注官方社群公告。</td>
          </tr>
        </tbody>
      </table>
    </div>
    <a class="btn btn--dark btn--sm" :href="lp('/zh/join/player/')" style="margin-top:1.5rem">登記試訓意願</a>
  </div>
</section>

<section id="trials-faq" class="band" aria-labelledby="trials-faq-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">FAQ</p>
        <h2 id="trials-faq-title" class="section-title">球員機會常見問題</h2>
      </div>
      <a :href="lp('/zh/faq/')">查看全部常見問題 →</a>
    </div>
    <p v-if="faqs.length === 0" class="is-pending" style="margin-top:1.5rem;">常見問題收錄中，稍後將於本頁公布。</p>
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
    <h2 class="visually-hidden" id="opp-cta-title">相關頁面</h2>
    <div class="cta-grid" style="grid-template-columns:repeat(2,minmax(0,1fr))">
      <div class="cta-card">
        <p class="cta-card__num">3.1</p>
        <p class="cta-card__title">認識一線隊</p>
        <p class="cta-card__desc">加入前，先了解一線隊陣容、教練團與賽程。</p>
        <a class="btn btn--primary" :href="lp('/zh/club/first-team/')">前往一線隊</a>
      </div>
      <!-- BW-C1：3.4 已重開（見 shared/utils/units.ts 檔頭），移除既有的 isTcrfc 隱藏。 -->
      <div class="cta-card">
        <p class="cta-card__num">3.4</p>
        <p class="cta-card__title">國際發展通道</p>
        <p class="cta-card__desc">
          <template v-if="isTcrfc">了解球員如何透過台中磐石通往歐洲、日本、香港的舞台。</template>
          <template v-else>了解球員如何透過台中藍鯨旅外日本、中國。</template>
        </p>
        <a class="btn btn--primary" :href="lp('/zh/club/international-pathways/')">查看國際通道</a>
      </div>
    </div>
  </div>
</section>
</template>

<style>
/* 3.3 球員機會 — 試訓表格 pending 狀態、英文優先段落
   .trial-table 與 3.1 .sched-table 結構相近，未來若試訓資料到位可考慮合併樣式收進共用 CSS */
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

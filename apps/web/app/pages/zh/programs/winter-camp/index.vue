<script setup lang="ts">
// app/pages/zh/programs/winter-camp/index.vue — 由 site/src/pages/zh/programs/winter-camp/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
//
// BW-C1（2026-09-29）：本頁對藍鯨重開（S2-10 曾以「藍鯨沒有對應的寒假營隊產品」為由
// 整頁 404，是誤用——理由同 programs/childrens-training/index.vue 檔頭）。
// `content/blue-whale/programs.md` 舊站內容盤點確實沒有找到對應的「冬令營」產品，
// 本頁對藍鯨內容維持誠實的空狀態（本頁磐石版原本就大量是「待公告」占位文字，
// 不需要大改），改讀 getWinterCampSeo()／getWinterCampHero()（club-copy.ts）。
//
// 本輪新增：讀真實 05 課程與活動公開 API 查詢 `program_type='winter_camp'`（同
// summer-camp/index.vue 既有做法）。現況 `programs` 表 0 筆種子資料，故本輪只做到
// 「接了 API、目前空清單」，既有「待公告」占位文字維持不變。
// 2026-10-02：磐石的「線上報名」接 P3（規劃書 §3.5 報名流程：選梯次→學員／家長資料→健康聲明→報名編號），
// 見 components/ProgramRegistration.vue。藍鯨的線上報名與收費是待確認事項（藍鯨規劃書 §10 第 8 點），不接。
// 目前沒有收得到報名的梯次時，CTA 維持原本的詢問表單路徑。
definePageMeta({ nav: 'programs', unit: '5.3', enReady: true, enReadyBw: true })

const { lp, locale, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')

useSeoMeta({
  title: computed(() => (isEn.value ? getWinterCampSeoEn(clubKey.value) : getWinterCampSeo(clubKey.value)).title),
  description: computed(() => (isEn.value ? getWinterCampSeoEn(clubKey.value) : getWinterCampSeo(clubKey.value)).description),
})
const hero = computed(() => (isEn.value ? getWinterCampHeroEn(clubKey.value) : getWinterCampHero(clubKey.value)))

// B-8：該類型全部已發布課程（內容、教練團、合作夥伴、封面、梯次），不再只取第一個。
const { programs, first: programDetail } = await useProgramsOfType('winter_camp')
const hasPartners = computed(() => programs.value.some((p) => p.partners.length > 0))
/** 每個課程各取第一個開放中（見 utils/program-session.ts）的梯次呈現早鳥價與名額；沒有任何課程時放一張「待公告」卡。 */
const sessionCards = computed(() => {
  const cards = programs.value.map((p) => ({
    key: p.id,
    name: programs.value.length > 1 ? p.name : null,
    session: p.sessions.find((x) => isSessionRegistrable(x)) ?? null,
  }))
  return cards.length ? cards : [{ key: 'none', name: null, session: null }]
})

const { programs: registrablePrograms, hasRegistrable } = await useRegistrablePrograms('winter_camp', { enabled: isTcrfc.value })

// G-12 常見問題快捷區塊：program_detail 掛載點，理由同 childrens-training/index.vue。
const { faqs } = useFaqEmbed(config.public.club, 'program_detail', locale.value)
useFaqPageSchema(faqs)

// Course JSON-LD（GEO-05／§7 結構化資料型別清單，S1-20）。provider 固定為俱樂部本身
// （本頁對藍鯨已整頁 404，理由同上）。
const siteConfig = useSiteConfig()
useCourseSchema(
  computed(() => (programDetail.value
    ? {
        name: programDetail.value.name ?? null,
        intro: programDetail.value.intro ?? null,
        ageMin: programDetail.value.ageMin ?? null,
        ageMax: programDetail.value.ageMax ?? null,
      }
    : null)),
  { providerName: getClubAssets(config.public.club).nameZh, siteUrl: computed(() => siteConfig.url ?? '') },
)
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/programs/')">{{ tx('課程與活動', 'Programs') }}</a></li>
      <li aria-current="page">{{ tx('冬令營', 'Winter Camp') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num" aria-hidden="true" style="left:var(--edge);bottom:-1.5rem;color:rgba(255,255,255,.06);">5.3</span>
  <div class="container">
    <p class="page-hero__eyebrow">5.3</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<!-- B-8：課程內容、適合對象、年齡、教練團來自後台課程（P1）；沒有課程資料時沿用原本的占位文字 -->
<ProgramInfoBand v-if="programs.length" :programs="programs" :content-title="tx('適合對象與課程內容', 'Who It Is For and What We Cover')" band-style="background:var(--paper-2);" />
<section v-else class="band" style="background:var(--paper-2);">
  <div class="container">
    <div class="grid grid--2" style="align-items:start;">
      <div class="prose">
        <h2>{{ tx('適合對象與課程內容', 'Who It Is For and What We Cover') }}</h2>
        <p>{{ tx('適合對象與課程內容將於梯次公告時同步發布。', 'Who the camp is for and what it covers will be published together with the session announcement.') }}</p>
      </div>
      <div class="prose">
        <h2>{{ tx('教練團', 'Coaching Team') }}</h2>
        <p>{{ tx('教練團陣容將於梯次公告時同步發布。', 'The coaching team will be announced together with the session announcement.') }}</p>
      </div>
    </div>
  </div>
</section>

<section v-if="hasPartners" class="band">
  <div class="container">
    <div class="prose" style="margin-bottom:1.75rem;"><h2>{{ tx('合作夥伴', 'Partners') }}</h2></div>
    <ProgramPartnersList :programs="programs" />
  </div>
</section>

<section class="band" style="background:var(--paper-2);">
  <div class="container">
    <div class="grid grid--2" style="align-items:start;">
      <div class="prose">
        <h2>{{ tx('日期與地點', 'Dates and Venue') }}</h2>
        <p>{{ tx('確切日期與地點將於梯次公告時公布。', 'The exact dates and venue will be announced with the session announcement.') }}</p>
      </div>
      <div class="prose">
        <h2>{{ tx('報名（早鳥價／名額倒數）', 'Registration (Early-Bird Price / Places Left)') }}</h2>
        <p>{{ tx('報名方式與早鳥優惠將於開放報名時公布。', 'How to register and any early-bird offer will be announced when registration opens.') }}</p>
      </div>
    </div>

    <div v-for="c in sessionCards" :key="c.key" class="signup-preview">
      <div v-if="c.name" class="signup-preview__row"><span>{{ tx('課程', 'Program') }}</span><span class="signup-preview__value">{{ c.name }}</span></div>
      <div class="signup-preview__row">
        <span>{{ tx('早鳥價', 'Early-bird price') }}</span>
        <span class="signup-preview__value">{{ c.session?.earlyBirdPrice ? `NT$ ${c.session.earlyBirdPrice}` : tx('待公告', 'To be announced') }}</span>
      </div>
      <div class="signup-preview__row">
        <span>{{ tx('剩餘名額', 'Places left') }}</span>
        <span class="signup-preview__value">{{ c.session?.capacity ? Math.max(c.session.capacity - c.session.enrolledCount, 0) : tx('待公告', 'To be announced') }}</span>
      </div>
      <div class="signup-preview__row">
        <span>{{ tx('梯次', 'Session') }}</span>
        <span class="signup-preview__value">{{ c.session ? `${c.session.startOn}${tx(' ～ ', ' - ')}${c.session.endOn}` : tx('待公告', 'To be announced') }}</span>
      </div>
    </div>
  </div>
</section>

<LocaleFallbackNotice v-if="isEn && hasFallbackLocale(faqs)" partial />
<section class="band">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">FAQ</p>
        <h2 class="section-title">{{ tx('冬令營常見問題', 'Winter Camp FAQ') }}</h2>
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

<ProgramRegistration v-if="isTcrfc && hasRegistrable" :programs="registrablePrograms" />

<section class="band grain cta-band" aria-labelledby="wc-cta-title">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">5.3</span>
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker kicker--on-dark">WINTER CAMP</p>
        <h2 class="section-title" id="wc-cta-title">{{ tx('關注下一梯次冬令營', 'Follow the Next Winter Camp') }}</h2>
      </div>
    </div>
    <p class="section-lede">{{ tx('梯次公告後將於此頁與社群帳號同步發布，站內不接受金流付款。', 'Once sessions are announced they will be posted here and on our social accounts. Payment is not taken on this site.') }}</p>
    <div class="hero__ctas" style="margin-top:2rem;">
      <a v-if="isTcrfc && hasRegistrable" class="btn btn--primary" href="#register">{{ tx('線上報名', 'Register Online') }}</a>
      <a v-else-if="isTcrfc" class="btn btn--primary" :href="lp('/zh/join/camp-registration/')">{{ tx('加入候補通知', 'Join the Notification List') }}</a>
      <a class="btn btn--light" :href="lp('/zh/programs/')">{{ tx('回課程總覽', 'Back to Programs Overview') }}</a>
    </div>
  </div>
</section>
</template>

<style>
/* PROGRAMS 單元共用元件（5.1–5.5 共用，重複三頁以上，建議收進 tcrfc.css） */
.signup-preview{ margin-top:2rem; border:1px solid var(--rule); max-width:420px; }
.signup-preview__row{ display:flex; justify-content:space-between; padding:.9rem 1.25rem; font-size:.85rem; border-bottom:1px solid var(--rule); }
.signup-preview__row:last-child{ border-bottom:none; }
.signup-preview__value{ font-weight:700; color:var(--muted); }

/* S2-10 新增：真實梯次為空、常見問題為空時的通用提示文字，以及 G-12 快捷區塊
   （沿用 programs/summer-camp 既有慣例）。 */
.is-pending{ color:var(--muted); font-style:italic; }
.faq-embed-list{ margin-top:1.5rem; display:flex; flex-direction:column; gap:1.25rem; }
.faq-embed-item dt{ font-weight:800; color:var(--heading); }
.faq-embed-item dd{ margin:.4rem 0 0; color:var(--muted); font-size:.9rem; line-height:1.7; }
</style>

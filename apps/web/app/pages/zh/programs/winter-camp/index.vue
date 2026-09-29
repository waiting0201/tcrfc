<script setup lang="ts">
// app/pages/zh/programs/winter-camp/index.vue — 由 site/src/pages/zh/programs/winter-camp/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
//
// S2-10（2026-09-29）：本頁對藍鯨已整頁 404（units.ts BLUE_WHALE_DISABLED_UNITS 的
// '5.3'）——與 5.2 共用同一份資料模型／版型，`content/blue-whale/programs.md` 的舊站
// 內容盤點沒有對應的「寒假營隊」產品可換，故關閉，不需要俱樂部分支。理由見
// shared/utils/units.ts 檔頭與 apps/web/README.md「S2-10」節。
//
// 本輪新增：讀真實 05 課程與活動公開 API 查詢 `program_type='winter_camp'`（同
// summer-camp/index.vue 既有做法）。現況 `programs` 表 0 筆種子資料，故本輪只做到
// 「接了 API、目前空清單」，既有「待公告」占位文字維持不變。
definePageMeta({ nav: 'programs', unit: '5.3' })

const { lp, locale } = useLocale()
const config = useRuntimeConfig()

useSeoMeta({
  title: '冬令營 Winter Camp｜課程與活動｜台中磐石足球俱樂部',
  description:
    '台中磐石足球冬令營，與夏令營共用版型與資料模型。梯次日期、地點、費用與教練團資訊將於報名開放時公告。',
})

const { data: programList } = await useFetch(`/api/backend/${config.public.club}/programs`, {
  query: { type: 'winter_camp', pageSize: 5, lang: locale.value },
})
const firstProgram = computed(() => programList.value?.items?.[0] ?? null)
const { data: programDetail } = await useFetch(
  `/api/backend/${config.public.club}/programs/${firstProgram.value?.slug ?? ''}`,
  { query: { lang: locale.value }, immediate: !!firstProgram.value },
)
const openSession = computed(() =>
  (programDetail.value?.sessions ?? []).find((s) => s.status === 'open' || s.status === 'waitlist') ?? null,
)

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
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/programs/')">課程與活動</a></li>
      <li aria-current="page">冬令營</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num" aria-hidden="true" style="left:var(--edge);bottom:-1.5rem;color:rgba(255,255,255,.06);">5.3</span>
  <div class="container">
    <p class="page-hero__eyebrow">5.3 Programs</p>
    <h1>冬令營<span class="en">Winter Camp</span></h1>
    <p class="page-hero__lede">寒假期間的密集足球訓練營，版型與資料模型與夏令營相同。梯次日期與費用將於報名開放前公告。</p>
  </div>
</section>

<section class="band" style="background:var(--paper-2);">
  <div class="container">
    <div class="grid grid--2" style="align-items:start;">
      <div class="prose">
        <h2>適合對象與課程內容</h2>
        <p>適合對象與課程內容將於梯次公告時同步發布。</p>
      </div>
      <div class="prose">
        <h2>教練團</h2>
        <p>教練團陣容將於梯次公告時同步發布。</p>
      </div>
    </div>
  </div>
</section>

<section class="band" style="background:var(--paper-2);">
  <div class="container">
    <div class="grid grid--2" style="align-items:start;">
      <div class="prose">
        <h2>日期與地點</h2>
        <p>確切日期與地點將於梯次公告時公布。</p>
      </div>
      <div class="prose">
        <h2>報名（早鳥價／名額倒數）</h2>
        <p>報名方式與早鳥優惠將於開放報名時公布。</p>
      </div>
    </div>

    <div class="signup-preview">
      <div class="signup-preview__row">
        <span>早鳥價</span>
        <span class="signup-preview__value">{{ openSession?.earlyBirdPrice ? `NT$ ${openSession.earlyBirdPrice}` : '待公告' }}</span>
      </div>
      <div class="signup-preview__row">
        <span>剩餘名額</span>
        <span class="signup-preview__value">{{ openSession?.capacity ? Math.max(openSession.capacity - openSession.enrolledCount, 0) : '待公告' }}</span>
      </div>
      <div class="signup-preview__row">
        <span>梯次</span>
        <span class="signup-preview__value">{{ openSession ? `${openSession.startOn} ～ ${openSession.endOn}` : '待公告' }}</span>
      </div>
    </div>
  </div>
</section>

<section class="band">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">FAQ</p>
        <h2 class="section-title">冬令營常見問題</h2>
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

<section class="band grain cta-band" aria-labelledby="wc-cta-title">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">5.3</span>
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker kicker--on-dark">WINTER CAMP</p>
        <h2 class="section-title" id="wc-cta-title">關注下一梯次冬令營</h2>
      </div>
    </div>
    <p class="section-lede">梯次公告後將於此頁與社群帳號同步發布，站內不接受金流付款。</p>
    <div class="hero__ctas" style="margin-top:2rem;">
      <a class="btn btn--primary" :href="lp('/zh/join/camp-registration/')">加入候補通知</a>
      <a class="btn btn--light" :href="lp('/zh/programs/')">回課程總覽</a>
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

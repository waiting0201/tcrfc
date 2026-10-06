<script setup lang="ts">
// app/pages/zh/programs/school-community/index.vue — 由 site/src/pages/zh/programs/school-community/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
//
// S2-10（2026-09-29）：本頁對藍鯨維持開放（不同於 5.1–5.4）——
// `content/blue-whale/club-profile.md` §1「建教合作」欄有 5 校真實名單、
// `programs.md` §2／§4 有真實社區推廣（運動 i 台灣 2.0）與教練講習內容，與本頁
// 「合作學校列表／社區計畫／教練培訓」三個子區塊直接對應，改為讀
// `shared/utils/club-copy.ts` 的 `SCHOOL_PARTNERS_BW`／`COMMUNITY_PROGRAM_BODY_BW`／
// `COACH_TRAINING_BODY_BW`（逐字節錄舊站原文，紀律 11）。磐石版「合作學校列表」與
// 「社區計畫」「教練培訓」兩段既有內容本來就是空白（客戶尚未提供），維持原樣，
// 不臆造磐石的對應內容。
definePageMeta({ nav: 'programs', unit: '5.5', enReady: true, enReadyBw: true })

const { lp, locale, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')

const hero = computed(() => (isEn.value ? getSchoolCommunityHeroEn(clubKey.value) : getSchoolCommunityHero(clubKey.value)))

useSeoMeta({
  title: computed(() => (isEn.value ? getSchoolCommunitySeoEn(clubKey.value) : getSchoolCommunitySeo(clubKey.value)).title),
  description: computed(() => (isEn.value ? getSchoolCommunitySeoEn(clubKey.value) : getSchoolCommunitySeo(clubKey.value)).description),
})

const schoolPartners = computed(() => (isEn.value ? SCHOOL_PARTNERS_BW_EN : SCHOOL_PARTNERS_BW))
const clubNameEn = computed(() => (isTcrfc.value ? 'Taichung Rock FC' : BW_NAME_EN))
// B-8：全部已發布校園與社區課程的詳情（內容、教練團、合作夥伴、封面）。
const { programs, first: programDetail } = await useProgramsOfType('school_community')

// G-12 常見問題快捷區塊：program_detail 掛載點，理由同 childrens-training/index.vue。
const { faqs } = useFaqEmbed(config.public.club, 'program_detail', locale.value)
useFaqPageSchema(faqs)

// Course JSON-LD（GEO-05／§7 結構化資料型別清單，S1-20）。provider 固定為俱樂部本身，
// 兩俱樂部皆開放，故用 clubKey 動態取得名稱。資料不足（現況：programs 表 0 筆種子
// 資料）時不輸出，見 shared/utils/schema-batch2.ts。
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
  { providerName: getClubAssets(clubKey.value).nameZh, siteUrl: computed(() => siteConfig.url ?? '') },
)
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/programs/')">{{ tx('課程與活動', 'Programs') }}</a></li>
      <li aria-current="page">{{ tx('校園與社區', 'School & Community') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num" aria-hidden="true" style="left:var(--edge);bottom:-1.5rem;color:rgba(255,255,255,.06);">5.5</span>
  <div class="container">
    <p class="page-hero__eyebrow">5.5 Programs</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<!-- B-8：後台課程（P1）的內容、適合對象、年齡、教練團、合作夥伴；有課程資料才顯示 -->
<ProgramInfoBand v-if="programs.length" :programs="programs" :content-title="tx('課程介紹', 'About the Programs')" show-partners />

<section class="band">
  <div class="container">
    <div class="grid grid--3">
      <div class="prose">
        <h2>{{ tx('校園合作方案', 'School Programs') }}</h2>
        <p v-if="!isTcrfc">{{ tx('建教合作學校名單見下方「合作學校列表」。', 'See the "Partner Schools" list below for our industry-academia partner schools.') }}</p>
        <p v-else class="is-pending">{{ tx('內容準備中，稍後將於本頁公布。', 'Content is being prepared and will be published on this page soon.') }}</p>
      </div>
      <div class="prose">
        <h2>{{ tx('社區計畫', 'Community Projects') }}</h2>
        <p v-if="!isTcrfc">{{ isEn ? COMMUNITY_PROGRAM_BODY_BW_EN : COMMUNITY_PROGRAM_BODY_BW }}</p>
        <p v-else class="is-pending">{{ tx('內容準備中，稍後將於本頁公布。', 'Content is being prepared and will be published on this page soon.') }}</p>
      </div>
      <div class="prose">
        <h2>{{ tx('教練培訓', 'Coach Education') }}</h2>
        <p v-if="!isTcrfc">{{ isEn ? COACH_TRAINING_BODY_BW_EN : COACH_TRAINING_BODY_BW }}</p>
        <p v-else class="is-pending">{{ tx('內容準備中，稍後將於本頁公布。', 'Content is being prepared and will be published on this page soon.') }}</p>
      </div>
    </div>
  </div>
</section>

<section class="band" style="background:var(--paper-2);" aria-labelledby="sch-list-title">
  <div class="container">
    <div class="prose" style="margin-bottom:1.5rem;">
      <h2 id="sch-list-title">{{ tx('合作學校列表', 'Partner Schools') }}</h2>
      <p v-if="!isTcrfc">{{ tx('台中藍鯨既有建教合作學校（女子足球隊）如下：', `The existing industry-academia partner schools (women's football teams) of ${BW_NAME_EN} are listed below:`) }}</p>
      <p v-else>{{ tx('合作學校名單將於客戶確認後公告於此處。', 'The list of partner schools will be published here once confirmed.') }}</p>
    </div>
    <div class="table-wrap">
      <table class="data-table">
        <thead>
          <tr><th scope="col">{{ tx('學校／單位', 'School / organisation') }}</th><th scope="col">{{ tx('合作內容', 'Collaboration') }}</th><th scope="col">{{ tx('合作年度', 'Year') }}</th></tr>
        </thead>
        <tbody v-if="!isTcrfc">
          <tr v-for="s in schoolPartners" :key="s.nameZh">
            <td>{{ s.nameZh }}</td>
            <td>{{ s.contentZh }}</td>
            <td :class="{ 'is-pending': !s.yearZh }">{{ s.yearZh ?? tx('未標明年度', SCHOOL_PARTNER_NO_YEAR_EN) }}</td>
          </tr>
        </tbody>
        <tbody v-else>
          <tr></tr>
          <tr></tr>
          <tr></tr>
        </tbody>
      </table>
    </div>
  </div>
</section>

<LocaleFallbackNotice v-if="isEn && hasFallbackLocale(faqs)" partial />
<section class="band">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">FAQ</p>
        <h2 class="section-title">{{ tx('校園與社區常見問題', 'School & Community FAQ') }}</h2>
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

<section class="band" aria-labelledby="sch-form-title">
  <div class="container">
    <div class="prose" style="margin-bottom:1.75rem;max-width:60ch;">
      <h2 id="sch-form-title">{{ tx('洽談合作', 'Partnership Enquiries') }}</h2>
      <p>{{ tx(`學校或社區單位如有合作意願，歡迎填寫以下表單，${getClubAssets(clubKey).shortNameZh}團隊將盡快與您聯繫。`, `If your school or community organisation would like to work with us, please fill in the form below and the ${clubNameEn} team will be in touch as soon as possible.`) }}</p>
    </div>

    <form class="inquiry-form" onsubmit="return false;">
      <div class="field">
        <label for="sch-org">{{ tx('學校／單位名稱', 'School / organisation name') }} <span aria-hidden="true">*</span></label>
        <input type="text" id="sch-org" name="org" autocomplete="organization" required>
      </div>
      <div class="field">
        <label for="sch-contact">{{ tx('聯絡人姓名', 'Contact name') }} <span aria-hidden="true">*</span></label>
        <input type="text" id="sch-contact" name="contact" autocomplete="name" required>
      </div>
      <div class="field">
        <label for="sch-phone">{{ tx('聯絡電話', 'Phone') }} <span aria-hidden="true">*</span></label>
        <input type="tel" id="sch-phone" name="phone" autocomplete="tel" required>
      </div>
      <div class="field">
        <label for="sch-email">Email <span aria-hidden="true">*</span></label>
        <input type="email" id="sch-email" name="email" autocomplete="email" required>
      </div>
      <div class="field field--full">
        <label for="sch-type">{{ tx('合作類型', 'Type of partnership') }}</label>
        <select id="sch-type" name="type">
          <option value="">{{ tx('請選擇', 'Please select') }}</option>
          <option value="school">{{ tx('校園合作方案', 'School program') }}</option>
          <option value="community">{{ tx('社區計畫', 'Community project') }}</option>
          <option value="coach-training">{{ tx('教練培訓', 'Coach education') }}</option>
          <option value="other">{{ tx('其他', 'Other') }}</option>
        </select>
      </div>
      <div class="field field--full">
        <label for="sch-message">{{ tx('洽談需求說明', 'What you have in mind') }}</label>
        <textarea id="sch-message" name="message" rows="5"></textarea>
      </div>
      <div class="field field--full">
        <button class="btn btn--primary" type="submit">{{ tx('送出洽談需求', 'Send Enquiry') }}</button>
      </div>
    </form>
  </div>
</section>

<section class="band grain cta-band" aria-labelledby="sch-cta-title">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">5.5</span>
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker kicker--on-dark">PARTNER WITH US</p>
        <h2 class="section-title" id="sch-cta-title">{{ tx(`與${getClubAssets(clubKey).shortNameZh}一起推廣足球`, `Promote Football with ${clubNameEn}`) }}</h2>
      </div>
    </div>
    <div class="hero__ctas" style="margin-top:1.5rem;">
      <a class="btn btn--light" :href="lp('/zh/programs/')">{{ tx('回課程總覽', 'Back to Programs Overview') }}</a>
      <a class="btn btn--light" :href="lp('/zh/partners/')">{{ tx('合作夥伴與贊助', 'Partners & Sponsors') }}</a>
    </div>
  </div>
</section>
</template>

<style>
/* PROGRAMS 單元共用元件（5.1–5.5 共用，重複三頁以上，建議收進 tcrfc.css） */
.table-wrap{ overflow-x:auto; }
.data-table{ width:100%; border-collapse:collapse; min-width:560px; }
.data-table th, .data-table td{ padding:.85rem 1rem; text-align:left; border-bottom:1px solid var(--rule); font-size:.85rem; }
.data-table thead th{ background:var(--ink); color:#fff; font-weight:700; letter-spacing:.03em; }
.data-table td.is-pending{ color:var(--muted); font-style:italic; }

/* S2-10 新增：一般性 pending 提示（非表格內）與 G-12 常見問題快捷區塊
   （沿用 programs 系列既有慣例）。 */
.is-pending{ color:var(--muted); font-style:italic; }
.faq-embed-list{ margin-top:1.5rem; display:flex; flex-direction:column; gap:1.25rem; }
.faq-embed-item dt{ font-weight:800; color:var(--heading); }
.faq-embed-item dd{ margin:.4rem 0 0; color:var(--muted); font-size:.9rem; line-height:1.7; }

/* 洽談表單（介面結構，非客戶內容） */
.inquiry-form{ background:var(--paper-2); padding:clamp(1.75rem,4vw,2.75rem); display:grid; gap:1.25rem; grid-template-columns:repeat(2,1fr); }
.inquiry-form .field{ display:flex; flex-direction:column; gap:.4rem; }
.inquiry-form .field--full{ grid-column:1/-1; }
.inquiry-form label{ font-size:.8rem; font-weight:700; color:var(--heading); }
.inquiry-form input, .inquiry-form select, .inquiry-form textarea{
  border:1px solid var(--rule); background:#fff; padding:.75rem .9rem; font:inherit; color:var(--text);
}
.inquiry-form input:focus, .inquiry-form select:focus, .inquiry-form textarea:focus{ outline:2px solid var(--brand-aa); outline-offset:1px; }
.inquiry-form textarea{ resize:vertical; min-height:120px; }
@media (max-width:640px){ .inquiry-form{ grid-template-columns:1fr; } }
</style>

<script setup lang="ts">
// app/pages/zh/programs/summer-camp/index.vue — 由 site/src/pages/zh/programs/summer-camp/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
//
// BW-C1（2026-09-29）：本頁對藍鯨重開（S1-15 曾以「藍鯨『05 推廣活動』是完全不同的
// 活動集合」為由整頁 404，是誤用——理由同 programs/childrens-training/index.vue 檔頭）。
// `content/blue-whale/programs.md` 舊站內容盤點沒有找到對應的「夏令營」產品（不同於
// 5.1 兒童足球訓練有真實對應內容），本頁對藍鯨內容改為誠實的空狀態，不臆造，改讀
// getSummerCampSeo()／getSummerCampHero()（club-copy.ts）。TCRFC 專屬的合作夥伴
// 照片（Rot-Weiss Ahlen）與往年花絮照片對藍鯨隱藏。CTA 不連到磐石專屬的
// `/zh/join/camp-registration/`，改導向已開放的聯絡頁面。
//
// 本輪新增：讀真實 05 課程與活動公開 API 查詢 `program_type='summer_camp'`
// （值域來源同 childrens-training/index.vue 檔頭說明）。現況「早鳥價／剩餘名額／梯次」
// 三個既有的靜態示意值（`signup-preview`）改為有真實梯次時顯示真實數字，
// 沒有梯次（現況：`programs` 表 0 筆種子資料）時維持既有「待公告」，不臆造金額。
definePageMeta({ nav: 'programs', unit: '5.2' })

const { lp, locale } = useLocale()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')

useSeoMeta({
  title: computed(() => getSummerCampSeo(clubKey.value).title),
  description: computed(() => getSummerCampSeo(clubKey.value).description),
})
const hero = computed(() => getSummerCampHero(clubKey.value))

const { data: programList } = await useFetch(`/api/backend/${config.public.club}/programs`, {
  query: { type: 'summer_camp', pageSize: 5, lang: locale.value },
})
const firstProgram = computed(() => programList.value?.items?.[0] ?? null)
const { data: programDetail } = await useFetch(
  `/api/backend/${config.public.club}/programs/${firstProgram.value?.slug ?? ''}`,
  { query: { lang: locale.value }, immediate: !!firstProgram.value },
)
/** 只取第一個開放中（狀態為 open／waitlist）的梯次呈現早鳥價與名額——本頁是單一課程
 * 項目的行銷頁，不是梯次列表頁，多梯次的完整選擇留給報名流程本身（10.2／5.x 報名
 * 表單，不在本輪範圍）。 */
const openSession = computed(() =>
  (programDetail.value?.sessions ?? []).find((s) => s.status === 'open' || s.status === 'waitlist') ?? null,
)

// G-12 常見問題快捷區塊：program_detail 掛載點，理由同 childrens-training/index.vue。
const { faqs } = useFaqEmbed(config.public.club, 'program_detail', locale.value)

// GEO-06（S1-18a）：G-12 嵌入區塊與 12 FAQ 獨立單元同條規定「一律輸出 FAQPage」，
// 沿用同一份資料（faqs）餵給畫面與結構化資料，不另外重打一次 API。
useFaqPageSchema(faqs)

// Course JSON-LD（GEO-05／§7 結構化資料型別清單，S1-20）。provider 固定為俱樂部本身
// （本頁對藍鯨已整頁 404，理由同 childrens-training/index.vue）。資料不足（現況：
// programs 表 0 筆種子資料，programDetail 為 null）時不輸出，見
// shared/utils/schema-batch2.ts。
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
      <li aria-current="page">夏令營</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <!-- 藍鯨目前沒有對應的夏令營活動，不沿用磐石照片頂替 -->
  <img v-if="isTcrfc" class="page-hero__bg" :src="siteImg('/assets/img/programs/summer-camp-05.jpg')" alt="" width="1600" height="1200">
  <div v-else class="page-hero__bg page-hero__bg--pending" aria-hidden="true" />
  <div class="container">
    <p class="page-hero__eyebrow">{{ isTcrfc ? '5.2 Programs' : '5.2' }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<section v-if="isTcrfc" class="band">
  <div class="container">
    <div class="grid grid--2" style="align-items:start;">
      <div class="prose">
        <h2>適合對象與課程內容</h2>

      </div>
      <div class="prose">
        <h2>教練團</h2>

      </div>
    </div>
  </div>
</section>

<section v-if="isTcrfc" class="band" style="background:var(--paper-2);">
  <div class="container">
    <div class="prose" style="margin-bottom:1.75rem;">
      <h2>合作夥伴</h2>
      <p>客戶收件夾內的歷史資料顯示台中磐石曾與以下單位合作，實際是否為夏令營之固定合作夥伴待客戶確認：</p>
    </div>
    <div class="grid grid--2">
      <figure class="clip-card" style="margin:0;">
        <img :src="siteImg('/assets/img/programs/summer-camp-02.jpg')" loading="lazy" width="1600" height="1200" alt="台中磐石與德國 Rot Weiss Ahlen 足球俱樂部代表於簽約儀式上握手，背板印有雙方隊徽與合作備忘錄字樣">

      </figure>

    </div>
  </div>
</section>

<section class="band">
  <div class="container">
    <template v-if="isTcrfc">
      <div class="grid grid--2" style="align-items:start;">
        <div class="prose">
          <h2>日期與地點</h2>

        </div>
        <div class="prose">
          <h2>報名（早鳥價／名額倒數）</h2>

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
    </template>
    <p v-else class="section-lede is-pending">目前尚無對應的夏令營活動，如未來規劃將於本頁與官方社群公布。</p>
  </div>
</section>

<section class="band">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">FAQ</p>
        <h2 class="section-title">夏令營常見問題</h2>
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

<section v-if="isTcrfc" class="band" style="background:var(--paper-2);" aria-labelledby="sc-gallery-title">
  <div class="container">
    <div class="prose" style="margin-bottom:1.75rem;">
      <h2 id="sc-gallery-title">往年花絮</h2>
    </div>
    <div class="photo-grid">
      <figure class="clip-card"><img :src="siteImg('/assets/img/programs/summer-camp-03.jpg')" loading="lazy" width="1600" height="1200" alt="一名學員於室外球場上凌空控球，展現盤球技巧動作"><figcaption>技巧練習片刻</figcaption></figure>
      <figure class="clip-card"><img :src="siteImg('/assets/img/programs/summer-camp-04.jpg')" loading="lazy" width="1600" height="1200" alt="穿著台中磐石白色球衣的兒童學員手比勝利手勢，於場邊補充水分"><figcaption>訓練空檔補水休息</figcaption></figure>
      <figure class="clip-card"><img :src="siteImg('/assets/img/programs/summer-camp-06.jpg')" loading="lazy" width="1600" height="1200" alt="教室內學員坐在課桌前，聆聽外籍教師以投影片進行課程說明"><figcaption>營隊課室活動</figcaption></figure>
    </div>
  </div>
</section>

<section class="band grain cta-band" aria-labelledby="sc-cta-title">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">5.2</span>
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker kicker--on-dark">SUMMER CAMP</p>
        <h2 class="section-title" id="sc-cta-title">{{ isTcrfc ? '關注下一梯次夏令營' : '推廣活動最新消息' }}</h2>
      </div>
    </div>
    <p v-if="isTcrfc" class="section-lede">梯次公告後將於此頁與社群帳號同步發布，站內不接受金流付款。</p>
    <p v-else class="section-lede">是否推出夏令營活動將視規劃進度公布，歡迎關注「推廣活動」總覽與官方社群最新消息。</p>
    <div class="hero__ctas" style="margin-top:2rem;">
      <a v-if="isTcrfc" class="btn btn--primary" :href="lp('/zh/join/camp-registration/')">加入候補通知</a>
      <a class="btn btn--light" :href="lp('/zh/programs/')">回課程總覽</a>
    </div>
  </div>
</section>
</template>

<style>
/* 藍鯨無對應照片時的頁首佔位漸層（BW-C1 新增，沿用 academy/pathway.vue 等既有樣式） */
.page-hero__bg--pending{ background:linear-gradient(160deg, var(--ink) 0%, var(--brand-deep) 100%); }

/* PROGRAMS 單元共用元件（5.1–5.5 共用，重複三頁以上，建議收進 tcrfc.css） */
.photo-grid{ display:grid; gap:1rem; grid-template-columns:repeat(auto-fit,minmax(220px,1fr)); }
.photo-grid figure{ margin:0; position:relative; }
.photo-grid img{ width:100%; aspect-ratio:4/3; object-fit:cover; display:block; }
.photo-grid figcaption{ font-size:.72rem; color:var(--muted); margin-top:.5rem; line-height:1.5; }

/* 報名資訊卡（早鳥價／名額倒數／梯次）：有開放中梯次時顯示真實數字（S1-15），
   沒有梯次時退回「待公告」——`programs` 表現況 0 筆種子資料，故現況仍全部待公告。 */
.signup-preview{ margin-top:2rem; border:1px solid var(--rule); max-width:420px; }
.signup-preview__row{ display:flex; justify-content:space-between; padding:.9rem 1.25rem; font-size:.85rem; border-bottom:1px solid var(--rule); }
.signup-preview__row:last-child{ border-bottom:none; }
.signup-preview__value{ font-weight:700; color:var(--muted); }

/* S1-15 新增：真實梯次為空、常見問題為空時的通用提示文字，以及 G-12 快捷區塊 */
.is-pending{ color:var(--muted); font-style:italic; }
.faq-embed-list{ margin-top:1.5rem; display:flex; flex-direction:column; gap:1.25rem; }
.faq-embed-item dt{ font-weight:800; color:var(--heading); }
.faq-embed-item dd{ margin:.4rem 0 0; color:var(--muted); font-size:.9rem; line-height:1.7; }
</style>

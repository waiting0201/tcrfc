<script setup lang="ts">
// app/pages/zh/programs/specialist/index.vue — 由 site/src/pages/zh/programs/specialist/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
//
// S2-10（2026-09-29）：本頁對藍鯨已整頁 404（units.ts BLUE_WHALE_DISABLED_UNITS 的
// '5.4')。內容是磐石男子一線隊球員真實訓練照片與「台中磐石成人足球訓練營」具名宣傳
// 文案，與 3.4／4.5／4.6 同一種「真實事實無法替換」問題；藍鯨唯一沾得上邊的是社區
// 推廣性質的「藍鯨守門員基礎班」（7–12 歲兒童班），規模與定位都不是同一種六大專項
// 競技訓練產品，換抬頭字樣會構成臆造，故關閉，不需要俱樂部分支。理由見
// shared/utils/units.ts 檔頭與 apps/web/README.md「S2-10」節。
//
// 本輪新增：讀真實 05 課程與活動公開 API 查詢 `program_type='specialist_training'`。
// 六大專項本身是固定分類介紹（既有靜態內容，非資料驅動），不覆寫；新增「目前開放
// 報名的專項」區塊，有真實梯次時顯示，沒有（現況：programs 表 0 筆種子資料）時維持
// 既有「站內不接受金流付款」提示,不臆造。
definePageMeta({ nav: 'programs', unit: '5.4' })

const { lp, locale } = useLocale()
const config = useRuntimeConfig()

useSeoMeta({
  title: '專項訓練 Specialist Training｜課程與活動｜台中磐石足球俱樂部',
  description:
    '台中磐石專項訓練涵蓋守門員、前鋒、後衛、中場、體能與速度、高階訓練六大類別，由台中磐石教練團規劃執行，線上報名。',
})

const { data: programList } = await useFetch(`/api/backend/${config.public.club}/programs`, {
  query: { type: 'specialist_training', pageSize: 10, lang: locale.value },
})
const openPrograms = computed(() => (programList.value?.items ?? []).filter((p) => p.hasOpenSession))
const firstProgram = computed(() => programList.value?.items?.[0] ?? null)
const { data: programDetail } = await useFetch(
  `/api/backend/${config.public.club}/programs/${firstProgram.value?.slug ?? ''}`,
  { query: { lang: locale.value }, immediate: !!firstProgram.value },
)

// G-12 常見問題快捷區塊：program_detail 掛載點，理由同 childrens-training/index.vue。
const { faqs } = useFaqEmbed(config.public.club, 'program_detail', locale.value)
useFaqPageSchema(faqs)

// Course JSON-LD（GEO-05／§7 結構化資料型別清單，S1-20）。本頁可能有多筆專項課程，
// 但 useCourseSchema 現況只接單一課程物件（同 S1-20 既有介面），故比照 5.1／5.2
// 既有做法只取第一筆——不逐一輸出六個專項，避免另開一套多節點介面卻沒有真實資料
// 可驗證。provider 固定為俱樂部本身（本頁對藍鯨已整頁 404，理由同上）。
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
      <li aria-current="page">專項訓練</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <img class="page-hero__bg" src="/assets/img/programs/specialist-06.jpg" alt="" width="1600" height="1067">
  <div class="container">
    <p class="page-hero__eyebrow">5.4 Programs</p>
    <h1>專項訓練<span class="en">Specialist Training</span></h1>
    <p class="page-hero__lede">針對特定位置與能力設計的分科訓練，由台中磐石教練團依學員需求規劃課程目標與適合對象。</p>
  </div>
</section>

<section class="band">
  <div class="container">
    <div class="prose" style="margin-bottom:1.75rem;">
      <h2>六大專項</h2>
      <p>每一專項皆有明確的訓練目標與適合對象，學員可依自身位置或想加強的能力選擇課程。</p>
    </div>

    <div class="level-grid level-grid--specialist">
      <div class="level-card">
        <p class="level-card__num">01</p>
        <h3>守門員<span class="en">Goalkeeper</span></h3>
        <p>撲救技術、位置判讀、出擊時機與腳下技術訓練。</p>
      </div>
      <div class="level-card">
        <p class="level-card__num">02</p>
        <h3>前鋒<span class="en">Forward</span></h3>
        <p>射門終結、無球跑位與禁區內處理球的決策訓練。</p>
      </div>
      <div class="level-card">
        <p class="level-card__num">03</p>
        <h3>後衛<span class="en">Defender</span></h3>
        <p>一對一防守、區域協防與由守轉攻的出球能力。</p>
      </div>
      <div class="level-card">
        <p class="level-card__num">04</p>
        <h3>中場<span class="en">Midfield</span></h3>
        <p>控球轉移、視野掌握與攻守轉換節奏的訓練。</p>
      </div>
      <div class="level-card">
        <p class="level-card__num">05</p>
        <h3>體能與速度<span class="en">Fitness &amp; Speed</span></h3>
        <p>爆發力、敏捷度與比賽所需體能基礎的系統訓練。</p>
      </div>
      <div class="level-card">
        <p class="level-card__num">06</p>
        <h3>高階訓練<span class="en">Advanced</span></h3>
        <p>面向具一定基礎的學員，強化戰術理解與比賽強度適應。</p>
      </div>
    </div>

  </div>
</section>

<section class="band" style="background:var(--paper-2);">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">OPEN NOW</p>
        <h2 class="section-title">目前開放報名的專項</h2>
      </div>
    </div>
    <p v-if="openPrograms.length === 0" class="is-pending" style="margin-top:1.5rem;">目前尚無開放報名中的專項梯次，請關注官方社群公告。</p>
    <ul v-else class="open-program-list">
      <li v-for="p in openPrograms" :key="p.id" class="open-program-item">
        <span class="open-program-item__name">{{ p.name }}</span>
        <span v-if="p.audience" class="open-program-item__audience">{{ p.audience }}</span>
      </li>
    </ul>
  </div>
</section>

<section class="band">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">FAQ</p>
        <h2 class="section-title">專項訓練常見問題</h2>
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

<section class="band" style="background:var(--paper-2);">
  <div class="container">
    <div class="prose">
      <h2>教練資格</h2>
      <p>依客戶提供之「台中磐石成人足球訓練營」宣傳資料，台中磐石專項訓練教練團具備 <span class="en">AFC</span> 教練證照。</p>
    </div>
    
  </div>
</section>

<section class="band" aria-labelledby="sp-gallery-title">
  <div class="container">
    <div class="prose" style="margin-bottom:1.75rem;">
      <h2 id="sp-gallery-title">訓練與交流花絮</h2>
    </div>
    <div class="photo-grid">
      <figure class="clip-card"><img src="/assets/img/programs/specialist-02.jpg" loading="lazy" width="1600" height="1067" alt="夜間球場上，一名球員盤球突破防守者，隊友於後方跟進"><figcaption>夜間友誼賽交流</figcaption></figure>
      <figure class="clip-card"><img src="/assets/img/programs/specialist-03.jpg" loading="lazy" width="1600" height="1067" alt="兩名球員於場邊碰拳致意，其中一人身穿台中磐石白色訓練服"><figcaption>訓練後互動交流</figcaption></figure>
      <figure class="clip-card"><img src="/assets/img/programs/specialist-04.jpg" loading="lazy" width="1600" height="1067" alt="教練於球場中央向圍成一圈的球員講解戰術"><figcaption>賽前戰術講解</figcaption></figure>
      <figure class="clip-card"><img src="/assets/img/programs/specialist-05.jpg" loading="lazy" width="1600" height="1067" alt="身穿綠色背心的教練於球場上跑動示範"><figcaption>教練親自示範跑位</figcaption></figure>
      <figure class="clip-card"><img src="/assets/img/programs/specialist-07.jpg" loading="lazy" width="1600" height="1067" alt="球員於夜間球場上準備射門，球場後方可見城市建築燈光"><figcaption>夜間場地訓練賽</figcaption></figure>
    </div>
  </div>
</section>

<section class="band grain cta-band" aria-labelledby="sp-cta-title">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">5.4</span>
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker kicker--on-dark">SPECIALIST TRAINING</p>
        <h2 class="section-title" id="sp-cta-title">選擇你的專項</h2>
      </div>
    </div>
    <p class="section-lede">梯次、地點與費用將於報名開放時公告，站內不接受金流付款。</p>
    <div class="hero__ctas" style="margin-top:2rem;">
      <a class="btn btn--primary" :href="lp('/zh/join/academy/')">線上報名</a>
      <a class="btn btn--light" :href="lp('/zh/programs/')">回課程總覽</a>
    </div>
  </div>
</section>
</template>

<style>
/* PROGRAMS 單元共用元件（5.1–5.5 共用，重複三頁以上，建議收進 tcrfc.css） */
.level-grid{ display:grid; gap:1.5rem; grid-template-columns:repeat(auto-fit,minmax(230px,1fr)); margin-top:1.5rem; }
.level-card{ background:var(--paper); padding:1.75rem 1.5rem; border-top:3px solid var(--brand-aa); }
.level-card__num{ font-size:.72rem; font-weight:800; color:var(--muted); letter-spacing:.08em; margin-bottom:.5rem; }
.level-card h3{ font-size:1.05rem; font-weight:800; margin-bottom:.6rem; color:var(--heading); }
.level-card h3 .en{ display:block; font-size:.72rem; font-weight:700; color:var(--muted); text-transform:uppercase; letter-spacing:.06em; margin-top:.15rem; }
.level-card p{ font-size:.85rem; color:var(--muted); line-height:1.7; }
.level-grid--specialist{ grid-template-columns:repeat(auto-fit,minmax(260px,1fr)); }

.photo-grid{ display:grid; gap:1rem; grid-template-columns:repeat(auto-fit,minmax(220px,1fr)); }
.photo-grid figure{ margin:0; position:relative; }
.photo-grid img{ width:100%; aspect-ratio:4/3; object-fit:cover; display:block; }
.photo-grid figcaption{ font-size:.72rem; color:var(--muted); margin-top:.5rem; line-height:1.5; }

/* S2-10 新增：開放報名專項清單／常見問題快捷區塊（沿用 programs 系列既有慣例）。 */
.is-pending{ color:var(--muted); font-style:italic; }
.open-program-list{ margin-top:1.5rem; display:flex; flex-direction:column; gap:.75rem; }
.open-program-item{ display:flex; justify-content:space-between; gap:1rem; padding:.9rem 1.1rem; background:var(--paper); border:1px solid var(--rule); }
.open-program-item__name{ font-weight:800; color:var(--heading); }
.open-program-item__audience{ font-size:.82rem; color:var(--muted); }
.faq-embed-list{ margin-top:1.5rem; display:flex; flex-direction:column; gap:1.25rem; }
.faq-embed-item dt{ font-weight:800; color:var(--heading); }
.faq-embed-item dd{ margin:.4rem 0 0; color:var(--muted); font-size:.9rem; line-height:1.7; }
</style>

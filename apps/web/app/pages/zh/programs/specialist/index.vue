<script setup lang="ts">
// app/pages/zh/programs/specialist/index.vue — 由 site/src/pages/zh/programs/specialist/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
//
// BW-C1（2026-09-29）：本頁對藍鯨重開（S2-10 曾以「真實事實無法替換」為由整頁
// 404，是誤用——理由同 programs/childrens-training/index.vue 檔頭）。藍鯨有部分真實
// 對應內容：「藍鯨守門員基礎班」（programs.md §2 第 10 項，7–12 歲），但規模與定位
// 不是磐石「六大專項競技訓練」同一種產品，不套用六大專項框架——改為單獨呈現這一項
// 真實課程，其餘專項維持誠實的「尚未推出」空狀態。改讀 getSpecialistTrainingSeo()／
// getSpecialistTrainingHero()（club-copy.ts）。TCRFC 專屬的
// 「AFC 教練證照」宣傳文案與訓練花絮照片對藍鯨隱藏，CTA 不連到磐石專屬的
// `/zh/join/academy/`，改走官方 LINE 洽詢（2026-11-16 起守門員基礎班等班別一律讀課程資料，不再寫死班別與外部報名表單網址）。
//
// 本輪新增：讀真實 05 課程與活動公開 API 查詢 `program_type='specialist_training'`。
// 六大專項本身是固定分類介紹（既有靜態內容，非資料驅動），不覆寫；新增「目前開放
// 報名的專項」區塊，有真實梯次時顯示，沒有（現況：programs 表 0 筆種子資料）時維持
// 既有「站內不接受金流付款」提示,不臆造。
// 2026-10-02：磐石的「線上報名」接 P3（規劃書 §3.5 報名流程：選梯次→學員／家長資料→健康聲明→報名編號），
// 見 components/ProgramRegistration.vue。藍鯨的線上報名與收費是待確認事項（藍鯨規劃書 §10 第 8 點），不接。
// 目前沒有收得到報名的梯次時，CTA 維持原本的詢問表單路徑。
definePageMeta({ nav: 'programs', unit: '5.4', enReady: true, enReadyBw: true })

const { lp, locale, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')

useSeoMeta({
  title: computed(() => (isEn.value ? getSpecialistTrainingSeoEn(clubKey.value) : getSpecialistTrainingSeo(clubKey.value)).title),
  description: computed(() => (isEn.value ? getSpecialistTrainingSeoEn(clubKey.value) : getSpecialistTrainingSeo(clubKey.value)).description),
})
const hero = computed(() => (isEn.value ? getSpecialistTrainingHeroEn(clubKey.value) : getSpecialistTrainingHero(clubKey.value)))

const identity = useClubIdentity()
const { data: programList } = await useFetch(`/api/backend/${config.public.club}/programs`, {
  query: { type: 'specialist_training', pageSize: 10, lang: locale.value },
})
const openPrograms = computed(() => (programList.value?.items ?? []).filter((p) => p.hasOpenSession))
// B-8：全部已發布專項課程的詳情（內容、教練團、合作夥伴、封面）；Course Schema 仍只取第一筆（見下方說明）。
const { programs, first: programDetail } = await useProgramsOfType('specialist_training')
/** 藍鯨梯次表：全部專項課程的梯次攤平（班別名稱＝課程名稱）。 */
const sessions = computed(() => programs.value.flatMap((p) => p.sessions.map((s) => ({ ...s, programName: p.name }))))

const { programs: registrablePrograms, hasRegistrable } = await useRegistrablePrograms('specialist_training', { enabled: isTcrfc.value })

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
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/programs/')">{{ tx('課程與活動', 'Programs') }}</a></li>
      <li aria-current="page">{{ tx('專項訓練', 'Specialist Training') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <!-- 藍鯨無對應的六大專項訓練照片可用，不沿用磐石照片頂替 -->
  <img v-if="isTcrfc" class="page-hero__bg" :src="siteImg('/assets/img/programs/specialist-06.jpg')" alt="" width="1600" height="1067">
  <div v-else class="page-hero__bg page-hero__bg--pending" aria-hidden="true" />
  <div class="container">
    <p class="page-hero__eyebrow">{{ isTcrfc ? '5.4 Programs' : '5.4' }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<!-- B-8：後台課程（P1）的內容、適合對象、年齡、教練團、合作夥伴；有課程資料才顯示 -->
<ProgramInfoBand v-if="programs.length" :programs="programs" :content-title="tx('課程介紹', 'About the Programs')" show-partners />

<section v-if="isTcrfc" class="band">
  <div class="container">
    <div class="prose" style="margin-bottom:1.75rem;">
      <h2>{{ tx('六大專項', 'Six Specialist Programs') }}</h2>
      <p>{{ tx('每一專項皆有明確的訓練目標與適合對象，學員可依自身位置或想加強的能力選擇課程。', 'Each program has clear training objectives and a target group, so players can choose according to their position or the ability they want to develop.') }}</p>
    </div>

    <div class="level-grid level-grid--specialist">
      <div class="level-card">
        <p class="level-card__num">01</p>
        <h3>{{ tx('守門員', 'Goalkeeper') }}<span v-if="!isEn" class="en">Goalkeeper</span></h3>
        <p>{{ tx('撲救技術、位置判讀、出擊時機與腳下技術訓練。', 'Shot-stopping, positioning, timing of when to come out, and footwork with the ball.') }}</p>
      </div>
      <div class="level-card">
        <p class="level-card__num">02</p>
        <h3>{{ tx('前鋒', 'Forward') }}<span v-if="!isEn" class="en">Forward</span></h3>
        <p>{{ tx('射門終結、無球跑位與禁區內處理球的決策訓練。', 'Finishing, movement off the ball and decision-making when handling the ball in the box.') }}</p>
      </div>
      <div class="level-card">
        <p class="level-card__num">03</p>
        <h3>{{ tx('後衛', 'Defender') }}<span v-if="!isEn" class="en">Defender</span></h3>
        <p>{{ tx('一對一防守、區域協防與由守轉攻的出球能力。', 'One-on-one defending, zonal cover and the ability to play out from defence into attack.') }}</p>
      </div>
      <div class="level-card">
        <p class="level-card__num">04</p>
        <h3>{{ tx('中場', 'Midfield') }}<span v-if="!isEn" class="en">Midfield</span></h3>
        <p>{{ tx('控球轉移、視野掌握與攻守轉換節奏的訓練。', 'Ball retention and switching play, awareness of the field and the rhythm of transitions between attack and defence.') }}</p>
      </div>
      <div class="level-card">
        <p class="level-card__num">05</p>
        <h3>{{ tx('體能與速度', 'Fitness & Speed') }}<span v-if="!isEn" class="en">Fitness &amp; Speed</span></h3>
        <p>{{ tx('爆發力、敏捷度與比賽所需體能基礎的系統訓練。', 'Systematic training in power, agility and the fitness base that matches demand.') }}</p>
      </div>
      <div class="level-card">
        <p class="level-card__num">06</p>
        <h3>{{ tx('高階訓練', 'Advanced Training') }}<span v-if="!isEn" class="en">Advanced</span></h3>
        <p>{{ tx('面向具一定基礎的學員，強化戰術理解與比賽強度適應。', 'For players with a solid base, strengthening tactical understanding and adaptation to match intensity.') }}</p>
      </div>
    </div>

  </div>
</section>

<!-- 藍鯨：班別、年齡、費用與梯次一律讀後台課程（P1）與梯次，不寫死；沒有課程時顯示空狀態 -->
<section v-else class="band">
  <div class="container">
    <div class="prose" style="margin-bottom:1.75rem;">
      <h2>{{ tx('專項課程與梯次', 'Specialist Programs and Sessions') }}</h2>
      <p v-if="sessions.length === 0">{{ tx('專項課程與梯次整理中，稍後將於本頁公布。', 'Specialist programs and sessions are being compiled and will be published on this page soon.') }}</p>
      <p v-else>{{ tx('目前的梯次如下，時段為每週固定上課時間，實際場地請以梯次公告為準。', 'The current sessions are listed below. Times are the fixed weekly class times; please refer to the session notice for the actual venue.') }}</p>
    </div>
    <div v-if="sessions.length" class="table-wrap">
      <table class="data-table">
        <thead>
          <tr><th scope="col">{{ tx('課程', 'Program') }}</th><th scope="col">{{ tx('時段', 'Schedule') }}</th><th scope="col">{{ tx('地點', 'Venue') }}</th><th scope="col">{{ tx('費用', 'Fee') }}</th></tr>
        </thead>
        <tbody>
          <tr v-for="s in sessions" :key="s.id">
            <td>{{ s.programName }}</td>
            <td>{{ formatWeeklySchedule(s.weeklySchedule, locale) ?? '—' }}</td>
            <td>{{ s.venueName ?? '—' }}</td>
            <td>{{ formatPrice(s.price) }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</section>

<LocaleFallbackNotice v-if="isEn && hasFallbackLocale(programList)" partial />
<section class="band" style="background:var(--paper-2);">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">OPEN NOW</p>
        <h2 class="section-title">{{ tx('目前開放報名的專項', 'Programs Open for Registration') }}</h2>
      </div>
    </div>
    <p v-if="openPrograms.length === 0" class="is-pending" style="margin-top:1.5rem;">{{ tx('目前尚無開放報名中的專項梯次，請關注官方社群公告。', 'There are no specialist sessions open for registration at the moment. Please follow our official social channels for announcements.') }}</p>
    <ul v-else class="open-program-list">
      <li v-for="p in openPrograms" :key="p.id" class="open-program-item">
        <span class="open-program-item__name">{{ p.name }}</span>
        <span v-if="p.audience" class="open-program-item__audience">{{ p.audience }}</span>
      </li>
    </ul>
  </div>
</section>

<LocaleFallbackNotice v-if="isEn && hasFallbackLocale(faqs)" partial />
<section class="band">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">FAQ</p>
        <h2 class="section-title">{{ tx('專項訓練常見問題', 'Specialist Training FAQ') }}</h2>
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

<section v-if="isTcrfc" class="band" style="background:var(--paper-2);">
  <div class="container">
    <div class="prose">
      <h2>{{ tx('教練資格', 'Coaching Qualifications') }}</h2>
      <p v-if="isEn">According to promotional material for the Taichung Rock FC adult football training camp, the Specialist Training coaching team hold <span class="en">AFC</span> coaching licences.</p>
      <p v-else>依「台中磐石成人足球訓練營」宣傳資料，台中磐石專項訓練教練團具備 <span class="en">AFC</span> 教練證照。</p>
    </div>

  </div>
</section>

<section v-if="isTcrfc" class="band" aria-labelledby="sp-gallery-title">
  <div class="container">
    <div class="prose" style="margin-bottom:1.75rem;">
      <h2 id="sp-gallery-title">{{ tx('訓練與交流花絮', 'Training and Exchange Highlights') }}</h2>
    </div>
    <div class="photo-grid">
      <figure class="clip-card"><img :src="siteImg('/assets/img/programs/specialist-02.jpg')" loading="lazy" width="1600" height="1067" :alt="tx('夜間球場上，一名球員盤球突破防守者，隊友於後方跟進', 'On a pitch at night, a player dribbles past a defender while a teammate follows behind')"><figcaption>{{ tx('夜間友誼賽交流', 'Evening friendly exchange') }}</figcaption></figure>
      <figure class="clip-card"><img :src="siteImg('/assets/img/programs/specialist-03.jpg')" loading="lazy" width="1600" height="1067" :alt="tx('兩名球員於場邊碰拳致意，其中一人身穿台中磐石白色訓練服', 'Two players bump fists at the pitch side, one wearing a Taichung Rock FC white training top')"><figcaption>{{ tx('訓練後互動交流', 'Chatting after training') }}</figcaption></figure>
      <figure class="clip-card"><img :src="siteImg('/assets/img/programs/specialist-04.jpg')" loading="lazy" width="1600" height="1067" :alt="tx('教練於球場中央向圍成一圈的球員講解戰術', 'A coach explains tactics to players gathered in a circle at the centre of the pitch')"><figcaption>{{ tx('賽前戰術講解', 'Pre-match tactics talk') }}</figcaption></figure>
      <figure class="clip-card"><img :src="siteImg('/assets/img/programs/specialist-05.jpg')" loading="lazy" width="1600" height="1067" :alt="tx('身穿綠色背心的教練於球場上跑動示範', 'A coach in a green bib demonstrates a movement while running on the pitch')"><figcaption>{{ tx('教練親自示範跑位', 'Coach demonstrating off-the-ball runs') }}</figcaption></figure>
      <figure class="clip-card"><img :src="siteImg('/assets/img/programs/specialist-07.jpg')" loading="lazy" width="1600" height="1067" :alt="tx('球員於夜間球場上準備射門，球場後方可見城市建築燈光', 'A player lines up a shot on a pitch at night, with city building lights in the background')"><figcaption>{{ tx('夜間場地訓練賽', 'Evening practice match') }}</figcaption></figure>
    </div>
  </div>
</section>
<section v-else class="band" aria-labelledby="sp-gallery-title">
  <div class="container">
    <div class="prose">
      <h2 id="sp-gallery-title">{{ tx('訓練與交流花絮', 'Training and Exchange Highlights') }}</h2>
      <p class="is-pending">{{ tx('花絮整理中，稍後將於本頁公布。', 'Highlights are being compiled and will be published on this page soon.') }}</p>
    </div>
  </div>
</section>

<ProgramRegistration v-if="isTcrfc && hasRegistrable" :programs="registrablePrograms" />

<section class="band grain cta-band" aria-labelledby="sp-cta-title">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">5.4</span>
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker kicker--on-dark">SPECIALIST TRAINING</p>
        <h2 class="section-title" id="sp-cta-title">{{ tx('選擇你的專項', 'Choose Your Specialism') }}</h2>
      </div>
    </div>
    <p v-if="isTcrfc" class="section-lede">{{ tx('梯次、地點與費用將於報名開放時公告，站內不接受金流付款。', 'Sessions, venues and fees will be announced when registration opens. Payment is not taken on this site.') }}</p>
    <p v-else class="section-lede">{{ tx('梯次、地點與費用以本頁公告為準，報名方式請洽台中藍鯨官方 LINE。', `Sessions, venues and fees follow the announcements on this page; to register, contact the official ${BW_NAME_EN} LINE account.`) }}</p>
    <div class="hero__ctas" style="margin-top:2rem;">
      <a v-if="isTcrfc" class="btn btn--primary" :href="hasRegistrable ? '#register' : lp('/zh/join/academy/')">{{ tx('線上報名', 'Register Online') }}</a>
      <a v-else-if="identity.social.line" class="btn btn--primary" :href="identity.social.line" target="_blank" rel="noopener">{{ tx('洽詢官方 LINE', 'Contact us on LINE') }}</a>
      <a class="btn btn--light" :href="lp('/zh/programs/')">{{ tx('回課程總覽', 'Back to Programs Overview') }}</a>
    </div>
  </div>
</section>
</template>

<style>
/* 藍鯨無對應照片時的頁首佔位漸層（BW-C1 新增，沿用 academy/pathway.vue 等既有樣式） */
.page-hero__bg--pending{ background:linear-gradient(160deg, var(--ink) 0%, var(--brand-deep) 100%); }

/* PROGRAMS 單元共用元件（5.1–5.5 共用，重複三頁以上，建議收進 tcrfc.css） */
.level-grid{ display:grid; gap:1.5rem; grid-template-columns:repeat(auto-fit,minmax(230px,1fr)); margin-top:1.5rem; }
.level-card{ background:var(--paper); padding:1.75rem 1.5rem; border-top:3px solid var(--brand-aa); }
.level-card__num{ font-size:.72rem; font-weight:800; color:var(--muted); letter-spacing:.08em; margin-bottom:.5rem; }
.level-card h3{ font-size:1.05rem; font-weight:800; margin-bottom:.6rem; color:var(--heading); }
.level-card h3 .en{ display:block; font-size:.72rem; font-weight:700; color:var(--muted); text-transform:uppercase; letter-spacing:.06em; margin-top:.15rem; }
.level-card p{ font-size:.85rem; color:var(--muted); line-height:1.7; }
.level-grid--specialist{ grid-template-columns:repeat(auto-fit,minmax(260px,1fr)); }

.table-wrap{ overflow-x:auto; }
.data-table{ width:100%; border-collapse:collapse; min-width:560px; }
.data-table th, .data-table td{ padding:.85rem 1rem; text-align:left; border-bottom:1px solid var(--rule); font-size:.85rem; }
.data-table thead th{ background:var(--ink); color:#fff; font-weight:700; letter-spacing:.03em; }

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

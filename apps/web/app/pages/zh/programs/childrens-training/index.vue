<script setup lang="ts">
// app/pages/zh/programs/childrens-training/index.vue — 由 site/src/pages/zh/programs/childrens-training/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
//
// BW-C1（2026-09-29）：本頁對藍鯨重開（S1-15 曾以「藍鯨『05 推廣活動』是完全不同的
// 活動集合」為由整頁 404，是誤用——藍鯨規劃書 §3.5（行 201）「05 推廣活動沿用主站
// 05 的活動版型；是否開放線上報名與收費，待確認」，這是「頁面開放、報名功能待定」
// 的明文依據，不是關閉整頁的理由；見 shared/utils/units.ts 檔頭）。藍鯨其實有真實
// 對應內容：`content/blue-whale/programs.md` §1（社區足球學校「小藍鯨」）與 §2
// （運動 i 台灣 2.0 運動熱區課程表，3–15 歲多種班別），改為讀
// getChildrensTrainingSeo()／getChildrensTrainingHero()／
// CHILDRENS_TRAINING_CLASSES_BW（club-copy.ts）。報名 CTA 對藍鯨不接站內報名流程
// （「是否開放線上報名」尚未確認），改顯示舊站真實報名方式（現場個人報名）＋
// 官方 LINE 聯絡連結，不連到磐石專屬的 `/zh/join/academy/`。
//
// 本輪新增：讀真實 05 課程與活動公開 API（P1–P3，S1-9 後端已完成）查詢
// `program_type='children_training'`（值域見 apps/admin/src/types/program.ts，
// 與後台 apps/api/Features/AdminPrograms/AdminProgramsRepository.AllowedProgramTypes
// 一致，5.1–5.5 逐頁對應五個固定代碼，非本輪自訂）。現況：`programs` 資料表兩俱樂部
// 皆 0 筆種子資料（db/seed 尚未涵蓋課程模組），故本輪只做到「接了 API、目前空清單」，
// 「週期課表」維持既有示意空表列，不臆造梯次。若之後後台真的建立本類型課程，
// 表格會自動改顯示真實梯次（星期／時段／分級／地點），不需要再改樣板。
definePageMeta({ nav: 'programs', unit: '5.1' })

const { lp, locale } = useLocale()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')
const identity = computed(() => getClubIdentity(clubKey.value))

// BW-C1：改讀 useSiteFacts(clubKey)（後端公開端點），不再固定讀 tcrfc。
const { facts, primaryVenue } = useSiteFacts(clubKey.value)

useSeoMeta({
  title: computed(() => getChildrensTrainingSeo(clubKey.value).title),
  description: computed(() => getChildrensTrainingSeo(clubKey.value).description),
})
const hero = computed(() => getChildrensTrainingHero(clubKey.value))

const { data: programList } = await useFetch(`/api/backend/${config.public.club}/programs`, {
  query: { type: 'children_training', pageSize: 5, lang: locale.value },
})
const firstProgram = computed(() => programList.value?.items?.[0] ?? null)
// firstProgram 在這裡已經是上一個 await 完成後的定值（不是待解析的非同步狀態），
// 用 `immediate: !!firstProgram.value` 決定要不要真的送出這支詳情請求即可，
// 不需要用「URL 回傳 null」這種 useFetch 沒有明確支援的寫法（$fetch 的 URL 參數
// 不接受 null，那樣寫在 SSR 階段會直接丟例外，不是優雅跳過）。
const { data: programDetail } = await useFetch(
  `/api/backend/${config.public.club}/programs/${firstProgram.value?.slug ?? ''}`,
  { query: { lang: locale.value }, immediate: !!firstProgram.value },
)
/** 真實梯次資料（週期課表來自 `weeklySchedule` JSON，經 `formatWeeklySchedule` 轉成可讀文字）。空陣列＝目前沒有已建立的梯次，
 * 樣板落回既有示意空表列，不是接失敗。 */
const sessions = computed(() => programDetail.value?.sessions ?? [])
/** `weeklySchedule` 是 JSON 文字，交給共用格式化函式（依路由語系）；解析不了顯示「—」並在開發環境警告。 */
function formatSchedule(raw: string | null | undefined): string {
  return formatWeeklySchedule(raw, locale.value, import.meta.dev ? (m) => console.warn(`[weekly-schedule] ${m}`) : undefined) ?? '—'
}

// G-12 常見問題快捷區塊：program_detail 掛載點（db/seed FAQ_EMBED_SLOTS「課程詳情頁
// （5.x 各課程）」），四個固定掛載點之一，理由見 useFaqEmbed.ts 檔頭。
const { faqs } = useFaqEmbed(config.public.club, 'program_detail', locale.value)

// GEO-06（S1-18a）：G-12 嵌入區塊與 12 FAQ 獨立單元同條規定「一律輸出 FAQPage」，
// 沿用同一份資料（faqs）餵給畫面與結構化資料，不另外重打一次 API。
useFaqPageSchema(faqs)

// Course JSON-LD（GEO-05／§7 結構化資料型別清單，S1-20）。provider 固定為俱樂部本身
// （本頁對藍鯨已整頁 404，見上方檔頭說明，固定讀 tcrfc 的俱樂部名稱即可）。資料不足
// （現況：programs 表 0 筆種子資料，programDetail 為 null）時不輸出，見
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
      <li aria-current="page">兒童足球訓練</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <!-- 藍鯨無已核實可用的兒童訓練照片可用，不沿用磐石照片頂替 -->
  <img v-if="isTcrfc" class="page-hero__bg" src="/assets/img/programs/childrens-03.jpg" alt="" width="1600" height="1067">
  <div v-else class="page-hero__bg page-hero__bg--pending" aria-hidden="true" />
  <div class="container">
    <p class="page-hero__eyebrow">{{ isTcrfc ? '5.1 Programs' : '5.1' }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<section v-if="isTcrfc" class="band">
  <div class="container">
    <div class="grid grid--2">
      <div class="prose">
        <h2>課程分級</h2>
        <p>兒童足球訓練依學員年齡與足球經驗分為三個等級，各級課程內容與訓練節奏將依報名梯次公告。</p>
      </div>
    </div>

    <div class="level-grid">
      <div class="level-card">
        <p class="level-card__num">Level 01</p>
        <h3>混齡體驗<span class="en" style="display:block;font-size:.72rem;font-weight:700;color:var(--muted);text-transform:uppercase;letter-spacing:.06em;margin-top:.15rem;">Mixed-age</span></h3>
        <p>不同年齡層學員一同參與，以遊戲化方式認識足球，建立對球的基本熟悉度與運動樂趣。</p>
      </div>
      <div class="level-card">
        <p class="level-card__num">Level 02</p>
        <h3>初學<span class="en" style="display:block;font-size:.72rem;font-weight:700;color:var(--muted);text-transform:uppercase;letter-spacing:.06em;margin-top:.15rem;">Beginner</span></h3>
        <p>建立基礎控球、傳接與跑動概念，養成規律練習習慣，為進一步的技巧訓練打底。</p>
      </div>
      <div class="level-card">
        <p class="level-card__num">Level 03</p>
        <h3>技巧發展<span class="en" style="display:block;font-size:.72rem;font-weight:700;color:var(--muted);text-transform:uppercase;letter-spacing:.06em;margin-top:.15rem;">Skill Development</span></h3>
        <p>已具備基礎能力的學員，加強個人技術、戰術理解與比賽情境應用。</p>
      </div>
    </div>

  </div>
</section>

<!-- 藍鯨：真實課程班別（programs.md §1／§2，年齡 3–15 歲），逐字節錄，非分級框架。 -->
<section v-else class="band">
  <div class="container">
    <div class="grid grid--2">
      <div class="prose">
        <h2>課程班別</h2>
        <p>社區足球學校「小藍鯨」承接臺中市政府「運動 i 台灣 2.0」運動熱區推廣計畫，依年齡分為多種班別，免試上、免測試、免入會費。</p>
      </div>
    </div>
    <div class="table-wrap">
      <table class="data-table">
        <thead>
          <tr><th scope="col">班別</th><th scope="col">適合年齡</th><th scope="col">費用</th></tr>
        </thead>
        <tbody>
          <tr v-for="c in CHILDRENS_TRAINING_CLASSES_BW" :key="c.nameZh">
            <td>{{ c.nameZh }}</td>
            <td>{{ c.ageZh }}</td>
            <td>{{ c.feeZh }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</section>

<section class="band" style="background:var(--paper-2);">
  <div class="container">
    <div class="grid grid--2" style="align-items:start;">
      <div class="prose">
        <h2>訓練地點</h2>
        <p v-if="isTcrfc">兒童足球訓練主要於台中磐石主場地舉行，實際梯次場地將於報名頁面標示。</p>
        <p v-else>課程主要於{{ primaryVenue.nameZh }}舉行，實際梯次場地將依公告為準。</p>
      </div>
      <div class="fact-panel">
        <dl style="margin:0;">
          <dt>主要場地</dt>
          <dd>{{ primaryVenue.nameZh }}</dd>
          <template v-if="facts.contact.address">
            <dt>地址</dt>
            <dd>{{ facts.contact.address }}</dd>
          </template>
          <template v-if="isTcrfc">
            <dt>地圖</dt>
            <dd><a href="https://www.google.com/maps/search/?api=1&query=%E8%A5%BF%E5%B1%AF%E8%B6%B3%E7%90%83%E5%A0%B4%20%E5%8F%B0%E4%B8%AD%E5%B8%82%E5%8C%97%E5%B1%AF%E5%8D%80%E5%B4%87%E5%B9%B3%E8%B7%AF%E4%BA%8C%E6%AE%B5%E6%99%AF%E8%B0%B7%E5%B7%B7%2011%20%E5%BC%84%2041%20%E8%99%9F" target="_blank" rel="noopener">在 Google 地圖開啟 <span class="visually-hidden">（新分頁開啟）</span></a></dd>
          </template>
        </dl>
      </div>
    </div>

  </div>
</section>

<section class="band">
  <div class="container">
    <div class="prose" style="margin-bottom:1.75rem;">
      <h2>週期課表</h2>
      <p v-if="sessions.length === 0">梯次尚未開放報名，時段與適合分級將於後台建立梯次後自動顯示於本頁。</p>
      <p v-else>目前開放中的梯次如下，時段為每週固定上課時間，實際場地請以梯次公告為準。</p>
    </div>
    <div class="table-wrap">
      <table class="data-table">
        <thead>
          <tr><th scope="col">時段</th><th scope="col">分級／人數</th><th scope="col">地點</th></tr>
        </thead>
        <tbody>
          <tr v-if="sessions.length === 0"><td colspan="3" class="is-pending">梯次資訊準備中</td></tr>
          <tr v-for="s in sessions" :key="s.id">
            <td>{{ formatSchedule(s.weeklySchedule) }}</td>
            <td>{{ s.enrolledCount }}{{ s.capacity ? ` / ${s.capacity}` : '' }} 人</td>
            <td>{{ s.venueName ?? primaryVenue.nameZh }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</section>

<section class="band">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">FAQ</p>
        <h2 class="section-title">兒童足球訓練常見問題</h2>
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

<section v-if="isTcrfc" class="band" style="background:var(--paper-2);" aria-labelledby="cft-gallery-title">
  <div class="container">
    <div class="prose" style="margin-bottom:1.75rem;">
      <h2 id="cft-gallery-title">活動花絮：台中磐石足球節</h2>
      <p>由台中磐石主辦的兒童足球嘉年華活動，邀集台中多所國小與地區球隊參與，是兒童足球訓練學員展現學習成果、與其他球隊交流的年度活動之一。</p>
    </div>
    <div class="photo-grid">
      <figure class="clip-card"><img src="/assets/img/programs/childrens-02.jpg" loading="lazy" width="1600" height="1067" alt="台中磐石足球節活動現場，多支國小球隊球員席地而坐聆聽工作人員說明活動流程"><figcaption>台中磐石足球節：賽前集合說明</figcaption></figure>
      <figure class="clip-card"><img src="/assets/img/programs/childrens-03.jpg" loading="lazy" width="1600" height="1067" alt="多支參賽國小足球隊學員與教練於場邊合影，手持隊旗與台中磐石活動布條"><figcaption>台中磐石足球節：參賽隊伍合影</figcaption></figure>
      <figure class="clip-card"><img src="/assets/img/programs/childrens-04.jpg" loading="lazy" width="1600" height="1067" alt="兒童球員於場上爭搶控球，隊友於後方跟進包抄"><figcaption>台中磐石足球節：場上比賽</figcaption></figure>
      <figure class="clip-card"><img src="/assets/img/programs/childrens-05.jpg" loading="lazy" width="1600" height="1067" alt="教練蹲低與一組兒童球員圍圈講解戰術板上的站位安排"><figcaption>台中磐石足球節：教練賽中講解</figcaption></figure>
      <figure class="clip-card"><img src="/assets/img/programs/childrens-06.jpg" loading="lazy" width="1600" height="1067" alt="場邊觀眾為正在射門的兒童球員加油，家長舉傘遮陽觀賽"><figcaption>台中磐石足球節：場邊加油</figcaption></figure>
      <figure class="clip-card"><img src="/assets/img/programs/childrens-07.jpg" loading="lazy" width="1600" height="1067" alt="兩名兒童球員在球場上近身爭搶球權"><figcaption>台中磐石足球節：一對一對抗</figcaption></figure>
    </div>

  </div>
</section>
<section v-else class="band" style="background:var(--paper-2);" aria-labelledby="cft-gallery-title">
  <div class="container">
    <div class="prose">
      <h2 id="cft-gallery-title">活動花絮</h2>
      <p class="is-pending">活動花絮整理中，稍後將於本頁公布。</p>
    </div>
  </div>
</section>

<section class="band grain cta-band" aria-labelledby="cft-cta-title">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">5.1</span>
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker kicker--on-dark">{{ isTcrfc ? 'ENROL NOW' : 'JOIN US' }}</p>
        <h2 class="section-title" id="cft-cta-title">立即為孩子報名</h2>
      </div>
    </div>
    <p v-if="isTcrfc" class="section-lede">選擇合適的分級，開始每週規律的足球訓練。站內不接受金流付款，梯次確認後將個別通知繳費方式。</p>
    <p v-else class="section-lede">免試上、免測試、免入會費，現場個人報名即可加入；報名方式與課表請洽台中藍鯨官方 LINE。</p>
    <div class="hero__ctas" style="margin-top:2rem;">
      <a v-if="isTcrfc" class="btn btn--primary" :href="lp('/zh/join/academy/')">線上報名</a>
      <a v-else-if="identity.social.line" class="btn btn--primary" :href="identity.social.line" target="_blank" rel="noopener">洽詢官方 LINE</a>
      <a class="btn btn--light" :href="lp('/zh/programs/')">回課程總覽</a>
    </div>
  </div>
</section>
</template>

<style>
/* 藍鯨無對應照片時的頁首佔位漸層（BW-C1 新增，沿用 academy/pathway.vue 等既有樣式） */
.page-hero__bg--pending{ background:linear-gradient(160deg, var(--ink) 0%, var(--brand-deep) 100%); }

/* PROGRAMS 單元共用元件（5.1–5.5 共用，重複三頁以上，建議收進 tcrfc.css） */
.level-grid{ display:grid; gap:1.5rem; grid-template-columns:repeat(auto-fit,minmax(230px,1fr)); margin-top:1.5rem; }
.level-card{ background:var(--paper-2); padding:1.75rem 1.5rem; border-top:3px solid var(--brand-aa); }
.level-card__num{ font-size:.72rem; font-weight:800; color:var(--muted); letter-spacing:.08em; margin-bottom:.5rem; }
.level-card h3{ font-size:1.05rem; font-weight:800; margin-bottom:.6rem; color:var(--heading); }
.level-card p{ font-size:.85rem; color:var(--muted); line-height:1.7; }

.fact-panel{ background:#fff; border-left:4px solid var(--brand-aa); padding:1.5rem 1.75rem; }
.fact-panel dt{ font-size:.72rem; font-weight:800; text-transform:uppercase; letter-spacing:.06em; color:var(--muted); margin-top:1rem; }
.fact-panel dt:first-child{ margin-top:0; }
.fact-panel dd{ font-size:.95rem; color:var(--heading); font-weight:600; margin-left:0; }
.fact-panel a{ color:var(--brand-aa); font-weight:700; }

.table-wrap{ overflow-x:auto; }
.data-table{ width:100%; border-collapse:collapse; min-width:560px; }
.data-table th, .data-table td{ padding:.85rem 1rem; text-align:left; border-bottom:1px solid var(--rule); font-size:.85rem; }
.data-table thead th{ background:var(--ink); color:#fff; font-weight:700; letter-spacing:.03em; }
.data-table td.is-pending{ color:var(--muted); font-style:italic; }

.photo-grid{ display:grid; gap:1rem; grid-template-columns:repeat(auto-fit,minmax(220px,1fr)); }
.photo-grid figure{ margin:0; position:relative; }
.photo-grid img{ width:100%; aspect-ratio:4/3; object-fit:cover; display:block; }
.photo-grid figcaption{ font-size:.72rem; color:var(--muted); margin-top:.5rem; line-height:1.5; }

/* S1-15 新增：真實梯次為空、常見問題為空時的通用提示文字，以及 G-12 快捷區塊 */
.is-pending{ color:var(--muted); font-style:italic; }
.faq-embed-list{ margin-top:1.5rem; display:flex; flex-direction:column; gap:1.25rem; }
.faq-embed-item dt{ font-weight:800; color:var(--heading); }
.faq-embed-item dd{ margin:.4rem 0 0; color:var(--muted); font-size:.9rem; line-height:1.7; }
</style>

<script setup lang="ts">
// app/pages/zh/about/our-people.vue — 由 site/src/pages/zh/about/our-people/index.html 轉來
// （S0-9 資料驅動頁搬遷）。
//
// 🔴 SSR 打真實教練／職員 API（8 筆，與 mockup 原本讀 site/src/data/staff.json
// 的 8 筆一一對應）。兩個已知落差（回報用，不影響 compare-dom，見下方說明）：
//   1. StaffDto.bio 種子資料全為 NULL（含「陳曉明」——mockup 原本有一句手寫簡介
//      「依俱樂部 2025 年 11 月 3 日發布之消息……」，見 apps/api README「已知落差」
//      同類問題）。這句簡介只會出現在彈窗（people-modal__bio），彈窗內容原本就是
//      JS 點擊後才寫入、SSR 輸出時是空的，所以「拿不到這句話」不會讓 compare-dom
//      比對不過，但功能上這句真實的公告文字目前顯示不出來，照實回報不自行編造。
//   2. B-11（2026-10-06）：讀 StaffDto.staffGroup（後台 C3 分組：管理層／行政／醫療／後勤）分組顯示。
//      沒有分組（NULL，含種子全部教練）者仍歸「教練團」；NULL 且 title==='顧問' 者仍歸「顧問」（種子唯一的陳曉明，
//      與 mockup 的顧問分區一致）。四個分組有人才輸出對應區塊，後台新增職員選了分組就會出現在對的位置。
//   3. 卡片顯示順序（總教練→教練→守門員教練→體能教練→青訓總監→青訓教練→顧問）
//      是 mockup 既有的人工編排順序，API 沒有 sort_order 欄位可用，這裡用姓名
//      對照表排序重建，仍是真實姓名資料只是補上顯示順序，不是編資料內容。
definePageMeta({ nav: 'about', unit: '02', enReady: true, enReadyBw: true })

// 文案依俱樂部切換：hero／SEO 取自 shared/utils/club-copy.ts；名單本身走既有 API
// （動態內容，不進 club-copy.ts）——藍鯨 staff 表目前 0 筆真實資料（客戶尚未提供，
// 不臆造），下方名單區塊會自然顯示空清單，不沿用磐石教練團頂替。
const config = useRuntimeConfig()
const club = config.public.club
const clubKey = computed<'tcrfc' | 'bw'>(() => (club === 'bw' ? 'bw' : 'tcrfc'))
const identity = computed(() => (isEn.value ? getClubIdentityEnFor(clubKey.value) : getClubIdentity(clubKey.value)))
const hero = computed(() => (isEn.value ? (clubKey.value === 'bw' ? OUR_PEOPLE_HERO_EN_BW : OUR_PEOPLE_HERO_EN) : OUR_PEOPLE_HERO[clubKey.value]))
const seo = computed(() => (isEn.value ? (clubKey.value === 'bw' ? OUR_PEOPLE_SEO_EN_BW : OUR_PEOPLE_SEO_EN) : OUR_PEOPLE_SEO[clubKey.value]))

// S1-13 判斷：這頁維持既有「一律同時抓 zh 與 en 兩種名稱」設計不變（不是本輪的
// lang 參數 bug），只補上麵包屑連結要跟著目前路由語系走（見下方樣板 lp() 呼叫）——
// 這頁本身固定顯示「中文姓名為主、英文姓名為輔」的卡片版面，/en/about/our-people/
// 這個孿生路由目前會顯示同一種版面（不是英文為主的版面），這是刻意先不做的頁面內容
// 設計決策，留給往後真的要做「英文為主」版面時再處理，不在 S1-13 框架範圍內。
const { lp, isEn, tx } = useLocale()

/** 後台 C3 分組值域（資料庫直接存的中文字面值，同英文版顯示名稱）。順序即前台區塊順序。 */
const STAFF_GROUPS = [
  { value: '管理層', zh: '管理層', en: 'Management', kicker: 'MANAGEMENT' },
  { value: '行政', zh: '行政', en: 'Administration', kicker: 'ADMINISTRATION' },
  { value: '醫療', zh: '醫療', en: 'Medical Staff', kicker: 'MEDICAL' },
  { value: '後勤', zh: '後勤', en: 'Operations', kicker: 'OPERATIONS' },
] as const

const [{ data: zhData }, { data: enData }] = await Promise.all([
  useFetch(`/api/backend/${club}/staff`, { query: { pageSize: 100, lang: 'zh' } }),
  useFetch(`/api/backend/${club}/staff`, { query: { pageSize: 100, lang: 'en' } }),
])

// mockup 既有的顯示順序與 data-person slug（用真實姓名比對，不是虛構資料）
const DISPLAY_ORDER = [
  { name: '瑪蒂諾', slug: 'matino' },
  { name: '托馬斯・卡斯泰洛', slug: 'thomas' },
  { name: '儒利亞諾・羅德里格斯', slug: 'juliano' },
  { name: '江奕璠', slug: 'jiang' },
  { name: '徐翊', slug: 'xu' },
  { name: '許志傑', slug: 'xu-zhijie' },
  { name: '黃聖傑', slug: 'huang' },
  { name: '陳曉明', slug: 'chen' },
]

interface PersonCard {
  id: string
  slug: string
  nameZh: string
  nameEn: string | null
  /** 卡片上顯示的職稱——mockup 對陳曉明的卡片刻意寫「技術顧問」（比 API title
   * 「顧問」更完整），彈窗與其他 7 人一律用 API title，只有這一筆是逐字比對
   * mockup 找到的既有落差，不是新造規則。 */
  cardRole: string | null
  role: string | null
  /** 後台 C3 分組原值（未分組為 null）。 */
  staffGroup: string | null
  bio: string | null
  photoKey: string | null
  /** GEO-05／S1-12f：apps/api 算好的 Person 結構化資料合格判斷（只要求姓名），
   * 單一來源見 apps/api/Features/Seo/SchemaCompleteness.cs（E-39，這裡不重新判斷一次）。 */
  schemaEligible: boolean
  /** 已套用肖像同意 fail-closed 規則後的完整照片網址，未同意者恆為 null（S1-7a）。 */
  photoUrl: string | null
  /** 英文版（/en/）顯示用：姓名、職稱、簡介一律取資料庫 en 側表已有的值，沒有就退回中文原值（不自行音譯／翻譯）。 */
  displayName: string
  displayRole: string | null
  displayBio: string | null
}

// mockup 卡片顯示職稱與 API title 不同的唯一例外（B-11：只在 API 職稱仍是「顧問」時才套用，後台改了職稱就以後台為準）
const CARD_ROLE_OVERRIDE: Record<string, string> = { chen: '技術顧問' }
// 英文版同一張卡片的職稱：優先用 en 側表的職稱；沒有時，陳曉明的卡片沿用既有的「技術顧問」覆寫（對應 Technical Adviser）。
const CARD_ROLE_OVERRIDE_EN: Record<string, string> = { chen: 'Technical Adviser' }

const people = computed<PersonCard[]>(() => {
  const zh = zhData.value?.items ?? []
  const enItems = new Map((enData.value?.items ?? []).map((s) => [s.id, s]))
  const enById = new Map((enData.value?.items ?? []).map((s) => [s.id, s.name]))
  const bySlug = new Map(DISPLAY_ORDER.map((d) => [d.name, d.slug]))
  return zh
    .map((s) => {
      const en = enById.get(s.id) ?? null
      const slug = bySlug.get(s.name ?? '') ?? s.id
      return {
        id: s.id,
        slug,
        nameZh: s.name ?? '',
        nameEn: en && en !== s.name ? en : null,
        cardRole: (s.title === '顧問' ? CARD_ROLE_OVERRIDE[slug] : null) ?? s.title,
        role: s.title,
        staffGroup: STAFF_GROUPS.some((g) => g.value === s.staffGroup) ? (s.staffGroup as string) : null,
        bio: s.bio,
        photoKey: s.photoKey,
        schemaEligible: s.schemaEligible,
        photoUrl: s.photoUrl,
        displayName: en && en !== s.name ? en : (s.name ?? ''),
        // 有翻譯的英文職稱優先；英文側表沒填（回傳值等同中文職稱）且是既有「技術顧問」例外才用覆寫。
        displayRole: ((enItems.get(s.id)?.title && enItems.get(s.id)?.title !== s.title) ? enItems.get(s.id)?.title : null)
          ?? (s.title === '顧問' ? CARD_ROLE_OVERRIDE_EN[slug] : null) ?? enItems.get(s.id)?.title ?? s.title,
        displayBio: enItems.get(s.id)?.bio ?? null,
      }
    })
    // 組內順序：DISPLAY_ORDER 名單內的人照名單；名單外的人依 API 回傳順序接在後面（Array.sort 為穩定排序）。
    .sort((a, b) => {
      const ia = DISPLAY_ORDER.findIndex((d) => d.name === a.nameZh)
      const ib = DISPLAY_ORDER.findIndex((d) => d.name === b.nameZh)
      return (ia === -1 ? 999 : ia) - (ib === -1 ? 999 : ib)
    })
})

// 未分組者：教練團（含種子全部教練）；未分組且職稱是「顧問」者：顧問（既有分區）。
const coaching = computed(() => people.value.filter((p) => !p.staffGroup && p.role !== '顧問'))
const advisory = computed(() => people.value.filter((p) => !p.staffGroup && p.role === '顧問'))
const groupedSections = computed(() => STAFF_GROUPS
  .map((g) => ({ ...g, people: people.value.filter((p) => p.staffGroup === g.value) }))
  .filter((g) => g.people.length > 0))

const bioPlaceholder = computed(() => tx('簡介準備中，稍後將於本頁公開。', 'A profile is being prepared and will be published here soon.'))

const modalOpen = ref(false)
const activePerson = ref<PersonCard | null>(null)
const closeBtnEl = ref<HTMLElement | null>(null)
let lastFocused: HTMLElement | null = null

function openModal(person: PersonCard) {
  activePerson.value = person
  modalOpen.value = true
  lastFocused = document.activeElement as HTMLElement | null
  document.body.style.overflow = 'hidden'
  nextTick(() => closeBtnEl.value?.focus())
}
function closeModal() {
  modalOpen.value = false
  document.body.style.overflow = ''
  lastFocused?.focus()
}
function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape' && modalOpen.value) closeModal()
}
onMounted(() => document.addEventListener('keydown', onKeydown))
onBeforeUnmount(() => document.removeEventListener('keydown', onKeydown))

useSeoMeta({
  title: computed(() => seo.value.title),
  description: computed(() => seo.value.description),
})

// Person JSON-LD（GEO-05／S1-12f）：本頁是「教練頁面」候選中唯一目前有真實資料可顯示的頁面
// （8 位真實教練／顧問，藍鯨目前 0 筆則整段不輸出，見上方 people 的既有落差說明）。逐人依
// schemaEligible 過濾（Person 只要求姓名，理論上恆為 true，仍照單一來源機制走，不因為
// 「反正都會是 true」就省略檢查，E-39）；image 只在 photoUrl 有值（＝已同意肖像使用，S1-7a）
// 時才帶，未同意者不得輸出照片。用 useSchemaOrg／definePerson（有專用定義器可用，比照
// news/[slug] 頁 Article 的既有寫法，不像 SportsTeam 要手刻原始 JSON-LD）。
watchEffect(() => {
  const eligible = people.value.filter((p) => p.schemaEligible)
  if (eligible.length === 0) return
  useSchemaOrg(
    eligible.map((p) =>
      definePerson({
        // 🔴 必須明確給每人一個唯一 @id：definePerson 預設 @id 是站台身分節點（#identity），多人共用同一個
        // @id 會被 nuxt-schema-org 合併成「一個」Person（8 位教練只輸出最後一位，且被當成整站的主體）。
        // 見 docs/18-work-errors.md E-96。
        '@id': `person-${p.id}`,
        name: isEn.value ? p.displayName : p.nameZh,
        jobTitle: (isEn.value ? p.displayRole : p.role) ?? undefined,
        image: p.photoUrl ?? undefined,
      }),
    ),
  )
})
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/about/')">{{ identity.aboutLabelZh }}</a></li>
      <li aria-current="page">{{ tx('團隊成員', 'Our People') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <ClubHeroBg :src="siteImg('/assets/img/nav-about.jpg')" width="1600" height="900" />
  <div class="container">
    <p class="page-hero__eyebrow">{{ isEn ? aboutEyebrowEn('2.4', clubKey) : aboutEyebrow('2.4', clubKey) }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<section v-if="coaching.length || !people.length" class="band people-band" aria-labelledby="people-coaching-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">COACHING STAFF</p>
        <h2 class="section-title" id="people-coaching-title">{{ tx('教練團', 'Coaching Staff') }}</h2>
      </div>
    </div>

    <div class="people-grid">
      <button v-for="p in coaching" :key="p.id" type="button" class="people-card" :data-person="p.slug" @click="openModal(p)">
        <span class="people-card__role">{{ isEn ? p.displayRole : p.cardRole }}</span>
        <span class="people-card__name">{{ isEn ? p.displayName : p.nameZh }}<span v-if="p.nameEn && !isEn" class="en">{{ p.nameEn }}</span></span>
      </button>
    </div>
  </div>
</section>

<!-- B-11：後台分組（管理層／行政／醫療／後勤），有人才輸出 -->
<section v-for="(g, gi) in groupedSections" :key="g.value" class="band people-band" :style="gi % 2 === 0 ? 'background:var(--paper-2);' : undefined" :aria-labelledby="`people-group-${gi}-title`">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">{{ g.kicker }}</p>
        <h2 class="section-title" :id="`people-group-${gi}-title`">{{ isEn ? g.en : g.zh }}</h2>
      </div>
    </div>
    <div class="people-grid">
      <button v-for="p in g.people" :key="p.id" type="button" class="people-card" :data-person="p.slug" @click="openModal(p)">
        <span class="people-card__role">{{ isEn ? p.displayRole : p.cardRole }}</span>
        <span class="people-card__name">{{ isEn ? p.displayName : p.nameZh }}<span v-if="p.nameEn && !isEn" class="en">{{ p.nameEn }}</span></span>
      </button>
    </div>
  </div>
</section>

<section v-if="advisory.length" class="band grain grain--2 people-band people-band--advisory" aria-labelledby="people-advisory-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker kicker--on-dark">ADVISORY</p>
        <h2 class="section-title" id="people-advisory-title" style="color:#fff;">{{ tx('顧問', 'Advisers') }}</h2>
      </div>
    </div>

    <div class="people-grid people-grid--on-dark">
      <button v-for="p in advisory" :key="p.id" type="button" class="people-card people-card--on-dark" :data-person="p.slug" @click="openModal(p)">
        <span class="people-card__role">{{ isEn ? p.displayRole : p.cardRole }}</span>
        <span class="people-card__name">{{ isEn ? p.displayName : p.nameZh }}<span v-if="p.nameEn && !isEn" class="en">{{ p.nameEn }}</span></span>
      </button>
    </div>
  </div>
</section>

<!-- 人員詳情彈窗 -->
<div class="people-modal" id="people-modal" :hidden="!modalOpen">
  <div class="people-modal__scrim" data-close @click="closeModal"></div>
  <div class="people-modal__panel" role="dialog" aria-modal="true" aria-labelledby="people-modal-name">
    <button ref="closeBtnEl" type="button" class="people-modal__close" data-close :aria-label="tx('關閉', 'Close')" @click="closeModal">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M6 6l12 12M18 6L6 18" /></svg>
    </button>
    <span class="people-modal__photo" id="people-modal-photo" aria-hidden="true"></span>
    <p class="people-modal__role" id="people-modal-role">{{ isEn ? activePerson?.displayRole : activePerson?.role }}</p>
    <h3 class="people-modal__name" id="people-modal-name">{{ isEn ? activePerson?.displayName : (activePerson?.nameEn ? `${activePerson.nameZh} ${activePerson.nameEn}` : activePerson?.nameZh) }}</h3>
    <p class="people-modal__bio" id="people-modal-bio">{{ activePerson ? ((isEn ? activePerson.displayBio : activePerson.bio) || bioPlaceholder) : '' }}</p>
  </div>
</div>
</template>

<style>
/* 【2.4 Our People】人員卡片列表 + 彈窗詳情 —— 若球員/教練頁也需要同款彈窗，建議收進共用 CSS/JS */
.people-band{ background:var(--paper); padding-block:clamp(3.5rem,6vw,5.5rem); }
.people-band--advisory{ color:#fff; }
.people-band--advisory .section-lede{ color:var(--muted-dark); }

.people-grid{
  display:grid; gap:1.5rem;
  grid-template-columns:repeat(auto-fill,minmax(200px,1fr));
}
.people-card{
  display:flex; flex-direction:column; align-items:center; text-align:center; gap:.6rem;
  background:var(--paper-2); border:1px solid var(--rule); padding:1.75rem 1.25rem;
  cursor:pointer; transition:transform var(--dur-fast) var(--ease), border-color var(--dur-fast) var(--ease);
}
.people-card:hover{ transform:translateY(-4px); border-color:var(--brand-aa); }
.people-card--on-dark{ background:rgba(255,255,255,.05); border-color:rgba(255,255,255,.15); }
.people-card--on-dark:hover{ border-color:var(--brand-bright); }
.people-card__photo{
  width:84px; height:84px; border-radius:50%; display:flex; align-items:center; justify-content:center;
  font-size:1.6rem; font-weight:800; color:var(--muted);
}
.people-card__photo--pending{ background:var(--rule); }
.people-card--on-dark .people-card__photo--pending{ background:rgba(255,255,255,.12); color:var(--muted-dark); }
.people-card__role{ font-size:.72rem; font-weight:800; letter-spacing:.06em; color:var(--brand-aa); text-transform:uppercase; }
.people-card--on-dark .people-card__role{ color:var(--brand); }
.people-card__name{ font-size:1rem; font-weight:800; color:var(--heading); }
.people-card--on-dark .people-card__name{ color:#fff; }
.people-card__name .en{ display:block; font-size:.76rem; font-weight:600; color:var(--muted); margin-top:.2rem; }
.people-card--on-dark .people-card__name .en{ color:var(--muted-dark); }

/* 彈窗 */
.people-modal{ position:fixed; inset:0; z-index:200; display:flex; align-items:center; justify-content:center; padding:1.25rem; }
.people-modal[hidden]{ display:none; }
.people-modal__scrim{ position:absolute; inset:0; background:rgba(35,25,22,.7); }
.people-modal__panel{
  position:relative; z-index:1; background:var(--paper); max-width:440px; width:100%;
  padding:2.5rem 2rem 2rem; text-align:center;
  clip-path: polygon(0 0, 100% 0, 100% calc(100% - 28px), calc(100% - 28px) 100%, 0 100%);
}
.people-modal__close{
  position:absolute; top:1rem; right:1rem; width:36px; height:36px;
  display:flex; align-items:center; justify-content:center; color:var(--muted);
}
.people-modal__close:hover{ color:var(--brand-aa); }
.people-modal__photo{
  display:block; width:96px; height:96px; border-radius:50%; margin:0 auto 1.1rem;
  background:var(--rule);
}
.people-modal__role{ font-size:.76rem; font-weight:800; letter-spacing:.06em; color:var(--brand-aa); text-transform:uppercase; }
.people-modal__name{ font-size:1.4rem; font-weight:900; color:var(--heading); margin:.4rem 0 1.25rem; }
.people-modal__bio{ text-align:left; font-size:.9rem; line-height:1.7; color:var(--muted); }
</style>

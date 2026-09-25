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
//   2. StaffDto 沒有「教練團／顧問」分類欄位（staffGroup 種子全為 NULL），
//      改用 title==='顧問' 判斷（API 資料裡目前唯一符合的就是陳曉明，與 mockup
//      的顧問分區一致），不是新發明的規則。
//   3. 卡片顯示順序（總教練→教練→守門員教練→體能教練→青訓總監→青訓教練→顧問）
//      是 mockup 既有的人工編排順序，API 沒有 sort_order 欄位可用，這裡用姓名
//      對照表排序重建，仍是真實姓名資料只是補上顯示順序，不是編資料內容。
definePageMeta({ nav: 'about', unit: '02' })

// 文案依俱樂部切換：hero／SEO 取自 shared/utils/club-copy.ts；名單本身走既有 API
// （動態內容，不進 club-copy.ts）——藍鯨 staff 表目前 0 筆真實資料（客戶尚未提供，
// 不臆造），下方名單區塊會自然顯示空清單，不沿用磐石教練團頂替。
const config = useRuntimeConfig()
const club = config.public.club
const clubKey = computed<'tcrfc' | 'bw'>(() => (club === 'bw' ? 'bw' : 'tcrfc'))
const identity = computed(() => getClubIdentity(clubKey.value))
const hero = computed(() => OUR_PEOPLE_HERO[clubKey.value])

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
  bio: string | null
  photoKey: string | null
  /** GEO-05／S1-12f：apps/api 算好的 Person 結構化資料合格判斷（只要求姓名），
   * 單一來源見 apps/api/Features/Seo/SchemaCompleteness.cs（E-39，這裡不重新判斷一次）。 */
  schemaEligible: boolean
  /** 已套用肖像同意 fail-closed 規則後的完整照片網址，未同意者恆為 null（S1-7a）。 */
  photoUrl: string | null
}

// mockup 卡片顯示職稱與 API title 不同的唯一例外
const CARD_ROLE_OVERRIDE: Record<string, string> = { chen: '技術顧問' }

const people = computed<PersonCard[]>(() => {
  const zh = zhData.value?.items ?? []
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
        cardRole: CARD_ROLE_OVERRIDE[slug] ?? s.title,
        role: s.title,
        bio: s.bio,
        photoKey: s.photoKey,
        schemaEligible: s.schemaEligible,
        photoUrl: s.photoUrl,
      }
    })
    .sort((a, b) => {
      const ia = DISPLAY_ORDER.findIndex((d) => d.name === a.nameZh)
      const ib = DISPLAY_ORDER.findIndex((d) => d.name === b.nameZh)
      return (ia === -1 ? 999 : ia) - (ib === -1 ? 999 : ib)
    })
})

const coaching = computed(() => people.value.filter((p) => p.role !== '顧問'))
const advisory = computed(() => people.value.filter((p) => p.role === '顧問'))

const BIO_PLACEHOLDER = '簡介準備中，稍後將於本頁公開。'

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
  title: computed(() => OUR_PEOPLE_SEO[clubKey.value].title),
  description: computed(() => OUR_PEOPLE_SEO[clubKey.value].description),
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
        name: p.nameZh,
        jobTitle: p.role ?? undefined,
        image: p.photoUrl ?? undefined,
      }),
    ),
  )
})
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a href="/zh/">首頁</a></li>
      <li><a href="/zh/about/">{{ identity.aboutLabelZh }}</a></li>
      <li aria-current="page">團隊成員</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <img class="page-hero__bg" src="/assets/img/nav-about.jpg" alt="" width="1600" height="900">
  <div class="container">
    <p class="page-hero__eyebrow">{{ aboutEyebrow('2.4', clubKey) }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<section class="band people-band" aria-labelledby="people-coaching-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">COACHING STAFF</p>
        <h2 class="section-title" id="people-coaching-title">教練團</h2>
      </div>
    </div>

    <div class="people-grid">
      <button v-for="p in coaching" :key="p.id" type="button" class="people-card" :data-person="p.slug" @click="openModal(p)">
        <span class="people-card__role">{{ p.cardRole }}</span>
        <span class="people-card__name">{{ p.nameZh }}<span v-if="p.nameEn" class="en">{{ p.nameEn }}</span></span>
      </button>
    </div>
  </div>
</section>

<section class="band grain grain--2 people-band people-band--advisory" aria-labelledby="people-advisory-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker kicker--on-dark">ADVISORY</p>
        <h2 class="section-title" id="people-advisory-title" style="color:#fff;">顧問</h2>
      </div>
    </div>

    <div class="people-grid people-grid--on-dark">
      <button v-for="p in advisory" :key="p.id" type="button" class="people-card people-card--on-dark" :data-person="p.slug" @click="openModal(p)">
        <span class="people-card__role">{{ p.cardRole }}</span>
        <span class="people-card__name">{{ p.nameZh }}<span v-if="p.nameEn" class="en">{{ p.nameEn }}</span></span>
      </button>
    </div>
  </div>
</section>

<!-- 人員詳情彈窗 -->
<div class="people-modal" id="people-modal" :hidden="!modalOpen">
  <div class="people-modal__scrim" data-close @click="closeModal"></div>
  <div class="people-modal__panel" role="dialog" aria-modal="true" aria-labelledby="people-modal-name">
    <button ref="closeBtnEl" type="button" class="people-modal__close" data-close aria-label="關閉" @click="closeModal">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M6 6l12 12M18 6L6 18" /></svg>
    </button>
    <span class="people-modal__photo" id="people-modal-photo" aria-hidden="true"></span>
    <p class="people-modal__role" id="people-modal-role">{{ activePerson?.role }}</p>
    <h3 class="people-modal__name" id="people-modal-name">{{ activePerson?.nameEn ? `${activePerson.nameZh} ${activePerson.nameEn}` : activePerson?.nameZh }}</h3>
    <p class="people-modal__bio" id="people-modal-bio">{{ activePerson ? (activePerson.bio || BIO_PLACEHOLDER) : '' }}</p>
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

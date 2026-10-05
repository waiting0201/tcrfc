<script setup lang="ts">
// app/pages/zh/programs/[slug]/index.vue — 課程詳情（依 API 的 `programs.slug` 動態渲染）
//
// App 規劃書 §2.3：`tcrfc://program/{slug}` → `/zh/programs/{slug}`，slug 是 `GET /api/v1/{club}/programs/{slug}`
// （`ProgramDetailDto`）的 slug。官網另有五個靜態課程頁（`summer-camp`／`winter-camp`／`childrens-training`／
// `school-community`／`specialist`），Nuxt 靜態路徑優先於動態區段，這五個網址不會進本檔；其餘 slug 進這裡：
//   - 查得到（已發布、同俱樂部）→ 渲染詳情，版型沿用既有課程頁（page-hero／prose／梯次資訊卡／報名元件）；
//   - 查無資料或後端回錯 → **不 404**，302 回 `/programs/`（App 未安裝時回退網址不得顯示錯誤頁，docs/19 §2）。
// 資料不足不輸出 Course Schema：由 `useCourseSchema` → `buildCourseSchemaNode` 判斷（名稱與說明缺一即不輸出）。
// 線上報名只對磐石開放（藍鯨的線上報名與收費待確認，藍鯨規劃書 §10 第 8 點，與五個靜態頁同一規則）。
// `content` 當純文字顯示（空行分段），**不使用 v-html**。
import type { ProgramSessionLike } from '~/utils/program-session'

definePageMeta({ nav: 'programs', unit: '05' })

interface ProgramDetail {
  id: string
  slug: string
  audience: string | null
  ageMin: number | null
  ageMax: number | null
  coverUrl: string | null
  name: string | null
  intro: string | null
  content: string | null
  staff: Array<{ id: string, name: string | null }>
  partners: Array<{ id: string, slug: string, name: string | null, websiteUrl: string | null }>
  sessions: Array<ProgramSessionLike & {
    id: string
    startOn: string | null
    endOn: string | null
    weeklySchedule: string | null
    capacity: number | null
    enrolledCount: number
    price: number | null
    earlyBirdPrice: number | null
    earlyBirdUntil: string | null
    venueName: string | null
    venueAddress: string | null
  }>
}

const { lp, locale } = useLocale()
const route = useRoute()
const config = useRuntimeConfig()
const club = config.public.club
const clubAssets = computed(() => getClubAssets(club))
const isTcrfc = computed(() => club !== 'bw')
const slug = String(route.params.slug ?? '').trim()

const { data: program, error } = await useFetch<ProgramDetail>(`/api/backend/${club}/programs/${encodeURIComponent(slug)}`, {
  query: { lang: locale.value },
  key: `program-detail-${club}-${slug}-${locale.value}`,
})
if (error.value || !program.value) {
  await navigateTo(lp('/zh/programs/'), { redirectCode: 302, replace: true })
}

const paragraphs = computed(() => (program.value?.content ?? '').split(/\n{2,}/).map((p) => p.trim()).filter(Boolean))
const openSessions = computed(() => (program.value?.sessions ?? []).filter((s) => isSessionRegistrable(s)))
const registrable = computed(() => (isTcrfc.value && program.value && openSessions.value.length > 0
  ? [{ slug: program.value.slug, name: program.value.name, sessions: openSessions.value }]
  : []))
const ageText = computed(() => {
  const p = program.value
  if (!p || p.ageMin == null) return null
  return p.ageMax != null ? `${p.ageMin}–${p.ageMax} 歲` : `${p.ageMin} 歲以上`
})

useSeoMeta({
  title: computed(() => `${program.value?.name ?? '課程'}｜課程與活動｜${clubAssets.value.nameZh}`),
  description: computed(() => program.value?.intro ?? `${clubAssets.value.nameZh}的課程資訊。`),
})

const siteConfig = useSiteConfig()
useCourseSchema(
  computed(() => (program.value
    ? { name: program.value.name ?? null, intro: program.value.intro ?? null, ageMin: program.value.ageMin ?? null, ageMax: program.value.ageMax ?? null }
    : null)),
  { providerName: clubAssets.value.nameZh, siteUrl: computed(() => siteConfig.url ?? '') },
)
</script>

<template>
<template v-if="program">
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/programs/')">課程與活動</a></li>
      <li aria-current="page">{{ program.name }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero" :class="{ 'page-hero--media': program.coverUrl }">
  <img v-if="program.coverUrl" class="page-hero__bg" :src="program.coverUrl" alt="" width="1600" height="1200">
  <div class="container">
    <p class="page-hero__eyebrow">Programs</p>
    <h1>{{ program.name }}</h1>
    <p v-if="program.intro" class="page-hero__lede">{{ program.intro }}</p>
  </div>
</section>

<section class="band">
  <div class="container">
    <div class="grid grid--2" style="align-items:start;">
      <div class="prose">
        <h2>課程內容</h2>
        <p v-if="program.audience || ageText">
          <template v-if="program.audience">適合對象：{{ program.audience }}</template>
          <template v-if="program.audience && ageText">｜</template>
          <template v-if="ageText">年齡：{{ ageText }}</template>
        </p>
        <p v-for="(para, i) in paragraphs" :key="i">{{ para }}</p>
        <p v-if="!paragraphs.length && !program.intro" class="is-pending">課程內容整理中，稍後公布。</p>
      </div>
      <div class="prose">
        <template v-if="program.staff.length">
          <h2>教練團</h2>
          <ul><li v-for="s in program.staff" :key="s.id">{{ s.name }}</li></ul>
        </template>
        <template v-if="program.partners.length">
          <h2>合作夥伴</h2>
          <ul>
            <li v-for="p in program.partners" :key="p.id">
              <a v-if="p.websiteUrl && /^https:\/\//i.test(p.websiteUrl)" :href="p.websiteUrl" target="_blank" rel="noopener noreferrer">{{ p.name }}</a>
              <template v-else>{{ p.name }}</template>
            </li>
          </ul>
        </template>
      </div>
    </div>
  </div>
</section>

<section class="band" style="background:var(--paper-2);">
  <div class="container">
    <div class="prose"><h2>梯次與地點</h2></div>
    <p v-if="!program.sessions.length" class="is-pending">梯次尚未公布。</p>
    <div v-for="s in program.sessions" :key="s.id" class="signup-preview" style="margin-bottom:1rem;">
      <div class="signup-preview__row"><span>日期</span><span class="signup-preview__value">{{ s.startOn ?? '待公告' }}<template v-if="s.endOn"> ～ {{ s.endOn }}</template></span></div>
      <div v-if="s.weeklySchedule" class="signup-preview__row"><span>時段</span><span class="signup-preview__value">{{ s.weeklySchedule }}</span></div>
      <div v-if="s.venueName" class="signup-preview__row"><span>地點</span><span class="signup-preview__value">{{ s.venueName }}</span></div>
      <div class="signup-preview__row"><span>費用</span><span class="signup-preview__value">{{ s.price != null ? `NT$ ${s.price}` : '待公告' }}</span></div>
      <div v-if="s.capacity" class="signup-preview__row"><span>剩餘名額</span><span class="signup-preview__value">{{ Math.max(s.capacity - s.enrolledCount, 0) }}</span></div>
    </div>
  </div>
</section>

<ProgramRegistration v-if="registrable.length" :programs="registrable" />

<section class="band">
  <div class="container">
    <div class="hero__ctas">
      <a v-if="registrable.length" class="btn btn--primary" href="#register">線上報名</a>
      <a v-else-if="isTcrfc" class="btn btn--primary" :href="lp('/zh/join/camp-registration/')">加入候補通知</a>
      <a class="btn btn--dark" :href="lp('/zh/programs/')">回課程總覽</a>
    </div>
  </div>
</section>
</template>
</template>

<style>
/* 與五個靜態課程頁共用的小元件樣式（各頁 style 非 scoped、只隨該頁載入，動態頁需自帶一份） */
.signup-preview{ margin-top:1rem; border:1px solid var(--rule); max-width:420px; }
.signup-preview__row{ display:flex; justify-content:space-between; gap:1rem; padding:.9rem 1.25rem; font-size:.85rem; border-bottom:1px solid var(--rule); }
.signup-preview__row:last-child{ border-bottom:none; }
.signup-preview__value{ font-weight:700; color:var(--muted); text-align:right; }
.is-pending{ color:var(--muted); font-style:italic; }
</style>

<script setup lang="ts">
// app/components/ProgramInfoBand.vue — 05 固定頁 5.1–5.5 的「課程介紹」區塊（B-8）
//
// 左欄：每個課程的封面、名稱、簡介、適合對象與年齡區間、課程內容（區塊 JSON 走 PageBlocks）。
// 右欄：教練團（姓名；照片屬肖像同意白名單，只在 /staff 輸出，見 ProgramStaffSummaryDto）；`showPartners` 時再列合作夥伴。
// 同類型有多個課程時全部列出（≥2 個才顯示各課程小標，避免單一課程重複標題）。
// 沒有任何課程資料時由頁面決定要不要渲染本元件（沿用既有寫死內容）；個別欄位空時用 slot 放既有占位。
const props = withDefaults(defineProps<{
  programs: ProgramDetailView[]
  contentTitle?: string
  staffTitle?: string
  partnersTitle?: string
  showPartners?: boolean
  bandStyle?: string
}>(), { contentTitle: undefined, staffTitle: undefined, partnersTitle: undefined, showPartners: false, bandStyle: undefined })

const { tx, isEn } = useLocale()
const mediaBaseUrl = useRuntimeConfig().public.mediaBaseUrl as string
const items = computed(() => props.programs.map((p) => ({
  p,
  age: programAgeText(p, isEn.value),
  body: buildProgramContent(p.content, isEn.value ? 'en' : 'zh', mediaBaseUrl),
})))
const staff = computed(() => uniqueById(props.programs.map((p) => p.staff)))
const hasPartners = computed(() => props.programs.some((p) => p.partners.length > 0))
const multi = computed(() => props.programs.length > 1)
</script>

<template>
  <section class="band" :style="bandStyle">
    <div class="container">
      <div class="grid grid--2" style="align-items:start;">
        <div class="prose program-info">
          <h2>{{ contentTitle ?? tx('課程內容', 'Program Details') }}</h2>
          <article v-for="it in items" :key="it.p.id" class="program-info__item">
            <img v-if="it.p.coverUrl" class="program-info__cover" :src="it.p.coverUrl" alt="" loading="lazy" width="800" height="600">
            <h3 v-if="multi">{{ it.p.name }}</h3>
            <p v-if="it.p.intro">{{ it.p.intro }}</p>
            <p v-if="it.p.audience || it.age">
              <template v-if="it.p.audience">{{ tx('適合對象：', 'Suitable for: ') }}{{ it.p.audience }}</template>
              <template v-if="it.p.audience && it.age">{{ tx('｜', ' | ') }}</template>
              <template v-if="it.age">{{ tx('年齡：', 'Age: ') }}{{ it.age }}</template>
            </p>
            <ContentPageBlocks v-if="it.body.blocks.length" :blocks="it.body.blocks" />
            <p v-for="(para, i) in it.body.paragraphs" :key="i">{{ para }}</p>
          </article>
          <slot v-if="!items.length" name="empty-content" />
        </div>
        <div class="prose">
          <h2>{{ staffTitle ?? tx('教練團', 'Coaching Team') }}</h2>
          <ul v-if="staff.length"><li v-for="s in staff" :key="s.id">{{ s.name }}</li></ul>
          <slot v-else name="empty-staff"><p class="is-pending">{{ tx('教練團陣容將於梯次公告時同步發布。', 'The coaching team will be announced together with the session announcement.') }}</p></slot>
          <template v-if="showPartners && hasPartners">
            <h2>{{ partnersTitle ?? tx('合作夥伴', 'Partners') }}</h2>
            <ProgramPartnersList :programs="programs" />
          </template>
        </div>
      </div>
    </div>
  </section>
</template>

<style>
.program-info__item + .program-info__item{ margin-top:2rem; padding-top:2rem; border-top:1px solid var(--rule); }
.program-info__cover{ width:100%; max-width:520px; aspect-ratio:4/3; object-fit:cover; display:block; margin-bottom:1rem; }
.is-pending{ color:var(--muted); font-style:italic; }
</style>

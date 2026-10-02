<script setup lang="ts">
// pages/[lang]/donors/index.vue — 捐款徵信名單（docs/22-charity-ui.md §2.8，規劃書 §3.6）。
//
// 規則（規劃書 §3.6、§11.1）：
//   · 只列出捐款時明示選擇「具名」的捐款人「姓名」；不顯示金額、Email、店家、單號、時間，也不顯示捐款次數。
//   · 可依項目與期間篩選；篩選條件放在網址上（可分享、重新整理後仍保留）。
//   · 後台可整站關閉（或逐筆隱藏）：關閉時這個網址仍然可以打開（連結可能已被分享出去），
//     顯示一般的「本頁功能暫未開放」頁面，不是 404（docs/22 §2.8）。
//   · 規劃書 v1.1 起徵信頁需 `noindex`。
import { useLang } from '../../../composables/useLang'
import { useHreflang } from '../../../composables/useHreflang'

definePageMeta({ layout: 'default' })

const route = useRoute()
const { lang, tr, tt } = useLang()
useHreflang('/donors/')

useHead(() => ({
  title: `${tr.value.donors.heading} | ${tr.value.associationName}`,
  meta: [{ name: 'robots', content: 'noindex, nofollow' }],
}))

function one(value: unknown): string {
  const v = Array.isArray(value) ? value[0] : value
  return typeof v === 'string' ? v : ''
}

const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/

const applied = computed(() => {
  const from = one(route.query.from)
  const to = one(route.query.to)
  return {
    projectSlug: one(route.query.project) || undefined,
    from: DATE_PATTERN.test(from) ? from : undefined,
    to: DATE_PATTERN.test(to) ? to : undefined,
    page: Math.max(1, Number.parseInt(one(route.query.page), 10) || 1),
  }
})

const { data: projects } = await useCharityProjects()
const { data: list } = await useCreditList(() => applied.value)

// 篩選表單：先在本地編輯，按「套用篩選」才寫進網址。
const form = reactive({
  project: applied.value.projectSlug ?? '',
  from: applied.value.from ?? '',
  to: applied.value.to ?? '',
})
const rangeInvalid = computed(() => Boolean(form.from && form.to && form.from > form.to))

function toQuery(page: number) {
  const q: Record<string, string> = {}
  if (form.project) q.project = form.project
  if (form.from) q.from = form.from
  if (form.to) q.to = form.to
  if (page > 1) q.page = String(page)
  return q
}

function apply() {
  if (rangeInvalid.value) return
  void navigateTo({ path: `/${lang.value}/donors/`, query: toQuery(1) })
}

function clear() {
  Object.assign(form, { project: '', from: '', to: '' })
  void navigateTo({ path: `/${lang.value}/donors/` })
}

const totalPages = computed(() => (list.value && list.value.pageSize > 0 ? Math.max(1, Math.ceil(list.value.totalCount / list.value.pageSize)) : 1))

function pageLink(page: number) {
  const q: Record<string, string> = {}
  if (applied.value.projectSlug) q.project = applied.value.projectSlug
  if (applied.value.from) q.from = applied.value.from
  if (applied.value.to) q.to = applied.value.to
  if (page > 1) q.page = String(page)
  return { path: `/${lang.value}/donors/`, query: q }
}
</script>

<template>
  <div class="container">
    <section class="section">
      <h1>{{ tr.donors.heading }}</h1>

      <p v-if="!list" class="notice-row" role="alert">{{ tr.donors.loadError }}</p>

      <p v-else-if="!list.enabled" class="text-secondary">{{ tr.donors.disabled }}</p>

      <template v-else>
        <p class="text-secondary">{{ tr.donors.intro }}</p>

        <form class="card stack" style="margin: var(--sp-4) 0;" @submit.prevent="apply">
          <div class="field">
            <label class="field-label" for="donors-project">{{ tr.donors.filterProject }}</label>
            <select id="donors-project" v-model="form.project" class="field-input">
              <option value="">{{ tr.donors.filterAllProjects }}</option>
              <option v-for="p in projects ?? []" :key="p.slug" :value="p.slug">{{ p.name }}</option>
            </select>
          </div>
          <div class="field">
            <label class="field-label" for="donors-from">{{ tr.donors.filterFrom }}</label>
            <input id="donors-from" v-model="form.from" type="date" class="field-input">
          </div>
          <div class="field">
            <label class="field-label" for="donors-to">{{ tr.donors.filterTo }}</label>
            <input id="donors-to" v-model="form.to" type="date" class="field-input" :min="form.from || undefined">
          </div>
          <div style="display: flex; gap: var(--sp-3); flex-wrap: wrap;">
            <button type="submit" class="btn btn-primary" :disabled="rangeInvalid">{{ tr.donors.apply }}</button>
            <button type="button" class="btn btn-secondary" @click="clear">{{ tr.donors.clear }}</button>
          </div>
        </form>

        <p v-if="list.names.length === 0" class="text-secondary">{{ tr.donors.empty }}</p>
        <template v-else>
          <p class="text-tertiary" style="margin-bottom: var(--sp-3);">{{ tt(tr.donors.total, { count: list.totalCount }) }}</p>
          <!-- 姓名以純文字插值輸出（不使用 v-html）。 -->
          <ul class="credit-names">
            <li v-for="(name, i) in list.names" :key="`${i}-${name}`">{{ name }}</li>
          </ul>

          <nav v-if="totalPages > 1" class="credit-pager" aria-label="pagination">
            <NuxtLink v-if="list.page > 1" :to="pageLink(list.page - 1)" class="btn btn-secondary" rel="prev">{{ tr.donors.prev }}</NuxtLink>
            <span class="text-tertiary">{{ tt(tr.donors.pageOf, { page: list.page, pages: totalPages }) }}</span>
            <NuxtLink v-if="list.page < totalPages" :to="pageLink(list.page + 1)" class="btn btn-secondary" rel="next">{{ tr.donors.next }}</NuxtLink>
          </nav>
        </template>
      </template>
    </section>
  </div>
</template>

<style scoped>
.credit-names {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(9rem, 1fr));
  gap: var(--sp-2) var(--sp-4);
}

.credit-names li {
  overflow-wrap: anywhere;
}

.credit-pager {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--sp-4);
  flex-wrap: wrap;
  margin-top: var(--sp-6);
}
</style>

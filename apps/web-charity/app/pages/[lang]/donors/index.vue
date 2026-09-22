<script setup lang="ts">
// pages/[lang]/donors/index.vue — 捐款徵信名單（docs/22-charity-ui.md §2.8，規劃書 §3.6）。
// 僅列出選擇「具名」且已完成付款的捐款人姓名，不顯示金額、Email、店家（見 server/utils/fixtures.ts
// 的 listNamedCompletedDonations 篩選邏輯）。可依項目篩選。
import { useLang } from '../../../composables/useLang'
import { useHreflang } from '../../../composables/useHreflang'

definePageMeta({ layout: 'default' })

const { lang, tr } = useLang()
useHreflang('/donors/')

const { data: donors } = await useFetch('/api/charity/donors')
const { data: projects } = await useFetch('/api/charity/projects')

const selectedProject = ref<string>('')

const filtered = computed(() => {
  if (!donors.value) return []
  if (!selectedProject.value) return donors.value
  return donors.value.filter((d) => d.projectKey === selectedProject.value)
})

function projectLabel(project: { slug: string, name_zh: string, name_en: string }) {
  return lang.value === 'zh' ? project.name_zh : (project.name_en || project.name_zh)
}

useHead({ title: `${tr.value.donors.heading} | ${tr.value.associationName}` })
</script>

<template>
  <div class="container">
    <section class="section">
      <h1>{{ tr.donors.heading }}</h1>
      <p class="text-secondary">{{ tr.donors.hint }}</p>

      <div class="field" style="max-width: 320px;">
        <label class="field-label" for="donor-project-filter">{{ tr.donors.filterProject }}</label>
        <select id="donor-project-filter" v-model="selectedProject" class="field-input">
          <option value="">{{ tr.donors.filterAll }}</option>
          <option v-for="project in projects ?? []" :key="project.slug" :value="project.key">
            {{ projectLabel(project) }}
          </option>
        </select>
      </div>

      <p v-if="filtered.length === 0" class="text-secondary">{{ tr.donors.empty }}</p>
      <ul v-else class="stack" style="list-style: none; padding: 0;">
        <li v-for="(donor, index) in filtered" :key="index" class="card" style="display:flex; justify-content: space-between;">
          <span>{{ donor.donorName }}</span>
          <span class="text-tertiary" style="font-size: 0.875rem;">
            {{ tr.donors.donatedTo }} {{ lang === 'zh' ? donor.projectNameZh : (donor.projectNameEn || donor.projectNameZh) }}
          </span>
        </li>
      </ul>
    </section>
  </div>
</template>

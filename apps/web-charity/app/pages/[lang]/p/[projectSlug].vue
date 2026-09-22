<script setup lang="ts">
// pages/[lang]/p/[projectSlug].vue — 項目詳情頁（docs/22-charity-ui.md §2.4／§2.5，規劃書 §3.2／§3.3）。
// 捐款表單固定在頁面最下方。店家歸屬取值優先序：URL ?s= > Cookie > 無店家（規劃書 §2.2 規則 3）。
import { useLang } from '../../../composables/useLang'
import { useHreflang } from '../../../composables/useHreflang'
import { pickText } from '../../../utils/i18n'

definePageMeta({ layout: 'default' })

const route = useRoute()
const projectSlug = route.params.projectSlug as string

const { lang, tr, tt } = useLang()
useHreflang(`/p/${projectSlug}`)

const { data: project } = await useFetch(`/api/charity/projects/${projectSlug}`)

// 店家歸屬：URL 參數優先，其次是掃碼落地頁寫入的 Cookie（規劃書 §2.2 規則 3）。
const storeCookie = useCookie<string | null>('charity_store_slug', { default: () => null })
const attributedStoreSlug = computed(() => {
  const fromQuery = route.query.s
  if (typeof fromQuery === 'string' && fromQuery.trim()) return fromQuery
  return storeCookie.value || null
})

const { data: attributedStore } = await useAsyncData(
  `project-store-attribution-${projectSlug}`,
  async () => {
    if (!attributedStoreSlug.value) return null
    return await $fetch(`/api/charity/stores/${attributedStoreSlug.value}`)
  },
  { watch: [attributedStoreSlug] },
)

// 查不到有效店家（已停止合作／已刪除）視同無店家歸屬，不中斷流程（規劃書 §2.2 規則 5）。
const effectiveStoreSlug = computed(() => {
  if (!attributedStoreSlug.value) return null
  if (attributedStore.value && attributedStore.value.status === 'active') return attributedStoreSlug.value
  return null
})

const storeName = computed(() => {
  if (!attributedStore.value) return ''
  return pickText(lang.value, attributedStore.value.name_zh, attributedStore.value.name_en).text
})

if (!project.value) {
  throw createError({ statusCode: 404, statusMessage: 'Project not found', fatal: false })
}

const nameResult = computed(() => pickText(lang.value, project.value!.name_zh, project.value!.name_en))
const oneLinerResult = computed(() => pickText(lang.value, project.value!.one_liner_zh, project.value!.one_liner_en))
const fundUsageResult = computed(() => pickText(lang.value, project.value!.fund_usage_zh, project.value!.fund_usage_en))

useHead({
  title: `${nameResult.value.text} | ${tr.value.associationName}`,
  meta: [
    { property: 'og:title', content: nameResult.value.text },
    { property: 'og:description', content: oneLinerResult.value.text },
    { property: 'og:type', content: 'website' },
  ],
})
</script>

<template>
  <div v-if="project" class="container">
    <section class="section">
      <span
        aria-hidden="true"
        style="
          display: flex; align-items: center; justify-content: center;
          width: 100%; height: 160px; border-radius: var(--radius-card);
          background: var(--charity-bg-surface-2); color: var(--charity-text-tertiary);
          font-size: 2.5rem; font-weight: 700; margin-bottom: var(--sp-4);
        "
      >{{ nameResult.text.replace('測試用．', '').charAt(0) }}</span>
      <h1>{{ nameResult.text }}</h1>
      <p class="text-secondary">{{ oneLinerResult.text }}</p>
      <p v-if="nameResult.isFallback || oneLinerResult.isFallback" class="field-hint">{{ tr.fallbackNotice }}</p>
    </section>

    <p v-if="effectiveStoreSlug" class="notice-row" style="margin-bottom: var(--sp-4);">
      {{ tt(tr.project.storeAttribution, { store: storeName }) }}
    </p>

    <section class="section">
      <h2>{{ tr.project.fundUsageHeading }}</h2>
      <p>{{ fundUsageResult.text }}</p>
    </section>

    <section v-if="project.charityName" class="section">
      <h2>{{ tr.project.relatedProgramHeading }}</h2>
      <p class="card">
        <strong style="display: block; margin-bottom: 4px;">{{ project.charityName }}</strong>
        <span v-if="project.programName" class="text-secondary" style="display: block; margin-bottom: 8px;">{{ project.programName }}</span>
        <span class="text-tertiary" style="font-size: 0.875rem;">{{ tr.project.relatedProgramHint }}</span>
      </p>
    </section>

    <section class="section">
      <DonationForm :project="project" :store-slug="effectiveStoreSlug" />
    </section>

    <section class="section">
      <h2>{{ tr.project.faqHeading }}</h2>
      <div class="stack">
        <div>
          <strong>{{ tr.project.faqInvoiceQ }}</strong>
          <p class="text-secondary">{{ tr.project.faqInvoiceA }}</p>
        </div>
        <div>
          <strong>{{ tr.project.faqFundQ }}</strong>
          <p class="text-secondary">{{ tr.project.faqFundA }}</p>
        </div>
        <div>
          <strong>{{ tr.project.faqRefundQ }}</strong>
          <p class="text-secondary">{{ tr.project.faqRefundA }}</p>
        </div>
      </div>
    </section>
  </div>
</template>

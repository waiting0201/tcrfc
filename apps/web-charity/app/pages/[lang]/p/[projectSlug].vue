<script setup lang="ts">
// pages/[lang]/p/[projectSlug].vue — 項目詳情頁（docs/22-charity-ui.md §2.4／§2.5，規劃書 §3.2／§3.3）。
// 捐款表單固定在頁面最下方。店家歸屬取值優先序：URL ?s= > Cookie > 無店家（規劃書 §2.2 規則 3）。
import { useLang } from '../../../composables/useLang'
import { useHreflang } from '../../../composables/useHreflang'

definePageMeta({ layout: 'default' })

const route = useRoute()
const projectSlug = route.params.projectSlug as string

const { lang, tr, tt } = useLang()
useHreflang(`/p/${projectSlug}`)

const { data: project } = await useProjectDetail(projectSlug)

// 店家歸屬：URL 參數優先，其次是掃碼落地頁寫入的 Cookie（規劃書 §2.2 規則 3）。
const storeCookie = useCookie<string | null>('charity_store_slug', { default: () => null })
const attributedStoreSlug = computed(() => {
  const fromQuery = route.query.s
  if (typeof fromQuery === 'string' && fromQuery.trim()) return fromQuery.trim()
  return storeCookie.value || null
})

// 查不到有效店家（已停止合作／已刪除／網址亂填）視同無店家歸屬，不中斷流程（規劃書 §2.2 規則 5）。
const { data: landing } = await useStoreLanding(() => attributedStoreSlug.value)
const effectiveStore = computed(() => landing.value?.store ?? null)
const effectiveStoreSlug = computed(() => (effectiveStore.value ? attributedStoreSlug.value : null))

// 項目不存在或未上架：回 404 狀態碼，但仍在站內版面顯示說明與回列表的連結（不丟到框架預設錯誤頁）。
if (!project.value && import.meta.server) {
  setResponseStatus(useRequestEvent()!, 404)
}

useHead(() => ({
  title: `${project.value?.name ?? ''} | ${tr.value.associationName}`,
  meta: [
    { property: 'og:title', content: project.value?.name ?? '' },
    { property: 'og:description', content: project.value?.oneLiner ?? '' },
    { property: 'og:type', content: 'website' },
    ...(project.value?.coverUrl ? [{ property: 'og:image', content: project.value.coverUrl }] : []),
  ],
}))
</script>

<template>
  <div v-if="project" class="container">
    <section class="section">
      <img
        v-if="project.coverUrl"
        :src="project.coverUrl"
        :alt="project.coverAlt ?? ''"
        class="cover-img"
        width="1280"
        height="280"
      >
      <span
        v-else
        aria-hidden="true"
        style="
          display: flex; align-items: center; justify-content: center;
          width: 100%; height: 160px; border-radius: var(--radius-card);
          background: var(--charity-bg-surface-2); color: var(--charity-text-tertiary);
          font-size: 2.5rem; font-weight: 700; margin-bottom: var(--sp-4);
        "
      >{{ project.name.charAt(0) }}</span>
      <h1>{{ project.name }}</h1>
      <p v-if="project.oneLiner" class="text-secondary">{{ project.oneLiner }}</p>
      <p v-if="project.isFallback" class="field-hint">{{ tr.fallbackNotice }}</p>
    </section>

    <p v-if="effectiveStore" class="notice-row" style="margin-bottom: var(--sp-4);">
      {{ tt(tr.project.storeAttribution, { store: effectiveStore.name }) }}
    </p>

    <section v-if="project.description" class="section">
      <BlockContent :content="project.description" />
    </section>

    <section v-if="project.fundUsage" class="section">
      <h2>{{ tr.project.fundUsageHeading }}</h2>
      <p>{{ project.fundUsage }}</p>
    </section>

    <section v-if="project.charityName" class="section">
      <h2>{{ tr.project.relatedProgramHeading }}</h2>
      <p class="card">
        <strong style="display: block; margin-bottom: 4px;">{{ project.charityName }}</strong>
        <span v-if="project.charityProgramName" class="text-secondary" style="display: block; margin-bottom: 8px;">{{ project.charityProgramName }}</span>
        <span class="text-tertiary" style="font-size: 0.875rem;">{{ tr.project.relatedProgramHint }}</span>
      </p>
    </section>

    <section class="section">
      <DonationForm :key="`${lang}-${project.slug}`" :project="project" :store-slug="effectiveStoreSlug" />
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

  <div v-else class="container">
    <section class="section">
      <h1>{{ tr.project.notFound }}</h1>
      <NuxtLink :to="`/${lang}/`" class="btn btn-primary">{{ tr.project.backToList }}</NuxtLink>
    </section>
  </div>
</template>

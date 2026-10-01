<script setup lang="ts">
// pages/[lang]/s/[storeSlug].vue — 掃碼落地頁（🔴 全平台最關鍵的一頁，docs/22-charity-ui.md §2.2）。
// 店家歸屬傳遞規則（規劃書 §2.2）：進入本頁時把 store_slug 寫入 24 小時 Cookie，
// 只有「找得到且合作中」的店家才寫入——已停止合作、不存在或不在合作期間一律視同無店家歸屬（規則 5）：
// API 對這些情況一律回 `{ store: null }`（不區分原因，避免外人探測），版面降級成等同一般入口，
// 不報錯、不中斷捐款流程，並清掉先前殘留的店家 Cookie（避免這次掃到失效 QR 卻沿用上一家的歸屬）。
import { useLang } from '../../../composables/useLang'
import { useHreflang } from '../../../composables/useHreflang'

definePageMeta({ layout: 'default' })

const route = useRoute()
const storeSlug = route.params.storeSlug as string

const { tr, tt } = useLang()
useHreflang(`/s/${storeSlug}`)

const { data: landing } = await useStoreLanding(() => storeSlug)
const { data: projects, status } = await useCharityProjects()
const { data: settings } = await useCharitySettings()

const store = computed(() => landing.value?.store ?? null)

const storeCookie = useCookie<string | null>('charity_store_slug', { maxAge: 60 * 60 * 24, sameSite: 'lax', default: () => null })
storeCookie.value = store.value ? storeSlug : null

const introText = computed(() => settings.value?.homeIntro || tr.value.home.introFallback)
const showFallbackNotice = computed(() => Boolean(
  store.value?.isFallback || settings.value?.isFallback || projects.value?.some((p) => p.isFallback),
))

useHead(() => ({
  title: `${tr.value.home.title} | ${tr.value.associationName}`,
  meta: [
    {
      property: 'og:title',
      content: store.value
        ? tt(tr.value.home.introHeadingWithStore, { store: store.value.name })
        : tt(tr.value.home.introHeadingPlain),
    },
    { property: 'og:description', content: introText.value },
    { property: 'og:type', content: 'website' },
  ],
}))
</script>

<template>
  <div class="container">
    <section v-if="store" class="section">
      <!-- 店家識別：有 Logo 顯示圖片，沒有就降級成純文字店名區塊，不留空框（規劃書 §3.1）。 -->
      <img
        v-if="store.logoUrl"
        :src="store.logoUrl"
        :alt="store.logoAlt || store.name"
        class="store-logo"
        height="56"
      >
      <p
        v-else
        style="
          display: inline-flex; align-items: center; justify-content: center;
          min-height: 44px; padding: 0 var(--sp-3); border-radius: var(--radius-control);
          background: var(--charity-bg-surface-2); font-weight: 700; margin-bottom: var(--sp-2);
        "
      >{{ store.name }}</p>
      <h1>{{ tt(tr.home.introHeadingWithStore, { store: store.name }) }}</h1>
      <p class="text-secondary">{{ introText }}</p>
      <p v-if="showFallbackNotice" class="field-hint">{{ tr.fallbackNotice }}</p>
    </section>

    <section v-else class="section">
      <h1>{{ tt(tr.home.introHeadingPlain) }}</h1>
      <p class="text-secondary">{{ introText }}</p>
      <p v-if="showFallbackNotice" class="field-hint">{{ tr.fallbackNotice }}</p>
    </section>

    <section class="section">
      <h2>{{ tr.home.projectsHeading }}</h2>
      <ProjectGrid
        :projects="projects ?? []"
        :store-slug="store ? storeSlug : null"
        :loading="status === 'pending'"
        :failed="projects === null"
      />
    </section>

    <section class="section">
      <TrustSection />
    </section>
  </div>
</template>

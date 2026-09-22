<script setup lang="ts">
// pages/[lang]/s/[storeSlug].vue — 掃碼落地頁（🔴 全平台最關鍵的一頁，docs/22-charity-ui.md §2.2）。
// 店家歸屬傳遞規則（規劃書 §2.2）：進入本頁時把 store_slug 寫入 24 小時 Cookie，
// 只有「找得到且合作中」的店家才寫入——已停止合作／查無此店家一律視同無店家歸屬（規則 5），
// 版面降級成等同一般入口，不報錯、不中斷捐款流程。
import { useLang } from '../../../composables/useLang'
import { useHreflang } from '../../../composables/useHreflang'
import { pickText } from '../../../utils/i18n'

definePageMeta({ layout: 'default' })

const route = useRoute()
const storeSlug = route.params.storeSlug as string

const { lang, tr, tt } = useLang()
useHreflang(`/s/${storeSlug}`)

const { data: store } = await useFetch(`/api/charity/stores/${storeSlug}`)
const { data: projects, pending } = await useFetch('/api/charity/projects')
const { data: settings } = await useFetch('/api/charity/settings')

const isActiveStore = computed(() => store.value && store.value.status === 'active')

if (import.meta.client && isActiveStore.value) {
  const cookie = useCookie('charity_store_slug', { maxAge: 60 * 60 * 24, sameSite: 'lax' })
  cookie.value = storeSlug
}

const storeName = computed(() => {
  if (!store.value) return ''
  return pickText(lang.value, store.value.name_zh, store.value.name_en).text
})

const introText = computed(() => {
  if (!settings.value) return tr.value.home.introFallback
  return pickText(lang.value, settings.value.homeIntro.zh, settings.value.homeIntro.en).text
})

useHead({
  title: `${tr.value.home.title} | ${tr.value.associationName}`,
  meta: [
    {
      property: 'og:title',
      content: isActiveStore.value
        ? tt(tr.value.home.introHeadingWithStore, { store: storeName.value })
        : tt(tr.value.home.introHeadingPlain),
    },
    { property: 'og:description', content: introText.value },
    { property: 'og:type', content: 'website' },
  ],
})
</script>

<template>
  <div class="container">
    <section v-if="isActiveStore" class="section">
      <p
        aria-hidden="true"
        style="
          display: inline-flex; align-items: center; justify-content: center;
          min-height: 44px; padding: 0 var(--sp-3); border-radius: var(--radius-control);
          background: var(--charity-bg-surface-2); font-weight: 700; margin-bottom: var(--sp-2);
        "
      >{{ storeName }}</p>
      <h1>{{ tt(tr.home.introHeadingWithStore, { store: storeName }) }}</h1>
      <p class="text-secondary">{{ introText }}</p>
    </section>

    <section v-else class="section">
      <p v-if="store && store.status === 'inactive'" class="notice-row" style="margin-bottom: var(--sp-4);">
        {{ tr.home.storeEndedNotice }}
      </p>
      <h1>{{ tt(tr.home.introHeadingPlain) }}</h1>
      <p class="text-secondary">{{ introText }}</p>
    </section>

    <section class="section">
      <h2>{{ tr.home.projectsHeading }}</h2>
      <ProjectGrid
        :projects="projects ?? []"
        :store-slug="isActiveStore ? storeSlug : null"
        :loading="pending"
      />
    </section>

    <section class="section">
      <TrustSection />
    </section>
  </div>
</template>

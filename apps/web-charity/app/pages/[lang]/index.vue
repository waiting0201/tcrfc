<script setup lang="ts">
// pages/[lang]/index.vue — 一般入口 `/{lang}/`（docs/22-charity-ui.md §2.3）。
// 與掃碼落地頁共用 ProjectGrid／TrustSection，差異只在沒有店家識別區塊。
import { useLang } from '../../composables/useLang'
import { useHreflang } from '../../composables/useHreflang'
import { pickText } from '../../utils/i18n'

definePageMeta({ layout: 'default' })

const { lang, tr, tt } = useLang()
useHreflang('/')

const { data: projects, pending } = await useFetch('/api/charity/projects')
const { data: settings } = await useFetch('/api/charity/settings')

const introText = computed(() => {
  if (!settings.value) return tr.value.home.introFallback
  return pickText(lang.value, settings.value.homeIntro.zh, settings.value.homeIntro.en).text
})

useHead({
  title: `${tr.value.home.title} | ${tr.value.associationName}`,
  meta: [
    { property: 'og:title', content: tt(tr.value.home.introHeadingPlain) },
    { property: 'og:description', content: introText.value },
    { property: 'og:type', content: 'website' },
  ],
})
</script>

<template>
  <div class="container">
    <section class="section">
      <h1>{{ tt(tr.home.introHeadingPlain) }}</h1>
      <p class="text-secondary">{{ introText }}</p>
    </section>

    <section class="section">
      <h2>{{ tr.home.projectsHeading }}</h2>
      <ProjectGrid :projects="projects ?? []" :store-slug="null" :loading="pending" />
    </section>

    <section class="section">
      <TrustSection />
    </section>
  </div>
</template>

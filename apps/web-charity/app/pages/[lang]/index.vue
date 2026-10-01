<script setup lang="ts">
// pages/[lang]/index.vue — 一般入口 `/{lang}/`（docs/22-charity-ui.md §2.3）。
// 與掃碼落地頁共用 ProjectGrid／TrustSection，差異只在沒有店家識別區塊。
import { useLang } from '../../composables/useLang'
import { useHreflang } from '../../composables/useHreflang'

definePageMeta({ layout: 'default' })

const { tr, tt } = useLang()
useHreflang('/')

const { data: projects, status } = await useCharityProjects()
const { data: settings } = await useCharitySettings()

const introText = computed(() => settings.value?.homeIntro || tr.value.home.introFallback)
const showFallbackNotice = computed(() => Boolean(settings.value?.isFallback || projects.value?.some((p) => p.isFallback)))

useHead(() => ({
  title: `${tr.value.home.title} | ${tr.value.associationName}`,
  meta: [
    { property: 'og:title', content: tt(tr.value.home.introHeadingPlain) },
    { property: 'og:description', content: introText.value },
    { property: 'og:type', content: 'website' },
  ],
}))
</script>

<template>
  <div class="container">
    <section class="section">
      <h1>{{ tt(tr.home.introHeadingPlain) }}</h1>
      <p class="text-secondary">{{ introText }}</p>
      <p v-if="showFallbackNotice" class="field-hint">{{ tr.fallbackNotice }}</p>
    </section>

    <section class="section">
      <h2>{{ tr.home.projectsHeading }}</h2>
      <ProjectGrid
        :projects="projects ?? []"
        :store-slug="null"
        :loading="status === 'pending'"
        :failed="projects === null"
      />
    </section>

    <section class="section">
      <TrustSection />
    </section>
  </div>
</template>

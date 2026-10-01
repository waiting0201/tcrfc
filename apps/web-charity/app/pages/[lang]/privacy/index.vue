<script setup lang="ts">
// pages/[lang]/privacy/index.vue — 隱私權政策（docs/22-charity-ui.md §2.9）。
// 內文來自 `GET /settings` 的 `privacyPolicy`（後台 N7 站台設定維護），⛔ 前台不自行編寫法律條文；
// 後台尚未填寫時只顯示「尚未提供內容」，不補預設文案。
import { useLang } from '../../../composables/useLang'
import { useHreflang } from '../../../composables/useHreflang'

definePageMeta({ layout: 'default' })

const { tr } = useLang()
useHreflang('/privacy/')

const { data: settings } = await useCharitySettings()

const bodyText = computed(() => settings.value?.privacyPolicy ?? '')

useHead(() => ({ title: `${tr.value.privacy.heading} | ${tr.value.associationName}` }))
</script>

<template>
  <div class="container">
    <section class="section">
      <h1>{{ tr.privacy.heading }}</h1>
      <p v-if="bodyText" style="white-space: pre-line;">{{ bodyText }}</p>
      <p v-else class="text-secondary">{{ tr.privacy.empty }}</p>
    </section>
  </div>
</template>

<script setup lang="ts">
// pages/[lang]/privacy/index.vue — 隱私權政策（docs/22-charity-ui.md §2.9）。
// 內文是 db/seed 種子資料裡明講的開發測試占位文字，⛔ 不自行編寫看起來更正式的法律條文
// （frontend-architect 的記憶庫有記錄：法律頁面的正文缺漏不能用一般行銷文案的填補邏輯處理，
// 這裡的作法是老實顯示「占位、待確認」，不是假裝寫了一份真的隱私權政策）。
import { useLang } from '../../../composables/useLang'
import { useHreflang } from '../../../composables/useHreflang'
import { pickText } from '../../../utils/i18n'

definePageMeta({ layout: 'default' })

const { lang, tr } = useLang()
useHreflang('/privacy/')

const { data: settings } = await useFetch('/api/charity/settings')

const bodyText = computed(() => {
  if (!settings.value) return ''
  return pickText(lang.value, settings.value.privacyPolicy.zh, settings.value.privacyPolicy.en).text
})

useHead({ title: `${tr.value.privacy.heading} | ${tr.value.associationName}` })
</script>

<template>
  <div class="container">
    <section class="section">
      <h1>{{ tr.privacy.heading }}</h1>
      <p class="text-tertiary">{{ tr.privacy.placeholderNotice }}</p>
      <p>{{ bodyText }}</p>
    </section>
  </div>
</template>

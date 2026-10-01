<script setup lang="ts">
// pages/[lang]/donors/index.vue — 捐款徵信名單（docs/22-charity-ui.md §2.8，規劃書 §3.6）。
//
// ⚠️ 後端目前沒有徵信名單的公開端點（apps/api README「慈善 CH-2／CH-3」：徵信名單屬 CH-5，尚未提供），
// 所以這一頁只依 `GET /settings` 的 `creditListEnabled` 呈現「未開放」或「準備中」，**不顯示任何名單、不用假資料頂替**。
// 端點到位後，這裡再接上名單與「依項目與期間篩選」。
import { useLang } from '../../../composables/useLang'
import { useHreflang } from '../../../composables/useHreflang'

definePageMeta({ layout: 'default' })

const { tr } = useLang()
useHreflang('/donors/')

const { data: settings } = await useCharitySettings()

useHead(() => ({ title: `${tr.value.donors.heading} | ${tr.value.associationName}` }))
</script>

<template>
  <div class="container">
    <section class="section">
      <h1>{{ tr.donors.heading }}</h1>
      <p v-if="settings && !settings.creditListEnabled" class="text-secondary">{{ tr.donors.disabled }}</p>
      <p v-else class="text-secondary">{{ tr.donors.pending }}</p>
    </section>
  </div>
</template>

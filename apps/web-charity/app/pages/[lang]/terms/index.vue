<script setup lang="ts">
// pages/[lang]/terms/index.vue — 捐款須知（docs/22-charity-ui.md §2.9）。
// 「捐款一經完成，原則上不受理退款」須以獨立段落＋加粗呈現，不得埋在其他條款中間（docs/22 §2.9 明文）。
import { useLang } from '../../../composables/useLang'
import { useHreflang } from '../../../composables/useHreflang'
import { pickText } from '../../../utils/i18n'

definePageMeta({ layout: 'default' })

const { lang, tr } = useLang()
useHreflang('/terms/')

const { data: settings } = await useFetch('/api/charity/settings')

const noticeText = computed(() => {
  if (!settings.value) return ''
  return pickText(lang.value, settings.value.notice.zh, settings.value.notice.en).text
})

useHead({ title: `${tr.value.terms.heading} | ${tr.value.associationName}` })
</script>

<template>
  <div class="container">
    <section class="section">
      <h1>{{ tr.terms.heading }}</h1>
      <p class="text-tertiary">{{ tr.terms.placeholderNotice }}</p>

      <div class="card" style="border-color: var(--charity-danger);">
        <h2 style="margin-bottom: var(--sp-2);">{{ tr.terms.noRefundHeading }}</h2>
        <p style="font-weight: 700; margin-bottom: 0;">{{ noticeText }}</p>
      </div>
    </section>
  </div>
</template>

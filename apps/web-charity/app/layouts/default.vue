<script setup lang="ts">
import { useLang } from '../composables/useLang'

const { lang, tr } = useLang()

// <html lang> 隨語系切換（規劃書 §2.4 雙語要求的一部分）。本專案沒有裝 @nuxtjs/seo，
// 不會撞上 apps/web 記錄過的 nuxt-seo-utils 覆蓋 htmlAttrs.lang 那個坑
// （docs/18-work-errors.md E-17）——這裡用一般的 useHead 就足夠，已用 curl 實測確認生效。
useHead(() => ({
  htmlAttrs: { lang: lang.value === 'en' ? 'en' : 'zh-Hant' },
}))
</script>

<template>
  <div>
    <a href="#main" class="skip-link">{{ tr.skipToContent }}</a>
    <div class="container">
      <LangSwitch />
    </div>
    <main id="main">
      <slot />
    </main>
    <SiteFooter />
  </div>
</template>

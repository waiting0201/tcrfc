<script setup lang="ts">
// LangSwitch.vue — 語系切換須停留在當前頁的對應語系版本，不跳回首頁（規劃書 §2.4）。
import { useLang } from '../composables/useLang'

const { lang, tr, pathForLang } = useLang()
</script>

<template>
  <div class="lang-switch">
    <NuxtLink :to="pathForLang('zh')" :aria-current="lang === 'zh' ? 'page' : undefined">
      {{ tr.langSwitch.zh }}
    </NuxtLink>
    <span aria-hidden="true">｜</span>
    <NuxtLink :to="pathForLang('en')" :aria-current="lang === 'en' ? 'page' : undefined">
      {{ tr.langSwitch.en }}
    </NuxtLink>
  </div>
</template>

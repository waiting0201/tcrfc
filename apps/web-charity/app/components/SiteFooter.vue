<script setup lang="ts">
import { useLang } from '../composables/useLang'

const { lang, tr, tt } = useLang()

// 徵信名單入口只在後台開放時顯示（規劃書 §3.6「可於後台整站關閉」）。
const { data: settings } = await useCharitySettings()
</script>

<template>
  <footer class="site-footer">
    <div class="container">
      <nav class="footer-links" aria-label="footer">
        <NuxtLink v-if="settings?.creditListEnabled" :to="`/${lang}/donors/`">{{ tr.footer.donors }}</NuxtLink>
        <NuxtLink :to="`/${lang}/privacy/`">{{ tr.footer.privacy }}</NuxtLink>
        <NuxtLink :to="`/${lang}/terms/`">{{ tr.footer.terms }}</NuxtLink>
      </nav>
      <p>{{ tt(tr.footer.contactValue) }}</p>
    </div>
  </footer>
</template>

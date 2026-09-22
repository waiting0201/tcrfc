<script setup lang="ts">
import { useLang } from '../composables/useLang'

const { lang, tr, tt } = useLang()
</script>

<template>
  <footer class="site-footer">
    <div class="container">
      <nav class="footer-links" aria-label="footer">
        <NuxtLink :to="`/${lang}/donors/`">{{ tr.footer.donors }}</NuxtLink>
        <NuxtLink :to="`/${lang}/privacy/`">{{ tr.footer.privacy }}</NuxtLink>
        <NuxtLink :to="`/${lang}/terms/`">{{ tr.footer.terms }}</NuxtLink>
      </nav>
      <p>{{ tt(tr.footer.contactValue) }}</p>
    </div>
  </footer>
</template>

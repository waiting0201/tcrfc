<script setup lang="ts">
// app/components/ProgramPartnersList.vue — 05 課程「合作夥伴」清單（B-8）：多個課程的夥伴合併去重，有 Logo 用 Logo 格，否則顯示名稱。
// 外部網址只接 https，且 `rel="noopener noreferrer"`。
const props = defineProps<{ programs: ProgramDetailView[] }>()
const partners = computed(() => uniqueById(props.programs.map((p) => p.partners)))
</script>

<template>
  <div v-if="partners.length" class="program-partners">
    <PartnerLogoTile
      v-for="p in partners" :key="p.id"
      :name="p.name" v-bind="pickLogoProps(p)"
      :href="safeHttps(p.websiteUrl)" external
    />
  </div>
</template>

<style>
.program-partners{ display:grid; gap:1rem; grid-template-columns:repeat(auto-fit,minmax(180px,1fr)); }
</style>

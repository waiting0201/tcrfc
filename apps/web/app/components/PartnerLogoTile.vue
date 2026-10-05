<script setup lang="ts">
// app/components/PartnerLogoTile.vue — 夥伴／贊助商 Logo 牆的單一格子（S2-7，首頁與 9.1／9.2 共用）
//
// 沿用 tcrfc.css 既有 `.sponsor-tile`（淺底、灰階 Logo、hover 還原色彩），不新增版型。
// 沒有任何 Logo 檔（後台 Logo 兩欄皆未上傳）時以名稱文字取代，不放假圖。
// 外部網址一律 `rel="noopener noreferrer"`；站內連結不開新分頁。
const props = defineProps<{
  name: string | null
  logoUrl: string | null
  /** 有值就整格可點；`external` 為 true 時開新分頁。 */
  href?: string | null
  external?: boolean
}>()
const { tx } = useLocale()
const tileLabel = computed(() => (props.href && props.name ? (props.external ? `${props.name}${tx('（另開新分頁）', ' (opens in a new tab)')}` : props.name) : undefined))
</script>

<template>
  <component
    :is="href ? 'a' : 'div'"
    class="sponsor-tile partner-logo-tile"
    :href="href || undefined"
    :target="href && external ? '_blank' : undefined"
    :rel="href && external ? 'noopener noreferrer' : undefined"
    :aria-label="tileLabel"
  >
    <img v-if="logoUrl" :src="logoUrl" :alt="href ? '' : (name ?? '')" loading="lazy" width="200" height="125">
    <span v-else class="partner-logo-tile__name">{{ name }}</span>
  </component>
</template>

<style>
.partner-logo-tile{ text-decoration:none; }
.partner-logo-tile__name{ font-size:.9rem; font-weight:800; color:var(--heading); text-align:center; line-height:1.4; }
</style>

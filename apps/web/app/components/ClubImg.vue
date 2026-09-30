<script setup lang="ts">
// 俱樂部安全圖片（E-83）。
//
// 用在「兩站共用頁面、但照片是磐石素材」的位置（例如厚底緩震機能襪的商品照，每張右上角都印有
// TCRFC 標誌）。磐石站輸出原本的 <img>；藍鯨站不得輸出任何磐石圖片，改輸出同尺寸比例的品牌
// 色佔位方塊。藍鯨取得自己的商品照後，把 v-else 換成藍鯨素材（並把素材目錄加進
// scripts/check-club-image-leak.mjs 的允許清單）。
defineProps<{
  src: string
  width: number | string
  height: number | string
  alt?: string
  loading?: 'lazy' | 'eager'
}>()

const config = useRuntimeConfig()
const isTcrfc = computed(() => config.public.club !== 'bw')
</script>

<template>
  <img v-if="isTcrfc" :src="src" :alt="alt ?? ''" :width="width" :height="height" :loading="loading">
  <span v-else class="club-img-pending" :style="{ aspectRatio: `${width} / ${height}` }" aria-hidden="true" />
</template>

<style>
.club-img-pending{ display:block; width:100%; height:auto; background:linear-gradient(160deg, var(--ink) 0%, var(--brand-deep) 100%); opacity:.85; }
.product-card__media .club-img-pending{ height:100%; }
.variant-swatch .club-img-pending{ width:60px; height:60px; }
</style>

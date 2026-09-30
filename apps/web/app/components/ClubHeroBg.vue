<script setup lang="ts">
// 頁首背景圖的俱樂部安全版本（E-83）。
//
// 頁首照片幾乎都是磐石素材（多含未成年學員）。藍鯨站不得輸出任何磐石照片——
// 藍鯨沒有對應照片時，改輸出與既有 `page-hero__bg--pending` 慣例相同的品牌漸層佔位。
// 磐石站輸出與原本手寫的 `<img class="page-hero__bg" …>` 相同。
// 藍鯨取得自己的頁首照片後，把這裡的 v-else 換成藍鯨素材即可（同時把素材目錄加進
// scripts/check-club-image-leak.mjs 的允許清單）。
defineProps<{
  src: string
  width: number | string
  height: number | string
  alt?: string
}>()

const config = useRuntimeConfig()
const isTcrfc = computed(() => config.public.club !== 'bw')
</script>

<template>
  <img v-if="isTcrfc" class="page-hero__bg" :src="src" :alt="alt ?? ''" :width="width" :height="height">
  <div v-else class="page-hero__bg page-hero__bg--pending" aria-hidden="true" />
</template>

<style>
/* 藍鯨無對應照片時的頁首佔位漸層（與 academy/pathway.vue 等既有 page-hero__bg--pending 同值） */
.page-hero__bg--pending{ background:linear-gradient(160deg, var(--ink) 0%, var(--brand-deep) 100%); }
</style>

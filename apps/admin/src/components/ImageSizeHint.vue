<script setup lang="ts">
/** 目前圖片尺寸提示（有圖且後端回了寬高才顯示）。圖片說明欄與「一欄說明對兩張圖」的頁面共用。 */
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{ hasImage: boolean; width?: number | null; height?: number | null; prefix?: string }>(),
  { width: null, height: null, prefix: '目前圖片尺寸' },
)
const text = computed(() => (props.hasImage && props.width && props.height ? `${props.prefix}：${props.width} × ${props.height} 像素` : ''))
</script>

<template>
  <p v-if="text" class="image-size-hint">{{ text }}</p>
</template>

<style scoped>
.image-size-hint {
  margin: 4px 0 0;
  font-size: 12px;
  color: var(--admin-text-tertiary);
}
</style>

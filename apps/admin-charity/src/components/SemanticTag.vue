<script setup lang="ts">
/**
 * 語意色 tag 的共用外殼（docs/22-charity-ui.md §1.6.3）。顏色一律是輔助訊號，文字標籤才是
 * 主要傳達管道（§1.8）——呼叫端一律要傳入完整中文標籤，不是只靠顏色。
 */
const props = defineProps<{
  variant: 'success' | 'danger' | 'warning' | 'info' | 'refund' | 'neutral'
}>()
</script>

<template>
  <el-tag :class="['charity-tag', `charity-tag--${variant}`]" disable-transitions size="small">
    <!-- 成功色與球場綠主色相近：成功一律「✓ 圖示＋文字」，不單靠綠色（docs/22 §1.8） -->
    <span v-if="props.variant === 'success'" class="charity-tag__check" aria-hidden="true">✓ </span>
    <slot />
  </el-tag>
</template>

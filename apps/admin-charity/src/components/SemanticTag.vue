<script setup lang="ts">
/**
 * 語意色 tag 的共用外殼（docs/22-charity-ui.md §1.6.3）。顏色一律是輔助訊號，文字標籤才是
 * 主要傳達管道（§1.8）——呼叫端一律要傳入完整中文標籤，不是只靠顏色。
 */
defineProps<{
  variant: 'success' | 'danger' | 'warning' | 'info' | 'refund' | 'neutral'
}>()
</script>

<template>
  <el-tag :class="['charity-tag', `charity-tag--${variant}`]" disable-transitions size="small">
    <slot />
  </el-tag>
</template>

<script setup lang="ts">
/** 「存檔成功，但尚未同步到靜態設定檔」的說明（版本、維護模式、功能開關共用）。目前恆為尚未串接，不是錯誤。 */
import type { EdgePublishDto } from '@/api/adminApp'
defineProps<{ edge: EdgePublishDto | null }>()
</script>

<template>
  <el-alert v-if="edge && !edge.published" type="warning" show-icon :closable="false" class="edge-notice" title="已存檔，但尚未同步到靜態設定檔">
    {{ edge.message }}同步串接完成前，App 只會從主機讀到這項設定；主機全部無法連線時，App 讀不到最新的維護或更新設定。
  </el-alert>
</template>

<style scoped>
.edge-notice { margin-bottom: 12px; }
</style>

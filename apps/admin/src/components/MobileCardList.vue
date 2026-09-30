<script setup lang="ts" generic="T extends Record<string, any>">
/**
 * 手機版清單：桌面用 `el-table`，< 768px 改成一筆一張的卡片（比照 B4 常見問題的既有做法）。
 * 標題、次要資訊與操作按鈕都用 slot 交給各清單頁決定，本元件只負責排版。
 */
defineProps<{
  rows: T[]
  rowKey: string
}>()
</script>

<template>
  <div class="mobile-card-list">
    <el-card v-for="row in rows" :key="row[rowKey]" shadow="never" class="mobile-card-list__card">
      <div class="mobile-card-list__title"><slot name="title" :row="row" /></div>
      <div class="mobile-card-list__meta"><slot name="meta" :row="row" /></div>
      <div class="mobile-card-list__actions"><slot name="actions" :row="row" /></div>
    </el-card>
  </div>
</template>

<style scoped>
.mobile-card-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.mobile-card-list__card {
  --el-card-padding: 12px;
}

.mobile-card-list__title {
  font-size: 14px;
  font-weight: 500;
  margin-bottom: 6px;
  word-break: break-word;
}

.mobile-card-list__meta {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px 8px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  margin-bottom: 8px;
}

.mobile-card-list__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  border-top: 1px solid var(--el-border-color-lighter);
  padding-top: 8px;
}
</style>

<script setup lang="ts">
/**
 * 編輯頁底部固定的操作列（桌面貼齊側欄右側，平板／手機貼齊左邊，手機按鈕撐滿）。
 * `#status` 插槽放在左側，用來顯示「有 N 處需要修正」＋「前往下一處」（見 FormErrorStatus），
 * 外層是 aria-live="polite"，數字變動時螢幕閱讀器會朗讀。
 */
</script>

<template>
  <div class="edit-action-bar">
    <div v-if="$slots.status" class="edit-action-bar__status" aria-live="polite"><slot name="status" /></div>
    <slot />
  </div>
</template>

<style scoped>
.edit-action-bar {
  position: fixed;
  bottom: 0;
  left: var(--admin-sidebar-width-expanded);
  right: 0;
  background: var(--admin-bg-surface-2);
  border-top: 1px solid var(--admin-border);
  padding: 12px 24px;
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 8px;
  z-index: 10;
}

.edit-action-bar__status {
  margin-right: auto;
  min-width: 0;
}

@media (max-width: 1023px) {
  .edit-action-bar { left: 0; }
}

@media (max-width: 767px) {
  .edit-action-bar {
    flex-wrap: wrap;
    justify-content: stretch;
  }
  .edit-action-bar__status { flex: 0 0 100%; }
  .edit-action-bar :deep(.el-button) { flex: 1; }
  .edit-action-bar__status :deep(.el-button) { flex: none; }
}
</style>

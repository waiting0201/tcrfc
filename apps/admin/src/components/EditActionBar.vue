<script setup lang="ts">
/** 編輯頁底部固定的操作列（桌面貼齊側欄右側，平板／手機貼齊左邊，手機按鈕撐滿）。 */
</script>

<template>
  <div class="edit-action-bar"><slot /></div>
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
  justify-content: flex-end;
  gap: 8px;
  z-index: 10;
}

@media (max-width: 1023px) {
  .edit-action-bar { left: 0; }
}

@media (max-width: 767px) {
  .edit-action-bar { justify-content: stretch; }
  .edit-action-bar :deep(.el-button) { flex: 1; }
}
</style>

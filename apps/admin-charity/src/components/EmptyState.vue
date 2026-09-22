<script setup lang="ts">
/** 空狀態（docs/22-charity-ui.md §3.5，沿用 docs/21 §10 骨架，只換色票） */
withDefaults(defineProps<{ text?: string }>(), { text: '目前沒有資料' })
</script>

<template>
  <div class="empty-state">
    <el-icon :size="40"><Box /></el-icon>
    <p class="empty-state__text">{{ text }}</p>
    <div v-if="$slots.action" class="empty-state__action">
      <slot name="action" />
    </div>
  </div>
</template>

<style scoped>
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: var(--charity-admin-space-8) var(--charity-admin-space-4);
  color: var(--charity-admin-text-tertiary);
  gap: var(--charity-admin-space-3);
}

.empty-state__text {
  margin: 0;
  font-size: 14px;
  color: var(--charity-admin-text-secondary);
}
</style>

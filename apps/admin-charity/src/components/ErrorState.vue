<script setup lang="ts">
/**
 * 查詢失敗（docs/22-charity-ui.md §3.5：沿用 docs/21 §10 判斷邏輯——用中性色警示三角，
 * 不用危險紅，因為「查不到資料」不是使用者操作失敗，用刺眼的紅色會過度歸咎使用者）。
 */
withDefaults(defineProps<{ text?: string }>(), { text: '查詢時發生問題，請稍後再試一次' })

const emit = defineEmits<{ (e: 'retry'): void }>()
</script>

<template>
  <div class="error-state">
    <el-icon :size="40" color="var(--charity-admin-text-tertiary)"><WarningFilled /></el-icon>
    <p class="error-state__text">{{ text }}</p>
    <el-button size="small" @click="emit('retry')">重新查詢</el-button>
  </div>
</template>

<style scoped>
.error-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: var(--charity-admin-space-8) var(--charity-admin-space-4);
  gap: var(--charity-admin-space-3);
}

.error-state__text {
  margin: 0;
  font-size: 14px;
  color: var(--charity-admin-text-secondary);
}
</style>

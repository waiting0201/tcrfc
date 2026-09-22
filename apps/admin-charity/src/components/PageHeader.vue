<script setup lang="ts">
/**
 * 頁面列（docs/22-charity-ui.md §3.3）：標題 ＋「本頁對應前台：○○○」輕量一行說明。
 *
 * 沿用 apps/admin PageHeader 的版面方法論（docs/22 §3.1 明文沿用列表頁標準型骨架），
 * 但不做「這裡管理的是」獨立結構——慈善後台只有 7 個模組、沒有多俱樂部語境，
 * 那一整套是為了解決多站台辨識問題，這裡沒有那個問題規模（docs/22 §3.2）。
 */
withDefaults(
  defineProps<{
    title: string
    frontendUnit?: string
  }>(),
  { frontendUnit: '' },
)
</script>

<template>
  <div class="page-header">
    <div class="page-header__row">
      <slot name="back" />
      <h1 class="page-header__title">{{ title }}</h1>
      <div class="page-header__actions">
        <slot name="actions" />
      </div>
    </div>
    <p v-if="frontendUnit" class="page-header__meta">本頁對應前台：{{ frontendUnit }}</p>
    <div v-if="$slots.meta" class="page-header__meta">
      <slot name="meta" />
    </div>
  </div>
</template>

<style scoped>
.page-header {
  background: var(--charity-admin-bg-surface);
  border-bottom: 1px solid var(--charity-admin-border);
  margin: calc(var(--charity-admin-space-6) * -1) calc(var(--charity-admin-space-6) * -1) var(--charity-admin-space-6);
  padding: var(--charity-admin-space-4) var(--charity-admin-space-6);
  display: flex;
  flex-direction: column;
  gap: var(--charity-admin-space-1);
}

.page-header__row {
  display: flex;
  align-items: center;
  gap: var(--charity-admin-space-3);
  min-width: 0;
}

.page-header__title {
  font-size: 20px;
  line-height: 1.3;
  margin: 0;
  color: var(--charity-admin-text-primary);
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.page-header__actions {
  display: flex;
  gap: var(--charity-admin-space-2);
  flex-shrink: 0;
}

.page-header__meta {
  margin: 0;
  font-size: 13px;
  color: var(--charity-admin-text-tertiary);
}

@media (max-width: 767px) {
  .page-header {
    margin: calc(var(--charity-admin-space-4) * -1) calc(var(--charity-admin-space-4) * -1) var(--charity-admin-space-4);
    padding: var(--charity-admin-space-3) var(--charity-admin-space-4);
  }

  .page-header__title {
    font-size: 17px;
  }
}
</style>

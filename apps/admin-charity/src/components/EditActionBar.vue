<script setup lang="ts">
/**
 * 編輯頁底部的操作列（docs/21 §3.3，慈善後台版）。
 *
 * apps/admin 用 `position: fixed` 貼齊側欄右側；慈善後台側欄有「展開／收合／抽屜」三種寬度且不是 CSS 變數，
 * 寫死寬度會蓋住或漏出側欄，所以改成 `position: sticky; bottom: 0`——放在 `AdminLayout` 的捲動容器內，
 * 內容超出一屏時貼齊底部，內容少時自然接在內容後面。左右抵銷捲動容器的內距，讓它撐滿內容區寬度。
 * `#status` 插槽放在左側，用來顯示「有 N 處需要修正」＋「前往下一處」（見 FormErrorStatus），
 * 外層是 aria-live="polite"，數字變動時螢幕閱讀器會朗讀。手機寬度按鈕撐滿。
 *
 * `variant="inline"`：給 `*Panel`（放在 el-tabs 內的設定面板）用——`.el-tabs__content` 是 overflow:hidden，
 * sticky 與抵銷內距都會失效，所以改成接在內容後面的一列，不貼底。
 */
withDefaults(defineProps<{ variant?: 'sticky' | 'inline' }>(), { variant: 'sticky' })
</script>

<template>
  <div class="edit-action-bar" :class="`edit-action-bar--${variant}`">
    <div v-if="$slots.status" class="edit-action-bar__status" aria-live="polite"><slot name="status" /></div>
    <slot />
  </div>
</template>

<style scoped>
.edit-action-bar {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: var(--charity-admin-space-2);
  border-top: 1px solid var(--charity-admin-border);
}

.edit-action-bar--sticky {
  position: sticky;
  bottom: 0;
  z-index: 10;
  margin: 0 calc(var(--charity-admin-space-6) * -1) calc(var(--charity-admin-space-6) * -1);
  padding: var(--charity-admin-space-3) var(--charity-admin-space-6);
  background: var(--charity-admin-bg-surface-2);
}

.edit-action-bar--inline {
  margin-top: var(--charity-admin-space-4);
  padding-top: var(--charity-admin-space-4);
}

.edit-action-bar__status {
  margin-right: auto;
  min-width: 0;
}

@media (max-width: 767px) {
  .edit-action-bar {
    flex-wrap: wrap;
    justify-content: stretch;
  }

  .edit-action-bar--sticky {
    margin-inline: calc(var(--charity-admin-space-4) * -1);
    margin-bottom: calc(var(--charity-admin-space-4) * -1);
    padding-inline: var(--charity-admin-space-4);
  }

  .edit-action-bar__status {
    flex: 0 0 100%;
  }

  .edit-action-bar :deep(.el-button) {
    flex: 1;
  }

  .edit-action-bar__status :deep(.el-button) {
    flex: none;
  }
}
</style>

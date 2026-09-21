<script setup lang="ts">
/**
 * 頁面列（docs/21-admin-ui.md §1.2／§3.2）：模組標題＋「這裡管理的是」meta 列固定兩行。
 *
 * 這一列在深色底下用 surface 色階（比系統列 surface-2 暗一階，屬於「內容邊框」而不是「系統膠條」，
 * 見 §1.2 的理由），full-bleed 到內容區左右與頂端邊緣（用負邊界抵消 AdminLayout 的內距），
 * 讓它在視覺上是獨立於下方內容的一條「band」，不是隨便一塊卡片。
 *
 * 「這裡管理的是」不再跟標題擠同一行（§6.2 的結構性修法，順帶解決 §14.3 記錄的手機換行踩雷）——
 * meta 永遠是獨立的第二行，桌面與手機共用同一份排版規則，不需要另外寫斷點例外。
 */
withDefaults(
  defineProps<{
    title: string
  }>(),
  {},
)
</script>

<template>
  <div class="page-header">
    <div class="page-header__row">
      <slot name="back" />
      <h1 class="page-header__title">{{ title }}</h1>
    </div>
    <div class="page-header__meta">
      <slot name="meta" />
    </div>
  </div>
</template>

<style scoped>
.page-header {
  background: var(--admin-bg-surface);
  border-bottom: 1px solid var(--admin-border);
  margin: calc(var(--admin-space-6) * -1) calc(var(--admin-space-6) * -1) var(--admin-space-6);
  padding: var(--admin-space-3) var(--admin-space-6);
  min-height: var(--admin-topbar-page-height);
  display: flex;
  flex-direction: column;
  justify-content: center;
  gap: var(--admin-space-1);
}

.page-header__row {
  display: flex;
  align-items: center;
  gap: var(--admin-space-3);
  min-width: 0;
}

.page-header__title {
  font-size: var(--el-font-size-extra-large, 20px);
  line-height: 1.3;
  margin: 0;
  color: var(--admin-text-primary);
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.page-header__meta {
  min-height: 18px;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--admin-space-3);
}

.page-header__meta:empty {
  display: none;
}

/* 手機（§8.2）：標題已經在合併後的單列頂欄（AppTopbar）顯示過一次，這裡不重複；
   「這裡管理的是」meta 列則保留，落到內容區最上方（這個元件本來就在內容區內，
   不需要額外搬動位置，只是拿掉標題這一行）。back 插槽（返回列表）維持顯示。 */
@media (max-width: 767px) {
  .page-header {
    margin: calc(var(--admin-space-4) * -1) calc(var(--admin-space-4) * -1) var(--admin-space-4);
    padding: var(--admin-space-2) var(--admin-space-4);
    min-height: 0;
  }

  .page-header__title {
    display: none;
  }
}
</style>

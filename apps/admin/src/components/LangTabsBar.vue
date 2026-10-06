<script setup lang="ts">
/**
 * 語言分頁（docs/21 §3）：**整頁一組**，放在編輯頁頂端（PageHeader 之下），把整個編輯區包在裡面；
 * 它 provide 唯一一個語言範圍給主欄與右側欄的所有雙語欄位，切到英文時全頁雙語欄位一起換，單語欄位照常顯示。
 *
 * - `variant="page"`（預設）：在主內容捲動容器內 sticky 於頂端，捲到深處也能切換。
 *   欄位的 scroll-margin 已把這條分頁列的高度算進去，定位時不會被遮住。
 * - `variant="bare"`：對話框專用，不 sticky；對話框自己一組，與頁面分頁是不同情境。
 * - 預設中文；切換只是畫面狀態，不算未儲存變更；唯讀時仍可切換；方向鍵／Home／End 切換。
 * - 標籤文字顯示整頁合計：「英文 （N 項尚未翻譯）」「⚠ N 處需修正」。未翻譯＝中文有值、英文空。
 * - 兩種語言都用 v-show 留在 DOM（保留輸入狀態與編輯器）。每頁（或每個對話框）恰好一個。
 */
import { useId } from 'vue'
import { provideLangScope, type Lang } from '@/composables/useLangScope'
import LangTablist from '@/components/LangTablist.vue'

const props = withDefaults(
  defineProps<{
    langs?: Lang[]
    variant?: 'page' | 'bare'
    label?: string
  }>(),
  { langs: () => ['zh', 'en'], variant: 'page', label: undefined },
)

const scope = provideLangScope(props.langs, `lang-${useId()}`)
</script>

<template>
  <div class="lang-tabs-bar" :class="`lang-tabs-bar--${variant}`">
    <div class="lang-tabs-bar__bar">
      <span v-if="variant === 'page'" class="lang-tabs-bar__label">編輯語言</span>
      <LangTablist :scope="scope" :label="label ?? '編輯語言'" />
    </div>
    <slot />
  </div>
</template>

<style scoped>
.lang-tabs-bar--page > .lang-tabs-bar__bar {
  position: sticky;
  top: 0;
  z-index: 9;
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: var(--admin-space-2) var(--admin-space-3);
  margin: 0 calc(var(--admin-space-6) * -1) var(--admin-space-4);
  padding: var(--admin-space-2) var(--admin-space-6);
  background: var(--admin-bg-canvas);
  border-bottom: 1px solid var(--admin-border);
}

.lang-tabs-bar__label {
  font-size: 13px;
  color: var(--admin-text-secondary);
}

.lang-tabs-bar--bare > .lang-tabs-bar__bar {
  margin-bottom: var(--admin-space-3);
}

@media (max-width: 767px) {
  .lang-tabs-bar--page > .lang-tabs-bar__bar {
    margin-inline: calc(var(--admin-space-4) * -1);
    padding-inline: var(--admin-space-4);
  }
}
</style>

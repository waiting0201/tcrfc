<script setup lang="ts">
/**
 * 雙語卡片：一張卡片一組「中文／英文」分頁（docs/21 §3）。
 *
 * - 每張卡片的分頁各自獨立、預設停在中文；切換分頁只是畫面狀態，不算未儲存變更；唯讀時仍可切換。
 * - 分頁標籤直接寫出狀態：「英文（3 項尚未翻譯）」「⚠ 1 處需修正」（符號 aria-hidden，文字才是訊息）。
 *   未翻譯＝中文有值、英文空。
 * - 兩種語言的內容都用 v-show 留在 DOM（保留輸入狀態與編輯器），由雙語欄位／LangPane 依目前語言自行顯示。
 * - variant：`card`（預設，帶卡片外框與標題）、`bare`（只有分頁列，用在對話框與 *Tab／*Panel）。
 */
import { useId } from 'vue'
import { provideLangScope, type Lang } from '@/composables/useLangScope'
import LangTablist from '@/components/LangTablist.vue'

const props = withDefaults(
  defineProps<{
    header?: string
    langs?: Lang[]
    variant?: 'card' | 'bare'
  }>(),
  { header: undefined, langs: () => ['zh', 'en'], variant: 'card' },
)

const scope = provideLangScope(props.langs, `lang-${useId()}`)
</script>

<template>
  <el-card v-if="variant === 'card'" shadow="never" class="lang-tabs-card">
    <template #header>
      <div class="lang-tabs-card__head">
        <span v-if="header" class="lang-tabs-card__title">{{ header }}</span>
        <LangTablist :scope="scope" :label="header" />
      </div>
    </template>
    <slot />
  </el-card>

  <div v-else class="lang-tabs-card lang-tabs-card--bare">
    <LangTablist :scope="scope" :label="header" class="lang-tabs-card__bare-list" />
    <slot />
  </div>
</template>

<style scoped>
.lang-tabs-card__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: var(--admin-space-2) var(--admin-space-4);
}

.lang-tabs-card__title {
  font-weight: 600;
}

.lang-tabs-card__bare-list {
  margin-bottom: var(--admin-space-3);
}
</style>

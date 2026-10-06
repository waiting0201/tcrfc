<script setup lang="ts">
/** LangTabsCard 的分頁列（內部元件）：role=tablist，方向鍵／Home／End 切換，標籤文字帶未翻譯與錯誤數。 */
import { computed } from 'vue'
import { LANG_LABEL, type LangScope } from '@/composables/useLangScope'

const props = defineProps<{ scope: LangScope; label?: string }>()

const tabs = computed(() =>
  props.scope.langs.map((lang) => ({
    lang,
    text: LANG_LABEL[lang],
    untranslated: lang === 'zh' ? 0 : props.scope.untranslatedCount.value,
    errors: props.scope.errorCount(lang),
  })),
)

function onKeydown(e: KeyboardEvent, index: number) {
  const langs = props.scope.langs
  const n = langs.length
  let next = -1
  if (e.key === 'ArrowRight' || e.key === 'ArrowDown') next = (index + 1) % n
  else if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') next = (index - 1 + n) % n
  else if (e.key === 'Home') next = 0
  else if (e.key === 'End') next = n - 1
  if (next < 0) return
  e.preventDefault()
  props.scope.setLang(langs[next])
  document.getElementById(props.scope.tabId(langs[next]))?.focus()
}
</script>

<template>
  <div class="lang-tablist" role="tablist" :aria-label="label ? `${label}的語言` : '內容語言'">
    <button
      v-for="(tab, i) in tabs"
      :id="scope.tabId(tab.lang)"
      :key="tab.lang"
      type="button"
      role="tab"
      class="lang-tablist__tab"
      :class="{ 'is-active': scope.current.value === tab.lang }"
      :aria-selected="scope.current.value === tab.lang"
      :tabindex="scope.current.value === tab.lang ? 0 : -1"
      @click="scope.setLang(tab.lang)"
      @keydown="onKeydown($event, i)"
    >
      {{ tab.text }}
      <span v-if="tab.untranslated > 0" class="lang-tablist__note">（{{ tab.untranslated }} 項尚未翻譯）</span>
      <span v-if="tab.errors > 0" class="lang-tablist__note lang-tablist__note--error">
        <span aria-hidden="true">⚠</span> {{ tab.errors }} 處需修正
      </span>
    </button>
  </div>
</template>

<style scoped>
.lang-tablist {
  display: flex;
  flex-wrap: wrap;
  gap: var(--admin-space-1);
}

.lang-tablist__tab {
  appearance: none;
  display: inline-flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0 var(--admin-space-1);
  min-height: 32px;
  padding: 4px var(--admin-space-3);
  font: inherit;
  font-size: 13px;
  color: var(--admin-text-secondary);
  background: transparent;
  border: 1px solid var(--admin-border-input);
  border-radius: 2px;
  cursor: pointer;
}

.lang-tablist__tab:hover {
  color: var(--admin-text-primary);
  background: var(--admin-bg-surface-2);
}

.lang-tablist__tab:focus-visible {
  outline: 2px solid var(--admin-border-focus);
  outline-offset: 2px;
}

/* 目前分頁：底色加深、文字加粗、底線（不只靠顏色） */
.lang-tablist__tab.is-active {
  color: var(--admin-text-primary);
  font-weight: 600;
  background: var(--admin-bg-surface-2);
  border-color: var(--admin-primary);
  box-shadow: inset 0 -2px 0 var(--admin-primary);
}

.lang-tablist__note {
  font-size: 12px;
  font-weight: 400;
  color: var(--admin-text-tertiary);
}

.lang-tablist__note--error {
  color: var(--admin-danger-text);
  font-weight: 600;
}
</style>

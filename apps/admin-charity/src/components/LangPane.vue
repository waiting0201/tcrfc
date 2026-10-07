<script setup lang="ts">
/**
 * 語言分頁裡的自訂內容（例如項目說明的區塊編輯器）。放在 LangTabsBar 內，每種語言一個：
 *
 *   <LangTabsBar>
 *     <LangPane lang="zh" field="descriptionZh">…</LangPane>
 *     <LangPane lang="en" field="descriptionEn" :untranslated="!!zhHasValue && !enHasValue">…</LangPane>
 *   </LangTabsBar>
 *
 * - 用 v-show 留在 DOM，輸入狀態與編輯器不會因切換而遺失。
 * - `field`：這個語言內容的欄位鍵，供分頁標籤計算「需修正」數（欄位本身的錯誤用 FormField 顯示）。
 * - `untranslated`：由呼叫端算好傳入（英文窗格傳「中文有值且英文空」）；只有非中文窗格會被計入。
 * - `show` 事件：窗格每次從隱藏變為顯示（含第一次）後觸發。
 */
import { nextTick, onBeforeUnmount, onMounted, watch } from 'vue'
import { useRequiredLangScope, type Lang } from '@/composables/useLangScope'

const props = withDefaults(defineProps<{ lang: Lang; field?: string; untranslated?: boolean }>(), {
  field: undefined,
  untranslated: false,
})
const emit = defineEmits<{ (e: 'show'): void }>()

const scope = useRequiredLangScope('LangPane')

let unregister: (() => void) | null = null
onMounted(() => {
  unregister = scope.register({
    id: Symbol('LangPane'),
    keys: { [props.lang]: props.field },
    untranslated: () => props.lang !== 'zh' && props.untranslated,
  })
})
onBeforeUnmount(() => unregister?.())

watch(
  () => scope.current.value === props.lang,
  async (visible) => {
    if (!visible) return
    await nextTick()
    emit('show')
  },
)
</script>

<template>
  <div v-show="scope.current.value === lang" class="lang-pane" role="tabpanel" :aria-labelledby="scope.tabId(lang)">
    <slot />
  </div>
</template>

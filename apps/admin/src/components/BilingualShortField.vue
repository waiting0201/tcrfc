<script setup lang="ts">
/**
 * 雙語短欄位（docs/21-admin-ui.md §3）：必須放在 LangTabsBar 內，只顯示目前語言，
 * 兩種語言都留在 DOM（v-show）；未翻譯與錯誤數由分頁標籤呈現。
 * 不在 LangTabsBar 內時不渲染任何東西，並在開發模式 console.warn（見 useBilingualField）。
 */
import { useBilingualField } from '@/composables/useBilingualField'
import FormField from '@/components/FormField.vue'

const props = withDefaults(
  defineProps<{
    label: string
    zh: string
    en: string
    required?: boolean
    placeholder?: string
    /** 欄位鍵基底：`field="name"` → 錯誤鍵 `nameZh`／`nameEn`（只在程式內使用，不顯示）。 */
    field?: string
    fieldZh?: string
    fieldEn?: string
    maxlength?: number
  }>(),
  { required: false, placeholder: '', field: undefined, fieldZh: undefined, fieldEn: undefined, maxlength: undefined },
)

const emit = defineEmits<{
  (e: 'update:zh', value: string): void
  (e: 'update:en', value: string): void
}>()

const { scope, keyZh, keyEn } = useBilingualField(props, 'BilingualShortField')
</script>

<template>
  <template v-if="scope">
    <FormField v-show="scope.current.value === 'zh'" :field="keyZh()" :label="`${label}（中文）`" :required="required" lang="zh">
      <el-input
        :model-value="zh"
        :placeholder="placeholder"
        :maxlength="maxlength"
        :show-word-limit="!!maxlength"
        @update:model-value="(v: string) => emit('update:zh', v)"
      />
    </FormField>
    <FormField v-show="scope.current.value === 'en'" :field="keyEn()" :label="`${label}（英文）`" lang="en">
      <el-input
        :model-value="en"
        :placeholder="placeholder"
        :maxlength="maxlength"
        :show-word-limit="!!maxlength"
        @update:model-value="(v: string) => emit('update:en', v)"
      />
    </FormField>
  </template>
</template>

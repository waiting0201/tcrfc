<script setup lang="ts">
/**
 * 雙語多行文字欄位——`BilingualShortField.vue` 的多行版本（docs/21-admin-ui.md §3 同一套雙語
 * 呈現規則：放在 LangTabsBar 內、只顯示目前語言），差別只在單行 `el-input` 換成
 * `type="textarea"`。獨立成元件而不是複製貼上，是因為 SEO 說明這類欄位在別的模組
 * （B4 常見問題、B5 慈善介紹……）以後也用得到同一種形狀。
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
    rows?: number
  }>(),
  { required: false, placeholder: '', field: undefined, fieldZh: undefined, fieldEn: undefined, maxlength: undefined, rows: 3 },
)

const emit = defineEmits<{
  (e: 'update:zh', value: string): void
  (e: 'update:en', value: string): void
}>()

const { scope, keyZh, keyEn } = useBilingualField(props, 'BilingualTextareaField')
</script>

<template>
  <template v-if="scope">
    <FormField v-show="scope.current.value === 'zh'" :field="keyZh()" :label="`${label}（中文）`" :required="required" lang="zh">
      <el-input
        type="textarea"
        :rows="rows"
        :model-value="zh"
        :placeholder="placeholder"
        :maxlength="maxlength"
        :show-word-limit="!!maxlength"
        @update:model-value="(v: string) => emit('update:zh', v)"
      />
    </FormField>
    <FormField v-show="scope.current.value === 'en'" :field="keyEn()" :label="`${label}（英文）`" lang="en">
      <el-input
        type="textarea"
        :rows="rows"
        :model-value="en"
        :placeholder="placeholder"
        :maxlength="maxlength"
        :show-word-limit="!!maxlength"
        @update:model-value="(v: string) => emit('update:en', v)"
      />
    </FormField>
  </template>
</template>

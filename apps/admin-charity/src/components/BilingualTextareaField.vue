<script setup lang="ts">
/**
 * 雙語多行文字欄位——`BilingualShortField` 的多行版本，差別只在單行 `el-input` 換成 `type="textarea"`。
 * 規則同上：必須放在 LangTabsBar 內，只顯示目前語言，`field="name"` → `nameZh`／`nameEn`。
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
    field?: string
    fieldZh?: string
    fieldEn?: string
    maxlength?: number
    disabled?: boolean
    rows?: number
    /** 欄位下方的一行說明（兩種語言共用）。 */
    hint?: string
  }>(),
  { required: false, placeholder: '', field: undefined, fieldZh: undefined, fieldEn: undefined, maxlength: undefined, disabled: false, rows: 3, hint: undefined },
)

const emit = defineEmits<{
  (e: 'update:zh', value: string): void
  (e: 'update:en', value: string): void
}>()

const { scope, keyZh, keyEn } = useBilingualField(props, 'BilingualTextareaField')
</script>

<template>
  <FormField v-show="scope.current.value === 'zh'" :field="keyZh()" :label="`${label}（中文）`" :required="required" lang="zh">
    <el-input
      type="textarea"
      :rows="rows"
      :model-value="zh"
      :placeholder="placeholder"
      :maxlength="maxlength"
      :disabled="disabled"
      :show-word-limit="!!maxlength"
      @update:model-value="(v: string) => emit('update:zh', v)"
    />
    <div v-if="hint" class="bilingual-textarea-field__hint">{{ hint }}</div>
  </FormField>
  <FormField v-show="scope.current.value === 'en'" :field="keyEn()" :label="`${label}（英文）`" lang="en">
    <el-input
      type="textarea"
      :rows="rows"
      :model-value="en"
      :placeholder="placeholder"
      :maxlength="maxlength"
      :disabled="disabled"
      :show-word-limit="!!maxlength"
      @update:model-value="(v: string) => emit('update:en', v)"
    />
    <div v-if="hint" class="bilingual-textarea-field__hint">{{ hint }}</div>
  </FormField>
</template>

<style scoped>
.bilingual-textarea-field__hint {
  width: 100%;
  margin-top: var(--charity-admin-space-1);
  font-size: 12px;
  line-height: 1.6;
  color: var(--charity-admin-text-tertiary);
}
</style>

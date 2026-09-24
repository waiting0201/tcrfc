<script setup lang="ts">
/**
 * 雙語多行文字欄位——`BilingualShortField.vue` 的多行版本（docs/21-admin-ui.md §3 同一套雙語
 * 呈現規則：桌面／平板並排雙欄，手機改用 `el-tabs`），差別只在單行 `el-input` 換成
 * `type="textarea"`。獨立成元件而不是複製貼上，是因為 SEO 說明這類欄位在別的模組
 * （B4 常見問題、B5 慈善介紹……）以後也用得到同一種形狀。
 */
import { computed } from 'vue'
import { useBreakpoint } from '@/composables/useBreakpoint'

const props = withDefaults(
  defineProps<{
    label: string
    zh: string
    en: string
    required?: boolean
    placeholder?: string
    rows?: number
  }>(),
  { required: false, placeholder: '', rows: 3 },
)

const emit = defineEmits<{
  (e: 'update:zh', value: string): void
  (e: 'update:en', value: string): void
}>()

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const isUntranslated = computed(() => !props.en.trim())
</script>

<template>
  <div v-if="!isMobile" class="bilingual-textarea-field">
    <el-form-item :label="`${label}（中文）`" :required="required" class="bilingual-textarea-field__col">
      <el-input
        type="textarea"
        :rows="rows"
        :model-value="zh"
        :placeholder="placeholder"
        @update:model-value="(v: string) => emit('update:zh', v)"
      />
    </el-form-item>
    <el-form-item class="bilingual-textarea-field__col">
      <template #label>
        {{ label }}（英文）
        <el-tag v-if="isUntranslated" size="small" type="info" class="bilingual-textarea-field__tag">
          尚未翻譯
        </el-tag>
      </template>
      <el-input
        type="textarea"
        :rows="rows"
        :model-value="en"
        :placeholder="placeholder"
        @update:model-value="(v: string) => emit('update:en', v)"
      />
    </el-form-item>
  </div>

  <el-form-item v-else :label="label" :required="required">
    <el-tabs class="bilingual-textarea-field__tabs">
      <el-tab-pane label="中文">
        <el-input
          type="textarea"
          :rows="rows"
          :model-value="zh"
          :placeholder="placeholder"
          @update:model-value="(v: string) => emit('update:zh', v)"
        />
      </el-tab-pane>
      <el-tab-pane>
        <template #label>
          英文
          <el-tag v-if="isUntranslated" size="small" type="info">尚未翻譯</el-tag>
        </template>
        <el-input
          type="textarea"
          :rows="rows"
          :model-value="en"
          :placeholder="placeholder"
          @update:model-value="(v: string) => emit('update:en', v)"
        />
      </el-tab-pane>
    </el-tabs>
  </el-form-item>
</template>

<style scoped>
.bilingual-textarea-field {
  display: flex;
  gap: 16px;
}

.bilingual-textarea-field__col {
  flex: 1;
  min-width: 0;
}

.bilingual-textarea-field__tag {
  margin-left: 4px;
}

.bilingual-textarea-field__tabs :deep(.el-tabs__content) {
  padding-top: 4px;
}
</style>

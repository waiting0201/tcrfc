<script setup lang="ts">
/**
 * 雙語短欄位（docs/21-admin-ui.md §3）：
 * - 桌面／平板：並排雙欄，中文在左、英文在右，方便一眼比對翻譯是否對得起來
 * - 手機（< 768px）：並排在窄螢幕擠不下兩個可用的 input，改用 el-tabs 分頁（docs/21 §8）
 */
import { computed } from 'vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
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

// 以下兩行只給過渡期的舊版畫面（沒放進 LangTabsBar）使用
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const isUntranslated = computed(() => !props.en.trim())
</script>

<template>
  <!-- 在 LangTabsBar 內：只顯示目前語言，兩種語言都留在 DOM（v-show）；未翻譯與錯誤數由分頁標籤呈現 -->
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

  <!-- 過渡期舊版（第 4 階段刪除）：不在 LangTabsBar 內時維持原本的並排／手機分頁 -->
  <template v-else>
    <div v-if="!isMobile" class="bilingual-short-field">
      <el-form-item :label="`${label}（中文）`" :required="required" class="bilingual-short-field__col">
        <el-input
          :model-value="zh"
          :placeholder="placeholder"
          @update:model-value="(v: string) => emit('update:zh', v)"
        />
      </el-form-item>
      <el-form-item class="bilingual-short-field__col">
        <template #label>
          {{ label }}（英文）
          <el-tag v-if="isUntranslated" size="small" type="info" class="bilingual-short-field__tag">
            尚未翻譯
          </el-tag>
        </template>
        <el-input
          :model-value="en"
          :placeholder="placeholder"
          @update:model-value="(v: string) => emit('update:en', v)"
        />
      </el-form-item>
    </div>

    <el-form-item v-else :label="label" :required="required" class="bilingual-short-field--mobile">
      <el-tabs class="bilingual-short-field__tabs">
        <el-tab-pane label="中文">
          <el-input
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
            :model-value="en"
            :placeholder="placeholder"
            @update:model-value="(v: string) => emit('update:en', v)"
          />
        </el-tab-pane>
      </el-tabs>
    </el-form-item>
  </template>
</template>

<style scoped>
.bilingual-short-field {
  display: flex;
  gap: 16px;
}

.bilingual-short-field__col {
  flex: 1;
  min-width: 0;
}

.bilingual-short-field__tag {
  margin-left: 4px;
}

.bilingual-short-field__tabs :deep(.el-tabs__content) {
  padding-top: 4px;
}
</style>

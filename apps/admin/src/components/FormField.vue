<script setup lang="ts">
/**
 * 表單欄位外殼：包 el-form-item，並接上頁面的欄位錯誤（useFormErrors）。
 *
 * - `field` 是程式內部的欄位鍵（`slug`、`nameZh`），寫在 data-field 與錯誤對照上，不會顯示在畫面。
 * - 有錯誤時：欄位外框變 2px 危險色（形狀改變，不只靠顏色）、欄位下方出現「⚠ 訊息」
 *   （role="alert"）、第一個可聚焦元件加 aria-invalid 與 aria-describedby。
 * - 使用者在欄位內輸入或選擇（input／change 事件）就清掉該鍵的錯誤；
 *   el-select 這類不冒泡 DOM 事件的元件，請在更新處理函式裡自己呼叫 `formErrors.clear(key)`。
 * - 欄位放在語言分頁裡時，傳 `lang`，定位時會先切到該語言；放在頁面層分頁或摺疊區時傳 `reveal`。
 */
import { computed, nextTick, onBeforeUnmount, onMounted, ref, useId, watch } from 'vue'
import { useFormErrors, firstFocusable } from '@/composables/useFormErrors'
import { useLangScope, type Lang } from '@/composables/useLangScope'

const props = withDefaults(
  defineProps<{
    field: string
    label?: string
    required?: boolean
    lang?: Lang
    reveal?: () => void | Promise<void>
  }>(),
  { label: undefined, required: false, lang: undefined, reveal: undefined },
)

const formErrors = useFormErrors()
const scope = useLangScope()
const itemRef = ref<{ $el: HTMLElement } | null>(null)
const errorId = `${useId()}-error`

const message = computed(() => formErrors.get(props.field))

let unregister: (() => void) | null = null
onMounted(() => {
  const el = itemRef.value?.$el
  if (!el) return
  unregister = formErrors.registerAnchor({ key: props.field, el, lang: props.lang, scope: props.lang ? scope : null, reveal: props.reveal })
})
onBeforeUnmount(() => unregister?.())

// 把錯誤狀態標到第一個可聚焦元件上（螢幕閱讀器不依賴顏色即可得知）
watch(
  message,
  async (msg) => {
    await nextTick()
    const el = itemRef.value?.$el
    const target = el ? firstFocusable(el) : null
    if (!target) return
    if (msg) {
      target.setAttribute('aria-invalid', 'true')
      target.setAttribute('aria-describedby', errorId)
    } else {
      target.removeAttribute('aria-invalid')
      target.removeAttribute('aria-describedby')
    }
  },
  { flush: 'post' },
)

function onUserChange() {
  if (message.value) formErrors.clear(props.field)
}
</script>

<template>
  <el-form-item
    ref="itemRef"
    class="form-field"
    :label="label"
    :required="required"
    :error="message"
    :data-field="field"
    @input="onUserChange"
    @change="onUserChange"
  >
    <slot />
    <template #error="{ error }">
      <div :id="errorId" :key="`${field}-error`" class="form-field__error" role="alert">
        <span aria-hidden="true" class="form-field__error-icon">⚠</span>{{ error }}
      </div>
    </template>
  </el-form-item>
</template>

<style scoped>
.form-field {
  scroll-margin-block: 96px;
}

.form-field__error {
  flex: 0 0 100%;
  margin-top: var(--admin-space-1);
  font-size: 12px;
  line-height: 1.5;
  color: var(--admin-danger-text);
}

.form-field__error-icon {
  margin-right: var(--admin-space-1);
}
</style>

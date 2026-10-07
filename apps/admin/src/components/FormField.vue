<script setup lang="ts">
/**
 * 表單欄位外殼：包 el-form-item，並接上頁面的欄位錯誤（useFormErrors）。
 *
 * - `field` 是程式內部的欄位鍵（`slug`、`nameZh`），寫在 data-field 與錯誤對照上，不會顯示在畫面。
 * - 有錯誤時：欄位外框變 2px 危險色（形狀改變，不只靠顏色）、欄位下方出現「⚠ 訊息」
 *   （role="alert"）、第一個可聚焦元件加 aria-invalid 與 aria-describedby。
 * - 使用者修改欄位就清掉該鍵的錯誤，兩層保險都自動生效、不必逐頁處理：
 *   1. 原生 input／change 事件冒泡（文字輸入、原生元素）；
 *   2. 「值探針」`ValueProbe`：el-select／el-date-picker／el-switch／el-checkbox／el-radio／
 *      el-input-number／el-cascader 等不一定發原生事件的元件，改看插槽內元件的 `modelValue`——
 *      插槽在探針自己的渲染裡執行，值一變探針就重算簽章，與上次不同即清錯誤。
 *      （只認直接寫在插槽裡、帶 v-model／:model-value 的元件；包在自訂元件內部的值不在其內，
 *      該情況仍可在更新處理函式裡自己呼叫 `formErrors.clear(key)`。）
 * - 欄位放在語言分頁裡時，傳 `lang`，定位時會先切到該語言；放在頁面層分頁或摺疊區時傳 `reveal`。
 */
import { computed, defineComponent, nextTick, onBeforeUnmount, onMounted, ref, useId, watch, type VNode } from 'vue'
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

/** 遞迴收集插槽 vnode 樹裡所有 modelValue（Fragment、v-for、元素子節點的陣列都會走訪）。 */
function collectModelValues(nodes: unknown, out: unknown[]) {
  if (!Array.isArray(nodes)) return
  for (const n of nodes as Array<VNode | unknown>) {
    if (Array.isArray(n)) collectModelValues(n, out)
    else if (n && typeof n === 'object') {
      const vn = n as VNode
      const props = vn.props as Record<string, unknown> | null
      if (props && ('modelValue' in props || 'model-value' in props)) out.push(props.modelValue ?? props['model-value'])
      collectModelValues(vn.children, out)
    }
  }
}

function signatureOf(values: unknown[]): string {
  try {
    return JSON.stringify(values)
  } catch {
    return String(values.length)
  }
}

/**
 * 值探針：不渲染任何額外 DOM，只把插槽原樣交回；每次（因值改變而）重新渲染時比對 modelValue 簽章，
 * 第一次只記基準，之後與上次不同就通知 `onChanged`。
 */
const ValueProbe = defineComponent({
  name: 'FormFieldValueProbe',
  props: { onChanged: { type: Function, required: true } },
  setup(probeProps, { slots }) {
    let last: string | null = null
    return () => {
      const nodes = slots.default?.() ?? []
      const values: unknown[] = []
      collectModelValues(nodes, values)
      const sig = signatureOf(values)
      if (last !== null && sig !== last) void nextTick(() => (probeProps.onChanged as () => void)())
      last = sig
      return nodes
    }
  },
})

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
    <ValueProbe :on-changed="onUserChange"><slot /></ValueProbe>
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

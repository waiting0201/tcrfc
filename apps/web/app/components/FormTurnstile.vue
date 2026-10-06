<script setup lang="ts">
// app/components/FormTurnstile.vue — 公開表單的 Cloudflare Turnstile 人機驗證（S1-17）。
//
// 只由 useFormSubmit 判定「site key 有值且該表單 captchaEnabled === true」後才被頁面渲染（v-if），
// 所以本元件本身不再判斷開關。Cloudflare 腳本只在本元件掛載時才載入（explicit render），
// 沒開驗證的表單與其他頁面完全不會連到 challenges.cloudflare.com。
// 權杖只能用一次：送出失敗後由父層呼叫 `reset()` 取新權杖（useFormSubmit 已代為處理）。
// 與慈善前台的 TurnstileWidget 各自一份、互不引用（兩個 app 是獨立專案）。
const props = defineProps<{ siteKey: string }>()
const emit = defineEmits<{ (e: 'token', value: string | null): void }>()

const { tx, isEn } = useLocale()

interface TurnstileApi {
  render: (el: HTMLElement, options: Record<string, unknown>) => string
  reset: (widgetId?: string) => void
  remove: (widgetId?: string) => void
}

const container = ref<HTMLElement | null>(null)
let widgetId: string | undefined

function getApi(): TurnstileApi | undefined {
  return (window as unknown as { turnstile?: TurnstileApi }).turnstile
}

function loadScript(): Promise<void> {
  return new Promise((resolve, reject) => {
    if (getApi()) return resolve()
    const existing = document.querySelector<HTMLScriptElement>('script[data-turnstile]')
    const script = existing ?? document.createElement('script')
    script.addEventListener('load', () => resolve(), { once: true })
    script.addEventListener('error', () => reject(new Error('turnstile script failed')), { once: true })
    if (!existing) {
      script.src = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit'
      script.async = true
      script.dataset.turnstile = '1'
      document.head.appendChild(script)
    }
  })
}

onMounted(async () => {
  try {
    await loadScript()
    const api = getApi()
    if (!api || !container.value) return
    widgetId = api.render(container.value, {
      sitekey: props.siteKey,
      language: isEn.value ? 'en' : 'zh-tw',
      callback: (token: string) => emit('token', token),
      'expired-callback': () => emit('token', null),
      'error-callback': () => emit('token', null),
    })
  }
  catch {
    // 腳本載入失敗：不放行也不擋畫面，送出時會因為沒有權杖而提示「請先完成人機驗證」。
    emit('token', null)
  }
})

onBeforeUnmount(() => {
  if (widgetId) getApi()?.remove(widgetId)
})

function reset() {
  emit('token', null)
  if (widgetId) getApi()?.reset(widgetId)
}

defineExpose({ reset })
</script>

<template>
  <div ref="container" class="form-turnstile__widget" role="group" :aria-label="tx('機器人驗證', 'Bot verification')" />
</template>

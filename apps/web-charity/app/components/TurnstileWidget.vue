<script setup lang="ts">
// TurnstileWidget.vue — Cloudflare Turnstile 人機驗證（規劃書 §3.3「防濫用」）。
//
// 只在 `NUXT_PUBLIC_TURNSTILE_SITE_KEY` 有給值時才渲染（後端對應 TURNSTILE_SECRET_KEY_CHARITY）。
// 後端沒有「是否啟用」的查詢端點，所以兩邊的開關必須一起開：後端啟用、前端沒給 site key，建單會回 422；
// 反過來前端給了、後端沒啟用，權杖只是被忽略。見 README「Turnstile」。
// 權杖只能用一次：建單失敗後呼叫 `reset()` 取新權杖。
const props = defineProps<{ siteKey: string, lang: 'zh' | 'en' }>()
const emit = defineEmits<{ (e: 'token', value: string | null): void }>()

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
      language: props.lang === 'en' ? 'en' : 'zh-tw',
      callback: (token: string) => emit('token', token),
      'expired-callback': () => emit('token', null),
      'error-callback': () => emit('token', null),
    })
  } catch {
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
  <div ref="container" class="turnstile-slot" />
</template>

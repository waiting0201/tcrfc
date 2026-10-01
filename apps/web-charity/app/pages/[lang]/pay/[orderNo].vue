<script setup lang="ts">
// pages/[lang]/pay/[orderNo].vue — 模擬 LINE Pay 付款頁（僅限本機／預備環境）。
//
// 對應後端的 `FakePaymentGateway`：它把付款網址指回前台這一頁 `/{lang}/pay/{單號}?transactionId=…`
// （apps/api README「慈善 CH-2／CH-3」）。真實的 LINE Pay 會直接帶著 transactionId 返回結果頁
// `/{lang}/result/{單號}?transactionId=…`（取消則帶 `?cancel=1`），不會經過這一頁。
// 🔴 正式環境不得出現：只有 `import.meta.dev` 或明確設定 `NUXT_PUBLIC_SIMULATED_PAYMENT=true` 才開，其餘回 404。
import { useLang } from '../../../composables/useLang'

definePageMeta({ layout: 'default' })

const route = useRoute()
const orderNo = route.params.orderNo as string
const config = useRuntimeConfig()
const enabled = import.meta.dev || config.public.simulatedPayment === true || String(config.public.simulatedPayment) === 'true'

const { lang, tr } = useLang()

if (!enabled && import.meta.server) {
  setResponseStatus(useRequestEvent()!, 404)
}

const transactionId = computed(() => (typeof route.query.transactionId === 'string' ? route.query.transactionId : ''))
const successHref = computed(() => `/${lang.value}/result/${encodeURIComponent(orderNo)}?transactionId=${encodeURIComponent(transactionId.value)}`)
const cancelHref = computed(() => `/${lang.value}/result/${encodeURIComponent(orderNo)}?cancel=1`)

useHead(() => ({ title: `${tr.value.pay.simTitle} | ${tr.value.associationName}` }))
</script>

<template>
  <div class="container">
    <section v-if="enabled" class="section text-center">
      <h1>{{ tr.pay.simTitle }}</h1>
      <p class="notice-row">{{ tr.pay.simNotice }}</p>
      <p class="text-secondary">{{ tr.result.orderNo }}：{{ orderNo }}</p>

      <div class="stack" style="max-width: 360px; margin: var(--sp-4) auto;">
        <NuxtLink :to="successHref" class="btn btn-primary">{{ tr.pay.simSuccess }}</NuxtLink>
        <NuxtLink :to="cancelHref" class="btn btn-secondary">{{ tr.pay.simCancel }}</NuxtLink>
      </div>
    </section>

    <section v-else class="section text-center">
      <h1>{{ tr.result.notFound }}</h1>
      <NuxtLink :to="`/${lang}/`" class="btn btn-primary">{{ tr.result.backHome }}</NuxtLink>
    </section>
  </div>
</template>

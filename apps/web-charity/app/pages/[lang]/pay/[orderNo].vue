<script setup lang="ts">
// pages/[lang]/pay/[orderNo].vue — 付款模擬轉場（docs/22-charity-ui.md §2.6，🔴 本輪不接 LINE Pay）。
// 這個 URL（/{lang}/pay/<order_no>）是本檔案自訂的路由，規劃書與 docs/22 都沒有給這一頁指定網址
// （規劃書 §3.4 只描述流程「送出 → 建立捐款單 → 導向 LINE Pay」，沒有前台自己的轉場頁 URL）；
// 選這個路徑純粹是 mockup 動線需要一個可以停留、倒數、導向結果頁的畫面，之後真正串接 LINE Pay 時
// 這一頁會被整個換成「導去 LINE Pay 網域」的一次性轉導，屆時這個路由多半直接淘汰。
import { useLang } from '../../../composables/useLang'
import { useMockOrder } from '../../../composables/useCheckoutDraft'
import { formatTwd } from '../../../utils/currency'

definePageMeta({ layout: 'default' })

const route = useRoute()
const orderNo = route.params.orderNo as string

const { lang, tr } = useLang()

const order = useMockOrder(orderNo)

let timer: ReturnType<typeof setTimeout> | undefined

function proceedToResult() {
  navigateTo(`/${lang.value}/result/${orderNo}?demo=success`)
}

onMounted(() => {
  timer = setTimeout(proceedToResult, 2200)
})

onBeforeUnmount(() => {
  if (timer) clearTimeout(timer)
})

useHead({ title: `${tr.value.pay.heading} | ${tr.value.associationName}` })
</script>

<template>
  <div class="container">
    <section class="section text-center">
      <div class="spinner" role="status" aria-live="polite">
        <span class="visually-hidden">{{ tr.pay.heading }}</span>
      </div>
      <p><strong>{{ tr.pay.heading }}</strong></p>

      <div class="card" style="max-width: 360px; margin: var(--sp-4) auto;">
        <p class="text-secondary" style="margin-bottom: 4px;">{{ tr.pay.orderNo }}：{{ orderNo }}</p>
        <p v-if="order" class="text-secondary" style="margin-bottom: 0;">{{ tr.pay.amount }}：{{ formatTwd(order.amount) }}</p>
      </div>

      <p class="text-tertiary">
        {{ tr.pay.fallbackHint }}
        <button type="button" class="btn-link" @click="proceedToResult">{{ tr.pay.retryLink }}</button>
      </p>
    </section>
  </div>
</template>

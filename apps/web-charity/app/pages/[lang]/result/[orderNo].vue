<script setup lang="ts">
// pages/[lang]/result/[orderNo].vue — 付款返回頁與結果頁三態（docs/22-charity-ui.md §2.7，規劃書 §3.4／§3.5）。
//
// 這一頁同時是 LINE Pay 的返回網址（apps/api 契約，CH-2）：
//   - `?transactionId=…`：付款完成返回 → POST /donations/{單號}/confirm（冪等；重複呼叫只回目前結果）。
//   - `?cancel=1`：使用者在 LINE Pay 取消 → POST /donations/{單號}/cancel（pending → failed，可重試，冪等）。
//   - 兩者都沒有（重新整理、分享連結、信件連結）→ GET /donations/{單號}。
// confirm／cancel 成功後把網址上的一次性參數換掉（router.replace），避免重新整理時重打。
// 🔴 confirm 暫時打不通（金流連線中斷、逾時）時**保留** transactionId 在網址上，並明示「不要重複付款」：
//   後端把這種單留在「處理中（待人工處理）」，禁止重新發起付款（409），使用者重新整理或按「重新查詢結果」會再 confirm。
// 「處理中」（processing:true）每 3 秒輪詢一次；總上限約 3 分鐘，超過就停止並提示稍後回來看，不無限轉圈（規劃書 §3.4）。
// 全頁在瀏覽器端取資料（confirm 是 POST，且輪詢的限流依訪客 IP，必須由瀏覽器直打，見 useCharityApi）。
import { useLang } from '../../../composables/useLang'
import { formatTwd } from '../../../utils/currency'
import { resultStateOf } from '../../../utils/donation-status'
import { interpolate } from '../../../utils/i18n'
import type { PublicDonationResult, StartPaymentResponse } from '../../../types/charity'

definePageMeta({ layout: 'default' })

const route = useRoute()
const router = useRouter()
const orderNo = route.params.orderNo as string

const { lang, tr } = useLang()
const api = useCharityApi()

type Phase = 'loading' | 'ready' | 'notfound' | 'error'

const POLL_INTERVAL_MS = 3000
const POLL_MAX_MS = 3 * 60 * 1000
const TIMEOUT_HINT_MS = 15 * 1000

const phase = ref<Phase>('loading')
const result = ref<PublicDonationResult | null>(null)
const confirmFailed = ref(false)
const pollExpired = ref(false)
const timedOutHint = ref(false)
const retrying = ref(false)
const retryError = ref('')
const shareFeedback = ref(false)

let pollTimer: ReturnType<typeof setTimeout> | undefined
let pollStartedAt = 0
let disposed = false

const state = computed(() => (result.value ? resultStateOf(result.value.status, result.value.processing) : 'processing'))
const isRefunded = computed(() => result.value?.status === 'refunded')

const voucherText = computed(() => {
  const r = result.value
  if (!r) return null
  const kind = r.invoiceMode === 'b2c_invoice' ? tr.value.result.voucherInvoice : tr.value.result.voucherReceipt
  if (r.invoiceStatus === 'issued') {
    const text = r.invoiceNo
      ? interpolate(tr.value.result.voucherIssuedNo, { no: r.invoiceNo })
      : tr.value.result.voucherIssued
    return `${kind}：${text}`
  }
  // 開立失敗對外一律顯示「處理中」，失敗細節是協會內部的人工補開佇列（不嚇捐款人）。
  return `${kind}：${tr.value.result.voucherPending}`
})

const currentUrl = computed(() => (import.meta.client ? `${window.location.origin}${window.location.pathname}` : ''))

function stopPolling() {
  if (pollTimer) clearTimeout(pollTimer)
  pollTimer = undefined
}

function schedulePoll() {
  stopPolling()
  if (disposed || !result.value?.processing) return
  const elapsed = Date.now() - pollStartedAt
  if (elapsed >= POLL_MAX_MS) {
    pollExpired.value = true
    return
  }
  if (elapsed >= TIMEOUT_HINT_MS) timedOutHint.value = true
  pollTimer = setTimeout(async () => {
    await fetchResult()
    schedulePoll()
  }, POLL_INTERVAL_MS)
}

function applyResult(value: PublicDonationResult) {
  result.value = value
  phase.value = 'ready'
  if (!value.processing) {
    pollExpired.value = false
    timedOutHint.value = false
  }
}

async function fetchResult(): Promise<void> {
  const path = `/donations/${encodeURIComponent(orderNo)}`
  try {
    applyResult(await api.request<PublicDonationResult>(path, { query: { lang: lang.value } }))
  } catch (error) {
    const problem = toApiProblem(error)
    if (problem.status === 404) {
      phase.value = 'notfound'
      stopPolling()
    } else if (!result.value) {
      // 第一次就讀不到才顯示錯誤；輪詢途中偶發失敗（含 429）保留上一份結果，下一輪再試。
      phase.value = 'error'
    }
  }
}

function stripOneTimeQuery() {
  router.replace({ query: {} })
}

async function confirmReturn(transactionId: string): Promise<void> {
  try {
    applyResult(await api.request<PublicDonationResult>(`/donations/${encodeURIComponent(orderNo)}/confirm`, {
      method: 'POST',
      query: { lang: lang.value },
      body: { transactionId },
    }))
    confirmFailed.value = false
    stripOneTimeQuery()
  } catch (error) {
    const problem = toApiProblem(error)
    if (problem.status === 404) {
      phase.value = 'notfound'
      return
    }
    // 400（交易識別碼對不上）以外都視為「暫時確認不了」：保留網址上的參數，改讀目前狀態並提示不要重複付款。
    confirmFailed.value = problem.status !== 400
    await fetchResult()
    if (result.value && confirmFailed.value && !result.value.processing && result.value.status !== 'paid') {
      // 後端已有明確結論（例如已失敗）就不必再提示。
      confirmFailed.value = false
    }
  }
}

async function cancelReturn(): Promise<void> {
  try {
    applyResult(await api.request<PublicDonationResult>(`/donations/${encodeURIComponent(orderNo)}/cancel`, {
      method: 'POST',
      query: { lang: lang.value },
    }))
    stripOneTimeQuery()
  } catch {
    await fetchResult()
  }
}

async function init() {
  const tx = typeof route.query.transactionId === 'string' ? route.query.transactionId.trim() : ''
  const cancelled = route.query.cancel === '1'
  pollStartedAt = Date.now()
  if (cancelled) await cancelReturn()
  else if (tx) await confirmReturn(tx)
  else await fetchResult()
  schedulePoll()
}

async function recheck() {
  pollExpired.value = false
  timedOutHint.value = false
  pollStartedAt = Date.now()
  const tx = typeof route.query.transactionId === 'string' ? route.query.transactionId.trim() : ''
  if (confirmFailed.value && tx) await confirmReturn(tx)
  else await fetchResult()
  schedulePoll()
}

async function retryPayment() {
  if (retrying.value) return
  retrying.value = true
  retryError.value = ''
  try {
    const payment = await api.request<StartPaymentResponse>(`/donations/${encodeURIComponent(orderNo)}/pay`, {
      method: 'POST',
      body: { lang: lang.value },
    })
    if (!/^https?:\/\//i.test(payment.paymentUrl)) throw new Error('invalid payment url')
    window.location.assign(payment.paymentUrl)
  } catch (error) {
    const problem = toApiProblem(error)
    retryError.value = problem.status === 0 ? tr.value.form.errorNetwork : (problem.detail ?? tr.value.form.errorSubmitFallback)
    retrying.value = false
    // 狀態可能已改變（例如別處已完成付款），重新讀一次。
    await fetchResult()
  }
}

async function sharePage() {
  const url = currentUrl.value
  if (navigator.share) {
    try {
      await navigator.share({ title: tr.value.result.successTitle, url })
      return
    } catch {
      // 使用者取消分享或裝置不支援，落到下面的複製連結 fallback。
    }
  }
  try {
    await navigator.clipboard.writeText(url)
    shareFeedback.value = true
    setTimeout(() => { shareFeedback.value = false }, 3000)
  } catch {
    // 剪貼簿也不可用時，不做任何事——連結本身已顯示在網址列，使用者仍可手動複製。
  }
}

onMounted(init)
onBeforeUnmount(() => {
  disposed = true
  stopPolling()
})
watch(lang, () => {
  if (phase.value === 'ready') void fetchResult()
})

useHead(() => {
  const title = state.value === 'success'
    ? tr.value.result.successTitle
    : state.value === 'processing' ? tr.value.result.pendingTitle : tr.value.result.incompleteTitle
  return { title: `${title} | ${tr.value.associationName}` }
})
</script>

<template>
  <div class="container">
    <section class="section text-center" style="max-width: 480px; margin: 0 auto;">
      <template v-if="phase === 'loading'">
        <div class="spinner" role="status" aria-live="polite">
          <span class="visually-hidden">{{ tr.result.loading }}</span>
        </div>
        <p class="text-secondary">{{ tr.result.loading }}</p>
      </template>

      <template v-else-if="phase === 'notfound'">
        <h1>{{ tr.result.notFound }}</h1>
        <NuxtLink :to="`/${lang}/`" class="btn btn-primary">{{ tr.result.backHome }}</NuxtLink>
      </template>

      <template v-else-if="phase === 'error' || !result">
        <p class="notice-row" role="alert">{{ tr.result.loadError }}</p>
        <button type="button" class="btn btn-secondary" @click="recheck">{{ tr.result.pendingRecheck }}</button>
      </template>

      <template v-else-if="state === 'success'">
        <div class="result-icon result-icon-success" aria-hidden="true">✓</div>
        <h1>{{ tr.result.successTitle }}</h1>
        <p v-if="isRefunded" class="tag tag-refund" style="margin-bottom: var(--sp-3);">
          {{ tr.status.refunded }}
        </p>

        <dl class="disclosure-body" style="text-align: left; margin-bottom: var(--sp-5);">
          <dt>{{ tr.result.orderNo }}</dt>
          <dd>{{ result.orderNo }}</dd>
          <dt>{{ tr.result.amount }}</dt>
          <dd>{{ formatTwd(result.amount) }}</dd>
          <dt>{{ tr.result.project }}</dt>
          <dd>{{ result.projectName }}</dd>
          <template v-if="result.storeName">
            <dt>{{ tr.result.store }}</dt>
            <dd>{{ result.storeName }}</dd>
          </template>
          <dt>{{ tr.result.donor }}</dt>
          <dd>{{ result.donorNameMasked }}（{{ result.donorEmailMasked }}）</dd>
          <template v-if="voucherText">
            <dt>{{ tr.result.voucher }}</dt>
            <dd>{{ voucherText }}</dd>
          </template>
        </dl>

        <div class="stack">
          <button type="button" class="btn btn-secondary" @click="sharePage">{{ tr.result.share }}</button>
          <p v-if="shareFeedback" role="status" class="field-hint">✓ {{ tr.result.shareCopied }}</p>
          <NuxtLink :to="`/${lang}/`" class="btn btn-primary">{{ tr.result.backHome }}</NuxtLink>
        </div>
      </template>

      <template v-else-if="state === 'processing'">
        <div class="spinner" role="status" aria-live="polite">
          <span class="visually-hidden">{{ tr.result.pendingTitle }}</span>
        </div>
        <h1>{{ tr.result.pendingTitle }}</h1>
        <p class="text-secondary">{{ tr.result.pendingHint }}</p>
        <p class="text-tertiary">{{ tr.result.orderNo }}：{{ result.orderNo }}</p>

        <p v-if="confirmFailed" class="notice-row" role="status">{{ tr.result.confirmingFailedHint }}</p>

        <div v-if="timedOutHint || pollExpired" class="card" role="status" style="margin-top: var(--sp-5); text-align: left;">
          <p style="margin-bottom: var(--sp-2);">{{ tr.result.pendingTimeoutHint }}</p>
          <p class="text-secondary" style="word-break: break-all; margin-bottom: 0;">{{ currentUrl }}</p>
        </div>

        <div v-if="pollExpired || confirmFailed" class="stack" style="margin-top: var(--sp-4);">
          <button type="button" class="btn btn-secondary" @click="recheck">{{ tr.result.pendingRecheck }}</button>
        </div>
      </template>

      <template v-else>
        <div class="result-icon result-icon-warning" aria-hidden="true">⚠</div>
        <h1>{{ tr.result.incompleteTitle }}</h1>
        <p class="text-secondary">{{ tr.result.incompleteReason }}</p>
        <p class="text-tertiary">{{ tr.result.orderNo }}：{{ result.orderNo }}</p>

        <div class="stack" style="margin-top: var(--sp-5);">
          <p v-if="retryError" class="form-error-banner" role="alert">{{ retryError }}</p>
          <button
            v-if="result.canRetry"
            type="button"
            class="btn btn-primary"
            :disabled="retrying"
            :aria-busy="retrying"
            @click="retryPayment"
          >
            {{ retrying ? tr.result.retrying : tr.result.retryPayment }}
          </button>
          <NuxtLink :to="`/${lang}/`" class="btn btn-secondary">{{ tr.result.backHome }}</NuxtLink>
        </div>
      </template>
    </section>
  </div>
</template>

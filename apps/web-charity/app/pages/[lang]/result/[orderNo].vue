<script setup lang="ts">
// pages/[lang]/result/[orderNo].vue — 結果頁三態（docs/22-charity-ui.md §2.7，規劃書 §3.5）。
// 🔴 三態都要能實際被看到，這裡用兩種資料來源示範：
//   1. db/seed 種子裡真實存在的捐款單號（見下方「示範連結」），依它們原本的狀態決定顯示哪一態，
//      三態各有至少一筆種子資料可以直接點到（見本頁 README 或任務回報列出的網址）。
//   2. 剛從本站捐款表單送出、mockup 自產的訂單（不在種子裡），沒有真實後端可查，
//      用 ?demo= 這個 query 參數當「這筆訂單目前應該顯示哪一態」的示範值（預設 success）。
// 頁面一律 noindex（nuxt.config.ts 的 routeRules 已全站套用），Email 遮罩顯示（規劃書 §3.5）。
import { useLang } from '../../../composables/useLang'
import { useMockOrder } from '../../../composables/useCheckoutDraft'
import { formatTwd } from '../../../utils/currency'
import { maskEmail } from '../../../utils/validators'
import { pickText } from '../../../utils/i18n'

definePageMeta({ layout: 'default' })

const route = useRoute()
const orderNo = route.params.orderNo as string

const { lang, tr } = useLang()

const { data: found } = await useFetch(`/api/charity/donations/${orderNo}`)
const mockOrder = useMockOrder(orderNo)

type ResultState = 'success' | 'pending' | 'failure'

const demoQuery = computed(() => {
  const value = route.query.demo
  return typeof value === 'string' ? value : null
})

const state = computed<ResultState>(() => {
  if (found.value) {
    const status = found.value.donation.status
    if (status === 'paid' || status === 'refunded') return 'success'
    if (status === 'pending' || status === 'created') return 'pending'
    return 'failure' // failed／expired
  }
  if (demoQuery.value === 'failure' || demoQuery.value === 'pending') return demoQuery.value
  return 'success'
})

const amount = computed(() => found.value?.donation.amount ?? mockOrder.value?.amount ?? null)
const projectName = computed(() => {
  if (found.value?.project) {
    return pickText(lang.value, found.value.project.name_zh, found.value.project.name_en).text
  }
  if (mockOrder.value) {
    return pickText(lang.value, mockOrder.value.projectNameZh, mockOrder.value.projectNameEn).text
  }
  return null
})
const maskedEmail = computed(() => (found.value ? maskEmail(found.value.donation.donor_email) : null))
const isRefunded = computed(() => found.value?.donation.status === 'refunded')

const voucherText = computed(() => {
  const invoice = found.value?.invoice
  if (!invoice) return null
  if (invoice.void_status === 'voided') return tr.value.result.voucherVoided
  if (invoice.void_status === 'allowance') return tr.value.result.voucherAllowance
  if (invoice.issue_status === 'issued') return tr.value.result.voucherIssued
  if (invoice.issue_status === 'failed') return tr.value.result.voucherFailed
  return tr.value.result.voucherPending
})

// 「處理中」逾時後改變文案（規劃書 §3.4：不可無限轉圈；docs/22 §2.6 標明秒數是本輪建議值，
// 最終數字待串接 LINE Pay 時由 backend-engineer 與客戶確認，見 docs/22 §6 第 3 項）。
const timedOut = ref(false)
let timeoutTimer: ReturnType<typeof setTimeout> | undefined

onMounted(() => {
  if (state.value === 'pending') {
    timeoutTimer = setTimeout(() => { timedOut.value = true }, 8000)
  }
})
onBeforeUnmount(() => {
  if (timeoutTimer) clearTimeout(timeoutTimer)
})

const currentUrl = computed(() => (import.meta.client ? window.location.href : ''))

function retryPayment() {
  navigateTo(`/${lang.value}/pay/${orderNo}?demo=success`)
}

async function sharePage() {
  if (import.meta.client && navigator.share) {
    try {
      await navigator.share({ title: tr.value.result.successTitle, url: window.location.href })
      return
    } catch {
      // 使用者取消分享或裝置不支援，落到下面的複製連結 fallback。
    }
  }
  if (import.meta.client && navigator.clipboard) {
    try {
      await navigator.clipboard.writeText(window.location.href)
      shareFeedback.value = true
      setTimeout(() => { shareFeedback.value = false }, 3000)
    } catch {
      // 剪貼簿也不可用時，不做任何事——連結本身已顯示在網址列，使用者仍可手動複製。
    }
  }
}

const shareFeedback = ref(false)

useHead({
  title: `${tr.value.result[state.value === 'success' ? 'successTitle' : state.value === 'pending' ? 'pendingTitle' : 'failedTitle']} | ${tr.value.associationName}`,
})
</script>

<template>
  <div class="container">
    <section class="section text-center" style="max-width: 480px; margin: 0 auto;">
      <template v-if="state === 'success'">
        <div class="result-icon result-icon-success" aria-hidden="true">✓</div>
        <h1>{{ tr.result.successTitle }}</h1>
        <p v-if="isRefunded" class="tag tag-refund" style="margin-bottom: var(--sp-3);">
          {{ tr.status.refunded }}
        </p>

        <dl class="disclosure-body" style="text-align: left; margin-bottom: var(--sp-5);">
          <dt>{{ tr.result.orderNo }}</dt>
          <dd>{{ orderNo }}</dd>
          <template v-if="amount !== null">
            <dt>{{ tr.result.amount }}</dt>
            <dd>{{ formatTwd(amount) }}</dd>
          </template>
          <template v-if="projectName">
            <dt>{{ tr.result.project }}</dt>
            <dd>{{ projectName }}</dd>
          </template>
          <template v-if="maskedEmail">
            <dt>Email</dt>
            <dd>{{ maskedEmail }}</dd>
          </template>
          <template v-if="voucherText">
            <dt>{{ tr.result.voucher }}</dt>
            <dd>{{ voucherText }}</dd>
          </template>
        </dl>

        <div class="stack">
          <button type="button" class="btn btn-secondary" @click="sharePage">{{ tr.result.share }}</button>
          <p v-if="shareFeedback" role="status" class="field-hint">✓</p>
          <NuxtLink :to="`/${lang}/`" class="btn btn-primary">{{ tr.result.backHome }}</NuxtLink>
        </div>
      </template>

      <template v-else-if="state === 'pending'">
        <div class="spinner" role="status" aria-live="polite">
          <span class="visually-hidden">{{ tr.result.pendingTitle }}</span>
        </div>
        <h1>{{ tr.result.pendingTitle }}</h1>
        <p class="text-secondary">{{ tr.result.pendingHint }}</p>

        <div v-if="timedOut" class="card" role="status" style="margin-top: var(--sp-5); text-align: left;">
          <p style="margin-bottom: var(--sp-2);">{{ tr.result.pendingTimeoutHint }}</p>
          <p class="text-secondary" style="word-break: break-all; margin-bottom: 0;">{{ currentUrl }}</p>
        </div>
      </template>

      <template v-else>
        <div class="result-icon result-icon-warning" aria-hidden="true">⚠</div>
        <h1>{{ tr.result.failedTitle }}</h1>
        <p class="text-secondary">{{ tr.result.failedReason }}</p>
        <p class="text-tertiary">{{ tr.result.orderNo }}：{{ orderNo }}</p>

        <div class="stack" style="margin-top: var(--sp-5);">
          <button type="button" class="btn btn-primary" @click="retryPayment">{{ tr.result.retryPayment }}</button>
          <!-- 「仍有問題請聯絡」的實際聯絡方式已在頁尾統一呈現（footer.contactValue），這裡不重複同一句話，
               避免同一頁出現兩次一模一樣的文字（曾經這樣寫過，390 寬度截圖比對時發現重複，已修正）。 -->
        </div>
      </template>
    </section>
  </div>
</template>

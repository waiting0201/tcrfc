<script setup lang="ts">
// app/components/member/MemberPlanPicker.vue — 會籍方案清單＋送出申請（購買會籍／到期續約改選），主站 §3.14（v3.29）
//
// 註冊帳號不是會籍；成為會員的方式只有「購買該俱樂部的會籍方案」。**不提供會籍期間內升級**——本元件只用在兩種情境：
//   - kind = 'buy'：該俱樂部目前球季還沒有會籍（含僅註冊帳號）→ 購買方案；
//   - kind = 'renew'：會籍即將到期或已到期 → 續約，可改選方案（磐石會員 ↔ 尊尚會員）。
//
// 🔴 網頁端**只送出 `created` 申請**（`POST {club}/member/membership-orders`），**不呼叫 `pay`／`confirm`、不放線上付款按鈕**
// （主站 §3.14「會籍的網頁站內結帳仍不做」，代理白名單也沒有放行，server/utils/member-proxy.ts）；
// 由工作人員聯繫完成收款與開通。金額與層級由伺服器依方案計算，前端不送金額、不自己算折扣。
import { formatPlainDate, toMemberApiError } from '#shared/utils/member'
import type { MembershipOrder, MembershipPlan } from '#shared/utils/member'

const props = defineProps<{
  clubCode: string
  /** `buy`＝購買會籍；`renew`＝到期續約（可改選方案）。 */
  kind: 'buy' | 'renew'
}>()
const emit = defineEmits<{ submitted: [order: MembershipOrder] }>()

const { locale, isEn, tx } = useLocale()
const { authedFetch } = useMemberSession()

const open = ref(false)
const plans = ref<MembershipPlan[] | null>(null)
const loading = ref(false)
const loadError = ref('')

async function toggle() {
  open.value = !open.value
  if (!open.value || plans.value) return
  loading.value = true
  loadError.value = ''
  try {
    plans.value = await $fetch<MembershipPlan[]>(`/api/backend/${props.clubCode}/membership/plans`, { query: { lang: locale.value } })
  }
  catch (err) { loadError.value = toMemberApiError(err, undefined, isEn.value).detail }
  finally { loading.value = false }
}

/** 冪等鍵：同一方案在送出成功前重複點擊（或網路逾時重試）一律用同一把，後端只會成立一張申請。 */
function idempotencyKey(planCode: string): string {
  const k = `tcrfc.member.idem.${props.clubCode}.${planCode}`
  try {
    const existing = sessionStorage.getItem(k)
    if (existing) return existing
    const fresh = crypto.randomUUID()
    sessionStorage.setItem(k, fresh)
    return fresh
  }
  catch { return crypto.randomUUID() }
}
function clearKey(planCode: string) {
  try { sessionStorage.removeItem(`tcrfc.member.idem.${props.clubCode}.${planCode}`) }
  catch { /* 忽略 */ }
}

const busy = ref('')
const errors = ref<Record<string, string>>({})

async function submit(plan: MembershipPlan) {
  const name = plan.name
  const confirmText = isEn.value
    ? `Submit your ${props.kind === 'renew' ? 'renewal' : 'membership'} request for "${name}"?\nAfter you submit, our staff will contact you to complete payment and activation. This website will not ask you to pay online.`
    : `確定送出「${name}」的${props.kind === 'renew' ? '續約' : '入會'}申請嗎？\n送出後由工作人員與您聯繫完成收款與開通，網頁不會要求您線上付款。`
  if (!window.confirm(confirmText)) return
  busy.value = plan.code
  errors.value = { ...errors.value, [plan.code]: '' }
  try {
    const order = await authedFetch<MembershipOrder>(`/api/backend/${props.clubCode}/member/membership-orders`, {
      method: 'POST',
      headers: { 'Idempotency-Key': idempotencyKey(plan.code) },
      body: { planCode: plan.code },
    })
    clearKey(plan.code)
    open.value = false
    emit('submitted', order)
  }
  catch (err) {
    const e = toMemberApiError(err, undefined, isEn.value)
    // 這幾種是「這個申請已經不能（或不必）再送」，鍵可以丟掉；其餘（逾時／5xx）保留同一把鍵讓使用者重試
    if (e.status >= 400 && e.status < 500) clearKey(plan.code)
    errors.value = { ...errors.value, [plan.code]: e.detail }
  }
  finally { busy.value = '' }
}

const feeText = (n: number) => `NT$ ${n.toLocaleString(isEn.value ? 'en-US' : 'zh-TW')}`
const collectingNote = computed(() => (props.clubCode === 'bw'
  ? (isEn.value
      ? `Payments for ${BW_NAME_EN} memberships are collected by ${CLUB_NAME_EN}. The payee and the invoice name are ${CLUB_NAME_EN}; the membership you are applying for is with ${BW_NAME_EN}.`
      : '藍鯨會籍的款項由台中磐石足球俱樂部代收，收款方與發票抬頭為台中磐石足球俱樂部；您申請的是台中藍鯨的會籍。')
  : ''))
</script>

<template>
  <div class="mc-upgrade">
    <button type="button" class="btn btn--primary btn--sm" :aria-expanded="open" @click="toggle">
      {{ kind === 'renew' ? tx('申請續約', 'Apply to renew') : tx('查看方案並申請入會', 'View plans and apply') }}
    </button>
    <div v-if="open" class="mc-plans">
      <p v-if="loading" class="mc-empty">{{ tx('載入方案中…', 'Loading plans…') }}</p>
      <p v-else-if="loadError" class="mc-alert mc-alert--error" role="alert">{{ loadError }}</p>
      <p v-else-if="(plans ?? []).length === 0" class="mc-empty">{{ tx('目前沒有開放申請的會籍方案。方案公布後即可在此申請。', 'There are no membership plans open for applications right now. You can apply here once plans are announced.') }}</p>
      <template v-else>
        <p class="mc-note">
          <template v-if="isEn"><strong>How to pay:</strong> Membership fees are collected through a LINE Pay payment link or in person. This website <strong>does not</strong> ask you to pay online. After you submit your request, our staff will check the payment and activate your membership.</template>
          <template v-else><strong>付款方式：</strong>會費以 LINE Pay 收款連結或現場收款完成。網頁<strong>不會</strong>要求線上付款——送出申請後，由工作人員核對款項並開通會籍。</template>
        </p>
        <p v-if="kind === 'renew'" class="mc-note">{{ tx('續約時可改選方案。', 'You can choose a different plan when you renew.') }}</p>
        <p v-if="collectingNote" class="mc-note">{{ collectingNote }}</p>
        <ul class="mc-planlist">
          <li v-for="p in plans" :key="p.code" class="mc-plan">
            <div>
              <p class="mc-plan__name">{{ p.name }}</p>
              <p class="mc-plan__fee">{{ feeText(p.fee) }}{{ isEn ? ' / ' : '／' }}{{ p.seasonCode }}{{ tx(' 球季', ' season') }}</p>
              <p class="mc-note mc-note--small">{{ isEn ? `Includes ${p.cardQuota} membership ${p.cardQuota === 1 ? 'card' : 'cards'} and ${p.jerseyQuota} ${p.jerseyQuota === 1 ? 'jersey' : 'jerseys'}` : `含會員卡 ${p.cardQuota} 張、球衣 ${p.jerseyQuota} 件` }}<template v-if="p.shopDiscountPercent > 0">{{ isEn ? `; ${p.shopDiscountPercent}% shop member discount` : `；商店會員折扣 ${p.shopDiscountPercent}%` }}</template><template v-if="p.startsOn || p.endsOn">{{ isEn ? '; period ' : '；期間 ' }}{{ formatPlainDate(p.startsOn) }} – {{ formatPlainDate(p.endsOn) }}</template></p>
              <p v-if="p.benefitNote" class="mc-note mc-note--small">{{ p.benefitNote }}</p>
              <p v-if="p.midSeasonRule" class="mc-note mc-note--small">{{ tx('季中入會：', 'Joining mid-season: ') }}{{ p.midSeasonRule }}</p>
            </div>
            <button type="button" class="btn btn--dark btn--sm" :disabled="busy === p.code" @click="submit(p)">
              {{ busy === p.code ? tx('送出中…', 'Submitting…') : tx('送出申請', 'Submit request') }}
            </button>
            <p v-if="errors[p.code]" class="mc-alert mc-alert--error" role="alert">{{ errors[p.code] }}</p>
          </li>
        </ul>
      </template>
    </div>
  </div>
</template>

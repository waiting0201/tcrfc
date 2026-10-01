<script setup lang="ts">
// app/components/member/MemberMemberships.vue — 我的會籍＋升級續會（主站 §3.14）
//
// 會籍是「一人每俱樂部一份」，這裡逐俱樂部列出；沒有現行會籍的俱樂部放在「加入」入口（不隱藏）。
// 藍鯨沒有開放中的球季時，後端回 409 `season_not_available`，**如實顯示後端的說明**，不假裝可以加入。
//
// 🔴 升級／續會：主站 §3.14 明文「網頁會籍站內結帳尚未拍板，未拍板前不得逕行實作」。
// 所以網頁端**只送出 `created` 升級申請**（`POST {club}/member/membership-orders`），
// **不呼叫 `pay`／`confirm`、不放線上付款按鈕**（代理白名單也沒有放行這兩條，server/utils/member-proxy.ts）；
// 畫面說明由工作人員聯繫完成收款與開通。金額由伺服器依方案計算，前端不送金額。
import { formatPlainDate, formatTaipeiDateTime, toMemberApiError } from '#shared/utils/member'
import type { MembershipOrder, MembershipPlan, MyMembership, MyMemberships } from '#shared/utils/member'

defineProps<{ data: MyMemberships | null }>()
const emit = defineEmits<{ changed: [] }>()

const { locale, lp } = useLocale()
const { authedFetch } = useMemberSession()

const safeUrl = (u: string | null | undefined) => (u && /^(https:\/\/|\/)/.test(u) ? u : null)

// ── 加入俱樂部 ──
const joinBusy = ref('')
const joinMessage = ref<Record<string, string>>({})
async function join(code: string) {
  joinBusy.value = code
  joinMessage.value = { ...joinMessage.value, [code]: '' }
  try {
    await authedFetch(`/api/backend/${code}/member/memberships/join`, { method: 'POST', body: {} })
    emit('changed')
  }
  catch (err) {
    const e = toMemberApiError(err)
    joinMessage.value = {
      ...joinMessage.value,
      [code]: e.code === 'season_not_available'
        ? `${e.detail} 球季開放後即可在此加入。`
        : e.detail,
    }
  }
  finally { joinBusy.value = '' }
}

// ── 升級／續會（只送出申請）──
const openClub = ref('')
const plans = ref<Record<string, MembershipPlan[]>>({})
const plansLoading = ref('')
const plansError = ref<Record<string, string>>({})

async function togglePlans(code: string) {
  if (openClub.value === code) { openClub.value = ''; return }
  openClub.value = code
  if (plans.value[code]) return
  plansLoading.value = code
  try {
    const list = await $fetch<MembershipPlan[]>(`/api/backend/${code}/membership/plans`, { query: { lang: locale.value } })
    plans.value = { ...plans.value, [code]: list }
  }
  catch (err) { plansError.value = { ...plansError.value, [code]: toMemberApiError(err).detail } }
  finally { plansLoading.value = '' }
}

/** 送出成功的提示放在元件最上層：成功後該會籍會出現「待確認」狀態、升級區塊隨之收起，放在區塊內的訊息會跟著消失。 */
const flash = ref('')
const submitBusy = ref('')
const submitResult = ref<Record<string, { ok: boolean, text: string }>>({})

/** 冪等鍵：同一方案在送出成功前重複點擊（或網路逾時重試）一律用同一把，後端只會成立一張申請。 */
function idempotencyKey(club: string, planCode: string): string {
  const k = `tcrfc.member.idem.${club}.${planCode}`
  try {
    const existing = sessionStorage.getItem(k)
    if (existing) return existing
    const fresh = crypto.randomUUID()
    sessionStorage.setItem(k, fresh)
    return fresh
  }
  catch { return crypto.randomUUID() }
}
function clearKey(club: string, planCode: string) {
  try { sessionStorage.removeItem(`tcrfc.member.idem.${club}.${planCode}`) }
  catch { /* 忽略 */ }
}

async function submitApplication(m: MyMembership, plan: MembershipPlan) {
  if (!window.confirm(`確定送出「${plan.name}」的${m.tier === 'registered' ? '升級' : '續會'}申請嗎？\n送出後由工作人員與您聯繫完成收款與開通，網頁不會要求您線上付款。`)) return
  const key = `${m.club.code}:${plan.code}`
  submitBusy.value = key
  submitResult.value = { ...submitResult.value, [key]: { ok: false, text: '' } }
  try {
    const order = await authedFetch<MembershipOrder>(`/api/backend/${m.club.code}/member/membership-orders`, {
      method: 'POST',
      headers: { 'Idempotency-Key': idempotencyKey(m.club.code, plan.code) },
      body: { planCode: plan.code },
    })
    clearKey(m.club.code, plan.code)
    flash.value = `申請已送出（單號 ${order.orderNo}，${order.statusLabel}）。工作人員將與您聯繫完成收款與開通，網頁不會要求您線上付款。`
    openClub.value = ''
    emit('changed')
    await loadOrders()
  }
  catch (err) {
    const e = toMemberApiError(err)
    // 這幾種是「這個申請已經不能（或不必）再送」，鍵可以丟掉；其餘（逾時／5xx）保留同一把鍵讓使用者重試
    if (e.status >= 400 && e.status < 500) clearKey(m.club.code, plan.code)
    submitResult.value = { ...submitResult.value, [key]: { ok: false, text: e.detail } }
  }
  finally { submitBusy.value = '' }
}

// ── 申請紀錄 ──
const orders = ref<MembershipOrder[]>([])
const ordersLoaded = ref(false)
async function loadOrders() {
  try {
    orders.value = await authedFetch<MembershipOrder[]>('/api/backend/member/membership-orders', { query: { lang: locale.value } })
  }
  catch { orders.value = [] }
  finally { ordersLoaded.value = true }
}
onMounted(loadOrders)

const cancelBusy = ref('')
const orderError = ref('')
async function cancelOrder(o: MembershipOrder) {
  if (!window.confirm('確定取消這筆升級申請嗎？')) return
  cancelBusy.value = o.orderNo
  orderError.value = ''
  try {
    await authedFetch(`/api/backend/member/membership-orders/${o.orderNo}/cancel`, { method: 'POST', body: {} })
    await loadOrders()
    emit('changed')
  }
  catch (err) { orderError.value = toMemberApiError(err).detail }
  finally { cancelBusy.value = '' }
}

function canApply(m: MyMembership) {
  return !m.pendingOrder && (m.tier === 'registered' || m.renewalDue || m.status === 'expired')
}
const feeText = (n: number) => `NT$ ${n.toLocaleString('zh-TW')}`
const collectingNote = (m: MyMembership) => (m.club.code === 'bw'
  ? '藍鯨會籍的款項由台中磐石足球俱樂部代收，收款方與發票抬頭為台中磐石足球俱樂部；您申請的是台中藍鯨的會籍。'
  : '')
</script>

<template>
  <div class="mc-memberships">
    <p v-if="!data" class="mc-empty">會籍資料載入中…</p>
    <template v-else>
      <p v-if="flash" class="mc-alert mc-alert--ok" role="status">{{ flash }}</p>
      <p v-if="data.memberships.length === 0" class="mc-empty">您目前還沒有任何會籍。{{ data.joinableClubs.length ? '可從下方加入俱樂部。' : '' }}</p>

      <article v-for="m in data.memberships" :key="m.id" class="mc-membership">
        <header class="mc-membership__head">
          <img v-if="safeUrl(m.club.logoLightUrl)" :src="safeUrl(m.club.logoLightUrl)!" :alt="m.club.name" height="36" class="mc-membership__logo">
          <h3>{{ m.club.name }}</h3>
          <span class="mc-badge" :class="m.status === 'active' ? 'mc-badge--ok' : 'mc-badge--off'">{{ m.statusLabel }}</span>
        </header>
        <dl class="mc-dl">
          <div><dt>球季</dt><dd>{{ m.seasonCode }}</dd></div>
          <div><dt>層級</dt><dd>{{ m.tierLabel }}</dd></div>
          <div><dt>期間</dt><dd>{{ formatPlainDate(m.startOn) || '—' }} – {{ formatPlainDate(m.endOn) || '—' }}</dd></div>
          <div v-if="m.planName"><dt>方案</dt><dd>{{ m.planName }}</dd></div>
          <div><dt>會員卡</dt><dd>{{ m.cards.length }} 張<template v-if="m.cardQuota">（方案含 {{ m.cardQuota }} 張）</template></dd></div>
          <div v-if="m.jerseyQuota"><dt>入會球衣</dt><dd>{{ m.jerseyQuota }} 件</dd></div>
        </dl>

        <p v-if="m.renewalDue" class="mc-alert mc-alert--info" role="status">
          會籍將於 {{ formatPlainDate(m.endOn) }} 到期，可於下方申請續會。
        </p>
        <p v-if="m.pendingOrder" class="mc-alert mc-alert--info" role="status">
          升級／續會申請<strong>{{ m.pendingOrder.statusLabel }}</strong>（單號 {{ m.pendingOrder.orderNo }}）。工作人員將與您聯繫完成收款與開通，開通後此處會顯示「已開通」。
        </p>

        <div v-if="canApply(m)" class="mc-upgrade">
          <button type="button" class="btn btn--primary btn--sm" :aria-expanded="openClub === m.club.code" @click="togglePlans(m.club.code)">
            {{ m.tier === 'registered' ? '升級付費球迷會員' : '申請續會' }}
          </button>
          <div v-if="openClub === m.club.code" class="mc-plans">
            <p v-if="plansLoading === m.club.code" class="mc-empty">載入方案中…</p>
            <p v-else-if="plansError[m.club.code]" class="mc-alert mc-alert--error" role="alert">{{ plansError[m.club.code] }}</p>
            <p v-else-if="(plans[m.club.code] ?? []).length === 0" class="mc-empty">目前沒有開放申請的會籍方案。方案公布後即可在此申請。</p>
            <template v-else>
              <p class="mc-note">
                <strong>付款方式：</strong>會費以 LINE Pay 收款連結或現場收款完成。網頁<strong>不會</strong>要求線上付款——送出申請後，由工作人員核對款項並開通會籍。
              </p>
              <p v-if="collectingNote(m)" class="mc-note">{{ collectingNote(m) }}</p>
              <ul class="mc-planlist">
                <li v-for="p in plans[m.club.code]" :key="p.code" class="mc-plan">
                  <div>
                    <p class="mc-plan__name">{{ p.name }}</p>
                    <p class="mc-plan__fee">{{ feeText(p.fee) }}／{{ p.seasonCode }} 球季</p>
                    <p class="mc-note mc-note--small">含會員卡 {{ p.cardQuota }} 張、球衣 {{ p.jerseyQuota }} 件<template v-if="p.startsOn || p.endsOn">；期間 {{ formatPlainDate(p.startsOn) }} – {{ formatPlainDate(p.endsOn) }}</template></p>
                    <p v-if="p.benefitNote" class="mc-note mc-note--small">{{ p.benefitNote }}</p>
                    <p v-if="p.midSeasonRule" class="mc-note mc-note--small">季中入會：{{ p.midSeasonRule }}</p>
                  </div>
                  <button type="button" class="btn btn--dark btn--sm" :disabled="submitBusy === `${m.club.code}:${p.code}`" @click="submitApplication(m, p)">
                    {{ submitBusy === `${m.club.code}:${p.code}` ? '送出中…' : '送出申請' }}
                  </button>
                  <p v-if="submitResult[`${m.club.code}:${p.code}`]?.text" class="mc-alert" :class="submitResult[`${m.club.code}:${p.code}`]!.ok ? 'mc-alert--ok' : 'mc-alert--error'" :role="submitResult[`${m.club.code}:${p.code}`]!.ok ? 'status' : 'alert'">{{ submitResult[`${m.club.code}:${p.code}`]!.text }}</p>
                </li>
              </ul>
            </template>
          </div>
        </div>
      </article>

      <section v-if="data.joinableClubs.length" class="mc-join" aria-label="加入俱樂部">
        <h3>加入俱樂部</h3>
        <p class="mc-note">同一個帳號可以同時擁有台中磐石與台中藍鯨的會籍，兩邊各自計算球季與到期日，各有一張會員卡。</p>
        <ul class="mc-joinlist">
          <li v-for="c in data.joinableClubs" :key="c.code">
            <span>{{ c.name }}</span>
            <button type="button" class="btn btn--dark btn--sm" :disabled="joinBusy === c.code" @click="join(c.code)">{{ joinBusy === c.code ? '處理中…' : `加入${c.name}（免費一般會員）` }}</button>
            <p v-if="joinMessage[c.code]" class="mc-alert mc-alert--info" role="status">{{ joinMessage[c.code] }}</p>
          </li>
        </ul>
      </section>

      <section v-if="orders.length" class="mc-orders" aria-label="升級申請紀錄">
        <h3>升級申請紀錄</h3>
        <div class="table-scroll">
          <table class="mc-table">
            <thead><tr><th scope="col">單號</th><th scope="col">方案</th><th scope="col">金額</th><th scope="col">狀態</th><th scope="col">申請時間</th><th scope="col"><span class="visually-hidden">操作</span></th></tr></thead>
            <tbody>
              <tr v-for="o in orders" :key="o.orderNo">
                <td class="mc-mono">{{ o.orderNo }}</td>
                <td>{{ o.planName ?? o.planCode }}（{{ o.seasonCode }}）</td>
                <td>{{ feeText(o.amount) }}</td>
                <td>{{ o.statusLabel }}</td>
                <td>{{ formatTaipeiDateTime(o.createdAt, locale) }}</td>
                <td><button v-if="o.canCancel" type="button" class="mc-link" :disabled="cancelBusy === o.orderNo" @click="cancelOrder(o)">取消申請</button></td>
              </tr>
            </tbody>
          </table>
        </div>
        <p v-if="orderError" class="mc-alert mc-alert--error" role="alert">{{ orderError }}</p>
      </section>

      <p class="mc-note">想比較權益？<a :href="lp('/zh/culture/fan-club/')">看球迷會方案與權益對照</a>。</p>
    </template>
  </div>
</template>

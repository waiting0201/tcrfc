<script setup lang="ts">
// app/components/member/MemberMemberships.vue — 我的會籍＋購買會籍／到期續約（主站 §3.14，v3.29）
//
// 會籍是「一人每俱樂部一份」，這裡逐俱樂部列出。**註冊帳號不是會籍**（v3.29）：沒有會籍時不顯示會員卡、不顯示層級，
// 只顯示「購買會籍」入口（導向方案清單；沒有會籍的俱樂部放在這裡、不隱藏）。成為會員的方式只有一種：購買該俱樂部的會籍方案。
// 三種狀態：未持有會籍（僅註冊帳號）／持有基礎方案（磐石會員）／持有高階方案（尊尚會員）；方案名稱、層級標籤一律取 API，
// 藍鯨的方案由藍鯨自訂，不套用磐石的兩級命名。
// 🔴 **不提供會籍期間內升級**（使用者 2026-10-16 拍板）：會籍有效期間不顯示任何升級入口，**只有到期（或到期前 30 天）續約時可改選方案**。
// 🔴 網頁端只送出 `created` 申請（`POST {club}/member/membership-orders`），不收款、不放線上付款按鈕（見 MemberPlanPicker）。
import { formatPlainDate, formatTaipeiDateTime, toMemberApiError } from '#shared/utils/member'
import type { MembershipOrder, MyMembership, MyMemberships } from '#shared/utils/member'

const props = defineProps<{ data: MyMemberships | null }>()
const emit = defineEmits<{ changed: [] }>()

const { locale, lp, isEn, tx } = useLocale()
const { authedFetch } = useMemberSession()

/** 送出成功的提示放在元件最上層：成功後該俱樂部會出現「待確認」狀態、方案清單隨之收起，放在區塊內的訊息會跟著消失。 */
const flash = ref('')
async function onSubmitted(order: MembershipOrder) {
  flash.value = tx(`申請已送出（單號 ${order.orderNo}，${order.statusLabel}）。工作人員將與您聯繫完成收款與開通，網頁不會要求您線上付款。`, `Your request has been submitted (reference ${order.orderNo}, ${order.statusLabel}). Our staff will contact you to complete payment and activation. This website will not ask you to pay online.`)
  emit('changed')
  await loadOrders()
}

// ── 申請紀錄 ──
const orders = ref<MembershipOrder[]>([])
async function loadOrders() {
  try {
    orders.value = await authedFetch<MembershipOrder[]>('/api/backend/member/membership-orders', { query: { lang: locale.value } })
  }
  catch { orders.value = [] }
}
onMounted(loadOrders)

const cancelBusy = ref('')
const orderError = ref('')
async function cancelOrder(o: MembershipOrder) {
  if (!window.confirm(tx('確定取消這筆會籍申請嗎？', 'Cancel this membership request?'))) return
  cancelBusy.value = o.orderNo
  orderError.value = ''
  try {
    await authedFetch(`/api/backend/member/membership-orders/${o.orderNo}/cancel`, { method: 'POST', body: {} })
    await loadOrders()
    emit('changed')
  }
  catch (err) { orderError.value = toMemberApiError(err, undefined, isEn.value).detail }
  finally { cancelBusy.value = '' }
}

/** 到期前 30 天內或已到期、且沒有待處理申請：可續約並改選方案。有效期間內不提供升級。 */
function canRenew(m: MyMembership) {
  return !m.pendingOrder && (m.renewalDue || m.status === 'expired')
}
/** 該俱樂部是否已有尚未開通的申請（待確認／待付款）：有就不再顯示購買入口，避免重複送出。 */
const openOrders = computed(() => props.data?.openOrders ?? [])
const hasOpenOrder = (clubCode: string) => openOrders.value.some(o => o.clubCode === clubCode)
const feeText = (n: number) => `NT$ ${n.toLocaleString(isEn.value ? 'en-US' : 'zh-TW')}`
const bwNameEn = BW_NAME_EN // B-5：藍鯨英文簡稱（2026-10-05 定案）
</script>

<template>
  <div class="mc-memberships">
    <p v-if="!data" class="mc-empty">{{ tx('會籍資料載入中…', 'Loading your memberships…') }}</p>
    <template v-else>
      <p v-if="flash" class="mc-alert mc-alert--ok" role="status">{{ flash }}</p>

      <!-- 待確認的申請（購買或續約後、開通前）：註冊帳號沒有會籍列，狀態放這裡 -->
      <p v-for="o in openOrders" :key="o.orderNo" class="mc-alert mc-alert--info" role="status">
        <template v-if="isEn">Your membership request for <strong>{{ o.planName ?? o.planCode }}</strong> ({{ o.seasonCode }}) is <strong>{{ o.statusLabel }}</strong> (reference {{ o.orderNo }}). Our staff will contact you to complete payment and activation, and this page will show your membership once it is activated.</template>
        <template v-else>您申請的「<strong>{{ o.planName ?? o.planCode }}</strong>」（{{ o.seasonCode }}）<strong>{{ o.statusLabel }}</strong>（單號 {{ o.orderNo }}）。工作人員將與您聯繫完成收款與開通，開通後此處會顯示您的會籍。</template>
      </p>

      <!-- 狀態一：還沒有任何會籍（僅註冊帳號）——沒有會員卡、不享會籍權益 -->
      <div v-if="data.memberships.length === 0" class="mc-empty">
        <p>{{ tx('您目前還沒有會籍。', 'You do not hold a membership yet.') }}</p>
        <p class="mc-note">{{ tx('註冊帳號可以登入、報名課程與活動、在商店購物，並查看會籍方案；成為會員（持有電子會員卡與會籍權益）需購買會籍方案，付款後由工作人員開通。', 'Your registered account lets you sign in, register for programs and events, shop in the store and browse membership plans. To become a member, with a digital membership card and membership benefits, purchase a membership plan; our staff will activate it once payment is received.') }}</p>
      </div>

      <article v-for="m in data.memberships" :key="m.id" class="mc-membership">
        <header class="mc-membership__head">
          <img :src="getClubAssets(m.club.code).headerMark.src" :alt="m.club.name" height="36" class="mc-membership__logo">
          <h3>{{ m.club.name }}</h3>
          <span class="mc-badge" :class="m.status === 'active' ? 'mc-badge--ok' : 'mc-badge--off'">{{ m.statusLabel }}</span>
        </header>
        <dl class="mc-dl">
          <div><dt>{{ tx('球季', 'Season') }}</dt><dd>{{ m.seasonCode }}</dd></div>
          <div><dt>{{ tx('方案', 'Plan') }}</dt><dd>{{ m.planName ?? m.tierLabel }}</dd></div>
          <div><dt>{{ tx('期間', 'Period') }}</dt><dd>{{ formatPlainDate(m.startOn) || '—' }} – {{ formatPlainDate(m.endOn) || '—' }}</dd></div>
          <div><dt>{{ tx('會員卡', 'Membership cards') }}</dt><dd>{{ isEn ? m.cards.length : `${m.cards.length} 張` }}<template v-if="m.cardQuota">{{ isEn ? ` (${m.cardQuota} included in the plan)` : `（方案含 ${m.cardQuota} 張）` }}</template></dd></div>
          <div v-if="m.jerseyQuota"><dt>{{ tx('入會球衣', 'Welcome jersey') }}</dt><dd>{{ isEn ? m.jerseyQuota : `${m.jerseyQuota} 件` }}</dd></div>
        </dl>

        <p v-if="m.renewalDue" class="mc-alert mc-alert--info" role="status">
          {{ tx(`會籍將於 ${formatPlainDate(m.endOn)} 到期，可於下方申請續約，續約時可改選方案。`, `Your membership expires on ${formatPlainDate(m.endOn)}. You can apply to renew below, and choose a different plan when you renew.`) }}
        </p>
        <!-- 沒有 openOrders 欄位的舊版後端：退回會籍自帶的待處理申請 -->
        <p v-if="m.pendingOrder && !data.openOrders" class="mc-alert mc-alert--info" role="status">
          <template v-if="isEn">Your membership request is <strong>{{ m.pendingOrder.statusLabel }}</strong> (reference {{ m.pendingOrder.orderNo }}). Our staff will contact you to complete payment and activation.</template>
          <template v-else>會籍申請<strong>{{ m.pendingOrder.statusLabel }}</strong>（單號 {{ m.pendingOrder.orderNo }}）。工作人員將與您聯繫完成收款與開通。</template>
        </p>
        <!-- 有效期間內不提供升級：只說明規則，不放入口 -->
        <p v-else-if="m.status === 'active' && !m.renewalDue" class="mc-note">
          {{ tx('會籍有效期間內不提供升級方案；到期後續約時可改選方案。', 'Plans cannot be upgraded while your membership is valid. You can choose a different plan when you renew after it expires.') }}
        </p>

        <MemberPlanPicker v-if="canRenew(m) && !hasOpenOrder(m.club.code)" :club-code="m.club.code" kind="renew" @submitted="onSubmitted" />
      </article>

      <!-- 目前球季還沒有會籍的俱樂部：購買會籍入口（不隱藏） -->
      <section v-if="data.joinableClubs.length" class="mc-join" :aria-label="tx('購買會籍', 'Get a membership')">
        <h3>{{ tx('購買會籍', 'Get a membership') }}</h3>
        <p class="mc-note">{{ tx('同一個帳號可以同時擁有台中磐石與台中藍鯨的會籍，兩邊各自計算球季與到期日，各有一張會員卡。', 'One account can hold memberships with both Taichung Rock FC and ' + bwNameEn + '. Each club has its own season and expiry date, and its own membership card.') }}</p>
        <ul class="mc-joinlist">
          <li v-for="c in data.joinableClubs" :key="c.code">
            <span>{{ c.name }}</span>
            <p v-if="hasOpenOrder(c.code)" class="mc-note">{{ tx('已有申請待處理，開通後此處會顯示您的會籍。', 'You already have a request in progress. Your membership will appear here once it is activated.') }}</p>
            <MemberPlanPicker v-else :club-code="c.code" kind="buy" @submitted="onSubmitted" />
          </li>
        </ul>
      </section>

      <section v-if="orders.length" class="mc-orders" :aria-label="tx('會籍申請紀錄', 'Membership requests')">
        <h3>{{ tx('會籍申請紀錄', 'Membership requests') }}</h3>
        <div class="table-scroll">
          <table class="mc-table">
            <thead><tr><th scope="col">{{ tx('單號', 'Reference') }}</th><th scope="col">{{ tx('方案', 'Plan') }}</th><th scope="col">{{ tx('金額', 'Amount') }}</th><th scope="col">{{ tx('狀態', 'Status') }}</th><th scope="col">{{ tx('申請時間', 'Requested') }}</th><th scope="col"><span class="visually-hidden">{{ tx('操作', 'Actions') }}</span></th></tr></thead>
            <tbody>
              <tr v-for="o in orders" :key="o.orderNo">
                <td class="mc-mono">{{ o.orderNo }}</td>
                <td>{{ o.planName ?? o.planCode }}（{{ o.seasonCode }}）</td>
                <td>{{ feeText(o.amount) }}</td>
                <td>{{ o.statusLabel }}</td>
                <td>{{ formatTaipeiDateTime(o.createdAt, locale) }}</td>
                <td><button v-if="o.canCancel" type="button" class="mc-link" :disabled="cancelBusy === o.orderNo" @click="cancelOrder(o)">{{ tx('取消申請', 'Cancel request') }}</button></td>
              </tr>
            </tbody>
          </table>
        </div>
        <p v-if="orderError" class="mc-alert mc-alert--error" role="alert">{{ orderError }}</p>
      </section>

      <p class="mc-note">{{ tx('想比較權益？', 'Want to compare benefits? ') }}<a :href="lp('/zh/culture/fan-club/')">{{ tx('看會籍方案與權益對照', 'See the membership plans and the benefits comparison') }}</a>{{ tx('。', '.') }}</p>
    </template>
  </div>
</template>

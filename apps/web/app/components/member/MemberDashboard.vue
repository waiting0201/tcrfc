<script setup lang="ts">
// app/components/member/MemberDashboard.vue — 登入後的會員中心（主站 §3.14「前台功能」）
//
// 五個分頁：我的會籍（含升級續會與權益對照）／電子會員卡／我的訂單（站內商店，S3-5）／球衣登記／個人資料與安全。
// 特約店家清單是公開頁（8.4），這裡只放入口。**不做**「我的報名」「我的抽獎」：主站 §3.14 明文網頁前台不納入
// （報名歸戶是行動 App 的功能）；「我的訂單」是站內商店 8.3 的會員入口（`MemberOrders.vue`）。
import { toMemberApiError } from '#shared/utils/member'
import type { MemberCard, MyMemberships } from '#shared/utils/member'

const emit = defineEmits<{ loggedOut: [] }>()

const { locale, lp } = useLocale()
const { member, authedFetch, logout } = useMemberSession()

type Tab = 'memberships' | 'cards' | 'orders' | 'jerseys' | 'profile'
const TABS: ReadonlyArray<{ id: Tab, label: string }> = [
  { id: 'memberships', label: '我的會籍' },
  { id: 'cards', label: '電子會員卡' },
  { id: 'orders', label: '我的訂單' },
  { id: 'jerseys', label: '球衣登記' },
  { id: 'profile', label: '個人資料與安全' },
]
const tab = ref<Tab>('memberships')
onMounted(() => {
  const h = window.location.hash.replace('#', '')
  if (TABS.some(t => t.id === h)) tab.value = h as Tab
})
function select(id: Tab) {
  tab.value = id
  history.replaceState(null, '', `#${id}`)
}
function onTabKey(e: KeyboardEvent) {
  const i = TABS.findIndex(t => t.id === tab.value)
  let next = i
  if (e.key === 'ArrowRight') next = (i + 1) % TABS.length
  else if (e.key === 'ArrowLeft') next = (i - 1 + TABS.length) % TABS.length
  else return
  e.preventDefault()
  select(TABS[next]!.id)
  nextTick(() => document.getElementById(`mtab-${TABS[next]!.id}`)?.focus())
}

const memberships = ref<MyMemberships | null>(null)
const cards = ref<MemberCard[]>([])
const error = ref('')

async function loadMemberships() {
  try {
    memberships.value = await authedFetch<MyMemberships>('/api/backend/member/memberships', { query: { lang: locale.value } })
  }
  catch (err) { error.value = toMemberApiError(err).detail }
}
async function loadCards() {
  try {
    const out = await authedFetch<MemberCard[] | { cards: MemberCard[] }>('/api/backend/member/cards', { query: { lang: locale.value } })
    cards.value = Array.isArray(out) ? out : out.cards
  }
  catch (err) { error.value = toMemberApiError(err).detail }
}
async function reload() {
  await Promise.all([loadMemberships(), loadCards()])
}
onMounted(reload)

async function onLogout() {
  await logout()
  emit('loggedOut')
}
</script>

<template>
  <div class="mc-dashboard">
    <div class="mc-welcome">
      <p>您好，<strong>{{ member?.name }}</strong>　<span class="mc-mono">{{ member?.memberNo }}</span></p>
      <button type="button" class="mc-link" @click="onLogout">登出</button>
    </div>

    <p v-if="member && !member.emailVerified" class="mc-alert mc-alert--info" role="status">
      您的 Email 尚未驗證，部分功能（例如送出升級申請）需要先完成驗證。
    </p>
    <p v-if="error" class="mc-alert mc-alert--error" role="alert">{{ error }}</p>

    <div class="mc-tabs" role="tablist" aria-label="會員中心功能" @keydown="onTabKey">
      <button
        v-for="t in TABS"
        :id="`mtab-${t.id}`"
        :key="t.id"
        type="button"
        role="tab"
        class="member-tabs__tab"
        :aria-selected="tab === t.id"
        :aria-controls="`mpanel-${t.id}`"
        :tabindex="tab === t.id ? 0 : -1"
        @click="select(t.id)"
      >{{ t.label }}</button>
    </div>

    <div v-show="tab === 'memberships'" id="mpanel-memberships" role="tabpanel" aria-labelledby="mtab-memberships" tabindex="0">
      <MemberMemberships :data="memberships" @changed="reload" />
      <div class="mc-benefits-wrap"><ContentMembershipBenefits /></div>
    </div>
    <div v-show="tab === 'cards'" id="mpanel-cards" role="tabpanel" aria-labelledby="mtab-cards" tabindex="0">
      <MemberCards :cards="cards" @changed="loadCards" />
      <p class="mc-note">想找哪裡可以使用？<a :href="lp('/zh/perks/')">查看特約店家清單</a>。</p>
    </div>
    <div v-show="tab === 'orders'" id="mpanel-orders" role="tabpanel" aria-labelledby="mtab-orders" tabindex="0">
      <MemberOrders v-if="tab === 'orders'" />
    </div>
    <div v-show="tab === 'jerseys'" id="mpanel-jerseys" role="tabpanel" aria-labelledby="mtab-jerseys" tabindex="0">
      <MemberJerseys v-if="tab === 'jerseys'" />
    </div>
    <div v-show="tab === 'profile'" id="mpanel-profile" role="tabpanel" aria-labelledby="mtab-profile" tabindex="0">
      <MemberProfile v-if="tab === 'profile'" @logged-out="emit('loggedOut')" />
    </div>
  </div>
</template>

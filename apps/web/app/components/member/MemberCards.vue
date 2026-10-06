<script setup lang="ts">
// app/components/member/MemberCards.vue — 電子會員卡（主站 §3.14「電子會員卡與折扣使用方式」）
//
// - **一份會籍一張卡**（家庭方案多張，API 已展平）；持有兩隊會籍者以分頁切換，各帶該俱樂部標誌與品牌色。
// - 卡面：俱樂部標誌、會員編號、QR Code、姓名、層級、有效期限。
// - QR 內容＝`{官網網址}/m/{token}`（在這裡組出）；到店出示、店家目視，**不做掃碼核銷**。
// - 重新產生 QR：同一張卡換 token，舊 token 立即失效（卡片外流時自保）。
// 🔴 不得在卡面標示「適用球隊」或同時標示兩種會籍（token 已隱含俱樂部，驗證頁亦不得有該欄位）。
import { formatPlainDate, toMemberApiError } from '#shared/utils/member'
import type { MemberCard } from '#shared/utils/member'

const props = defineProps<{ cards: MemberCard[] }>()
const emit = defineEmits<{ changed: [] }>()

const { authedFetch } = useMemberSession()
const { isEn, tx } = useLocale()
const siteConfig = useSiteConfig()

const index = ref(0)
const current = computed(() => props.cards[Math.min(index.value, props.cards.length - 1)])

const base = computed(() => {
  const site = (siteConfig.url ?? '').replace(/\/$/, '')
  return site || (import.meta.client ? window.location.origin : '')
})
const qrValue = computed(() => (current.value ? `${base.value}/m/${current.value.token}` : ''))

// v3.20：卡片主色／標誌取自前台靜態資產（依該張卡的俱樂部代碼），不再讀 API。
const cardAssets = computed(() => getClubAssets(current.value?.club.code ?? ''))
const cardStyle = computed(() => ({ '--mc-card-main': cardAssets.value.themeColor, '--mc-card-sub': 'var(--ink)' }))

const busy = ref(false)
const message = ref('')
const error = ref('')

async function regenerate() {
  const c = current.value
  if (!c) return
  if (!window.confirm(tx('重新產生 QR Code 後，舊的 QR Code 會立即失效。確定要重新產生嗎？', 'Once you regenerate the QR code, the old one stops working immediately. Regenerate it?'))) return
  busy.value = true
  message.value = ''
  error.value = ''
  try {
    await authedFetch(`/api/backend/member/cards/${c.id}/regenerate`, { method: 'POST', body: {} })
    message.value = tx('已重新產生 QR Code，舊的 QR Code 已失效。', 'Your QR code has been regenerated and the old one is no longer valid.')
    emit('changed')
  }
  catch (err) { error.value = toMemberApiError(err, undefined, isEn.value).detail }
  finally { busy.value = false }
}

function tabLabel(c: MemberCard) {
  const same = props.cards.filter(x => x.club.code === c.club.code).length > 1
  return same ? `${c.club.name}（${c.holderName}）` : c.club.name
}
</script>

<template>
  <div class="mc-cards">
    <p v-if="cards.length === 0" class="mc-empty">
      {{ tx('目前沒有電子會員卡。完成 Email 驗證或加入俱樂部會籍後，會自動發給您第一張卡。', 'You do not have a digital membership card yet. Your first card is issued automatically once you verify your email or join a club membership.') }}
    </p>
    <template v-else>
      <div v-if="cards.length > 1" class="mc-subtabs" role="tablist" :aria-label="tx('切換會員卡', 'Switch membership card')">
        <button
          v-for="(c, i) in cards"
          :key="c.id"
          type="button"
          role="tab"
          class="mc-subtabs__tab"
          :aria-selected="i === index"
          @click="index = i; message = ''; error = ''"
        >{{ tabLabel(c) }}</button>
      </div>

      <div v-if="current" class="mc-cardwrap">
        <article class="member-card" :class="{ 'member-card--off': !current.isValid }" :style="cardStyle" :aria-label="tx(`${current.club.name}電子會員卡`, `${current.club.name} digital membership card`)">
          <header class="member-card__head">
            <img class="member-card__logo" :src="cardAssets.footerMark.src" :alt="current.club.name" height="40">
            <p class="member-card__club">{{ current.club.name }}</p>
            <p class="member-card__tier">{{ current.tierLabel }}</p>
          </header>
          <div class="member-card__body">
            <MemberQr v-if="current.status !== 'revoked'" :value="qrValue" :label="tx(`${current.club.name}會員卡 QR Code`, `${current.club.name} membership card QR code`)" />
            <dl class="member-card__meta">
              <div><dt>{{ tx('姓名', 'Name') }}</dt><dd>{{ current.holderName }}</dd></div>
              <div><dt>{{ tx('會員編號', 'Member number') }}</dt><dd>{{ current.memberNo }}</dd></div>
              <div><dt>{{ tx('有效期限', 'Valid until') }}</dt><dd>{{ current.validUntil ? formatPlainDate(current.validUntil) : '—' }}</dd></div>
              <div><dt>{{ tx('狀態', 'Status') }}</dt><dd>{{ current.statusLabel }}</dd></div>
            </dl>
          </div>
          <p v-if="!current.isValid" class="member-card__off" role="status">{{ isEn ? `This card is currently ${current.statusLabel}. Store verification will show it as invalid.` : `此卡目前${current.statusLabel}，到店驗證會顯示為無效。` }}</p>
        </article>

        <div class="mc-cardside">
          <p v-if="isEn" class="mc-note">At a partner store, just <strong>show this screen</strong>. The store checks it by eye, and no scanning or redemption is needed. Please do not share a screenshot of your QR code publicly.</p>
          <p v-else class="mc-note">到特約店家<strong>出示此畫面</strong>即可，店家目視查驗，不需掃碼核銷。請勿將 QR Code 截圖公開分享。</p>
          <p class="mc-note mc-note--small">{{ tx('QR 連結：', 'QR link: ') }}<span class="mc-mono">{{ base }}/m/…</span>{{ tx('（掃開僅顯示姓名首字、會員編號、層級與有效／已過期）', ' (opening it shows only the first letter of your name, your member number, your tier and whether the card is valid or expired)') }}</p>
          <p v-if="current.status !== 'revoked'">
            <button type="button" class="btn btn--dark btn--sm" :disabled="busy" @click="regenerate">{{ busy ? tx('處理中…', 'Processing…') : tx('重新產生 QR Code', 'Regenerate QR code') }}</button>
          </p>
          <p v-if="current.reissueCount > 0" class="mc-note mc-note--small">{{ isEn ? `This card has been regenerated ${current.reissueCount} ${current.reissueCount === 1 ? 'time' : 'times'}.` : `此卡已重新產生 ${current.reissueCount} 次。` }}</p>
          <p v-if="message" class="mc-alert mc-alert--ok" role="status">{{ message }}</p>
          <p v-if="error" class="mc-alert mc-alert--error" role="alert">{{ error }}</p>
        </div>
      </div>
    </template>
  </div>
</template>

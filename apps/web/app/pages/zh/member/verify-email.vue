<script setup lang="ts">
// app/pages/zh/member/verify-email.vue — Email 驗證連結頁（`/{zh|en}/member/verify-email?token=…`，S2-11）
//
// 信件連結的落地頁。驗證在瀏覽器端進行（onMounted 才呼叫），後端驗證是冪等的，重複點擊不會出錯。
// 成功後該俱樂部會自動建立免費（一般會員）會籍與第一張會員卡。連結 24 小時有效，失效時提供重寄。
import { toMemberApiError } from '#shared/utils/member'

definePageMeta({ nav: '', unit: '14', enReady: true, enReadyBw: true })

const { lp, locale, isEn, tx } = useLocale()
const route = useRoute()
const config = useRuntimeConfig()
const clubAssets = computed(() => getClubAssets(config.public.club))
const clubNameEn = computed(() => getShopClubNameEn(config.public.club === 'bw' ? 'bw' : 'tcrfc'))

useSeoMeta({
  title: computed(() => (isEn.value ? getShopSeoEn('verifyEmail', clubNameEn.value).title : `Email 驗證｜${clubAssets.value.nameZh}`)),
  robots: 'noindex, nofollow',
})

const state = ref<'checking' | 'ok' | 'bad'>('checking')
const message = ref('')

onMounted(async () => {
  const token = typeof route.query.token === 'string' ? route.query.token : ''
  if (!token) {
    state.value = 'bad'
    message.value = tx('驗證連結不完整，請回到信件重新點擊連結。', 'The verification link is incomplete. Please click the link in the email again.')
    return
  }
  try {
    await $fetch('/api/backend/member/auth/verify-email', { method: 'POST', body: { token } })
    state.value = 'ok'
  }
  catch (err) {
    state.value = 'bad'
    message.value = toMemberApiError(err, tx('驗證連結無效或已過期。', 'This verification link is invalid or has expired.'), isEn.value).detail
  }
})

const email = ref('')
const resendBusy = ref(false)
const resendMessage = ref('')
async function resend() {
  if (!email.value.trim()) { resendMessage.value = tx('請輸入註冊用的 Email。', 'Please enter the email address you registered with.'); return }
  resendBusy.value = true
  try {
    await $fetch('/api/backend/member/auth/resend-verification', {
      method: 'POST',
      body: { email: email.value.trim(), club: config.public.club, lang: locale.value },
    })
    resendMessage.value = tx('若此 Email 已註冊且尚未驗證，驗證信將於稍後寄出，請檢查信箱（含垃圾信件匣）。', 'If this email address is registered and not yet verified, a verification email will be sent shortly. Please check your inbox, including your spam folder.')
  }
  catch (err) { resendMessage.value = toMemberApiError(err, undefined, isEn.value).detail }
  finally { resendBusy.value = false }
}
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/member/')">{{ tx('會員中心', 'Member Centre') }}</a></li>
      <li aria-current="page">{{ tx('Email 驗證', 'Email verification') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">Member Centre</p>
    <h1>{{ tx('Email 驗證', 'Email verification') }}<span v-if="!isEn" class="en">Verify Email</span></h1>
  </div>
</section>

<section class="band member-band" aria-labelledby="verify-title">
  <div class="band-inner container">
    <h2 id="verify-title" class="visually-hidden">{{ tx('Email 驗證結果', 'Email verification result') }}</h2>
    <div class="form-layout form-layout--narrow">
      <p v-if="state === 'checking'" class="mc-empty" role="status">{{ tx('驗證中…', 'Verifying…') }}</p>
      <div v-else-if="state === 'ok'" class="mc-alert mc-alert--ok" role="status">
        <p v-if="isEn"><strong>Your email has been verified.</strong> Your account is now active, and a Registered member membership and digital membership card for {{ clubNameEn }} have been created.</p>
        <p v-else><strong>Email 驗證完成。</strong>您的帳號已啟用，並已建立{{ clubAssets.shortNameZh }}的一般會員會籍與電子會員卡。</p>
        <p><a class="btn btn--primary btn--sm" :href="lp('/zh/member/')">{{ tx('前往登入', 'Go to sign in') }}</a></p>
      </div>
      <template v-else>
        <div class="mc-alert mc-alert--error" role="alert">
          <p>{{ message }}</p>
        </div>
        <form class="tcrfc-form" novalidate @submit.prevent="resend">
          <fieldset>
            <legend>{{ tx('重寄驗證信', 'Resend verification email') }}</legend>
            <div class="form-field">
              <label for="ve-email">{{ tx('註冊用的 Email', 'Email address you registered with') }}</label>
              <input id="ve-email" v-model="email" type="email" autocomplete="email" maxlength="200">
            </div>
          </fieldset>
          <p v-if="resendMessage" class="mc-alert mc-alert--info" role="status">{{ resendMessage }}</p>
          <button type="submit" class="btn btn--primary btn--block" :disabled="resendBusy">{{ resendBusy ? tx('處理中…', 'Processing…') : tx('重寄驗證信', 'Resend verification email') }}</button>
        </form>
      </template>
    </div>
  </div>
</section>
</template>

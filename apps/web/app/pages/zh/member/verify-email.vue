<script setup lang="ts">
// app/pages/zh/member/verify-email.vue — Email 驗證連結頁（`/{zh|en}/member/verify-email?token=…`，S2-11）
//
// 信件連結的落地頁。驗證在瀏覽器端進行（onMounted 才呼叫），後端驗證是冪等的，重複點擊不會出錯。
// 成功後該俱樂部會自動建立免費（一般會員）會籍與第一張會員卡。連結 24 小時有效，失效時提供重寄。
import { toMemberApiError } from '#shared/utils/member'

definePageMeta({ nav: '', unit: '14' })

const { lp, locale } = useLocale()
const route = useRoute()
const config = useRuntimeConfig()
const clubAssets = computed(() => getClubAssets(config.public.club))

useSeoMeta({
  title: computed(() => `Email 驗證｜${clubAssets.value.nameZh}`),
  robots: 'noindex, nofollow',
})

const state = ref<'checking' | 'ok' | 'bad'>('checking')
const message = ref('')

onMounted(async () => {
  const token = typeof route.query.token === 'string' ? route.query.token : ''
  if (!token) {
    state.value = 'bad'
    message.value = '驗證連結不完整，請回到信件重新點擊連結。'
    return
  }
  try {
    await $fetch('/api/backend/member/auth/verify-email', { method: 'POST', body: { token } })
    state.value = 'ok'
  }
  catch (err) {
    state.value = 'bad'
    message.value = toMemberApiError(err, '驗證連結無效或已過期。').detail
  }
})

const email = ref('')
const resendBusy = ref(false)
const resendMessage = ref('')
async function resend() {
  if (!email.value.trim()) { resendMessage.value = '請輸入註冊用的 Email。'; return }
  resendBusy.value = true
  try {
    await $fetch('/api/backend/member/auth/resend-verification', {
      method: 'POST',
      body: { email: email.value.trim(), club: config.public.club, lang: locale.value },
    })
    resendMessage.value = '若此 Email 已註冊且尚未驗證，驗證信將於稍後寄出，請檢查信箱（含垃圾信件匣）。'
  }
  catch (err) { resendMessage.value = toMemberApiError(err).detail }
  finally { resendBusy.value = false }
}
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/member/')">會員中心</a></li>
      <li aria-current="page">Email 驗證</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">Member Centre</p>
    <h1>Email 驗證<span class="en">Verify Email</span></h1>
  </div>
</section>

<section class="band member-band" aria-labelledby="verify-title">
  <div class="band-inner container">
    <h2 id="verify-title" class="visually-hidden">Email 驗證結果</h2>
    <div class="form-layout form-layout--narrow">
      <p v-if="state === 'checking'" class="mc-empty" role="status">驗證中…</p>
      <div v-else-if="state === 'ok'" class="mc-alert mc-alert--ok" role="status">
        <p><strong>Email 驗證完成。</strong>您的帳號已啟用，並已建立{{ clubAssets.shortNameZh }}的一般會員會籍與電子會員卡。</p>
        <p><a class="btn btn--primary btn--sm" :href="lp('/zh/member/')">前往登入</a></p>
      </div>
      <template v-else>
        <div class="mc-alert mc-alert--error" role="alert">
          <p>{{ message }}</p>
        </div>
        <form class="tcrfc-form" novalidate @submit.prevent="resend">
          <fieldset>
            <legend>重寄驗證信</legend>
            <div class="form-field">
              <label for="ve-email">註冊用的 Email</label>
              <input id="ve-email" v-model="email" type="email" autocomplete="email" maxlength="200">
            </div>
          </fieldset>
          <p v-if="resendMessage" class="mc-alert mc-alert--info" role="status">{{ resendMessage }}</p>
          <button type="submit" class="btn btn--primary btn--block" :disabled="resendBusy">{{ resendBusy ? '處理中…' : '重寄驗證信' }}</button>
        </form>
      </template>
    </div>
  </div>
</section>
</template>

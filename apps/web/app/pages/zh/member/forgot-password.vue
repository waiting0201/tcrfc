<script setup lang="ts">
// app/pages/zh/member/forgot-password.vue — 忘記密碼（S2-11）
// 後端一律回 202（不洩漏 Email 是否註冊），畫面也一律顯示同一句話。重設連結 1 小時有效、只能用一次。
import { toMemberApiError } from '#shared/utils/member'

definePageMeta({ nav: '', unit: '14' })

const { lp, locale } = useLocale()
const config = useRuntimeConfig()
const clubAssets = computed(() => getClubAssets(config.public.club))

useSeoMeta({
  title: computed(() => `忘記密碼｜${clubAssets.value.nameZh}`),
  robots: 'noindex, nofollow',
})

const email = ref('')
const busy = ref(false)
const sent = ref(false)
const error = ref('')

async function submit() {
  error.value = ''
  if (!email.value.trim()) { error.value = '請輸入註冊用的 Email。'; return }
  busy.value = true
  try {
    await $fetch('/api/backend/member/auth/forgot-password', {
      method: 'POST',
      body: { email: email.value.trim(), club: config.public.club, lang: locale.value },
    })
    sent.value = true
  }
  catch (err) { error.value = toMemberApiError(err).detail }
  finally { busy.value = false }
}
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/member/')">會員中心</a></li>
      <li aria-current="page">忘記密碼</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">Member Centre</p>
    <h1>忘記密碼<span class="en">Forgot Password</span></h1>
    <p class="page-hero__lede">輸入註冊用的 Email，我們會寄出重設密碼的連結。</p>
  </div>
</section>

<section class="band member-band" aria-labelledby="forgot-title">
  <div class="band-inner container">
    <h2 id="forgot-title" class="visually-hidden">重設密碼申請</h2>
    <div class="form-layout form-layout--narrow">
      <div v-if="sent" class="mc-alert mc-alert--ok" role="status">
        <p>若此 Email 已註冊，重設密碼的連結將於稍後寄出（1 小時內有效，只能使用一次），請檢查信箱，含垃圾信件匣。</p>
        <p><a :href="lp('/zh/member/')">回到會員登入</a></p>
      </div>
      <form v-else class="tcrfc-form" novalidate @submit.prevent="submit">
        <fieldset>
          <legend>重設密碼</legend>
          <div class="form-field">
            <label for="fp-email">Email<span class="req" aria-hidden="true">*</span></label>
            <input id="fp-email" v-model="email" type="email" required autocomplete="email" maxlength="200">
          </div>
        </fieldset>
        <p v-if="error" class="mc-alert mc-alert--error" role="alert">{{ error }}</p>
        <button type="submit" class="btn btn--primary btn--block" :disabled="busy">{{ busy ? '處理中…' : '寄出重設連結' }}</button>
        <p class="member-forgot"><a :href="lp('/zh/member/')">回到會員登入</a></p>
      </form>
    </div>
  </div>
</section>
</template>

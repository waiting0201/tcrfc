<script setup lang="ts">
// app/pages/zh/member/reset-password.vue — 重設密碼連結頁（`/{zh|en}/member/reset-password?token=…`，S2-11）
// 連結只能用一次（密碼一改就失效）；成功後全部裝置登出，並順便完成 Email 驗證。
import { MEMBER_PASSWORD_HINT, MEMBER_PASSWORD_HINT_EN, passwordProblem, toMemberApiError } from '#shared/utils/member'

definePageMeta({ nav: '', unit: '14', enReady: true, enReadyBw: true })

const { lp, isEn, tx } = useLocale()
const route = useRoute()
const config = useRuntimeConfig()
const clubAssets = computed(() => getClubAssets(config.public.club))
const clubNameEn = computed(() => getShopClubNameEn(config.public.club === 'bw' ? 'bw' : 'tcrfc'))

useSeoMeta({
  title: computed(() => (isEn.value ? getShopSeoEn('resetPassword', clubNameEn.value).title : `重設密碼｜${clubAssets.value.nameZh}`)),
  robots: 'noindex, nofollow',
})

// token 從網址讀（SSR 與 client 一致，不放進任何儲存空間）；表單成功後不再保留
const token = computed(() => (typeof route.query.token === 'string' ? route.query.token : ''))
const password = ref('')
const confirmPw = ref('')
const busy = ref(false)
const done = ref(false)
const error = ref('')
const linkDead = ref(false)

async function submit() {
  error.value = ''
  const problem = passwordProblem(password.value, isEn.value)
  if (problem) { error.value = problem; return }
  if (password.value !== confirmPw.value) { error.value = tx('兩次輸入的新密碼不一致。', 'The two new passwords do not match.'); return }
  busy.value = true
  try {
    await $fetch('/api/backend/member/auth/reset-password', { method: 'POST', body: { token: token.value, newPassword: password.value } })
    done.value = true
    password.value = confirmPw.value = ''
  }
  catch (err) {
    const e = toMemberApiError(err, tx('重設連結無效或已過期。', 'This reset link is invalid or has expired.'), isEn.value)
    error.value = e.detail
    linkDead.value = e.code === 'token_invalid'
  }
  finally { busy.value = false }
}
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/member/')">{{ tx('會員中心', 'Member Centre') }}</a></li>
      <li aria-current="page">{{ tx('重設密碼', 'Reset password') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">Member Centre</p>
    <h1>{{ tx('重設密碼', 'Reset password') }}<span v-if="!isEn" class="en">Reset Password</span></h1>
  </div>
</section>

<section class="band member-band" aria-labelledby="reset-title">
  <div class="band-inner container">
    <h2 id="reset-title" class="visually-hidden">{{ tx('設定新密碼', 'Set a new password') }}</h2>
    <div class="form-layout form-layout--narrow">
      <div v-if="!token" class="mc-alert mc-alert--error" role="alert">
        <p v-if="isEn">The reset link is incomplete. Please click the link in the email again, or <a :href="lp('/zh/member/forgot-password/')">request a new one</a>.</p>
        <p v-else>重設連結不完整，請回到信件重新點擊連結，或<a :href="lp('/zh/member/forgot-password/')">重新申請</a>。</p>
      </div>
      <div v-else-if="done" class="mc-alert mc-alert--ok" role="status">
        <p><strong>{{ tx('密碼已重設。', 'Your password has been reset.') }}</strong>{{ tx('為了安全，所有裝置都已登出，請用新密碼重新登入。', ' For your security, all devices have been signed out. Please sign in again with your new password.') }}</p>
        <p><a class="btn btn--primary btn--sm" :href="lp('/zh/member/')">{{ tx('前往登入', 'Go to sign in') }}</a></p>
      </div>
      <form v-else class="tcrfc-form" novalidate @submit.prevent="submit">
        <fieldset>
          <legend>{{ tx('設定新密碼', 'Set a new password') }}</legend>
          <div class="form-grid" style="grid-template-columns:1fr;">
            <div class="form-field">
              <label for="rp-new">{{ tx('新密碼', 'New password') }}<span class="req" aria-hidden="true">*</span></label>
              <input id="rp-new" v-model="password" type="password" required autocomplete="new-password" maxlength="128" aria-describedby="rp-hint">
              <p id="rp-hint" class="field-hint">{{ isEn ? MEMBER_PASSWORD_HINT_EN : MEMBER_PASSWORD_HINT }}</p>
            </div>
            <div class="form-field">
              <label for="rp-confirm">{{ tx('再輸入一次新密碼', 'Re-enter new password') }}<span class="req" aria-hidden="true">*</span></label>
              <input id="rp-confirm" v-model="confirmPw" type="password" required autocomplete="new-password" maxlength="128">
            </div>
          </div>
        </fieldset>
        <div v-if="error" class="mc-alert mc-alert--error" role="alert">
          <p>{{ error }}</p>
          <p v-if="linkDead"><a :href="lp('/zh/member/forgot-password/')">{{ tx('重新申請重設連結', 'Request a new reset link') }}</a></p>
        </div>
        <button type="submit" class="btn btn--primary btn--block" :disabled="busy">{{ busy ? tx('處理中…', 'Processing…') : tx('重設密碼', 'Reset password') }}</button>
      </form>
    </div>
  </div>
</section>
</template>

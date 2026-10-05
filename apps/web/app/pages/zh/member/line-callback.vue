<script setup lang="ts">
// app/pages/zh/member/line-callback.vue — LINE 授權導回頁（S2-11）
//
// LINE 導回 `?code=…&state=…`。**state 必須與 sessionStorage 裡的值一致，不符一律丟棄**（防登入 CSRF）；
// 授權碼只用一次。結果有三種：
//   - `logged_in`：登入完成（更新權杖由 Nuxt 伺服器寫進 HttpOnly Cookie），回會員中心（或 `next`）。
//   - `bound`：綁定完成，回「個人資料與安全」。
//   - `signup_required`：這個 LINE 帳號還沒有會員，請補 Email 完成註冊（不會自動併入同 Email 的既有帳號，
//     LINE 提供的 Email 不保證驗證過；Email 已有帳號時 409 `email_taken`，引導先用 Email 登入再綁定）。
// 後端缺 LINE 憑證回 503：顯示「暫不提供」，不是錯誤頁。
//
// ⚠️ 導回網址 `/zh/member/line-callback/` 必須登記在後端 `LINE_LOGIN_REDIRECT_URIS` 與 LINE Developers 的 Callback URL。
import { GUARDIAN_CONSENT_VERSION_PENDING, isMinorBirth, taipeiAge, toMemberApiError } from '#shared/utils/member'
import type { GuardianRelationship, LineCallbackResult, MemberBrowserSession } from '#shared/utils/member'
import type { LinePending } from '~/composables/useMemberLine'

definePageMeta({ nav: '', unit: '14' })

const { lp } = useLocale()
const route = useRoute()
const config = useRuntimeConfig()
const clubAssets = computed(() => getClubAssets(config.public.club))
const { authedFetch, adopt } = useMemberSession()

useSeoMeta({
  title: computed(() => `LINE 登入｜${clubAssets.value.nameZh}`),
  robots: 'noindex, nofollow',
})

const state = ref<'working' | 'signup' | 'error'>('working')
const message = ref('')
const ticket = ref('')
const form = reactive({ email: '', name: '', phone: '', birthOn: '' })
const guardian = ref<{ consented: boolean, name: string, relationship: GuardianRelationship | '' }>({ consented: false, name: '', relationship: '' })
const busy = ref(false)
const formError = ref('')
let pendingInfo: LinePending | null = null

function backTo(pending: LinePending | null, fallbackHash = ''): string {
  const loc = pending?.locale ?? 'zh'
  return pending?.next ?? `${localizePath('/zh/member/', loc)}${fallbackHash}`
}

onMounted(async () => {
  const q = route.query
  const pending = readLinePending()
  pendingInfo = pending
  // 取用後立即作廢：授權碼與 state 都只能用一次
  clearLinePending()

  if (typeof q.error === 'string') {
    state.value = 'error'
    message.value = q.error === 'access_denied' ? '您取消了 LINE 授權，未做任何變更。' : 'LINE 授權失敗，請稍後再試或改用 Email 登入。'
    return
  }
  const code = typeof q.code === 'string' ? q.code : ''
  const st = typeof q.state === 'string' ? q.state : ''
  if (!code || !st) {
    state.value = 'error'
    message.value = 'LINE 授權資料不完整，請回會員中心重新操作。'
    return
  }
  // state 比對：沒有進行中的流程、或與網址上的不符，一律丟棄
  if (!pending || pending.state !== st) {
    state.value = 'error'
    message.value = '授權驗證失敗（state 不符或流程已逾時），為了您的帳號安全已中止，請回會員中心重新操作。'
    return
  }

  try {
    const body = { code, state: st }
    const result = pending.mode === 'bind'
      ? await authedFetch<LineCallbackResult>('/api/member-auth/line-callback', { method: 'POST', body })
      : await $fetch<LineCallbackResult>('/api/member-auth/line-callback', { method: 'POST', body })

    if (result.status === 'logged_in') {
      adopt(result.session)
      await navigateTo(backTo(pending), { replace: true })
    }
    else if (result.status === 'bound') {
      await navigateTo(`${localizePath('/zh/member/', pending.locale)}#profile`, { replace: true })
    }
    else {
      ticket.value = result.ticket
      form.email = result.suggestedEmail ?? ''
      form.name = result.displayName ?? ''
      state.value = 'signup'
    }
  }
  catch (err) {
    const e = toMemberApiError(err)
    state.value = 'error'
    message.value = e.status === 503 || e.code === 'line_not_configured'
      ? 'LINE 登入目前暫不提供，請改用 Email 登入。'
      : e.detail
  }
})

async function complete() {
  formError.value = ''
  if (!form.email.trim()) { formError.value = '請輸入 Email。'; return }
  if (!form.birthOn) { formError.value = '請填寫生日。'; return }
  if (taipeiAge(form.birthOn) === null) { formError.value = '生日格式不正確，或晚於今天。'; return }
  const minor = isMinorBirth(form.birthOn)
  if (minor) {
    const g = guardian.value
    if (!g.name.trim()) { formError.value = '請填寫監護人姓名。'; return }
    if (!g.relationship) { formError.value = '請選擇監護人與會員的關係。'; return }
    if (!g.consented) { formError.value = '未滿 18 歲須經監護人同意，請由監護人勾選同意。'; return }
  }
  busy.value = true
  try {
    const session = await $fetch<MemberBrowserSession>('/api/member-auth/line-complete', {
      method: 'POST',
      body: {
        club: config.public.club,
        ticket: ticket.value,
        email: form.email.trim(),
        name: form.name.trim() || undefined,
        phone: form.phone.trim() || undefined,
        birthOn: form.birthOn,
        // 文案版本標 pending-legal：同意條款待法務定稿（B-9）。
        guardianConsent: minor
          ? { consented: true, guardianName: guardian.value.name.trim(), relationship: guardian.value.relationship, consentTextVersion: GUARDIAN_CONSENT_VERSION_PENDING }
          : undefined,
        lang: pendingInfo?.locale ?? 'zh',
      },
    })
    adopt(session)
    await navigateTo(backTo(pendingInfo), { replace: true })
  }
  catch (err) {
    const e = toMemberApiError(err)
    formError.value = e.code === 'email_taken'
      ? `${e.detail} 請先用 Email 登入，再到會員中心「個人資料與安全」綁定 LINE。`
      : e.detail
  }
  finally { busy.value = false }
}
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/member/')">會員中心</a></li>
      <li aria-current="page">LINE 登入</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">Member Centre</p>
    <h1>LINE 登入<span class="en">LINE Login</span></h1>
  </div>
</section>

<section class="band member-band" aria-labelledby="line-title">
  <div class="band-inner container">
    <h2 id="line-title" class="visually-hidden">LINE 授權結果</h2>
    <div class="form-layout form-layout--narrow">
      <p v-if="state === 'working'" class="mc-empty" role="status">處理 LINE 授權中…</p>
      <div v-else-if="state === 'error'" class="mc-alert mc-alert--error" role="alert">
        <p>{{ message }}</p>
        <p><a :href="lp('/zh/member/')">回到會員中心</a></p>
      </div>
      <form v-else class="tcrfc-form" novalidate @submit.prevent="complete">
        <fieldset>
          <legend>完成註冊</legend>
          <p class="mc-note">這個 LINE 帳號還沒有會員資料。請補上 Email 完成註冊（我們會寄驗證信；之後可用 LINE 一鍵登入）。</p>
          <div class="form-grid" style="grid-template-columns:1fr;">
            <div class="form-field">
              <label for="lc-email">Email<span class="req" aria-hidden="true">*</span></label>
              <input id="lc-email" v-model="form.email" type="email" required autocomplete="email" maxlength="200">
            </div>
            <div class="form-field">
              <label for="lc-name">姓名</label>
              <input id="lc-name" v-model="form.name" type="text" autocomplete="name" maxlength="60">
            </div>
            <div class="form-field">
              <label for="lc-phone">手機</label>
              <input id="lc-phone" v-model="form.phone" type="tel" autocomplete="tel" maxlength="30">
            </div>
            <MemberAgeGuardian v-model:birth-on="form.birthOn" v-model:guardian="guardian" id-prefix="lc" />
          </div>
        </fieldset>
        <p v-if="formError" class="mc-alert mc-alert--error" role="alert">{{ formError }}</p>
        <button type="submit" class="btn btn--primary btn--block" :disabled="busy">{{ busy ? '處理中…' : '完成註冊' }}</button>
      </form>
    </div>
  </div>
</section>
</template>

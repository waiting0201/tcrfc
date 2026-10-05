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

definePageMeta({ nav: '', unit: '14', enReady: true })

const { lp, isEn: isEnPath } = useLocale()
const route = useRoute()
const config = useRuntimeConfig()
const clubAssets = computed(() => getClubAssets(config.public.club))
// 🔴 LINE 的導回網址固定是 `/zh/member/line-callback/`（兩個語系共用，見 useMemberLine.ts 檔頭），英文使用者回來時
// 網址是 /zh/。所以英文與否＝網址是 /en/，或這次授權流程記下的語系是 en（onMounted 讀 sessionStorage 後才知道，
// SSR 與 hydration 一律先出繁中，不會 mismatch）。藍鯨站（isEn 恆 false）不適用英文。
const pendingLocale = ref<'zh' | 'en'>('zh')
const isEn = computed(() => isEnPath.value || (pendingLocale.value === 'en' && config.public.club !== 'bw'))
const tx = (zh: string, en: string): string => (isEn.value ? en : zh)
const lpx = (path: string): string => (isEn.value ? localizePath(path, 'en') : lp(path))
const { authedFetch, adopt } = useMemberSession()

useSeoMeta({
  title: computed(() => (isEn.value ? getShopSeoEn('lineCallback', CLUB_NAME_EN).title : `LINE 登入｜${clubAssets.value.nameZh}`)),
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
  if (pending?.locale === 'en') pendingLocale.value = 'en'
  // 取用後立即作廢：授權碼與 state 都只能用一次
  clearLinePending()

  if (typeof q.error === 'string') {
    state.value = 'error'
    message.value = q.error === 'access_denied'
      ? tx('您取消了 LINE 授權，未做任何變更。', 'You cancelled the LINE authorisation. No changes were made.')
      : tx('LINE 授權失敗，請稍後再試或改用 Email 登入。', 'LINE authorisation failed. Please try again later or sign in with your email instead.')
    return
  }
  const code = typeof q.code === 'string' ? q.code : ''
  const st = typeof q.state === 'string' ? q.state : ''
  if (!code || !st) {
    state.value = 'error'
    message.value = tx('LINE 授權資料不完整，請回會員中心重新操作。', 'The LINE authorisation data is incomplete. Please go back to the Member Centre and try again.')
    return
  }
  // state 比對：沒有進行中的流程、或與網址上的不符，一律丟棄
  if (!pending || pending.state !== st) {
    state.value = 'error'
    message.value = tx('授權驗證失敗（state 不符或流程已逾時），為了您的帳號安全已中止，請回會員中心重新操作。', 'Authorisation could not be verified (the state did not match or the process timed out). For the security of your account it has been stopped. Please go back to the Member Centre and try again.')
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
    const e = toMemberApiError(err, undefined, isEn.value)
    state.value = 'error'
    message.value = e.status === 503 || e.code === 'line_not_configured'
      ? tx('LINE 登入目前暫不提供，請改用 Email 登入。', 'LINE sign-in is not available right now. Please sign in with your email instead.')
      : e.detail
  }
})

async function complete() {
  formError.value = ''
  if (!form.email.trim()) { formError.value = tx('請輸入 Email。', 'Please enter your email address.'); return }
  if (!form.birthOn) { formError.value = tx('請填寫生日。', 'Please enter your date of birth.'); return }
  if (taipeiAge(form.birthOn) === null) { formError.value = tx('生日格式不正確，或晚於今天。', 'The date of birth is not valid, or it is in the future.'); return }
  const minor = isMinorBirth(form.birthOn)
  if (minor) {
    const g = guardian.value
    if (!g.name.trim()) { formError.value = tx('請填寫監護人姓名。', 'Please enter the guardian name.'); return }
    if (!g.relationship) { formError.value = tx('請選擇監護人與會員的關係。', 'Please choose the guardian relationship to the member.'); return }
    if (!g.consented) { formError.value = tx('未滿 18 歲須經監護人同意，請由監護人勾選同意。', 'Members under 18 need their guardian\'s consent. Please ask the guardian to tick the consent box.'); return }
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
    const e = toMemberApiError(err, undefined, isEn.value)
    formError.value = e.code === 'email_taken'
      ? `${e.detail} ${tx('請先用 Email 登入，再到會員中心「個人資料與安全」綁定 LINE。', 'Please sign in with your email first, then link LINE under Profile and security in the Member Centre.')}`
      : e.detail
  }
  finally { busy.value = false }
}
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lpx('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lpx('/zh/member/')">{{ tx('會員中心', 'Member Centre') }}</a></li>
      <li aria-current="page">{{ tx('LINE 登入', 'LINE sign-in') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">Member Centre</p>
    <h1>{{ tx('LINE 登入', 'LINE sign-in') }}<span v-if="!isEn" class="en">LINE Login</span></h1>
  </div>
</section>

<section class="band member-band" aria-labelledby="line-title">
  <div class="band-inner container">
    <h2 id="line-title" class="visually-hidden">{{ tx('LINE 授權結果', 'LINE authorisation result') }}</h2>
    <div class="form-layout form-layout--narrow">
      <p v-if="state === 'working'" class="mc-empty" role="status">{{ tx('處理 LINE 授權中…', 'Processing LINE authorisation…') }}</p>
      <div v-else-if="state === 'error'" class="mc-alert mc-alert--error" role="alert">
        <p>{{ message }}</p>
        <p><a :href="lpx('/zh/member/')">{{ tx('回到會員中心', 'Back to the Member Centre') }}</a></p>
      </div>
      <form v-else class="tcrfc-form" novalidate @submit.prevent="complete">
        <fieldset>
          <legend>{{ tx('完成註冊', 'Complete your registration') }}</legend>
          <p class="mc-note">{{ tx('這個 LINE 帳號還沒有會員資料。請補上 Email 完成註冊（我們會寄驗證信；之後可用 LINE 一鍵登入）。', 'This LINE account does not have member details yet. Please add your email to finish registering (we will send a verification email, and you can then sign in with LINE in one step).') }}</p>
          <div class="form-grid" style="grid-template-columns:1fr;">
            <div class="form-field">
              <label for="lc-email">Email<span class="req" aria-hidden="true">*</span></label>
              <input id="lc-email" v-model="form.email" type="email" required autocomplete="email" maxlength="200">
            </div>
            <div class="form-field">
              <label for="lc-name">{{ tx('姓名', 'Name') }}</label>
              <input id="lc-name" v-model="form.name" type="text" autocomplete="name" maxlength="60">
            </div>
            <div class="form-field">
              <label for="lc-phone">{{ tx('手機', 'Mobile phone') }}</label>
              <input id="lc-phone" v-model="form.phone" type="tel" autocomplete="tel" maxlength="30">
            </div>
            <MemberAgeGuardian v-model:birth-on="form.birthOn" v-model:guardian="guardian" id-prefix="lc" :en="isEn" />
          </div>
        </fieldset>
        <p v-if="formError" class="mc-alert mc-alert--error" role="alert">{{ formError }}</p>
        <button type="submit" class="btn btn--primary btn--block" :disabled="busy">{{ busy ? tx('處理中…', 'Processing…') : tx('完成註冊', 'Complete registration') }}</button>
      </form>
    </div>
  </div>
</section>
</template>

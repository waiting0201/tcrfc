<script setup lang="ts">
// app/components/member/MemberAuthPanel.vue — 會員登入／加入會員（S2-11，主站 §3.14「前台功能」）
//
// 版型沿用 mockup 的 member-tabs（DOM／class 不動），把靜態表單接上真實流程：
//   - 登入：Email＋密碼、記住我、LINE 一鍵登入；失敗依後端 `code` 區分（未驗證信箱／帳號鎖定／停用）。
//   - 加入會員：Email 註冊，寄驗證信後才能登入（驗證前不能以密碼登入）。
// 🔴 寄信供應商尚未串接時（後端 `emailSent=false`），如實告知「驗證信尚未寄出」，不假裝成功。
import { GUARDIAN_CONSENT_VERSION_PENDING, MEMBER_PASSWORD_HINT, MEMBER_PASSWORD_HINT_EN, formatTaipeiDateTime, isMinorBirth, passwordProblem, taipeiAge, toMemberApiError } from '#shared/utils/member'
import type { GuardianRelationship, MemberRegistered } from '#shared/utils/member'

const config = useRuntimeConfig()
const { locale, lp, isEn, tx } = useLocale()
const clubAssets = computed(() => getClubAssets(config.public.club))
const clubNameEn = computed(() => getShopClubNameEn(config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const { login } = useMemberSession()
const line = useMemberLine()

const tab = ref<'login' | 'register'>('login')
onMounted(() => {
  // header 的「註冊」連到 /zh/member/#tab-register，進站時直接切到加入會員分頁
  if (window.location.hash === '#tab-register') tab.value = 'register'
})

function onTabKey(e: KeyboardEvent) {
  if (e.key !== 'ArrowRight' && e.key !== 'ArrowLeft') return
  e.preventDefault()
  tab.value = tab.value === 'login' ? 'register' : 'login'
  nextTick(() => document.getElementById(`tab-${tab.value}`)?.focus())
}

// ── 登入 ──
const loginEmail = ref('')
const loginPassword = ref('')
const rememberMe = ref(false)
const loginBusy = ref(false)
const loginError = ref('')
const needVerify = ref(false)
const lineMessage = ref('')
const lineBusy = ref(false)

async function onLogin() {
  loginError.value = ''
  needVerify.value = false
  if (!loginEmail.value.trim() || !loginPassword.value) {
    loginError.value = tx('請輸入 Email 與密碼。', 'Please enter your email and password.')
    return
  }
  loginBusy.value = true
  try {
    await login(loginEmail.value.trim(), loginPassword.value, rememberMe.value)
    loginPassword.value = ''
  }
  catch (err) {
    const e = toMemberApiError(err, undefined, isEn.value)
    if (e.code === 'account_locked' && e.lockedUntil) {
      loginError.value = tx(`登入失敗次數過多，帳號暫時鎖定，請於 ${formatTaipeiDateTime(e.lockedUntil, locale.value)} 後再試。`, `Too many failed sign-in attempts. Your account is temporarily locked. Please try again after ${formatTaipeiDateTime(e.lockedUntil, locale.value)} (Taipei time).`)
    }
    else {
      loginError.value = e.detail
    }
    needVerify.value = e.code === 'email_not_verified'
  }
  finally { loginBusy.value = false }
}

async function onLine() {
  lineMessage.value = ''
  lineBusy.value = true
  const msg = await line.start('login')
  lineBusy.value = false
  if (msg) lineMessage.value = msg
}

// ── 重寄驗證信（登入被擋、註冊後都會用到） ──
const resendBusy = ref(false)
const resendMessage = ref('')
async function resend(email: string) {
  resendMessage.value = ''
  resendBusy.value = true
  try {
    await $fetch('/api/backend/member/auth/resend-verification', {
      method: 'POST',
      body: { email: email.trim(), club: config.public.club, lang: locale.value },
    })
    resendMessage.value = tx('若此 Email 已註冊且尚未驗證，驗證信將於稍後寄出，請檢查信箱（含垃圾信件匣）。', 'If this email address is registered and not yet verified, a verification email will be sent shortly. Please check your inbox, including your spam folder.')
  }
  catch (err) { resendMessage.value = toMemberApiError(err, undefined, isEn.value).detail }
  finally { resendBusy.value = false }
}

// ── 註冊 ──
const reg = reactive({ name: '', phone: '', email: '', password: '', consent: false, birthOn: '' })
const regGuardian = ref<{ consented: boolean, name: string, relationship: GuardianRelationship | '' }>({ consented: false, name: '', relationship: '' })
const regBusy = ref(false)
const regError = ref('')
const registered = ref<MemberRegistered | null>(null)

async function onRegister() {
  regError.value = ''
  if (!reg.name.trim()) { regError.value = tx('請輸入姓名。', 'Please enter your name.'); return }
  if (!reg.phone.trim()) { regError.value = tx('請輸入手機。', 'Please enter your mobile phone number.'); return }
  if (!reg.email.trim()) { regError.value = tx('請輸入 Email。', 'Please enter your email address.'); return }
  const pw = passwordProblem(reg.password, isEn.value)
  if (pw) { regError.value = pw; return }
  if (!reg.birthOn) { regError.value = tx('請填寫生日。', 'Please enter your date of birth.'); return }
  if (taipeiAge(reg.birthOn) === null) { regError.value = tx('生日格式不正確，或晚於今天。', 'The date of birth is not valid, or it is in the future.'); return }
  const minor = isMinorBirth(reg.birthOn)
  if (minor) {
    const g = regGuardian.value
    if (!g.name.trim()) { regError.value = tx('請填寫監護人姓名。', 'Please enter the guardian name.'); return }
    if (!g.relationship) { regError.value = tx('請選擇監護人與會員的關係。', 'Please choose the guardian relationship to the member.'); return }
    if (!g.consented) { regError.value = tx('未滿 18 歲須經監護人同意，請由監護人勾選同意。', 'Members under 18 need their guardian\'s consent. Please ask the guardian to tick the consent box.'); return }
  }
  if (!reg.consent) { regError.value = tx('請先閱讀並勾選同意隱私權政策與會員條款。', 'Please read and tick the box to agree to the Privacy Policy and Membership Terms.'); return }
  regBusy.value = true
  try {
    registered.value = await $fetch<MemberRegistered>('/api/backend/member/auth/register', {
      method: 'POST',
      body: {
        club: config.public.club,
        email: reg.email.trim(),
        password: reg.password,
        name: reg.name.trim(),
        phone: reg.phone.trim(),
        birthOn: reg.birthOn,
        // 成年不送監護人資料（後端也不儲存）；文案版本標 pending-legal（B-9，同意條款待法務定稿）。
        guardianConsent: minor
          ? { consented: true, guardianName: regGuardian.value.name.trim(), relationship: regGuardian.value.relationship, consentTextVersion: GUARDIAN_CONSENT_VERSION_PENDING }
          : undefined,
        lang: locale.value,
      },
    })
    reg.password = ''
  }
  catch (err) {
    const e = toMemberApiError(err, undefined, isEn.value)
    regError.value = e.code === 'email_taken'
      ? `${e.detail} ${tx('若這是您的 Email，請改用「會員登入」；忘記密碼可使用「忘記密碼」重設。', 'If this is your email address, please use Member sign-in instead. If you have forgotten your password, use Forgot password to reset it.')}`
      : e.detail
  }
  finally { regBusy.value = false }
}

</script>

<template>
  <div class="member-tabs">
    <div class="member-tabs__list" role="tablist" :aria-label="tx('登入或加入會員', 'Sign in or join')" @keydown="onTabKey">
      <button id="tab-login" type="button" role="tab" aria-controls="panel-login" :aria-selected="tab === 'login'" :tabindex="tab === 'login' ? 0 : -1" class="member-tabs__tab" @click="tab = 'login'">{{ tx('會員登入', 'Member sign-in') }}</button>
      <button id="tab-register" type="button" role="tab" aria-controls="panel-register" :aria-selected="tab === 'register'" :tabindex="tab === 'register' ? 0 : -1" class="member-tabs__tab" @click="tab = 'register'">{{ tx('加入會員', 'Join') }}</button>
    </div>

    <div v-show="tab === 'login'" id="panel-login" class="member-tabs__panel" role="tabpanel" aria-labelledby="tab-login" tabindex="0">
      <div class="form-layout form-layout--narrow">
        <form class="tcrfc-form" novalidate @submit.prevent="onLogin">
          <fieldset>
            <legend>{{ tx('會員登入', 'Member sign-in') }}</legend>
            <div class="form-grid" style="grid-template-columns:1fr;">
              <div class="form-field">
                <label for="m-login-email">Email<span class="req" aria-hidden="true">*</span></label>
                <input id="m-login-email" v-model="loginEmail" type="email" name="email" required autocomplete="email">
              </div>
              <div class="form-field">
                <label for="m-login-password">{{ tx('密碼', 'Password') }}<span class="req" aria-hidden="true">*</span></label>
                <input id="m-login-password" v-model="loginPassword" type="password" name="password" required autocomplete="current-password">
              </div>
              <div class="checkbox-field">
                <input id="m-login-remember" v-model="rememberMe" type="checkbox" name="remember">
                <label for="m-login-remember">{{ tx('記住我（此裝置保持登入 30 天）', 'Remember me (stay signed in on this device for 30 days)') }}</label>
              </div>
            </div>
          </fieldset>
          <div v-if="loginError" class="mc-alert mc-alert--error" role="alert">
            <p>{{ loginError }}</p>
            <p v-if="needVerify"><button type="button" class="mc-link" :disabled="resendBusy" @click="resend(loginEmail)">{{ tx('重寄驗證信', 'Resend verification email') }}</button></p>
          </div>
          <p v-if="resendMessage" class="mc-alert mc-alert--info" role="status">{{ resendMessage }}</p>
          <button class="btn btn--primary btn--block" type="submit" :disabled="loginBusy">{{ loginBusy ? tx('登入中…', 'Signing in…') : tx('登入', 'Sign in') }}</button>
          <p class="member-alt-login">
            <button type="button" class="btn btn--dark btn--block" :disabled="lineBusy" @click="onLine">{{ tx('以 LINE 登入', 'Sign in with LINE') }}</button>
          </p>
          <p v-if="lineMessage" class="mc-alert mc-alert--info" role="status">{{ lineMessage }}</p>
          <p class="member-forgot"><a :href="lp('/zh/member/forgot-password/')">{{ tx('忘記密碼？', 'Forgot password?') }}</a></p>
        </form>
      </div>
    </div>

    <div v-show="tab === 'register'" id="panel-register" class="member-tabs__panel" role="tabpanel" aria-labelledby="tab-register" tabindex="0">
      <!-- 權益先於表單：規劃書 3.14 要求加入頁必須讓人先看到「能得到什麼」，且未登入即可檢視 -->
      <ContentMembershipBenefits />

      <MemberPerksTeaser />

      <h2 class="section-title" style="margin-top:4rem;">{{ tx('兩種入會管道', 'Two ways to join') }}</h2>
      <p class="section-lede">{{ tx('兩種管道建立的是同一套會員資料。已用 Email 註冊的帳號，登入後可在「個人資料」綁定 LINE。', 'Both ways create the same member record. If you registered with your email, you can link LINE under Profile after signing in.') }}</p>

      <div class="channel-grid channel-grid--2">
        <div class="channel-card clip-card clip-card--outlined">
          <p class="channel-card__num">A</p>
          <h3>{{ tx('Email 註冊', 'Register with email') }}</h3>
          <p>{{ tx('於下方表單填寫並設定密碼，收到驗證信後啟用帳號。', 'Fill in the form below and set a password, then activate your account from the verification email.') }}</p>
        </div>
        <div class="channel-card clip-card clip-card--outlined">
          <p class="channel-card__num">B</p>
          <h3>{{ tx('LINE 一鍵註冊', 'Register with LINE') }}</h3>
          <p>{{ tx('以既有 LINE 帳號授權建立會員並完成綁定，免記密碼。之後可用 LINE 一鍵登入。', 'Authorise with your existing LINE account to create your membership and link it, with no password to remember. You can then sign in with LINE in one step.') }}</p>
          <p style="margin-top:1rem;"><button type="button" class="btn btn--dark btn--sm" :disabled="lineBusy" @click="onLine">{{ tx('以 LINE 註冊', 'Register with LINE') }}</button></p>
          <p v-if="lineMessage" class="mc-alert mc-alert--info" role="status" style="margin-top:.75rem;">{{ lineMessage }}</p>
        </div>
      </div>

      <div class="form-layout">
        <div v-if="registered" class="mc-alert mc-alert--ok" role="status">
          <p v-if="isEn"><strong>Your account has been created (member number {{ registered.memberNo }}).</strong></p>
          <p v-else><strong>帳號已建立（會員編號 {{ registered.memberNo }}）。</strong></p>
          <p v-if="registered.emailSent">{{ tx(`我們已寄出驗證信到 ${reg.email}，請點信中連結完成驗證後再登入（連結 24 小時內有效）。`, `We have sent a verification email to ${reg.email}. Please click the link in it to verify your email before signing in (the link is valid for 24 hours).`) }}</p>
          <p v-else-if="isEn">Your account has been created, but <strong>the verification email has not been sent yet</strong> (the email service is not active), so you cannot verify your email or sign in for now. Please contact {{ clubNameEn }} for help. We apologise for the inconvenience.</p>
          <p v-else>
            帳號已建立，但<strong>驗證信目前尚未寄出</strong>（寄信服務尚未啟用），暫時無法完成 Email 驗證與登入。
            請聯繫{{ clubAssets.nameZh }}客服協助，造成不便敬請見諒。
          </p>
          <p><button type="button" class="mc-link" :disabled="resendBusy" @click="resend(reg.email)">{{ tx('沒收到？重寄驗證信', 'Did not get it? Resend the verification email') }}</button></p>
          <p v-if="resendMessage">{{ resendMessage }}</p>
        </div>
        <form v-else class="tcrfc-form" novalidate @submit.prevent="onRegister">
          <fieldset>
            <legend>{{ tx('Email 註冊表單', 'Email registration form') }}</legend>
            <div class="form-grid">
              <div class="form-field">
                <label for="m-reg-name">{{ tx('姓名', 'Name') }}<span class="req" aria-hidden="true">*</span></label>
                <input id="m-reg-name" v-model="reg.name" type="text" name="name" required autocomplete="name" maxlength="60">
              </div>
              <div class="form-field">
                <label for="m-reg-phone">{{ tx('手機', 'Mobile phone') }}<span class="req" aria-hidden="true">*</span></label>
                <input id="m-reg-phone" v-model="reg.phone" type="tel" name="phone" required autocomplete="tel" maxlength="30">
              </div>
              <div class="form-field">
                <label for="m-reg-email">Email<span class="req" aria-hidden="true">*</span></label>
                <input id="m-reg-email" v-model="reg.email" type="email" name="email" required autocomplete="email" maxlength="200">
              </div>
              <div class="form-field">
                <label for="m-reg-password">{{ tx('設定密碼', 'Set a password') }}<span class="req" aria-hidden="true">*</span></label>
                <input id="m-reg-password" v-model="reg.password" type="password" name="password" required autocomplete="new-password" aria-describedby="m-reg-password-hint" maxlength="128">
                <p id="m-reg-password-hint" class="field-hint">{{ isEn ? MEMBER_PASSWORD_HINT_EN : MEMBER_PASSWORD_HINT }}</p>
              </div>
              <MemberAgeGuardian v-model:birth-on="reg.birthOn" v-model:guardian="regGuardian" id-prefix="m-reg" />
            </div>
          </fieldset>
          <div class="consent-block">
            <div class="checkbox-field">
              <input id="m-reg-consent" v-model="reg.consent" type="checkbox" name="consent" required>
              <label v-if="isEn" for="m-reg-consent">I have read and agree to the <a :href="lp('/zh/privacy/')">Privacy Policy</a> and <a :href="lp('/zh/member-terms/')">Membership Terms</a>, and I agree that {{ clubNameEn }} may use the personal data collected through this form to create my membership and provide related services. Members under 18 need their guardian's consent.<span class="req" aria-hidden="true">*</span></label>
              <label v-else for="m-reg-consent">我已閱讀並同意<a :href="lp('/zh/privacy/')">隱私權政策</a>與<a :href="lp('/zh/member-terms/')">會員條款</a>，並同意{{ clubAssets.nameZh }}依本表單蒐集之個人資料，用於會員身分建立與相關服務提供。未滿 18 歲須經監護人同意。<span class="req" aria-hidden="true">*</span></label>
            </div>
          </div>
          <p v-if="regError" class="mc-alert mc-alert--error" role="alert">{{ regError }}</p>
          <button class="btn btn--primary btn--block" type="submit" :disabled="regBusy">{{ regBusy ? tx('送出中…', 'Submitting…') : tx('建立會員', 'Create membership') }}</button>
        </form>
      </div>
    </div>
  </div>
</template>

<style>
/* member-tabs（沿用 4.2 .team-tabs 的頁籤模式，原在 pages/zh/member/index.vue） */
.member-tabs__list{ display:flex; gap:.5rem; border-bottom:2px solid var(--rule); margin-bottom:2.25rem; }
.member-tabs__tab{
  padding:.85rem 1.4rem; font-weight:800; font-size:.92rem; color:var(--muted);
  border-bottom:3px solid transparent; margin-bottom:-2px;
  transition:color var(--dur-fast) var(--ease), border-color var(--dur-fast) var(--ease);
}
.member-tabs__tab:hover{ color:var(--brand-aa); }
.member-tabs__tab[aria-selected="true"]{ color:var(--heading); border-bottom-color:var(--brand); }
.member-tabs__panel[hidden]{ display:none; }
.member-alt-login{ margin-top:1.25rem; }
.member-forgot{ margin-top:1rem; text-align:center; font-size:.85rem; }
.member-forgot a{ color:var(--brand-aa); text-decoration:underline; }
.channel-grid{ display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:1.5rem; margin-bottom:1rem; }
.channel-grid--2{ grid-template-columns:repeat(2,minmax(0,1fr)); }
@media (max-width:900px){ .channel-grid, .channel-grid--2{ grid-template-columns:1fr; } }
.channel-card{ --clip-fill:var(--paper); padding:1.75rem 1.5rem; }
.channel-card__num{ font-size:.72rem; font-weight:800; color:var(--muted); letter-spacing:.08em; margin-bottom:.5rem; }
.channel-card h3{ font-size:1.05rem; font-weight:800; color:var(--heading); margin-bottom:.6rem; }
.channel-card p{ font-size:.86rem; line-height:1.7; color:var(--text); }
</style>

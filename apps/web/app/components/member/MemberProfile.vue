<script setup lang="ts">
// app/components/member/MemberProfile.vue — 個人資料、安全設定、LINE 綁定、刪除帳號（主站 §3.14「會員資料」「LINE 綁定管理」）
//
// - Email 是登入鍵，**本期不提供修改**（規劃書沒寫換 Email 的流程）。
// - 變更密碼會撤銷其他裝置；LINE 註冊、尚未設定密碼者可直接設定第一組密碼。
// - 解除 LINE 綁定須至少保留一種登入方式：沒設定過密碼時後端回 409 `password_required`，引導先設定密碼。
// - 刪除帳號：欄位清除（保留會員編號與遮罩姓名）；會員卡作廢；**會籍與付款紀錄依稅法與會計法規保留**，要如實告知。
import { MEMBER_PASSWORD_HINT, MEMBER_PASSWORD_HINT_EN, passwordProblem, toMemberApiError } from '#shared/utils/member'
import type { MemberBrowserSession, MemberProfile } from '#shared/utils/member'

const emit = defineEmits<{ loggedOut: [] }>()

const { lp, isEn, tx } = useLocale()
const { authedFetch, member, setBrief, adopt, logoutAll, logout } = useMemberSession()
const line = useMemberLine()

const profile = ref<MemberProfile | null>(null)
const loadError = ref('')

const form = reactive({ name: '', phone: '', birthOn: '', locale: '' as '' | 'zh' | 'en' })
const saveBusy = ref(false)
const saveMessage = ref('')
const saveError = ref('')

async function load() {
  try {
    const p = await authedFetch<MemberProfile>('/api/backend/member/me')
    profile.value = p
    form.name = p.name
    form.phone = p.phone ?? ''
    form.birthOn = p.birthOn ?? ''
    form.locale = p.locale ?? ''
  }
  catch (err) { loadError.value = toMemberApiError(err, undefined, isEn.value).detail }
}
onMounted(load)

async function saveProfile() {
  saveMessage.value = ''
  saveError.value = ''
  if (!form.name.trim()) { saveError.value = tx('請輸入姓名。', 'Please enter your name.'); return }
  saveBusy.value = true
  try {
    // PUT 是整份取代：省略＝清除，所以空字串一律轉成 undefined（清除）
    await authedFetch('/api/backend/member/me', {
      method: 'PUT',
      body: {
        name: form.name.trim(),
        phone: form.phone.trim() || undefined,
        birthOn: form.birthOn || undefined,
        locale: form.locale || undefined,
      },
    })
    saveMessage.value = tx('已儲存個人資料。', 'Your profile has been saved.')
    if (member.value) setBrief({ ...member.value, name: form.name.trim() })
    await load()
  }
  catch (err) { saveError.value = toMemberApiError(err, undefined, isEn.value).detail }
  finally { saveBusy.value = false }
}

// ── 變更密碼 ──
const pw = reactive({ current: '', next: '', confirm: '' })
const pwBusy = ref(false)
const pwMessage = ref('')
const pwError = ref('')

async function changePassword() {
  pwMessage.value = ''
  pwError.value = ''
  const problem = passwordProblem(pw.next, isEn.value)
  if (problem) { pwError.value = problem; return }
  if (pw.next !== pw.confirm) { pwError.value = tx('兩次輸入的新密碼不一致。', 'The two new passwords do not match.'); return }
  if (profile.value?.hasPassword && !pw.current) { pwError.value = tx('請輸入目前的密碼。', 'Please enter your current password.'); return }
  pwBusy.value = true
  try {
    const session = await authedFetch<MemberBrowserSession>('/api/member-auth/change-password', {
      method: 'POST',
      body: { currentPassword: pw.current || undefined, newPassword: pw.next },
    })
    adopt(session)
    pwMessage.value = profile.value?.hasPassword ? tx('密碼已變更，其他裝置已全部登出。', 'Your password has been changed and all other devices have been signed out.') : tx('已設定密碼。', 'Your password has been set.')
    pw.current = pw.next = pw.confirm = ''
    await load()
  }
  catch (err) { pwError.value = toMemberApiError(err, undefined, isEn.value).detail }
  finally { pwBusy.value = false }
}

// ── LINE ──
const lineBusy = ref(false)
const lineMessage = ref('')
async function bindLine() {
  lineBusy.value = true
  lineMessage.value = ''
  const msg = await line.start('bind')
  lineBusy.value = false
  if (msg) lineMessage.value = msg
}
async function unbindLine() {
  if (!window.confirm(tx('確定要解除 LINE 綁定嗎？解除後將無法用 LINE 一鍵登入。', 'Unlink LINE? You will no longer be able to sign in with LINE in one step.'))) return
  lineBusy.value = true
  lineMessage.value = ''
  try {
    await authedFetch('/api/backend/member/me/line', { method: 'DELETE' })
    lineMessage.value = tx('已解除 LINE 綁定。', 'LINE has been unlinked.')
    await load()
  }
  catch (err) {
    const e = toMemberApiError(err, undefined, isEn.value)
    lineMessage.value = e.code === 'password_required' ? `${e.detail} ${tx('請先於上方「密碼」區設定密碼，再解除綁定。', 'Please set a password in the Password section above before unlinking.')}` : e.detail
  }
  finally { lineBusy.value = false }
}

// ── 全部登出 ──
const allBusy = ref(false)
const allError = ref('')
async function onLogoutAll() {
  if (!window.confirm(tx('確定要登出所有裝置嗎？包含目前這台，都需要重新登入。', 'Sign out of all devices? You will need to sign in again everywhere, including this one.'))) return
  allBusy.value = true
  allError.value = ''
  try {
    await logoutAll()
    emit('loggedOut')
  }
  catch (err) { allError.value = toMemberApiError(err, undefined, isEn.value).detail }
  finally { allBusy.value = false }
}

// ── 刪除帳號 ──
const delOpen = ref(false)
const delPassword = ref('')
const delConfirm = ref('')
const delBusy = ref(false)
const delError = ref('')
async function deleteAccount() {
  delError.value = ''
  const hasPw = profile.value?.hasPassword
  if (hasPw && !delPassword.value) { delError.value = tx('請輸入密碼以確認刪除。', 'Please enter your password to confirm deletion.'); return }
  if (!hasPw && delConfirm.value !== 'DELETE') { delError.value = tx('請輸入 DELETE 以確認刪除。', 'Please type DELETE to confirm deletion.'); return }
  if (!window.confirm(tx('帳號刪除後無法復原，會員卡會立即作廢。確定要刪除嗎？', 'Once deleted, your account cannot be recovered and your membership cards are voided immediately. Delete it?'))) return
  delBusy.value = true
  try {
    await authedFetch('/api/backend/member/me', { method: 'DELETE', body: hasPw ? { password: delPassword.value } : { confirm: 'DELETE' } })
    await logout()
    await navigateTo(lp('/zh/'))
  }
  catch (err) { delError.value = toMemberApiError(err, undefined, isEn.value).detail }
  finally { delBusy.value = false }
}
</script>

<template>
  <div class="mc-profile">
    <p v-if="loadError" class="mc-alert mc-alert--error" role="alert">{{ loadError }}</p>
    <p v-else-if="!profile" class="mc-empty">{{ tx('載入中…', 'Loading…') }}</p>

    <template v-else>
      <form class="tcrfc-form mc-section" novalidate @submit.prevent="saveProfile">
        <fieldset>
          <legend>{{ tx('個人資料', 'Profile') }}</legend>
          <div class="form-grid">
            <div class="form-field">
              <label for="mp-name">{{ tx('姓名', 'Name') }}<span class="req" aria-hidden="true">*</span></label>
              <input id="mp-name" v-model="form.name" type="text" maxlength="60" required autocomplete="name">
            </div>
            <div class="form-field">
              <label for="mp-email">Email</label>
              <input id="mp-email" type="email" :value="profile.email" readonly aria-describedby="mp-email-hint">
              <p id="mp-email-hint" class="field-hint">{{ tx('Email 是登入帳號，無法在此修改。', 'Your email address is your sign-in account and cannot be changed here.') }}{{ profile.emailVerified ? tx('（已驗證）', ' (verified)') : tx('（尚未驗證）', ' (not verified)') }}</p>
            </div>
            <div class="form-field">
              <label for="mp-phone">{{ tx('手機', 'Mobile phone') }}</label>
              <input id="mp-phone" v-model="form.phone" type="tel" maxlength="30" autocomplete="tel">
            </div>
            <div class="form-field">
              <label for="mp-birth">{{ tx('生日', 'Date of birth') }}</label>
              <input id="mp-birth" v-model="form.birthOn" type="date" autocomplete="bday">
            </div>
            <div class="form-field">
              <label for="mp-locale">{{ tx('語系偏好', 'Language preference') }}</label>
              <select id="mp-locale" v-model="form.locale">
                <option value="">{{ tx('未設定', 'Not set') }}</option>
                <option value="zh">{{ tx('繁體中文', 'Traditional Chinese') }}</option>
                <option value="en">English</option>
              </select>
            </div>
            <div class="form-field">
              <span class="mc-legend">{{ tx('會員編號', 'Member number') }}</span>
              <p class="mc-mono">{{ profile.memberNo }}</p>
              <p class="field-hint">{{ tx('註冊來源：', 'Signed up via: ') }}{{ profile.signupSourceLabel }}</p>
            </div>
          </div>
        </fieldset>
        <p v-if="saveError" class="mc-alert mc-alert--error" role="alert">{{ saveError }}</p>
        <p v-if="saveMessage" class="mc-alert mc-alert--ok" role="status">{{ saveMessage }}</p>
        <button type="submit" class="btn btn--primary btn--sm" :disabled="saveBusy">{{ saveBusy ? tx('儲存中…', 'Saving…') : tx('儲存個人資料', 'Save profile') }}</button>
      </form>

      <form class="tcrfc-form mc-section" novalidate @submit.prevent="changePassword">
        <fieldset>
          <legend>{{ profile.hasPassword ? tx('變更密碼', 'Change password') : tx('設定密碼', 'Set a password') }}</legend>
          <p v-if="!profile.hasPassword" class="mc-note">{{ tx('您是以 LINE 建立的帳號，尚未設定密碼。設定密碼後，也能用 Email＋密碼登入。', 'Your account was created with LINE and does not have a password yet. Once you set one, you can also sign in with your email and password.') }}</p>
          <div class="form-grid">
            <div v-if="profile.hasPassword" class="form-field form-field--full">
              <label for="mpw-current">{{ tx('目前的密碼', 'Current password') }}<span class="req" aria-hidden="true">*</span></label>
              <input id="mpw-current" v-model="pw.current" type="password" autocomplete="current-password" maxlength="128">
            </div>
            <div class="form-field">
              <label for="mpw-new">{{ tx('新密碼', 'New password') }}<span class="req" aria-hidden="true">*</span></label>
              <input id="mpw-new" v-model="pw.next" type="password" autocomplete="new-password" maxlength="128" aria-describedby="mpw-hint">
              <p id="mpw-hint" class="field-hint">{{ isEn ? MEMBER_PASSWORD_HINT_EN : MEMBER_PASSWORD_HINT }}</p>
            </div>
            <div class="form-field">
              <label for="mpw-confirm">{{ tx('再輸入一次新密碼', 'Re-enter new password') }}<span class="req" aria-hidden="true">*</span></label>
              <input id="mpw-confirm" v-model="pw.confirm" type="password" autocomplete="new-password" maxlength="128">
            </div>
          </div>
        </fieldset>
        <p v-if="pwError" class="mc-alert mc-alert--error" role="alert">{{ pwError }}</p>
        <p v-if="pwMessage" class="mc-alert mc-alert--ok" role="status">{{ pwMessage }}</p>
        <button type="submit" class="btn btn--primary btn--sm" :disabled="pwBusy">{{ pwBusy ? tx('處理中…', 'Processing…') : (profile.hasPassword ? tx('變更密碼', 'Change password') : tx('設定密碼', 'Set a password')) }}</button>
      </form>

      <section class="mc-section" aria-labelledby="mp-line-title">
        <h3 id="mp-line-title" class="mc-h3">{{ tx('LINE 綁定', 'LINE link') }}</h3>
        <p class="mc-note">{{ profile.lineBound ? tx('已綁定 LINE，可用 LINE 一鍵登入。', 'LINE is linked, so you can sign in with LINE in one step.') : tx('尚未綁定 LINE。綁定後可用 LINE 一鍵登入。', 'LINE is not linked yet. Once linked, you can sign in with LINE in one step.') }}{{ tx('須至少保留一種登入方式。', ' You must keep at least one way to sign in.') }}</p>
        <button v-if="!profile.lineBound" type="button" class="btn btn--dark btn--sm" :disabled="lineBusy" @click="bindLine">{{ tx('綁定 LINE', 'Link LINE') }}</button>
        <button v-else type="button" class="btn btn--dark btn--sm" :disabled="lineBusy" @click="unbindLine">{{ tx('解除 LINE 綁定', 'Unlink LINE') }}</button>
        <p v-if="lineMessage" class="mc-alert mc-alert--info" role="status">{{ lineMessage }}</p>
      </section>

      <section class="mc-section" aria-labelledby="mp-devices-title">
        <h3 id="mp-devices-title" class="mc-h3">{{ tx('登入裝置', 'Signed-in devices') }}</h3>
        <p class="mc-note">{{ tx('如果懷疑帳號被他人使用，可以登出所有裝置（含目前這台）。', 'If you suspect someone else is using your account, you can sign out of all devices, including this one.') }}</p>
        <button type="button" class="btn btn--dark btn--sm" :disabled="allBusy" @click="onLogoutAll">{{ tx('登出所有裝置', 'Sign out of all devices') }}</button>
        <p v-if="allError" class="mc-alert mc-alert--error" role="alert">{{ allError }}</p>
      </section>

      <section class="mc-section mc-danger" aria-labelledby="mp-del-title">
        <h3 id="mp-del-title" class="mc-h3">{{ tx('刪除帳號', 'Delete account') }}</h3>
        <p class="mc-note">
          <template v-if="isEn">
            After deletion, your name, email, phone number, date of birth and LINE link are cleared, all digital membership cards are voided immediately, any unfinished upgrade requests are cancelled, and this <strong>cannot be undone</strong>.
            Under tax and accounting regulations, <strong>membership and payment records that have already been created are kept</strong> (without personally identifying data).
            For more details, see the <a :href="lp('/zh/privacy/')">Privacy Policy</a>.
          </template>
          <template v-else>
            刪除後，您的姓名、Email、電話、生日與 LINE 綁定會被清除，所有電子會員卡立即作廢，未完成的升級申請會取消，且<strong>無法復原</strong>。
            依稅法與會計法規，已成立的<strong>會籍與付款紀錄會保留</strong>（不含可識別身分的個資）。
            更多說明請見<a :href="lp('/zh/privacy/')">隱私權政策</a>。
          </template>
        </p>
        <button v-if="!delOpen" type="button" class="btn btn--dark btn--sm" @click="delOpen = true">{{ tx('我要刪除帳號', 'Delete my account') }}</button>
        <form v-else class="mc-delform" novalidate @submit.prevent="deleteAccount">
          <div v-if="profile.hasPassword" class="form-field">
            <label for="md-pw">{{ tx('輸入密碼以確認', 'Enter your password to confirm') }}</label>
            <input id="md-pw" v-model="delPassword" type="password" autocomplete="current-password" maxlength="128">
          </div>
          <div v-else class="form-field">
            <label for="md-confirm">{{ tx('輸入 DELETE 以確認（LINE 帳號尚未設定密碼）', 'Type DELETE to confirm (your LINE account does not have a password yet)') }}</label>
            <input id="md-confirm" v-model="delConfirm" type="text" autocomplete="off" maxlength="10">
          </div>
          <p v-if="delError" class="mc-alert mc-alert--error" role="alert">{{ delError }}</p>
          <div class="mc-actions">
            <button type="submit" class="btn btn--primary btn--sm" :disabled="delBusy">{{ delBusy ? tx('處理中…', 'Processing…') : tx('確認刪除帳號', 'Confirm account deletion') }}</button>
            <button type="button" class="btn btn--dark btn--sm" :disabled="delBusy" @click="delOpen = false">{{ tx('取消', 'Cancel') }}</button>
          </div>
        </form>
      </section>
    </template>
  </div>
</template>

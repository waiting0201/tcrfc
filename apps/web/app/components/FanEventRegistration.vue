<script setup lang="ts">
// app/components/culture/FanEventRegistration.vue — 8.2 球迷會活動報名／取消報名（S3-2）
//
// 對應 `POST {club}/fan-events/{slug}/registrations`、`DELETE …/registrations/me`（apps/api README E 批）：
//   - **會員**（帶存取權杖）只需備註，報名記到會員、不另存個資；**非會員**必須有姓名與（手機或 Email）。
//   - **限付費會員的活動**：沒登入 → 引導登入（回來後回到這一頁）；登入但沒有有效球迷會員會籍 → 後端 403
//     `fan_club_required`，顯示後端說明並導向加入／升級。
//   - 名額已滿自動進候補（`isFull` 時按鈕寫「登記候補」）；重複報名、截止、已開始由後端以 409 回覆，顯示其說明。
//   - 取消報名：已報名或候補皆可取消；**取消已報名者不會自動遞補候補者**（客服人工處理，同後台 F2）。
// 🔴 回應不含任何個資；非會員的姓名／電話／Email 只送出、不存任何瀏覽器儲存空間。
import { formatTaipeiDateTime, toMemberApiError } from '#shared/utils/member'
import type { FanEvent, FanEventDetail } from '#shared/utils/member'

const props = defineProps<{ event: FanEvent, initialMy: FanEventDetail['myRegistration'] }>()

const config = useRuntimeConfig()
const club = config.public.club
const route = useRoute()
const { locale, lp, isEn, tx } = useLocale()
const { isLoggedIn, restored, restore, authedFetch } = useMemberSession()

const my = ref<FanEventDetail['myRegistration']>(props.initialMy)
const loginHref = computed(() => `${lp('/zh/member/')}?next=${encodeURIComponent(route.path)}`)

onMounted(async () => {
  // 取得登入狀態後重抓詳情，才知道這位會員自己的報名狀態（匿名的 SSR 輸出永遠是 null）
  if (await restore()) await refreshMy()
})

async function refreshMy() {
  try {
    const d = await authedFetch<FanEventDetail>(`/api/backend/${club}/fan-events/${props.event.slug}`, { query: { lang: locale.value } })
    my.value = d.myRegistration
  }
  catch { /* 查不到就維持現狀，不阻擋報名 */ }
}

const form = reactive({ applicantName: '', phone: '', email: '', note: '', consent: false })
const busy = ref(false)
const error = ref('')
const needFanClub = ref(false)
const result = ref<{ status: string, statusLabel: string, isWaitlisted: boolean } | null>(null)

/** 報名狀態文字：英文版依狀態代碼翻譯（POST 回應沒帶語系，statusLabel 會是繁中）；其他狀態沿用後端文字。 */
const EN_STATUS: Record<string, string> = { registered: 'Registered', waitlist: 'Waitlisted', waitlisted: 'Waitlisted', attended: 'Attended', cancelled: 'Cancelled' }
function statusText(status: string, label: string): string {
  return isEn.value ? (EN_STATUS[status] ?? label) : label
}
/** 後端錯誤訊息為繁中；英文版對已知代碼改用英文說明，其餘退回通用英文訊息。 */
function errorText(code: string | undefined, detail: string): string {
  if (!isEn.value) return detail
  if (code === 'fan_club_required') return 'This event is for paid Fan Club members only. Please upgrade your membership in the Member Centre.'
  if (code === 'login_required') return 'Please log in to register for this event.'
  return /[\u3400-\u9fff]/.test(detail)
    ? 'We could not complete your registration. Please check the details and try again, or contact us.'
    : detail
}

const closed = computed(() => !props.event.isRegistrationOpen)
const paidOnlyBlocked = computed(() => props.event.isPaidMembersOnly && restored.value && !isLoggedIn.value)

async function submit() {
  error.value = ''
  needFanClub.value = false
  const url = `/api/backend/${club}/fan-events/${props.event.slug}/registrations`
  const loggedIn = isLoggedIn.value
  if (!loggedIn) {
    if (!form.applicantName.trim()) { error.value = tx('請輸入姓名。', 'Please enter your name.'); return }
    if (!form.phone.trim() && !form.email.trim()) { error.value = tx('請至少填寫手機或 Email，方便活動聯繫。', 'Please enter at least a mobile number or an email address so we can contact you about the event.'); return }
    if (!form.consent) { error.value = tx('請先閱讀並勾選同意隱私權政策。', 'Please read the Privacy Policy and tick the box to agree.'); return }
  }
  busy.value = true
  try {
    const body = loggedIn
      ? { note: form.note.trim() || undefined }
      : { applicantName: form.applicantName.trim(), phone: form.phone.trim() || undefined, email: form.email.trim() || undefined, note: form.note.trim() || undefined }
    const out = loggedIn
      ? await authedFetch<NonNullable<typeof result.value>>(url, { method: 'POST', body })
      : await $fetch<NonNullable<typeof result.value>>(url, { method: 'POST', body })
    result.value = out
    my.value = loggedIn ? { status: out.status, statusLabel: out.statusLabel } : null
    form.note = ''
    form.phone = form.email = form.applicantName = ''
    form.consent = false
  }
  catch (err) {
    const e = toMemberApiError(err)
    error.value = errorText(e.code, e.detail)
    needFanClub.value = e.code === 'fan_club_required'
    if (e.code === 'login_required') needFanClub.value = false
  }
  finally { busy.value = false }
}

async function cancel() {
  if (!window.confirm(tx('確定要取消報名嗎？', 'Are you sure you want to cancel your registration?'))) return
  busy.value = true
  error.value = ''
  try {
    await authedFetch(`/api/backend/${club}/fan-events/${props.event.slug}/registrations/me`, { method: 'DELETE' })
    my.value = null
    result.value = null
  }
  catch (err) { const e = toMemberApiError(err); error.value = errorText(e.code, e.detail) }
  finally { busy.value = false }
}
</script>

<template>
  <div class="fe-reg">
    <p v-if="event.registrationDeadlineAt" class="mc-note">{{ tx('報名截止：', 'Registration deadline: ') }}{{ formatTaipeiDateTime(event.registrationDeadlineAt, locale) }}</p>

    <div v-if="my" class="mc-alert mc-alert--ok" role="status">
      <p><template v-if="isEn">Your registration status: <strong>{{ statusText(my.status, my.statusLabel) }}</strong></template><template v-else>您的報名狀態：<strong>{{ my.statusLabel }}</strong></template></p>
      <p><button type="button" class="mc-link" :disabled="busy" @click="cancel">{{ tx('取消報名', 'Cancel registration') }}</button></p>
    </div>

    <template v-else>
      <div v-if="result && !isLoggedIn" class="mc-alert mc-alert--ok" role="status">
        <p><strong>{{ isEn ? statusText(result.status, result.statusLabel) + '.' : result.statusLabel + '。' }}</strong>{{ result.isWaitlisted ? tx('目前名額已滿，您已列入候補，若有名額會與您聯繫。', 'The event is currently full and you have been added to the waitlist. We will contact you if a place becomes available.') : tx('活動相關訊息將以您留的聯絡方式通知。', 'We will send event updates using the contact details you provided.') }}</p>
      </div>

      <p v-else-if="closed" class="mc-alert mc-alert--info" role="status">{{ tx('此活動目前不開放報名（已截止或已開始）。', 'Registration for this event is not open at the moment (the deadline has passed or the event has started).') }}</p>

      <div v-else-if="paidOnlyBlocked" class="mc-alert mc-alert--info" role="status">
        <p><template v-if="isEn">This event is open to <strong>paid Fan Club members</strong> only. Please log in to your member account first.</template><template v-else>此活動<strong>限付費球迷會員</strong>報名。請先登入會員帳號。</template></p>
        <p><a class="btn btn--primary btn--sm" :href="loginHref">{{ tx('登入會員', 'Log in') }}</a>　<a class="btn btn--dark btn--sm" :href="lp('/zh/culture/fan-club/')">{{ tx('了解球迷會', 'About the Fan Club') }}</a></p>
      </div>

      <form v-else class="tcrfc-form fe-reg__form" novalidate @submit.prevent="submit">
        <fieldset>
          <legend>{{ event.isFull ? tx('登記候補', 'Join the waitlist') : tx('活動報名', 'Event registration') }}</legend>
          <p v-if="event.isFull" class="mc-note">{{ tx('目前名額已滿，送出後會列入候補，若有名額會與您聯繫。', 'The event is currently full. If you submit, you will be added to the waitlist and we will contact you if a place becomes available.') }}</p>
          <p v-if="event.isPaidMembersOnly" class="mc-note">{{ tx('此活動限付費球迷會員；系統會核對您的會籍。', 'This event is for paid Fan Club members only. We will check your membership.') }}</p>
          <div class="form-grid">
            <template v-if="!isLoggedIn">
              <div class="form-field">
                <label for="fe-name">{{ tx('姓名', 'Name') }}<span class="req" aria-hidden="true">*</span></label>
                <input id="fe-name" v-model="form.applicantName" type="text" maxlength="60" autocomplete="name">
              </div>
              <div class="form-field">
                <label for="fe-phone">{{ tx('手機', 'Mobile number') }}</label>
                <input id="fe-phone" v-model="form.phone" type="tel" maxlength="30" autocomplete="tel">
              </div>
              <div class="form-field">
                <label for="fe-email">Email</label>
                <input id="fe-email" v-model="form.email" type="email" maxlength="200" autocomplete="email">
                <p class="field-hint">{{ tx('手機與 Email 至少填一項。', 'Please provide at least a mobile number or an email address.') }}</p>
              </div>
            </template>
            <p v-else class="mc-note form-field--full"><template v-if="isEn">You are registering as a member, so you do not need to fill in your personal details again. <a :href="lp('/zh/member/')">Member Centre</a></template><template v-else>以會員身分報名，不需再填個人資料。<a :href="lp('/zh/member/')">會員中心</a></template></p>
            <div class="form-field form-field--full">
              <label for="fe-note">{{ tx('備註', 'Notes') }}</label>
              <textarea id="fe-note" v-model="form.note" maxlength="500" rows="3" />
            </div>
          </div>
        </fieldset>
        <div v-if="!isLoggedIn" class="consent-block">
          <p v-if="isEn" class="field-hint">The consent notice below is pending legal review and is shown in Traditional Chinese.</p>
          <div class="checkbox-field">
            <input id="fe-consent" v-model="form.consent" type="checkbox">
            <label for="fe-consent">我已閱讀並同意<a :href="lp('/zh/privacy/')">隱私權政策</a>，並同意主辦單位依本表單蒐集之個人資料，用於活動報名與聯繫。<span class="req" aria-hidden="true">*</span></label>
          </div>
        </div>
        <div v-if="error" class="mc-alert mc-alert--error" role="alert">
          <p>{{ error }}</p>
          <p v-if="needFanClub"><a href="#" @click.prevent="navigateTo(lp('/zh/member/'))">{{ tx('前往會員中心升級球迷會員', 'Go to the Member Centre to upgrade to a Fan Club membership') }}</a></p>
        </div>
        <button type="submit" class="btn btn--primary" :disabled="busy">{{ busy ? tx('送出中…', 'Submitting...') : (event.isFull ? tx('登記候補', 'Join the waitlist') : tx('送出報名', 'Submit registration')) }}</button>
      </form>
    </template>
    <p v-if="error && my" class="mc-alert mc-alert--error" role="alert">{{ error }}</p>
  </div>
</template>

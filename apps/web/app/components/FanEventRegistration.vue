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
const { locale, lp } = useLocale()
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

const closed = computed(() => !props.event.isRegistrationOpen)
const paidOnlyBlocked = computed(() => props.event.isPaidMembersOnly && restored.value && !isLoggedIn.value)

async function submit() {
  error.value = ''
  needFanClub.value = false
  const url = `/api/backend/${club}/fan-events/${props.event.slug}/registrations`
  const loggedIn = isLoggedIn.value
  if (!loggedIn) {
    if (!form.applicantName.trim()) { error.value = '請輸入姓名。'; return }
    if (!form.phone.trim() && !form.email.trim()) { error.value = '請至少填寫手機或 Email，方便活動聯繫。'; return }
    if (!form.consent) { error.value = '請先閱讀並勾選同意隱私權政策。'; return }
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
    error.value = e.detail
    needFanClub.value = e.code === 'fan_club_required'
    if (e.code === 'login_required') needFanClub.value = false
  }
  finally { busy.value = false }
}

async function cancel() {
  if (!window.confirm('確定要取消報名嗎？')) return
  busy.value = true
  error.value = ''
  try {
    await authedFetch(`/api/backend/${club}/fan-events/${props.event.slug}/registrations/me`, { method: 'DELETE' })
    my.value = null
    result.value = null
  }
  catch (err) { error.value = toMemberApiError(err).detail }
  finally { busy.value = false }
}
</script>

<template>
  <div class="fe-reg">
    <p v-if="event.registrationDeadlineAt" class="mc-note">報名截止：{{ formatTaipeiDateTime(event.registrationDeadlineAt, locale) }}</p>

    <div v-if="my" class="mc-alert mc-alert--ok" role="status">
      <p>您的報名狀態：<strong>{{ my.statusLabel }}</strong></p>
      <p><button type="button" class="mc-link" :disabled="busy" @click="cancel">取消報名</button></p>
    </div>

    <template v-else>
      <div v-if="result && !isLoggedIn" class="mc-alert mc-alert--ok" role="status">
        <p><strong>{{ result.statusLabel }}。</strong>{{ result.isWaitlisted ? '目前名額已滿，您已列入候補，若有名額會與您聯繫。' : '活動相關訊息將以您留的聯絡方式通知。' }}</p>
      </div>

      <p v-else-if="closed" class="mc-alert mc-alert--info" role="status">此活動目前不開放報名（已截止或已開始）。</p>

      <div v-else-if="paidOnlyBlocked" class="mc-alert mc-alert--info" role="status">
        <p>此活動<strong>限付費球迷會員</strong>報名。請先登入會員帳號。</p>
        <p><a class="btn btn--primary btn--sm" :href="loginHref">登入會員</a>　<a class="btn btn--dark btn--sm" :href="lp('/zh/culture/fan-club/')">了解球迷會</a></p>
      </div>

      <form v-else class="tcrfc-form fe-reg__form" novalidate @submit.prevent="submit">
        <fieldset>
          <legend>{{ event.isFull ? '登記候補' : '活動報名' }}</legend>
          <p v-if="event.isFull" class="mc-note">目前名額已滿，送出後會列入候補，若有名額會與您聯繫。</p>
          <p v-if="event.isPaidMembersOnly" class="mc-note">此活動限付費球迷會員；系統會核對您的會籍。</p>
          <div class="form-grid">
            <template v-if="!isLoggedIn">
              <div class="form-field">
                <label for="fe-name">姓名<span class="req" aria-hidden="true">*</span></label>
                <input id="fe-name" v-model="form.applicantName" type="text" maxlength="60" autocomplete="name">
              </div>
              <div class="form-field">
                <label for="fe-phone">手機</label>
                <input id="fe-phone" v-model="form.phone" type="tel" maxlength="30" autocomplete="tel">
              </div>
              <div class="form-field">
                <label for="fe-email">Email</label>
                <input id="fe-email" v-model="form.email" type="email" maxlength="200" autocomplete="email">
                <p class="field-hint">手機與 Email 至少填一項。</p>
              </div>
            </template>
            <p v-else class="mc-note form-field--full">以會員身分報名，不需再填個人資料。<a :href="lp('/zh/member/')">會員中心</a></p>
            <div class="form-field form-field--full">
              <label for="fe-note">備註</label>
              <textarea id="fe-note" v-model="form.note" maxlength="500" rows="3" />
            </div>
          </div>
        </fieldset>
        <div v-if="!isLoggedIn" class="consent-block">
          <div class="checkbox-field">
            <input id="fe-consent" v-model="form.consent" type="checkbox">
            <label for="fe-consent">我已閱讀並同意<a :href="lp('/zh/privacy/')">隱私權政策</a>，並同意主辦單位依本表單蒐集之個人資料，用於活動報名與聯繫。<span class="req" aria-hidden="true">*</span></label>
          </div>
        </div>
        <div v-if="error" class="mc-alert mc-alert--error" role="alert">
          <p>{{ error }}</p>
          <p v-if="needFanClub"><a href="#" @click.prevent="navigateTo(lp('/zh/member/'))">前往會員中心升級球迷會員</a></p>
        </div>
        <button type="submit" class="btn btn--primary" :disabled="busy">{{ busy ? '送出中…' : (event.isFull ? '登記候補' : '送出報名') }}</button>
      </form>
    </template>
    <p v-if="error && my" class="mc-alert mc-alert--error" role="alert">{{ error }}</p>
  </div>
</template>

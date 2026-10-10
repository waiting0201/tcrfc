<script setup lang="ts">
// app/components/TrialSchedule.vue — 3.3 球員機會「試訓場次列表 ＋ 線上報名」（P4，H 批，2026-10-02）
//
// 規格：主站規劃書 §3.3「試訓場次列表（日期／地點／對象／名額／報名截止／狀態）＋ 線上報名」，後台 P4。
// 對應 apps/api `POST /api/v1/{club}/trials/{trialId}/registrations`（apps/api/README.md「H 批」§2；
// 經 BFF 代理白名單轉發，不帶任何認證——網頁前台不做報名歸戶，主站規劃書 §3.14）。
//
// 規則（逐條對應後端契約）：
//   - 電話與 Email 至少填一項；未滿 18 歲家長姓名與電話必填（有填出生日期才判斷，沒填由後端把關）。
//   - 回 `待確認`（已佔名額，佔滿時場次自動轉「額滿」）或 `候補`；400 驗證／409 已結束、已過截止日、重複報名
//     的訊息是後端寫給使用者的日常中文，直接顯示（BFF 已把上游 4xx 訊息轉出，E-131）。
//   - 🔴 健康聲明（B-9，特種個資待法務確認）：同課程報名，只收**一個勾選同意**，不開自由文字欄位。
//   - ⚠️ 後端目前**不寄**報名通知信，成功畫面因此只告知「請記下報名編號、我們會與您聯繫」，不宣稱已寄信。
import type { PublicTrial } from '~/composables/usePublicTrials'

const props = defineProps<{ trials: PublicTrial[] }>()

const HEALTH_DECLARATION_MARKER = '已勾選同意健康聲明（未蒐集健康細節）'

const config = useRuntimeConfig()
const club = config.public.club
const { lp, locale, isEn, tx } = useLocale()
const clubAssets = getClubAssets(club)

/** 可報名（開放或額滿候補）的場次。 */
const registrable = computed(() => props.trials.filter((t) => t.isSignupOpen || t.acceptsWaitlist))

function dateText(d: string | null): string {
  return d ? d.replaceAll('-', '/') : '—'
}
function quotaText(t: PublicTrial): string {
  if (t.capacity == null) return isEn.value ? `${t.enrolledCount} registered (no limit on places)` : `已報名 ${t.enrolledCount} 人（名額不限）`
  const left = Math.max(t.capacity - t.enrolledCount, 0)
  if (isEn.value) return left > 0 ? `${left} places left (${t.capacity} in total)` : `Full (${t.capacity} places in total)`
  return left > 0 ? `剩餘 ${left} 名（共 ${t.capacity} 名）` : `名額已滿（共 ${t.capacity} 名）`
}
function statusText(t: PublicTrial): string {
  if (t.isSignupOpen) return tx('開放報名', 'Open for registration')
  if (t.acceptsWaitlist) return tx('額滿候補', 'Full, waitlist open')
  return statusLabel(t, locale.value)
}

// ── 表單狀態 ──
const trialId = ref('')
watch(registrable, (list) => {
  if (!list.some((t) => t.id === trialId.value)) trialId.value = list[0]?.id ?? ''
}, { immediate: true })
const currentTrial = computed(() => registrable.value.find((t) => t.id === trialId.value) ?? null)

const applicantName = ref('')
const birthOn = ref('')
const phone = ref('')
const email = ref('')
const guardianName = ref('')
const guardianPhone = ref('')
const note = ref('')
const healthConsent = ref(false)
const privacyConsent = ref(false)
const website = ref('')

type Phase = 'idle' | 'submitting' | 'success' | 'error'
const phase = ref<Phase>('idle')
const errorMessage = ref('')
const result = ref<{ registrationNo: string, status: string, statusCode?: string, statusLabelZh?: string, statusLabelEn?: string, trialLabel: string } | null>(null)

const GENERIC_ERROR = computed(() => tx('送出失敗，請確認各欄位已正確填寫後再試一次；若持續發生，請改用電話或 Email 聯繫我們。', 'Submission failed. Please check that every field is filled in correctly and try again; if it keeps happening, please contact us by phone or email instead.'))

function trialLabel(t: PublicTrial): string {
  return [dateText(t.trialOn), t.venueName, t.teamName].filter(Boolean).join(isEn.value ? ' · ' : '　')
}

/** 依出生日期（YYYY-MM-DD）算是否未滿 18 歲；沒填或格式不對回 null（交給後端判斷）。 */
function isMinor(dateText: string): boolean | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(dateText)
  if (!m) return null
  const today = new Date()
  let age = today.getFullYear() - Number(m[1])
  const beforeBirthday = today.getMonth() + 1 < Number(m[2]) || (today.getMonth() + 1 === Number(m[2]) && today.getDate() < Number(m[3]))
  if (beforeBirthday) age -= 1
  return age < 18
}

function extractMessage(err: unknown): string | null {
  const msg = extractErrorMessage(err, isEn.value)
  if (msg) return msg
  const status = (err as { statusCode?: number, status?: number } | null)?.statusCode ?? (err as { status?: number } | null)?.status
  if (status === 429) return tx('送出次數過多，請稍候幾分鐘再試。', 'Too many submissions. Please wait a few minutes and try again.')
  return null
}

function chooseTrial(id: string) {
  trialId.value = id
  if (import.meta.client) document.getElementById('trial-register')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

async function onSubmit() {
  if (phase.value === 'submitting' || !currentTrial.value) return
  errorMessage.value = ''
  if (!phone.value.trim() && !email.value.trim()) {
    phase.value = 'error'
    errorMessage.value = tx('聯絡電話與 Email 至少需要填寫一項，以便與您聯繫。', 'Please fill in at least one of phone number or email so we can contact you.')
    return
  }
  if (isMinor(birthOn.value) && (!guardianName.value.trim() || !guardianPhone.value.trim())) {
    phase.value = 'error'
    errorMessage.value = tx('未滿 18 歲的報名者，請填寫家長／緊急聯絡人的姓名與電話。', 'Registrants under 18 must provide the name and phone number of a parent or emergency contact.')
    return
  }
  if (!privacyConsent.value) {
    phase.value = 'error'
    errorMessage.value = tx('請先閱讀並勾選同意隱私權政策，才能送出。', 'Please read and agree to the Privacy Policy before submitting.')
    return
  }
  // 誘捕欄位有值＝機器人：安靜當作成功，不送出（端點本身另有依 IP 的限流）。
  if (website.value) { phase.value = 'success'; result.value = null; return }

  phase.value = 'submitting'
  const trial = currentTrial.value
  try {
    const res = await $fetch<{ registrationNo: string, status: string, statusCode?: string, statusLabelZh?: string, statusLabelEn?: string }>(
      `/api/backend/${club}/trials/${trial.id}/registrations`,
      {
        method: 'POST',
        body: {
          applicantName: applicantName.value.trim(),
          phone: phone.value.trim() || undefined,
          email: email.value.trim() || undefined,
          birthOn: birthOn.value || undefined,
          guardianName: guardianName.value.trim() || undefined,
          guardianPhone: guardianPhone.value.trim() || undefined,
          healthDeclaration: healthConsent.value ? HEALTH_DECLARATION_MARKER : undefined,
          privacyConsent: true,
          note: note.value.trim() || undefined,
        },
      },
    )
    result.value = { ...res, trialLabel: trialLabel(trial) }
    phase.value = 'success'
  }
  catch (err: unknown) {
    phase.value = 'error'
    errorMessage.value = extractMessage(err) ?? GENERIC_ERROR.value
  }
}

function registerAnother() {
  applicantName.value = ''
  birthOn.value = ''
  note.value = ''
  healthConsent.value = false
  privacyConsent.value = false
  result.value = null
  phase.value = 'idle'
  errorMessage.value = ''
}
</script>

<template>
<div class="trial-schedule">
  <div class="table-wrap">
    <table class="trial-table">
      <caption class="visually-hidden">{{ tx('試訓場次列表', 'Trial sessions') }}</caption>
      <thead>
        <tr>
          <th scope="col">{{ tx('日期', 'Date') }}</th>
          <th scope="col">{{ tx('地點', 'Venue') }}</th>
          <th scope="col">{{ tx('對象', 'Who it is for') }}</th>
          <th scope="col">{{ tx('名額', 'Places') }}</th>
          <th scope="col">{{ tx('報名截止', 'Registration deadline') }}</th>
          <th scope="col">{{ tx('狀態', 'Status') }}</th>
          <th scope="col"><span class="visually-hidden">{{ tx('報名', 'Register') }}</span></th>
        </tr>
      </thead>
      <tbody>
        <tr v-if="trials.length === 0" class="trial-table__pending-row">
          <td colspan="7">{{ tx('目前尚無公告中的試訓場次，請關注官方社群公告。', 'There are no trial sessions announced at the moment. Please follow our official social media channels for announcements.') }}</td>
        </tr>
        <tr v-for="t in trials" :key="t.id">
          <td>{{ dateText(t.trialOn) }}</td>
          <td>
            {{ t.venueName ?? tx('地點待公告', 'Venue to be announced') }}
            <span v-if="t.venueAddress" class="trial-table__sub">{{ t.venueAddress }}</span>
          </td>
          <td>{{ [t.teamName, t.audience].filter(Boolean).join(isEn ? ' · ' : '　') || '—' }}</td>
          <td>{{ quotaText(t) }}</td>
          <td>{{ t.deadlineOn ? dateText(t.deadlineOn) : '—' }}</td>
          <td><span class="trial-table__status">{{ statusText(t) }}</span></td>
          <td>
            <button v-if="t.isSignupOpen || t.acceptsWaitlist" type="button" class="btn btn--dark btn--sm" @click="chooseTrial(t.id)">{{ t.isSignupOpen ? tx('我要報名', 'Register') : tx('候補報名', 'Join waitlist') }}</button>
          </td>
        </tr>
      </tbody>
    </table>
  </div>

  <div v-if="registrable.length > 0" id="trial-register" class="trial-reg">
    <h3 class="trial-reg__title">{{ tx('試訓線上報名', 'Trial online registration') }}</h3>
    <p class="section-lede">{{ tx('選擇場次並填寫資料後送出，系統會產生報名編號。名額已滿的場次會列入候補，有空位時依序聯繫。', 'Choose a session, fill in your details and submit, and the system will give you a registration number. Sessions that are full will place you on a waitlist, and we will contact people in order when a place opens up.') }}</p>

    <div v-if="phase === 'success'" class="form-status form-status--success" role="status">
      <template v-if="result">
        <p v-if="isEn"><strong>Your registration has been submitted.</strong> Your registration number is <strong class="trial-reg__no">{{ result.registrationNo }}</strong>, current status: {{ statusLabel(result, locale) }}.</p>
        <p v-else><strong>報名資料已送出。</strong>您的報名編號是 <strong class="trial-reg__no">{{ result.registrationNo }}</strong>，目前狀態：{{ statusLabel(result, locale) }}。</p>
        <p v-if="result.statusCode === 'waitlisted'">{{ tx('這場試訓目前名額已滿，您已列入候補，有空位時我們會依序與您聯繫。', 'This trial session is currently full and you have been placed on the waitlist. We will contact people in order when a place opens up.') }}</p>
        <p v-else>{{ tx('我們會依您留下的聯絡方式與您確認試訓細節。請記下報名編號，查詢時使用。', 'We will confirm the trial details using the contact details you left. Please note down your registration number to use when you enquire.') }}</p>
        <p class="trial-reg__summary">{{ result.trialLabel }}</p>
      </template>
      <p v-else>{{ tx('已收到您的報名資料。', 'We have received your registration.') }}</p>
      <p><button type="button" class="btn btn--light btn--sm" @click="registerAnother">{{ tx('為另一位報名', 'Register someone else') }}</button></p>
    </div>

    <form v-else class="tcrfc-form trial-reg__form" method="post" @submit.prevent="onSubmit">
      <HoneypotField v-model="website" />

      <div v-if="phase === 'error'" class="form-status form-status--error" role="alert"><p>{{ errorMessage }}</p></div>

      <fieldset>
        <legend>{{ tx('試訓場次', 'Trial session') }}</legend>
        <div class="form-grid">
          <div class="form-field form-field--full">
            <span id="tr-trial-label" class="trial-reg__legend">{{ tx('場次', 'Session') }}<span class="req" aria-hidden="true">*</span></span>
            <ul class="trial-reg__list" role="radiogroup" aria-labelledby="tr-trial-label">
              <li v-for="t in registrable" :key="t.id">
                <label :class="['trial-reg__option', { 'is-selected': trialId === t.id }]">
                  <input v-model="trialId" type="radio" name="trial" :value="t.id" required>
                  <span class="trial-reg__option-main">
                    <strong>{{ dateText(t.trialOn) }}</strong>
                    <span v-if="t.venueName">{{ t.venueName }}</span>
                    <span v-if="t.teamName || t.audience">{{ [t.teamName, t.audience].filter(Boolean).join(isEn ? ' · ' : '　') }}</span>
                  </span>
                  <span class="trial-reg__option-meta">
                    <span class="trial-reg__badge">{{ statusText(t) }}</span>
                    <span>{{ quotaText(t) }}</span>
                    <span v-if="t.deadlineOn">{{ tx('截止', 'Deadline') }} {{ dateText(t.deadlineOn) }}</span>
                  </span>
                </label>
              </li>
            </ul>
            <p v-if="currentTrial && !currentTrial.isSignupOpen && currentTrial.acceptsWaitlist" class="field-hint">{{ tx('這場試訓目前名額已滿，送出後會列入候補，有空位時依序聯繫。', 'This trial session is currently full. If you submit, you will be placed on the waitlist and contacted in order when a place opens up.') }}</p>
          </div>
        </div>
      </fieldset>

      <fieldset>
        <legend>{{ tx('報名者資料', 'Registrant details') }}</legend>
        <div class="form-grid">
          <div class="form-field">
            <label for="tr-name">{{ tx('姓名', 'Name') }}<span class="req" aria-hidden="true">*</span></label>
            <input id="tr-name" v-model="applicantName" type="text" name="applicant_name" required autocomplete="name" maxlength="100">
          </div>
          <div class="form-field">
            <label for="tr-dob">{{ tx('出生日期', 'Date of birth') }}</label>
            <input id="tr-dob" v-model="birthOn" type="date" name="birth_on" autocomplete="bday">
          </div>
          <div class="form-field">
            <label for="tr-phone">{{ tx('聯絡電話', 'Phone number') }}</label>
            <input id="tr-phone" v-model="phone" type="tel" name="phone" autocomplete="tel" maxlength="32">
          </div>
          <div class="form-field">
            <label for="tr-email">Email</label>
            <input id="tr-email" v-model="email" type="email" name="email" autocomplete="email" maxlength="200">
          </div>
        </div>
        <p class="field-hint">{{ tx('聯絡電話與 Email 至少填寫一項。', 'Please fill in at least one of phone number and email.') }}</p>
      </fieldset>

      <fieldset>
        <legend>{{ tx('家長／緊急聯絡人', 'Parent / emergency contact') }}</legend>
        <p class="field-hint" style="margin-bottom:1rem;">{{ tx('未滿 18 歲的報名者必須填寫家長或緊急聯絡人的姓名與電話。', 'Registrants under 18 must provide the name and phone number of a parent or emergency contact.') }}</p>
        <div class="form-grid">
          <div class="form-field">
            <label for="tr-guardian">{{ tx('家長／緊急聯絡人姓名', 'Parent / emergency contact name') }}</label>
            <input id="tr-guardian" v-model="guardianName" type="text" name="guardian_name" maxlength="100">
          </div>
          <div class="form-field">
            <label for="tr-guardian-phone">{{ tx('家長／緊急聯絡人電話', 'Parent / emergency contact phone') }}</label>
            <input id="tr-guardian-phone" v-model="guardianPhone" type="tel" name="guardian_phone" maxlength="32">
          </div>
          <div class="form-field form-field--full">
            <label for="tr-note">{{ tx('備註', 'Notes') }}</label>
            <textarea id="tr-note" v-model="note" name="note" rows="3" maxlength="500" />
          </div>
        </div>
      </fieldset>

      <p v-if="isEn" class="field-hint">The two consent statements below are shown in Traditional Chinese only, as they are legal wording.</p>

      <div class="consent-block">
        <div class="checkbox-field">
          <input id="tr-health" v-model="healthConsent" type="checkbox" name="health_declaration" required>
          <label for="tr-health">本人確認已據實告知報名者的健康狀況（如過敏史、慢性病、目前服用藥物），如有變動將主動告知。<span class="req" aria-hidden="true">*</span></label>
        </div>
        <p class="field-hint">{{ tx('本表單不蒐集健康細節，請於我們聯繫時當面說明。', 'This form does not collect health details. Please explain them in person when we contact you.') }}</p>
      </div>

      <div class="consent-block">
        <div class="checkbox-field">
          <input id="tr-privacy" v-model="privacyConsent" type="checkbox" name="privacy_consent" required>
          <label for="tr-privacy">本人已閱讀並同意<a :href="lp('/zh/privacy/')">隱私權政策</a>，並同意{{ clubAssets.nameZh }}依本表單蒐集報名者與聯絡人之個人資料，用於處理本次試訓報名之聯繫與安全作業。<span class="req" aria-hidden="true">*</span></label>
        </div>
      </div>

      <button class="btn btn--primary btn--block" type="submit" :disabled="phase === 'submitting' || !currentTrial">{{ phase === 'submitting' ? tx('送出中…', 'Submitting…') : tx('送出試訓報名', 'Submit trial registration') }}</button>
    </form>
  </div>
</div>
</template>

<style>
.trial-table__sub{ display:block; font-size:.78rem; color:var(--muted); margin-top:.15rem; }
.trial-table__status{ font-weight:800; color:var(--brand-aa); }
.trial-table td{ padding:.85rem 1rem; border-bottom:1px solid var(--rule); vertical-align:top; }
.trial-table .trial-table__pending-row td{ padding:1.5rem 1rem; }
.trial-reg{ margin-top:2.5rem; }
.trial-reg__title{ font-size:1.25rem; font-weight:900; color:var(--heading); }
.trial-reg__form{ max-width:760px; margin-top:1.5rem; }
.trial-reg__legend{ display:block; font-size:.85rem; font-weight:700; color:var(--heading); margin-bottom:.6rem; }
.trial-reg__list{ list-style:none; margin:0; padding:0; display:grid; gap:.75rem; }
.trial-reg__option{ display:grid; grid-template-columns:auto minmax(0,1fr) auto; gap:.9rem; align-items:start; padding:1rem 1.15rem; border:1px solid var(--rule); background:var(--paper); cursor:pointer; }
.trial-reg__option.is-selected{ border-color:var(--brand-aa); background:var(--paper-2); }
.trial-reg__option input{ margin-top:.35rem; }
.trial-reg__option-main{ display:flex; flex-direction:column; gap:.2rem; font-size:.88rem; min-width:0; overflow-wrap:anywhere; }
.trial-reg__option-meta{ display:flex; flex-direction:column; align-items:flex-end; gap:.2rem; font-size:.8rem; color:var(--muted); text-align:right; }
.trial-reg__badge{ font-weight:800; color:var(--brand-aa); }
.trial-reg__no{ font-size:1.15rem; letter-spacing:.04em; }
.trial-reg__summary{ font-size:.85rem; color:var(--muted); }
@media (max-width:640px){
  .trial-reg__option{ grid-template-columns:auto minmax(0,1fr); }
  .trial-reg__option-meta{ grid-column:2; align-items:flex-start; text-align:left; flex-direction:row; flex-wrap:wrap; gap:.75rem; }
}
</style>

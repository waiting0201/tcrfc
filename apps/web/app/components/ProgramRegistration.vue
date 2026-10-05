<script setup lang="ts">
// app/components/ProgramRegistration.vue — 05 課程與活動「線上報名」（P3，2026-10-02）
//
// 規格：主站規劃書 §3.5「報名流程（前台）：選擇課程 → 選擇梯次 → 填寫學員資料 → 家長／緊急聯絡人 →
// 健康聲明與同意條款 → 送出 → 產生報名編號」；5.1–5.4 各頁「線上報名」；P2「前台自動依狀態顯示
// 立即報名／額滿候補／已截止」。對應 apps/api `POST /api/v1/{club}/programs/sessions/{sessionId}/registrations`
// （Features/Programs，回 `registrationNo`＋`status`：待確認／候補；經 BFF 代理白名單轉發，不帶任何認證——
// 網頁前台不做報名歸戶，主站規劃書 §3.14）。
//
// 只供**磐石**使用：藍鯨「推廣活動是否開放線上報名與收費」是待確認事項（藍鯨規劃書 §10 第 8 點），
// 呼叫端必須以 isTcrfc 守門，藍鯨維持現場報名說明。
//
// 🔴 健康聲明（B-9，個資法 §6 特種個資待法務確認）：比照 S1-17 營隊報名表單的處理——只收**一個勾選同意**，
// 不開自由文字欄位蒐集病史、過敏、用藥。勾選後送出固定標記文字（`HEALTH_DECLARATION_MARKER`），
// 讓後台 P3 看得到「已聲明」，但不蒐集任何健康細節。B-9 解除（確認能不能蒐集、保存多久）之前不得加欄位。
//
// ⚠️ 後端目前**不寄**報名通知信或簡訊（apps/api README「前台報名流程的 Email／簡訊通知未實作」，待決），
// 成功畫面因此只告知「請記下報名編號、課程部會與您聯繫」，不宣稱已寄信。
// ⚠️ 規劃書的「可多名」學員：端點一次只收一位，成功後提供「為另一位學員報名」保留家長／聯絡資料重填。
import type { RegistrableProgram, RegistrableSession } from '~/composables/useRegistrablePrograms'

type SessionDto = RegistrableSession

const props = defineProps<{
  /** 目前收得到報名的課程與梯次（useRegistrablePrograms 回傳的 programs，已濾掉已截止的梯次）。 */
  programs: RegistrableProgram[]
}>()

const HEALTH_DECLARATION_MARKER = '已勾選同意健康聲明（未蒐集健康細節）'

const config = useRuntimeConfig()
const club = config.public.club
const { lp, locale, isEn, tx } = useLocale()
const clubAssets = getClubAssets(club)

const registrable = computed(() => props.programs)
const hasAny = computed(() => registrable.value.length > 0)

// ── 表單狀態 ──
const programSlug = ref(registrable.value[0]?.slug ?? '')
const currentProgram = computed(() => registrable.value.find((p) => p.slug === programSlug.value) ?? registrable.value[0] ?? null)
const sessionId = ref('')
watch(currentProgram, (p) => {
  if (!p) { sessionId.value = ''; return }
  if (!p.sessions.some((s) => s.id === sessionId.value)) sessionId.value = p.sessions[0]!.id
}, { immediate: true })
const currentSession = computed(() => currentProgram.value?.sessions.find((s) => s.id === sessionId.value) ?? null)

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
// 英文版：後端寫給使用者的訊息是日常中文，不翻譯、以 lang=zh-Hant 附在英文說明後面（C-6／S2-13）。
const errorDetail = ref('')
const result = ref<{ registrationNo: string, status: string, statusCode?: string, statusLabelZh?: string, statusLabelEn?: string, programName: string | null, sessionLabel: string } | null>(null)

const GENERIC_ERROR = '送出失敗，請確認各欄位已正確填寫後再試一次；若持續發生，請改用電話或 Email 聯繫我們。'
const GENERIC_ERROR_EN = 'Submission failed. Please check that every field is filled in correctly and try again. If the problem persists, please contact us by phone or email.'

function dateRange(s: SessionDto): string {
  const a = s.startOn?.replaceAll('-', '/')
  const b = s.endOn?.replaceAll('-', '/')
  if (a && b) return a === b ? a : `${a} – ${b}`
  return a ?? b ?? tx('日期待公告', 'Dates to be announced')
}
function schedule(s: SessionDto): string | null {
  return formatWeeklySchedule(s.weeklySchedule, locale.value)
}
/** 早鳥價在早鳥截止日（含）之前有效；沒填截止日視為持續有效。 */
function effectivePrice(s: SessionDto): { price: number, early: boolean } | null {
  const today = new Date().toISOString().slice(0, 10)
  if (s.earlyBirdPrice != null && (!s.earlyBirdUntil || s.earlyBirdUntil >= today)) return { price: s.earlyBirdPrice, early: true }
  return s.price != null ? { price: s.price, early: false } : null
}
function remaining(s: SessionDto): string | null {
  if (s.capacity == null) return null
  const left = Math.max(s.capacity - s.enrolledCount, 0)
  return left > 0 ? tx(`剩餘 ${left} 名`, `${left} left`) : tx('名額已滿', 'Full')
}
function sessionLabel(s: SessionDto): string {
  return `${dateRange(s)}${schedule(s) ? tx(`（${schedule(s)}）`, ` (${schedule(s)})`) : ''}`
}

function extractMessage(err: unknown): string | null {
  const msg = extractErrorMessage(err)
  if (msg) {
    if (!isEn.value) return msg
    errorDetail.value = msg
    return 'We could not submit your registration. The message from the system is shown below.'
  }
  const status = (err as { statusCode?: number, status?: number } | null)?.statusCode ?? (err as { status?: number } | null)?.status
  if (status === 429) return tx('送出次數過多，請稍候幾分鐘再試。', 'Too many submissions. Please wait a few minutes and try again.')
  return null
}

async function onSubmit() {
  if (phase.value === 'submitting' || !currentSession.value || !currentProgram.value) return
  errorMessage.value = ''
  errorDetail.value = ''
  if (!phone.value.trim() && !email.value.trim()) {
    phase.value = 'error'
    errorMessage.value = tx('聯絡電話與 Email 至少需要填寫一項，以便課程部與您聯繫。', 'Please provide at least a phone number or an email address so the programs team can contact you.')
    return
  }
  // 誘捕欄位有值＝機器人：安靜當作成功，不送出（端點本身另有依 IP 的限流）。
  if (website.value) { phase.value = 'success'; result.value = null; return }

  phase.value = 'submitting'
  const session = currentSession.value
  try {
    const res = await $fetch<{ registrationNo: string, status: string, statusCode?: string, statusLabelZh?: string, statusLabelEn?: string }>(
      `/api/backend/${club}/programs/sessions/${session.id}/registrations`,
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
          note: note.value.trim() || undefined,
        },
      },
    )
    result.value = { ...res, programName: currentProgram.value.name, sessionLabel: sessionLabel(session) }
    phase.value = 'success'
  }
  catch (err: unknown) {
    phase.value = 'error'
    errorMessage.value = extractMessage(err) ?? (isEn.value ? GENERIC_ERROR_EN : GENERIC_ERROR)
  }
}

/** 成功後「為另一位學員報名」：清掉學員本人欄位，保留家長與聯絡資料、已選課程梯次。 */
function registerAnother() {
  applicantName.value = ''
  birthOn.value = ''
  note.value = ''
  healthConsent.value = false
  result.value = null
  phase.value = 'idle'
  errorMessage.value = ''
  errorDetail.value = ''
}
</script>

<template>
<section v-if="hasAny" id="register" class="band form-band program-reg" aria-labelledby="program-reg-title">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">REGISTER</p>
        <h2 id="program-reg-title" class="section-title">{{ tx('線上報名', 'Online Registration') }}</h2>
      </div>
    </div>
    <p class="section-lede">{{ tx('選擇梯次、填寫學員與聯絡資料後送出，系統會產生報名編號。課程費用不在站內付款，確認報名後由課程部另行通知繳費方式。', 'Choose a session, fill in the player and contact details and submit, and you will receive a registration number. Fees are not paid on this site: once your registration is confirmed, the programs team will let you know how to pay.') }}</p>

    <div v-if="phase === 'success'" class="form-status form-status--success" role="status">
      <template v-if="result">
        <p><strong>{{ tx('報名資料已送出。', 'Your registration has been submitted.') }}</strong>{{ tx('您的報名編號是', ' Your registration number is') }} <strong class="program-reg__no">{{ result.registrationNo }}</strong>{{ tx('，目前狀態：', '. Current status: ') }}{{ statusLabel(result, locale) }}{{ tx('。', '.') }}</p>
        <p v-if="result.statusCode === 'waitlisted'">{{ tx('這個梯次目前名額已滿，您已列入候補，有空位時課程部會依序與您聯繫。', 'This session is currently full and you have been placed on the waitlist. The programs team will contact you in order when a place opens up.') }}</p>
        <p v-else>{{ tx('課程部會依您留下的聯絡方式與您確認梯次、名額與繳費方式。請記下報名編號，查詢時使用。', 'The programs team will confirm the session, your place and payment details using the contact information you left. Please keep your registration number for any enquiries.') }}</p>
        <p class="program-reg__summary">{{ result.programName }}　{{ result.sessionLabel }}</p>
      </template>
      <p v-else>{{ tx('已收到您的報名資料。', 'We have received your registration.') }}</p>
      <p><button type="button" class="btn btn--light btn--sm" @click="registerAnother">{{ tx('為另一位學員報名', 'Register another player') }}</button></p>
    </div>

    <form v-else class="tcrfc-form program-reg__form" method="post" @submit.prevent="onSubmit">
      <HoneypotField v-model="website" />

      <div v-if="phase === 'error'" class="form-status form-status--error" role="alert"><p>{{ errorMessage }}<span v-if="errorDetail" lang="zh-Hant"> {{ errorDetail }}</span></p></div>

      <fieldset>
        <legend>{{ tx('課程與梯次', 'Program and Session') }}</legend>
        <div class="form-grid">
          <div v-if="registrable.length > 1" class="form-field form-field--full">
            <label for="pr-program">{{ tx('課程', 'Program') }}</label>
            <select id="pr-program" v-model="programSlug" name="program">
              <option v-for="p in registrable" :key="p.slug" :value="p.slug">{{ p.name }}</option>
            </select>
          </div>
          <div v-else-if="currentProgram" class="form-field form-field--full">
            <p class="program-reg__program">{{ currentProgram.name }}</p>
          </div>
          <div class="form-field form-field--full">
            <span id="pr-session-label" class="program-reg__legend">{{ tx('梯次', 'Session') }}<span class="req" aria-hidden="true">*</span></span>
            <ul class="program-reg__sessions" role="radiogroup" aria-labelledby="pr-session-label">
              <li v-for="s in currentProgram?.sessions ?? []" :key="s.id">
                <label :class="['program-reg__session', { 'is-selected': sessionId === s.id }]">
                  <input v-model="sessionId" type="radio" name="session" :value="s.id" required>
                  <span class="program-reg__session-main">
                    <strong>{{ dateRange(s) }}</strong>
                    <span v-if="schedule(s)">{{ schedule(s) }}</span>
                    <span v-if="s.venueName">{{ s.venueName }}</span>
                  </span>
                  <span class="program-reg__session-meta">
                    <span class="program-reg__badge">{{ sessionSignupLabel(sessionSignupState(s), locale) }}</span>
                    <span v-if="effectivePrice(s)">{{ effectivePrice(s)!.early ? tx('早鳥價 ', 'Early-bird ') : '' }}{{ formatPrice(effectivePrice(s)!.price) }}</span>
                    <span v-if="remaining(s)">{{ remaining(s) }}</span>
                  </span>
                </label>
              </li>
            </ul>
            <p v-if="currentSession && sessionSignupState(currentSession) === 'waitlist'" class="field-hint">{{ tx('這個梯次目前名額已滿，送出後會列入候補，有空位時由課程部依序聯繫。', 'This session is currently full. After you submit you will be placed on the waitlist, and the programs team will contact you in order when a place opens up.') }}</p>
          </div>
        </div>
      </fieldset>

      <fieldset>
        <legend>{{ tx('學員資料', 'Player Details') }}</legend>
        <div class="form-grid">
          <div class="form-field">
            <label for="pr-name">{{ tx('學員姓名', 'Player name') }}<span class="req" aria-hidden="true">*</span></label>
            <input id="pr-name" v-model="applicantName" type="text" name="applicant_name" required autocomplete="off" maxlength="100">
          </div>
          <div class="form-field">
            <label for="pr-dob">{{ tx('學員出生日期', 'Player date of birth') }}</label>
            <input id="pr-dob" v-model="birthOn" type="date" name="birth_on">
          </div>
        </div>
      </fieldset>

      <fieldset>
        <legend>{{ tx('家長／緊急聯絡人', 'Parent / Emergency Contact') }}</legend>
        <p class="field-hint" style="margin-bottom:1rem;">{{ tx('未成年學員請填寫家長或緊急聯絡人。聯絡電話與 Email 至少填寫一項。', 'For players under 18, please provide a parent or emergency contact. Please provide at least a phone number or an email address.') }}</p>
        <div class="form-grid">
          <div class="form-field">
            <label for="pr-guardian">{{ tx('家長／緊急聯絡人姓名', 'Parent / emergency contact name') }}</label>
            <input id="pr-guardian" v-model="guardianName" type="text" name="guardian_name" autocomplete="name" maxlength="100">
          </div>
          <div class="form-field">
            <label for="pr-guardian-phone">{{ tx('家長／緊急聯絡人電話', 'Parent / emergency contact phone') }}</label>
            <input id="pr-guardian-phone" v-model="guardianPhone" type="tel" name="guardian_phone" autocomplete="tel" maxlength="32">
          </div>
          <div class="form-field">
            <label for="pr-phone">{{ tx('聯絡電話', 'Phone') }}</label>
            <input id="pr-phone" v-model="phone" type="tel" name="phone" autocomplete="tel" maxlength="32">
          </div>
          <div class="form-field">
            <label for="pr-email">Email</label>
            <input id="pr-email" v-model="email" type="email" name="email" autocomplete="email" maxlength="200">
          </div>
          <div class="form-field form-field--full">
            <label for="pr-note">{{ tx('備註', 'Notes') }}</label>
            <textarea id="pr-note" v-model="note" name="note" rows="3" maxlength="500" />
          </div>
        </div>
      </fieldset>

      <div class="consent-block">
        <div class="checkbox-field">
          <input id="pr-health" v-model="healthConsent" type="checkbox" name="health_declaration" required>
          <label for="pr-health"><span v-if="isEn" lang="zh-Hant">本人確認已據實告知學員的健康狀況（如過敏史、慢性病、目前服用藥物），如有變動將主動告知課程部。</span><template v-else>本人確認已據實告知學員的健康狀況（如過敏史、慢性病、目前服用藥物），如有變動將主動告知課程部。</template><span class="req" aria-hidden="true">*</span></label>
        </div>
        <p class="field-hint">{{ tx('本表單不蒐集健康細節，請於課程部聯繫時當面說明。', 'This form does not collect health details. Please explain them in person when the programs team contacts you.') }}</p>
      </div>

      <div class="consent-block">
        <div class="checkbox-field">
          <input id="pr-privacy" v-model="privacyConsent" type="checkbox" name="privacy_consent" required>
          <label for="pr-privacy">{{ tx('本人已閱讀並同意', 'I have read and agree to the ') }}<a :href="lp('/zh/privacy/')">{{ tx('隱私權政策', 'Privacy Policy') }}</a>{{ isEn ? ', and I consent to Taichung Rock FC collecting the personal data of the player and contacts through this form, to be used for handling this registration and for program safety.' : `，並同意${clubAssets.nameZh}依本表單蒐集學員與聯絡人之個人資料，用於處理本次報名之聯繫與課程安全作業。` }}<span class="req" aria-hidden="true">*</span></label>
        </div>
      </div>

      <button class="btn btn--primary btn--block" type="submit" :disabled="phase === 'submitting' || !currentSession">{{ phase === 'submitting' ? tx('送出中…', 'Submitting...') : tx('送出報名', 'Submit Registration') }}</button>
    </form>
  </div>
</section>
</template>

<style>
.program-reg__form{ max-width:760px; margin-top:2rem; }
.program-reg__program{ font-size:1.1rem; font-weight:800; color:var(--heading); }
.program-reg__legend{ display:block; font-size:.85rem; font-weight:700; color:var(--heading); margin-bottom:.6rem; }
.program-reg__sessions{ list-style:none; margin:0; padding:0; display:grid; gap:.75rem; }
.program-reg__session{ display:grid; grid-template-columns:auto minmax(0,1fr) auto; gap:.9rem; align-items:start; padding:1rem 1.15rem; border:1px solid var(--rule); background:var(--paper); cursor:pointer; }
.program-reg__session.is-selected{ border-color:var(--brand-aa); background:var(--paper-2); }
.program-reg__session input{ margin-top:.35rem; }
.program-reg__session-main{ display:flex; flex-direction:column; gap:.2rem; font-size:.88rem; }
.program-reg__session-meta{ display:flex; flex-direction:column; align-items:flex-end; gap:.2rem; font-size:.8rem; color:var(--muted); text-align:right; }
.program-reg__badge{ font-weight:800; color:var(--brand-aa); }
.program-reg__no{ font-size:1.15rem; letter-spacing:.04em; }
.program-reg__summary{ font-size:.85rem; color:var(--muted); }
@media (max-width:640px){
  .program-reg__session{ grid-template-columns:auto minmax(0,1fr); }
  .program-reg__session-meta{ grid-column:2; align-items:flex-start; text-align:left; flex-direction:row; flex-wrap:wrap; gap:.75rem; }
}
</style>

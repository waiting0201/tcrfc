<script setup lang="ts">
// app/pages/zh/join/international-player/index.vue — 由 site/src/pages/zh/join/international-player/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
//
// S1-17：接上 POST /api/v1/{club}/forms/international_player_enquiry/submissions。
// 🔴 後端只定義 7 個欄位（name／nationality／passport_no／experience／video_url／visa_status／
// contact，見 db/seed/generate-club-seed-sql.py）。對應決策：
//   name = full_name／nationality = nationality／passport_no = passport_number（選填）／
//   experience = position＋current_club＋playing_level 併入 career_summary 前面（後端這欄
//     題目文字是「足球經歷」，本來就是一段自由文字摘要，併入不算新增欄位）／
//   video_url = video_url／visa_status = 選項顯示文字（後端 visa_status 是 text 型別，
//     無選項限制，直接送顯示文字比英文代碼更利於閱讀）／contact = email＋phone 合併。
// 🔴 S1-17 收尾修正（2026-09-29）：dob（出生日期，這張表單後端沒有 birth_date 鍵，跟
// 10.1／10.2 不同）、agent_contact（經紀人聯絡方式）、cv_file／doc_file（檔案上傳）在規格
// （§3.10 10.4「主要欄位」：英文姓名、國籍、護照、經歷、影片、簽證狀態）與後端都沒有定義，
// 原本畫面留著（dob 甚至沒有 v-model，填了會被瀏覽器原生驗證擋著、值也從未被讀取），
// 現已**從畫面移除**。
definePageMeta({ nav: '', unit: '10.4' })

const { lp } = useLocale()

useSeoMeta({
  title: 'International Player Enquiries 國際球員詢問｜Join / Contact｜Taichung Rock FC',
  description:
    'Interested in playing for Taichung Rock FC (TCRFC) in Taiwan? Submit your football background, video highlights and visa status. Our International department will get back to you.',
})

const VISA_STATUS_LABELS: Record<string, string> = {
  not_in_taiwan: 'Not currently in Taiwan',
  arc: 'Holds ARC / resident visa in Taiwan',
  needs_sponsorship: 'Would need sponsorship / work permit',
  other: 'Other',
}
const POSITION_LABELS: Record<string, string> = {
  gk: 'Goalkeeper (GK)', cb: 'Centre Back (CB)', fb: 'Full Back (FB)', dm: 'Defensive Midfielder (DM)',
  cm: 'Central Midfielder (CM)', wg: 'Winger (WG)', st: 'Striker (ST)',
}

const fullName = ref('')
const nationality = ref('')
const passportNumber = ref('')
const position = ref('')
const currentClub = ref('')
const playingLevel = ref('')
const careerSummary = ref('')
const videoUrl = ref('')
const visaStatus = ref('')
const email = ref('')
const phone = ref('')
const consent = ref(false)
const website = ref('')

const { status, errorMessage, submit } = useFormSubmit('international_player_enquiry')

async function onSubmit() {
  const background = [
    position.value ? `Position: ${POSITION_LABELS[position.value] ?? position.value}` : '',
    currentClub.value ? `Current club/team: ${currentClub.value}` : '',
    playingLevel.value ? `Playing level: ${playingLevel.value}` : '',
  ].filter(Boolean).join('. ')

  await submit({
    name: fullName.value,
    nationality: nationality.value,
    passport_no: passportNumber.value,
    experience: [background, careerSummary.value].filter(Boolean).join('. '),
    video_url: videoUrl.value,
    visa_status: VISA_STATUS_LABELS[visaStatus.value] ?? visaStatus.value,
    contact: [email.value, phone.value].filter(Boolean).join(' / '),
    privacy_consent: consent.value ? 'true' : '',
  }, { website: website.value })
}
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑 Breadcrumb">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁 Home</a></li>
      <li><a :href="lp('/zh/join/')">加入與聯絡 Join / Contact</a></li>
      <li aria-current="page">International Player Enquiries</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">10.4</span>
  <div class="container">
    <p class="page-hero__eyebrow">10.4 International Department</p>
    <h1 lang="en">International Player Enquiries<span class="zh-sub" lang="zh-Hant">國際球員詢問</span></h1>
    <p class="page-hero__lede" lang="en">Interested in playing for TCRFC in Taiwan? Tell us about your football background, highlight videos and visa status, and our International department will follow up with you.</p>
    <p class="page-hero__lede zh-sub-para" lang="zh-Hant">有意加入台中磐石足球俱樂部的國際球員，請填寫以下表單，國際部將盡快與你聯繫。</p>
  </div>
</section>

<section class="band form-band" aria-labelledby="form-title">
  <div class="container">
    <h2 class="visually-hidden" id="form-title" lang="en">International Player Enquiry Form</h2>
    <div class="form-layout form-layout--split">
      <FormStatusBanner
        :status="status"
        :error-message="errorMessage"
        success-message="Enquiry received! A confirmation email has been sent to you. Our International department will follow up with you directly."
      />
      <form v-if="status !== 'success'" class="tcrfc-form" action="" method="post" lang="en" @submit.prevent="onSubmit">
        <!-- action left empty: this is a static site. Actual submission (auto-reply, notification email, backend record) is handled by a server or third-party form service. This markup is the front-end field layout and validation scaffold only. -->
        <HoneypotField v-model="website" />

        <fieldset>
          <legend>Player Information <span class="zh-sub-inline" lang="zh-Hant">球員基本資料</span></legend>
          <div class="form-grid">
            <div class="form-field">
              <label for="ip-name">Full Name <span class="zh-sub-inline" lang="zh-Hant">姓名</span><span class="req" aria-hidden="true">*</span></label>
              <input id="ip-name" v-model="fullName" type="text" name="full_name" required autocomplete="name" aria-describedby="ip-name-error">
              <p class="field-error" id="ip-name-error" role="alert">Please enter your full name</p>
            </div>
            <div class="form-field">
              <label for="ip-nationality">Nationality <span class="zh-sub-inline" lang="zh-Hant">國籍</span><span class="req" aria-hidden="true">*</span></label>
              <input id="ip-nationality" v-model="nationality" type="text" name="nationality" required autocomplete="country-name" aria-describedby="ip-nationality-error">
              <p class="field-error" id="ip-nationality-error" role="alert">Please enter your nationality</p>
            </div>
            <div class="form-field">
              <label for="ip-passport">Passport Number <span class="zh-sub-inline" lang="zh-Hant">護照號碼（選填）</span></label>
              <input id="ip-passport" v-model="passportNumber" type="text" name="passport_number" autocomplete="off" aria-describedby="ip-passport-hint">
              <p class="field-hint" id="ip-passport-hint">Optional at enquiry stage; may be requested later if we proceed with a trial or contract.</p>
            </div>
          </div>
        </fieldset>

        <fieldset>
          <legend>Football Background <span class="zh-sub-inline" lang="zh-Hant">足球背景</span></legend>
          <div class="form-grid">
            <div class="form-field">
              <label for="ip-position">Position <span class="zh-sub-inline" lang="zh-Hant">場上位置</span><span class="req" aria-hidden="true">*</span></label>
              <select id="ip-position" v-model="position" name="position" required aria-describedby="ip-position-error">
                <option value="">Select</option>
                <option value="gk">Goalkeeper (GK)</option>
                <option value="cb">Centre Back (CB)</option>
                <option value="fb">Full Back (FB)</option>
                <option value="dm">Defensive Midfielder (DM)</option>
                <option value="cm">Central Midfielder (CM)</option>
                <option value="wg">Winger (WG)</option>
                <option value="st">Striker (ST)</option>
              </select>
              <p class="field-error" id="ip-position-error" role="alert">Please select your position</p>
            </div>
            <div class="form-field">
              <label for="ip-club">Current Club / Team</label>
              <input id="ip-club" v-model="currentClub" type="text" name="current_club" autocomplete="off">
            </div>
            <div class="form-field form-field--full">
              <label for="ip-level">Playing Level / League</label>
              <input id="ip-level" v-model="playingLevel" type="text" name="playing_level" placeholder="e.g. semi-professional, university league, national youth team">
            </div>
            <div class="form-field form-field--full">
              <label for="ip-experience">Career Summary <span class="zh-sub-inline" lang="zh-Hant">足球經歷簡述</span><span class="req" aria-hidden="true">*</span></label>
              <textarea id="ip-experience" v-model="careerSummary" name="career_summary" required aria-describedby="ip-experience-hint ip-experience-error"></textarea>
              <p class="field-hint" id="ip-experience-hint">Clubs, leagues, honours or representative caps — a brief summary is enough.</p>
              <p class="field-error" id="ip-experience-error" role="alert">Please summarise your football career</p>
            </div>
            <div class="form-field form-field--full">
              <label for="ip-video">Video Highlight Link <span class="zh-sub-inline" lang="zh-Hant">影片連結</span><span class="req" aria-hidden="true">*</span></label>
              <input id="ip-video" v-model="videoUrl" type="url" name="video_url" required placeholder="https://" aria-describedby="ip-video-hint ip-video-error">
              <p class="field-hint" id="ip-video-hint">A publicly viewable link (YouTube, cloud drive, etc.) speeds up our evaluation.</p>
              <p class="field-error" id="ip-video-error" role="alert">Please provide a video link</p>
            </div>
          </div>
        </fieldset>

        <fieldset>
          <legend>Visa Status <span class="zh-sub-inline" lang="zh-Hant">簽證狀態</span></legend>
          <div class="form-grid">
            <div class="form-field">
              <label for="ip-visa">Current Visa Status <span class="zh-sub-inline" lang="zh-Hant">簽證狀態</span><span class="req" aria-hidden="true">*</span></label>
              <select id="ip-visa" v-model="visaStatus" name="visa_status" required aria-describedby="ip-visa-error">
                <option value="">Select</option>
                <option value="not_in_taiwan">Not currently in Taiwan</option>
                <option value="arc">Holds ARC / resident visa in Taiwan</option>
                <option value="needs_sponsorship">Would need sponsorship / work permit</option>
                <option value="other">Other</option>
              </select>
              <p class="field-error" id="ip-visa-error" role="alert">Please select your visa status</p>
            </div>
          </div>
        </fieldset>

        <fieldset>
          <legend>Contact Information <span class="zh-sub-inline" lang="zh-Hant">聯絡方式</span></legend>
          <div class="form-grid">
            <div class="form-field">
              <label for="ip-email">Email<span class="req" aria-hidden="true">*</span></label>
              <input id="ip-email" v-model="email" type="email" name="email" required autocomplete="email" aria-describedby="ip-email-error">
              <p class="field-error" id="ip-email-error" role="alert">Please enter a valid email address</p>
            </div>
            <div class="form-field">
              <label for="ip-phone">Phone / WhatsApp<span class="req" aria-hidden="true">*</span></label>
              <input id="ip-phone" v-model="phone" type="tel" name="phone" required autocomplete="tel" aria-describedby="ip-phone-error">
              <p class="field-error" id="ip-phone-error" role="alert">Please enter a phone or WhatsApp number</p>
            </div>
          </div>
        </fieldset>

        <div class="consent-block">
          <div class="checkbox-field">
            <input id="ip-consent" v-model="consent" type="checkbox" name="consent" required aria-describedby="ip-consent-error">
            <label for="ip-consent">I have read and agree to the <a :href="lp('/zh/privacy/')">Privacy Policy</a>, and consent to Taichung Rock FC collecting the personal data submitted in this form for the purpose of processing this player enquiry. <span class="req" aria-hidden="true">*</span><span class="zh-sub-inline" lang="zh-Hant">本人已閱讀並同意隱私權政策，並同意台中磐石足球俱樂部依本表單蒐集之個人資料，用於處理本次國際球員詢問。</span></label>
          </div>
          <p class="field-error" id="ip-consent-error" role="alert">Please check the consent box to continue</p>
          
        </div>

        <div class="form-turnstile">
          <!-- Cloudflare Turnstile anti-bot widget: sitekey pending client's Cloudflare account setup, see https://developers.cloudflare.com/turnstile/ -->
          <div class="cf-turnstile" data-sitekey="" role="group" aria-label="Bot verification"></div>
          <p class="field-hint">This form is protected by Cloudflare Turnstile; the widget activates once a sitekey is configured.</p>
        </div>

        <button class="btn btn--primary btn--block" type="submit" :disabled="status === 'submitting'">Submit Enquiry</button>

        <div class="form-submit-note">
          <p><strong>What happens after you submit?</strong> You will receive an automatic confirmation email immediately. Our International department will also receive a notification and follow up with you directly regarding next steps.</p>
        </div>
      </form>

      <aside class="form-sidebar">
        <div class="form-sidebar__sticky">
        <div class="form-sidebar__card">
          <p class="form-sidebar__dept">Handled by <span class="zh-sub-inline" lang="zh-Hant">收件單位</span></p>
          <h2 lang="en">International Department</h2>
          
        </div>
        <div class="form-sidebar__card">
          <h2 lang="en">Before you apply</h2>
          <ul lang="en">
            <li>A recent match or training video link</li>
            <li>A short summary of your football career</li>
            <li>An email and phone/WhatsApp we can reach you on</li>
          </ul>
        </div>
        </div>
      </aside>
    </div>
  </div>
</section>
</template>

<style>
/* 本頁專用：英文優先版型的中文輔助文字（規劃書行 320 明訂 10.4 英文優先，中文輔助） */
.zh-sub{
  display:block; font-size:.42em; font-weight:700; letter-spacing:.08em;
  color:var(--muted-dark); margin-top:.75rem; text-transform:none;
}
.zh-sub-para{ color:rgba(255,255,255,.6); font-size:.88rem; margin-top:.6rem; }
.zh-sub-inline{
  display:inline-block; font-weight:500; color:var(--muted); font-size:.85em; margin-left:.5em;
}
.pending .zh-sub-inline{ display:inline; margin-left:.4em; }
</style>

<script setup lang="ts">
// app/pages/zh/join/general/index.vue — 由 site/src/pages/zh/join/general/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
//
// S1-17：接上 POST /api/v1/{club}/forms/general_contact/submissions。
// 欄位對應：name = name／contact = email（規劃書 §3.10 10.7 的欄位定義本來就是「姓名、Email、
// 主旨、內容」，contact 這個鍵語意上就是 Email，見 db/seed FORM_FIELD_DEFAULTS["general_contact"]
// label_zh="Email"）／subject = 主旨選單的中文顯示文字（後端 subject 是 text 型別，無選項限制）／
// message = message。
// 🔴 S1-17 收尾修正（2026-09-29）：phone（選填的聯絡電話）、doc_file（附件上傳）在規格
// （§3.10 10.7「主要欄位」：姓名、Email、主旨、內容）與後端都沒有定義，原本畫面留著卻悄悄
// 不送出，現已**從畫面移除**。
definePageMeta({ nav: '', unit: '10.7', enReady: true, enReadyBw: true })

const { lp, isEn, tx } = useLocale()
const config = useRuntimeConfig()
// BW-C1（品牌外洩全站盤點）：改讀既有的 getClubAssets()／getClubIdentity()。
// 「加入學院」連結字面沿用磐石 04 單元舊詞，比照 join/index.vue 既有做法（S0-9n）
// 改讀 identity.academyShortLabelZh，不新造文案。
const clubAssets = computed(() => getClubAssets(config.public.club))
const identity = computed(() => getClubIdentity(config.public.club))
const clubEn = computed(() => bizClubNameEn(config.public.club))
const isTcrfc = computed(() => config.public.club !== 'bw')

useSeoMeta({
  title: computed(() => (isEn.value ? `General Contact | Join / Contact | ${clubEn.value}` : `一般聯絡 General Contact｜加入與聯絡｜${clubAssets.value.nameZh}`)),
  description: computed(() => (isEn.value ? `Cannot find a suitable form? Leave us a message through the general contact form and the ${clubEn.value} administration team will pass it to the right person.` : `找不到適合的分類表單？透過一般聯絡表單留言，${clubAssets.value.nameZh}行政團隊會轉交給對應窗口處理。`)),
})

const SUBJECT_LABELS: Record<string, string> = {
  general: '一般詢問', membership: '會員與帳號', website: '網站問題回報', suggestion: '建議與回饋', other: '其他',
}

const name = ref('')
const email = ref('')
const subject = ref('')
const message = ref('')
const consent = ref(false)
const website = ref('')

const { status, errorMessage, submit } = useFormSubmit('general_contact')

async function onSubmit() {
  await submit({
    name: name.value,
    contact: email.value,
    subject: SUBJECT_LABELS[subject.value] ?? subject.value,
    message: message.value,
    privacy_consent: consent.value ? 'true' : '',
  }, { website: website.value })
}
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/join/')">{{ tx('加入與聯絡', 'Join / Contact') }}</a></li>
      <li aria-current="page">{{ tx('一般聯絡', 'General Contact') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">10.7</span>
  <div class="container">
    <p class="page-hero__eyebrow">10.7 General Contact</p>
    <h1><template v-if="isEn">General Contact</template><template v-else>{{ tx('一般聯絡', 'General Contact') }}<span class="en">General Contact</span></template></h1>
    <p class="page-hero__lede">{{ tx('以上分類都不符合你的需求？透過這個表單留言，我們會轉交給對應窗口處理。', 'None of the categories above fit? Leave us a message through this form and we will pass it to the right person.') }}</p>
  </div>
</section>

<section class="band form-band" aria-labelledby="form-title">
  <div class="container">
    <h2 class="visually-hidden" id="form-title">{{ tx('一般聯絡表單', 'General contact form') }}</h2>
    <div class="form-layout form-layout--split">
      <FormStatusBanner
        :status="status"
        :error-message="errorMessage"
        :success-message="tx('已收到你的留言！系統已寄送自動回覆信到你填寫的 Email，行政窗口會視內容轉交給對應部門回覆。', 'We have received your message! An automatic reply has been sent to the email address you provided, and our administration team will pass it to the relevant department to reply.')"
      />
      <form v-if="status !== 'success'" class="tcrfc-form" action="" method="post" @submit.prevent="onSubmit">
        <!-- action 留空：本站為純靜態站，實際送出（寄發自動回覆信／通知信／寫入後台）由後端或第三方表單服務接手，此處僅完成前端欄位配置與必填驗證骨架 -->
        <HoneypotField v-model="website" />

        <fieldset>
          <legend>{{ tx('你的資料', 'Your details') }}</legend>
          <div class="form-grid">
            <div class="form-field">
              <label for="gc-name">{{ tx('姓名', 'Name') }}<span class="req" aria-hidden="true">*</span></label>
              <input id="gc-name" v-model="name" type="text" name="name" required autocomplete="name" aria-describedby="gc-name-error">
              <p class="field-error" id="gc-name-error" role="alert">{{ tx('請填寫姓名', 'Please enter your name') }}</p>
            </div>
            <div class="form-field">
              <label for="gc-email">Email<span class="req" aria-hidden="true">*</span></label>
              <input id="gc-email" v-model="email" type="email" name="email" required autocomplete="email" aria-describedby="gc-email-error">
              <p class="field-error" id="gc-email-error" role="alert">{{ tx('請填寫有效的 Email', 'Please enter a valid email address') }}</p>
            </div>
          </div>
        </fieldset>

        <fieldset>
          <legend>{{ tx('留言內容', 'Your message') }}</legend>
          <div class="form-grid">
            <div class="form-field form-field--full">
              <label for="gc-subject">{{ tx('主旨', 'Subject') }}<span class="req" aria-hidden="true">*</span></label>
              <select id="gc-subject" v-model="subject" name="subject" required aria-describedby="gc-subject-error">
                <option value="">{{ tx('請選擇', 'Please select') }}</option>
                <option value="general">{{ tx('一般詢問', 'General enquiry') }}</option>
                <option value="membership">{{ tx('會員與帳號', 'Membership and account') }}</option>
                <option value="website">{{ tx('網站問題回報', 'Website issue report') }}</option>
                <option value="suggestion">{{ tx('建議與回饋', 'Suggestions and feedback') }}</option>
                <option value="other">{{ tx('其他', 'Other') }}</option>
              </select>
              <p class="field-error" id="gc-subject-error" role="alert">{{ tx('請選擇主旨', 'Please select a subject') }}</p>
            </div>
            <div class="form-field form-field--full">
              <label for="gc-message">{{ tx('內容', 'Message') }}<span class="req" aria-hidden="true">*</span></label>
              <textarea id="gc-message" v-model="message" name="message" required aria-describedby="gc-message-error"></textarea>
              <p class="field-error" id="gc-message-error" role="alert">{{ tx('請填寫留言內容', 'Please enter your message') }}</p>
            </div>
          </div>
        </fieldset>

        <div class="consent-block">
          <p v-if="isEn" class="field-hint">The consent notice below is pending legal review and is shown in Traditional Chinese.</p>
          <div class="checkbox-field">
            <input id="gc-consent" v-model="consent" type="checkbox" name="consent" required aria-describedby="gc-consent-error">
            <label for="gc-consent">我已閱讀並同意<a :href="lp('/zh/privacy/')">隱私權政策</a>，並同意{{ clubAssets.nameZh }}依本表單蒐集之個人資料，用於處理本次聯絡事項。<span class="req" aria-hidden="true">*</span></label>
          </div>
          <p class="field-error" id="gc-consent-error" role="alert">{{ tx('請勾選同意個資蒐集聲明', 'Please tick the box to agree to the personal data collection notice') }}</p>
          
        </div>

        <div class="form-turnstile">
          <!-- Cloudflare Turnstile 防機器人驗證：sitekey 待客戶申請 Cloudflare 帳號後設定，見 https://developers.cloudflare.com/turnstile/ -->
          <div class="cf-turnstile" data-sitekey="" role="group" :aria-label="tx('機器人驗證', 'Bot verification')"></div>
          <p class="field-hint">{{ tx('此表單由 Cloudflare Turnstile 防護，驗證元件將於 sitekey 設定後生效。', 'This form is protected by Cloudflare Turnstile. The verification widget will take effect once the site key is configured.') }}</p>
        </div>

        <button class="btn btn--primary btn--block" type="submit" :disabled="status === 'submitting'">{{ tx('送出留言', 'Send message') }}</button>

        <div class="form-submit-note">
          <p><template v-if="isEn"><strong>What happens after you submit?</strong> An automatic reply is sent straight away to the email address you provided to confirm we have received your message. Our administration team also receives a notification and will pass it to the relevant department to reply.</template><template v-else><strong>送出後會發生什麼事？</strong> 系統會立即寄送自動回覆信到你填寫的 Email，確認我們已收到留言；行政窗口會另外收到通知信，並視內容轉交給對應部門回覆。</template></p>
        </div>
      </form>

      <aside class="form-sidebar">
        <div class="form-sidebar__sticky">
        <div class="form-sidebar__card">
          <p class="form-sidebar__dept">{{ tx('收件單位', 'Handled by') }}</p>
          <h2>{{ tx('行政', 'Administration') }}</h2>
          
        </div>
        <div class="form-sidebar__card">
          <h2>{{ tx('找特定窗口？', 'Looking for a specific contact?') }}</h2>
          <ul>
            <li><a :href="lp('/zh/join/player/')">{{ tx('加入球隊', 'Join as a Player') }}</a></li>
            <li><a :href="lp('/zh/join/academy/')">{{ isEn ? (isTcrfc ? 'Join the Academy' : 'Join the Youth Team') : `加入${identity.academyShortLabelZh}` }}</a></li>
            <li><a :href="lp('/zh/join/partnership/')">{{ tx('贊助洽詢', 'Sponsorship enquiries') }}</a></li>
            <li><a :href="lp('/zh/join/media/')">{{ tx('媒體詢問', 'Media Enquiries') }}</a></li>
            <li><a :href="lp('/zh/join/')">{{ tx('查看全部七種表單', 'View all seven forms') }}</a></li>
          </ul>
        </div>
        </div>
      </aside>
    </div>
  </div>
</section>
</template>

<script setup lang="ts">
// app/pages/zh/join/media/index.vue — 由 site/src/pages/zh/join/media/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
//
// S1-17：接上 POST /api/v1/{club}/forms/media_enquiry/submissions。
// 欄位對應：media_name = media_outlet／name = contact_name／
// topic = 採訪類型顯示文字＋採訪主題／需求說明併成一段（後端這欄本來就是自由文字）／
// deadline = deadline／contact = phone＋email 合併。
// 🔴 S1-17 收尾修正（2026-09-29）：contact_title（職稱）、doc_file（採訪大綱文件上傳）在
// 規格（§3.10 10.6「主要欄位」）與後端都沒有定義，原本畫面留著卻悄悄不送出，現已**從畫面
// 移除**。
definePageMeta({ nav: '', unit: '10.6', enReady: true })

const { lp, isEn, tx } = useLocale()
const config = useRuntimeConfig()
// BW-C1（品牌外洩全站盤點）：改讀既有的 getClubAssets()，理由同 privacy/index.vue。
const clubAssets = computed(() => getClubAssets(config.public.club))

useSeoMeta({
  title: computed(() => (isEn.value ? 'Media Enquiries | Join / Contact | Taichung Rock FC' : `媒體詢問 Media Enquiries｜加入與聯絡｜${clubAssets.value.nameZh}`)),
  description: computed(() => (isEn.value ? 'The Taichung Rock FC contact point for media interviews and collaboration enquiries. Provide your media details, interview topic and deadline, and our communications team will reply as soon as possible.' : `${clubAssets.value.nameZh}媒體採訪與合作詢問窗口。填寫媒體資料、採訪主題與截稿日，公關團隊將盡快回覆。`)),
})

const COVERAGE_TYPE_LABELS: Record<string, string> = {
  'print-online': '平面／網路報導', 'tv-video': '電視／影音採訪', 'live-podcast': '直播／Podcast',
  'photo-drone': '攝影／空拍需求', other: '其他',
}

const mediaOutlet = ref('')
const coverageType = ref('')
const contactName = ref('')
const phone = ref('')
const email = ref('')
const topic = ref('')
const deadline = ref('')
const consent = ref(false)
const website = ref('')

const { status, errorMessage, submit } = useFormSubmit('media_enquiry')

async function onSubmit() {
  const coverageLabel = COVERAGE_TYPE_LABELS[coverageType.value]

  await submit({
    media_name: mediaOutlet.value,
    name: contactName.value,
    topic: [coverageLabel ? `［${coverageLabel}］` : '', topic.value].filter(Boolean).join(' '),
    deadline: deadline.value,
    contact: [phone.value, email.value].filter(Boolean).join(' ／ '),
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
      <li aria-current="page">{{ tx('媒體詢問', 'Media Enquiries') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">10.6</span>
  <div class="container">
    <p class="page-hero__eyebrow">10.6 Media Enquiries</p>
    <h1><template v-if="isEn">Media Enquiries</template><template v-else>{{ tx('媒體詢問', 'Media Enquiries') }}<span class="en">Media Enquiries</span></template></h1>
    <p class="page-hero__lede">{{ tx('採訪邀約、新聞稿需求或媒體合作，請填寫以下資料，公關團隊將盡快回覆採訪相關安排。', 'For interview requests, press release needs or media collaboration, please fill in the details below and our communications team will reply as soon as possible with interview arrangements.') }}</p>
  </div>
</section>

<section class="band form-band" aria-labelledby="form-title">
  <div class="container">
    <h2 class="visually-hidden" id="form-title">{{ tx('媒體詢問表單', 'Media enquiry form') }}</h2>
    <div class="form-layout form-layout--split">
      <FormStatusBanner
        :status="status"
        :error-message="errorMessage"
        :success-message="tx('已收到你的媒體詢問！系統已寄送自動回覆信到你填寫的 Email，公關窗口會依採訪時程盡快回覆安排。', 'We have received your media enquiry! An automatic reply has been sent to the email address you provided, and our communications team will reply with arrangements as soon as possible, in line with your interview schedule.')"
      />
      <form v-if="status !== 'success'" class="tcrfc-form" action="" method="post" @submit.prevent="onSubmit">
        <!-- action 留空：本站為純靜態站，實際送出（寄發自動回覆信／通知信／寫入後台）由後端或第三方表單服務接手，此處僅完成前端欄位配置與必填驗證骨架 -->
        <HoneypotField v-model="website" />

        <fieldset>
          <legend>{{ tx('媒體資料', 'Media details') }}</legend>
          <div class="form-grid">
            <div class="form-field">
              <label for="md-outlet">{{ tx('媒體／機構名稱', 'Media outlet / organisation name') }}<span class="req" aria-hidden="true">*</span></label>
              <input id="md-outlet" v-model="mediaOutlet" type="text" name="media_outlet" required autocomplete="organization" aria-describedby="md-outlet-error">
              <p class="field-error" id="md-outlet-error" role="alert">{{ tx('請填寫媒體／機構名稱', 'Please enter the media outlet / organisation name') }}</p>
            </div>
            <div class="form-field">
              <label for="md-type">{{ tx('採訪類型', 'Type of coverage') }}</label>
              <select id="md-type" v-model="coverageType" name="coverage_type">
                <option value="">{{ tx('請選擇', 'Please select') }}</option>
                <option value="print-online">{{ tx('平面／網路報導', 'Print / online coverage') }}</option>
                <option value="tv-video">{{ tx('電視／影音採訪', 'TV / video interview') }}</option>
                <option value="live-podcast">{{ tx('直播／Podcast', 'Livestream / podcast') }}</option>
                <option value="photo-drone">{{ tx('攝影／空拍需求', 'Photography / drone filming') }}</option>
                <option value="other">{{ tx('其他', 'Other') }}</option>
              </select>
            </div>
          </div>
        </fieldset>

        <fieldset>
          <legend>{{ tx('記者／聯絡人資料', 'Journalist / contact details') }}</legend>
          <div class="form-grid">
            <div class="form-field">
              <label for="md-name">{{ tx('記者／聯絡人姓名', 'Journalist / contact name') }}<span class="req" aria-hidden="true">*</span></label>
              <input id="md-name" v-model="contactName" type="text" name="contact_name" required autocomplete="name" aria-describedby="md-name-error">
              <p class="field-error" id="md-name-error" role="alert">{{ tx('請填寫記者／聯絡人姓名', 'Please enter the journalist / contact name') }}</p>
            </div>
            <div class="form-field">
              <label for="md-phone">{{ tx('聯絡電話', 'Phone number') }}<span class="req" aria-hidden="true">*</span></label>
              <input id="md-phone" v-model="phone" type="tel" name="phone" required autocomplete="tel" aria-describedby="md-phone-error">
              <p class="field-error" id="md-phone-error" role="alert">{{ tx('請填寫聯絡電話', 'Please enter a phone number') }}</p>
            </div>
            <div class="form-field">
              <label for="md-email">Email<span class="req" aria-hidden="true">*</span></label>
              <input id="md-email" v-model="email" type="email" name="email" required autocomplete="email" aria-describedby="md-email-error">
              <p class="field-error" id="md-email-error" role="alert">{{ tx('請填寫有效的 Email', 'Please enter a valid email address') }}</p>
            </div>
          </div>
        </fieldset>

        <fieldset>
          <legend>{{ tx('採訪需求', 'Interview requirements') }}</legend>
          <div class="form-grid">
            <div class="form-field form-field--full">
              <label for="md-topic">{{ tx('採訪主題／需求說明', 'Interview topic / requirements') }}<span class="req" aria-hidden="true">*</span></label>
              <textarea id="md-topic" v-model="topic" name="topic" required aria-describedby="md-topic-hint md-topic-error"></textarea>
              <p class="field-hint" id="md-topic-hint">{{ tx('請說明採訪對象（如球員、教練、管理層）、報導角度與希望配合的形式。', 'Please describe who you would like to interview (for example players, coaches or management), the angle of your coverage and the format you have in mind.') }}</p>
              <p class="field-error" id="md-topic-error" role="alert">{{ tx('請說明採訪主題與需求', 'Please describe the interview topic and requirements') }}</p>
            </div>
            <div class="form-field">
              <label for="md-deadline">{{ tx('期望採訪日期／截稿日', 'Preferred interview date / deadline') }}<span class="req" aria-hidden="true">*</span></label>
              <input id="md-deadline" v-model="deadline" type="date" name="deadline" required aria-describedby="md-deadline-error">
              <p class="field-error" id="md-deadline-error" role="alert">{{ tx('請填寫期望採訪日期或截稿日', 'Please enter your preferred interview date or deadline') }}</p>
            </div>
          </div>
        </fieldset>

        <div class="consent-block">
          <p v-if="isEn" class="field-hint">The consent notice below is pending legal review and is shown in Traditional Chinese.</p>
          <div class="checkbox-field">
            <input id="md-consent" v-model="consent" type="checkbox" name="consent" required aria-describedby="md-consent-error">
            <label for="md-consent">我已閱讀並同意<a :href="lp('/zh/privacy/')">隱私權政策</a>，並同意{{ clubAssets.nameZh }}依本表單蒐集之聯絡人個人資料，用於處理本次媒體採訪詢問之聯繫與安排。<span class="req" aria-hidden="true">*</span></label>
          </div>
          <p class="field-error" id="md-consent-error" role="alert">{{ tx('請勾選同意個資蒐集聲明', 'Please tick the box to agree to the personal data collection notice') }}</p>
          
        </div>

        <div class="form-turnstile">
          <!-- Cloudflare Turnstile 防機器人驗證：sitekey 待客戶申請 Cloudflare 帳號後設定，見 https://developers.cloudflare.com/turnstile/ -->
          <div class="cf-turnstile" data-sitekey="" role="group" :aria-label="tx('機器人驗證', 'Bot verification')"></div>
          <p class="field-hint">{{ tx('此表單由 Cloudflare Turnstile 防護，驗證元件將於 sitekey 設定後生效。', 'This form is protected by Cloudflare Turnstile. The verification widget will take effect once the site key is configured.') }}</p>
        </div>

        <button class="btn btn--primary btn--block" type="submit" :disabled="status === 'submitting'">{{ tx('送出詢問', 'Submit enquiry') }}</button>

        <div class="form-submit-note">
          <p><template v-if="isEn"><strong>What happens after you submit?</strong> An automatic reply is sent straight away to the email address you provided to confirm we have received your details. Our communications team also receives a notification and will reply with arrangements as soon as possible, in line with your interview schedule.</template><template v-else><strong>送出後會發生什麼事？</strong> 系統會立即寄送自動回覆信到你填寫的 Email，確認我們已收到資料；公關窗口會另外收到通知信，並依採訪時程盡快回覆安排。</template></p>
        </div>
      </form>

      <aside class="form-sidebar">
        <div class="form-sidebar__sticky">
        <div class="form-sidebar__card">
          <p class="form-sidebar__dept">{{ tx('收件單位', 'Handled by') }}</p>
          <h2>{{ tx('公關', 'Communications') }}</h2>
          
        </div>
        <div class="form-sidebar__card">
          <h2>{{ tx('延伸閱讀', 'Further reading') }}</h2>
          <ul>
            <li><a :href="lp('/zh/news/media/')">{{ tx('媒體專區 Media Hub', 'Press & Media') }}</a></li>
          </ul>
        </div>
        </div>
      </aside>
    </div>
  </div>
</section>
</template>

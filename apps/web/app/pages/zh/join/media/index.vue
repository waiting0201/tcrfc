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
definePageMeta({ nav: '', unit: '10.6' })

const { lp } = useLocale()
const config = useRuntimeConfig()
// BW-C1（品牌外洩全站盤點）：改讀既有的 getClubAssets()，理由同 privacy/index.vue。
const clubAssets = computed(() => getClubAssets(config.public.club))

useSeoMeta({
  title: computed(() => `媒體詢問 Media Enquiries｜加入與聯絡｜${clubAssets.value.nameZh}`),
  description: computed(() => `${clubAssets.value.nameZh}媒體採訪與合作詢問窗口。填寫媒體資料、採訪主題與截稿日，公關團隊將盡快回覆。`),
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
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/join/')">加入與聯絡</a></li>
      <li aria-current="page">媒體詢問</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">10.6</span>
  <div class="container">
    <p class="page-hero__eyebrow">10.6 Media Enquiries</p>
    <h1>媒體詢問<span class="en">Media Enquiries</span></h1>
    <p class="page-hero__lede">採訪邀約、新聞稿需求或媒體合作，請填寫以下資料，公關團隊將盡快回覆採訪相關安排。</p>
  </div>
</section>

<section class="band form-band" aria-labelledby="form-title">
  <div class="container">
    <h2 class="visually-hidden" id="form-title">媒體詢問表單</h2>
    <div class="form-layout form-layout--split">
      <FormStatusBanner
        :status="status"
        :error-message="errorMessage"
        success-message="已收到你的媒體詢問！系統已寄送自動回覆信到你填寫的 Email，公關窗口會依採訪時程盡快回覆安排。"
      />
      <form v-if="status !== 'success'" class="tcrfc-form" action="" method="post" @submit.prevent="onSubmit">
        <!-- action 留空：本站為純靜態站，實際送出（寄發自動回覆信／通知信／寫入後台）由後端或第三方表單服務接手，此處僅完成前端欄位配置與必填驗證骨架 -->
        <HoneypotField v-model="website" />

        <fieldset>
          <legend>媒體資料</legend>
          <div class="form-grid">
            <div class="form-field">
              <label for="md-outlet">媒體／機構名稱<span class="req" aria-hidden="true">*</span></label>
              <input id="md-outlet" v-model="mediaOutlet" type="text" name="media_outlet" required autocomplete="organization" aria-describedby="md-outlet-error">
              <p class="field-error" id="md-outlet-error" role="alert">請填寫媒體／機構名稱</p>
            </div>
            <div class="form-field">
              <label for="md-type">採訪類型</label>
              <select id="md-type" v-model="coverageType" name="coverage_type">
                <option value="">請選擇</option>
                <option value="print-online">平面／網路報導</option>
                <option value="tv-video">電視／影音採訪</option>
                <option value="live-podcast">直播／Podcast</option>
                <option value="photo-drone">攝影／空拍需求</option>
                <option value="other">其他</option>
              </select>
            </div>
          </div>
        </fieldset>

        <fieldset>
          <legend>記者／聯絡人資料</legend>
          <div class="form-grid">
            <div class="form-field">
              <label for="md-name">記者／聯絡人姓名<span class="req" aria-hidden="true">*</span></label>
              <input id="md-name" v-model="contactName" type="text" name="contact_name" required autocomplete="name" aria-describedby="md-name-error">
              <p class="field-error" id="md-name-error" role="alert">請填寫記者／聯絡人姓名</p>
            </div>
            <div class="form-field">
              <label for="md-phone">聯絡電話<span class="req" aria-hidden="true">*</span></label>
              <input id="md-phone" v-model="phone" type="tel" name="phone" required autocomplete="tel" aria-describedby="md-phone-error">
              <p class="field-error" id="md-phone-error" role="alert">請填寫聯絡電話</p>
            </div>
            <div class="form-field">
              <label for="md-email">Email<span class="req" aria-hidden="true">*</span></label>
              <input id="md-email" v-model="email" type="email" name="email" required autocomplete="email" aria-describedby="md-email-error">
              <p class="field-error" id="md-email-error" role="alert">請填寫有效的 Email</p>
            </div>
          </div>
        </fieldset>

        <fieldset>
          <legend>採訪需求</legend>
          <div class="form-grid">
            <div class="form-field form-field--full">
              <label for="md-topic">採訪主題／需求說明<span class="req" aria-hidden="true">*</span></label>
              <textarea id="md-topic" v-model="topic" name="topic" required aria-describedby="md-topic-hint md-topic-error"></textarea>
              <p class="field-hint" id="md-topic-hint">請說明採訪對象（如球員、教練、管理層）、報導角度與希望配合的形式。</p>
              <p class="field-error" id="md-topic-error" role="alert">請說明採訪主題與需求</p>
            </div>
            <div class="form-field">
              <label for="md-deadline">期望採訪日期／截稿日<span class="req" aria-hidden="true">*</span></label>
              <input id="md-deadline" v-model="deadline" type="date" name="deadline" required aria-describedby="md-deadline-error">
              <p class="field-error" id="md-deadline-error" role="alert">請填寫期望採訪日期或截稿日</p>
            </div>
          </div>
        </fieldset>

        <div class="consent-block">
          <div class="checkbox-field">
            <input id="md-consent" v-model="consent" type="checkbox" name="consent" required aria-describedby="md-consent-error">
            <label for="md-consent">我已閱讀並同意<a :href="lp('/zh/privacy/')">隱私權政策</a>，並同意{{ clubAssets.nameZh }}依本表單蒐集之聯絡人個人資料，用於處理本次媒體採訪詢問之聯繫與安排。<span class="req" aria-hidden="true">*</span></label>
          </div>
          <p class="field-error" id="md-consent-error" role="alert">請勾選同意個資蒐集聲明</p>
          
        </div>

        <div class="form-turnstile">
          <!-- Cloudflare Turnstile 防機器人驗證：sitekey 待客戶申請 Cloudflare 帳號後設定，見 https://developers.cloudflare.com/turnstile/ -->
          <div class="cf-turnstile" data-sitekey="" role="group" aria-label="機器人驗證"></div>
          <p class="field-hint">此表單由 Cloudflare Turnstile 防護，驗證元件將於 sitekey 設定後生效。</p>
        </div>

        <button class="btn btn--primary btn--block" type="submit" :disabled="status === 'submitting'">送出詢問</button>

        <div class="form-submit-note">
          <p><strong>送出後會發生什麼事？</strong> 系統會立即寄送自動回覆信到你填寫的 Email，確認我們已收到資料；公關窗口會另外收到通知信，並依採訪時程盡快回覆安排。</p>
        </div>
      </form>

      <aside class="form-sidebar">
        <div class="form-sidebar__sticky">
        <div class="form-sidebar__card">
          <p class="form-sidebar__dept">收件單位</p>
          <h2>公關</h2>
          
        </div>
        <div class="form-sidebar__card">
          <h2>延伸閱讀</h2>
          <ul>
            <li><a :href="lp('/zh/news/media/')">媒體專區 Media Hub</a></li>
          </ul>
        </div>
        </div>
      </aside>
    </div>
  </div>
</section>
</template>

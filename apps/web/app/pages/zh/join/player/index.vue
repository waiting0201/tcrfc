<script setup lang="ts">
// app/pages/zh/join/player/index.vue — 由 site/src/pages/zh/join/player/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
//
// S1-17：接上 POST /api/v1/{club}/forms/join_player/submissions（apps/api 既有端點）。
// 🔴 S1-17 收尾修正（2026-09-29）：後端 join_player 只定義 6 個欄位（name／birth_date／
// position／experience／video_url／contact，見 db/seed/generate-club-seed-sql.py
// FORM_FIELD_DEFAULTS），但 mockup 前台原有 14 個可見欄位——性別、居住城市、慣用腳、目前球隊、
// 履歷／照片檔案這 6 個欄位在規格（主站規劃書 §3.10「主要欄位」：姓名、生日、位置、經歷、
// 影片連結、聯絡方式）與後端都沒有定義，原本「畫面留著、悄悄不送出」會誤導使用者以為填了會被
// 收到，現已**從畫面移除**（不是隱藏）。完整的欄位對應決策記在 apps/web/README.md「S1-17」節
// 「表單欄位對應表」，這裡只放程式碼：
//   name = 中文姓名（英文姓名有填就併入括號——英文姓名不在移除之列，因為它會被合併送出，
//     不是「填了被丟棄」）／birth_date = 出生日期／
//   position = 場上位置選項的中文顯示文字（後端 position 是 text 型別，無選項限制，
//     直接送顯示文字比送英文代碼對後台閱讀者更有意義）／experience = 足球經歷簡述／
//     video_url = 影片連結／contact = 電話與 Email 合併（後端只有一個「聯絡方式」欄位）
definePageMeta({ nav: '', unit: '10.1' })

const { lp } = useLocale()

useSeoMeta({
  title: '加入球隊 Join as a Player｜加入與聯絡｜台中磐石足球俱樂部',
  description:
    '台中磐石足球俱樂部持續招募一線隊與各梯隊球員。填寫加入球隊表單，提供你的基本資料、足球背景與比賽影片連結，競技部將盡快與你聯繫。',
})

const POSITION_LABELS: Record<string, string> = {
  gk: '門將 GK', cb: '中後衛 CB', fb: '邊後衛 FB', dm: '後腰 DM', cm: '中場 CM', wg: '邊鋒 WG', st: '前鋒 ST',
}

const nameZh = ref('')
const nameEn = ref('')
const dob = ref('')
const position = ref('')
const experience = ref('')
const videoUrl = ref('')
const phone = ref('')
const email = ref('')
const consent = ref(false)
const website = ref('') // honeypot

const { status, errorMessage, submit } = useFormSubmit('join_player')

async function onSubmit() {
  await submit({
    name: nameEn.value ? `${nameZh.value}（${nameEn.value}）` : nameZh.value,
    birth_date: dob.value,
    position: POSITION_LABELS[position.value] ?? position.value,
    experience: experience.value,
    video_url: videoUrl.value,
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
      <li aria-current="page">加入球隊</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">10.1</span>
  <div class="container">
    <p class="page-hero__eyebrow">10.1 Join as a Player</p>
    <h1>加入球隊<span class="en">Join as a Player</span></h1>
    <p class="page-hero__lede">具備競技實力、渴望在企甲聯賽舞台證明自己？台中磐石一線隊與各梯隊持續招募新血，填寫以下表單，讓競技部認識你。</p>
  </div>
</section>

<section class="band form-band" aria-labelledby="form-title">
  <div class="container">
    <h2 class="visually-hidden" id="form-title">加入球隊報名表單</h2>
    <div class="form-layout form-layout--split">
      <FormStatusBanner
        :status="status"
        :error-message="errorMessage"
        success-message="已收到你的報名資料！系統已寄送自動回覆信到你填寫的 Email，競技部會盡快與你聯繫。"
      />
      <form v-if="status !== 'success'" class="tcrfc-form" action="" method="post" @submit.prevent="onSubmit">
        <!-- action 留空：本站為純靜態站，實際送出（寄發自動回覆信／通知信／寫入後台）由後端或第三方表單服務接手，此處僅完成前端欄位配置與必填驗證骨架 -->
        <HoneypotField v-model="website" />

        <fieldset>
          <legend>球員基本資料</legend>
          <div class="form-grid">
            <div class="form-field">
              <label for="p-name-zh">中文姓名<span class="req" aria-hidden="true">*</span></label>
              <input id="p-name-zh" v-model="nameZh" type="text" name="name_zh" required autocomplete="name" aria-describedby="p-name-zh-error">
              <p class="field-error" id="p-name-zh-error" role="alert">請填寫中文姓名</p>
            </div>
            <div class="form-field">
              <label for="p-name-en">英文姓名</label>
              <input id="p-name-en" v-model="nameEn" type="text" name="name_en" autocomplete="off">
            </div>
            <div class="form-field">
              <label for="p-dob">出生日期<span class="req" aria-hidden="true">*</span></label>
              <input id="p-dob" v-model="dob" type="date" name="dob" required aria-describedby="p-dob-error">
              <p class="field-error" id="p-dob-error" role="alert">請填寫出生日期</p>
            </div>
          </div>
        </fieldset>

        <fieldset>
          <legend>足球背景</legend>
          <div class="form-grid">
            <div class="form-field">
              <label for="p-position">場上位置<span class="req" aria-hidden="true">*</span></label>
              <select id="p-position" v-model="position" name="position" required aria-describedby="p-position-error">
                <option value="">請選擇</option>
                <option value="gk">門將 GK</option>
                <option value="cb">中後衛 CB</option>
                <option value="fb">邊後衛 FB</option>
                <option value="dm">後腰 DM</option>
                <option value="cm">中場 CM</option>
                <option value="wg">邊鋒 WG</option>
                <option value="st">前鋒 ST</option>
              </select>
              <p class="field-error" id="p-position-error" role="alert">請選擇場上位置</p>
            </div>
            <div class="form-field form-field--full">
              <label for="p-experience">足球經歷簡述<span class="req" aria-hidden="true">*</span></label>
              <textarea id="p-experience" v-model="experience" name="experience" required aria-describedby="p-experience-hint p-experience-error"></textarea>
              <p class="field-hint" id="p-experience-hint">曾效力的球隊、參加過的聯賽或代表隊經歷，簡述即可。</p>
              <p class="field-error" id="p-experience-error" role="alert">請簡述你的足球經歷</p>
            </div>
            <div class="form-field form-field--full">
              <label for="p-video">比賽或訓練影片連結<span class="req" aria-hidden="true">*</span></label>
              <input id="p-video" v-model="videoUrl" type="url" name="video_url" required placeholder="https://" aria-describedby="p-video-hint p-video-error">
              <p class="field-hint" id="p-video-hint">YouTube、雲端硬碟等可公開觀看的連結，有助於加快評估。</p>
              <p class="field-error" id="p-video-error" role="alert">請提供影片連結</p>
            </div>
          </div>
        </fieldset>

        <fieldset>
          <legend>聯絡方式</legend>
          <div class="form-grid">
            <div class="form-field">
              <label for="p-phone">聯絡電話<span class="req" aria-hidden="true">*</span></label>
              <input id="p-phone" v-model="phone" type="tel" name="phone" required autocomplete="tel" aria-describedby="p-phone-error">
              <p class="field-error" id="p-phone-error" role="alert">請填寫聯絡電話</p>
            </div>
            <div class="form-field">
              <label for="p-email">Email<span class="req" aria-hidden="true">*</span></label>
              <input id="p-email" v-model="email" type="email" name="email" required autocomplete="email" aria-describedby="p-email-error">
              <p class="field-error" id="p-email-error" role="alert">請填寫有效的 Email</p>
            </div>
          </div>
        </fieldset>

        <div class="consent-block">
          <div class="checkbox-field">
            <input id="p-consent" v-model="consent" type="checkbox" name="consent" required aria-describedby="p-consent-error">
            <label for="p-consent">我已閱讀並同意<a :href="lp('/zh/privacy/')">隱私權政策</a>，並同意台中磐石足球俱樂部依本表單蒐集之個人資料，用於處理本次加入球隊申請之聯繫與評估作業。<span class="req" aria-hidden="true">*</span></label>
          </div>
          <p class="field-error" id="p-consent-error" role="alert">請勾選同意個資蒐集聲明</p>
          
        </div>

        <div class="form-turnstile">
          <!-- Cloudflare Turnstile 防機器人驗證：sitekey 待客戶申請 Cloudflare 帳號後設定，見 https://developers.cloudflare.com/turnstile/ -->
          <div class="cf-turnstile" data-sitekey="" role="group" aria-label="機器人驗證"></div>
          <p class="field-hint">此表單由 Cloudflare Turnstile 防護，驗證元件將於 sitekey 設定後生效。</p>
        </div>

        <button class="btn btn--primary btn--block" type="submit" :disabled="status === 'submitting'">送出報名</button>

        <div class="form-submit-note">
          <p><strong>送出後會發生什麼事？</strong> 系統會立即寄送自動回覆信到你填寫的 Email，確認我們已收到資料；競技部窗口會另外收到通知信，並視評估結果安排後續聯繫（如試訓邀請）。</p>
        </div>
      </form>

      <aside class="form-sidebar">
        <div class="form-sidebar__sticky">
        <div class="form-sidebar__card">
          <p class="form-sidebar__dept">收件單位</p>
          <h2>競技部</h2>
          
        </div>
        <div class="form-sidebar__card">
          <h2>填寫前可以先準備</h2>
          <ul>
            <li>近期比賽或訓練影片連結</li>
            <li>可聯絡到本人的電話與 Email</li>
          </ul>
        </div>
        </div>
      </aside>
    </div>
  </div>
</section>
</template>

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
definePageMeta({ nav: '', unit: '10.1', enReady: true })

const { lp, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')
// BW-C1（品牌外洩全站盤點）：改讀既有的 getClubAssets()。「競技部」是磐石既有的內部
// 部門稱呼，藍鯨沒有已核實的對應部門名稱，不得沿用（比照 getJoinFirstTeamBody 對 bw
// 不具名部門的既有做法），bw 版一律只講「俱樂部」。
const clubAssets = computed(() => getClubAssets(config.public.club))
const { facts } = useSiteFacts(clubKey.value)
const deptLabel = computed(() => (isEn.value ? 'Football Department' : isTcrfc.value ? '競技部' : clubAssets.value.shortNameZh))

useSeoMeta({
  title: computed(() => (isEn.value
    ? 'Join as a Player | Join / Contact | Taichung Rock FC'
    : `加入球隊 Join as a Player｜加入與聯絡｜${clubAssets.value.nameZh}`)),
  description: computed(() => (isEn.value
    ? 'Taichung Rock FC is recruiting players for the First Team and age-group squads. Complete the Join as a Player form with your details, football background and a match video link, and the Football Department will get in touch.'
    : isTcrfc.value
    ? '台中磐石足球俱樂部持續招募一線隊與各梯隊球員。填寫加入球隊表單，提供你的基本資料、足球背景與比賽影片連結，競技部將盡快與你聯繫。'
    : `${clubAssets.value.nameZh}持續招募一線隊球員。填寫加入球隊表單，提供你的基本資料、足球背景與比賽影片連結，俱樂部將盡快與你聯繫。`)),
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
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/join/')">{{ tx('加入與聯絡', 'Join / Contact') }}</a></li>
      <li aria-current="page">{{ tx('加入球隊', 'Join as a Player') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">10.1</span>
  <div class="container">
    <p class="page-hero__eyebrow">10.1 Join as a Player</p>
    <h1><template v-if="isEn">Join as a Player</template><template v-else>{{ tx('加入球隊', 'Join as a Player') }}<span class="en">Join as a Player</span></template></h1>
    <p v-if="isTcrfc" class="page-hero__lede"><template v-if="isEn">Got the ability and eager to prove yourself in the league? The First Team and our age-group squads are always looking for new talent. Complete the form below and let the Football Department get to know you.</template><template v-else>具備競技實力、渴望在企甲聯賽舞台證明自己？台中磐石一線隊與各梯隊持續招募新血，填寫以下表單，讓競技部認識你。</template></p>
    <p v-else class="page-hero__lede">具備競技實力、渴望在{{ facts.league.nameZh }}舞台證明自己？台中藍鯨一線隊持續招募新血，填寫以下表單，讓俱樂部認識你。</p>
  </div>
</section>

<section class="band form-band" aria-labelledby="form-title">
  <div class="container">
    <h2 class="visually-hidden" id="form-title">{{ tx('加入球隊報名表單', 'Join as a Player application form') }}</h2>
    <div class="form-layout form-layout--split">
      <FormStatusBanner
        :status="status"
        :error-message="errorMessage"
        :success-message="isEn ? `We have received your application! An automatic reply has been sent to the email address you provided, and the ${deptLabel} will be in touch soon.` : `已收到你的報名資料！系統已寄送自動回覆信到你填寫的 Email，${deptLabel}會盡快與你聯繫。`"
      />
      <form v-if="status !== 'success'" class="tcrfc-form" action="" method="post" @submit.prevent="onSubmit">
        <!-- action 留空：本站為純靜態站，實際送出（寄發自動回覆信／通知信／寫入後台）由後端或第三方表單服務接手，此處僅完成前端欄位配置與必填驗證骨架 -->
        <HoneypotField v-model="website" />

        <fieldset>
          <legend>{{ tx('球員基本資料', 'Player details') }}</legend>
          <div class="form-grid">
            <div class="form-field">
              <label for="p-name-zh">{{ tx('中文姓名', 'Name (Chinese)') }}<span class="req" aria-hidden="true">*</span></label>
              <input id="p-name-zh" v-model="nameZh" type="text" name="name_zh" required autocomplete="name" aria-describedby="p-name-zh-error">
              <p class="field-error" id="p-name-zh-error" role="alert">{{ tx('請填寫中文姓名', 'Please enter your name in Chinese') }}</p>
            </div>
            <div class="form-field">
              <label for="p-name-en">{{ tx('英文姓名', 'Name (English)') }}</label>
              <input id="p-name-en" v-model="nameEn" type="text" name="name_en" autocomplete="off">
            </div>
            <div class="form-field">
              <label for="p-dob">{{ tx('出生日期', 'Date of birth') }}<span class="req" aria-hidden="true">*</span></label>
              <input id="p-dob" v-model="dob" type="date" name="dob" required aria-describedby="p-dob-error">
              <p class="field-error" id="p-dob-error" role="alert">{{ tx('請填寫出生日期', 'Please enter your date of birth') }}</p>
            </div>
          </div>
        </fieldset>

        <fieldset>
          <legend>{{ tx('足球背景', 'Football background') }}</legend>
          <div class="form-grid">
            <div class="form-field">
              <label for="p-position">{{ tx('場上位置', 'Playing position') }}<span class="req" aria-hidden="true">*</span></label>
              <select id="p-position" v-model="position" name="position" required aria-describedby="p-position-error">
                <option value="">{{ tx('請選擇', 'Please select') }}</option>
                <option value="gk">{{ tx('門將 GK', 'Goalkeeper (GK)') }}</option>
                <option value="cb">{{ tx('中後衛 CB', 'Centre-back (CB)') }}</option>
                <option value="fb">{{ tx('邊後衛 FB', 'Full-back (FB)') }}</option>
                <option value="dm">{{ tx('後腰 DM', 'Defensive midfielder (DM)') }}</option>
                <option value="cm">{{ tx('中場 CM', 'Central midfielder (CM)') }}</option>
                <option value="wg">{{ tx('邊鋒 WG', 'Winger (WG)') }}</option>
                <option value="st">{{ tx('前鋒 ST', 'Forward (ST)') }}</option>
              </select>
              <p class="field-error" id="p-position-error" role="alert">{{ tx('請選擇場上位置', 'Please select your playing position') }}</p>
            </div>
            <div class="form-field form-field--full">
              <label for="p-experience">{{ tx('足球經歷簡述', 'Football experience') }}<span class="req" aria-hidden="true">*</span></label>
              <textarea id="p-experience" v-model="experience" name="experience" required aria-describedby="p-experience-hint p-experience-error"></textarea>
              <p class="field-hint" id="p-experience-hint">{{ tx('曾效力的球隊、參加過的聯賽或代表隊經歷，簡述即可。', 'Clubs you have played for, leagues you have taken part in, or representative-team experience. A brief summary is fine.') }}</p>
              <p class="field-error" id="p-experience-error" role="alert">{{ tx('請簡述你的足球經歷', 'Please briefly describe your football experience') }}</p>
            </div>
            <div class="form-field form-field--full">
              <label for="p-video">{{ tx('比賽或訓練影片連結', 'Match or training video link') }}<span class="req" aria-hidden="true">*</span></label>
              <input id="p-video" v-model="videoUrl" type="url" name="video_url" required placeholder="https://" aria-describedby="p-video-hint p-video-error">
              <p class="field-hint" id="p-video-hint">{{ tx('YouTube、雲端硬碟等可公開觀看的連結，有助於加快評估。', 'A publicly viewable link (YouTube, cloud storage, etc.) helps us assess your application faster.') }}</p>
              <p class="field-error" id="p-video-error" role="alert">{{ tx('請提供影片連結', 'Please provide a video link') }}</p>
            </div>
          </div>
        </fieldset>

        <fieldset>
          <legend>{{ tx('聯絡方式', 'Contact details') }}</legend>
          <div class="form-grid">
            <div class="form-field">
              <label for="p-phone">{{ tx('聯絡電話', 'Phone number') }}<span class="req" aria-hidden="true">*</span></label>
              <input id="p-phone" v-model="phone" type="tel" name="phone" required autocomplete="tel" aria-describedby="p-phone-error">
              <p class="field-error" id="p-phone-error" role="alert">{{ tx('請填寫聯絡電話', 'Please enter your phone number') }}</p>
            </div>
            <div class="form-field">
              <label for="p-email">Email<span class="req" aria-hidden="true">*</span></label>
              <input id="p-email" v-model="email" type="email" name="email" required autocomplete="email" aria-describedby="p-email-error">
              <p class="field-error" id="p-email-error" role="alert">{{ tx('請填寫有效的 Email', 'Please enter a valid email address') }}</p>
            </div>
          </div>
        </fieldset>

        <div class="consent-block">
          <p v-if="isEn" class="field-hint">The consent notice below is pending legal review and is shown in Traditional Chinese.</p>
          <div class="checkbox-field">
            <input id="p-consent" v-model="consent" type="checkbox" name="consent" required aria-describedby="p-consent-error">
            <label for="p-consent">我已閱讀並同意<a :href="lp('/zh/privacy/')">隱私權政策</a>，並同意{{ clubAssets.nameZh }}依本表單蒐集之個人資料，用於處理本次加入球隊申請之聯繫與評估作業。<span class="req" aria-hidden="true">*</span></label>
          </div>
          <p class="field-error" id="p-consent-error" role="alert">{{ tx('請勾選同意個資蒐集聲明', 'Please tick the box to agree to the personal data collection notice') }}</p>
          
        </div>

        <div class="form-turnstile">
          <!-- Cloudflare Turnstile 防機器人驗證：sitekey 待客戶申請 Cloudflare 帳號後設定，見 https://developers.cloudflare.com/turnstile/ -->
          <div class="cf-turnstile" data-sitekey="" role="group" :aria-label="tx('機器人驗證', 'Bot verification')"></div>
          <p class="field-hint">{{ tx('此表單由 Cloudflare Turnstile 防護，驗證元件將於 sitekey 設定後生效。', 'This form is protected by Cloudflare Turnstile. The verification widget will take effect once the site key is configured.') }}</p>
        </div>

        <button class="btn btn--primary btn--block" type="submit" :disabled="status === 'submitting'">{{ tx('送出報名', 'Submit') }}</button>

        <div class="form-submit-note">
          <p><template v-if="isEn"><strong>What happens after you submit?</strong> An automatic reply is sent straight away to the email address you provided to confirm we have received your details. The {{ deptLabel }} also receives a notification and will follow up depending on the assessment (for example, with a trial invitation).</template><template v-else><strong>送出後會發生什麼事？</strong> 系統會立即寄送自動回覆信到你填寫的 Email，確認我們已收到資料；{{ deptLabel }}窗口會另外收到通知信，並視評估結果安排後續聯繫（如試訓邀請）。</template></p>
        </div>
      </form>

      <aside class="form-sidebar">
        <div class="form-sidebar__sticky">
        <div class="form-sidebar__card">
          <p class="form-sidebar__dept">{{ tx('收件單位', 'Handled by') }}</p>
          <h2>{{ deptLabel }}</h2>
          
        </div>
        <div class="form-sidebar__card">
          <h2>{{ tx('填寫前可以先準備', 'Before you start') }}</h2>
          <ul>
            <li>{{ tx('近期比賽或訓練影片連結', 'A link to a recent match or training video') }}</li>
            <li>{{ tx('可聯絡到本人的電話與 Email', 'A phone number and email address where we can reach you') }}</li>
          </ul>
        </div>
        </div>
      </aside>
    </div>
  </div>
</section>
</template>

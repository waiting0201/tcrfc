<script setup lang="ts">
// app/pages/zh/join/camp-registration/index.vue — 由 site/src/pages/zh/join/camp-registration/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
//
// S1-17：接上 POST /api/v1/{club}/forms/camp_registration/submissions。
// 🔴 S1-17 收尾修正（2026-09-29，完整說明見 apps/web/README.md「S1-17」節「規格疑點」）：
// 後端 `health_declaration` 是 `consent`（布林同意）型別（db/seed/generate-club-seed-sql.py
// FORM_FIELD_DEFAULTS["camp_registration"]），不是自由文字——原本把健康聲明文字併入
// `contact` 欄位的暫行寫法已移除（那樣會讓「緊急聯絡人」欄位混入不相干的健康資訊，且使用者
// 看到的是一個文字框、送出時卻被系統當成單純打勾，一樣是「畫面與送出行為不一致」）。
// 現在畫面上是**一個勾選框**，文案為中性聲明（見下方 template），與後端 consent 型別一致。
// ⚠️ 代價：**營隊實際的健康狀況細節（過敏史、慢性病、服用藥物）目前完全沒有欄位可以收**——
// 這是規格疑點，已在 README 與任務回報列出，建議下一輪把 `health_declaration` 欄位型別改為
// `textarea`，讓真正的健康內容有地方存。
//
// 其餘欄位對應：session_choice = 希望報名的梯次／name = 學員姓名／birth_date = 學員出生日期／
// contact = 緊急聯絡人姓名＋關係＋電話。**家長聯絡資料（parent_name／parent_phone／
// parent_email）已從畫面移除**——規劃書 §3.10 10.3 的欄位定義只列「營隊梯次、學員資料、
// 健康聲明、緊急聯絡人」，沒有「家長聯絡」這一項（跟 10.2 不同），後端也沒有對應鍵。
// ⚠️ 規格疑點：緊急聯絡人未必是家長本人，若客戶希望營隊報名也收家長聯絡方式，需先確認規格
// 再新增欄位，不能自行加回畫面。
definePageMeta({ nav: '', unit: '10.3', enReady: true, enReadyBw: true })

const { lp, isEn, tx } = useLocale()
const config = useRuntimeConfig()
// BW-C1（品牌外洩全站盤點）：改讀既有的 getClubAssets()。⚠️ 規格疑點：藍鯨目前沒有
// 已開放的寒／暑令營活動（5.2／5.3 現況皆為「尚未推出」，見 club-copy.ts
// getSummerCampSeo／getWinterCampSeo bw 分支），本頁描述改為中性用詞，不臆造藍鯨的
// 具體營隊名稱；表單本身是否該對藍鯨開放，留待營隊真的推出時再確認（不在本輪自行
// 決定關閉）。
const clubAssets = computed(() => getClubAssets(config.public.club))
const clubEn = computed(() => bizClubNameEn(config.public.club))

useSeoMeta({
  title: computed(() => (isEn.value ? `Camp Registration | Join / Contact | ${clubEn.value}` : `營隊報名 Camp Registration｜加入與聯絡｜${clubAssets.value.nameZh}`)),
  description: computed(() => (isEn.value ? `Register for the ${clubEn.value} winter and summer football camps. Complete the student details, your preferred session, health declaration and emergency contact, and the Programs Department will confirm the session and availability with parents.` : `報名${clubAssets.value.shortNameZh}寒暑假足球營隊。填寫學員資料、希望報名的梯次、健康聲明與緊急聯絡人，課程部將盡快與家長確認梯次與名額。`)),
})

const EMERGENCY_RELATION_LABELS: Record<string, string> = {
  father: '父親', mother: '母親', grandparent: '祖父母／外祖父母', relative: '其他親屬', other: '其他',
}

const campSession = ref('')
const studentName = ref('')
const studentDob = ref('')
const healthDeclarationConsent = ref(false)
const emergencyName = ref('')
const emergencyPhone = ref('')
const emergencyRelation = ref('')
const consent = ref(false)
const website = ref('')

const { status, errorMessage, submit, siteKey, captchaActive, captchaWidget, onCaptchaToken } = useFormSubmit('camp_registration')

async function onSubmit() {
  const relationLabel = EMERGENCY_RELATION_LABELS[emergencyRelation.value] ?? emergencyRelation.value

  await submit({
    session_choice: campSession.value,
    name: studentName.value,
    birth_date: studentDob.value,
    health_declaration: healthDeclarationConsent.value ? 'true' : '',
    contact: `${emergencyName.value}（${relationLabel}）：${emergencyPhone.value}`,
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
      <li aria-current="page">{{ tx('營隊報名', 'Camp Registration') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">10.3</span>
  <div class="container">
    <p class="page-hero__eyebrow">10.3 Camp Registration</p>
    <h1><template v-if="isEn">Camp Registration</template><template v-else>{{ tx('營隊報名', 'Camp Registration') }}<span class="en">Camp Registration</span></template></h1>
    <p class="page-hero__lede"><template v-if="isEn">Short football camps during the winter and summer breaks give children intensive training and a quick way to build match experience. Parents, please fill in the details below, and the Programs Department will contact you once the session and availability are confirmed.</template><template v-else>寒暑假期間的短期足球營隊，讓孩子在密集訓練中快速累積比賽經驗。請家長協助填寫以下資料，課程部將確認梯次與名額後與你聯繫。</template></p>
  </div>
</section>

<section class="band form-band" aria-labelledby="form-title">
  <div class="container">
    <h2 class="visually-hidden" id="form-title">{{ tx('營隊報名表單', 'Camp registration form') }}</h2>
    <div class="form-layout form-layout--split">
      <FormStatusBanner
        :status="status"
        :error-message="errorMessage"
        :success-message="tx('已收到營隊報名資料！系統已寄送自動回覆信到家長填寫的 Email，課程部會盡快確認梯次與名額。', 'We have received the camp registration! An automatic reply has been sent to the parent\'s email address, and the Programs Department will confirm the session and availability shortly.')"
      />
      <form v-if="status !== 'success'" class="tcrfc-form" action="" method="post" @submit.prevent="onSubmit">
        <!-- action 留空：本站為純靜態站，實際送出（寄發自動回覆信／通知信／寫入後台）由後端或第三方表單服務接手，此處僅完成前端欄位配置與必填驗證骨架 -->
        <HoneypotField v-model="website" />

        <fieldset>
          <legend>{{ tx('營隊梯次', 'Camp session') }}</legend>
          <div class="form-grid">
            <div class="form-field form-field--full">
              <label for="cp-session">{{ tx('希望報名的梯次', 'Preferred session') }}<span class="req" aria-hidden="true">*</span></label>
              <input id="cp-session" v-model="campSession" type="text" name="camp_session" required :placeholder="tx('例：2027 年暑假足球營', 'e.g. 2027 summer football camp')" aria-describedby="cp-session-hint cp-session-error">
              <p class="field-hint" id="cp-session-hint">{{ tx('目前梯次日期尚未公布，請填寫大略期望時段（如寒假／暑假），課程部會與最新開課資訊比對後回覆。', 'Session dates have not been announced yet. Please enter your approximate preferred period (for example, winter or summer break), and the Programs Department will check it against the latest schedule and reply.') }}</p>
              <p class="field-error" id="cp-session-error" role="alert">{{ tx('請填寫希望報名的梯次', 'Please enter your preferred session') }}</p>
            </div>
          </div>
          
        </fieldset>

        <fieldset>
          <legend>{{ tx('學員資料', 'Student details') }}</legend>
          <div class="form-grid">
            <div class="form-field">
              <label for="cp-name">{{ tx('學員姓名', "Student's name") }}<span class="req" aria-hidden="true">*</span></label>
              <input id="cp-name" v-model="studentName" type="text" name="student_name" required autocomplete="name" aria-describedby="cp-name-error">
              <p class="field-error" id="cp-name-error" role="alert">{{ tx('請填寫學員姓名', 'Please enter the student\'s name') }}</p>
            </div>
            <div class="form-field">
              <label for="cp-dob">{{ tx('學員出生日期', "Student's date of birth") }}<span class="req" aria-hidden="true">*</span></label>
              <input id="cp-dob" v-model="studentDob" type="date" name="student_dob" required aria-describedby="cp-dob-error">
              <p class="field-error" id="cp-dob-error" role="alert">{{ tx('請填寫學員出生日期', 'Please enter the student\'s date of birth') }}</p>
            </div>
            <div class="form-field form-field--full">
              <p v-if="isEn" class="field-hint">The health declaration below is pending legal review and is shown in Traditional Chinese.</p>
              <div class="checkbox-field">
                <input id="cp-health-declaration" v-model="healthDeclarationConsent" type="checkbox" name="health_declaration" required aria-describedby="cp-health-declaration-error">
                <label for="cp-health-declaration">本人確認已據實告知學員的過敏史、慢性病、目前服用藥物等健康狀況，如有變動將主動告知課程部。<span class="req" aria-hidden="true">*</span></label>
              </div>
              <p class="field-error" id="cp-health-declaration-error" role="alert">{{ tx('請勾選健康聲明', 'Please tick the box for the health declaration') }}</p>
            </div>
          </div>
        </fieldset>

        <fieldset>
          <legend>{{ tx('緊急聯絡人', 'Emergency contact') }}</legend>
          <div class="form-grid">
            <div class="form-field">
              <label for="cp-emg-name">{{ tx('緊急聯絡人姓名', 'Emergency contact name') }}<span class="req" aria-hidden="true">*</span></label>
              <input id="cp-emg-name" v-model="emergencyName" type="text" name="emergency_name" required autocomplete="name" aria-describedby="cp-emg-name-error">
              <p class="field-error" id="cp-emg-name-error" role="alert">{{ tx('請填寫緊急聯絡人姓名', 'Please enter the emergency contact name') }}</p>
            </div>
            <div class="form-field">
              <label for="cp-emg-phone">{{ tx('緊急聯絡人電話', 'Emergency contact phone number') }}<span class="req" aria-hidden="true">*</span></label>
              <input id="cp-emg-phone" v-model="emergencyPhone" type="tel" name="emergency_phone" required autocomplete="tel" aria-describedby="cp-emg-phone-error">
              <p class="field-error" id="cp-emg-phone-error" role="alert">{{ tx('請填寫緊急聯絡人電話', 'Please enter the emergency contact phone number') }}</p>
            </div>
            <div class="form-field form-field--full">
              <label for="cp-emg-relation">{{ tx('與學員關係', 'Relationship to the student') }}<span class="req" aria-hidden="true">*</span></label>
              <select id="cp-emg-relation" v-model="emergencyRelation" name="emergency_relation" required aria-describedby="cp-emg-relation-error">
                <option value="">{{ tx('請選擇', 'Please select') }}</option>
                <option value="father">{{ tx('父親', 'Father') }}</option>
                <option value="mother">{{ tx('母親', 'Mother') }}</option>
                <option value="grandparent">{{ tx('祖父母／外祖父母', 'Grandparent') }}</option>
                <option value="relative">{{ tx('其他親屬', 'Other relative') }}</option>
                <option value="other">{{ tx('其他', 'Other') }}</option>
              </select>
              <p class="field-error" id="cp-emg-relation-error" role="alert">{{ tx('請選擇與學員關係', 'Please select your relationship to the student') }}</p>
            </div>
          </div>
        </fieldset>

        <div class="consent-block">
          <p v-if="isEn" class="field-hint">The consent notice below is pending legal review and is shown in Traditional Chinese.</p>
          <div class="checkbox-field">
            <input id="cp-consent" v-model="consent" type="checkbox" name="consent" required aria-describedby="cp-consent-error">
            <label for="cp-consent">本人為上述學員之家長／法定監護人，已閱讀並同意<a :href="lp('/zh/privacy/')">隱私權政策</a>，並同意{{ clubAssets.nameZh }}依本表單蒐集學員、家長與緊急聯絡人之個人資料及健康聲明內容，用於處理本次營隊報名之聯繫、安全與應變作業。<span class="req" aria-hidden="true">*</span></label>
          </div>
          <p class="field-error" id="cp-consent-error" role="alert">{{ tx('請勾選同意個資蒐集聲明', 'Please tick the box to agree to the personal data collection notice') }}</p>
          
        </div>

        <div v-if="captchaActive" class="form-turnstile">
          <FormTurnstile ref="captchaWidget" :site-key="siteKey" @token="onCaptchaToken" />
        </div>

        <button class="btn btn--primary btn--block" type="submit" :disabled="status === 'submitting'">{{ tx('送出報名', 'Submit') }}</button>

        <div class="form-submit-note">
          <p><template v-if="isEn"><strong>What happens after you submit?</strong> An automatic reply is sent straight away to the parent's email address to confirm we have received the details. The Programs Department also receives a notification and will contact parents to confirm the session, availability and payment method.</template><template v-else><strong>送出後會發生什麼事？</strong> 系統會立即寄送自動回覆信到家長填寫的 Email，確認我們已收到資料；課程部窗口會另外收到通知信，並與家長確認梯次、名額與繳費方式。</template></p>
        </div>
      </form>

      <aside class="form-sidebar">
        <div class="form-sidebar__sticky">
        <div class="form-sidebar__card">
          <p class="form-sidebar__dept">{{ tx('收件單位', 'Handled by') }}</p>
          <h2>{{ tx('課程部', 'Programs Department') }}</h2>
          
        </div>
        <div class="form-sidebar__card">
          <h2>{{ tx('填寫前可以先準備', 'Before you start') }}</h2>
          <ul>
            <li>{{ tx('學員的健康與過敏史資訊', 'The student\'s health and allergy information') }}</li>
            <li>{{ tx('一位可隨時聯絡到的緊急聯絡人', 'An emergency contact who can be reached at any time') }}</li>
            <li>{{ tx('希望報名的梯次或時段', 'The session or time period you would like to join') }}</li>
          </ul>
        </div>
        </div>
      </aside>
    </div>
  </div>
</section>
</template>

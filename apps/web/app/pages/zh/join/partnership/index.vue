<script setup lang="ts">
// app/pages/zh/join/partnership/index.vue — 由 site/src/pages/zh/join/partnership/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
//
// S1-17：接上 POST /api/v1/{club}/forms/partnership_sponsorship/submissions。
// 🔴 欄位對應（完整決策見 apps/web/README.md「S1-17」節）：
//   enquiry_type：mockup 送英文代碼（partnership／sponsorship／both），後端 select 選項是
//     封閉的中文字面值 ["合作夥伴","贊助","兩者"]（見 db/seed/generate-club-seed-sql.py），
//     用對照表轉換，不是新增欄位。
//   industry／budget_range：後端是 text 型別（無選項限制），直接送選單的中文顯示文字。
//   cooperation_direction：後端題目文字是「合作方向」，內容併入「合作方向」checkbox 群組
//     選取的項目名稱＋「合作構想或洽詢內容」欄位原文，兩者本來就是同一個規劃書段落
//     （9.4／10.5）的同一組資訊，合併不算新增欄位。
//   sponsorship_interest：併入「感興趣的贊助方案」checkbox 群組選取的項目名稱。
//   name = contact_name／contact = phone＋email 合併。
// 🔴 S1-17 收尾修正（2026-09-29）：tax_id（統一編號）、contact_title（職稱）、doc_file
// （提案文件上傳）在規格（§3.10 10.5「主要欄位」）與後端都沒有定義，原本畫面留著卻悄悄不
// 送出，現已**從畫面移除**。
definePageMeta({ nav: '', unit: '10.5', enReady: true, enReadyBw: true })

const { lp, isEn, tx } = useLocale()
const config = useRuntimeConfig()
// BW-C1（品牌外洩全站盤點）：改讀既有的 getClubAssets()／getClubIdentity()。
// `PLAN_LABELS.academy` 原本寫死「學院贊助」，藍鯨依 docs/13-blue-whale-site.md §3
// 一律稱「青年隊」，改為 computed 依 identity.academyShortLabelZh 組字。
const clubAssets = computed(() => getClubAssets(config.public.club))
const identity = computed(() => getClubIdentity(config.public.club))
const isTcrfc = computed(() => config.public.club !== 'bw')
const seoEnBw = getPartnershipFormSeoEnBw()
// 藍鯨規劃書 v1.9 §2.1：藍鯨不設 8.1 漫畫，贊助方案不列「漫畫內容合作」。
const mangaEnabled = computed(() => isUnitEnabledForClub('8.1', config.public.club))

useSeoMeta({
  title: computed(() => (isEn.value ? (isTcrfc.value ? 'Partnership & Sponsorship | Join / Contact | Taichung Rock FC' : seoEnBw.title) : `合作夥伴與贊助洽詢 Partnership & Sponsorship｜加入與聯絡｜${clubAssets.value.nameZh}`)),
  description: computed(() => (isEn.value ? (!isTcrfc.value ? seoEnBw.description : 'Talk to Taichung Rock FC about partnership or sponsorship. Complete one form with your company details, partnership direction or the sponsorship packages you are interested in, and your budget range, and our Partnerships Department will be in touch.') : `與${clubAssets.value.nameZh}洽談合作或贊助。一份表單填寫公司資料、合作方向或感興趣的贊助方案與預算區間，商務部將盡快與你聯繫。`)),
})

const ENQUIRY_TYPE_MAP: Record<string, string> = { partnership: '合作夥伴', sponsorship: '贊助', both: '兩者' }
const INDUSTRY_LABELS: Record<string, string> = {
  brand: '品牌／零售', education: '教育', tech: '科技', media: '媒體／娛樂', finance: '金融／保險',
  sports: '運動產業', public: '政府／公益機構', other: '其他',
}
const BUDGET_LABELS: Record<string, string> = {
  'under-100k': 'NT$10 萬以下', '100k-500k': 'NT$10 萬–50 萬', '500k-1m': 'NT$50 萬–100 萬',
  'over-1m': 'NT$100 萬以上', undecided: '尚未定案，希望先了解方案',
}
const DIRECTION_LABELS: Record<string, string> = {
  strategic: '策略夥伴', international: '國際夥伴', training: '訓練夥伴', education: '教育夥伴', brand: '品牌夥伴',
}
const PLAN_LABEL_MAP = computed<Record<string, string>>(() => ({
  club: '俱樂部贊助', academy: `${identity.value.academyShortLabelZh}贊助`, team: '球隊贊助', camp: '營隊贊助', international: '國際計畫贊助',
  manga: '漫畫內容合作', merchandise: '商品合作', fan_club: '球迷會贊助', naming_rights: '場館冠名',
}))

const enquiryType = ref('')
const companyName = ref('')
const industry = ref('')
const budgetRange = ref('')
const contactName = ref('')
const phone = ref('')
const email = ref('')
const direction = ref<string[]>([])
const plan = ref<string[]>([])
const proposal = ref('')
const consent = ref(false)
const website = ref('')

const { status, errorMessage, submit, siteKey, captchaActive, captchaWidget, onCaptchaToken } = useFormSubmit('partnership_sponsorship')

async function onSubmit() {
  const directionLabels = direction.value.map((v) => DIRECTION_LABELS[v] ?? v)
  const planLabels = plan.value.map((v) => PLAN_LABEL_MAP.value[v] ?? v)

  await submit({
    enquiry_type: ENQUIRY_TYPE_MAP[enquiryType.value] ?? enquiryType.value,
    company: companyName.value,
    industry: INDUSTRY_LABELS[industry.value] ?? industry.value,
    budget_range: BUDGET_LABELS[budgetRange.value] ?? budgetRange.value,
    cooperation_direction: [directionLabels.length ? `合作方向：${directionLabels.join('、')}` : '', proposal.value].filter(Boolean).join('｜'),
    sponsorship_interest: planLabels.join('、'),
    name: contactName.value,
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
      <li aria-current="page">{{ tx('合作夥伴與贊助洽詢', 'Partnership & Sponsorship') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">10.5</span>
  <div class="container">
    <p class="page-hero__eyebrow">10.5 Partnership &amp; Sponsorship</p>
    <h1><template v-if="isEn">Partnership &amp; Sponsorship</template><template v-else>{{ tx('合作夥伴與贊助洽詢', 'Partnership & Sponsorship') }}<span class="en">Partnership &amp; Sponsorship</span></template></h1>
    <p v-if="isTcrfc" class="page-hero__lede"><template v-if="isEn">Join forces with Taichung Rock FC to reach the local community through a professional football platform and create value for your brand and the community alike. Whether you want to discuss a long-term partnership or a specific sponsorship package, this one form covers it. Fill in the details below and our Partnerships Department will get in touch to discuss the details.</template><template v-else>攜手台中磐石，透過職業足球平台觸及在地社群，共創品牌與社區的雙贏價值。不論你想談的是長期合作夥伴關係，或是特定的贊助方案，都在這一份表單完成。填寫以下資料，商務部將盡快與你聯繫討論細節。</template></p>
    <p v-else class="page-hero__lede"><template v-if="isEn">{{ PARTNERSHIP_FORM_LEDE_EN_BW }}</template><template v-else>攜手台中藍鯨，透過女子足球平台觸及在地社群，共創品牌與社區的雙贏價值。不論你想談的是長期合作夥伴關係，或是特定的贊助方案，都在這一份表單完成。填寫以下資料，俱樂部將盡快與你聯繫討論細節。</template></p>
  </div>
</section>

<section class="band form-band" aria-labelledby="form-title">
  <div class="container">
    <h2 class="visually-hidden" id="form-title">{{ tx('合作夥伴與贊助洽詢表單', 'Partnership and sponsorship enquiry form') }}</h2>
    <div class="form-layout form-layout--split">
      <FormStatusBanner
        :status="status"
        :error-message="errorMessage"
        :success-message="tx('已收到你的洽詢資料！系統已寄送自動回覆信到你填寫的 Email，商務部會盡快安排後續討論會議。', 'We have received your enquiry! An automatic reply has been sent to the email address you provided, and our Partnerships Department will arrange a follow-up meeting shortly.')"
      />
      <form v-if="status !== 'success'" class="tcrfc-form" action="" method="post" @submit.prevent="onSubmit">
        <!-- action 留空：本站為純靜態站，實際送出（寄發自動回覆信／通知信／寫入後台）由後端或第三方表單服務接手，此處僅完成前端欄位配置與必填驗證骨架 -->
        <HoneypotField v-model="website" />

        <fieldset>
          <legend>{{ tx('洽詢類型', 'Enquiry type') }}</legend>
          <div class="form-grid">
            <div class="form-field form-field--full">
              <label for="pn-type">{{ tx('這次想洽詢的是', 'This enquiry is about') }}<span class="req" aria-hidden="true">*</span></label>
              <select id="pn-type" v-model="enquiryType" name="enquiry_type" required aria-describedby="pn-type-error pn-type-hint">
                <option value="">{{ tx('請選擇', 'Please select') }}</option>
                <option value="partnership">{{ tx('合作夥伴關係', 'A partnership') }}</option>
                <option value="sponsorship">{{ tx('贊助方案', 'A sponsorship package') }}</option>
                <option value="both">{{ tx('兩者都想了解', 'Both') }}</option>
              </select>
              <p class="field-error" id="pn-type-error" role="alert">{{ tx('請選擇洽詢類型', 'Please select an enquiry type') }}</p>
              <p class="field-hint" id="pn-type-hint">{{ tx('合作夥伴偏向長期資源互換與共同計畫，贊助則對應特定方案與權益。下方兩區可依需求擇一或同時勾選。', 'Partnerships are about long-term resource exchange and joint projects, while sponsorship relates to specific packages and benefits. In the two sections below, you can tick either one or both.') }}</p>
            </div>
          </div>
        </fieldset>

        <fieldset>
          <legend>{{ tx('公司／機構資料', 'Company / organisation details') }}</legend>
          <div class="form-grid">
            <div class="form-field">
              <label for="pn-company">{{ tx('公司／機構名稱', 'Company / organisation name') }}<span class="req" aria-hidden="true">*</span></label>
              <input id="pn-company" v-model="companyName" type="text" name="company_name" required autocomplete="organization" aria-describedby="pn-company-error">
              <p class="field-error" id="pn-company-error" role="alert">{{ tx('請填寫公司／機構名稱', 'Please enter your company / organisation name') }}</p>
            </div>
            <div class="form-field">
              <label for="pn-industry">{{ tx('產業別', 'Industry') }}</label>
              <select id="pn-industry" v-model="industry" name="industry">
                <option value="">{{ tx('請選擇', 'Please select') }}</option>
                <option value="brand">{{ tx('品牌／零售', 'Brand / retail') }}</option>
                <option value="education">{{ tx('教育', 'Education') }}</option>
                <option value="tech">{{ tx('科技', 'Technology') }}</option>
                <option value="media">{{ tx('媒體／娛樂', 'Media / entertainment') }}</option>
                <option value="finance">{{ tx('金融／保險', 'Finance / insurance') }}</option>
                <option value="sports">{{ tx('運動產業', 'Sports industry') }}</option>
                <option value="public">{{ tx('政府／公益機構', 'Government / non-profit') }}</option>
                <option value="other">{{ tx('其他', 'Other') }}</option>
              </select>
            </div>
            <div class="form-field">
              <label for="pn-budget">{{ tx('預算區間', 'Budget range') }}</label>
              <select id="pn-budget" v-model="budgetRange" name="budget_range">
                <option value="">{{ tx('請選擇（選填）', 'Please select (optional)') }}</option>
                <option value="under-100k">{{ tx('NT$10 萬以下', 'Under NT$100,000') }}</option>
                <option value="100k-500k">{{ tx('NT$10 萬–50 萬', 'NT$100,000-500,000') }}</option>
                <option value="500k-1m">{{ tx('NT$50 萬–100 萬', 'NT$500,000-1,000,000') }}</option>
                <option value="over-1m">{{ tx('NT$100 萬以上', 'Over NT$1,000,000') }}</option>
                <option value="undecided">{{ tx('尚未定案，希望先了解方案', 'Not decided yet - I would like to learn about the packages first') }}</option>
              </select>
            </div>
          </div>
        </fieldset>

        <fieldset>
          <legend>{{ tx('聯絡人資料', 'Contact person') }}</legend>
          <div class="form-grid">
            <div class="form-field">
              <label for="pn-contact-name">{{ tx('聯絡人姓名', 'Contact name') }}<span class="req" aria-hidden="true">*</span></label>
              <input id="pn-contact-name" v-model="contactName" type="text" name="contact_name" required autocomplete="name" aria-describedby="pn-contact-name-error">
              <p class="field-error" id="pn-contact-name-error" role="alert">{{ tx('請填寫聯絡人姓名', 'Please enter the contact name') }}</p>
            </div>
            <div class="form-field">
              <label for="pn-phone">{{ tx('聯絡電話', 'Phone number') }}<span class="req" aria-hidden="true">*</span></label>
              <input id="pn-phone" v-model="phone" type="tel" name="phone" required autocomplete="tel" aria-describedby="pn-phone-error">
              <p class="field-error" id="pn-phone-error" role="alert">{{ tx('請填寫聯絡電話', 'Please enter a phone number') }}</p>
            </div>
            <div class="form-field">
              <label for="pn-email">Email<span class="req" aria-hidden="true">*</span></label>
              <input id="pn-email" v-model="email" type="email" name="email" required autocomplete="email" aria-describedby="pn-email-error">
              <p class="field-error" id="pn-email-error" role="alert">{{ tx('請填寫有效的 Email', 'Please enter a valid email address') }}</p>
            </div>
          </div>
        </fieldset>

        <fieldset>
          <legend>{{ tx('合作方向', 'Partnership direction') }}</legend>
          <div class="checkbox-group" role="group" aria-labelledby="pn-direction-legend">
            <p id="pn-direction-legend" class="field-hint" style="margin-top:0">{{ tx('可複選；若這次只洽詢贊助方案，本區可略過。', 'Multiple choices allowed. If you are only enquiring about sponsorship packages, you can skip this section.') }}</p>
            <div class="checkbox-field">
              <input id="pn-dir-strategic" v-model="direction" type="checkbox" name="direction" value="strategic">
              <label for="pn-dir-strategic">{{ tx('策略夥伴', 'Strategic partner') }}</label>
            </div>
            <div class="checkbox-field">
              <input id="pn-dir-international" v-model="direction" type="checkbox" name="direction" value="international">
              <label for="pn-dir-international">{{ tx('國際夥伴', 'International partner') }}</label>
            </div>
            <div class="checkbox-field">
              <input id="pn-dir-training" v-model="direction" type="checkbox" name="direction" value="training">
              <label for="pn-dir-training">{{ tx('訓練夥伴', 'Training partner') }}</label>
            </div>
            <div class="checkbox-field">
              <input id="pn-dir-education" v-model="direction" type="checkbox" name="direction" value="education">
              <label for="pn-dir-education">{{ tx('教育夥伴', 'Education partner') }}</label>
            </div>
            <div class="checkbox-field">
              <input id="pn-dir-brand" v-model="direction" type="checkbox" name="direction" value="brand">
              <label for="pn-dir-brand">{{ tx('品牌夥伴', 'Brand partner') }}</label>
            </div>
          </div>
        </fieldset>

        <fieldset>
          <legend>{{ tx('感興趣的贊助方案', 'Sponsorship packages you are interested in') }}</legend>
          <div class="checkbox-group" role="group" aria-labelledby="pn-plan-legend">
            <p id="pn-plan-legend" class="field-hint" style="margin-top:0">{{ tx('可複選；若這次只洽詢合作夥伴關係，本區可略過。', 'Multiple choices allowed. If you are only enquiring about partnerships, you can skip this section.') }}</p>
            <div class="checkbox-field">
              <input id="pn-plan-club" v-model="plan" type="checkbox" name="plan" value="club">
              <label for="pn-plan-club">{{ tx('俱樂部贊助', 'Club sponsorship') }}</label>
            </div>
            <div class="checkbox-field">
              <input id="pn-plan-academy" v-model="plan" type="checkbox" name="plan" value="academy">
              <label for="pn-plan-academy">{{ isEn ? (isTcrfc ? 'Academy sponsorship' : PARTNERS_PLAN_YOUTH_LABEL_EN_BW) : PLAN_LABEL_MAP.academy }}</label>
            </div>
            <div class="checkbox-field">
              <input id="pn-plan-team" v-model="plan" type="checkbox" name="plan" value="team">
              <label for="pn-plan-team">{{ tx('球隊贊助', 'Team sponsorship') }}</label>
            </div>
            <div class="checkbox-field">
              <input id="pn-plan-camp" v-model="plan" type="checkbox" name="plan" value="camp">
              <label for="pn-plan-camp">{{ tx('營隊贊助', 'Camp sponsorship') }}</label>
            </div>
            <div class="checkbox-field">
              <input id="pn-plan-international" v-model="plan" type="checkbox" name="plan" value="international">
              <label for="pn-plan-international">{{ tx('國際計畫贊助', 'International program sponsorship') }}</label>
            </div>
            <div v-if="mangaEnabled" class="checkbox-field">
              <input id="pn-plan-manga" v-model="plan" type="checkbox" name="plan" value="manga">
              <label for="pn-plan-manga">{{ tx('漫畫內容合作', 'Manga content collaboration') }}</label>
            </div>
            <div class="checkbox-field">
              <input id="pn-plan-merch" v-model="plan" type="checkbox" name="plan" value="merchandise">
              <label for="pn-plan-merch">{{ tx('商品合作', 'Merchandise collaboration') }}</label>
            </div>
            <div class="checkbox-field">
              <input id="pn-plan-fanclub" v-model="plan" type="checkbox" name="plan" value="fan_club">
              <label for="pn-plan-fanclub">{{ tx('球迷會贊助', 'Fan Club sponsorship') }}</label>
            </div>
            <div class="checkbox-field">
              <input id="pn-plan-naming" v-model="plan" type="checkbox" name="plan" value="naming_rights">
              <label for="pn-plan-naming">{{ tx('場館冠名', 'Venue naming rights') }}</label>
            </div>
          </div>
          <div class="form-grid" style="margin-top:1.5rem">
            <div class="form-field form-field--full">
              <label for="pn-idea">{{ tx('合作構想或洽詢內容', 'Your ideas or enquiry') }}<span class="req" aria-hidden="true">*</span></label>
              <textarea id="pn-idea" v-model="proposal" name="proposal" required aria-describedby="pn-idea-error"></textarea>
              <p class="field-error" id="pn-idea-error" role="alert">{{ tx('請說明合作構想或洽詢內容', 'Please describe your ideas or enquiry') }}</p>
            </div>
          </div>
        </fieldset>

        <div class="consent-block">
          <p v-if="isEn" class="field-hint">The consent notice below is pending legal review and is shown in Traditional Chinese.</p>
          <div class="checkbox-field">
            <input id="pn-consent" v-model="consent" type="checkbox" name="consent" required aria-describedby="pn-consent-error">
            <label for="pn-consent">我已閱讀並同意<a :href="lp('/zh/privacy/')">隱私權政策</a>，並同意{{ clubAssets.nameZh }}依本表單蒐集之聯絡人個人資料，用於處理本次合作與贊助洽詢之聯繫與評估作業。<span class="req" aria-hidden="true">*</span></label>
          </div>
          <p class="field-error" id="pn-consent-error" role="alert">{{ tx('請勾選同意個資蒐集聲明', 'Please tick the box to agree to the personal data collection notice') }}</p>

        </div>

        <div v-if="captchaActive" class="form-turnstile">
          <FormTurnstile ref="captchaWidget" :site-key="siteKey" @token="onCaptchaToken" />
        </div>

        <button class="btn btn--primary btn--block" type="submit" :disabled="status === 'submitting'">{{ tx('送出洽詢', 'Submit enquiry') }}</button>

        <div class="form-submit-note">
          <p><template v-if="isEn"><strong>What happens after you submit?</strong> An automatic reply is sent straight away to the email address you provided to confirm we have received your details. Our Partnerships Department also receives a notification and will arrange a follow-up meeting.</template><template v-else><strong>送出後會發生什麼事？</strong> 系統會立即寄送自動回覆信到你填寫的 Email，確認我們已收到資料；商務部窗口會另外收到通知信，並安排後續討論會議。</template></p>
        </div>
      </form>

      <aside class="form-sidebar">
        <div class="form-sidebar__sticky">
        <div class="form-sidebar__card">
          <p class="form-sidebar__dept">{{ tx('收件單位', 'Handled by') }}</p>
          <h2>{{ tx('商務部', 'Partnerships Department') }}</h2>

        </div>
        <div class="form-sidebar__card">
          <h2>{{ tx('延伸閱讀', 'Further reading') }}</h2>
          <ul>
            <li><a :href="lp('/zh/partners/our-partners/')">{{ tx('合作夥伴 Our Partners', 'Our Partners') }}</a></li>
            <li><a :href="lp('/zh/partners/become-a-partner/')">{{ tx('成為合作夥伴 Become a Partner', 'Become a Partner') }}</a></li>
            <li><a :href="lp('/zh/partners/opportunities/')">{{ tx('贊助方案 Sponsorship Opportunities', 'Sponsorship Opportunities') }}</a></li>
            <li><a :href="lp('/zh/partners/our-sponsors/')">{{ tx('贊助商 Our Sponsors', 'Our Sponsors') }}</a></li>
          </ul>
        </div>
        </div>
      </aside>
    </div>
  </div>
</section>
</template>

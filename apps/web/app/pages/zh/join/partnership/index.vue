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
definePageMeta({ nav: '', unit: '10.5' })

const { lp } = useLocale()

useSeoMeta({
  title: '合作夥伴與贊助洽詢 Partnership & Sponsorship｜加入與聯絡｜台中磐石足球俱樂部',
  description:
    '與台中磐石足球俱樂部洽談合作或贊助。一份表單填寫公司資料、合作方向或感興趣的贊助方案與預算區間，商務部將盡快與你聯繫。',
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
const PLAN_LABELS: Record<string, string> = {
  club: '俱樂部贊助', academy: '學院贊助', team: '球隊贊助', camp: '營隊贊助', international: '國際計畫贊助',
  manga: '漫畫內容合作', merchandise: '商品合作', fan_club: '球迷會贊助', naming_rights: '場館冠名',
}

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

const { status, errorMessage, submit } = useFormSubmit('partnership_sponsorship')

async function onSubmit() {
  const directionLabels = direction.value.map((v) => DIRECTION_LABELS[v] ?? v)
  const planLabels = plan.value.map((v) => PLAN_LABELS[v] ?? v)

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
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/join/')">加入與聯絡</a></li>
      <li aria-current="page">合作夥伴與贊助洽詢</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">10.5</span>
  <div class="container">
    <p class="page-hero__eyebrow">10.5 Partnership &amp; Sponsorship</p>
    <h1>合作夥伴與贊助洽詢<span class="en">Partnership &amp; Sponsorship</span></h1>
    <p class="page-hero__lede">攜手台中磐石，透過職業足球平台觸及在地社群，共創品牌與社區的雙贏價值。不論你想談的是長期合作夥伴關係，或是特定的贊助方案，都在這一份表單完成。填寫以下資料，商務部將盡快與你聯繫討論細節。</p>
  </div>
</section>

<section class="band form-band" aria-labelledby="form-title">
  <div class="container">
    <h2 class="visually-hidden" id="form-title">合作夥伴與贊助洽詢表單</h2>
    <div class="form-layout form-layout--split">
      <FormStatusBanner
        :status="status"
        :error-message="errorMessage"
        success-message="已收到你的洽詢資料！系統已寄送自動回覆信到你填寫的 Email，商務部會盡快安排後續討論會議。"
      />
      <form v-if="status !== 'success'" class="tcrfc-form" action="" method="post" @submit.prevent="onSubmit">
        <!-- action 留空：本站為純靜態站，實際送出（寄發自動回覆信／通知信／寫入後台）由後端或第三方表單服務接手，此處僅完成前端欄位配置與必填驗證骨架 -->
        <HoneypotField v-model="website" />

        <fieldset>
          <legend>洽詢類型</legend>
          <div class="form-grid">
            <div class="form-field form-field--full">
              <label for="pn-type">這次想洽詢的是<span class="req" aria-hidden="true">*</span></label>
              <select id="pn-type" v-model="enquiryType" name="enquiry_type" required aria-describedby="pn-type-error pn-type-hint">
                <option value="">請選擇</option>
                <option value="partnership">合作夥伴關係</option>
                <option value="sponsorship">贊助方案</option>
                <option value="both">兩者都想了解</option>
              </select>
              <p class="field-error" id="pn-type-error" role="alert">請選擇洽詢類型</p>
              <p class="field-hint" id="pn-type-hint">合作夥伴偏向長期資源互換與共同計畫，贊助則對應特定方案與權益。下方兩區可依需求擇一或同時勾選。</p>
            </div>
          </div>
        </fieldset>

        <fieldset>
          <legend>公司／機構資料</legend>
          <div class="form-grid">
            <div class="form-field">
              <label for="pn-company">公司／機構名稱<span class="req" aria-hidden="true">*</span></label>
              <input id="pn-company" v-model="companyName" type="text" name="company_name" required autocomplete="organization" aria-describedby="pn-company-error">
              <p class="field-error" id="pn-company-error" role="alert">請填寫公司／機構名稱</p>
            </div>
            <div class="form-field">
              <label for="pn-industry">產業別</label>
              <select id="pn-industry" v-model="industry" name="industry">
                <option value="">請選擇</option>
                <option value="brand">品牌／零售</option>
                <option value="education">教育</option>
                <option value="tech">科技</option>
                <option value="media">媒體／娛樂</option>
                <option value="finance">金融／保險</option>
                <option value="sports">運動產業</option>
                <option value="public">政府／公益機構</option>
                <option value="other">其他</option>
              </select>
            </div>
            <div class="form-field">
              <label for="pn-budget">預算區間</label>
              <select id="pn-budget" v-model="budgetRange" name="budget_range">
                <option value="">請選擇（選填）</option>
                <option value="under-100k">NT$10 萬以下</option>
                <option value="100k-500k">NT$10 萬–50 萬</option>
                <option value="500k-1m">NT$50 萬–100 萬</option>
                <option value="over-1m">NT$100 萬以上</option>
                <option value="undecided">尚未定案，希望先了解方案</option>
              </select>
            </div>
          </div>
        </fieldset>

        <fieldset>
          <legend>聯絡人資料</legend>
          <div class="form-grid">
            <div class="form-field">
              <label for="pn-contact-name">聯絡人姓名<span class="req" aria-hidden="true">*</span></label>
              <input id="pn-contact-name" v-model="contactName" type="text" name="contact_name" required autocomplete="name" aria-describedby="pn-contact-name-error">
              <p class="field-error" id="pn-contact-name-error" role="alert">請填寫聯絡人姓名</p>
            </div>
            <div class="form-field">
              <label for="pn-phone">聯絡電話<span class="req" aria-hidden="true">*</span></label>
              <input id="pn-phone" v-model="phone" type="tel" name="phone" required autocomplete="tel" aria-describedby="pn-phone-error">
              <p class="field-error" id="pn-phone-error" role="alert">請填寫聯絡電話</p>
            </div>
            <div class="form-field">
              <label for="pn-email">Email<span class="req" aria-hidden="true">*</span></label>
              <input id="pn-email" v-model="email" type="email" name="email" required autocomplete="email" aria-describedby="pn-email-error">
              <p class="field-error" id="pn-email-error" role="alert">請填寫有效的 Email</p>
            </div>
          </div>
        </fieldset>

        <fieldset>
          <legend>合作方向</legend>
          <div class="checkbox-group" role="group" aria-labelledby="pn-direction-legend">
            <p id="pn-direction-legend" class="field-hint" style="margin-top:0">可複選；若這次只洽詢贊助方案，本區可略過。</p>
            <div class="checkbox-field">
              <input id="pn-dir-strategic" v-model="direction" type="checkbox" name="direction" value="strategic">
              <label for="pn-dir-strategic">策略夥伴</label>
            </div>
            <div class="checkbox-field">
              <input id="pn-dir-international" v-model="direction" type="checkbox" name="direction" value="international">
              <label for="pn-dir-international">國際夥伴</label>
            </div>
            <div class="checkbox-field">
              <input id="pn-dir-training" v-model="direction" type="checkbox" name="direction" value="training">
              <label for="pn-dir-training">訓練夥伴</label>
            </div>
            <div class="checkbox-field">
              <input id="pn-dir-education" v-model="direction" type="checkbox" name="direction" value="education">
              <label for="pn-dir-education">教育夥伴</label>
            </div>
            <div class="checkbox-field">
              <input id="pn-dir-brand" v-model="direction" type="checkbox" name="direction" value="brand">
              <label for="pn-dir-brand">品牌夥伴</label>
            </div>
          </div>
        </fieldset>

        <fieldset>
          <legend>感興趣的贊助方案</legend>
          <div class="checkbox-group" role="group" aria-labelledby="pn-plan-legend">
            <p id="pn-plan-legend" class="field-hint" style="margin-top:0">可複選；若這次只洽詢合作夥伴關係，本區可略過。</p>
            <div class="checkbox-field">
              <input id="pn-plan-club" v-model="plan" type="checkbox" name="plan" value="club">
              <label for="pn-plan-club">俱樂部贊助</label>
            </div>
            <div class="checkbox-field">
              <input id="pn-plan-academy" v-model="plan" type="checkbox" name="plan" value="academy">
              <label for="pn-plan-academy">學院贊助</label>
            </div>
            <div class="checkbox-field">
              <input id="pn-plan-team" v-model="plan" type="checkbox" name="plan" value="team">
              <label for="pn-plan-team">球隊贊助</label>
            </div>
            <div class="checkbox-field">
              <input id="pn-plan-camp" v-model="plan" type="checkbox" name="plan" value="camp">
              <label for="pn-plan-camp">營隊贊助</label>
            </div>
            <div class="checkbox-field">
              <input id="pn-plan-international" v-model="plan" type="checkbox" name="plan" value="international">
              <label for="pn-plan-international">國際計畫贊助</label>
            </div>
            <div class="checkbox-field">
              <input id="pn-plan-manga" v-model="plan" type="checkbox" name="plan" value="manga">
              <label for="pn-plan-manga">漫畫內容合作</label>
            </div>
            <div class="checkbox-field">
              <input id="pn-plan-merch" v-model="plan" type="checkbox" name="plan" value="merchandise">
              <label for="pn-plan-merch">商品合作</label>
            </div>
            <div class="checkbox-field">
              <input id="pn-plan-fanclub" v-model="plan" type="checkbox" name="plan" value="fan_club">
              <label for="pn-plan-fanclub">球迷會贊助</label>
            </div>
            <div class="checkbox-field">
              <input id="pn-plan-naming" v-model="plan" type="checkbox" name="plan" value="naming_rights">
              <label for="pn-plan-naming">場館冠名</label>
            </div>
          </div>
          <div class="form-grid" style="margin-top:1.5rem">
            <div class="form-field form-field--full">
              <label for="pn-idea">合作構想或洽詢內容<span class="req" aria-hidden="true">*</span></label>
              <textarea id="pn-idea" v-model="proposal" name="proposal" required aria-describedby="pn-idea-error"></textarea>
              <p class="field-error" id="pn-idea-error" role="alert">請說明合作構想或洽詢內容</p>
            </div>
          </div>
        </fieldset>

        <div class="consent-block">
          <div class="checkbox-field">
            <input id="pn-consent" v-model="consent" type="checkbox" name="consent" required aria-describedby="pn-consent-error">
            <label for="pn-consent">我已閱讀並同意<a :href="lp('/zh/privacy/')">隱私權政策</a>，並同意台中磐石足球俱樂部依本表單蒐集之聯絡人個人資料，用於處理本次合作與贊助洽詢之聯繫與評估作業。<span class="req" aria-hidden="true">*</span></label>
          </div>
          <p class="field-error" id="pn-consent-error" role="alert">請勾選同意個資蒐集聲明</p>

        </div>

        <div class="form-turnstile">
          <!-- Cloudflare Turnstile 防機器人驗證：sitekey 待客戶申請 Cloudflare 帳號後設定，見 https://developers.cloudflare.com/turnstile/ -->
          <div class="cf-turnstile" data-sitekey="" role="group" aria-label="機器人驗證"></div>
          <p class="field-hint">此表單由 Cloudflare Turnstile 防護，驗證元件將於 sitekey 設定後生效。</p>
        </div>

        <button class="btn btn--primary btn--block" type="submit" :disabled="status === 'submitting'">送出洽詢</button>

        <div class="form-submit-note">
          <p><strong>送出後會發生什麼事？</strong> 系統會立即寄送自動回覆信到你填寫的 Email，確認我們已收到資料；商務部窗口會另外收到通知信，並安排後續討論會議。</p>
        </div>
      </form>

      <aside class="form-sidebar">
        <div class="form-sidebar__sticky">
        <div class="form-sidebar__card">
          <p class="form-sidebar__dept">收件單位</p>
          <h2>商務部</h2>

        </div>
        <div class="form-sidebar__card">
          <h2>延伸閱讀</h2>
          <ul>
            <li><a :href="lp('/zh/partners/our-partners/')">合作夥伴 Our Partners</a></li>
            <li><a :href="lp('/zh/partners/become-a-partner/')">成為合作夥伴 Become a Partner</a></li>
            <li><a :href="lp('/zh/partners/opportunities/')">贊助方案 Sponsorship Opportunities</a></li>
            <li><a :href="lp('/zh/partners/our-sponsors/')">贊助商 Our Sponsors</a></li>
          </ul>
        </div>
        </div>
      </aside>
    </div>
  </div>
</section>
</template>

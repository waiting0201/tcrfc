<script setup lang="ts">
// app/pages/zh/join/academy/index.vue — 由 site/src/pages/zh/join/academy/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
//
// S1-17：接上 POST /api/v1/{club}/forms/academy_children_training/submissions。
// 🔴 後端欄位對應（完整決策見 apps/web/README.md「S1-17」節）：
//   enrollment_category（select，封閉選項）——mockup 13 個細項（3 梯隊＋3 兒童班別＋6 種
//     專項訓練＋「尚未確定」）對到後端固定 7 選項（3 梯隊＋3 兒童班別＋單一「專項訓練」），
//     6 種專項訓練細項一律收斂為「專項訓練」；「尚未確定，請協助建議」在後端沒有對應選項
//     （封閉選項沒有「不確定」），退回「兒童混齡班」當技術預設值，同時把使用者實際選擇的
//     細項文字（program 顯示文字）併入 experience 欄位開頭，不遺失使用者真正的選擇。
//   name = 學員中文姓名（英文姓名有填就併入括號）／birth_date = 學員出生日期／
//   location_preference = 偏好受訓地點選項的顯示文字／
//   contact = 家長聯絡方式（後端這個鍵的題目文字是「家長聯絡方式」，對應姓名＋關係＋電話＋Email）／
//   experience／health_status = 對應欄位原樣傳遞。
// 性別／居住地區／學員與健康聲明以外的證明文件：後端沒有對應欄位，不送出。
definePageMeta({ nav: '', unit: '10.2' })

const { lp } = useLocale()

// S1-12d 收尾：梯隊代碼與主場改讀 useSiteFacts('tcrfc')（後端公開端點）。
const { academyLabel: tcrfcAcademyLabel, primaryVenue: tcrfcVenue } = useSiteFacts('tcrfc')

useSeoMeta({
  title: '加入學院／兒童訓練 Academy & Children\'s Training｜加入與聯絡｜台中磐石足球俱樂部',
  description: computed(
    () =>
      `為孩子報名台中磐石足球學院 ${tcrfcAcademyLabel()} 梯隊，或兒童訓練與專項訓練各類課程。一份表單填寫學員與家長資料，學院部與課程部將盡快與家長聯繫。`,
  ),
})

const PROGRAM_LABELS: Record<string, string> = {
  'academy-u12': 'U12 梯隊', 'academy-u14': 'U14 梯隊', 'academy-u15': 'U15 梯隊',
  'children-mixed': '混齡班', 'children-beginner': '初學班', 'children-skill': '技巧發展班',
  'specialist-goalkeeper': '專項訓練（守門員）', 'specialist-forward': '專項訓練（前鋒）',
  'specialist-defender': '專項訓練（後衛）', 'specialist-midfield': '專項訓練（中場）',
  'specialist-fitness': '專項訓練（體能與速度）', 'specialist-advanced': '專項訓練（高階訓練）',
  undecided: '尚未確定，請協助建議',
}
/** mockup 13 個細項 → 後端 enrollment_category 封閉選項的 7 選 1（見上方檔頭說明）。 */
const ENROLLMENT_CATEGORY_MAP: Record<string, string> = {
  'academy-u12': '學院 U12', 'academy-u14': '學院 U14', 'academy-u15': '學院 U15',
  'children-mixed': '兒童混齡班', 'children-beginner': '兒童初學班', 'children-skill': '兒童技巧發展班',
  'specialist-goalkeeper': '專項訓練', 'specialist-forward': '專項訓練', 'specialist-defender': '專項訓練',
  'specialist-midfield': '專項訓練', 'specialist-fitness': '專項訓練', 'specialist-advanced': '專項訓練',
  undecided: '兒童混齡班', // 封閉選項沒有「尚未確定」，見上方檔頭說明
}

const program = ref('')
const locationPreference = ref('')
const studentNameZh = ref('')
const studentNameEn = ref('')
const studentDob = ref('')
const experience = ref('')
const healthNote = ref('')
const parentName = ref('')
const parentRelation = ref('')
const parentPhone = ref('')
const parentEmail = ref('')
const consent = ref(false)
const website = ref('')

const PARENT_RELATION_LABELS: Record<string, string> = { father: '父親', mother: '母親', guardian: '法定監護人', other: '其他' }

const { status, errorMessage, submit } = useFormSubmit('academy_children_training')

async function onSubmit() {
  const programLabel = PROGRAM_LABELS[program.value] ?? program.value
  const experienceWithProgram = program.value === 'undecided'
    ? `[報名項目：${programLabel}] ${experience.value}`.trim()
    : experience.value
  const relationLabel = PARENT_RELATION_LABELS[parentRelation.value] ?? parentRelation.value

  await submit({
    enrollment_category: ENROLLMENT_CATEGORY_MAP[program.value] ?? '兒童混齡班',
    name: studentNameEn.value ? `${studentNameZh.value}（${studentNameEn.value}）` : studentNameZh.value,
    birth_date: studentDob.value,
    location_preference: locationPreference.value === 'xitun' ? `${tcrfcVenue.value.nameZh}（主場）` : '尚無偏好，請協助安排',
    contact: [`${parentName.value}（${relationLabel}）`, parentPhone.value, parentEmail.value].filter(Boolean).join(' ／ '),
    experience: experienceWithProgram,
    health_status: healthNote.value,
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
      <li aria-current="page">加入學院／兒童訓練</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">10.2</span>
  <div class="container">
    <p class="page-hero__eyebrow">10.2 Academy &amp; Children's Training</p>
    <h1>加入學院／兒童訓練<span class="en">Academy &amp; Children's Training</span></h1>
    <p class="page-hero__lede">不論是申請加入台中磐石足球學院 U12／U14／U15 梯隊，或是報名兒童訓練與專項訓練的各類課程，都在這一份表單完成。請家長協助填寫以下資料，我們會依你選擇的報名項目轉交學院部或課程部，安排後續試訓與課程說明。</p>
  </div>
</section>

<section class="band form-band" aria-labelledby="form-title">
  <div class="container">
    <h2 class="visually-hidden" id="form-title">加入學院／兒童訓練報名表單</h2>
    <div class="form-layout form-layout--split">
      <FormStatusBanner
        :status="status"
        :error-message="errorMessage"
        success-message="已收到報名資料！系統已寄送自動回覆信到家長填寫的 Email，學院部或課程部會依報名項目盡快聯繫。"
      />
      <form v-if="status !== 'success'" class="tcrfc-form" action="" method="post" @submit.prevent="onSubmit">
        <!-- action 留空：本站為純靜態站，實際送出（寄發自動回覆信／通知信／寫入後台）由後端或第三方表單服務接手，此處僅完成前端欄位配置與必填驗證骨架 -->
        <HoneypotField v-model="website" />

        <fieldset>
          <legend>報名項目</legend>
          <div class="form-grid">
            <div class="form-field form-field--full">
              <label for="a-program">想報名的項目<span class="req" aria-hidden="true">*</span></label>
              <select id="a-program" v-model="program" name="program" required aria-describedby="a-program-error a-program-hint">
                <option value="">請選擇</option>
                <optgroup label="足球學院梯隊">
                  <option value="academy-u12">U12 梯隊</option>
                  <option value="academy-u14">U14 梯隊</option>
                  <option value="academy-u15">U15 梯隊</option>
                </optgroup>
                <optgroup label="兒童訓練班別">
                  <option value="children-mixed">混齡班</option>
                  <option value="children-beginner">初學班</option>
                  <option value="children-skill">技巧發展班</option>
                </optgroup>
                <optgroup label="專項訓練">
                  <option value="specialist-goalkeeper">守門員</option>
                  <option value="specialist-forward">前鋒</option>
                  <option value="specialist-defender">後衛</option>
                  <option value="specialist-midfield">中場</option>
                  <option value="specialist-fitness">體能與速度</option>
                  <option value="specialist-advanced">高階訓練</option>
                </optgroup>
                <option value="undecided">尚未確定，請協助建議</option>
              </select>
              <p class="field-error" id="a-program-error" role="alert">請選擇想報名的項目</p>
              <p class="field-hint" id="a-program-hint">學院梯隊為長期培訓編制，需經試訓；兒童訓練與專項訓練為分級課程，依梯次開課。不確定適合哪一種，選「尚未確定」即可，我們會依學員年齡與程度建議。</p>
            </div>
            <div class="form-field">
              <label for="a-location">偏好受訓地點</label>
              <select id="a-location" v-model="locationPreference" name="location_preference">
                <option value="">尚無偏好，請協助安排</option>
                <option value="xitun">{{ tcrfcVenue.nameZh }}（主場）</option>
              </select>
              <p class="field-hint">如需查詢其他受訓地點，請見<a :href="lp('/zh/join/location/')">場地位置與地圖</a>。</p>
            </div>
          </div>
        </fieldset>

        <fieldset>
          <legend>學員基本資料</legend>
          <div class="form-grid">
            <div class="form-field">
              <label for="a-name-zh">學員中文姓名<span class="req" aria-hidden="true">*</span></label>
              <input id="a-name-zh" v-model="studentNameZh" type="text" name="student_name_zh" required autocomplete="name" aria-describedby="a-name-zh-error">
              <p class="field-error" id="a-name-zh-error" role="alert">請填寫學員中文姓名</p>
            </div>
            <div class="form-field">
              <label for="a-name-en">學員英文姓名</label>
              <input id="a-name-en" v-model="studentNameEn" type="text" name="student_name_en" autocomplete="off">
            </div>
            <div class="form-field">
              <label for="a-dob">學員出生日期<span class="req" aria-hidden="true">*</span></label>
              <input id="a-dob" v-model="studentDob" type="date" name="student_dob" required aria-describedby="a-dob-error">
              <p class="field-error" id="a-dob-error" role="alert">請填寫學員出生日期</p>
            </div>
            <div class="form-field">
              <label for="a-gender">性別<span class="req" aria-hidden="true">*</span></label>
              <select id="a-gender" name="student_gender" required aria-describedby="a-gender-error">
                <option value="">請選擇</option>
                <option value="male">男</option>
                <option value="female">女</option>
                <option value="undisclosed">不願透露</option>
              </select>
              <p class="field-error" id="a-gender-error" role="alert">請選擇性別</p>
            </div>
            <div class="form-field">
              <label for="a-city">居住地區</label>
              <input type="text" id="a-city" name="city" autocomplete="address-level2">
            </div>
            <div class="form-field form-field--full">
              <label for="a-experience">足球或運動經歷（選填）</label>
              <textarea id="a-experience" v-model="experience" name="experience" aria-describedby="a-experience-hint"></textarea>
              <p class="field-hint" id="a-experience-hint">是否曾參加校隊、社區球隊或其他訓練課程，簡述即可；沒有經驗也歡迎報名。</p>
            </div>
            <div class="form-field form-field--full">
              <label for="a-health">特殊健康狀況說明（選填）</label>
              <textarea id="a-health" v-model="healthNote" name="health_note" aria-describedby="a-health-hint"></textarea>
              <p class="field-hint" id="a-health-hint">如過敏、氣喘、慢性病或需特別留意的事項，有助於教練提前準備。</p>
            </div>
          </div>
        </fieldset>

        <fieldset>
          <legend>家長／監護人聯絡資料</legend>
          <div class="form-grid">
            <div class="form-field">
              <label for="a-parent-name">家長／監護人姓名<span class="req" aria-hidden="true">*</span></label>
              <input id="a-parent-name" v-model="parentName" type="text" name="parent_name" required autocomplete="name" aria-describedby="a-parent-name-error">
              <p class="field-error" id="a-parent-name-error" role="alert">請填寫家長／監護人姓名</p>
            </div>
            <div class="form-field">
              <label for="a-parent-relation">與學員關係<span class="req" aria-hidden="true">*</span></label>
              <select id="a-parent-relation" v-model="parentRelation" name="parent_relation" required aria-describedby="a-parent-relation-error">
                <option value="">請選擇</option>
                <option value="father">父親</option>
                <option value="mother">母親</option>
                <option value="guardian">法定監護人</option>
                <option value="other">其他</option>
              </select>
              <p class="field-error" id="a-parent-relation-error" role="alert">請選擇與學員關係</p>
            </div>
            <div class="form-field">
              <label for="a-parent-phone">家長聯絡電話<span class="req" aria-hidden="true">*</span></label>
              <input id="a-parent-phone" v-model="parentPhone" type="tel" name="parent_phone" required autocomplete="tel" aria-describedby="a-parent-phone-error">
              <p class="field-error" id="a-parent-phone-error" role="alert">請填寫家長聯絡電話</p>
            </div>
            <div class="form-field">
              <label for="a-parent-email">家長 Email<span class="req" aria-hidden="true">*</span></label>
              <input id="a-parent-email" v-model="parentEmail" type="email" name="parent_email" required autocomplete="email" aria-describedby="a-parent-email-error">
              <p class="field-error" id="a-parent-email-error" role="alert">請填寫有效的 Email</p>
            </div>
          </div>
        </fieldset>

        <fieldset>
          <legend>上傳資料（選填）</legend>
          <div class="form-grid">
            <div class="form-field form-field--full file-field">
              <label for="a-photo-file">學員照片</label>
              <input type="file" id="a-photo-file" name="photo_file" accept=".jpg,.jpeg,.png" aria-describedby="a-photo-hint">
              <p class="field-hint" id="a-photo-hint">支援 JPG／PNG。</p>
            </div>
            <div class="form-field form-field--full file-field">
              <label for="a-doc-file">健康聲明或相關證明文件</label>
              <input type="file" id="a-doc-file" name="doc_file" accept=".pdf,.jpg,.jpeg,.png" aria-describedby="a-doc-hint">
              <p class="field-hint" id="a-doc-hint">例如健康聲明、過去參賽證明或訓練紀錄，非必填。支援 PDF／JPG／PNG。</p>
            </div>
          </div>
        </fieldset>

        <div class="consent-block">
          <div class="checkbox-field">
            <input id="a-consent" v-model="consent" type="checkbox" name="consent" required aria-describedby="a-consent-error">
            <label for="a-consent">本人為上述學員之家長／法定監護人，已閱讀並同意<a :href="lp('/zh/privacy/')">隱私權政策</a>，並同意台中磐石足球俱樂部依本表單蒐集學員與家長之個人資料，用於處理本次報名之聯繫、試訓與課程安排。<span class="req" aria-hidden="true">*</span></label>
          </div>
          <p class="field-error" id="a-consent-error" role="alert">請勾選同意個資蒐集聲明</p>

        </div>

        <div class="form-turnstile">
          <!-- Cloudflare Turnstile 防機器人驗證：sitekey 待客戶申請 Cloudflare 帳號後設定，見 https://developers.cloudflare.com/turnstile/ -->
          <div class="cf-turnstile" data-sitekey="" role="group" aria-label="機器人驗證"></div>
          <p class="field-hint">此表單由 Cloudflare Turnstile 防護，驗證元件將於 sitekey 設定後生效。</p>
        </div>

        <button class="btn btn--primary btn--block" type="submit" :disabled="status === 'submitting'">送出報名</button>

        <div class="form-submit-note">
          <p><strong>送出後會發生什麼事？</strong> 系統會立即寄送自動回覆信到家長填寫的 Email，確認我們已收到資料；報名學院梯隊由學院部窗口接手安排試訓與說明會，報名兒童訓練或專項訓練則由課程部窗口聯繫確認開課時段。</p>
        </div>
      </form>

      <aside class="form-sidebar">
        <div class="form-sidebar__sticky">
        <div class="form-sidebar__card">
          <p class="form-sidebar__dept">收件單位</p>
          <h2>學院部／課程部</h2>
          <p class="field-hint">依表單中選擇的報名項目轉交對應單位。</p>
        </div>
        <div class="form-sidebar__card">
          <h2>報名項目</h2>
          <ul>
            <li>足球學院：U12／U14／U15 梯隊</li>
            <li>兒童訓練：混齡班／初學班／技巧發展班</li>
            <li>專項訓練：守門員／前鋒／後衛／中場／體能與速度／高階訓練</li>
          </ul>
          <p class="field-hint">不確定適合哪一項也沒關係，可選擇「尚未確定」，我們會依學員年齡與程度建議。</p>
        </div>
        </div>
      </aside>
    </div>
  </div>
</section>
</template>

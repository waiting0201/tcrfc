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
// 🔴 S1-17 收尾修正（2026-09-29）：性別、居住地區、學員照片／健康聲明證明文件這幾個欄位在
// 規格（§3.10「學員資料」）與後端都沒有對應鍵，原本畫面留著卻悄悄不送出，現已**從畫面移除**。
//
// 🔴 BW-C1（品牌外洩全站盤點）：本頁原本整頁固定磐石內容，且把「學院梯隊」「兒童訓練」
// 「專項訓練」三種完全不同性質的報名合併成一份表單——這個合併結構本身只適用磐石：
// 藍鯨青年隊梯隊代碼比磐石少一個（沒有 U14），且藍鯨的兒童訓練／專項訓練（05 單元 5.1／5.4）
// 現況一律現場個人報名，不接這套站內線上流程（見 shared/utils/club-copy.ts
// getProgramsHubEnrolNoteBw 檔頭說明）。故 bw 版簡化為只收「加入青年隊」單一報名項目
// （青年隊各梯隊試訓申請），不提供兒童訓練／專項訓練選項，不得對藍鯨假裝這些課程也接受
// 這份表單線上報名。
definePageMeta({ nav: '', unit: '10.2', enReady: true })

const { lp, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')
const clubAssets = computed(() => getClubAssets(clubKey.value))

// S1-12d 收尾：梯隊代碼與主場改讀 useSiteFacts(clubKey)（後端公開端點，BW-C1 改為動態帶入）。
const { academyLabel, primaryVenue } = useSiteFacts(clubKey.value)

useSeoMeta({
  title: computed(() => (isEn.value
    ? 'Academy & Children\'s Training | Join / Contact | Taichung Rock FC'
    : isTcrfc.value
    ? '加入學院／兒童訓練 Academy & Children\'s Training｜加入與聯絡｜台中磐石足球俱樂部'
    : `加入青年隊 Join Youth Team｜加入與聯絡｜${clubAssets.value.nameZh}`)),
  description: computed(() => (isEn.value
    ? `Register your child for the TCRFC Academy ${academyLabel(' / ')} squads, or for children's training and specialist training programs. Complete one form with the student's and parent's details, and the Academy and Programs departments will contact parents shortly.`
    : isTcrfc.value
    ? `為孩子報名台中磐石足球學院 ${academyLabel()} 梯隊，或兒童訓練與專項訓練各類課程。一份表單填寫學員與家長資料，學院部與課程部將盡快與家長聯繫。`
    : `為孩子報名${clubAssets.value.shortNameZh}青年隊 ${academyLabel()} 試訓。一份表單填寫學員與家長資料，俱樂部將盡快與家長聯繫。`)),
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
    location_preference: locationPreference.value === 'xitun' ? `${primaryVenue.value.nameZh}（主場）` : '尚無偏好，請協助安排',
    contact: [`${parentName.value}（${relationLabel}）`, parentPhone.value, parentEmail.value].filter(Boolean).join(' ／ '),
    experience: experienceWithProgram,
    health_status: healthNote.value,
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
      <li aria-current="page">{{ isEn ? 'Academy & Children\'s Training' : isTcrfc ? '加入學院／兒童訓練' : '加入青年隊' }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">10.2</span>
  <div class="container">
    <p class="page-hero__eyebrow">{{ isTcrfc ? "10.2 Academy & Children's Training" : '10.2 Join Youth Team' }}</p>
    <h1 v-if="isTcrfc"><template v-if="isEn">Academy &amp; Children's Training</template><template v-else>加入學院／兒童訓練<span class="en">Academy &amp; Children's Training</span></template></h1>
    <h1 v-else>加入青年隊<span class="en">Join Youth Team</span></h1>
    <p v-if="isTcrfc" class="page-hero__lede"><template v-if="isEn">Whether you are applying for the TCRFC Academy U12 / U14 / U15 squads or registering for children's training and specialist training programs, this one form covers it all. Parents, please fill in the details below. Depending on the program you choose, we will pass your application to the Academy or Programs department to arrange trials and program information.</template><template v-else>不論是申請加入台中磐石足球學院 U12／U14／U15 梯隊，或是報名兒童訓練與專項訓練的各類課程，都在這一份表單完成。請家長協助填寫以下資料，我們會依你選擇的報名項目轉交學院部或課程部，安排後續試訓與課程說明。</template></p>
    <p v-else class="page-hero__lede">申請加入{{ clubAssets.shortNameZh }}青年隊 {{ academyLabel() }} 試訓，請家長協助填寫以下資料，我們會盡快安排後續試訓與說明。</p>
  </div>
</section>

<section class="band form-band" aria-labelledby="form-title">
  <div class="container">
    <h2 class="visually-hidden" id="form-title">{{ isEn ? 'Academy & Children\'s Training registration form' : isTcrfc ? '加入學院／兒童訓練報名表單' : '加入青年隊報名表單' }}</h2>
    <div class="form-layout form-layout--split">
      <FormStatusBanner
        :status="status"
        :error-message="errorMessage"
        :success-message="isEn
          ? 'We have received the registration! An automatic reply has been sent to the parent\'s email address, and the Academy or Programs department will be in touch shortly, depending on the program chosen.'
          : isTcrfc
          ? '已收到報名資料！系統已寄送自動回覆信到家長填寫的 Email，學院部或課程部會依報名項目盡快聯繫。'
          : '已收到報名資料！系統已寄送自動回覆信到家長填寫的 Email，俱樂部會盡快聯繫。'"
      />
      <form v-if="status !== 'success'" class="tcrfc-form" action="" method="post" @submit.prevent="onSubmit">
        <!-- action 留空：本站為純靜態站，實際送出（寄發自動回覆信／通知信／寫入後台）由後端或第三方表單服務接手，此處僅完成前端欄位配置與必填驗證骨架 -->
        <HoneypotField v-model="website" />

        <fieldset>
          <legend>{{ tx('報名項目', 'Program') }}</legend>
          <div class="form-grid">
            <div class="form-field form-field--full">
              <label for="a-program">{{ tx('想報名的項目', 'Program you would like to join') }}<span class="req" aria-hidden="true">*</span></label>
              <select v-if="isTcrfc" id="a-program" v-model="program" name="program" required aria-describedby="a-program-error a-program-hint">
                <option value="">{{ tx('請選擇', 'Please select') }}</option>
                <optgroup :label="tx('足球學院梯隊', 'Academy squads')">
                  <option value="academy-u12">{{ tx('U12 梯隊', 'U12 squad') }}</option>
                  <option value="academy-u14">{{ tx('U14 梯隊', 'U14 squad') }}</option>
                  <option value="academy-u15">{{ tx('U15 梯隊', 'U15 squad') }}</option>
                </optgroup>
                <optgroup :label="tx('兒童訓練班別', 'Children\'s training classes')">
                  <option value="children-mixed">{{ tx('混齡班', 'Mixed-age class') }}</option>
                  <option value="children-beginner">{{ tx('初學班', 'Beginner class') }}</option>
                  <option value="children-skill">{{ tx('技巧發展班', 'Skills development class') }}</option>
                </optgroup>
                <optgroup :label="tx('專項訓練', 'Specialist training')">
                  <option value="specialist-goalkeeper">{{ tx('守門員', 'Goalkeeper') }}</option>
                  <option value="specialist-forward">{{ tx('前鋒', 'Forward') }}</option>
                  <option value="specialist-defender">{{ tx('後衛', 'Defender') }}</option>
                  <option value="specialist-midfield">{{ tx('中場', 'Midfield') }}</option>
                  <option value="specialist-fitness">{{ tx('體能與速度', 'Fitness and speed') }}</option>
                  <option value="specialist-advanced">{{ tx('高階訓練', 'Advanced training') }}</option>
                </optgroup>
                <option value="undecided">{{ tx('尚未確定，請協助建議', 'Not sure yet - please advise') }}</option>
              </select>
              <!-- bw：只收青年隊各梯隊試訓申請，不提供兒童訓練／專項訓練選項（見本頁檔頭說明）。
                   選項值沿用既有 academy-u15／academy-u12（ENROLLMENT_CATEGORY_MAP 既有對照），
                   只是畫面上不再稱「學院」。 -->
              <select v-else id="a-program" v-model="program" name="program" required aria-describedby="a-program-error a-program-hint">
                <option value="">{{ tx('請選擇', 'Please select') }}</option>
                <option value="academy-u15">U15 青年隊</option>
                <option value="academy-u12">U12 青年隊</option>
              </select>
              <p class="field-error" id="a-program-error" role="alert">{{ tx('請選擇想報名的項目', 'Please select a program') }}</p>
              <p v-if="isTcrfc" class="field-hint" id="a-program-hint"><template v-if="isEn">Academy squads are long-term training squads and require a trial; children's training and specialist training are graded programs that run in sessions. If you are not sure which suits your child, choose "Not sure yet" and we will recommend one based on age and level.</template><template v-else>學院梯隊為長期培訓編制，需經試訓；兒童訓練與專項訓練為分級課程，依梯次開課。不確定適合哪一種，選「尚未確定」即可，我們會依學員年齡與程度建議。</template></p>
              <p v-else id="a-program-hint" class="field-hint">青年隊為長期培訓編制，需經試訓。兒童訓練與專項訓練請見<a :href="lp('/zh/programs/')">推廣活動</a>單元，現場個人報名。</p>
            </div>
            <div class="form-field">
              <label for="a-location">{{ tx('偏好受訓地點', 'Preferred training location') }}</label>
              <select id="a-location" v-model="locationPreference" name="location_preference">
                <option value="">{{ tx('尚無偏好，請協助安排', 'No preference - please arrange for me') }}</option>
                <option value="xitun">{{ isEn ? `${primaryVenue.nameEn ?? primaryVenue.nameZh} (home ground)` : `${primaryVenue.nameZh}（主場）` }}</option>
              </select>
              <p class="field-hint"><template v-if="isEn">For other training locations, see <a :href="lp('/zh/join/location/')">Location &amp; Map</a>.</template><template v-else>如需查詢其他受訓地點，請見<a :href="lp('/zh/join/location/')">場地位置與地圖</a>。</template></p>
            </div>
          </div>
        </fieldset>

        <fieldset>
          <legend>{{ tx('學員基本資料', 'Student details') }}</legend>
          <div class="form-grid">
            <div class="form-field">
              <label for="a-name-zh">{{ tx('學員中文姓名', "Student's name (Chinese)") }}<span class="req" aria-hidden="true">*</span></label>
              <input id="a-name-zh" v-model="studentNameZh" type="text" name="student_name_zh" required autocomplete="name" aria-describedby="a-name-zh-error">
              <p class="field-error" id="a-name-zh-error" role="alert">{{ tx('請填寫學員中文姓名', 'Please enter the student\'s name in Chinese') }}</p>
            </div>
            <div class="form-field">
              <label for="a-name-en">{{ tx('學員英文姓名', 'Student\'s name (English)') }}</label>
              <input id="a-name-en" v-model="studentNameEn" type="text" name="student_name_en" autocomplete="off">
            </div>
            <div class="form-field">
              <label for="a-dob">{{ tx('學員出生日期', "Student's date of birth") }}<span class="req" aria-hidden="true">*</span></label>
              <input id="a-dob" v-model="studentDob" type="date" name="student_dob" required aria-describedby="a-dob-error">
              <p class="field-error" id="a-dob-error" role="alert">{{ tx('請填寫學員出生日期', 'Please enter the student\'s date of birth') }}</p>
            </div>
            <div class="form-field form-field--full">
              <label for="a-experience">{{ tx('足球或運動經歷（選填）', 'Football or sports experience (optional)') }}</label>
              <textarea id="a-experience" v-model="experience" name="experience" aria-describedby="a-experience-hint"></textarea>
              <p class="field-hint" id="a-experience-hint">{{ tx('是否曾參加校隊、社區球隊或其他訓練課程，簡述即可；沒有經驗也歡迎報名。', 'Let us know if they have played for a school team, community team or taken other training programs. A brief summary is fine, and beginners are welcome.') }}</p>
            </div>
            <div class="form-field form-field--full">
              <label for="a-health">{{ tx('特殊健康狀況說明（選填）', 'Special health conditions (optional)') }}</label>
              <textarea id="a-health" v-model="healthNote" name="health_note" aria-describedby="a-health-hint"></textarea>
              <p class="field-hint" id="a-health-hint">{{ tx('如過敏、氣喘、慢性病或需特別留意的事項，有助於教練提前準備。', 'Allergies, asthma, chronic conditions or anything else we should know about, so the coaches can prepare in advance.') }}</p>
            </div>
          </div>
        </fieldset>

        <fieldset>
          <legend>{{ tx('家長／監護人聯絡資料', 'Parent / guardian contact details') }}</legend>
          <div class="form-grid">
            <div class="form-field">
              <label for="a-parent-name">{{ tx('家長／監護人姓名', 'Parent / guardian name') }}<span class="req" aria-hidden="true">*</span></label>
              <input id="a-parent-name" v-model="parentName" type="text" name="parent_name" required autocomplete="name" aria-describedby="a-parent-name-error">
              <p class="field-error" id="a-parent-name-error" role="alert">{{ tx('請填寫家長／監護人姓名', 'Please enter the parent / guardian name') }}</p>
            </div>
            <div class="form-field">
              <label for="a-parent-relation">{{ tx('與學員關係', 'Relationship to the student') }}<span class="req" aria-hidden="true">*</span></label>
              <select id="a-parent-relation" v-model="parentRelation" name="parent_relation" required aria-describedby="a-parent-relation-error">
                <option value="">{{ tx('請選擇', 'Please select') }}</option>
                <option value="father">{{ tx('父親', 'Father') }}</option>
                <option value="mother">{{ tx('母親', 'Mother') }}</option>
                <option value="guardian">{{ tx('法定監護人', 'Legal guardian') }}</option>
                <option value="other">{{ tx('其他', 'Other') }}</option>
              </select>
              <p class="field-error" id="a-parent-relation-error" role="alert">{{ tx('請選擇與學員關係', 'Please select your relationship to the student') }}</p>
            </div>
            <div class="form-field">
              <label for="a-parent-phone">{{ tx('家長聯絡電話', "Parent's phone number") }}<span class="req" aria-hidden="true">*</span></label>
              <input id="a-parent-phone" v-model="parentPhone" type="tel" name="parent_phone" required autocomplete="tel" aria-describedby="a-parent-phone-error">
              <p class="field-error" id="a-parent-phone-error" role="alert">{{ tx('請填寫家長聯絡電話', 'Please enter the parent\'s phone number') }}</p>
            </div>
            <div class="form-field">
              <label for="a-parent-email">{{ tx('家長 Email', "Parent's email") }}<span class="req" aria-hidden="true">*</span></label>
              <input id="a-parent-email" v-model="parentEmail" type="email" name="parent_email" required autocomplete="email" aria-describedby="a-parent-email-error">
              <p class="field-error" id="a-parent-email-error" role="alert">{{ tx('請填寫有效的 Email', 'Please enter a valid email address') }}</p>
            </div>
          </div>
        </fieldset>

        <div class="consent-block">
          <p v-if="isEn" class="field-hint">The consent notice below is pending legal review and is shown in Traditional Chinese.</p>
          <div class="checkbox-field">
            <input id="a-consent" v-model="consent" type="checkbox" name="consent" required aria-describedby="a-consent-error">
            <label for="a-consent">本人為上述學員之家長／法定監護人，已閱讀並同意<a :href="lp('/zh/privacy/')">隱私權政策</a>，並同意{{ clubAssets.nameZh }}依本表單蒐集學員與家長之個人資料，用於處理本次報名之聯繫、試訓與課程安排。<span class="req" aria-hidden="true">*</span></label>
          </div>
          <p class="field-error" id="a-consent-error" role="alert">{{ tx('請勾選同意個資蒐集聲明', 'Please tick the box to agree to the personal data collection notice') }}</p>

        </div>

        <div class="form-turnstile">
          <!-- Cloudflare Turnstile 防機器人驗證：sitekey 待客戶申請 Cloudflare 帳號後設定，見 https://developers.cloudflare.com/turnstile/ -->
          <div class="cf-turnstile" data-sitekey="" role="group" :aria-label="tx('機器人驗證', 'Bot verification')"></div>
          <p class="field-hint">{{ tx('此表單由 Cloudflare Turnstile 防護，驗證元件將於 sitekey 設定後生效。', 'This form is protected by Cloudflare Turnstile. The verification widget will take effect once the site key is configured.') }}</p>
        </div>

        <button class="btn btn--primary btn--block" type="submit" :disabled="status === 'submitting'">{{ tx('送出報名', 'Submit') }}</button>

        <div class="form-submit-note">
          <p v-if="isEn"><strong>What happens after you submit?</strong> An automatic reply is sent straight away to the parent's email address to confirm we have received the details. For Academy squads, the Academy Department will arrange trials and information sessions; for children's training or specialist training, the Programs Department will contact you to confirm session times.</p>
          <p v-else-if="isTcrfc"><strong>送出後會發生什麼事？</strong> 系統會立即寄送自動回覆信到家長填寫的 Email，確認我們已收到資料；報名學院梯隊由學院部窗口接手安排試訓與說明會，報名兒童訓練或專項訓練則由課程部窗口聯繫確認開課時段。</p>
          <p v-else><strong>送出後會發生什麼事？</strong> 系統會立即寄送自動回覆信到家長填寫的 Email，確認我們已收到資料；俱樂部會另外收到通知信，安排後續試訓與說明。</p>
        </div>
      </form>

      <aside class="form-sidebar">
        <div class="form-sidebar__sticky">
        <div class="form-sidebar__card">
          <p class="form-sidebar__dept">{{ tx('收件單位', 'Handled by') }}</p>
          <h2>{{ isEn ? 'Academy Department / Programs Department' : isTcrfc ? '學院部／課程部' : clubAssets.shortNameZh }}</h2>
          <p v-if="isTcrfc" class="field-hint"><template v-if="isEn">Your application is passed to the relevant team based on the program you choose.</template><template v-else>依表單中選擇的報名項目轉交對應單位。</template></p>
        </div>
        <div class="form-sidebar__card">
          <h2>{{ tx('報名項目', 'Program') }}</h2>
          <ul v-if="isTcrfc">
            <li>{{ tx('足球學院：U12／U14／U15 梯隊', 'Academy: U12 / U14 / U15 squads') }}</li>
            <li>{{ tx('兒童訓練：混齡班／初學班／技巧發展班', 'Children\'s training: mixed-age, beginner and skills development classes') }}</li>
            <li>{{ tx('專項訓練：守門員／前鋒／後衛／中場／體能與速度／高階訓練', 'Specialist training: goalkeeper, forward, defender, midfield, fitness and speed, advanced training') }}</li>
          </ul>
          <ul v-else>
            <li>青年隊：{{ academyLabel() }} 試訓申請</li>
          </ul>
          <p v-if="isTcrfc" class="field-hint"><template v-if="isEn">If you are not sure which one suits your child, choose "Not sure yet" and we will recommend one based on age and level.</template><template v-else>不確定適合哪一項也沒關係，可選擇「尚未確定」，我們會依學員年齡與程度建議。</template></p>
        </div>
        </div>
      </aside>
    </div>
  </div>
</section>
</template>

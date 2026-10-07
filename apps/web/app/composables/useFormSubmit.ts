// app/composables/useFormSubmit.ts — 10 表單中心（7 類）共用的送出邏輯（S1-17）。
//
// 對應 apps/api `POST /api/v1/{club}/forms/{formCode}/submissions`
// （apps/api/Features/Forms/FormsEndpoints.cs／FormsRepository.cs）。7 張表單頁面各自的
// DOM／欄位配置逐字沿用 mockup（docs/13-blue-whale-site.md §6 紀律 9／10「不得改寫版型」），
// 但送出邏輯（狀態管理、呼叫端點、成功／失敗訊息）完全相同，集中在這裡避免七頁各自重寫一份、
// 七份實作互相漂移（同樣的理由見 app/components/news/NewsListBody.vue 檔頭）。
//
// 🔴 **`answers` 的鍵必須是後端這張表單目前存在的 `field_key`**（見
// apps/api/Features/Forms/FormDtos.cs 的 `SubmitFormRequest.Answers` 說明），不是前台頁面
// 欄位的 `name` 屬性——mockup 的欄位遠比後端 `form_fields` 種子資料豐富（例如 10.1 加入球隊
// 前台有中英文姓名分開、性別、居住城市、慣用腳、目前球隊、履歷／照片檔案等 14 個欄位，後端
// 只定義 6 個：`name`／`birth_date`／`position`／`experience`／`video_url`／`contact`，見
// db/seed/generate-club-seed-sql.py 的 `FORM_FIELD_DEFAULTS["join_player"]`）。每個表單頁面
// 自己負責把可見欄位「對應」到這 6（或該表單既有）個鍵，本檔不做這件事、也不知道每頁有哪些
// 可見欄位——這是刻意的分工，理由見 apps/web/README.md「S1-17」節「表單欄位對應表」。
export type FormSubmitStatus = 'idle' | 'submitting' | 'success' | 'error'

export interface FormSubmitOptions {
  /** 誘捕欄位（honeypot）目前的值。正常訪客看不到這個欄位、也不會填，有值即視為機器人
   * （apps/api 端會安靜回成功、不寫入任何資料，見 FormsRepository.SubmitAsync）。 */
  website?: string
}

export function useFormSubmit(formCode: string) {
  const config = useRuntimeConfig()
  const club = config.public.club
  const route = useRoute()
  const { tx, isEn, lp } = useLocale()
  // B-19：七類表單共用的錯誤訊息接入介面字串（`form.*`，規劃書 I4 字串翻譯表「提示訊息、錯誤訊息」）；後台沒建立代號就用原文。
  const { t } = useUiStrings()

  const siteKey = (config.public.turnstileSiteKey as string | undefined)?.trim() ?? ''

  // 防機器人驗證（Cloudflare Turnstile）：site key 有值「且」該表單公開設定 captchaEnabled === true
  // 才顯示元件並要求權杖。表單定義取自 `GET /api/v1/{club}/forms/{formCode}`（PublicFormDto.captchaEnabled，
  // 經同源代理），只在瀏覽器端掛載後取一次——取不到就視為不需要驗證（後端是最後防線，會自己擋）。
  const captchaEnabled = ref(false)
  // B-16：送出成功後導向頁（`PublicFormDto.redirectPath`，站內相對路徑，可為 null；欄位尚未出現時視同 null）。
  const redirectPath = ref<string | null>(null)
  const captchaToken = ref<string | null>(null)
  /** 頁面以 `ref="captchaWidget"` 綁到 `<FormTurnstile>`，送出失敗時由這裡 reset。 */
  const captchaWidget = ref<{ reset: () => void } | null>(null)

  onMounted(async () => {
    try {
      // 表單定義不論 siteKey 有無都要取（redirectPath 與驗證碼無關）；取不到＝維持現狀，不影響送出。
      const def = await $fetch<{ captchaEnabled?: boolean, redirectPath?: string | null }>(`/api/backend/${club}/forms/${formCode}`, {
        query: { lang: isEn.value ? 'en' : 'zh' },
      })
      captchaEnabled.value = !!siteKey && def?.captchaEnabled === true
      redirectPath.value = safeRedirectPath(def?.redirectPath)
    }
    catch {
      captchaEnabled.value = false
      redirectPath.value = null
    }
  })

  const status = ref<FormSubmitStatus>('idle')
  /** 失敗時的訊息——一律顯示後端 400 回應的 `message`／`detail`（例如「缺少必填欄位：xxx」），
   * 這是後端刻意設計成「可以直接顯示給使用者看」的訊息（不含欄位技術代碼以外的內部細節，
   * 見 docs/18-work-errors.md E-52 同一原則——這裡不是後端訊息，但比照同一條規則不在前台
   * 另外編一套看不出對應關係的通用錯誤文字，方便使用者知道少填了什麼）。找不到明確訊息時
   * 才使用下面的通用文案。 */
  const errorMessage = ref('')

  const genericError = () => t('form.error_generic', tx(
    '送出失敗，請確認各欄位已正確填寫後再試一次；若持續發生，請改用電話或 Email 聯繫我們。',
    'We could not submit your form. Please check that all fields are filled in correctly and try again. If the problem continues, please contact us by phone or email.',
  ))

  async function submit(answers: Record<string, string>, options: FormSubmitOptions = {}) {
    errorMessage.value = ''
    const captchaRequired = !!siteKey && captchaEnabled.value
    if (captchaRequired && !captchaToken.value) {
      status.value = 'error'
      errorMessage.value = t('form.captcha_required', tx('請先完成人機驗證。', 'Please complete the verification first.'))
      return
    }
    status.value = 'submitting'

    try {
      await $fetch(`/api/backend/${club}/forms/${formCode}/submissions`, {
        method: 'POST',
        body: {
          answers,
          // A-4：後端自動回覆信依這個語系選 zh／en 內文（沒填該語系回退中文）。
          lang: isEn.value ? 'en' : 'zh',
          sourcePath: route.fullPath,
          website: options.website || undefined,
          turnstileToken: captchaRequired ? captchaToken.value ?? undefined : undefined,
        },
      })
      status.value = 'success'
      // B-16：有設定導向頁就導過去（經 lp() 轉成目前語系）；導向失敗不影響已成功的送出，頁面仍顯示成功訊息。
      if (redirectPath.value) {
        try { await navigateTo(lp(redirectPath.value)) }
        catch { /* 留在原頁顯示成功訊息 */ }
      }
    }
    catch (err: unknown) {
      status.value = 'error'
      errorMessage.value = isCaptchaFailed(err)
        ? t('form.captcha_failed', tx('人機驗證未通過，請重新整理頁面後再試一次。', 'Verification failed. Please refresh the page and try again.'))
        : extractErrorMessage(err, isEn.value) ?? genericError()
    }
    finally {
      // 權杖只能用一次：不論成功或失敗都換新的（失敗後使用者可修改欄位再送）。
      if (captchaRequired && status.value !== 'success') captchaWidget.value?.reset()
      if (captchaRequired) captchaToken.value = null
    }
  }

  /** 讓使用者在失敗後可以修改欄位重新送出（回到 idle，表單本身不會被本 composable 清空——
   * 欄位的 ref 由呼叫端各自持有，是否要在成功後清空由呼叫端決定）。 */
  function reset() {
    status.value = 'idle'
    errorMessage.value = ''
  }

  /** 目前是否要顯示並要求人機驗證（給頁面 `v-if` 用）。 */
  const captchaActive = computed(() => !!siteKey && captchaEnabled.value)
  function onCaptchaToken(value: string | null) { captchaToken.value = value }

  return { status, errorMessage, submit, reset, siteKey, captchaActive, captchaWidget, onCaptchaToken }
}

/** 只接受站內相對路徑（單一 `/` 開頭；拒絕 `//host`、`/\\host`、含控制字元或協定）；其餘回 null。 */
export function safeRedirectPath(raw: unknown): string | null {
  if (typeof raw !== 'string') return null
  const v = raw.trim()
  if (!v.startsWith('/') || v.startsWith('//') || v.startsWith('/\\')) return null
  for (const ch of v) { const c = ch.charCodeAt(0); if (c < 0x20 || c === 0x7f) return null }
  return v
}

/** 後端 422 `captcha_failed`（BFF 把上游 ProblemDetails 的 code 放在 `err.data.data.code`）。 */
function isCaptchaFailed(err: unknown): boolean {
  const e = err as { data?: { code?: unknown, data?: { code?: unknown } } } | null
  return e?.data?.data?.code === 'captcha_failed' || e?.data?.code === 'captcha_failed'
}

/** 從 $fetch 拋出的例外中取出後端回傳的中文錯誤訊息（400 的 body 一律是純文字或
 * `{ message }`／`{ detail }` 形狀，見 apps/api 的 PublicFormSubmissionValidationException
 * 系列例外如何被轉成 ProblemDetails）。取不到就回傳 null，交由呼叫端使用通用文案，
 * 不在這裡假設一定拿得到、也不把整包例外物件字串化塞給使用者看。 */
export function extractErrorMessage(err: unknown, en = false): string | null {
  if (!err || typeof err !== 'object') return null
  const data = (err as { data?: unknown }).data
  if (!data) return null
  if (en) {
    // 主站英文版（C-6 第二輪）：只吃後端 ProblemDetails 的 `messageEn`（BFF 把它放在 `data.data.messageEn`，
    // 直通代理則在 `data.messageEn`）；沒有就回 null，由呼叫端用英文通用文案，**不把繁中訊息顯示在英文介面**。
    const d = data as { messageEn?: unknown, data?: { messageEn?: unknown } }
    const m = typeof d.messageEn === 'string' ? d.messageEn : (typeof d.data?.messageEn === 'string' ? d.data.messageEn : '')
    return m.trim() && !/[\u3400-\u9fff]/.test(m) ? m.trim() : null
  }
  if (typeof data === 'string') return data
  if (typeof data === 'object') {
    const detail = (data as { detail?: unknown; message?: unknown; title?: unknown })
    if (typeof detail.detail === 'string') return detail.detail
    if (typeof detail.message === 'string') return detail.message
    if (typeof detail.title === 'string') return detail.title
  }
  return null
}

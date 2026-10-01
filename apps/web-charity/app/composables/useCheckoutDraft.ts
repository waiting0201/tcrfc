// app/composables/useCheckoutDraft.ts — 捐款表單草稿與建單冪等鍵的保存。
//
// 規劃書 §3.3：「表單狀態需在導向 LINE Pay 前保存，付款失敗返回時不得要求重填」。
// 付款失敗／取消返回結果頁後，重試走的是「沿用原捐款單」的 `POST /donations/{orderNo}/pay`，
// 不重新建單、也不需要重填，所以草稿只負責同一個頁面內的表單狀態（含建單失敗後的重送）。
// ⛔ 草稿含身分證字號等個資，只放在瀏覽器記憶體（useState），不寫入 localStorage／Cookie。
//
// 冪等鍵（`Idempotency-Key`，16–64 個英數／底線／連字號）：
//   - 同一份表單內容重複送出（連點、網路中斷後重試）要沿用同一個鍵，後端才會回原單而不重複建單；
//   - 使用者改了表單內容就換新鍵（同鍵不同內容後端回 409）。
//   用「送出內容的指紋」判斷是否改過：指紋相同就沿用，不同就產生新鍵。
export interface CheckoutDraft {
  projectSlug: string
  storeSlug: string | null
  amount: number | null
  donorName: string
  donorEmail: string
  isAnonymous: boolean
  invoiceType: 'mobile_carrier' | 'love_code' | 'tax_id' | ''
  mobileCarrier: string
  loveCode: string
  taxId: string
  invoiceTitle: string
  receiptTitle: string
  nationalId: string
  address: string
  isAnnualSummary: boolean
  agreedToPrivacy: boolean
  /** 最近一次送出的內容指紋與當時使用的冪等鍵。 */
  lastFingerprint: string | null
  idempotencyKey: string | null
}

export function emptyCheckoutDraft(projectSlug: string, storeSlug: string | null): CheckoutDraft {
  return {
    projectSlug,
    storeSlug,
    amount: null,
    donorName: '',
    donorEmail: '',
    isAnonymous: false,
    invoiceType: '',
    mobileCarrier: '',
    loveCode: '',
    taxId: '',
    invoiceTitle: '',
    receiptTitle: '',
    nationalId: '',
    address: '',
    isAnnualSummary: false,
    agreedToPrivacy: false,
    lastFingerprint: null,
    idempotencyKey: null,
  }
}

export function useCheckoutDraft(projectSlug: string, storeSlug: string | null) {
  return useState<CheckoutDraft>(`charity-checkout-${projectSlug}`, () =>
    emptyCheckoutDraft(projectSlug, storeSlug))
}

/** 產生冪等鍵：UUID（36 字元、英數與連字號，落在後端 16–64 的範圍內）。 */
export function newIdempotencyKey(): string {
  return globalThis.crypto.randomUUID()
}

/** 依本次送出的內容決定使用哪個冪等鍵：內容沒變沿用，內容變了換新。 */
export function resolveIdempotencyKey(draft: CheckoutDraft, fingerprint: string): string {
  if (draft.idempotencyKey && draft.lastFingerprint === fingerprint) return draft.idempotencyKey
  draft.idempotencyKey = newIdempotencyKey()
  draft.lastFingerprint = fingerprint
  return draft.idempotencyKey
}

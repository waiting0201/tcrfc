// app/composables/useCheckoutDraft.ts — 捐款表單草稿的跨頁保存。
//
// 規劃書 §3.3：「表單狀態需在導向 LINE Pay 前保存，付款失敗返回時不得要求重填」。
// 本專案沒有真正的後端訂單狀態機（mockup 階段），用 Nuxt 的 useState 在同一個 client-side
// session 內跨頁保留最後一次表單輸入，滿足「從結果頁按重試回到表單時不清空」的體驗要求。
// ⚠️ 這是 mockup 專屬的克難做法：真正串接 LINE Pay 後，保存與復原的權威來源會是後端的
// 捐款單本身（依 order_no 查回），不會是瀏覽器記憶體，這裡列入 docs/22 §6 待決回報同類的
// 「本輪假設值」，不是最終架構。
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
  agreedToPrivacy: boolean
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
    agreedToPrivacy: false,
  }
}

export function useCheckoutDraft(projectSlug: string, storeSlug: string | null) {
  return useState<CheckoutDraft>(`charity-checkout-${projectSlug}`, () =>
    emptyCheckoutDraft(projectSlug, storeSlug))
}

/** 送出表單後產生的模擬訂單，供付款轉場頁與結果頁讀取（同樣是 mockup 專屬的暫存機制）。 */
export interface MockOrder {
  orderNo: string
  projectSlug: string
  projectNameZh: string
  projectNameEn: string
  amount: number
  createdAt: string
}

export function useMockOrder(orderNo: string) {
  return useState<MockOrder | null>(`charity-mock-order-${orderNo}`, () => null)
}

export function generateMockOrderNo(): string {
  const now = new Date()
  const y = now.getFullYear()
  const m = String(now.getMonth() + 1).padStart(2, '0')
  const d = String(now.getDate()).padStart(2, '0')
  const rand = String(Math.floor(Math.random() * 90000) + 10000)
  return `DN${y}${m}${d}-${rand}`
}

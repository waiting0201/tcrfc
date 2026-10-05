// shared/utils/club-copy-en-shop.ts — 主站（tcrfc）英文版文案：站內商店、購物車、結帳、訂單、會員中心、App 下載頁（群組代號 shop）
//
// 約定（英文版文案檔共通）：
// - 只提供 tcrfc 的英文值；藍鯨站英文版不在範圍（isEn 在藍鯨一律 false）。
// - 原頁面的 `useSeoMeta` 文案 → 以頁面代號為鍵的 `SHOP_SEO_EN`；含變數的以函式提供（`getShopProductSeoEn`）。
//   欄位名稱 `title`／`description` 與 `useSeoMeta` 一致，英文版只放英文（不用「中文 + English」慣例）。
// - 檔內字串不得含中文字元（scripts 有檢查）；英文用詞一律照 docs/06-conventions.md §1.1 對照表。
// - 品牌名稱由呼叫端以 `CLUB_NAME_EN`（club-copy-en-core.ts）帶入，不在此重複定義。

import { BW_NAME_EN } from './club-copy'
import { CLUB_NAME_EN } from './club-copy-en-core'

export interface ShopSeoEn {
  title: string
  description: string
}

/** 各頁 SEO（英文版）。`{club}` 由呼叫端替換成 `CLUB_NAME_EN`——用函式而非字串樣板，避免漏換。 */
const SEO: Record<string, (club: string) => ShopSeoEn> = {
  shop: club => ({
    title: `Shop | ${club}`,
    description: `The official ${club} shop. Pay with LINE Pay, receive an e-invoice automatically, and choose home delivery, convenience-store pickup or on-site pickup.`,
  }),
  cart: club => ({
    title: `Cart | Shop | ${club}`,
    description: `Your ${club} shop cart: check your items, options and quantities, then go to checkout.`,
  }),
  checkout: club => ({
    title: `Checkout | Shop | ${club}`,
    description: `Checkout for the ${club} shop: enter your delivery details, choose a delivery method and invoice type, then pay with LINE Pay. Guest checkout is available.`,
  }),
  complete: club => ({
    title: `Order result | Shop | ${club}`,
    description: `Your ${club} shop order result: order number, payment result and e-invoice status, with a link to order lookup.`,
  }),
  lookup: club => ({
    title: `Order lookup | Shop | ${club}`,
    description: `Look up the status, tracking number and e-invoice of your ${club} shop order with your order number and email. Members can also check My Orders in the Member Centre.`,
  }),
  policy: club => ({
    title: `Shopping information and returns policy | Shop | ${club}`,
    description: `Shopping information, shipping details, returns policy and terms of sale for the ${club} shop.`,
  }),
  member: club => ({
    title: `Member Centre | ${club}`,
    description: `The ${club} Member Centre: sign in or join. Members enjoy discounts at partner stores, and Paid Fan Club members also receive a jersey.`,
  }),
  lineCallback: club => ({
    title: `LINE sign-in | ${club}`,
    description: '',
  }),
  resetPassword: club => ({
    title: `Reset password | ${club}`,
    description: '',
  }),
  forgotPassword: club => ({
    title: `Forgot password | ${club}`,
    description: '',
  }),
  verifyEmail: club => ({
    title: `Email verification | ${club}`,
    description: '',
  }),
  upgrade: club => ({
    title: `Upgrade membership | Member Centre | ${club}`,
    description: `Compare benefits and apply to upgrade to a Paid Fan Club membership with ${club}.`,
  }),
  app: club => ({
    title: `Download the app | Taichung Football | ${club}`,
    description: 'The Taichung Football app: the official app shared by both teams, with fixtures, news, players, partner stores, courses and a digital membership card, all in one app.',
  }),
}

export type ShopSeoKey = keyof typeof SEO

/** 取英文版 SEO：`getShopSeoEn('cart', CLUB_NAME_EN)`。 */
export function getShopSeoEn(key: ShopSeoKey, club: string): ShopSeoEn {
  return SEO[key]!(club)
}

/** 商品詳情頁（後台沒填 SEO 時的備援）。 */
export function getShopProductSeoEn(club: string, name: string | null | undefined): ShopSeoEn {
  const product = name ?? 'Product'
  return {
    title: `${product} | Shop | ${club}`,
    description: `${product} from the official ${club} shop. Pay with LINE Pay and receive an e-invoice.`,
  }
}

/** App 下載頁「App 裡有什麼」五張卡（對應原 FEATURES，順序一致）。 */
export const SHOP_APP_FEATURES_EN: ReadonlyArray<{ title: string, desc: string }> = [
  { title: 'Fixtures and matches', desc: 'See the fixtures of both teams in one place and open any match for details.' },
  { title: 'News and players', desc: 'The latest news and player profiles, synced from the website.' },
  { title: 'Partner stores', desc: 'Browse partner stores and their offers.' },
  { title: 'Courses', desc: 'View course information and register.' },
  { title: 'Digital membership card and membership', desc: 'Carry your membership card with you, and apply to upgrade your membership in the app.' },
]

/** 商店「結帳」頁預設配送方式（API 沒回 deliveryMethods 時的備援，順序與代碼對應原寫法）。 */
export const SHOP_DELIVERY_FALLBACK_EN: Readonly<Record<'home_delivery' | 'cvs_pickup' | 'onsite_pickup', string>> = {
  home_delivery: 'Home delivery',
  cvs_pickup: 'Convenience-store pickup (pickup only, no payment at the store)',
  onsite_pickup: 'Pickup on home match days or at the club',
}

/** 發票開立方式（代碼 → 英文標籤）。 */
export const SHOP_INVOICE_LABEL_EN: Readonly<Record<'mobile_barcode' | 'citizen_cert' | 'tax_id' | 'donation', string>> = {
  mobile_barcode: 'Mobile barcode carrier',
  citizen_cert: 'Citizen Digital Certificate carrier',
  tax_id: 'Unified Business Number (company invoice)',
  donation: 'Donate the invoice',
}

/** 英文頁上，後端 `?lang=en` 逐欄位回退時沒有 `isFallbackLocale` 旗標的內容（商店入口與政策）：偵測是否含漢字，有就視為繁中備援。 */
export function shopHasCjk(...values: Array<string | null | undefined>): boolean {
  return values.some(v => typeof v === 'string' && /[㐀-鿿]/.test(v))
}

// ---------------------------------------------------------------------------
// 藍鯨（bw）英文版（B-5，2026-10-05）
// 藍鯨變體命名：用「club 參數」而非另開一組 `_BW` 常數——本檔的函式都已吃 `club` 名稱字串，
// 藍鯨只需要換名稱與少數藍鯨專屬句子。既有簽名與預設行為不變。
// ---------------------------------------------------------------------------

/** 英文版俱樂部簡稱：磐石 `Taichung Rock FC`、藍鯨 `Taichung Blue Whale`（B-5）。 */
export function getShopClubNameEn(club: 'tcrfc' | 'bw'): string {
  return club === 'bw' ? BW_NAME_EN : CLUB_NAME_EN
}

/** 麵包屑「俱樂部文化」英文標籤（磐石 `TCRFC Culture`、藍鯨 `Taichung Blue Whale Culture`）。 */
export function getShopCultureLabelEn(club: 'tcrfc' | 'bw'): string {
  return club === 'bw' ? `${BW_NAME_EN} Culture` : 'TCRFC Culture'
}

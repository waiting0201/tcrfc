/**
 * 發票狀態（慈善獨立庫 `invoices` 的兩個狀態欄位值域，docs/16-charity-schema.md）。
 * - 開立狀態：待開立／已開立／開立失敗
 * - 作廢／折讓狀態：無／已作廢／已折讓
 * 原本放在 mockup 時代的假資料型別檔（已隨假資料入口刪除），畫面元件與 API 回應共用這裡。
 */
export type InvoiceIssueStatus = 'pending' | 'issued' | 'failed'
export type InvoiceVoidStatus = 'none' | 'voided' | 'allowance'

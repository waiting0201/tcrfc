/**
 * `X-Dev-Operator-Id` 標頭的值。🔴🔴🔴 這不是身分驗證——沒有登入系統之前，這個值只是
 * 「呼叫端自稱是誰」，伺服器會拿去查 `admin_users` 表，查不到就存 `null`（見 apps/api/README.md
 * 「寫入端點開發模式開關」整節、`Security/IDevOperatorResolver.cs`）。`admin_users` 目前是空表，
 * 所以現況下不管這裡送什麼值，`created_by`／`updated_by` 實際上都會是 `null`，這是預期行為。
 *
 * 這裡只是每個瀏覽器分頁給自己配一個穩定的假值（存在 localStorage，重整頁面不會變），
 * 純粹是為了讓開發時從資料庫看得出「這幾筆是同一個瀏覽器工作階段建立的」，不是為了辨識使用者。
 * 等登入系統做出來，這整個檔案都要換成真正的權杖。
 */
const STORAGE_KEY = 'tcrfc-admin-dev-operator-id'

export function devOperatorId(): string {
  let id = localStorage.getItem(STORAGE_KEY)
  if (!id) {
    id = crypto.randomUUID()
    localStorage.setItem(STORAGE_KEY, id)
  }
  return id
}

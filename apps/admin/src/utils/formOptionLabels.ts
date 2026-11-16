/**
 * G1 表單設計器：選項「前台顯示文字（中文）」的儲存判斷（apps/api/README.md「表單定義對齊頁面與後台
 * 中文選項顯示文字」，docs/23 W-3）。
 *
 * 後端對 `optionLabelsZh` 的**更新語意與其他欄位相反**：
 *   - 省略（`undefined`）／`null`＝**保留**現值
 *   - `[]`＝清除（前台回到顯示送出值本身）
 *   - 有內容＝整組取代
 * 其他欄位（如 `optionLabelsEn`）是「整筆取代」，漏帶會清資料（E-351）；這個欄位是反過來，
 * 所以「沒動過就不要送」才是正確的，而且**不能把送出值複製成顯示文字存進去**——那會讓
 * 「沒設定」變成「已設定且與送出值相同」，之後送出值若有調整，前台就會顯示過期的文字。
 *
 * 畫面上中文欄位的預設值是送出值本身（`optionLabelsZh ?? options`），所以畫面狀態分不出
 * 「原本就是 null」和「原本設成與送出值相同」，必須拿載入時的原值一起比對。
 */

/** 畫面上中文顯示文字的初始值：後端沒設定（`null`／筆數對不上）就用送出值本身。 */
export function initialOptionLabelsZh(options: readonly string[], saved: readonly string[] | null | undefined): string[] {
  if (!saved || saved.length !== options.length) return [...options]
  return options.map((value, i) => saved[i] ?? value)
}

/**
 * 決定請求要帶什麼：
 *   - 回傳 `undefined`＝請求不帶這個欄位（後端保留現值）
 *   - 回傳 `[]`＝清除
 *   - 回傳字串陣列＝整組取代
 *
 * @param options  目前的送出值清單
 * @param saved    載入時後端回的 `optionLabelsZh`（`null`＝沒設定）；新增欄位傳 `null`
 * @param edited   畫面上目前的中文顯示文字（與 `options` 同序）；空白視為「用送出值」
 */
export function buildOptionLabelsZhPayload(
  options: readonly string[],
  saved: readonly string[] | null | undefined,
  edited: readonly string[],
): string[] | undefined {
  if (options.length === 0) return undefined
  // 空白＝沒填，等同預設（送出值）；後端不接受空字串，所以不能原樣送。
  const effective = options.map((value, i) => (edited[i] ?? '').trim() || value)
  const sameAsValues = effective.every((v, i) => v === options[i])

  if (sameAsValues) {
    // 全部等於送出值：原本沒設定 → 維持沒設定（不送）；原本有設定 → 送 [] 清除。
    return saved ? [] : undefined
  }
  // 與原本已存的內容完全相同（沒動過）→ 不送，避免無謂覆寫。
  if (saved && saved.length === effective.length && saved.every((v, i) => v === effective[i])) return undefined
  return effective
}

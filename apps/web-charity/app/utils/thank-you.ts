// app/utils/thank-you.ts — 結果頁成功態的「感謝語」挑選（docs/23 K-1）。
//
// 後台 N7「感謝語樣板」（`donation.thank_you_message_template`，中英各一）經公開 `GET /settings` 的 `thankYouTemplate` 輸出。
// 後端對這個欄位**沒有定義任何 `{標記}` 代換**（標記只屬於系統信樣板，且是信件端代換）——所以前台一律當純文字顯示，
// 不代換、不用 v-html。語系回退規則：
//   - 中文頁：樣板有填就用，空白用固定文案（呼叫端不顯示額外段落）。
//   - 英文頁：`PublicSettings.isFallback` 是「任一文案欄位英文缺漏」的整體旗標，無法指出是不是感謝語缺英文；
//     因此 isFallback 為真且文字含中日韓字元時，視為英文版缺漏，不把中文樣板塞進英文頁（改用固定英文文案）。

const CJK = /[\u3400-\u9fff\uff00-\uffef]/

/** 回傳要顯示的感謝語純文字；沒有可用的後台樣板時回傳 null（呼叫端就只顯示固定標題）。 */
export function pickThankYouText(template: string | null | undefined, lang: 'zh' | 'en', settingsFallback: boolean): string | null {
  const text = template?.trim()
  if (!text) return null
  if (lang === 'en' && settingsFallback && CJK.test(text)) return null
  return text
}

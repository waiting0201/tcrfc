// app/utils/validators.ts — 捐款表單的欄位驗證規則（規劃書 §3.3、§5.2）。
// 一律回傳 boolean，錯誤文案交給呼叫端依 app/utils/i18n.ts 的字典組字，方便中英文共用同一套規則。

export function isValidEmail(value: string): boolean {
  // 一般前端夠用的寬鬆格式檢查，不追求完整 RFC 5322。
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim())
}

export function isValidMobileCarrier(value: string): boolean {
  // 手機條碼載具格式：斜線 + 7 碼英數（規劃書 §5.2）。
  return /^\/[0-9A-Z.+-]{7}$/.test(value.trim().toUpperCase())
}

/** 統一編號檢查碼驗證（財政部公告的加權公式）。 */
export function isValidTaxId(value: string): boolean {
  const digits = value.trim()
  if (!/^\d{8}$/.test(digits)) return false

  const weights = [1, 2, 1, 2, 1, 2, 4, 1]
  const nums = digits.split('').map(Number)

  const sumDigits = (n: number) => Math.floor(n / 10) + (n % 10)

  let total = 0
  for (let i = 0; i < 8; i++) {
    total += sumDigits((nums[i] ?? 0) * (weights[i] ?? 0))
  }

  if (total % 10 === 0) return true
  // 統編第七碼為 7 時，允許加 1 後整除的特例（財政部公告的例外規則）。
  if (nums[6] === 7 && (total + 1) % 10 === 0) return true
  return false
}

/** 捐贈碼（愛心碼）：3–7 碼數字（與後端 CharityDonationRules 相同）。 */
export function isValidLoveCode(value: string): boolean {
  return /^\d{3,7}$/.test(value.trim())
}

/** 身分證字號檢核碼（內政部公告的加權公式；供捐贈收據的選填欄位，後端會再驗一次並加密儲存）。 */
export function isValidNationalId(value: string): boolean {
  const id = value.trim().toUpperCase()
  if (!/^[A-Z][1289]\d{8}$/.test(id)) return false
  const letters = 'ABCDEFGHJKLMNPQRSTUVXYWZIO'
  const n = letters.indexOf(id.charAt(0)) + 10
  const digits = [Math.floor(n / 10), n % 10, ...id.slice(1).split('').map(Number)]
  const weights = [1, 9, 8, 7, 6, 5, 4, 3, 2, 1, 1]
  const sum = digits.reduce((acc, d, i) => acc + d * (weights[i] ?? 0), 0)
  return sum % 10 === 0
}

export function isAmountInRange(amount: number, min: number, max: number): boolean {
  return Number.isFinite(amount) && amount >= min && amount <= max
}


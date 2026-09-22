// app/utils/validators.ts — 捐款表單的欄位驗證規則（規劃書 §3.3、§5.2）。
// 一律回傳 boolean，錯誤文案交給呼叫端依 app/utils/i18n.ts 的字典組字，方便中英文共用同一套規則。

export function isValidEmail(value: string): boolean {
  // 一般前端夠用的寬鬆格式檢查，不追求完整 RFC 5322。
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim())
}

export function isValidMobileCarrier(value: string): boolean {
  // 手機條碼載具格式：斜線 + 7 碼英數（規劃書 §5.2）。
  return /^\/[0-9A-Z.+-]{7}$/i.test(value.trim())
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
    total += sumDigits(nums[i] * weights[i])
  }

  if (total % 10 === 0) return true
  // 統編第七碼為 7 時，允許加 1 後整除的特例（財政部公告的例外規則）。
  if (nums[6] === 7 && (total + 1) % 10 === 0) return true
  return false
}

export function isAmountInRange(amount: number, min: number, max: number): boolean {
  return Number.isFinite(amount) && amount >= min && amount <= max
}

/** 結果頁不顯示完整個資（規劃書 §3.5），Email 遮罩成 a***@domain 的形式。 */
export function maskEmail(email: string): string {
  const [local, domain] = email.split('@')
  if (!domain) return email
  const visible = local.slice(0, 1) || '*'
  return `${visible}***@${domain}`
}

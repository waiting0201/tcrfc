/**
 * 報名／聯絡資料的共用前端驗證（對照後端 TrialsRepository 的規則，提早在畫面上提示）。
 * 後端才是最後防線；這裡只負責讓操作人員在送出前就看到白話的錯誤。
 */
import { taipeiToday } from '@/utils/dateTime'

const EMAIL_RE = /^[^@\s]+@[^@\s]+\.[^@\s]+$/
// 與後端 AdminInput 電話規則一致：只能含數字、+、-、空白與括號，且至少 6 碼數字（上限 32 字）。
const PHONE_CHARS_RE = /^[0-9+\-\s()]+$/

export function isValidEmail(value: string): boolean {
  const v = value.trim()
  return v.length <= 255 && EMAIL_RE.test(v)
}

export function isValidPhone(value: string): boolean {
  const v = value.trim()
  return v.length <= 32 && PHONE_CHARS_RE.test(v) && v.replace(/\D/g, '').length >= 6
}

/** 出生日期（YYYY-MM-DD）是否未滿 18 歲；無日期回傳 false。 */
export function isUnder18(birthOn: string | null | undefined): boolean {
  if (!birthOn) return false
  const today = taipeiToday()
  const [y, m, d] = today.split('-').map(Number)
  const cutoff = `${String((y ?? 0) - 18).padStart(4, '0')}-${String(m).padStart(2, '0')}-${String(d).padStart(2, '0')}`
  return birthOn > cutoff
}

export interface ContactFields {
  phone: string
  email: string
  birthOn?: string | null
  guardianName?: string
  guardianPhone?: string
}

/** 回傳第一個錯誤的白話訊息；通過則回傳 null。 */
export function validateContact(f: ContactFields, opts: { requireGuardianForMinor: boolean }): string | null {
  const phone = f.phone.trim()
  const email = f.email.trim()
  if (!phone && !email) return '電話與 Email 至少要填一項'
  if (phone && !isValidPhone(phone)) return '電話格式不正確，請填寫數字（可含 - 與括號），例如 0912-345-678'
  if (email && !isValidEmail(email)) return 'Email 格式不正確，請檢查後再儲存'
  if (f.guardianPhone && f.guardianPhone.trim() && !isValidPhone(f.guardianPhone)) {
    return '家長電話格式不正確，請填寫數字（可含 - 與括號）'
  }
  if (opts.requireGuardianForMinor && isUnder18(f.birthOn)) {
    if (!f.guardianName?.trim() || !f.guardianPhone?.trim()) return '報名者未滿 18 歲，請填寫家長（監護人）的姓名與電話'
  }
  return null
}

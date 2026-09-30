// shared/utils/weekly-schedule.ts — `sessions.weekly_schedule`（JSON）→ 可讀文字
//
// 資料庫欄位是 JSON（db/club-schema.sql `weekly_schedule json`），API 原樣以字串輸出
// （ProgramDetailDto.sessions[].weeklySchedule）。後台（apps/admin ProgramSessionEditView）只驗證
// 「是合法 JSON」，不限形狀，所以這裡對形狀採寬鬆解析、無法解析就降級成 null（呼叫端顯示「—」），
// 絕不把原始 JSON 印到畫面上。
//
// 已知形狀（種子 db/seed/backoffice_seed.py 與後台 placeholder）：
//   {"mon":"18:00-19:30","wed":"18:00-19:30"}   星期鍵 → 時段字串
//   {"mon-fri":"09:00-16:00"}                    區間鍵（週一至週五）
//   {"mon":"1.5 小時"}                           值不是時段而是自由文字（藍鯨舊站原文）→ 原樣顯示
// 另外容許（後台可能輸入、目前無人使用）：值為字串陣列（同日多時段）、鍵以逗號列多天（"mon,wed"）。
//
// 不依賴 Vue／Nuxt runtime，供 app/pages 與 scripts/check-weekly-schedule.mjs 共用。
// 🔴 純 Node 直接 import 執行單元檢查，故無相對 import、型別自含。

export type WeeklyScheduleLocale = 'zh' | 'en'

const DAY_ORDER = ['mon', 'tue', 'wed', 'thu', 'fri', 'sat', 'sun'] as const
type DayKey = (typeof DAY_ORDER)[number]

const DAY_ALIASES: Record<string, DayKey> = {
  mon: 'mon', monday: 'mon', tue: 'tue', tues: 'tue', tuesday: 'tue', wed: 'wed', wednesday: 'wed',
  thu: 'thu', thur: 'thu', thurs: 'thu', thursday: 'thu', fri: 'fri', friday: 'fri',
  sat: 'sat', saturday: 'sat', sun: 'sun', sunday: 'sun',
}

const DAY_LABEL: Record<WeeklyScheduleLocale, Record<DayKey, string>> = {
  zh: { mon: '週一', tue: '週二', wed: '週三', thu: '週四', fri: '週五', sat: '週六', sun: '週日' },
  en: { mon: 'Mon', tue: 'Tue', wed: 'Wed', thu: 'Thu', fri: 'Fri', sat: 'Sat', sun: 'Sun' },
}

const TIME_RANGE = /^(\d{1,2}):(\d{2})\s*[-–—~]\s*(\d{1,2}):(\d{2})$/
const MAX_FREE_TEXT = 40

function parseDay(raw: string): DayKey | null {
  return DAY_ALIASES[raw.trim().toLowerCase()] ?? null
}

/** 鍵 → 顯示標籤與排序位置；無法辨識回傳 null。 */
function parseDayKey(key: string, locale: WeeklyScheduleLocale): { order: number; labels: string[] } | null {
  const labels = DAY_LABEL[locale]
  const range = key.split(/\s*[-–—~]\s*/)
  if (range.length === 2) {
    const a = parseDay(range[0]!)
    const b = parseDay(range[1]!)
    if (!a || !b) return null
    const ia = DAY_ORDER.indexOf(a)
    const ib = DAY_ORDER.indexOf(b)
    if (ib < ia) return null
    if (ia === ib) return { order: ia, labels: [labels[a]] }
    return { order: ia, labels: [locale === 'zh' ? `${labels[a]}至${labels[b]}` : `${labels[a]}–${labels[b]}`] }
  }
  const days = key.split(/\s*[,，、]\s*/).map(parseDay)
  if (days.length === 0 || days.some((d) => d === null)) return null
  const list = days as DayKey[]
  return { order: DAY_ORDER.indexOf(list[0]!), labels: list.map((d) => labels[d]) }
}

/** 值 → 時段文字；`08:00-9:30` 標準化為 `08:00–09:30`，其他短文字原樣（Vue 樣板插值會轉義）。 */
function formatSlot(value: unknown): string | null {
  if (typeof value !== 'string') return null
  const text = value.trim()
  if (!text) return null
  const m = TIME_RANGE.exec(text)
  if (m) {
    const [h1, m1, h2, m2] = [Number(m[1]), m[2]!, Number(m[3]), m[4]!]
    if (h1 > 24 || h2 > 24 || Number(m1) > 59 || Number(m2) > 59) return null
    const p = (n: number) => String(n).padStart(2, '0')
    return `${p(h1)}:${m1}–${p(h2)}:${m2}`
  }
  return text.length <= MAX_FREE_TEXT ? text : null
}

/**
 * 回傳可讀文字（中文以「、」、英文以「, 」串接），無資料或無法解析回傳 `null`。
 * `onWarn` 只在「有值卻解析不了」時呼叫（開發環境由呼叫端接 console.warn）。
 */
export function formatWeeklySchedule(
  raw: string | null | undefined,
  locale: WeeklyScheduleLocale,
  onWarn?: (message: string) => void,
): string | null {
  if (raw === null || raw === undefined || raw.trim() === '') return null
  const warn = (why: string) => {
    onWarn?.(`weekly_schedule 無法解析（${why}），已降級為不顯示：${raw.length > 120 ? `${raw.slice(0, 120)}…` : raw}`)
    return null
  }

  let parsed: unknown
  try {
    parsed = JSON.parse(raw)
  } catch {
    return warn('不是合法 JSON')
  }
  if (parsed === null) return null
  if (typeof parsed !== 'object' || Array.isArray(parsed)) return warn('不是物件')

  const entries: { order: number; seq: number; text: string }[] = []
  let seq = 0
  for (const [key, value] of Object.entries(parsed as Record<string, unknown>)) {
    const day = parseDayKey(key, locale)
    if (!day) return warn(`無法辨識的星期鍵 "${key}"`)
    const slots = Array.isArray(value) ? value : [value]
    for (const slot of slots) {
      const slotText = formatSlot(slot)
      if (!slotText) return warn(`"${key}" 的值不是時段文字`)
      for (const label of day.labels) entries.push({ order: day.order, seq: seq++, text: `${label} ${slotText}` })
    }
  }
  if (entries.length === 0) return null
  entries.sort((a, b) => a.order - b.order || a.seq - b.seq)
  return entries.map((e) => e.text).join(locale === 'zh' ? '、' : ', ')
}

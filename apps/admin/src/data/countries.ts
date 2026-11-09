// 球員國籍選項（2026-11-09 起，國籍存代碼而非自由文字）。
//
// 值：ISO 3166-1 alpha-2 大寫代碼（249 個），另加英國四地 GB-ENG／GB-SCT／GB-WLS／GB-NIR；可為空、單一國籍。
// 🔴 這份清單要與後端 apps/api 的國家代碼清單、官網 apps/web/shared/utils/player.ts 的 UK_HOME_NATIONS 保持同步：
//    後端驗證只收清單內的值，這裡多出來的會被退回、少的會讓使用者選不到。
// 國名不存、不寫死：用 Intl.DisplayNames 依語系產生（Intl 沒有「列舉所有地區」的 API，所以代碼清單要自己放）。

const ISO_CODES = [
  'AD', 'AE', 'AF', 'AG', 'AI', 'AL', 'AM', 'AO', 'AQ', 'AR', 'AS', 'AT',
  'AU', 'AW', 'AX', 'AZ', 'BA', 'BB', 'BD', 'BE', 'BF', 'BG', 'BH', 'BI',
  'BJ', 'BL', 'BM', 'BN', 'BO', 'BQ', 'BR', 'BS', 'BT', 'BV', 'BW', 'BY',
  'BZ', 'CA', 'CC', 'CD', 'CF', 'CG', 'CH', 'CI', 'CK', 'CL', 'CM', 'CN',
  'CO', 'CR', 'CU', 'CV', 'CW', 'CX', 'CY', 'CZ', 'DE', 'DJ', 'DK', 'DM',
  'DO', 'DZ', 'EC', 'EE', 'EG', 'EH', 'ER', 'ES', 'ET', 'FI', 'FJ', 'FK',
  'FM', 'FO', 'FR', 'GA', 'GB', 'GD', 'GE', 'GF', 'GG', 'GH', 'GI', 'GL',
  'GM', 'GN', 'GP', 'GQ', 'GR', 'GS', 'GT', 'GU', 'GW', 'GY', 'HK', 'HM',
  'HN', 'HR', 'HT', 'HU', 'ID', 'IE', 'IL', 'IM', 'IN', 'IO', 'IQ', 'IR',
  'IS', 'IT', 'JE', 'JM', 'JO', 'JP', 'KE', 'KG', 'KH', 'KI', 'KM', 'KN',
  'KP', 'KR', 'KW', 'KY', 'KZ', 'LA', 'LB', 'LC', 'LI', 'LK', 'LR', 'LS',
  'LT', 'LU', 'LV', 'LY', 'MA', 'MC', 'MD', 'ME', 'MF', 'MG', 'MH', 'MK',
  'ML', 'MM', 'MN', 'MO', 'MP', 'MQ', 'MR', 'MS', 'MT', 'MU', 'MV', 'MW',
  'MX', 'MY', 'MZ', 'NA', 'NC', 'NE', 'NF', 'NG', 'NI', 'NL', 'NO', 'NP',
  'NR', 'NU', 'NZ', 'OM', 'PA', 'PE', 'PF', 'PG', 'PH', 'PK', 'PL', 'PM',
  'PN', 'PR', 'PS', 'PT', 'PW', 'PY', 'QA', 'RE', 'RO', 'RS', 'RU', 'RW',
  'SA', 'SB', 'SC', 'SD', 'SE', 'SG', 'SH', 'SI', 'SJ', 'SK', 'SL', 'SM',
  'SN', 'SO', 'SR', 'SS', 'ST', 'SV', 'SX', 'SY', 'SZ', 'TC', 'TD', 'TF',
  'TG', 'TH', 'TJ', 'TK', 'TL', 'TM', 'TN', 'TO', 'TR', 'TT', 'TV', 'TW',
  'TZ', 'UA', 'UG', 'UM', 'US', 'UY', 'UZ', 'VA', 'VC', 'VE', 'VG', 'VI',
  'VN', 'VU', 'WF', 'WS', 'YE', 'YT', 'ZA', 'ZM', 'ZW',
] as const

const UK_HOME_NATIONS: Record<string, string> = {
  'GB-ENG': '英格蘭',
  'GB-SCT': '蘇格蘭',
  'GB-WLS': '威爾斯',
  'GB-NIR': '北愛爾蘭',
}

export interface CountryOption {
  code: string
  /** 中文國名（畫面顯示用）。 */
  name: string
  /** 下拉選單的完整標籤：「台灣（TW）」，方便用代碼搜尋。 */
  label: string
}

const regionNames = new Intl.DisplayNames(['zh-Hant-TW'], { type: 'region' })

/** 代碼 → 中文國名；不是合法代碼時回傳 null。 */
export function countryNameZh(code: string): string | null {
  if (UK_HOME_NATIONS[code]) return UK_HOME_NATIONS[code]
  if (!(ISO_CODES as readonly string[]).includes(code)) return null
  return regionNames.of(code) ?? null
}

export function isCountryCode(value: string): boolean {
  return countryNameZh(value) !== null
}

/** 台灣排第一，其餘依中文名排序；英國四地併入清單一起排。 */
export const COUNTRY_OPTIONS: CountryOption[] = (() => {
  const all = [...ISO_CODES, ...Object.keys(UK_HOME_NATIONS)]
    .map((code) => ({ code, name: countryNameZh(code) ?? code }))
    .map((o) => ({ ...o, label: `${o.name}（${o.code}）` }))
  const rest = all.filter((o) => o.code !== 'TW').sort((a, b) => a.name.localeCompare(b.name, 'zh-Hant-TW'))
  const tw = all.find((o) => o.code === 'TW')
  return tw ? [tw, ...rest] : rest
})()

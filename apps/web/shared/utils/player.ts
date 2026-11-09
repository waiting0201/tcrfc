// shared/utils/player.ts — 球員公開資料型別與網址（對應 apps/api `PlayerDto`，shared/openapi.json）。
//
// 球員詳情網址以 `slug`（同俱樂部唯一，`[a-z0-9]+(-[a-z0-9]+)*`）為正規形式：
// `/zh/club/first-team/player/{slug}/`（App 深連結 `tcrfc://player/{slug}` 的回退網址，App 規劃書 §2.3）。
// id（UUID）網址仍可解析，但詳情頁會 301 到 slug 網址，避免同一球員兩個網址的重複內容。

export interface PlayerDto {
  id: string
  /** 網址代稱；後端保證非空（資料表 NOT NULL）。 */
  slug: string
  teamCode: string
  shirtNo: number | null
  position: string | null
  birthOn?: string | null
  heightCm: number | null
  weightKg: number | null
  nationality: string | null
  preferredFoot: string | null
  photoKey?: string | null
  name: string | null
  bio: string | null
  photoUrl: string | null
  photoWidth?: number | null
  photoHeight?: number | null
  photoAlt?: string | null
  schemaEligible?: boolean
  /** 球員狀態（稽核 A-2）；公開端只回現役，目前恆為 `active`。前台不依此過濾，僅型別容許。 */
  status?: string
}

/** 球員詳情的站內路徑（不含語系前綴，交給 `lp()`）。`slugOrId` 優先給 slug；只有 id 時網址仍可用（頁面會 301 到 slug）。 */
export function playerPath(slugOrId: string): string {
  return `/zh/club/first-team/player/${slugOrId}/`
}

/**
 * 慣用腳代碼正規化：值域是 right／left／both（docs/12a §5.2）。2026-11-09 前後台曾把中文「左腳」直接存進資料庫，
 * 一併認得（API 讀出時也會正規化，見 apps/api Features/Players/PreferredFoot.cs）。認不得的值原樣回傳。
 */
export function normalizePreferredFoot(foot: string): string {
  const f = foot.trim().toLowerCase()
  if (f === 'right' || f === '右腳') return 'right'
  if (f === 'left' || f === '左腳') return 'left'
  if (f === 'both' || f === '雙腳') return 'both'
  return foot
}

/**
 * 國籍：資料表 `player.nationality` 存 ISO 3166-1 alpha-2 大寫代碼（另加英國四地 GB-ENG／GB-SCT／GB-WLS／GB-NIR），
 * 可為空、單一國籍（2026-11-09 起；後台為國家下拉選單，後端 migration 把舊中文值轉成代碼）。
 * 國名一律在前端依語系產生：Intl.DisplayNames（瀏覽器與 Node 的 CLDR 資料一致，SSR 與 hydration 輸出相同）；
 * 英國四地用固定對照。不是合法代碼的值（認不得的舊資料）原樣顯示。
 * 後台清單見 apps/admin/src/constants/countries.ts，後端清單在 apps/api，三處要同步。
 */
const UK_HOME_NATIONS: Record<string, { zh: string, en: string }> = {
  'GB-ENG': { zh: '英格蘭', en: 'England' },
  'GB-SCT': { zh: '蘇格蘭', en: 'Scotland' },
  'GB-WLS': { zh: '威爾斯', en: 'Wales' },
  'GB-NIR': { zh: '北愛爾蘭', en: 'Northern Ireland' },
}
const displayNamesCache = new Map<string, Intl.DisplayNames | null>()
function regionNames(locale: 'zh' | 'en'): Intl.DisplayNames | null {
  const tag = locale === 'en' ? 'en' : 'zh-Hant-TW'
  if (!displayNamesCache.has(tag)) {
    try {
      displayNamesCache.set(tag, new Intl.DisplayNames([tag], { type: 'region' }))
    }
    catch {
      displayNamesCache.set(tag, null)
    }
  }
  return displayNamesCache.get(tag) ?? null
}
export function nationalityLabel(code: string | null | undefined, locale: 'zh' | 'en'): string {
  const raw = (code ?? '').trim()
  if (!raw) return ''
  const uk = UK_HOME_NATIONS[raw]
  if (uk) return uk[locale]
  if (!/^[A-Z]{2}$/.test(raw)) return raw // 舊資料（非代碼）原樣顯示
  try {
    const name = regionNames(locale)?.of(raw)
    return name && name !== raw ? name : raw
  }
  catch {
    return raw
  }
}

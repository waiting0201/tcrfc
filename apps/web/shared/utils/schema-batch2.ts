// shared/utils/schema-batch2.ts — GEO-05 結構化資料第二批（S1-20）純函式
//
// 主站規劃書 §7「結構化資料 Schema Markup」型別清單裡的 Event（俱樂部活動）／Course（課程）
// 兩個型別，以及既有 SportsEvent（app/pages/zh/schedule.vue，S0-9j／S1-12c 既有）補上「場地與
// 地址，來自場地資料」這項本輪要求。不依賴 Vue／Nuxt runtime，供：
//   - app/composables/useClubEventSchema.ts（Event，defineEvent()）
//   - app/composables/useCourseSchema.ts（Course，defineCourse()）
//   - app/pages/zh/schedule.vue（SportsEvent 的 venueAddressByName()）
//   - scripts/check-schema-batch2.mjs（固定 fixture 驗證，不需要啟動 apps/api）
// 共用同一份「哪些節點合格、欄位怎麼組」判斷，不各自重寫一份（比照 shared/utils/faq-schema.ts
// 的 E-39「單一來源」既有原則）。
//
// 🔴 已知落差（GEO-05／E-39，回報見 apps/web/README.md「S1-20」節「需要後端補的欄位」）：
// apps/api 的 apps/api/Features/Seo/SchemaCompleteness.cs 已經把 Event／Course 兩型別的
// 必填欄位單一來源宣告好（SchemaType.Event＝name／startDate／location，SchemaType.Course＝
// name／description），但目前只有 MatchDto／ArticleDetailDto 兩個既有端點實際算出對應的
// SchemaEligible 布林值——PublicCalendarEventDto（俱樂部活動）與 ProgramDetailDto（課程）
// 都還沒有這個欄位。本檔的 isClubEventSchemaEligible()／isCourseSchemaEligible() 是前台
// 暫時鏡射同一份必填欄位判斷（判斷條件逐一對照 SchemaRequiredFields.ByType 的欄位鍵，
// 沒有新增或放寬任何一條），不是本輪自創的另一套標準；後端補上對應布林值後，這裡應該
// 改回「直接讀後端算好的欄位」，不再自行判斷（同 apps/web/README.md「S1-12f」節
// Organization／SportsTeam／SportsEvent 的既有模式）。
// 🔴 明確帶副檔名（`.ts`，不是像 shared/utils/club-copy.ts 等既有檔案那樣省略）：本檔會被
// scripts/check-schema-batch2.mjs 用純 Node（非 Vite／Nuxt 打包）直接 import 執行單元測試，
// Node 的原生型別剝離模組解析不像 bundler 會自動幫忙補副檔名，省略會直接拋
// ERR_MODULE_NOT_FOUND（已於本輪撰寫檢查腳本時實測到）。Nuxt／Vite 端一樣能正確解析
// 帶副檔名的相對路徑，不影響既有建置。
import { cleanFaqSchemaText } from './faq-schema.ts'
import { schemaImage } from './image-attrs.ts'

// ---------------------------------------------------------------------------
// SportsEvent 場地地址查找（本輪任務要求「場地（Place 與地址，來自場地資料）」）
// ---------------------------------------------------------------------------

export interface SchemaVenueFact {
  nameZh: string
  address: string | null
}

/**
 * `matches.venue`（`MatchDto.Venue`／`PublicCalendarEventDto.VenueName`）是自由文字欄位，
 * 沒有 `venue_id` 外鍵可在公開端點直接 join 出地址（apps/api 兩份 DTO 檔頭皆未提供）。
 * 用名稱比對既有 `useSiteFacts(club).facts.value.venues`（GEO-03 單一來源，已含
 * `nameZh`／`address`）找出對應地址；找不到就回傳 `null`，不臆造——這時 SportsEvent 的
 * `location.name` 仍然輸出，只是沒有 `address` 子欄位（GEO-05「資料不足時不輸出該欄位」，
 * 不是不輸出整個型別）。
 */
export function venueAddressByName(venueName: string | null, venues: readonly SchemaVenueFact[]): string | null {
  if (!venueName) return null
  return venues.find((v) => v.nameZh === venueName)?.address ?? null
}

// ---------------------------------------------------------------------------
// Event（俱樂部活動，calendar_custom_events，app/pages/zh/schedule.vue「俱樂部活動」分頁）
// ---------------------------------------------------------------------------

export interface ClubEventSchemaSourceItem {
  id: string
  /** 每次發生的唯一鍵；重複活動同 `id` 多筆，錨點 url 用它避免多個 Event 節點撞同一個 `url`（被當成同一個節點合併）。沒有時回退 `id`。 */
  occurrenceId?: string
  title: string | null
  /** ISO 8601 時間戳（真正的 UTC，不是牆上時間字面值，見 PublicCalendarEventDto 檔頭）。 */
  startsAt: string
  endsAt: string | null
  venueName: string | null
  description: string | null
  coverUrl: string | null
  /** 後端圖片欄位組：兩者都有時 image 輸出 ImageObject（含寬高），否則維持網址字串。 */
  coverWidth?: number | null
  coverHeight?: number | null
}

/**
 * GEO-05：對應 `apps/api/Features/Seo/SchemaCompleteness.cs` 的 `SchemaType.Event` 必填欄位
 * （`name`／`startDate`／`location`，該檔行 116–121）。見本檔檔頭「已知落差」——
 * `PublicCalendarEventDto` 目前沒有算好的 `SchemaEligible`，這裡鏡射同一份判斷條件。
 */
export function isClubEventSchemaEligible(item: ClubEventSchemaSourceItem): boolean {
  if (!item.title || !cleanFaqSchemaText(item.title)) return false
  if (!item.venueName || !cleanFaqSchemaText(item.venueName)) return false
  if (!item.startsAt || Number.isNaN(Date.parse(item.startsAt))) return false
  return true
}

export interface ClubEventSchemaOpts {
  siteUrl: string
  /** 這一頁的路徑（含語系前綴、不含網域），例如 `/zh/schedule/`——用來組每則活動的
   * `url` 錨點，比照既有 `sportsEvents` 的 `fixtureId()` 錨點做法。 */
  pagePath: string
}

/**
 * 輸出 `defineEvent()` 要吃的純資料物件陣列（GEO-05：逐筆檢查合不合格，不合格的那一筆不輸出，
 * 其餘合格的活動仍要輸出——不是「有一筆不合格就整個不輸出」）。呼叫端
 * （`app/composables/useClubEventSchema.ts`）逐一包 `defineEvent()`。
 */
export function buildClubEventSchemaNodes(
  items: readonly ClubEventSchemaSourceItem[],
  opts: ClubEventSchemaOpts,
): Record<string, unknown>[] {
  const base = opts.siteUrl.replace(/\/$/, '')
  const nodes: Record<string, unknown>[] = []
  for (const item of items) {
    if (!isClubEventSchemaEligible(item)) continue
    const node: Record<string, unknown> = {
      name: cleanFaqSchemaText(item.title!),
      startDate: item.startsAt,
      location: { '@type': 'Place', name: cleanFaqSchemaText(item.venueName!) },
      eventAttendanceMode: 'https://schema.org/OfflineEventAttendanceMode',
      url: `${base}${opts.pagePath}#ce-${item.occurrenceId || item.id}`,
    }
    if (item.endsAt) node.endDate = item.endsAt
    // 🔴 `description`／`image` 兩欄位刻意「沒有真實資料時明確填 null，不是整段省略不寫」：
    // `nuxt-schema-org` 的 eventResolver 設有 `inheritMeta: ['description', 'image', ...]`
    // （node_modules/nuxt-schema-org/dist/schema.mjs 已查證），節點缺這兩個鍵時框架會自動
    // 拿「這一頁的 SEO meta description／預設 OG 圖」頂替——那會讓沒有專屬說明或封面圖的
    // 活動，被冠上一整份跟這個活動毫無關係的全站預設圖文，正是「不臆造」要防的事。框架的
    // `setIfEmpty()` 只在鍵值為 `undefined` 時才會頂替，明確填 `null` 可以擋下它；序列化前
    // 的 `stripNullProperties()`（同檔案已查證）會把值為 `null` 的鍵整個移除，最終輸出仍然
    // 乾淨，不會出現 `"image":null` 這種殘缺欄位。
    const desc = item.description ? cleanFaqSchemaText(item.description) : ''
    node.description = desc || null
    node.image = schemaImage(item.coverUrl, item.coverWidth, item.coverHeight) ?? null
    nodes.push(node)
  }
  return nodes
}

// ---------------------------------------------------------------------------
// Course（05 課程與活動，training_programs，5.1／5.2 等已接 API 的頁面）
// ---------------------------------------------------------------------------

export interface CourseSchemaSourceProgram {
  name: string | null
  intro: string | null
  ageMin: number | null
  ageMax: number | null
}

export interface CourseSchemaOpts {
  /** 固定為俱樂部本身（規劃書任務指示「provider 為俱樂部」），永遠有值，不列為必填判斷
   * （比照 apps/api SchemaCompleteness.cs 對 Course 的同一句既有註解）。 */
  providerName: string
  siteUrl: string
  /** 輸出語系；只影響本檔自己組的字串（`educationalLevel` 的年齡單位）。省略＝'zh'（既有輸出逐字不變）。 */
  locale?: 'zh' | 'en'
}

/**
 * GEO-05：對應 `SchemaCompleteness.cs` 的 `SchemaType.Course` 必填欄位（`name`／`description`，
 * 該檔行 141–145）。`ProgramDetailDto` 目前沒有算好的 `SchemaEligible`，這裡鏡射同一份判斷。
 */
export function isCourseSchemaEligible(program: CourseSchemaSourceProgram | null): boolean {
  if (!program) return false
  return Boolean(program.name && cleanFaqSchemaText(program.name) && program.intro && cleanFaqSchemaText(program.intro))
}

/**
 * 輸出 `defineCourse()` 要吃的純資料物件；資料不合格（無課程名稱或說明——現況 `programs`
 * 表兩俱樂部皆 0 筆種子資料，見 apps/web/README.md「S1-15」節）回傳 `null`，呼叫端不輸出
 * 該型別（GEO-05）。`educationalLevel` 只在 `ageMin`／`ageMax` 兩者皆有值時才附上——
 * 「只輸出有真實資料的欄位」（任務指示原文），不臆造年齡範圍。
 */
export function buildCourseSchemaNode(
  program: CourseSchemaSourceProgram | null,
  opts: CourseSchemaOpts,
): Record<string, unknown> | null {
  if (!isCourseSchemaEligible(program)) return null
  const node: Record<string, unknown> = {
    name: cleanFaqSchemaText(program!.name!),
    description: cleanFaqSchemaText(program!.intro!),
    provider: { name: opts.providerName, url: opts.siteUrl.replace(/\/$/, '') || undefined },
  }
  if (program!.ageMin != null && program!.ageMax != null) {
    node.educationalLevel = opts.locale === 'en'
      ? `Ages ${program!.ageMin}–${program!.ageMax}`
      : `${program!.ageMin}–${program!.ageMax} 歲`
  }
  return node
}

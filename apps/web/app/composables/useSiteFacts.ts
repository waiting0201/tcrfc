// app/composables/useSiteFacts.ts — GEO-03／GEO-04 站台事實：改讀後端公開端點（S1-12d 收尾）
//
// 對應 apps/api `GET /api/v1/{club}/site-facts?lang=zh|en`（apps/api/README.md「S1-12d」節，
// 2026-09-29 backend-engineer 交付）。這是接續 apps/web/README.md「S1-12d」節（frontend-architect
// 前一輪的盤點：後端當時完全沒有承載成立年份／主場／聯賽／梯隊組成／聯絡方式這五類事實的
// 欄位與公開端點，做了前台暫定的 shared/utils/site-facts.ts 靜態快照）——本檔把「頁面文字」
// 與 useSchemaOrgClub.ts 的 JSON-LD 一併改為讀這支 API，兩者共用同一份回應內容
// （GEO-04：結構化資料與明文雙重呈現，數值必須一致）。
//
// 🔴 一次固定抓中英兩種語系（各一支 useFetch），不是依目前頁面語系只抓一種——既有頁面的
// 呼叫方式本來就是「明確指定要 Zh 還是 En 欄位」（例如 club/opportunities/index.vue 同一段落
// 需要 league.nameZh 與 league.nameEn 同時出現，join/location/index.vue 需要場地 nameZh／nameEn
// 並列），不是跟隨 useLocale() 目前路由語系切換——本檔沿用這個既有慣例，只換掉資料來源，
// 不改變「頁面各自要哪個語言欄位」的既有邏輯，避免無謂放大本輪改動範圍。
//
// 回傳形狀刻意與 shared/utils/site-facts.ts 既有 SiteFacts 型別（Zh／En 成對欄位）相容，
// 讓呼叫端從「靜態物件屬性存取」換成「這支 composable 的回傳值屬性存取」時，屬性名稱
// 完全不用改，只需要把 `SITE_FACTS.tcrfc.foundedYear` 換成 `tcrfcFacts.foundedYear`
// 這種等價寫法（`tcrfcFacts` 是本 composable回傳的 `facts`，一個 ComputedRef）。
//
// 降級：任一語系 API 失敗（useFetch 的 data 為 null，例如 apps/api 未啟動或回傳錯誤）時，
// 整組退回 shared/utils/site-facts.ts 的靜態快照（比照 useHomeSections／useFaqEmbed 既有
// fail-open 慣例，不出 500）。這是本輪保留 site-facts.ts 靜態資料的唯一理由——它不再是
// 頁面的主要來源，只在 API 打不到時墊背；lint:fact-single-source 的既有防呆邏輯不受影響
// （靜態字面值只允許留在 site-facts.ts 本身，這條規則沒有變）。
//
// ✅ S1-12d 收尾第二輪（2026-09-29）：上一輪這裡記錄的「club-copy.ts 仍直接讀
// site-facts.ts 靜態快照」已解決——club-copy.ts 依賴事實的內容鍵全部改成
// `getXxx(club, facts)` 工廠函式，呼叫端（各消費頁）改用本 composable 回傳的
// `facts` 產生文案，不再從 club-copy.ts 內部讀取 SITE_FACTS。詳見
// shared/utils/club-copy.ts 檔頭與 apps/web/README.md「S1-12d 收尾第二輪」節。
import { SITE_FACTS, type SiteFacts, type SiteFactVenue } from '#shared/utils/site-facts'
import type { ClubCode } from '#shared/utils/club'

interface PublicSiteFactsLeagueDto {
  name: string
  shortName: string | null
}

interface PublicSiteFactsVenueDto {
  name: string
  address: string | null
  isHomeGround: boolean
}

interface PublicSiteFactsContactDto {
  address: string | null
  phone: string | null
  hours: string | null
  /** C-2：後台維護；舊版後端不回此欄位。 */
  email?: string | null
  departments?: Array<{ name: string, email?: string | null, phoneExtension?: string | null }> | null
}

interface PublicSiteFactsDto {
  foundedYear: string
  foundingDateIso: string | null
  foundedDisplay: string
  foundingTitle: string | null
  league: PublicSiteFactsLeagueDto
  venues: PublicSiteFactsVenueDto[]
  squadStructureSummary: string
  squadCodes: string[]
  contact: PublicSiteFactsContactDto
  /** 台中藍鯨官網網址（S1-16／S1-12d 後續補完）。概念上只屬於 `tcrfc`，`bw` 呼叫時恆為
   * `null`。`null`＝尚未設定，呼叫端（womens/index.vue）應退回既有
   * `useRuntimeConfig().public.blueWhaleSiteUrl`，不要顯示空連結。 */
  blueWhaleSiteUrl: string | null
  /** C-2：整個物件可能不存在（舊版後端）或全部為 null（尚未維護）。 */
  social?: { facebook?: string | null, instagram?: string | null, youtube?: string | null, line?: string | null } | null
  footerBlurb?: string | null
}

function normalizeClub(club: string): ClubCode {
  return club === 'bw' ? 'bw' : 'tcrfc'
}

/** zh 回應是必要的主結構；en 只用來補齊既有頁面需要的 nameEn 並列欄位，缺席時該欄位
 * 為 null（不臆測英文，比照 GEO-05「資料不足時不輸出」的一貫原則）。zh 本身失敗時
 * （代表 API 整個打不到）才整組退回靜態快照，不做「部分欄位退回、部分欄位打 API」
 * 的混合狀態——那會讓同一個俱樂部的事實一部分來自後端、一部分來自可能已經過期的
 * 靜態快照，違反 GEO-03「單一維護處」的精神。 */
const CJK_RE = /[\u3000-\u303f\u3400-\u9fff\uff00-\uffef]/

/** 後端 `?lang=en` 在英文值缺漏時會回退中文；英文欄位只收不含中日文字元的值，
 * 否則改用靜態快照的英文（可能為 null），避免英文版混入中文。 */
function pickEn(fromApi: string | null | undefined, fallback: string | null): string | null {
  const v = fromApi?.trim()
  return v && !CJK_RE.test(v) ? v : fallback
}

function mergeSiteFacts(
  club: ClubCode,
  zh: PublicSiteFactsDto | null,
  en: PublicSiteFactsDto | null,
): SiteFacts {
  if (!zh) return SITE_FACTS[club]
  return {
    foundedYear: zh.foundedYear,
    foundingDateIso: zh.foundingDateIso,
    foundedDisplayZh: zh.foundedDisplay,
    foundedDisplayEn: pickEn(en?.foundedDisplay, SITE_FACTS[club].foundedDisplayEn),
    foundingTitleZh: zh.foundingTitle,
    foundingTitleEn: pickEn(en?.foundingTitle, SITE_FACTS[club].foundingTitleEn),
    league: {
      nameZh: zh.league.name,
      nameEn: pickEn(en?.league.name, SITE_FACTS[club].league.nameEn),
      shortNameZh: zh.league.shortName,
    },
    venues: zh.venues.map(
      (v, i): SiteFactVenue => ({
        nameZh: v.name,
        nameEn: pickEn(en?.venues[i]?.name, SITE_FACTS[club].venues.find((sv) => sv.nameZh === v.name)?.nameEn ?? null),
        address: v.address,
        isHomeGround: v.isHomeGround,
      }),
    ),
    squadStructureZh: zh.squadStructureSummary,
    squadStructureEn: pickEn(en?.squadStructureSummary, SITE_FACTS[club].squadStructureEn),
    squadCodes: zh.squadCodes,
    contact: {
      address: zh.contact.address,
      phone: zh.contact.phone,
      hours: zh.contact.hours,
      hoursEn: pickEn(en?.contact.hours, SITE_FACTS[club].contact.hoursEn ?? null),
      email: zh.contact.email?.trim() || null,
      departments: (zh.contact.departments ?? []).map((d, i) => ({
        nameZh: d.name,
        nameEn: pickEn(en?.contact.departments?.[i]?.name, null),
        email: d.email?.trim() || null,
        phoneExtension: d.phoneExtension?.trim() || null,
      })),
    },
    social: {
      facebook: zh.social?.facebook?.trim() || null,
      instagram: zh.social?.instagram?.trim() || null,
      youtube: zh.social?.youtube?.trim() || null,
      line: zh.social?.line?.trim() || null,
    },
    footerBlurbZh: zh.footerBlurb?.trim() || null,
    footerBlurbEn: pickEn(en?.footerBlurb, null),
    blueWhaleSiteUrl: zh.blueWhaleSiteUrl,
  }
}

/** 讀取指定俱樂部的站台事實（GEO-03 單一來源，來自後端 apps/api site-facts 端點）。
 * `club` 一律傳明確字面值（'tcrfc' 或 'bw'），比照既有呼叫端「每個呼叫點自己決定要
 * 哪個俱樂部」的既有寫法（例如 app/pages/zh/womens/index.vue 在 tcrfc 容器裡仍需要
 * 讀 'bw' 的事實），不是自動讀目前容器的 config.public.club。
 *
 * 回傳：
 * - `facts`：與 SiteFacts 型別相容的 ComputedRef，供頁面文字與 JSON-LD 直接讀欄位。
 * - `primaryVenue`：主場（`isHomeGround` 第一筆）的 ComputedRef，等價於舊版
 *   `getPrimaryVenue(club)` 但改讀本次 fetch 的結果。
 * - `academyLabel(separator?)`：梯隊代碼組字函式，等價於舊版 `academyTeamCodesLabel(club, sep)`。
 */
export function useSiteFacts(club: string) {
  const normalized = normalizeClub(club)

  const { data: zh } = useFetch<PublicSiteFactsDto>(`/api/backend/${normalized}/site-facts`, {
    query: { lang: 'zh' },
    key: `site-facts-${normalized}-zh`,
  })
  const { data: en } = useFetch<PublicSiteFactsDto>(`/api/backend/${normalized}/site-facts`, {
    query: { lang: 'en' },
    key: `site-facts-${normalized}-en`,
  })

  const facts = computed<SiteFacts>(() => mergeSiteFacts(normalized, zh.value, en.value))

  const primaryVenue = computed<SiteFactVenue>(
    () => facts.value.venues.find((v) => v.isHomeGround) ?? facts.value.venues[0],
  )

  /** 把 `facts.squadCodes` 陣列組成單一顯示字串（例如年齡層代碼依序以分隔符連接）。
   * `separator` 預設全形斜線，比照既有頁面既定寫法；部分頁面用頓號（見
   * academy/join.vue），呼叫端自行傳入。 */
  function academyLabel(separator = '／'): string {
    return facts.value.squadCodes.join(separator)
  }

  return { facts, primaryVenue, academyLabel }
}

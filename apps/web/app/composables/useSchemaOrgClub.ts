// app/composables/useSchemaOrgClub.ts — GEO-05（S1-12f）Organization／SportsTeam 結構化資料
//
// 兩個 composable 都走 apps/api 算好的 schemaEligible 布林值（單一來源見
// apps/api/Features/Seo/SchemaCompleteness.cs 檔頭「必填欄位怎麼訂出來的」），這裡不重新判斷
// 一次「名稱／網址／隊徽是否齊全」（docs/18-work-errors.md E-39）。
//
// 🔴 已知現況：clubs.logo_light_key／teams.hero_key 目前的種子資料與既有後台（AdminClubDetailDto
// 對標誌三組欄位刻意唯讀）都沒有寫入路徑，兩個俱樂部現況下 schemaEligible 恆為 false——這是
// GEO-05「缺漏者不輸出該型別」的正確行為，不是這裡的判斷有誤，見 apps/web/README.md「S1-12f」節。
// 一旦後台補上隊徽上傳路徑、apps/api 算出的 schemaEligible 變 true，這裡不需要再改任何程式碼。
//
// 🔴 GEO-03／GEO-04（S1-12d 收尾，2026-09-29）：foundingDate／address／memberOf（聯賽）
// 三個欄位不是 apps/api Clubs／Teams DTO 算出來的（見 shared/utils/site-facts.ts 檔頭
// 說明），改讀 `useSiteFacts(club)`（`app/composables/useSiteFacts.ts`，打
// `GET /api/v1/{club}/site-facts?lang=zh`）——跟頁面明文使用同一支 composable、同一份
// 回應，滿足 GEO-04「結構化資料與明文同時呈現、數值一致」。這裡固定用中文全名（不受
// 目前頁面語系影響，比照 apps/api/README.md「S1-12d」節「回應形狀」的建議：JSON-LD
// 需要不受 `lang` 影響的中文全名時，`useSiteFacts()` 本來就一律同時抓 zh／en，`facts`
// 的 `xxxZh` 欄位固定來自 zh 那次呼叫）。`foundingDateIso` 為 `null` 時（目前 tcrfc
// 恆為此情形，確切成立月日未核實）該欄位整個不輸出，不臆測日期；地址同理，`contact.address`
// 為 `null` 時不輸出 `address` 欄位（藍鯨現況實際上已有真實地址，見 apps/api/README.md
// 「S1-12d」節「已知資料落差」，會如實顯示，不是本檔判斷有誤）。

const CJK_RE = /[\u3000-\u303f\u3400-\u9fff\uff00-\uffef]/

/** 英文版 JSON-LD 的字串不得混入中文：後端 `?lang=en` 缺值時回退中文，這裡改用 `fallback`
 * （沒有可用英文時為 undefined＝整欄不輸出，比照 GEO-05「資料不足時不輸出」）。 */
function englishOnly<T extends string | undefined | null>(value: string | null | undefined, fallback: T): string | T {
  const v = value?.trim()
  return v && !CJK_RE.test(v) ? v : fallback
}

/** 俱樂部英文名（Organization 用）：主站 docs/06 §1.1；藍鯨 B-5 已於 2026-10-05 定案，Organization 用全名
 * `BW_FULL_NAME_EN`（SportsTeam 用簡稱，見 `useSportsTeamSchema`）。
 *
 * 🔴 JSON-LD 的英文判斷一律用 `locale.value === 'en'`（URL 語系），**不用 `isEn`**：藍鯨 `/en/` 頁要宣告
 * `enReadyBw` 才算 `isEn`，但 JSON-LD 與頁面可見文字是兩回事——藍鯨尚未翻譯的 `/en/` 頁，結構化資料輸出英文名稱
 * 不構成矛盾（hreflang 本來就是 URL 層級）。名稱類欄位取不到英文時退回上表的英文名，絕不輸出中文。 */
const EN_CLUB_NAME: Record<string, string> = {
  tcrfc: 'Taichung Rock FC',
  bw: BW_FULL_NAME_EN,
}

// 主站規劃書 v3.20：`clubs` 公開 DTO 不再有 logoUrl／品牌色，標誌改用前台靜態資產（getClubAssets）。
// 型別容許舊版後端仍回傳多餘欄位，也容許缺 `schemaEligible`（缺＝視為不合格，保守不輸出）。
interface ClubSchemaData {
  name: string
  domain: string
  schemaEligible?: boolean
}

interface TeamSchemaData {
  code: string
  name: string | null
  schemaEligible: boolean
}

/** 在目前頁面輸出 Organization JSON-LD（GEO-05）。資料不合格時整段不輸出，見檔頭說明。
 * 用 useSchemaOrg／defineOrganization（比照 app/pages/zh/news/[slug]/index.vue 的 Article
 * 一樣有專用定義器可用，不像 SportsTeam／SportsEvent 沒有專用型別要手刻原始 JSON-LD）。 */
export function useOrganizationSchema() {
  const config = useRuntimeConfig()
  const club = config.public.club
  const siteConfig = useSiteConfig()
  const { facts } = useSiteFacts(club)
  const { locale } = useLocale()
  const isEnUrl = locale.value === 'en'
  // /en/（含藍鯨未翻頁面，見 EN_CLUB_NAME 註解）：名稱走 `?lang=en`（後端缺英文時回退中文，下方 `englishOnly`
  // 擋掉，改用品牌英文名）。地址維持中文原文（docs/06 §1.1：不自行翻成英文地址格式）。
  const lang = isEnUrl ? 'en' : 'zh'

  const { data } = useFetch<ClubSchemaData>(`/api/backend/clubs/${club}`, {
    key: `org-schema-${club}-${lang}`,
    query: { lang },
  })

  // 🔴 BW-7 驗收修正（2026-10-05）：原本是 `watchEffect(() => { ... useSchemaOrg([...]) })`，**合格時 SSR 完全不輸出
  // Organization**——實測（fixture API 回 schemaEligible=true）首頁沒有任何 Organization 節點。根因兩層：
  //   1. `watchEffect` 回呼第一次同步執行時 `useFetch` 還沒回來（這裡刻意不 await），之後資料到手時才呼叫
  //      `useSchemaOrg`，已脫離元件的注入脈絡；
  //   2. 改成 `useSchemaOrg(computed(...))` 也不行：nuxt-schema-org 在 server 端對 ref 只 `toValue` 一次（呼叫當下），
  //      資料仍是 null。
  // 之前沒被發現是因為真資料下 schemaEligible 恆為 false（見檔頭），「合格時輸出」這條路徑從未被實際走過。
  // 改成與下方 SportsTeam 同一種寫法：`useHead(() => ...)` 以**函式**傳入，SSR 在所有 await 完成後、輸出標籤時才求值。
  // 輸出的是手刻 JSON-LD（不經 nuxt-schema-org 的 graph 合併）：Organization 本來就只在這個 composable 輸出一次，
  // 沒有需要合併的同 @id 節點。不合格時回空物件、完全不輸出（GEO-05）。
  useHead(() => {
    const c = data.value
    if (!c?.schemaEligible) return {}
    const siteUrl = (siteConfig.url ?? '').replace(/\/$/, '') || `https://${c.domain}`
    const soc = facts.value.social
    const sameAs = [soc?.facebook, soc?.instagram, soc?.youtube, soc?.line].filter((u): u is string => !!u)
    return {
      script: [{
        key: 'organization-schema',
        type: 'application/ld+json',
        innerHTML: JSON.stringify({
          '@context': 'https://schema.org',
          '@type': 'Organization',
          '@id': `${siteUrl}/#organization`,
          name: isEnUrl ? englishOnly(c.name, EN_CLUB_NAME[club] ?? EN_CLUB_NAME.tcrfc) : c.name,
          url: siteUrl,
          logo: `${siteUrl}${getClubAssets(club).headerMark.src}`,
          // C-2：聯絡 Email／電話／社群（sameAs）與明文同一來源 useSiteFacts；未維護則整欄不輸出。
          email: facts.value.contact.email ?? undefined,
          telephone: facts.value.contact.phone ?? undefined,
          sameAs: sameAs.length ? sameAs : undefined,
          // GEO-03／GEO-04：成立年份／主場地址與明文同一來源（useSiteFacts），資料不明時
          // 整欄不輸出（不臆測），比照 GEO-05「資料不足時不輸出該欄位」的一貫原則。
          foundingDate: facts.value.foundingDateIso ?? undefined,
          address: facts.value.contact.address
            ? { '@type': 'PostalAddress', streetAddress: facts.value.contact.address, addressCountry: 'TW' }
            : undefined,
        }),
      }],
    }
  })
}

/** 在目前頁面輸出指定球隊代碼（如 'D1'）的 SportsTeam JSON-LD（GEO-05）。schema-org-js 沒有
 * SportsTeam 專用定義器，手刻原始 JSON-LD，理由比照 app/pages/zh/schedule.vue 的 SportsEvent
 * 既有寫法（硬塞 @type 進通用定義器能不能穿過正規化未經查證，直接手刻風險最低）。 */
export function useSportsTeamSchema(teamCode: string) {
  const config = useRuntimeConfig()
  const club = config.public.club
  const siteConfig = useSiteConfig()
  const { facts, primaryVenue } = useSiteFacts(club)
  const { locale } = useLocale()
  const isEnUrl = locale.value === 'en'
  const lang = isEnUrl ? 'en' : 'zh'

  const { data } = useFetch<TeamSchemaData[]>(`/api/backend/${club}/teams`, {
    key: `team-schema-${club}-${lang}`,
    query: { lang },
  })

  useHead(() => {
    const team = data.value?.find((t) => t.code === teamCode)
    if (!team?.schemaEligible) return {}
    const siteUrl = (siteConfig.url ?? '').replace(/\/$/, '')
    return {
      script: [{
        key: `sports-team-schema-${teamCode}`,
        type: 'application/ld+json',
        innerHTML: JSON.stringify({
          '@context': 'https://schema.org',
          '@type': 'SportsTeam',
          // 英文版：球隊名取 `?lang=en`，缺英文（回退中文）時改用品牌英文名／team code，不混入中文。
          // 藍鯨一線隊 `BW1` 退回簡稱 `BW_NAME_EN`（B-5）；其餘梯隊用 team code。
          name: isEnUrl ? englishOnly(team.name, club === 'bw' && team.code === 'BW1' ? BW_NAME_EN : team.code) : team.name,
          url: siteUrl || undefined,
          logo: siteUrl ? `${siteUrl}${getClubAssets(club).headerMark.src}` : undefined,
          sport: 'Soccer',
          // GEO-03／GEO-04：所屬聯賽與主場與明文同一來源（useSiteFacts），數值必須一致。
          // 英文版：聯賽／場地用 nameEn（聯賽英文名「待客戶確認」，沿用既有值）；缺則退回中文全名。
          memberOf: {
            '@type': 'SportsOrganization',
            name: (isEnUrl && facts.value.league.nameEn) || facts.value.league.nameZh,
          },
          location: {
            '@type': 'Place',
            name: (isEnUrl && primaryVenue.value.nameEn) || primaryVenue.value.nameZh,
          },
        }),
      }],
    }
  })
}

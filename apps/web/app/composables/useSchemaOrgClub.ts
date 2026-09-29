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
// 🔴 GEO-03／GEO-04（S1-12d）：foundingDate／address／memberOf（聯賽）三個欄位不是
// apps/api 算出來的（後端 ClubDto／TeamDto 目前沒有這些欄位，見 shared/utils/site-facts.ts
// 檔頭說明），改讀前台單一來源 site-facts.ts——跟頁面明文（各頁的 `SITE_FACTS.xxx` 用法）是
// 同一份資料，滿足 GEO-04「結構化資料與明文同時呈現、數值一致」。`foundingDateIso` 為 `null`
// 時（目前 tcrfc 恆為此情形，確切成立月日未核實）該欄位整個不輸出，不臆測日期；地址同理，
// bw 沒有可公開地址（`contact.address` 為 `null`）時不輸出 `address` 欄位。

interface ClubSchemaData {
  name: string
  domain: string
  logoUrl: string | null
  schemaEligible: boolean
}

interface TeamSchemaData {
  code: string
  name: string | null
  logoUrl: string | null
  schemaEligible: boolean
}

/** 在目前頁面輸出 Organization JSON-LD（GEO-05）。資料不合格時整段不輸出，見檔頭說明。
 * 用 useSchemaOrg／defineOrganization（比照 app/pages/zh/news/[slug]/index.vue 的 Article
 * 一樣有專用定義器可用，不像 SportsTeam／SportsEvent 沒有專用型別要手刻原始 JSON-LD）。 */
export function useOrganizationSchema() {
  const config = useRuntimeConfig()
  const club = config.public.club
  const siteConfig = useSiteConfig()
  const facts = getSiteFacts(club)

  const { data } = useFetch<ClubSchemaData>(`/api/backend/clubs/${club}`, {
    key: `org-schema-${club}`,
  })

  watchEffect(() => {
    const c = data.value
    if (!c?.schemaEligible) return
    const siteUrl = (siteConfig.url ?? '').replace(/\/$/, '') || `https://${c.domain}`
    useSchemaOrg([
      defineOrganization({
        name: c.name,
        url: siteUrl,
        logo: c.logoUrl ?? undefined,
        // GEO-03／GEO-04：成立年份／主場地址與明文同一來源（site-facts.ts），資料不明時
        // 整欄不輸出（不臆測），比照 GEO-05「資料不足時不輸出該欄位」的一貫原則。
        foundingDate: facts.foundingDateIso ?? undefined,
        address: facts.contact.address
          ? { '@type': 'PostalAddress', streetAddress: facts.contact.address, addressCountry: 'TW' }
          : undefined,
      }),
    ])
  })
}

/** 在目前頁面輸出指定球隊代碼（如 'D1'）的 SportsTeam JSON-LD（GEO-05）。schema-org-js 沒有
 * SportsTeam 專用定義器，手刻原始 JSON-LD，理由比照 app/pages/zh/schedule.vue 的 SportsEvent
 * 既有寫法（硬塞 @type 進通用定義器能不能穿過正規化未經查證，直接手刻風險最低）。 */
export function useSportsTeamSchema(teamCode: string) {
  const config = useRuntimeConfig()
  const club = config.public.club
  const siteConfig = useSiteConfig()
  const facts = getSiteFacts(club)

  const { data } = useFetch<TeamSchemaData[]>(`/api/backend/${club}/teams`, {
    key: `team-schema-${club}`,
  })

  useHead(() => {
    const team = data.value?.find((t) => t.code === teamCode)
    if (!team?.schemaEligible) return {}
    const siteUrl = (siteConfig.url ?? '').replace(/\/$/, '')
    const homeVenue = getPrimaryVenue(club)
    return {
      script: [{
        key: `sports-team-schema-${teamCode}`,
        type: 'application/ld+json',
        innerHTML: JSON.stringify({
          '@context': 'https://schema.org',
          '@type': 'SportsTeam',
          name: team.name,
          url: siteUrl || undefined,
          logo: team.logoUrl ?? undefined,
          sport: 'Soccer',
          // GEO-03／GEO-04：所屬聯賽與主場與明文同一來源（site-facts.ts），數值必須一致。
          memberOf: { '@type': 'SportsOrganization', name: facts.league.nameZh },
          location: { '@type': 'Place', name: homeVenue.nameZh },
        }),
      }],
    }
  })
}

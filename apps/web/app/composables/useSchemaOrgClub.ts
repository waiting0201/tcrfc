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

  const { data } = useFetch<TeamSchemaData[]>(`/api/backend/${club}/teams`, {
    key: `team-schema-${club}`,
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
          name: team.name,
          url: siteUrl || undefined,
          logo: team.logoUrl ?? undefined,
          sport: 'Soccer',
        }),
      }],
    }
  })
}

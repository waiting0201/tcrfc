// app/composables/useCharityCta.ts — 11 各頁「球迷捐款」CTA 的導流網址與文案（S2-9，CH-6）
//
// 對應 apps/api `GET /api/v1/{club}/charity/cta?lang=`（後台 B5「捐款導流與參與方式設定」）。
// 🔴 網址與文案來自後台，前台**不寫死**（主站規劃書 §3.11、docs/14 E1a 規則③）。
// 🔴 設定了捐款網址時後端強制中文文案點明「台灣足球策略發展協會」；前台自己加的固定說明文字也要點明收受者
// （shared/utils/charity.ts CHARITY_RECIPIENT），不得讓人以為是捐給俱樂部。
// 🔴 沒有設定網址（`donationUrl` 為 null，或 API 打不到）：不顯示可點擊的外連按鈕，維持既有 disabled 佔位
// 與說明；各頁的球迷捐款卡片退回站內錨點 `/zh/charity/#donate`（說明三步驟與收受者）。
// 🔴 外連一律 `target="_blank" rel="noopener noreferrer"`，且只放行 https（後端已驗證，這裡再擋一次）。
import type { CharityCta } from '#shared/utils/charity'

export async function useCharityCta() {
  const config = useRuntimeConfig()
  const { locale, lp } = useLocale()
  const club = config.public.club
  const { data } = await useFetch<CharityCta>(`/api/backend/${club}/charity/cta`, {
    query: { lang: locale.value },
    key: `charity-cta-${club}-${locale.value}`,
  })

  const cta = computed<CharityCta>(() => data.value ?? {
    donationUrl: null, donationCta: null, fanCta: null, corporateCta: null, corporateUrl: null,
  })

  const donationUrl = computed(() => {
    const url = cta.value.donationUrl
    return url && /^https:\/\//i.test(url) ? url : null
  })

  /** 球迷捐款卡片的連結：有導流網址直接連到平台（外連），沒有退回 11 首頁的說明錨點（站內）。 */
  const donateLink = computed(() => donationUrl.value
    ? { href: donationUrl.value, external: true }
    : { href: lp('/zh/charity/#donate'), external: false })

  /** 企業合作的連結：後台設定了 `corporateUrl`（站內路徑或 https）就用，否則回 9.4 贊助方案。 */
  const corporateLink = computed(() => {
    const url = cta.value.corporateUrl
    if (url && /^https:\/\//i.test(url)) return { href: url, external: true }
    if (url && url.startsWith('/') && !url.startsWith('//')) return { href: lp(url), external: false }
    return { href: lp('/zh/partners/opportunities/'), external: false }
  })

  return { cta, donationUrl, donateLink, corporateLink }
}

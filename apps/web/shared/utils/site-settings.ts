// shared/utils/site-settings.ts — I 網站設定公開端點的型別與純函式（H 批，2026-10-02）。
// 對應 apps/api/README.md「H 批」§5「公開讀取」：site-settings／policies／ui-strings／venues。

export interface PublicSiteSettings {
  // 主站規劃書 v3.20：標誌、Favicon、品牌色由前台靜態資產與樣式定義，`site-settings` 不再有 `brand`；
  // 舊版後端若仍回傳，前台忽略（型別不收）。
  maintenance: { enabled: boolean, message: string | null }
  languages: Array<{ code: string, name: string, isDefault: boolean, fallbackCode: string | null }>
  fallbackMode: 'show_default' | 'hide'
  formats: { dateFormat: string | null, numberFormat: string | null, thousandsSeparator: string | null, decimalSeparator: string | null }
  policies: Array<{ code: string, title: string, hasContent: boolean }>
}

export interface PublicPolicy {
  code: string
  title: string
  /** 純文字，空行分段；前台必須用文字節點輸出，不得 v-html。 */
  body: string
  updatedAt: string | null
  isFallbackLocale: boolean
}

export interface PublicVenue {
  id: string
  name: string
  address: string | null
  directions: string | null
  lat: number | null
  lng: number | null
  photoUrl: string | null
  photoWidth: number | null
  photoHeight: number | null
  photoAlt: string | null
  isHome: boolean
}

/** Google 地圖連結：有座標用座標，否則用地址；兩者都沒有回 null。 */
export function venueMapHref(v: Pick<PublicVenue, 'lat' | 'lng' | 'address' | 'name'>): string | null {
  const query = v.lat != null && v.lng != null ? `${v.lat},${v.lng}` : (v.address ?? v.name)
  return query ? `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(query)}` : null
}

export function venueEmbedSrc(v: Pick<PublicVenue, 'lat' | 'lng' | 'address' | 'name'>): string | null {
  const query = v.lat != null && v.lng != null ? `${v.lat},${v.lng}` : (v.address ?? v.name)
  return query ? `https://www.google.com/maps?q=${encodeURIComponent(query)}&output=embed` : null
}

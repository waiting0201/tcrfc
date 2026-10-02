// shared/utils/site-settings.ts — I 網站設定公開端點的型別與純函式（H 批，2026-10-02）。
// 對應 apps/api/README.md「H 批」§5「公開讀取」：menus／site-settings／policies／ui-strings／venues。

export interface PublicMenuItem {
  id: string
  label: string
  /** 內部連結是不含語系前綴的路徑（`/about/`），外部連結是完整網址；沒有連結（只當分類標題）為 null。 */
  url: string | null
  isExternal: boolean
  children: PublicMenuItem[]
}

export interface PublicMenus {
  main: PublicMenuItem[]
  mega: PublicMenuItem[]
  footer: PublicMenuItem[]
}

export interface PublicSiteSettings {
  brand: {
    logoLightUrl: string | null
    logoDarkUrl: string | null
    faviconUrl: string | null
    brandColor: string | null
    brandSecondaryColor: string | null
  }
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

/**
 * 選單項目 → 連結。內部連結補上語系前綴（`/about/` → `/zh/about/` 或 `/en/about/`，透過 `localize`，即 `lp`），
 * 外部連結只接受 http／https（其他協定一律視為沒有連結，避免 `javascript:` 之類被塞進選單）。
 */
export function menuItemHref(item: PublicMenuItem, localize: (zhPath: string) => string): string | null {
  const url = item.url?.trim()
  if (!url) return null
  if (item.isExternal) return /^https?:\/\/\S+$/i.test(url) ? url : null
  if (!url.startsWith('/') || url.startsWith('//')) return null
  return localize(`/zh${url}`)
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

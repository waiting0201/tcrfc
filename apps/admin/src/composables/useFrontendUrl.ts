import { activeClubId } from '@/auth/clubAccess'
import { resolveFrontendBaseUrl } from '@/api/runtimeConfig'
import { BLUE_WHALE_CLUB_CODE } from '@/composables/useClubFeatures'

/**
 * 把前台路徑（如 `/zh/news/`）轉成目前俱樂部前台的完整網址。
 * 已是完整網址的原樣回傳；前台網址未設定時回傳 undefined，呼叫端不放連結（不得退回後台網域）。
 */
export function toFrontendUrl(path: string | undefined): string | undefined {
  if (!path) return undefined
  if (/^https?:\/\//i.test(path)) return path
  const base = resolveFrontendBaseUrl(activeClubId.value === BLUE_WHALE_CLUB_CODE)
  if (!base) return undefined
  return `${base}${path.startsWith('/') ? '' : '/'}${path}`
}

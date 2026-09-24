/**
 * 公開的 `GET /api/v1/clubs`（`Features/Clubs/ClubsEndpoints.cs`）——不需要登入。
 * 站台切換器用它組出「系統裡有哪些俱樂部」，見 `@/auth/clubAccess.ts` 的說明與已知限制
 * （目前沒有「查詢目前帳號被授權哪些俱樂部」的自助端點，這是本輪回報的 API 缺口之一）。
 */
import { apiRequest } from './http'

export interface PublicClubDto {
  code: string
  name: string
  description?: string | null
  domain: string
  logoLightKey?: string | null
  logoDarkKey?: string | null
  faviconKey?: string | null
  ogImageKey?: string | null
  brandColor?: string | null
  brandSecondaryColor?: string | null
  defaultLocale: string
}

export function listPublicClubs(): Promise<PublicClubDto[]> {
  return apiRequest<PublicClubDto[]>('/api/v1/clubs?lang=zh')
}

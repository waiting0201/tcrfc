// shared/utils/press.ts — 7.8 媒體專區（S2-12）的公開 API 型別與純函式
//
// 對應 apps/api `GET /api/v1/{club}/press?type=&lang=&page=&pageSize=`（後台 B6，只回已發布；共同列〔club_id 為空〕
// 一併列出、專屬優先）與 `GET /api/v1/{club}/press/{slug}/download`（累計下載次數後 302 轉址到檔案）。
// 檔案**不直接給網址**：一律經同源代理 `/api/backend/{club}/press/{slug}/download`（server/api/backend/[...path].ts
// 不跟隨轉址、把 Location 原樣回給瀏覽器），所以下載次數才會被後端累計。

export type PressResourceType = 'press_release' | 'brand_kit' | 'hires_image'

export interface PressResource {
  id: string
  slug: string
  resourceType: PressResourceType | string
  title: string | null
  description: string | null
  publishedOn: string | null
  fileBytes: number | null
  /** `.pdf`／`.zip`／`.webp` 等（含點）。 */
  fileExtension: string | null
  coverUrl: string | null
  /** API 回傳的相對路徑（`/api/v1/{club}/press/{slug}/download`）；前台改用 pressDownloadHref()，不直接使用。 */
  downloadPath: string
}

/** 同源下載連結（走 Nuxt 代理，見檔頭）。slug 只允許小寫英數與連字號，否則回傳 null（不信任資料）。 */
export function pressDownloadHref(club: string, slug: string): string | null {
  return /^[a-z0-9-]+$/.test(slug) ? `/api/backend/${club}/press/${slug}/download` : null
}

/** 檔案大小：`1.2 MB`／`340 KB`；沒有大小回傳 null。 */
export function formatFileSize(bytes: number | null): string | null {
  if (bytes == null || bytes <= 0) return null
  if (bytes >= 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`
  return `${Math.max(1, Math.round(bytes / 1024))} KB`
}

/** 檔案類型標籤：`PDF`／`ZIP`／`圖片（WEBP）`；認不得的副檔名回傳 null。 */
export function fileKindLabel(ext: string | null): string | null {
  const e = (ext ?? '').replace('.', '').toLowerCase()
  if (!e) return null
  if (e === 'pdf' || e === 'zip') return e.toUpperCase()
  if (['webp', 'jpg', 'jpeg', 'png'].includes(e)) return `圖片（${e.toUpperCase()}）`
  return null
}

// app/utils/crest.ts — 對手隊徽（`opponentCrest`／積分榜 `crest`）取圖網址：優先 320 衍生檔，圖片網址一律過 safeImageUrl（E-301）。

export interface CrestUrls {
  url: string
  url1280?: string | null
  url640?: string | null
  url320?: string | null
}

export function crestSrc(c: CrestUrls | null | undefined): string | null {
  if (!c) return null
  return safeImageUrl(c.url320) ?? safeImageUrl(c.url640) ?? safeImageUrl(c.url)
}

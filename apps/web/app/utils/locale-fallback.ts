// app/utils/locale-fallback.ts — 英文頁上「這批內容後端回的是繁中備援」的判斷（C-6／S2-13）
//
// 後端對 `?lang=en` 逐欄位回退（見 apps/api/README.md），回應列有 `isFallbackLocale`
// （新聞、FAQ、政策、球員、課程、夥伴等 DTO）。宣告 `enReady: true` 的頁面（版面文字已全數
// 翻成英文）若同時顯示這類 API 內容，用這支函式決定要不要在內容上方出示
// <LocaleFallbackNotice partial />：任何一筆是備援，就提示「部分內容只有繁體中文」。
// 不放進 layout：layout 的提示在 SSR 時先於頁面資料轉譯，讀不到頁面才拿到的資料。
export function hasFallbackLocale(
  data: unknown,
): boolean {
  if (Array.isArray(data)) return data.some((x) => hasFallbackLocale(x))
  if (data && typeof data === 'object') {
    const o = data as Record<string, unknown>
    if (o.isFallbackLocale === true) return true
    if (Array.isArray(o.items)) return o.items.some((x) => hasFallbackLocale(x))
  }
  return false
}

// app/utils/safe-image-url.ts — 後端回傳的圖片網址放進 <img src> 前的唯一檢查（E-301）
//
// 放行：`https://…`、站內路徑（`/` 開頭但不是 `//`、`/\`）。
// 開發模式（import.meta.dev）才多放行 `http://`——本機 API 接 Azurite 時圖片網址是
// `http://127.0.0.1:10000/…`；正式環境行為不變，仍只收 https。
// 拒絕：`javascript:`／`data:`／`vbscript:`、協定相對 `//host`、其他一切。
// 頁面不得自己複製正規式（8 份複製品曾漏掉 `//evil.com`），一律用這個函式。

/** 純函式版本（可單元測試）：`allowHttp` 由呼叫端決定，預設不放行。 */
export function checkImageUrl(u: unknown, allowHttp = false): string | null {
  if (typeof u !== 'string') return null
  const v = u.trim()
  if (!v) return null
  if (/^https:\/\//i.test(v)) return v
  if (allowHttp && /^http:\/\//i.test(v)) return v
  if (v.startsWith('/') && !v.startsWith('//') && !v.startsWith('/\\')) return v
  return null
}

export function safeImageUrl(u: string | null | undefined): string | null {
  return checkImageUrl(u, !!import.meta.dev)
}

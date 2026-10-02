// app/utils/siteImage.ts — 站台靜態照片的唯一出口（客戶照片不進建置、不進映像檔）
//
// 🔴 為什麼存在：`apps/web/public/assets/img/` 的客戶照片（含未成年學員肖像）不納版控、
// 不得進公開映像檔。Nuxt 的 Vue SFC 編譯器會把模板裡**靜態**的 `src="/assets/img/…"`
// 轉成 `import` 並在建置時解析，檔案不存在（乾淨 checkout、GitHub Actions）就整個 build
// 失敗（UNRESOLVED_IMPORT）。因此全站一律走 `siteImg('/assets/img/…')`（動態綁定，
// 編譯器不會碰），照片由執行期設定決定從哪裡來。
//
// 契約（與 infra/README.md「站台照片」一節一致，不得自行更改）：
//   - 設了 `NUXT_PUBLIC_MEDIA_BASE_URL`（runtimeConfig.public.mediaBaseUrl，
//     形如 https://<帳戶>.blob.core.windows.net/images，日後可換 CDN 網域）：
//       `/assets/img/<路徑>.<任一副檔名>` → `${base}/site/<路徑>.webp`
//     例：/assets/img/academy/life-09.jpg → ${base}/site/academy/life-09.webp
//   - 未設定（本機開發）：回傳原路徑，讀本機 public，行為不變。
//   - .svg 不上傳 Blob、留在 repo，任何情況都回傳原路徑。
//
// 參數必須是 `/assets/img/…` 開頭的**字面字串**（或 `/assets/img/news/${slug}.jpg` 這種
// 受控的動態前綴）：`scripts/check-site-images.mjs`（掛在 `npm run lint`）靠這個寫法
// 抽出「實際被引用的照片」並與 `scripts/site-images.txt` 比對。

const LOCAL_PREFIX = '/assets/img/'

/** 純函式版本（可單元測試、不依賴 Nuxt context）。 */
export function resolveSiteImg(path: string, mediaBaseUrl: string | undefined | null): string {
  const base = (mediaBaseUrl ?? '').trim().replace(/\/+$/, '')
  if (!base || !path.startsWith(LOCAL_PREFIX) || /\.svg$/i.test(path)) return path
  const rel = path.slice(LOCAL_PREFIX.length).replace(/\.[^./]+$/, '.webp')
  return `${base}/site/${rel}`
}

/** 在 setup／模板／composable 內使用（需要 Nuxt context 才讀得到 runtimeConfig）。
 *  SSR 與 client 讀同一份 runtimeConfig.public（payload 傳遞），輸出一致，無 hydration mismatch。 */
export function siteImg(path: string): string {
  return resolveSiteImg(path, useRuntimeConfig().public.mediaBaseUrl as string | undefined)
}

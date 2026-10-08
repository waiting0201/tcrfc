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
//   - 衍生檔（E-297 補充）：`infra/upload-site-images.sh` 另外上傳 `<鍵去 .webp>-1280／-640／-320.webp`，
//     命名與後台上傳同一套（apps/api/Images/ImageObjectKey）。`siteImgSrcset()` 回傳對應 srcset；
//     本機開發（未設 base）與 .svg 回 undefined，模板的 :srcset／:sizes 綁 undefined 即不輸出屬性。
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

/** 規劃書 §4.0 固定產出的長邊衍生檔（與 apps/api/Images/ImageUploadOptions.DerivativeLongEdges 一致）。 */
export const DERIVATIVE_EDGES = [1280, 640, 320] as const

/**
 * 主檔網址 → 1280／640／320 衍生檔 srcset（w 描述子為衍生檔寬度）。
 * 衍生檔網址規則由後端定義、前台照抄，不自創：`<主檔鍵去掉 .webp>-<長邊>.webp`
 * （apps/api/Images/ImageObjectKey.ForLongEdge，規劃書 §4.0「鍵由主鍵推導，不另存欄位」）。
 * 網址不是 .webp、寬高缺，就回 undefined，退回只用 src，不猜。
 *
 * `capToSource`：
 *   - true（後台上傳封面）：width／height 是主檔**真實**尺寸；主檔長邊小於目標時後端沿用主檔，
 *     寬度取主檔寬度。
 *   - false（站台照片）：width／height 只當**比例**用（模板上的 width／height 屬性常是顯示尺寸，
 *     不等於主檔像素）；描述子照名目長邊算。主檔小於 1280 時 -1280 物件就是主檔，
 *     瀏覽器挑到它等於挑主檔，選圖結果與真實描述子相同。
 */
export function derivativeSrcset(
  url: string,
  width: number | null | undefined,
  height: number | null | undefined,
  capToSource: boolean,
): string | undefined {
  if (!width || !height || width <= 0 || height <= 0) return undefined
  const m = /^([^?#]+)\.webp((?:[?#].*)?)$/i.exec(url)
  if (!m) return undefined
  const [, stem, tail] = m
  const longEdge = Math.max(width, height)
  const seen = new Set<number>()
  const parts: string[] = []
  for (const edge of DERIVATIVE_EDGES) {
    const w = capToSource && longEdge <= edge ? width : Math.round(width * edge / longEdge)
    if (seen.has(w)) continue // 重複的描述子是無效 srcset；後端沿用主檔的情況取第一個
    seen.add(w)
    parts.push(`${stem}-${edge}.webp${tail} ${w}w`)
  }
  return parts.join(', ')
}

/** 純函式版本：站台照片的 srcset。未設 mediaBaseUrl（本機讀 public）或 .svg → undefined。 */
export function resolveSiteImgSrcset(
  path: string,
  mediaBaseUrl: string | undefined | null,
  width: number | null | undefined,
  height: number | null | undefined,
): string | undefined {
  const base = (mediaBaseUrl ?? '').trim().replace(/\/+$/, '')
  if (!base || !path.startsWith(LOCAL_PREFIX) || /\.svg$/i.test(path)) return undefined
  return derivativeSrcset(resolveSiteImg(path, base), width, height, false)
}

/** 站台照片的 srcset（需 Nuxt context）。width／height＝該 <img> 的 width／height 屬性（只取比例）。 */
export function siteImgSrcset(path: string, width: number, height: number): string | undefined {
  return resolveSiteImgSrcset(path, useRuntimeConfig().public.mediaBaseUrl as string | undefined, width, height)
}

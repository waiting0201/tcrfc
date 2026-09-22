/**
 * 圖片上傳元件的瀏覽器端前置處理：HEIC 轉檔與尺寸讀取。
 *
 * HEIC 決定見 docs/17-deployment.md §6（2026-09-20 定案）：伺服器端（`ImageSharp`）不解 HEIC，
 * 一律由**前端在瀏覽器把 HEIC 轉成 JPEG 再送**——理由是伺服器端加 HEIF 解碼元件會牽進 HEVC
 * 專利授權，一個俱樂部官網不值得為此擔這個坑。⚠️ 該決定明文：「伺服器端仍要擋，不得假設前端
 * 一定轉過」——iPhone 拍的照片預設是 HEIC，Safari 透過 `<input type="file">` 上傳時多半已自動
 * 轉成 JPEG，但**不能當成保證**，拖曳與部分版本仍會送出原始 `.heic`，所以這裡仍要主動偵測並轉檔，
 * 不能依賴瀏覽器自己處理。
 *
 * 🔴 `heic2any` 動態 import（不是檔頭靜態 import）：它內建一份 WASM 版 HEIF 解碼器，
 * 打包後接近 1.4MB，靜態 import 會讓這包體積綁進「新聞編輯頁」這個一般人天天會開的頁面的主
 * chunk（`vite build` 曾經因此跳出 chunk 過大警告）。改成只有真的選到 HEIC 檔案才動態載入，
 * 大多數使用者（選 JPG／PNG）完全不會下載到這包。
 */

/** 後端實際接受的格式（apps/api/README.md「給前端接的契約」錯誤文案），前端沿用同一份清單
 * 做選檔後的第一道擋線，不是要取代伺服器端的 magic bytes 驗證（那個才是最終依據）。 */
const ACCEPTED_MIME_TYPES = ['image/jpeg', 'image/png', 'image/webp']
const ACCEPTED_EXTENSIONS = /\.(jpe?g|png|webp)$/i

function looksLikeHeic(file: File): boolean {
  const name = file.name.toLowerCase()
  return name.endsWith('.heic') || name.endsWith('.heif')
    || file.type === 'image/heic' || file.type === 'image/heif'
}

/** 副檔名／MIME 都不像常見支援格式（且不是可轉檔的 HEIC）——這只是前端的第一道粗篩，
 * 假副檔名這種情況前端本來就篩不出來，交給伺服器端的內容偵測把關（見 apps/api/README.md）。 */
export function looksUnsupportedFormat(file: File): boolean {
  if (looksLikeHeic(file)) return false
  if (ACCEPTED_MIME_TYPES.includes(file.type)) return false
  return !ACCEPTED_EXTENSIONS.test(file.name)
}

export class HeicConversionError extends Error {}

/**
 * 是 HEIC／HEIF 才轉檔，其餘格式原封不動回傳（不做「大圖也轉檔」這種額外行為——長邊縮放與
 * 重新編碼一律是伺服器端 `ImageProcessor` 的責任，前端只解決「伺服器端解不開的格式」這一件事）。
 */
export async function convertHeicIfNeeded(file: File): Promise<File> {
  if (!looksLikeHeic(file)) return file

  let converted: Blob
  try {
    const { default: heic2any } = await import('heic2any')
    const result = await heic2any({ blob: file, toType: 'image/jpeg', quality: 0.92 })
    converted = Array.isArray(result) ? result[0] : result
  } catch (cause) {
    throw new HeicConversionError(
      cause instanceof Error ? cause.message : String(cause),
    )
  }

  const newName = file.name.replace(/\.(heic|heif)$/i, '') || 'converted'
  return new File([converted], `${newName}.jpg`, { type: 'image/jpeg' })
}

export interface ImageDimensions {
  width: number
  height: number
}

/** 讀取圖片實際像素尺寸，用來做前端的解析度下限檢查（伺服器端沒有這一層，見
 * apps/api/README.md「已知缺口」第 2 點）。檔案本身無法被瀏覽器解成圖片時 reject。 */
export function readImageDimensions(file: File): Promise<ImageDimensions> {
  return new Promise((resolve, reject) => {
    const objectUrl = URL.createObjectURL(file)
    const img = new Image()
    img.onload = () => {
      resolve({ width: img.naturalWidth, height: img.naturalHeight })
      URL.revokeObjectURL(objectUrl)
    }
    img.onerror = () => {
      URL.revokeObjectURL(objectUrl)
      reject(new Error('無法辨識為圖片'))
    }
    img.src = objectUrl
  })
}

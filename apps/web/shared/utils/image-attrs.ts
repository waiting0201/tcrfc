// shared/utils/image-attrs.ts — 後端圖片欄位組（物件鍵、寬、高、雙語 Alt）→ <img> 屬性的唯一出口
//
// 規劃書 §4.0：每個圖片欄位是一組。公開 API 對每個圖片欄位給 `{名稱}Width`／`{名稱}Height`／`{名稱}Alt`
// （既有列未回填時寬高為 null；沒有圖片或肖像未同意時三者皆 null）。
//   - 寬高：兩者都是正整數才輸出；任一為 null 就**都不帶**（不發明尺寸，由 CSS 決定版面）。
//   - Alt：`imgAlt(後台 Alt, ...回退文字)`——第一個 trim 後非空的字串；全部為空回 ''。
//     回退順序由呼叫端依內容決定（球員／教練＝姓名、標誌＝夥伴／贊助商／組織名稱、封面＝標題、商品＝商品名）；
//     **只有裝飾性圖片**才直接寫 alt=""。
// 用法：<img :src="…" :alt="imgAlt(p.photoAlt, p.name)" v-bind="imgAttrs(p.photoWidth, p.photoHeight)">

export interface ImgSizeAttrs {
  width?: number
  height?: number
}

export function imgAttrs(width?: number | null, height?: number | null): ImgSizeAttrs {
  if (typeof width === 'number' && typeof height === 'number' && width > 0 && height > 0) {
    return { width, height }
  }
  return {}
}

export function imgAlt(...candidates: Array<string | null | undefined>): string {
  for (const c of candidates) {
    const t = typeof c === 'string' ? c.trim() : ''
    if (t) return t
  }
  return ''
}

/**
 * Schema.org（JSON-LD）的 image：寬高都有時輸出 ImageObject（url＋width＋height），否則維持單純網址字串
 * （不發明尺寸）；沒有網址回 undefined（欄位不輸出）。比照 news/[slug] 的 Article 寫法。
 */
export function schemaImage(url?: string | null, width?: number | null, height?: number | null): string | { '@type': 'ImageObject', url: string, width: number, height: number } | undefined {
  if (!url) return undefined
  const size = imgAttrs(width, height)
  return size.width && size.height ? { '@type': 'ImageObject', url, width: size.width, height: size.height } : url
}

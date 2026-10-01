// shared/utils/shop-schema.ts — GEO-05 結構化資料第三批（S3-5a）：商店商品 Product ＋ Offer 純函式
//
// 主站規劃書 §7「結構化資料型別清單」的 Product／Offer（價格、幣別、供貨狀態）。商品詳情頁
// `app/pages/zh/shop/[slug]/index.vue` 透過 `app/composables/useProductSchema.ts` 呼叫本檔。
// 判斷邏輯抽到純函式，讓 `scripts/check-shop-lib.mjs` 不啟動任何服務就能驗證（比照 schema-batch2.ts）。
//
// 🔴 GEO-05「資料不足時不輸出該型別」：
//   - 商品沒有名稱 → 不輸出；
//   - 沒有任何「有有限正價格」的規格 → 不輸出（沒有價格的 Offer 對搜尋引擎無意義，也不得編一個價格）；
//   - `image`／`description`／`sku`／`brand` 沒有資料就不帶，不放空字串或預設值；
//   - 供貨狀態只能由後端的 `purchasable`／`availableQty` 推得：可購買且 `availableQty > 0`＝`InStock`
//     （可售量低於 5 件用 `LimitedAvailability`），其餘＝`OutOfStock`。後端回報的可售量上限是 99，
//     這裡只用「有沒有」與「是否緊張」兩種判斷，不輸出具體庫存數量（不洩漏精確庫存）。
//
// 🔴 同頁多節點唯一 `@id`（E-96）：Product 節點用 `@id`＝`product-{slug}`（相對 id，由 nuxt-schema-org
// 依頁面網址解析成 `…/#/schema/product/product-{slug}`）；巢狀的 AggregateOffer／Offer **自己組絕對 id**
// （`{頁面網址}#offers`、`{頁面網址}#offer-{sku}`——模組只解析頂層節點的相對 id，巢狀節點的相對字串會原樣輸出），
// 沒有任何兩個節點共用 id，也不會與站台身分節點合併。
// 🔴 AggregateOffer 的 `availability` 必須明確給（模組會替沒有值的 Offer 補預設 `InStock`，全部規格售完時會說謊）：
// 任一規格可購買＝InStock，否則 OutOfStock。
import { cleanFaqSchemaText } from './faq-schema.ts'
import type { ShopProductDetail, ShopVariant } from './shop.ts'

const IN_STOCK = 'https://schema.org/InStock'
const LIMITED = 'https://schema.org/LimitedAvailability'
const OUT_OF_STOCK = 'https://schema.org/OutOfStock'
const LOW_STOCK_THRESHOLD = 5

export interface ProductSchemaOpts {
  /** 品牌名稱（俱樂部簡稱）。 */
  brandName: string
  /** 商品詳情頁的完整網址（含語系與結尾斜線）。 */
  pageUrl: string
  /** 賣方名稱（收款主體俱樂部名稱）；沒有就不輸出 `seller`。 */
  sellerName?: string | null
}

export function variantAvailability(v: Pick<ShopVariant, 'purchasable' | 'availableQty'>): string {
  if (!v.purchasable || v.availableQty <= 0) return OUT_OF_STOCK
  return v.availableQty < LOW_STOCK_THRESHOLD ? LIMITED : IN_STOCK
}

function isPositiveFinite(n: unknown): n is number {
  return typeof n === 'number' && Number.isFinite(n) && n > 0
}

/** 有「有限正價格」的規格才可輸出 Offer。 */
export function priceableVariants(product: Pick<ShopProductDetail, 'variants'>): ShopVariant[] {
  return product.variants.filter(v => isPositiveFinite(v.price))
}

export function isProductSchemaEligible(product: ShopProductDetail | null | undefined): product is ShopProductDetail {
  if (!product) return false
  if (!product.name || !cleanFaqSchemaText(product.name)) return false
  return priceableVariants(product).length > 0
}

function offerNode(v: ShopVariant, productId: string, opts: ProductSchemaOpts): Record<string, unknown> {
  const node: Record<string, unknown> = {
    '@type': 'Offer',
    '@id': `${opts.pageUrl}#offer-${v.sku}`,
    sku: v.sku,
    price: v.price,
    priceCurrency: 'TWD',
    availability: variantAvailability(v),
    itemCondition: 'https://schema.org/NewCondition',
    url: opts.pageUrl,
  }
  if (opts.sellerName) node.seller = { '@type': 'Organization', name: opts.sellerName }
  return node
}

/**
 * 輸出 `defineProduct()` 要吃的純資料物件；不合格回 `null`（呼叫端不得呼叫 `useSchemaOrg`）。
 * 單一規格 → `offers` 是單一 Offer；多個規格 → `AggregateOffer`（`lowPrice`／`highPrice`／`offerCount`）內含各規格 Offer。
 */
export function buildProductSchemaNode(product: ShopProductDetail | null | undefined, opts: ProductSchemaOpts): Record<string, unknown> | null {
  if (!isProductSchemaEligible(product)) return null
  const variants = priceableVariants(product)
  const productId = `product-${product.slug}`

  const node: Record<string, unknown> = {
    '@id': productId,
    name: cleanFaqSchemaText(product.name!),
    url: opts.pageUrl,
    brand: { '@type': 'Brand', name: opts.brandName },
  }

  const description = cleanFaqSchemaText(product.seoDescription || product.narrative || '')
  if (description) node.description = description.length > 300 ? `${description.slice(0, 299)}…` : description

  const images = product.images.map(i => i.url).filter(u => typeof u === 'string' && /^https?:\/\//i.test(u))
  if (images.length) node.image = images

  if (variants.length === 1) {
    node.sku = variants[0]!.sku
    node.offers = offerNode(variants[0]!, productId, opts)
  }
  else {
    const prices = variants.map(v => v.price)
    node.offers = {
      '@type': 'AggregateOffer',
      '@id': `${opts.pageUrl}#offers`,
      priceCurrency: 'TWD',
      availability: variants.some(v => variantAvailability(v) !== OUT_OF_STOCK) ? IN_STOCK : OUT_OF_STOCK,
      lowPrice: Math.min(...prices),
      highPrice: Math.max(...prices),
      offerCount: variants.length,
      offers: variants.map(v => offerNode(v, productId, opts)),
    }
  }
  return node
}

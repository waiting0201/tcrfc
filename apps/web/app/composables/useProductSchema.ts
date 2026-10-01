// app/composables/useProductSchema.ts — GEO-05 第三批（S3-5a）商品 Product＋Offer 結構化資料
//
// 判斷與組裝全在 `shared/utils/shop-schema.ts`（純函式，`scripts/check-shop-lib.mjs` 驗證）；
// 這裡只負責接上 `useSchemaOrg`／`defineProduct`。資料不合格（無名稱、或沒有任何有正價格的規格）
// 時 `buildProductSchemaNode` 回 `null`，完全不呼叫 `useSchemaOrg`（GEO-05「資料不足時不輸出該型別」）。
import type { MaybeRefOrGetter } from 'vue'
import { buildProductSchemaNode } from '#shared/utils/shop-schema'
import type { ShopProductDetail } from '#shared/utils/shop'

export function useProductSchema(
  product: MaybeRefOrGetter<ShopProductDetail | null | undefined>,
  opts: { brandName: MaybeRefOrGetter<string>, pageUrl: MaybeRefOrGetter<string>, sellerName?: MaybeRefOrGetter<string | null | undefined> },
) {
  watchEffect(() => {
    const node = buildProductSchemaNode(toValue(product), {
      brandName: toValue(opts.brandName),
      pageUrl: toValue(opts.pageUrl),
      sellerName: opts.sellerName ? toValue(opts.sellerName) : null,
    })
    if (!node) return
    useSchemaOrg([defineProduct(node)])
  })
}

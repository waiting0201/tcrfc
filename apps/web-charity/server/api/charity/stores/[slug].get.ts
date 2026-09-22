// GET /api/charity/stores/:slug — 掃碼落地頁 `/{lang}/s/<store_slug>` 用。
// 規劃書 §2.2 第 5 條：slug 對應不到有效店家時視同無店家歸屬，不得報錯中斷捐款流程——
// 這裡回傳 null（而不是 404），交給頁面自己決定要不要降級成一般入口版面。
export default defineEventHandler((event) => {
  const slug = getRouterParam(event, 'slug') ?? ''
  return findStoreBySlug(slug)
})

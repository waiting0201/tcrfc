// shared/utils/api-types.ts — 多支公開端點共用的回應外殼型別
//
// 對應 apps/api `Common/PagedResult<T>`（`Items`／`Page`／`PageSize`／`TotalCount`／`TotalPages`，camelCase 序列化）。
export interface PagedResponse<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

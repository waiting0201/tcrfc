/** 商店訂單狀態的標籤顏色（訂單狀態本身就是中文）。 */
export function orderStatusTag(status: string): 'success' | 'warning' | 'info' | 'danger' | undefined {
  switch (status) {
    case '已付款':
    case '備貨中':
      return 'warning'
    case '已出貨':
      return undefined
    case '已完成':
      return 'success'
    case '已取消':
    case '已退款':
      return 'info'
    case '退貨處理中':
      return 'danger'
    default:
      return 'info'
  }
}

export function refundStatusTag(status: string): 'success' | 'warning' | 'info' | 'danger' | undefined {
  switch (status) {
    case 'requested':
      return 'warning'
    case 'approved':
    case 'received':
    case 'processing':
      return undefined
    case 'refunded':
      return 'success'
    case 'rejected':
      return 'info'
    default:
      return 'info'
  }
}

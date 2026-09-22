// GET /api/charity/donations/:orderNo — 結果頁 `/{lang}/result/<order_no>` 用。
// 只回傳結果頁需要呈現的欄位，不回傳完整個資（Email 在頁面端遮罩顯示，這裡仍原樣回傳
// 由頁面負責遮罩，因為遮罩是呈現規則不是資料存取規則——mockup 沒有身分驗證機制可做細緻的
// 欄位級授權，但頁面渲染時一律套用遮罩，不會把完整 Email 印到畫面上）。
export default defineEventHandler((event) => {
  const orderNo = getRouterParam(event, 'orderNo') ?? ''
  const found = findDonationByOrderNo(orderNo)
  return found
})

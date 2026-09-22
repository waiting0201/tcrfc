// GET /api/charity/stores — 目前只有一般入口需要「無店家歸屬」，此端點暫時不對頁面開放使用，
// 保留給之後可能需要的「店家一覽」用途。維持存在是為了讓 fixtures 的讀取入口成組、好維護。
export default defineEventHandler(() => listActiveStores())

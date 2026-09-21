/**
 * 「這裡管理的是：{前台單元中文名稱} ↗」（docs/21-admin-ui.md §6，規劃書 §4.0 前後台對照表）。
 *
 * - none：無前台產出（儀表板、系統管理），畫面文字改成灰色說明，不放假連結
 * - link：對應單一前台頁面／單元，可點擊開新分頁
 * - multi：對應多個前台頁面（如頁面管理對應一整批靜態頁），連結改指到入口頁或網站地圖
 * - app：只在行動 App 呈現，網頁前台沒有對應頁面，也不放假連結，但仍要說明理由（本檔案為此新增的
 *   第三種變體，docs/21 §6 原本只寫了 none／link／multi 三種行為中的兩種＋一個「多頁」例外，
 *   沒有涵蓋「後台模組確實有產出，但產出的是 App 不是網頁」這種情況，屬實作時發現需要回填的落差）
 */
export type FrontendUnitLinkType = 'none' | 'link' | 'multi' | 'app'

export interface FrontendUnitInfo {
  linkType: FrontendUnitLinkType
  /** 前台單元中文名稱，畫面直接顯示 */
  label: string
  /** linkType 為 link／multi 時的前台網址（mock 環境不保證能實際開啟，見 apps/admin/README.md） */
  url?: string
}

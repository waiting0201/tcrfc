// app/composables/useYearChips.ts — 年份篩選鈕的共用行為
// （about/milestones、charity/impact-stories 兩頁共用同一份 17 行 client script，
// docs/13-blue-whale-site.md §6：務必做成元件／共用邏輯，不要複製兩次）。
//
// 初始狀態固定 'all'，對應 mockup 原始 HTML：「全部」鈕 aria-pressed="true"、
// 其餘鈕 "false"、所有 .timeline-year 皆無 hidden 屬性（全部可見）——用 computed
// 直接綁定即可與 SSR 輸出一致，不需要像新聞列表那樣等 client 掛載後才套用篩選。
export function useYearChips() {
  const activeYear = ref('all')

  function isPressed(year: string): boolean {
    return activeYear.value === year
  }

  function isPanelHidden(panelYear: string): boolean {
    return !(activeYear.value === 'all' || activeYear.value === panelYear)
  }

  return { activeYear, isPressed, isPanelHidden }
}

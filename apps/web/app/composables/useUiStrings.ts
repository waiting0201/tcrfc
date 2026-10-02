// app/composables/useUiStrings.ts — 介面字串翻譯表（I4 多語系管理 → 字串翻譯表，H 批，2026-10-02）。
//
// `GET /api/v1/ui-strings?lang=&group=` → `{ locale, strings: { 字串代號: 文字 } }`（全站共用，不分俱樂部；缺該語系回退繁中）。
//
// 🔴 **機制 + 少數示範位置，不是全面替換**：前台現有的寫死中文字串數以千計（導覽、表單標籤、錯誤訊息……），
// 逐一換成 `t('代號', '原文')` 工程過大且風險高（每一處都要先在後台建立字串代號）。本 composable 提供：
//   - `t(key, fallback)`：查得到翻譯就用，查不到（API 沒資料、代號不存在、API 失敗）回 `fallback`（＝原本寫死的文字）。
//     所以**把某處寫死字串改成 t('代號', '原文') 永遠是安全的**，行為與改動前相同，直到後台建立該代號才會生效。
//   - 目前示範位置：頁尾電子報訂閱區（標題、說明、按鈕與回饋訊息，代號 `newsletter.*`）。其餘位置待後續逐步替換。
// 與頁面其他資料一樣在 SSR 取好、payload 帶到瀏覽器，不閃爍。
export function useUiStrings(group?: string) {
  const { locale } = useLocale()
  const { data } = useFetch<{ locale: string, strings: Record<string, string> }>('/api/backend/ui-strings', {
    query: computed(() => ({ lang: locale.value, ...(group ? { group } : {}) })),
    key: `ui-strings-${locale.value}-${group ?? 'all'}`,
  })
  const strings = computed(() => data.value?.strings ?? {})
  function t(key: string, fallback: string): string {
    const v = strings.value[key]
    return typeof v === 'string' && v.trim() ? v : fallback
  }
  return { t, strings }
}

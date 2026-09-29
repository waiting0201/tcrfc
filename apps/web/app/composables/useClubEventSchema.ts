// app/composables/useClubEventSchema.ts — GEO-05／GEO-08 俱樂部活動（calendar_custom_events）
// Event 結構化資料（S1-20）
//
// 主站規劃書 §7 結構化資料型別清單「Event」。對應 `app/pages/zh/schedule.vue`「俱樂部活動」
// 分頁（S1-19 補完已接上 `GET /api/v1/{club}/calendar/events?team=club`）。`nuxt-schema-org`
// 有專用的 `defineEvent()` 定義器（不像 SportsEvent 沒有專用型別要手刻原始 JSON-LD，見
// `app/pages/zh/schedule.vue` 檔頭既有說明），直接使用，寫法比照
// `app/composables/useFaqPageSchema.ts`（`useSchemaOrg` ＋ 專用定義器）。
//
// 判斷邏輯（哪些活動合格、欄位怎麼組）抽到 `shared/utils/schema-batch2.ts`
// （`buildClubEventSchemaNodes`），這裡只負責接上 `useSchemaOrg`／`defineEvent`，
// 不合格時整批可能為空陣列，此時完全不呼叫 `useSchemaOrg`（GEO-05「資料不足時不輸出」）。
import type { MaybeRefOrGetter } from 'vue'
import { buildClubEventSchemaNodes, type ClubEventSchemaSourceItem } from '#shared/utils/schema-batch2'

export function useClubEventSchema(
  items: MaybeRefOrGetter<ClubEventSchemaSourceItem[]>,
  opts: { siteUrl: MaybeRefOrGetter<string>; pagePath: MaybeRefOrGetter<string> },
) {
  watchEffect(() => {
    const nodes = buildClubEventSchemaNodes(toValue(items), {
      siteUrl: toValue(opts.siteUrl),
      pagePath: toValue(opts.pagePath),
    })
    if (nodes.length === 0) return
    useSchemaOrg(nodes.map((n) => defineEvent(n)))
  })
}

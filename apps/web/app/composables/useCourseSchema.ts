// app/composables/useCourseSchema.ts — GEO-05 課程（training_programs）Course 結構化資料
// （S1-20）
//
// 主站規劃書 §7 結構化資料型別清單「Course」。對應 05 課程與活動已接 API 的頁面
// （`programs/childrens-training/index.vue`／`programs/summer-camp/index.vue`，S1-15 既有
// 接線）。`nuxt-schema-org` 有專用的 `defineCourse()` 定義器，直接使用，寫法比照
// `app/composables/useFaqPageSchema.ts`。
//
// 判斷邏輯（合不合格、欄位怎麼組）抽到 `shared/utils/schema-batch2.ts`
// （`buildCourseSchemaNode`），這裡只負責接上 `useSchemaOrg`／`defineCourse`。資料不合格
// （現況：`programs` 表兩俱樂部皆 0 筆種子資料）時回傳 `null`，完全不呼叫 `useSchemaOrg`
// （GEO-05「資料不足時不輸出該型別」）。
import type { MaybeRefOrGetter } from 'vue'
import { buildCourseSchemaNode, type CourseSchemaSourceProgram } from '#shared/utils/schema-batch2'

export function useCourseSchema(
  program: MaybeRefOrGetter<CourseSchemaSourceProgram | null>,
  opts: { providerName: MaybeRefOrGetter<string>; siteUrl: MaybeRefOrGetter<string> },
) {
  // JSON-LD 的語系看 URL（`locale`），不看 `isEn`：藍鯨未宣告 enReadyBw 的 /en/ 頁，結構化資料仍輸出英文（見 useSchemaOrgClub.ts）。
  const { locale } = useLocale()
  watchEffect(() => {
    const node = buildCourseSchemaNode(toValue(program), {
      locale: locale.value === 'en' ? 'en' : 'zh',
      providerName: toValue(opts.providerName),
      siteUrl: toValue(opts.siteUrl),
    })
    if (!node) return
    useSchemaOrg([defineCourse(node)])
  })
}

// app/composables/useClubIdentity.ts — 俱樂部識別（頁尾簡介／社群／Email）＝ club-copy 過渡值 ＋ 後台 site-facts（C-2）
//
// 合併規則見 shared/utils/club-copy.ts `applySiteFactsToIdentity`。`useSiteFacts` 的兩支 useFetch 以固定 key 去重，
// 頁首／頁尾／聯絡頁同時呼叫不會多打 API。`club` 預設取目前容器（config.public.club），不得傳另一個俱樂部。
import { applySiteFactsToIdentity, getClubIdentity } from '#shared/utils/club-copy'
import { getClubIdentityEnFor } from '#shared/utils/club-copy-en-core'

export function useClubIdentity() {
  const club = useRuntimeConfig().public.club
  const { isEn } = useLocale()
  const { facts } = useSiteFacts(club)
  return computed(() =>
    applySiteFactsToIdentity(
      isEn.value ? getClubIdentityEnFor(club) : getClubIdentity(club),
      facts.value,
      isEn.value ? 'en' : 'zh',
    ),
  )
}

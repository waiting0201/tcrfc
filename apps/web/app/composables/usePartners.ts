// app/composables/usePartners.ts — 09 合作夥伴／贊助商／贊助方案／提案的公開讀取（S2-7）
//
// 一律以目前容器的 `config.public.club` 組網址，**不接受外部傳入其他俱樂部代碼**——藍鯨與磐石
// 的夥伴／贊助商是各自簽約、各自建一筆（docs/14 五種商業對象），不得混列。
// API 失敗（useFetch 的 data 為 null）一律回傳空陣列：頁面據此顯示既有的「尚未公開」空狀態，
// 不出 500、不放假資料（比照 useHomeSections／useFaqEmbed 既有 fail-open 慣例）。
import type { PublicPartner, PublicSponsor, PublicSponsorPackage, PublicProposal } from '#shared/utils/partners'

export async function usePartnerList(options: { home?: boolean } = {}) {
  const config = useRuntimeConfig()
  const { locale } = useLocale()
  const club = config.public.club
  const { data, error } = await useFetch<PublicPartner[]>(`/api/backend/${club}/partners`, {
    query: { lang: locale.value, ...(options.home ? { home: true } : {}) },
    key: `partners-${club}-${locale.value}-${options.home ? 'home' : 'all'}`,
  })
  return { partners: computed(() => data.value ?? []), failed: computed(() => Boolean(error.value)) }
}

export async function useSponsorList() {
  const config = useRuntimeConfig()
  const { locale } = useLocale()
  const club = config.public.club
  const { data, error } = await useFetch<PublicSponsor[]>(`/api/backend/${club}/sponsors`, {
    query: { lang: locale.value },
    key: `sponsors-${club}-${locale.value}`,
  })
  return { sponsors: computed(() => data.value ?? []), failed: computed(() => Boolean(error.value)) }
}

export async function useSponsorPackages() {
  const config = useRuntimeConfig()
  const { locale } = useLocale()
  const club = config.public.club
  const { data, error } = await useFetch<PublicSponsorPackage[]>(`/api/backend/${club}/sponsor-packages`, {
    query: { lang: locale.value },
    key: `sponsor-packages-${club}-${locale.value}`,
  })
  return { packages: computed(() => data.value ?? []), failed: computed(() => Boolean(error.value)) }
}

export async function usePublicProposals() {
  const config = useRuntimeConfig()
  const club = config.public.club
  const { data, error } = await useFetch<PublicProposal[]>(`/api/backend/${club}/proposals`, {
    key: `proposals-${club}`,
  })
  return { proposals: computed(() => data.value ?? []), failed: computed(() => Boolean(error.value)) }
}

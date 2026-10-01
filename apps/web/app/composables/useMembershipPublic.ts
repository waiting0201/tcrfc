// app/composables/useMembershipPublic.ts — 會籍方案、權益對照表、特約店家的公開讀取（S2-11／S3-2）
//
// 一律以容器的 `config.public.club` 組網址（藍鯨與磐石由後端 `club_id` 分區，不混列；
// 特約店家另含「兩隊共同」`isShared`）。API 失敗＝空資料，頁面落回既有空狀態，不出 500、不放假資料
// （比照 usePartners.ts）。這些端點全是公開唯讀、不帶權杖，可安全走 SSR。
import type { MembershipBenefits, MembershipPlan, PartnerStore, PartnerStoreFilters } from '#shared/utils/member'

export async function useMembershipPlans() {
  const config = useRuntimeConfig()
  const { locale } = useLocale()
  const club = config.public.club
  const { data, error } = await useFetch<MembershipPlan[]>(`/api/backend/${club}/membership/plans`, {
    query: { lang: locale.value },
    key: `membership-plans-${club}-${locale.value}`,
  })
  return { plans: computed(() => data.value ?? []), failed: computed(() => Boolean(error.value)) }
}

/** 權益對照表（K4）。**一份資料多處使用**：加入頁、8.2 球迷會頁、會員中心升級頁共用 `ContentMembershipBenefits`。 */
export async function useMembershipBenefits(planCode?: string) {
  const config = useRuntimeConfig()
  const { locale } = useLocale()
  const club = config.public.club
  const { data, error } = await useFetch<MembershipBenefits>(`/api/backend/${club}/membership/benefits`, {
    query: { lang: locale.value, ...(planCode ? { planCode } : {}) },
    key: `membership-benefits-${club}-${locale.value}-${planCode ?? 'default'}`,
  })
  return { benefits: computed(() => data.value ?? null), failed: computed(() => Boolean(error.value)) }
}

export async function usePartnerStoreFilters() {
  const config = useRuntimeConfig()
  const club = config.public.club
  const { data } = await useFetch<PartnerStoreFilters>(`/api/backend/${club}/partner-stores/filters`, {
    key: `partner-store-filters-${club}`,
  })
  return { filters: computed(() => data.value ?? { categories: [], regions: [] }) }
}

export async function usePartnerStores(filters?: () => { category?: string, region?: string, tier?: string }) {
  const config = useRuntimeConfig()
  const { locale } = useLocale()
  const club = config.public.club
  const { data, error, status } = await useFetch<PartnerStore[]>(`/api/backend/${club}/partner-stores`, {
    query: computed(() => {
      const f = filters?.() ?? {}
      return {
        lang: locale.value,
        ...(f.category ? { category: f.category } : {}),
        ...(f.region ? { region: f.region } : {}),
        ...(f.tier ? { tier: f.tier } : {}),
      }
    }),
    key: `partner-stores-${club}-${locale.value}-${filters ? 'filtered' : 'all'}`,
  })
  return { stores: computed(() => data.value ?? []), failed: computed(() => Boolean(error.value)), pending: computed(() => status.value === 'pending') }
}

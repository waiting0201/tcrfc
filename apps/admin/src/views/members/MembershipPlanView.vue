<script setup lang="ts">
/**
 * 會籍與方案：同頁五個分頁——會籍（到期提醒）、待確認申請、方案、付款紀錄、會員編號規則。
 * 目前分頁記在網址的 `?tab=`，重新整理不會跳回第一頁。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MembershipsTab from './parts/MembershipsTab.vue'
import ApplicationsTab from './parts/ApplicationsTab.vue'
import PlansTab from './parts/PlansTab.vue'
import PaymentsTab from './parts/PaymentsTab.vue'
import NumberingTab from './parts/NumberingTab.vue'
import { usePermission } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { listMembershipApplications, listMembershipPlans, listMembershipSeasons, type MembershipPlanListItemDto } from '@/api/adminMemberships'

const TABS = ['memberships', 'applications', 'plans', 'payments', 'numbering'] as const
type TabName = (typeof TABS)[number]

const route = useRoute()
const router = useRouter()
const canViewMemberships = usePermission('member.membership.view')
const canViewPlans = usePermission('member.plan.view')
const canViewSettings = usePermission('member.setting.view')
const club = computed(() => activeClubId.value)

function readTab(): TabName {
  const q = route.query.tab
  return typeof q === 'string' && (TABS as readonly string[]).includes(q) ? (q as TabName) : 'memberships'
}
const tab = ref<TabName>(readTab())
watch(tab, (t) => {
  if (readTab() !== t) router.replace({ query: { ...route.query, tab: t } })
})
watch(
  () => route.query.tab,
  () => {
    if (route.name === 'membership-plan-list') tab.value = readTab()
  },
)

// 各分頁共用的參考資料：球季、方案
const plans = ref<MembershipPlanListItemDto[]>([])
const seasonsFromApi = ref<{ id: string; code: string }[]>([])
const seasons = computed(() => {
  const map = new Map<string, string>()
  for (const s of seasonsFromApi.value) map.set(s.id, s.code)
  // 球季清單來自會籍專用端點；萬一讀取失敗，至少用方案上出現過的球季
  for (const p of plans.value) if (!map.has(p.seasonId)) map.set(p.seasonId, p.seasonCode)
  return [...map.entries()].map(([id, code]) => ({ id, code })).sort((a, b) => b.code.localeCompare(a.code))
})

async function loadRefs() {
  const [s, p] = await Promise.allSettled([listMembershipSeasons(club.value), listMembershipPlans(club.value)])
  seasonsFromApi.value = s.status === 'fulfilled' ? s.value.map((x) => ({ id: x.id, code: x.code })) : []
  plans.value = p.status === 'fulfilled' ? p.value : []
}
// 「待確認申請」分頁標籤上的待辦數（只取總筆數，不載入清單）
const pendingCount = ref(0)
async function loadPendingCount() {
  if (!canViewMemberships.value) return
  try {
    pendingCount.value = (await listMembershipApplications(club.value, { status: 'created', pageSize: 1 })).totalCount
  } catch {
    pendingCount.value = 0 // 徽章讀不到就不顯示，不影響其他分頁
  }
}
onMounted(() => {
  loadRefs()
  loadPendingCount()
})
watch(club, () => {
  loadRefs()
  loadPendingCount()
})
</script>

<template>
  <div class="plan-view">
    <PageHeader title="會籍與方案">
      <template #meta><FrontendUnitBanner module-code="K2" /></template>
    </PageHeader>

    <el-tabs v-model="tab" class="plan-view__tabs">
      <el-tab-pane v-if="canViewMemberships" label="會籍" name="memberships" lazy>
        <MembershipsTab :plans="plans" :seasons="seasons" />
      </el-tab-pane>
      <el-tab-pane v-if="canViewMemberships" name="applications" lazy>
        <template #label>
          待確認申請<el-badge v-if="pendingCount > 0" :value="pendingCount" :max="99" class="plan-view__badge" />
        </template>
        <ApplicationsTab :plans="plans" @count="(n: number) => (pendingCount = n)" />
      </el-tab-pane>
      <el-tab-pane v-if="canViewPlans" label="方案" name="plans" lazy>
        <PlansTab :seasons="seasons" @changed="loadRefs" />
      </el-tab-pane>
      <el-tab-pane v-if="canViewMemberships" label="付款紀錄" name="payments" lazy>
        <PaymentsTab />
      </el-tab-pane>
      <el-tab-pane v-if="canViewSettings" label="會員編號規則" name="numbering" lazy>
        <NumberingTab />
      </el-tab-pane>
    </el-tabs>
  </div>
</template>

<style scoped>
.plan-view { min-width: 0; }
.plan-view__tabs { min-width: 0; }
.plan-view__badge { margin-left: 6px; vertical-align: middle; }
.plan-view__tabs :deep(.el-tabs__content) { overflow: visible; }
</style>

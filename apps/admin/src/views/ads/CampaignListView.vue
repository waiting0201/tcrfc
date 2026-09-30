<script setup lang="ts">
/**
 * 投放檔期清單：某個廣告主在某個版位、某段期間的廣告投放。點進檔期可審核素材、送審、暫停與查看進度。
 * 🔴 廣告不分俱樂部：兩隊共用 App，切換站台不影響這裡。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import CampaignFormDialog from './parts/CampaignFormDialog.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { AdminApiError } from '@/api/http'
import { listAdSlots, listAdvertisers, listCampaigns, type AdSlotDto, type AdvertiserDto, type CampaignListItemDto } from '@/api/adminAds'
import { formatDateTime } from '@/utils/dateTime'
import { CAMPAIGN_STATUS_OPTIONS, campaignStatusTag } from './adLabels'

const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canView, canCreate } = useCrudPermissions('ad.campaign')

const filters = reactive({ keyword: '', status: '', slotId: '', advertiserId: '' })
const rows = ref<CampaignListItemDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
const slots = ref<AdSlotDto[]>([])
const advertisers = ref<AdvertiserDto[]>([])

async function load() {
  if (!canView.value) {
    loading.value = false
    return
  }
  loading.value = true
  loadError.value = null
  try {
    rows.value = await listCampaigns({
      keyword: filters.keyword.trim() || undefined,
      status: filters.status || undefined,
      slotId: filters.slotId || undefined,
      advertiserId: filters.advertiserId || undefined,
    })
  } catch (e) {
    rows.value = []
    loadError.value = e instanceof AdminApiError ? e.message : '檔期清單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}
function clearFilter() {
  Object.assign(filters, { keyword: '', status: '', slotId: '', advertiserId: '' })
  load()
}
onMounted(async () => {
  load()
  // 篩選用的選項；沒有版位／廣告主檢視權限時就只是沒有這兩個篩選
  slots.value = await listAdSlots().catch(() => [])
  advertisers.value = await listAdvertisers().catch(() => [])
})

const createVisible = ref(false)
const go = (row: CampaignListItemDto) => router.push(`/business/campaigns/${row.id}`)
const progressText = (r: CampaignListItemDto) => (r.goalImpressions ? `${r.deliveredTotal.toLocaleString()} / ${r.goalImpressions.toLocaleString()}` : `${r.deliveredTotal.toLocaleString()}`)
const creativeText = (r: CampaignListItemDto) => `${r.approvedCreativeCount} / ${r.creativeCount} 已通過`
</script>

<template>
  <div class="camp">
    <PageHeader title="投放檔期">
      <template #meta><FrontendUnitBanner module-code="E5" /></template>
    </PageHeader>
    <el-alert class="camp__block" type="info" show-icon :closable="false" title="兩隊共用：行動 App 是台中磐石與台中藍鯨共用的平台，檔期不分俱樂部。素材未通過審核的檔期不會開始投放。" />

    <el-card v-if="!canView" shadow="never"><el-empty description="你的帳號沒有檢視投放檔期的權限" /></el-card>
    <template v-else>
      <el-card shadow="never" class="camp__block">
        <div class="camp__row">
          <el-input v-model="filters.keyword" placeholder="搜尋檔期名稱" clearable class="camp__keyword" @keyup.enter="load" @clear="load">
            <template #prefix><el-icon><Search /></el-icon></template>
          </el-input>
          <el-select v-model="filters.status" placeholder="狀態" clearable class="camp__select" @change="load">
            <el-option v-for="o in CAMPAIGN_STATUS_OPTIONS" :key="o.value" :label="o.label" :value="o.value" />
          </el-select>
          <el-select v-model="filters.slotId" placeholder="版位" clearable class="camp__select" @change="load">
            <el-option v-for="s in slots" :key="s.id" :label="s.nameZh || s.slotCode" :value="s.id" />
          </el-select>
          <el-select v-model="filters.advertiserId" placeholder="廣告主" clearable filterable class="camp__select" @change="load">
            <el-option v-for="a in advertisers" :key="a.id" :label="a.nameZh || '（未命名）'" :value="a.id" />
          </el-select>
          <el-button type="primary" @click="load">篩選</el-button>
          <el-button @click="clearFilter">清除</el-button>
          <span class="camp__spacer" />
          <el-button v-if="canCreate" type="primary" @click="createVisible = true">+ 新增檔期</el-button>
        </div>
      </el-card>

      <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
      <el-card v-else-if="loadError" shadow="never"><el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty></el-card>
      <el-card v-else shadow="never">
        <el-empty v-if="rows.length === 0" description="目前沒有符合條件的檔期（最多列出最近 500 筆）">
          <el-button v-if="canCreate" type="primary" @click="createVisible = true">+ 新增第一個檔期</el-button>
        </el-empty>
        <template v-else>
          <el-table v-if="!isMobile" :data="rows" row-key="id" @row-click="go">
            <el-table-column label="檔期" min-width="200"><template #default="{ row }"><el-link type="primary" :underline="false">{{ row.name }}</el-link><div class="camp__muted">{{ row.advertiserName || '—' }}</div></template></el-table-column>
            <el-table-column label="版位" min-width="130"><template #default="{ row }">{{ row.slotName || row.slotCode }}</template></el-table-column>
            <el-table-column label="投放期間（台灣時間）" min-width="210"><template #default="{ row }">{{ formatDateTime(row.startsAt) }}<div class="camp__muted">～ {{ formatDateTime(row.endsAt) }}</div></template></el-table-column>
            <el-table-column label="狀態" width="100"><template #default="{ row }"><el-tag :type="campaignStatusTag(row.status)" size="small">{{ row.statusLabel }}</el-tag></template></el-table-column>
            <el-table-column label="目標" width="110" prop="goalTypeLabel" />
            <el-table-column label="累計曝光／目標" width="150"><template #default="{ row }">{{ progressText(row) }}</template></el-table-column>
            <el-table-column label="素材" width="120"><template #default="{ row }">{{ creativeText(row) }}</template></el-table-column>
            <el-table-column label="權重" width="70" prop="weight" />
          </el-table>
          <MobileCardList v-else :rows="rows" row-key="id">
            <template #title="{ row }">{{ row.name }}</template>
            <template #meta="{ row }">
              <el-tag :type="campaignStatusTag(row.status)" size="small">{{ row.statusLabel }}</el-tag>
              <span>{{ row.advertiserName }}</span><span>{{ row.slotName || row.slotCode }}</span><span>{{ formatDateTime(row.startsAt) }} ～ {{ formatDateTime(row.endsAt) }}</span>
            </template>
            <template #actions="{ row }"><el-button size="small" text type="primary" @click="go(row)">查看檔期</el-button></template>
          </MobileCardList>
        </template>
      </el-card>
    </template>

    <CampaignFormDialog v-model="createVisible" :campaign="null" @saved="(c) => router.push(`/business/campaigns/${c.id}`)" />
  </div>
</template>

<style scoped>
.camp { min-width: 0; }
.camp__block { margin-bottom: 12px; }
.camp__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.camp__keyword { width: 220px; max-width: 100%; }
.camp__select { width: 150px; max-width: 100%; }
.camp__spacer { flex: 1; }
.camp__muted { font-size: 12px; color: var(--admin-text-tertiary); }
:deep(.el-table__row) { cursor: pointer; }
</style>

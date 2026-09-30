<script setup lang="ts">
/**
 * 夥伴（對應前台「合作夥伴與贊助」、首頁夥伴標誌牆、頁尾）。
 * 清單不分頁，可用上移／下移調整前台排列順序（篩選中不能調整，避免只排到一部分）。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { deletePartner, listPartners, listPartnerTypes, reorderPartners, type PartnerListItemDto } from '@/api/adminPartners'

const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canCreate, canUpdate, canDelete } = useCrudPermissions('business.partner')
const club = computed(() => activeClubId.value)

const rows = ref<PartnerListItemDto[]>([])
const types = ref<string[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
const filters = ref({ keyword: '', partnerType: '' })
const reordering = ref(false)
const isFiltered = computed(() => !!filters.value.keyword.trim() || !!filters.value.partnerType)

async function load() {
  loading.value = true
  loadError.value = null
  try {
    rows.value = await listPartners(club.value, {
      keyword: filters.value.keyword.trim() || undefined,
      partnerType: filters.value.partnerType || undefined,
    })
  } catch (error) {
    rows.value = []
    loadError.value = error instanceof AdminApiError ? error.message : '清單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

async function loadTypes() {
  try {
    const t = await listPartnerTypes(club.value)
    types.value = Array.from(new Set([...t.standardTypes, ...t.usedTypes]))
  } catch {
    types.value = []
  }
}

onMounted(() => {
  load()
  loadTypes()
})
watch(club, () => {
  filters.value = { keyword: '', partnerType: '' }
  load()
  loadTypes()
})

function clearFilters() {
  filters.value = { keyword: '', partnerType: '' }
  load()
}

async function move(index: number, delta: -1 | 1) {
  const target = index + delta
  if (target < 0 || target >= rows.value.length) return
  const ids = rows.value.map((r) => r.id)
  ;[ids[index], ids[target]] = [ids[target], ids[index]]
  reordering.value = true
  try {
    await reorderPartners(club.value, ids)
    await load()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '調整順序失敗，請稍後再試')
  } finally {
    reordering.value = false
  }
}

async function handleDelete(row: PartnerListItemDto) {
  try {
    await ElMessageBox.confirm(
      `確定要刪除「${row.nameZh || '（未命名）'}」嗎？標誌圖片與它在課程、慈善計畫上的關聯會一併清除，無法復原。`,
      '刪除夥伴',
      { confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning' },
    )
  } catch {
    return
  }
  try {
    await deletePartner(club.value, row.id)
    ElMessage.success('已刪除')
    await load()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
  }
}

function period(row: PartnerListItemDto): string {
  if (!row.startOn && !row.endOn) return '長期合作'
  return `${row.startOn ?? '—'} ～ ${row.endOn ?? '—'}`
}
</script>

<template>
  <div class="partner-list">
    <PageHeader title="夥伴">
      <template #meta>
        <FrontendUnitBanner module-code="E1" />
      </template>
    </PageHeader>

    <el-card shadow="never" class="partner-list__bar">
      <div class="partner-list__bar-row">
        <el-input v-model="filters.keyword" placeholder="搜尋夥伴名稱" clearable class="partner-list__keyword" @keyup.enter="load" @clear="load">
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-select v-model="filters.partnerType" placeholder="夥伴類型" clearable class="partner-list__select" @change="load">
          <el-option v-for="t in types" :key="t" :label="t" :value="t" />
        </el-select>
        <el-button type="primary" @click="load">篩選</el-button>
        <el-button @click="clearFilters">清除</el-button>
        <span class="partner-list__spacer" />
        <el-button v-if="canCreate" type="primary" @click="router.push('/business/partners/new')">+ 新增夥伴</el-button>
      </div>
      <p class="partner-list__hint">
        前台依這裡的順序排列夥伴；只有「合作期間涵蓋今天」的夥伴會出現在前台。{{ isFiltered ? '篩選中無法調整順序。' : '' }}
      </p>
    </el-card>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>
    <el-card v-else shadow="never">
      <template v-if="rows.length > 0">
        <el-table v-if="!isMobile" :data="rows" row-key="id">
          <el-table-column label="標誌" width="84">
            <template #default="{ row }">
              <img v-if="row.logoLightThumbUrl || row.logoDarkThumbUrl" :src="(row.logoLightThumbUrl || row.logoDarkThumbUrl)!" alt="" class="partner-list__logo">
              <span v-else class="partner-list__muted">—</span>
            </template>
          </el-table-column>
          <el-table-column label="名稱" min-width="160">
            <template #default="{ row }">{{ row.nameZh || '（未命名）' }}</template>
          </el-table-column>
          <el-table-column label="類型" width="110">
            <template #default="{ row }">{{ row.partnerType || '—' }}</template>
          </el-table-column>
          <el-table-column label="合作期間" min-width="170">
            <template #default="{ row }">{{ period(row) }}</template>
          </el-table-column>
          <el-table-column label="前台狀態" width="110">
            <template #default="{ row }">
              <el-tag :type="row.isActive ? 'success' : 'info'" size="small">{{ row.isActive ? '顯示中' : '合作已結束' }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="曝光位置" min-width="130">
            <template #default="{ row }">
              <el-tag v-if="row.showOnHome" size="small" class="partner-list__tag">首頁</el-tag>
              <el-tag v-if="row.showInFooter" size="small" class="partner-list__tag">頁尾</el-tag>
              <span v-if="!row.showOnHome && !row.showInFooter" class="partner-list__muted">—</span>
            </template>
          </el-table-column>
          <el-table-column v-if="canUpdate" label="順序" width="96">
            <template #default="{ $index }">
              <el-button size="small" text :disabled="isFiltered || reordering || $index === 0" aria-label="上移" @click="move($index, -1)"><el-icon><ArrowUp /></el-icon></el-button>
              <el-button size="small" text :disabled="isFiltered || reordering || $index === rows.length - 1" aria-label="下移" @click="move($index, 1)"><el-icon><ArrowDown /></el-icon></el-button>
            </template>
          </el-table-column>
          <el-table-column label="操作" width="130" fixed="right">
            <template #default="{ row }">
              <el-button size="small" text type="primary" @click="router.push(`/business/partners/${row.id}/edit`)">{{ canUpdate ? '編輯' : '檢視' }}</el-button>
              <el-button v-if="canDelete" size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
            </template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">{{ row.nameZh || '（未命名）' }}</template>
          <template #meta="{ row }">
            <span>{{ row.partnerType || '未分類' }}</span>
            <el-tag :type="row.isActive ? 'success' : 'info'" size="small">{{ row.isActive ? '顯示中' : '合作已結束' }}</el-tag>
            <span>{{ period(row) }}</span>
          </template>
          <template #actions="{ row }">
            <el-button size="small" text type="primary" @click="router.push(`/business/partners/${row.id}/edit`)">{{ canUpdate ? '編輯' : '檢視' }}</el-button>
            <el-button v-if="canDelete" size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
          </template>
        </MobileCardList>
      </template>
      <el-empty v-else description="目前沒有符合條件的夥伴">
        <el-button v-if="canCreate" type="primary" @click="router.push('/business/partners/new')">+ 新增第一位夥伴</el-button>
      </el-empty>
    </el-card>
  </div>
</template>

<style scoped>
.partner-list__bar { margin-bottom: 12px; }
.partner-list__bar-row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.partner-list__keyword { width: 220px; max-width: 100%; }
.partner-list__select { width: 160px; max-width: 100%; }
.partner-list__spacer { flex: 1; }
.partner-list__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.partner-list__logo { width: 56px; height: 36px; object-fit: contain; background: var(--admin-lightbox-neutral); border-radius: 3px; }
.partner-list__muted { color: var(--admin-text-tertiary); }
.partner-list__tag { margin-right: 4px; }
</style>

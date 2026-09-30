<script setup lang="ts">
/** 「特約店家」分頁：清單、篩選、排序。共同店家（兩隊共用）不參與排序，非系統管理員只能檢視。 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { useIsSuperAdmin } from '@/composables/useRolePermissions'
import { activeClubId } from '@/auth/clubAccess'
import {
  deletePartnerStore,
  getPartnerStoreFilters,
  listPartnerStores,
  reorderPartnerStores,
  type PartnerStoreListItemDto,
} from '@/api/adminPartnerStores'
import { errorMessage } from './membershipHelpers'

const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canCreate, canUpdate, canDelete } = useCrudPermissions('member.store')
const isSuperAdmin = useIsSuperAdmin()
const club = computed(() => activeClubId.value)

function emptyFilters() {
  return { keyword: '', category: '', region: '', status: '', tier: '' }
}
const filters = ref(emptyFilters())
const rows = ref<PartnerStoreListItemDto[]>([])
const categories = ref<string[]>([])
const regions = ref<string[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
const reordering = ref(false)
const isFiltered = computed(() => Object.values(filters.value).some((v) => !!v.trim()))

/** 共同店家只有系統管理員能改。 */
const readOnlyRow = (row: PartnerStoreListItemDto) => row.isShared && !isSuperAdmin.value
const localRows = computed(() => rows.value.filter((r) => !r.isShared))

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const f = filters.value
    rows.value = await listPartnerStores(club.value, {
      keyword: f.keyword.trim() || undefined,
      category: f.category || undefined,
      region: f.region || undefined,
      status: f.status || undefined,
      tier: f.tier || undefined,
    })
  } catch (error) {
    rows.value = []
    loadError.value = errorMessage(error, '店家清單載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
async function loadFilterOptions() {
  try {
    const f = await getPartnerStoreFilters(club.value)
    categories.value = f.categories
    regions.value = f.regions
  } catch {
    categories.value = []
    regions.value = []
  }
}
onMounted(() => {
  load()
  loadFilterOptions()
})
watch(club, () => {
  filters.value = emptyFilters()
  load()
  loadFilterOptions()
})
function clearFilters() {
  filters.value = emptyFilters()
  load()
}

// 排序只送本俱樂部的店家；共同店家不在其中
function localIndex(row: PartnerStoreListItemDto): number {
  return localRows.value.findIndex((r) => r.id === row.id)
}
function canMove(row: PartnerStoreListItemDto, delta: -1 | 1): boolean {
  if (row.isShared) return false
  const i = localIndex(row)
  return i >= 0 && i + delta >= 0 && i + delta < localRows.value.length
}
async function move(row: PartnerStoreListItemDto, delta: -1 | 1) {
  if (!canMove(row, delta)) return
  const ids = localRows.value.map((r) => r.id)
  const i = localIndex(row)
  ;[ids[i], ids[i + delta]] = [ids[i + delta], ids[i]]
  reordering.value = true
  try {
    await reorderPartnerStores(club.value, ids)
    await load()
  } catch (error) {
    ElMessage.error(errorMessage(error, '調整順序失敗，請稍後再試'))
  } finally {
    reordering.value = false
  }
}

async function handleDelete(row: PartnerStoreListItemDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除「${row.nameZh || '（未命名）'}」嗎？店家照片會一併清除，無法復原。`, '刪除特約店家', {
      confirmButtonText: '刪除',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deletePartnerStore(club.value, row.id)
    ElMessage.success('已刪除')
    await Promise.all([load(), loadFilterOptions()])
  } catch (error) {
    ElMessage.error(errorMessage(error, '刪除失敗，請稍後再試'))
  }
}

const period = (row: PartnerStoreListItemDto) => (row.startOn || row.endOn ? `${row.startOn ?? '—'} ～ ${row.endOn ?? '—'}` : '長期合作')
const tierText = (row: PartnerStoreListItemDto) => row.applicableTierLabel || (row.applicableTier === 'fan_club' ? '限付費球迷會員' : '全部會員')
const edit = (row: PartnerStoreListItemDto) => router.push(`/members/partner-stores/${row.id}/edit`)
</script>

<template>
  <div class="stores-tab">
    <el-card shadow="never" class="stores-tab__bar">
      <div class="stores-tab__row">
        <el-input v-model="filters.keyword" placeholder="搜尋店家名稱" clearable class="stores-tab__keyword" @keyup.enter="load" @clear="load">
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-select v-model="filters.category" placeholder="分類" clearable class="stores-tab__select" @change="load">
          <el-option v-for="c in categories" :key="c" :label="c" :value="c" />
        </el-select>
        <el-select v-model="filters.region" placeholder="地區" clearable class="stores-tab__select" @change="load">
          <el-option v-for="r in regions" :key="r" :label="r" :value="r" />
        </el-select>
        <el-select v-model="filters.tier" placeholder="適用層級" clearable class="stores-tab__select" @change="load">
          <el-option label="全部會員" value="all" />
          <el-option label="限付費球迷會員" value="fan_club" />
        </el-select>
        <el-select v-model="filters.status" placeholder="狀態" clearable class="stores-tab__select" @change="load">
          <el-option label="已發布" value="published" />
          <el-option label="草稿" value="draft" />
        </el-select>
        <el-button type="primary" @click="load">篩選</el-button>
        <el-button @click="clearFilters">清除</el-button>
        <span class="stores-tab__spacer" />
        <el-button v-if="canCreate" type="primary" @click="router.push('/members/partner-stores/new')">+ 新增特約店家</el-button>
      </div>
      <p class="stores-tab__hint">
        前台依這裡的順序列出店家；標示「兩隊共用」的店家由系統管理員維護，不參與排序。{{ isFiltered ? '篩選中無法調整順序。' : '' }}
      </p>
    </el-card>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>
    <el-card v-else shadow="never">
      <template v-if="rows.length > 0">
        <el-table v-if="!isMobile" :data="rows" row-key="id">
          <el-table-column label="照片" width="84">
            <template #default="{ row }">
              <img v-if="row.imageThumbUrl" :src="row.imageThumbUrl" alt="" class="stores-tab__thumb">
              <span v-else class="stores-tab__muted">—</span>
            </template>
          </el-table-column>
          <el-table-column label="名稱" min-width="170">
            <template #default="{ row }">
              {{ row.nameZh || '（未命名）' }}
              <el-tag v-if="row.isShared" type="info" size="small" class="stores-tab__tag">兩隊共用</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="分類" width="100"><template #default="{ row }">{{ row.category || '—' }}</template></el-table-column>
          <el-table-column label="地區" width="100"><template #default="{ row }">{{ row.region || '—' }}</template></el-table-column>
          <el-table-column label="適用層級" width="130"><template #default="{ row }">{{ tierText(row) }}</template></el-table-column>
          <el-table-column label="合作期間" min-width="170"><template #default="{ row }">{{ period(row) }}</template></el-table-column>
          <el-table-column label="狀態" width="150">
            <template #default="{ row }">
              <el-tag :type="row.status === 'published' ? 'success' : 'info'" size="small">{{ row.statusLabel || (row.status === 'published' ? '已發布' : '草稿') }}</el-tag>
              <el-tag v-if="row.status === 'published' && !row.isActive" type="warning" size="small" class="stores-tab__tag">合作已結束</el-tag>
            </template>
          </el-table-column>
          <el-table-column v-if="canUpdate" label="順序" width="96">
            <template #default="{ row }">
              <el-button size="small" text :disabled="isFiltered || reordering || !canMove(row, -1)" aria-label="上移" @click="move(row, -1)"><el-icon><ArrowUp /></el-icon></el-button>
              <el-button size="small" text :disabled="isFiltered || reordering || !canMove(row, 1)" aria-label="下移" @click="move(row, 1)"><el-icon><ArrowDown /></el-icon></el-button>
            </template>
          </el-table-column>
          <el-table-column label="操作" width="130" fixed="right">
            <template #default="{ row }">
              <el-button size="small" text type="primary" @click="edit(row)">{{ canUpdate && !readOnlyRow(row) ? '編輯' : '檢視' }}</el-button>
              <el-button v-if="canDelete && !readOnlyRow(row)" size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
            </template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">{{ row.nameZh || '（未命名）' }}</template>
          <template #meta="{ row }">
            <el-tag v-if="row.isShared" type="info" size="small">兩隊共用</el-tag>
            <el-tag :type="row.status === 'published' ? 'success' : 'info'" size="small">{{ row.statusLabel || (row.status === 'published' ? '已發布' : '草稿') }}</el-tag>
            <span>{{ row.category || '未分類' }}</span>
            <span>{{ row.region || '' }}</span>
            <span>{{ tierText(row) }}</span>
          </template>
          <template #actions="{ row }">
            <el-button size="small" text type="primary" @click="edit(row)">{{ canUpdate && !readOnlyRow(row) ? '編輯' : '檢視' }}</el-button>
            <el-button v-if="canDelete && !readOnlyRow(row)" size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
            <template v-if="canUpdate && !isFiltered && !row.isShared">
              <el-button size="small" text :disabled="reordering || !canMove(row, -1)" @click="move(row, -1)">上移</el-button>
              <el-button size="small" text :disabled="reordering || !canMove(row, 1)" @click="move(row, 1)">下移</el-button>
            </template>
          </template>
        </MobileCardList>
      </template>
      <el-empty v-else description="目前沒有符合條件的特約店家">
        <el-button v-if="canCreate" type="primary" @click="router.push('/members/partner-stores/new')">+ 新增第一家店家</el-button>
      </el-empty>
    </el-card>
  </div>
</template>

<style scoped>
.stores-tab { min-width: 0; }
.stores-tab__bar { margin-bottom: 12px; }
.stores-tab__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.stores-tab__keyword { width: 200px; max-width: 100%; }
.stores-tab__select { width: 140px; max-width: 100%; }
.stores-tab__spacer { flex: 1; }
.stores-tab__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.stores-tab__thumb { width: 56px; height: 40px; object-fit: cover; border-radius: 3px; }
.stores-tab__muted { color: var(--admin-text-tertiary); }
.stores-tab__tag { margin-left: 4px; }
</style>

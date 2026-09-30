<script setup lang="ts">
/** 「方案」分頁：方案清單、排序、刪除。 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { deleteMembershipPlan, listMembershipPlans, reorderMembershipPlans, type MembershipPlanListItemDto } from '@/api/adminMemberships'
import { errorMessage, formatMoney } from './membershipHelpers'

defineProps<{ seasons: { id: string; code: string }[] }>()
const emit = defineEmits<{ (e: 'changed'): void }>()

const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canCreate, canUpdate, canDelete } = useCrudPermissions('member.plan')
const club = computed(() => activeClubId.value)

const rows = ref<MembershipPlanListItemDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
const filters = ref({ seasonId: '', status: '' })
const reordering = ref(false)
const isFiltered = computed(() => !!filters.value.seasonId || !!filters.value.status)

async function load() {
  loading.value = true
  loadError.value = null
  try {
    rows.value = await listMembershipPlans(club.value, {
      seasonId: filters.value.seasonId || undefined,
      status: filters.value.status || undefined,
    })
  } catch (error) {
    rows.value = []
    loadError.value = errorMessage(error, '方案清單載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
onMounted(load)
watch(club, () => {
  filters.value = { seasonId: '', status: '' }
  load()
})
function clearFilters() {
  filters.value = { seasonId: '', status: '' }
  load()
}

/** 只在同一球季內調整順序（清單本來就依球季分組）。 */
function canMove(index: number, delta: -1 | 1): boolean {
  const other = rows.value[index + delta]
  return !!other && other.seasonId === rows.value[index].seasonId
}
async function move(index: number, delta: -1 | 1) {
  if (!canMove(index, delta)) return
  const ids = rows.value.map((r) => r.id)
  ;[ids[index], ids[index + delta]] = [ids[index + delta], ids[index]]
  reordering.value = true
  try {
    await reorderMembershipPlans(club.value, ids)
    await load()
  } catch (error) {
    ElMessage.error(errorMessage(error, '調整順序失敗，請稍後再試'))
  } finally {
    reordering.value = false
  }
}

async function handleDelete(row: MembershipPlanListItemDto) {
  try {
    await ElMessageBox.confirm(
      `確定要刪除「${row.nameZh || row.code}」嗎？方案底下的會員權益條目會一併刪除，無法復原。`,
      '刪除方案',
      { confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning' },
    )
  } catch {
    return
  }
  try {
    await deleteMembershipPlan(club.value, row.id)
    ElMessage.success('已刪除')
    await load()
    emit('changed')
  } catch (error) {
    if (error instanceof AdminApiError && error.kind !== 'network') {
      await ElMessageBox.alert(`${error.message}${error.status === 409 ? '\n\n建議改把這個方案設為「下架」，會員端就不會再看到它。' : ''}`, '無法刪除', {
        confirmButtonText: '知道了',
        customStyle: { whiteSpace: 'pre-line' },
      }).catch(() => undefined)
    } else {
      ElMessage.error(errorMessage(error, '刪除失敗，請稍後再試'))
    }
  }
}

function period(row: MembershipPlanListItemDto): string {
  return row.startsOn || row.endsOn ? `${row.startsOn ?? '—'} ～ ${row.endsOn ?? '—'}` : '依球季'
}
const idx = (row: MembershipPlanListItemDto) => rows.value.indexOf(row)
const edit = (row: MembershipPlanListItemDto) => router.push(`/members/plans/${row.id}/edit`)
</script>

<template>
  <div class="plans-tab">
    <el-card shadow="never" class="plans-tab__bar">
      <div class="plans-tab__row">
        <el-select v-model="filters.seasonId" placeholder="球季" clearable class="plans-tab__select" @change="load">
          <el-option v-for="s in seasons" :key="s.id" :label="s.code" :value="s.id" />
        </el-select>
        <el-select v-model="filters.status" placeholder="狀態" clearable class="plans-tab__select" @change="load">
          <el-option label="上架" value="published" />
          <el-option label="下架" value="draft" />
        </el-select>
        <el-button @click="clearFilters">清除</el-button>
        <span class="plans-tab__spacer" />
        <el-button v-if="canCreate" type="primary" @click="router.push('/members/plans/new')">+ 新增方案</el-button>
      </div>
      <p class="plans-tab__hint">
        只有「上架」的方案，會員才能在前台看到，客服也才能用它手動開通。順序只在同一球季內調整。{{ isFiltered ? '篩選中無法調整順序。' : '' }}
      </p>
    </el-card>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="5" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>
    <el-card v-else shadow="never">
      <template v-if="rows.length > 0">
        <el-table v-if="!isMobile" :data="rows" row-key="id">
          <el-table-column label="球季" width="90" prop="seasonCode" />
          <el-table-column label="方案" min-width="160">
            <template #default="{ row }">{{ row.nameZh || '（未命名）' }}<div class="plans-tab__muted">{{ row.code }}</div></template>
          </el-table-column>
          <el-table-column label="費用" width="110"><template #default="{ row }">{{ formatMoney(row.fee) }}</template></el-table-column>
          <el-table-column label="卡數／球衣" width="100"><template #default="{ row }">{{ row.cardQuota }}／{{ row.jerseyQuota }}</template></el-table-column>
          <el-table-column label="方案期間" min-width="170"><template #default="{ row }">{{ period(row) }}</template></el-table-column>
          <el-table-column label="會籍數" width="80" prop="membershipCount" />
          <el-table-column label="狀態" width="80">
            <template #default="{ row }"><el-tag :type="row.status === 'published' ? 'success' : 'info'" size="small">{{ row.statusLabel }}</el-tag></template>
          </el-table-column>
          <el-table-column v-if="canUpdate" label="順序" width="96">
            <template #default="{ $index }">
              <el-button size="small" text :disabled="isFiltered || reordering || !canMove($index, -1)" aria-label="上移" @click="move($index, -1)"><el-icon><ArrowUp /></el-icon></el-button>
              <el-button size="small" text :disabled="isFiltered || reordering || !canMove($index, 1)" aria-label="下移" @click="move($index, 1)"><el-icon><ArrowDown /></el-icon></el-button>
            </template>
          </el-table-column>
          <el-table-column label="操作" width="130" fixed="right">
            <template #default="{ row }">
              <el-button size="small" text type="primary" @click="edit(row)">{{ canUpdate ? '編輯' : '檢視' }}</el-button>
              <el-button v-if="canDelete" size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
            </template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">{{ row.seasonCode }}｜{{ row.nameZh || row.code }}</template>
          <template #meta="{ row }">
            <el-tag :type="row.status === 'published' ? 'success' : 'info'" size="small">{{ row.statusLabel }}</el-tag>
            <span>{{ formatMoney(row.fee) }}</span>
            <span>{{ row.cardQuota }} 張卡／{{ row.jerseyQuota }} 件球衣</span>
            <span>{{ row.membershipCount }} 份會籍</span>
          </template>
          <template #actions="{ row }">
            <el-button size="small" text type="primary" @click="edit(row)">{{ canUpdate ? '編輯' : '檢視' }}</el-button>
            <el-button v-if="canDelete" size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
            <template v-if="canUpdate && !isFiltered">
              <el-button size="small" text :disabled="reordering || !canMove(idx(row), -1)" @click="move(idx(row), -1)">上移</el-button>
              <el-button size="small" text :disabled="reordering || !canMove(idx(row), 1)" @click="move(idx(row), 1)">下移</el-button>
            </template>
          </template>
        </MobileCardList>
      </template>
      <el-empty v-else description="目前沒有符合條件的方案">
        <el-button v-if="canCreate" type="primary" @click="router.push('/members/plans/new')">+ 新增第一個方案</el-button>
      </el-empty>
    </el-card>
  </div>
</template>

<style scoped>
.plans-tab { min-width: 0; }
.plans-tab__bar { margin-bottom: 12px; }
.plans-tab__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.plans-tab__select { width: 150px; max-width: 100%; }
.plans-tab__spacer { flex: 1; }
.plans-tab__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.plans-tab__muted { font-size: 12px; color: var(--admin-text-tertiary); }
</style>

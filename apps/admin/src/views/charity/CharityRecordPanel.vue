<script setup lang="ts">
/** 事蹟紀錄清單（分頁）。每筆事蹟必須有公益團體、捐助內容與活動圖片。 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { deleteRecord, listOrgs, listRecords, type OrgListItemDto, type RecordListItemDto } from '@/api/adminCharity'

const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canCreate, canUpdate, canDelete } = useCrudPermissions('charity.content')
const club = computed(() => activeClubId.value)

const rows = ref<RecordListItemDto[]>([])
const orgs = ref<OrgListItemDto[]>([])
const filters = reactive({ keyword: '', charityId: '', year: null as number | null })
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const loading = ref(true)
const loadError = ref<string | null>(null)

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const r = await listRecords(club.value, {
      keyword: filters.keyword.trim() || undefined,
      charityId: filters.charityId || undefined,
      year: filters.year ?? undefined,
      page: page.value,
      pageSize: pageSize.value,
    })
    rows.value = r.items
    total.value = r.totalCount
  } catch (error) {
    rows.value = []
    total.value = 0
    loadError.value = error instanceof AdminApiError ? error.message : '清單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}
function apply() {
  page.value = 1
  load()
}
async function init() {
  orgs.value = await listOrgs(club.value).catch(() => [])
  await load()
}
onMounted(init)
watch(club, () => {
  Object.assign(filters, { keyword: '', charityId: '', year: null })
  page.value = 1
  init()
})

async function handleDelete(row: RecordListItemDto) {
  try {
    await ElMessageBox.confirm('確定要刪除這筆事蹟紀錄嗎？活動圖片會一併刪除，無法復原。', '刪除事蹟紀錄', {
      confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteRecord(club.value, row.id)
    ElMessage.success('已刪除')
    await load()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
  }
}

const edit = (row: RecordListItemDto) => router.push(`/content/charity/records/${row.id}/edit`)
const editable = (row: RecordListItemDto) => canUpdate.value && !row.isShared
</script>

<template>
  <div>
    <el-card shadow="never" class="panel__block">
      <div class="panel__row">
        <el-input v-model="filters.keyword" placeholder="搜尋捐助內容或地點" clearable class="panel__keyword" @keyup.enter="apply" @clear="apply">
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-select v-model="filters.charityId" placeholder="公益團體" clearable filterable class="panel__select" @change="apply">
          <el-option v-for="o in orgs" :key="o.id" :label="o.nameZh || '（未命名）'" :value="o.id" />
        </el-select>
        <el-input-number v-model="filters.year" :min="2000" :max="2200" :controls="false" placeholder="年份" class="panel__year" @change="apply" />
        <el-button type="primary" @click="apply">篩選</el-button>
        <span class="panel__spacer" />
        <el-button v-if="canCreate" type="primary" @click="router.push('/content/charity/records/new')">+ 新增事蹟紀錄</el-button>
      </div>
      <p class="panel__hint">前台「影響力事蹟」依置頂、排序值與日期排列，每筆會呈現受贈團體、捐助內容與活動圖片。</p>
    </el-card>
    <el-card v-if="loading" shadow="never"><el-skeleton :rows="5" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never"><el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty></el-card>
    <el-card v-else shadow="never">
      <template v-if="rows.length > 0">
        <el-table v-if="!isMobile" :data="rows" row-key="id">
          <el-table-column label="圖片" width="84">
            <template #default="{ row }"><img v-if="row.imageThumbUrl" :src="row.imageThumbUrl" alt="" class="panel__cover"><span v-else class="panel__muted">—</span></template>
          </el-table-column>
          <el-table-column label="受贈團體" min-width="150">
            <template #default="{ row }">
              {{ row.charityNameZh || '—' }}
              <el-tag v-if="row.isPinned" size="small" class="panel__tag">置頂</el-tag>
              <el-tag v-if="row.isShared" type="info" size="small" class="panel__tag">兩隊共用</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="捐助內容" min-width="220"><template #default="{ row }">{{ row.donationContentZh || '—' }}</template></el-table-column>
          <el-table-column label="日期" width="110"><template #default="{ row }">{{ row.happenedOn || '—' }}</template></el-table-column>
          <el-table-column label="地點" min-width="110"><template #default="{ row }">{{ row.locationZh || '—' }}</template></el-table-column>
          <el-table-column label="所屬計畫" min-width="140"><template #default="{ row }">{{ row.programNameZh || '—' }}</template></el-table-column>
          <el-table-column label="操作" width="130" fixed="right">
            <template #default="{ row }">
              <el-button size="small" text type="primary" @click="edit(row)">{{ editable(row) ? '編輯' : '檢視' }}</el-button>
              <el-button v-if="canDelete && !row.isShared" size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
            </template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">{{ row.charityNameZh }}：{{ row.donationContentZh }}</template>
          <template #meta="{ row }"><el-tag v-if="row.isShared" type="info" size="small">兩隊共用</el-tag><span>{{ row.happenedOn || '未填日期' }}</span><span>{{ row.locationZh }}</span></template>
          <template #actions="{ row }">
            <el-button size="small" text type="primary" @click="edit(row)">{{ editable(row) ? '編輯' : '檢視' }}</el-button>
            <el-button v-if="canDelete && !row.isShared" size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
          </template>
        </MobileCardList>
        <div class="panel__pagination">
          <el-pagination v-model:current-page="page" v-model:page-size="pageSize" :total="total" :page-sizes="[20, 50, 100]" :layout="isMobile ? 'prev, pager, next' : 'total, sizes, prev, pager, next'" :small="isMobile" @current-change="load" @size-change="apply" />
        </div>
      </template>
      <el-empty v-else description="還沒有事蹟紀錄" />
    </el-card>
  </div>
</template>

<style scoped>
.panel__block { margin-bottom: 12px; }
.panel__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.panel__keyword { width: 220px; max-width: 100%; }
.panel__select { width: 180px; max-width: 100%; }
.panel__year { width: 100px; }
.panel__spacer { flex: 1; }
.panel__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.panel__cover { width: 56px; height: 40px; object-fit: cover; border-radius: 3px; background: var(--admin-lightbox-neutral); }
.panel__muted { color: var(--admin-text-tertiary); }
.panel__tag { margin-left: 6px; }
.panel__pagination { display: flex; justify-content: flex-end; margin-top: 16px; }
@media (max-width: 767px) { .panel__pagination { justify-content: center; } }
</style>

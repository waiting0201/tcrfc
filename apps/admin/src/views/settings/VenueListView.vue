<script setup lang="ts">
/**
 * I 場地管理——列表（apps/api/README.md「H 批」§5；規劃書 §4.9「場地管理」）。
 * 🔴 場地是**全站共用主檔**（不帶俱樂部），任何俱樂部看到的清單都一樣；路由段的俱樂部只是借用授權管線。
 * 刪除被賽事／梯次／試訓／行事曆事件／球迷會活動引用或登記為主場時，後端回 409 並說明原因，不連帶刪除。
 */
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { deleteAdminVenue, listAdminVenueRows, type AdminVenueRow } from '@/api/adminVenues'

const router = useRouter()
const { canCreate, canUpdate, canDelete } = useCrudPermissions('site.venue')
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')

const rows = ref<AdminVenueRow[]>([])
const loading = ref(true)
const listError = ref<string | null>(null)
const deletingId = ref<string | null>(null)

async function fetchList() {
  loading.value = true
  listError.value = null
  try {
    rows.value = await listAdminVenueRows(activeClubId.value)
  } catch (error) {
    rows.value = []
    listError.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}
onMounted(fetchList)

function coordText(row: AdminVenueRow): string {
  return row.lat != null && row.lng != null ? `${row.lat}, ${row.lng}` : '尚未設定'
}

function handleEdit(row: AdminVenueRow) {
  void router.push(`/settings/venues/${row.id}/edit`)
}

async function handleDelete(row: AdminVenueRow) {
  try {
    await ElMessageBox.confirm(`確定要刪除場地「${row.nameZh}」嗎？此動作無法復原。`, '刪除場地', {
      confirmButtonText: '刪除',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    })
  } catch {
    return
  }
  deletingId.value = row.id
  try {
    await deleteAdminVenue(activeClubId.value, row.id)
    ElMessage.success('已刪除')
    await fetchList()
  } catch (error) {
    // 409：被引用或登記為主場，訊息已說明原因
    ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
  } finally {
    deletingId.value = null
  }
}
</script>

<template>
  <div class="venue-list">
    <PageHeader title="場地管理">
      <template #meta>
        <FrontendUnitBanner module-code="I" />
      </template>
    </PageHeader>

    <el-alert
      title="場地是兩個俱樂部共用的資料，這裡的修改會套用到所有用到這座場地的賽事、課程梯次、試訓與活動，以及前台的交通資訊頁。"
      type="info"
      show-icon
      :closable="false"
      class="venue-list__hint"
    />

    <div class="venue-list__toolbar">
      <el-button v-if="canCreate" type="primary" @click="router.push('/settings/venues/new')">+ 新增場地</el-button>
    </div>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
    <el-card v-else-if="listError" shadow="never">
      <el-empty :image-size="96" :description="listError"><el-button type="primary" @click="fetchList">重新載入</el-button></el-empty>
    </el-card>
    <el-card v-else-if="rows.length === 0" shadow="never">
      <el-empty description="目前還沒有任何場地">
        <el-button v-if="canCreate" type="primary" @click="router.push('/settings/venues/new')">+ 新增第一個場地</el-button>
      </el-empty>
    </el-card>

    <MobileCardList v-else-if="isMobile" :rows="rows" row-key="id">
      <template #title="{ row }">{{ row.nameZh }}</template>
      <template #meta="{ row }">
        <div>{{ row.address || '尚未填寫地址' }}</div>
        <div>座標：{{ coordText(row) }}</div>
      </template>
      <template #actions="{ row }">
        <el-button size="small" @click="handleEdit(row)">{{ canUpdate ? '編輯' : '檢視' }}</el-button>
        <el-button v-if="canDelete" size="small" type="danger" plain :loading="deletingId === row.id" @click="handleDelete(row)">刪除</el-button>
      </template>
    </MobileCardList>

    <el-card v-else shadow="never">
      <el-table :data="rows" row-key="id">
        <el-table-column label="照片" width="90">
          <template #default="{ row }">
            <img v-if="row.photoUrl" :src="row.photoUrl" alt="" class="venue-list__thumb" loading="lazy">
            <span v-else class="venue-list__none">無</span>
          </template>
        </el-table-column>
        <el-table-column label="場地名稱" min-width="180">
          <template #default="{ row }">
            <div>{{ row.nameZh }}</div>
            <div v-if="row.nameEn" class="venue-list__sub">{{ row.nameEn }}</div>
            <el-tag v-else size="small" type="info">英文尚未填寫</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="地址" prop="address" min-width="220" />
        <el-table-column label="座標（緯度, 經度）" min-width="170">
          <template #default="{ row }">{{ coordText(row) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="160" fixed="right">
          <template #default="{ row }">
            <el-button size="small" text type="primary" @click="handleEdit(row)">{{ canUpdate ? '編輯' : '檢視' }}</el-button>
            <el-button v-if="canDelete" size="small" text type="danger" :loading="deletingId === row.id" @click="handleDelete(row)">刪除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>
  </div>
</template>

<style scoped>
.venue-list { min-width: 0; }
.venue-list__hint { margin-bottom: 12px; }
.venue-list__toolbar { margin-bottom: 12px; display: flex; justify-content: flex-end; }
.venue-list__thumb { width: 64px; height: 44px; object-fit: cover; border-radius: 4px; display: block; }
.venue-list__none { color: var(--admin-text-tertiary); font-size: 12px; }
.venue-list__sub { color: var(--admin-text-secondary); font-size: 12px; }
</style>

<script setup lang="ts">
/**
 * C4「對手球隊」——列表頁（主站規劃書 v3.31）。各俱樂部各自維護；賽事與積分榜從這份清單選對手，
 * 清單沒有的對手仍可在賽事／積分榜直接輸入隊名。被賽事或積分榜用到的對手球隊不能刪除，
 * 後端回 409 並說明用到幾處，畫面直接顯示該訊息。權限沿用賽程（`team.match.*`）。
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
import {
  deleteAdminOpponentTeam,
  listAdminOpponentTeams,
  type AdminOpponentTeamListItem,
} from '@/api/adminOpponentTeams'

const router = useRouter()
const { canCreate, canUpdate, canDelete } = useCrudPermissions('team.match')
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')

const rows = ref<AdminOpponentTeamListItem[]>([])
const loading = ref(true)
const listError = ref<string | null>(null)
const deletingId = ref<string | null>(null)

async function fetchList() {
  loading.value = true
  listError.value = null
  try {
    rows.value = await listAdminOpponentTeams(activeClubId.value)
  } catch (error) {
    rows.value = []
    listError.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}
onMounted(fetchList)
watch(activeClubId, fetchList)

function goNew() {
  void router.push('/teams/opponent-teams/new')
}

function handleEdit(row: AdminOpponentTeamListItem) {
  void router.push(`/teams/opponent-teams/${row.id}/edit`)
}

async function handleDelete(row: AdminOpponentTeamListItem) {
  try {
    await ElMessageBox.confirm(`確定要刪除對手球隊「${row.name}」嗎？此動作無法復原。`, '刪除對手球隊', {
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
    await deleteAdminOpponentTeam(activeClubId.value, row.id)
    ElMessage.success('已刪除')
    await fetchList()
  } catch (error) {
    // 409：被賽事或積分榜用到，訊息已含數量
    const message = error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試'
    await ElMessageBox.alert(message, '無法刪除', { confirmButtonText: '我知道了' })
  } finally {
    deletingId.value = null
  }
}
</script>

<template>
  <div class="opponent-list">
    <PageHeader title="對手球隊">
      <template #meta>
        <FrontendUnitBanner module-code="C4" />
      </template>
    </PageHeader>

    <el-alert
      title="先在這裡建立對手球隊（隊名與隊徽），之後在賽程與積分榜直接挑選，不用每次重打隊名。被賽事或積分榜用到的對手球隊不能刪除。"
      type="info"
      show-icon
      :closable="false"
      class="opponent-list__hint"
    />

    <div class="opponent-list__toolbar">
      <el-button v-if="canCreate" type="primary" @click="goNew">+ 新增對手球隊</el-button>
    </div>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
    <el-card v-else-if="listError" shadow="never">
      <el-empty :image-size="96" :description="listError"><el-button type="primary" @click="fetchList">重新載入</el-button></el-empty>
    </el-card>
    <el-card v-else-if="rows.length === 0" shadow="never">
      <el-empty description="目前還沒有任何對手球隊">
        <el-button v-if="canCreate" type="primary" @click="goNew">+ 新增第一支對手球隊</el-button>
      </el-empty>
    </el-card>

    <MobileCardList v-else-if="isMobile" :rows="rows" row-key="id">
      <template #title="{ row }">{{ row.name }}</template>
      <template #meta="{ row }">
        <div>{{ row.nameEn || '英文隊名尚未填寫' }}</div>
        <div>使用場數：{{ row.usageCount }}</div>
      </template>
      <template #actions="{ row }">
        <el-button size="small" @click="handleEdit(row)">{{ canUpdate ? '編輯' : '檢視' }}</el-button>
        <el-button v-if="canDelete" size="small" type="danger" plain :loading="deletingId === row.id" @click="handleDelete(row)">刪除</el-button>
      </template>
    </MobileCardList>

    <el-card v-else shadow="never">
      <el-table :data="rows" row-key="id">
        <el-table-column label="隊徽" width="90">
          <template #default="{ row }">
            <img v-if="row.crestThumbUrl || row.crestUrl" :src="row.crestThumbUrl || row.crestUrl || ''" alt="" class="opponent-list__crest" loading="lazy">
            <span v-else class="opponent-list__none">無</span>
          </template>
        </el-table-column>
        <el-table-column label="隊名" prop="name" min-width="180" />
        <el-table-column label="英文隊名" min-width="180">
          <template #default="{ row }">
            <span v-if="row.nameEn">{{ row.nameEn }}</span>
            <el-tag v-else size="small" type="info">尚未填寫</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="使用場數" prop="usageCount" width="110" />
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
.opponent-list { min-width: 0; }
.opponent-list__hint { margin-bottom: 12px; }
.opponent-list__toolbar { margin-bottom: 12px; display: flex; justify-content: flex-end; }
.opponent-list__crest { width: 40px; height: 40px; object-fit: contain; display: block; }
.opponent-list__none { color: var(--admin-text-tertiary); font-size: 12px; }
</style>

<script setup lang="ts">
/**
 * C3 教練與團隊成員（對應主站規劃書 §4.3 C3，行 1070–1072；apps/api/README.md「S1-7」「S1-7a」）。
 * 這裡管理的是一線隊／學院梯隊的教練與團隊成員資料，前台對應各球隊頁的教練與團隊介紹。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { activeClubId } from '@/auth/clubAccess'
import { listAdminStaff, type AdminStaffListItemDto } from '@/api/adminStaff'
import { AdminApiError } from '@/api/http'
import { PORTRAIT_CONSENT_STATUS_LABEL, type PortraitConsentStatus } from '@/types/team'

const router = useRouter()
const club = computed(() => activeClubId.value)

const staffList = ref<AdminStaffListItemDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)

async function loadStaff() {
  loading.value = true
  loadError.value = null
  try {
    staffList.value = await listAdminStaff(club.value)
  } catch (error) {
    staffList.value = []
    loadError.value = error instanceof AdminApiError ? error.message : '清單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

onMounted(loadStaff)
watch(club, loadStaff)

function handleAdd() {
  router.push('/teams/staff/new')
}

function handleEdit(row: AdminStaffListItemDto) {
  router.push(`/teams/staff/${row.id}/edit`)
}

function portraitTagType(status: string): 'success' | 'warning' {
  return status === 'consented' || status === 'consented_by_guardian' ? 'success' : 'warning'
}
</script>

<template>
  <div class="staff-list">
    <PageHeader title="教練與團隊成員">
      <template #meta>
        <FrontendUnitBanner module-code="C3" />
      </template>
    </PageHeader>

    <el-card shadow="never" class="staff-list__toolbar">
      <div class="staff-list__toolbar-row">
        <el-button type="primary" @click="handleAdd">+ 新增</el-button>
      </div>
    </el-card>

    <el-card v-if="loading" shadow="never">
      <el-skeleton :rows="6" animated />
    </el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError">
        <el-button type="primary" @click="loadStaff">重新載入</el-button>
      </el-empty>
    </el-card>
    <el-card v-else shadow="never">
      <el-table v-if="staffList.length > 0" :data="staffList" row-key="id">
        <el-table-column label="姓名" min-width="140">
          <template #default="{ row }">
            <span>{{ row.nameZh || '（未命名）' }}</span>
            <el-tag v-if="row.isShared" type="info" size="small" class="staff-list__inline-tag">共用內容</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="分組" width="100">
          <template #default="{ row }">{{ row.staffGroup || '—' }}</template>
        </el-table-column>
        <el-table-column label="證照" width="140">
          <template #default="{ row }">{{ row.licence || '—' }}</template>
        </el-table-column>
        <el-table-column label="負責梯隊" min-width="160">
          <template #default="{ row }">{{ row.teamCodes.length > 0 ? row.teamCodes.join('、') : '—' }}</template>
        </el-table-column>
        <el-table-column label="肖像同意" width="120">
          <template #default="{ row }">
            <el-tag :type="portraitTagType(row.portraitConsentStatus)" size="small">
              {{ PORTRAIT_CONSENT_STATUS_LABEL[row.portraitConsentStatus as PortraitConsentStatus] ?? row.portraitConsentStatus }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="90" fixed="right">
          <template #default="{ row }">
            <el-button size="small" text type="primary" @click="handleEdit(row)">{{ row.isShared ? '檢視' : '編輯' }}</el-button>
          </template>
        </el-table-column>
      </el-table>
      <el-empty v-else description="目前還沒有任何資料">
        <el-button type="primary" @click="handleAdd">+ 新增第一筆</el-button>
      </el-empty>
    </el-card>
  </div>
</template>

<style scoped>
.staff-list__toolbar {
  margin-bottom: 12px;
}

.staff-list__toolbar-row {
  display: flex;
  justify-content: flex-end;
}

.staff-list__inline-tag {
  margin-left: 6px;
}
</style>

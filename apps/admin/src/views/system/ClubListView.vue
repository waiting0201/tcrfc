<script setup lang="ts">
/** J4 俱樂部主檔列表頁。僅系統管理員可進入。 */
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { listAdminClubs, type AdminClubListItemDto } from '@/api/adminClubs'
import { AdminApiError } from '@/api/http'

const router = useRouter()

const clubs = ref<AdminClubListItemDto[]>([])
const loading = ref(true)
const listError = ref<string | null>(null)

async function fetchList() {
  loading.value = true
  listError.value = null
  try {
    clubs.value = await listAdminClubs()
  } catch (error) {
    clubs.value = []
    listError.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

onMounted(fetchList)

function handleAdd() {
  router.push('/system/clubs/new')
}

function handleEdit(row: AdminClubListItemDto) {
  router.push(`/system/clubs/${row.id}/edit`)
}

const isEmpty = computed(() => !loading.value && !listError.value && clubs.value.length === 0)
</script>

<template>
  <div class="club-list">
    <PageHeader title="俱樂部與授權管理">
      <template #meta>
        <FrontendUnitBanner module-code="J4" />
      </template>
    </PageHeader>

    <el-alert
      title="這裡維護的是俱樂部主檔與法人資料。指派帳號可操作哪些俱樂部，請到「帳號」的編輯頁設定。"
      type="info"
      show-icon
      :closable="false"
      class="club-list__hint"
    />

    <div class="club-list__toolbar">
      <el-button type="primary" @click="handleAdd">+ 新增俱樂部</el-button>
    </div>

    <el-card v-if="loading" shadow="never">
      <el-skeleton :rows="6" animated />
    </el-card>

    <el-card v-else-if="listError" shadow="never">
      <el-empty :image-size="96" :description="listError">
        <el-button type="primary" @click="fetchList">重新載入</el-button>
      </el-empty>
    </el-card>

    <el-card v-else-if="isEmpty" shadow="never">
      <el-empty description="目前還沒有任何俱樂部">
        <el-button type="primary" @click="handleAdd">+ 新增第一個俱樂部</el-button>
      </el-empty>
    </el-card>

    <el-card v-else shadow="never">
      <el-table :data="clubs" row-key="id">
        <el-table-column label="名稱" min-width="140">
          <template #default="{ row }">{{ row.nameZh ?? row.code }}</template>
        </el-table-column>
        <el-table-column label="前台網域" prop="domain" min-width="160" />
        <el-table-column label="收款主體" width="100">
          <template #default="{ row }">
            <el-tag :type="row.isCollectingSubject ? 'success' : 'info'" size="small">
              {{ row.isCollectingSubject ? '是' : '否' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="排序" prop="sortOrder" width="80" />
        <el-table-column label="狀態" width="100">
          <template #default="{ row }">{{ row.status ?? '—' }}</template>
        </el-table-column>
        <el-table-column label="操作" width="100" fixed="right">
          <template #default="{ row }">
            <el-button size="small" text type="primary" @click="handleEdit(row)">編輯</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>
  </div>
</template>

<style scoped>
.club-list__hint {
  margin-bottom: 12px;
}

.club-list__toolbar {
  margin-bottom: 12px;
  display: flex;
  justify-content: flex-end;
}
</style>

<script setup lang="ts">
/**
 * 賽事系列（Competition）列表頁——C4「賽程與賽果」底下的支援型別（賽季分類，例如企業甲級
 * 聯賽），不是賽事本身。賽事本身（日期、比分、進球者、卡牌、出賽名單）與積分榜在
 * `/teams/matches`／`/teams/standings`（S1-8，見 `views/teams/MatchListView.vue`／
 * `StandingListView.vue`）。這一頁維持獨立，因為它是相對低頻的設定型資料，跟逐場維護的賽事
 * 不是同一種操作節奏。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import StatusTag from '@/components/StatusTag.vue'
import { activeClubId } from '@/auth/clubAccess'
import { listAdminCompetitions, type AdminCompetitionListItemDto } from '@/api/adminCompetitions'
import { AdminApiError } from '@/api/http'
import { formatDateTime } from '@/utils/formatDateTime'

const router = useRouter()

const competitions = ref<AdminCompetitionListItemDto[]>([])
const loading = ref(true)
const listError = ref<string | null>(null)

async function fetchList() {
  loading.value = true
  listError.value = null
  try {
    competitions.value = await listAdminCompetitions(activeClubId.value)
  } catch (error) {
    competitions.value = []
    listError.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

onMounted(fetchList)
watch(activeClubId, fetchList)

function handleAdd() {
  router.push('/teams/competitions/new')
}

function handleEdit(row: AdminCompetitionListItemDto) {
  router.push(`/teams/competitions/${row.id}/edit`)
}

const isEmpty = computed(() => !loading.value && !listError.value && competitions.value.length === 0)
</script>

<template>
  <div class="competition-list">
    <PageHeader title="賽事系列">
      <template #meta>
        <FrontendUnitBanner module-code="C4" />
      </template>
    </PageHeader>

    <el-alert
      title="這裡維護的是賽季內的賽事分類（例如企業甲級聯賽），賽事本身的日期、比分、進球者與出賽名單請到「賽程與賽果」，積分榜表格請到「積分榜」。"
      type="info"
      show-icon
      :closable="false"
      class="competition-list__hint"
    />

    <div class="competition-list__toolbar">
      <el-button type="primary" @click="handleAdd">+ 新增賽事系列</el-button>
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
      <el-empty description="目前還沒有任何賽事系列">
        <el-button type="primary" @click="handleAdd">+ 新增第一筆</el-button>
      </el-empty>
    </el-card>

    <el-card v-else shadow="never">
      <el-table :data="competitions" row-key="id">
        <el-table-column label="名稱" min-width="160">
          <template #default="{ row }">{{ row.nameZh ?? row.code }}</template>
        </el-table-column>
        <el-table-column label="賽季" prop="seasonCode" width="120" />
        <el-table-column label="類型" width="120">
          <template #default="{ row }">{{ row.compType ?? '—' }}</template>
        </el-table-column>
        <el-table-column label="狀態" width="100">
          <template #default="{ row }"><StatusTag :status="row.status" /></template>
        </el-table-column>
        <el-table-column label="更新時間" width="160">
          <template #default="{ row }">{{ formatDateTime(row.updatedAt) }}</template>
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
.competition-list__hint {
  margin-bottom: 12px;
}

.competition-list__toolbar {
  margin-bottom: 12px;
  display: flex;
  justify-content: flex-end;
}
</style>

<script setup lang="ts">
/**
 * C4「賽程與賽果」——列表頁。對照 apps/api/README.md「S1-8」。
 *
 * 🔴 這裡管理的是實際的賽事（日期、比分、出賽名單……），跟 `/teams/competitions`
 * （賽事系列，只是賽季分類的支援型別）是兩回事——後者本階段之前先做，這裡才是規劃書 §4.3 C4
 * 的完整內容。
 *
 * 列級授權（`academy_only`／`own_teams`）目前只套用在**寫入端點**（建立／更新／刪除／CSV
 * 匯入），這個列表端點沒有依帳號的球隊授權範圍過濾——受限帳號會看到整個俱樂部的賽事清單，
 * 但寫入不在授權範圍內的賽事時會被 403 擋下（見 apps/api/README.md「S1-8」〈列表／檢視端點
 * 沒有套用〉），畫面上用篩選列的「球隊」下拉幫使用者自己縮小範圍，不是真正的權限邊界。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { activeClubId } from '@/auth/clubAccess'
import { listAdminClubTeams, type AdminTeamAdminListItemDto } from '@/api/adminTeams'
import { listAdminSeasons, type AdminSeasonListItemDto } from '@/api/adminCompetitions'
import {
  deleteAdminMatch,
  importAdminMatchesCsv,
  listAdminMatches,
  type AdminMatchListItemDto,
  type MatchCsvImportResultDto,
} from '@/api/adminMatches'
import { AdminApiError } from '@/api/http'
import {
  MATCH_STATUS_ORDER,
  matchCompetitionTagLabel,
  matchHomeAwayLabel,
  matchStatusLabel,
} from '@/types/match'

const router = useRouter()
const club = computed(() => activeClubId.value)

const seasons = ref<AdminSeasonListItemDto[]>([])
const teams = ref<AdminTeamAdminListItemDto[]>([])
const teamLabelById = computed(() => {
  const map = new Map<string, string>()
  for (const t of teams.value) map.set(t.id, t.nameZh || t.code)
  return map
})

const filters = reactive({
  seasonId: '',
  teamId: '',
  status: '' as string,
})

const matches = ref<AdminMatchListItemDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)

async function loadFilters() {
  try {
    const [seasonList, teamList] = await Promise.all([listAdminSeasons(club.value), listAdminClubTeams(club.value)])
    seasons.value = seasonList
    teams.value = teamList
  } catch {
    seasons.value = []
    teams.value = []
  }
}

async function loadMatches() {
  loading.value = true
  loadError.value = null
  try {
    matches.value = await listAdminMatches(club.value, {
      seasonId: filters.seasonId || undefined,
      teamId: filters.teamId || undefined,
      status: filters.status || undefined,
    })
  } catch (error) {
    matches.value = []
    loadError.value = error instanceof AdminApiError ? error.message : '賽程清單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

async function bootstrap() {
  filters.seasonId = ''
  filters.teamId = ''
  filters.status = ''
  await Promise.all([loadFilters(), loadMatches()])
}

onMounted(bootstrap)
watch(club, bootstrap)

function applyFilters() {
  loadMatches()
}

function clearFilters() {
  filters.seasonId = ''
  filters.teamId = ''
  filters.status = ''
  applyFilters()
}

function handleAdd() {
  router.push('/teams/matches/new')
}

function handleEdit(row: AdminMatchListItemDto) {
  router.push(`/teams/matches/${row.id}/edit`)
}

function teamCodesLabel(row: AdminMatchListItemDto): string {
  return row.teamIds.map((id, index) => teamLabelById.value.get(id) ?? row.teamCodes[index]).join('、')
}

function scoreLabel(row: AdminMatchListItemDto): string {
  if (row.scoreHome === null || row.scoreHome === undefined || row.scoreAway === null || row.scoreAway === undefined) {
    return '—'
  }
  return `${row.scoreHome} : ${row.scoreAway}`
}

function statusTagType(status: string | null | undefined): 'success' | 'warning' | 'info' | 'danger' {
  if (status === 'played') return 'success'
  if (status === 'live') return 'warning'
  if (status === 'postponed' || status === 'cancelled') return 'danger'
  return 'info'
}

async function handleDelete(row: AdminMatchListItemDto) {
  try {
    await ElMessageBox.confirm(
      `確定要刪除「${row.matchOn} ${row.opponent ?? ''}」這場賽事嗎？這個動作無法復原，比分、進球者、卡牌與出賽名單會一併清除。`,
      '確認刪除',
      { confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning' },
    )
  } catch {
    return
  }
  try {
    await deleteAdminMatch(club.value, row.id)
    ElMessage.success('已刪除')
    await loadMatches()
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'forbidden') {
      await ElMessageBox.alert(error.message, '沒有權限刪除', { confirmButtonText: '我知道了' })
    } else {
      ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
    }
  }
}

// ── CSV 匯入（整季賽程，整批新建）─────────────────────────────────────────────────────

const csvFileInput = ref<HTMLInputElement | null>(null)
const csvImporting = ref(false)
const csvImportResult = ref<MatchCsvImportResultDto | null>(null)
const csvImportDialogVisible = ref(false)

function openCsvFileDialog() {
  csvFileInput.value?.click()
}

async function handleCsvFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return

  csvImporting.value = true
  try {
    const result = await importAdminMatchesCsv(club.value, file)
    csvImportResult.value = result
    csvImportDialogVisible.value = true
    if (result.errors.length === 0) {
      ElMessage.success(`已匯入 ${result.importedCount} 場賽事`)
      await loadMatches()
    }
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '匯入失敗，請稍後再試')
  } finally {
    csvImporting.value = false
  }
}

const isEmpty = computed(() => !loading.value && !loadError.value && matches.value.length === 0)
</script>

<template>
  <div class="match-list">
    <PageHeader title="賽程與賽果">
      <template #meta>
        <FrontendUnitBanner module-code="C4" />
      </template>
    </PageHeader>

    <el-card shadow="never" class="match-list__filters">
      <div class="match-list__filter-row">
        <el-select v-model="filters.seasonId" placeholder="賽季" clearable filterable class="match-list__filter-select" @change="applyFilters">
          <el-option v-for="s in seasons" :key="s.id" :label="s.code" :value="s.id" />
        </el-select>
        <el-select v-model="filters.teamId" placeholder="球隊" clearable filterable class="match-list__filter-select" @change="applyFilters">
          <el-option v-for="t in teams" :key="t.id" :label="t.nameZh || t.code" :value="t.id" />
        </el-select>
        <el-select v-model="filters.status" placeholder="狀態" clearable class="match-list__filter-select" @change="applyFilters">
          <el-option v-for="s in MATCH_STATUS_ORDER" :key="s" :label="matchStatusLabel(s)" :value="s" />
        </el-select>
        <el-button @click="clearFilters">清除</el-button>
        <div class="match-list__filter-placeholder" />
        <input ref="csvFileInput" type="file" accept=".csv,text/csv" class="match-list__hidden-input" @change="handleCsvFileChange">
        <el-button :loading="csvImporting" @click="openCsvFileDialog">匯入整季賽程 CSV</el-button>
        <el-button type="primary" @click="handleAdd">+ 新增賽事</el-button>
      </div>
      <p class="match-list__hint">
        CSV 匯入是整批新建，不是逐列更新，任一列有錯整份檔案都不會寫入。「狀態」欄請填「未開始」「進行中」「已結束」「延賽」或「取消」。
      </p>
    </el-card>

    <el-card v-if="loading" shadow="never">
      <el-skeleton :rows="6" animated />
    </el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError">
        <el-button type="primary" @click="loadMatches">重新載入</el-button>
      </el-empty>
    </el-card>
    <el-card v-else shadow="never">
      <el-table v-if="!isEmpty" :data="matches" row-key="id">
        <el-table-column label="日期／時間" width="150">
          <template #default="{ row }">
            <div>{{ row.matchOn }}</div>
            <div v-if="row.kickoff" class="match-list__muted">{{ row.kickoff }}</div>
          </template>
        </el-table-column>
        <el-table-column label="球隊" min-width="140">
          <template #default="{ row }">{{ teamCodesLabel(row) }}</template>
        </el-table-column>
        <el-table-column label="主客場" width="90">
          <template #default="{ row }">{{ matchHomeAwayLabel(row.homeAway) }}</template>
        </el-table-column>
        <el-table-column label="對手" min-width="120">
          <template #default="{ row }">{{ row.opponent || '—' }}</template>
        </el-table-column>
        <el-table-column label="賽事類型" width="100">
          <template #default="{ row }">{{ matchCompetitionTagLabel(row.competitionTag) }}</template>
        </el-table-column>
        <el-table-column label="場次／輪次" width="100">
          <template #default="{ row }">{{ row.matchNo ?? '—' }} ／ {{ row.roundNo ?? '—' }}</template>
        </el-table-column>
        <el-table-column label="比分" width="90">
          <template #default="{ row }">{{ scoreLabel(row) }}</template>
        </el-table-column>
        <el-table-column label="狀態" width="90">
          <template #default="{ row }">
            <el-tag :type="statusTagType(row.status)" size="small">{{ matchStatusLabel(row.status) }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="130" fixed="right">
          <template #default="{ row }">
            <el-button size="small" text type="primary" @click="handleEdit(row)">編輯</el-button>
            <el-button size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
          </template>
        </el-table-column>
      </el-table>
      <el-empty v-else description="目前還沒有任何賽事，或找不到符合篩選條件的賽事">
        <el-button type="primary" @click="handleAdd">+ 新增第一場賽事</el-button>
      </el-empty>
    </el-card>

    <el-dialog v-model="csvImportDialogVisible" title="CSV 匯入結果" width="640px">
      <template v-if="csvImportResult">
        <el-result
          v-if="csvImportResult.errors.length === 0"
          icon="success"
          :title="`已匯入 ${csvImportResult.importedCount} 場賽事`"
        />
        <template v-else>
          <el-alert
            title="整份檔案有錯誤列，本次沒有任何一列被寫入，請修正後重新上傳。"
            type="error"
            show-icon
            class="match-list__form-error"
          />
          <el-table :data="csvImportResult.errors" max-height="360">
            <el-table-column label="行號" width="80">
              <template #default="{ row }">{{ row.rowNumber }}</template>
            </el-table-column>
            <el-table-column label="錯誤原因">
              <template #default="{ row }">{{ row.reason }}</template>
            </el-table-column>
          </el-table>
        </template>
      </template>
      <template #footer>
        <el-button type="primary" @click="csvImportDialogVisible = false">關閉</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.match-list__filters {
  margin-bottom: 12px;
}

.match-list__filter-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}

.match-list__filter-select {
  width: 160px;
  max-width: 100%;
}

.match-list__hint {
  margin: 12px 0 0;
  font-size: 12px;
  color: var(--admin-text-tertiary);
}

.match-list__filter-placeholder {
  flex: 1;
}

.match-list__hidden-input {
  display: none;
}

.match-list__muted {
  font-size: 12px;
  color: var(--admin-text-tertiary);
}

.match-list__form-error {
  margin-bottom: 12px;
}
</style>

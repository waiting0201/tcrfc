<script setup lang="ts">
/**
 * 試訓報名名單（apps/api/README.md「B1」節「P4 試訓場次」）。
 * 快速處理（確認／取消／加入候補）走整份覆寫端點：先讀詳情再只換狀態，健康聲明等欄位原樣帶回。
 * 「遞補」把候補改為已確認（佔名額）；後台不擋超額，是否遞補由承辦判斷。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import ExportPurposeDialog from '@/components/ExportPurposeDialog.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions, usePermission } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import {
  exportTrialRegistrations,
  getTrial,
  getTrialRegistration,
  listTrialRegistrations,
  promoteTrialRegistration,
  updateTrialRegistration,
  type TrialDetailDto,
  type TrialRegistrationListItemDto,
} from '@/api/adminTrials'
import { REGISTRATION_STATUS_ORDER, registrationStatusTagType } from '@/types/program'
import { formatDateTime } from '@/utils/formatDateTime'

const props = defineProps<{ id: string }>()

const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const club = computed(() => activeClubId.value)
const { canView, canCreate, canUpdate } = useCrudPermissions('program.trial_registration')
const canExport = usePermission('program.trial_registration.export')

const trial = ref<TrialDetailDto | null>(null)
const rows = ref<TrialRegistrationListItemDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
const busyId = ref<string | null>(null)
const filters = reactive({ status: '', keyword: '', isMember: '' as '' | 'yes' | 'no' })

const exportOpen = ref(false)
const exporting = ref(false)

const trialTitle = computed(() => {
  const t = trial.value
  if (!t) return '試訓報名名單'
  return `試訓報名名單：${t.trialOn} ${t.teamName || '俱樂部整體'}`
})

async function loadTrial() {
  try {
    trial.value = await getTrial(club.value, props.id)
  } catch {
    trial.value = null
  }
}

async function load() {
  loading.value = true
  loadError.value = null
  try {
    rows.value = await listTrialRegistrations(club.value, props.id, {
      status: filters.status || undefined,
      keyword: filters.keyword.trim() || undefined,
      isMember: filters.isMember === '' ? undefined : filters.isMember === 'yes',
    })
  } catch (error) {
    rows.value = []
    loadError.value =
      error instanceof AdminApiError && error.kind === 'not-found'
        ? '找不到這場試訓，可能已被刪除，或不屬於目前選擇的俱樂部。'
        : error instanceof AdminApiError
          ? error.message
          : '報名名單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

function resetFilters() {
  filters.status = ''
  filters.keyword = ''
  filters.isMember = ''
}

onMounted(() => {
  loadTrial()
  load()
})
watch(club, () => {
  resetFilters()
  loadTrial()
  load()
})
watch(
  () => props.id,
  () => {
    resetFilters()
    loadTrial()
    load()
  },
)

function clearFilters() {
  resetFilters()
  load()
}

async function changeStatus(row: TrialRegistrationListItemDto, status: string, confirmText?: string) {
  if (confirmText) {
    try {
      await ElMessageBox.confirm(confirmText, '變更報名狀態', { confirmButtonText: '確定', cancelButtonText: '不變更', type: 'warning' })
    } catch {
      return
    }
  }
  busyId.value = row.id
  try {
    const d = await getTrialRegistration(club.value, props.id, row.id)
    await updateTrialRegistration(club.value, props.id, row.id, {
      applicantName: d.applicantName,
      phone: d.phone ?? null,
      email: d.email ?? null,
      birthOn: d.birthOn ?? null,
      guardianName: d.guardianName ?? null,
      guardianPhone: d.guardianPhone ?? null,
      healthDeclaration: d.healthDeclaration ?? null,
      note: d.note ?? null,
      memberId: d.memberId ?? null,
      status,
    })
    ElMessage.success(`已改為「${status}」`)
    await Promise.all([load(), loadTrial()])
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '處理失敗，請稍後再試')
  } finally {
    busyId.value = null
  }
}

async function promote(row: TrialRegistrationListItemDto) {
  busyId.value = row.id
  try {
    await promoteTrialRegistration(club.value, props.id, row.id)
    ElMessage.success(`已遞補「${row.applicantName}」為已確認，請依電話聯繫`)
    await Promise.all([load(), loadTrial()])
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '遞補失敗，請稍後再試')
  } finally {
    busyId.value = null
  }
}

async function doExport(purpose: string) {
  exporting.value = true
  try {
    await exportTrialRegistrations(club.value, props.id, { status: filters.status || undefined }, purpose)
    exportOpen.value = false
    ElMessage.success('已匯出')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '匯出失敗，請稍後再試')
  } finally {
    exporting.value = false
  }
}

const go = (path: string) => router.push(path)
const editPath = (row: TrialRegistrationListItemDto) => `/programs/trials/${props.id}/registrations/${row.id}/edit`
</script>

<template>
  <div class="trial-reg-list">
    <PageHeader :title="trialTitle">
      <template #back>
        <el-button text @click="go('/programs/trials')"><el-icon><ArrowLeft /></el-icon>返回場次列表</el-button>
      </template>
      <template #meta><FrontendUnitBanner module-code="P4" /></template>
    </PageHeader>

    <el-card v-if="trial" shadow="never" class="trial-reg-list__summary">
      已報名 {{ trial.enrolledCount }} / {{ trial.capacity ?? '不限' }} 人，候補 {{ trial.waitlistCount }} 人，場次狀態「{{ trial.status }}」。
    </el-card>

    <el-card shadow="never" class="trial-reg-list__bar">
      <div class="trial-reg-list__bar-row">
        <el-input v-model="filters.keyword" placeholder="搜尋編號、姓名、電話、Email" clearable class="trial-reg-list__keyword" @keyup.enter="load" @clear="load">
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-select v-model="filters.status" placeholder="狀態" clearable class="trial-reg-list__select" @change="load">
          <el-option v-for="s in REGISTRATION_STATUS_ORDER" :key="s" :label="s" :value="s" />
        </el-select>
        <el-select v-model="filters.isMember" placeholder="是否會員" clearable class="trial-reg-list__select" @change="load">
          <el-option label="是會員" value="yes" />
          <el-option label="非會員" value="no" />
        </el-select>
        <el-button type="primary" @click="load">篩選</el-button>
        <el-button @click="clearFilters">清除</el-button>
        <span class="trial-reg-list__spacer" />
        <el-button v-if="canView" @click="go(`/programs/trials/${id}/sign-in`)">簽到表</el-button>
        <el-button v-if="canExport" @click="exportOpen = true">匯出名單</el-button>
        <el-button v-if="canCreate" type="primary" @click="go(`/programs/trials/${id}/registrations/new`)">+ 新增報名（後台代填）</el-button>
      </div>
      <p class="trial-reg-list__hint">遞補只能用在「候補」的報名，遞補後會佔用名額。系統不會自動寄通知信，請依名單上的電話聯繫。</p>
    </el-card>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>
    <el-card v-else shadow="never">
      <template v-if="rows.length > 0">
        <el-table v-if="!isMobile" :data="rows" row-key="id">
          <el-table-column label="報名編號" width="180" prop="registrationNo" />
          <el-table-column label="報名人" min-width="110" prop="applicantName" />
          <el-table-column label="聯絡方式" min-width="170">
            <template #default="{ row }">
              <div>{{ row.phone || '—' }}</div>
              <div class="trial-reg-list__sub">{{ row.email || '—' }}</div>
            </template>
          </el-table-column>
          <el-table-column label="家長" min-width="130">
            <template #default="{ row }">
              <div>{{ row.guardianName || '—' }}</div>
              <div class="trial-reg-list__sub">{{ row.guardianPhone || '' }}</div>
            </template>
          </el-table-column>
          <el-table-column label="會員" width="70">
            <template #default="{ row }">
              <el-tag :type="row.isMember ? 'success' : 'info'" size="small">{{ row.isMember ? '是' : '否' }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="狀態" width="80">
            <template #default="{ row }">
              <el-tag :type="registrationStatusTagType(row.status)" size="small">{{ row.status }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="送出時間" width="150">
            <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
          </el-table-column>
          <el-table-column label="操作" width="280" fixed="right">
            <template #default="{ row }">
              <template v-if="canUpdate">
                <el-button v-if="row.status === '候補'" size="small" text type="primary" :loading="busyId === row.id" @click="promote(row)">遞補</el-button>
                <el-button v-if="row.status === '待確認'" size="small" text type="primary" :loading="busyId === row.id" @click="changeStatus(row, '已確認')">確認</el-button>
                <el-button v-if="row.status !== '候補' && row.status !== '取消'" size="small" text :loading="busyId === row.id" @click="changeStatus(row, '候補', `確定要把「${row.applicantName}」改為候補嗎？`)">加入候補</el-button>
                <el-button v-if="row.status !== '取消'" size="small" text type="danger" :loading="busyId === row.id" @click="changeStatus(row, '取消', `確定要取消「${row.applicantName}」的報名嗎？`)">取消報名</el-button>
              </template>
              <el-button size="small" text type="primary" @click="go(editPath(row))">{{ canUpdate ? '處理' : '檢視' }}</el-button>
            </template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">{{ row.applicantName }}</template>
          <template #meta="{ row }">
            <el-tag :type="registrationStatusTagType(row.status)" size="small">{{ row.status }}</el-tag>
            <el-tag :type="row.isMember ? 'success' : 'info'" size="small">{{ row.isMember ? '會員' : '非會員' }}</el-tag>
            <span>{{ row.registrationNo }}</span>
            <span>電話 {{ row.phone || '—' }}</span>
            <span v-if="row.guardianName">家長 {{ row.guardianName }} {{ row.guardianPhone || '' }}</span>
            <span>{{ formatDateTime(row.createdAt) }}</span>
          </template>
          <template #actions="{ row }">
            <template v-if="canUpdate">
              <el-button v-if="row.status === '候補'" size="small" text type="primary" :loading="busyId === row.id" @click="promote(row)">遞補</el-button>
              <el-button v-if="row.status === '待確認'" size="small" text type="primary" :loading="busyId === row.id" @click="changeStatus(row, '已確認')">確認</el-button>
              <el-button v-if="row.status !== '候補' && row.status !== '取消'" size="small" text :loading="busyId === row.id" @click="changeStatus(row, '候補', `確定要把「${row.applicantName}」改為候補嗎？`)">加入候補</el-button>
              <el-button v-if="row.status !== '取消'" size="small" text type="danger" :loading="busyId === row.id" @click="changeStatus(row, '取消', `確定要取消「${row.applicantName}」的報名嗎？`)">取消報名</el-button>
            </template>
            <el-button size="small" text type="primary" @click="go(editPath(row))">{{ canUpdate ? '處理' : '檢視' }}</el-button>
          </template>
        </MobileCardList>
      </template>
      <el-empty v-else description="找不到符合條件的報名" />
    </el-card>

    <ExportPurposeDialog
      v-model="exportOpen"
      title="匯出試訓報名名單"
      description="名單含姓名、電話、Email、家長資料，不含健康聲明。若上方有選狀態，只匯出該狀態的報名。"
      :loading="exporting"
      @confirm="doExport"
    />
  </div>
</template>

<style scoped>
.trial-reg-list__summary { margin-bottom: 12px; font-size: 14px; }
.trial-reg-list__bar { margin-bottom: 12px; }
.trial-reg-list__bar-row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.trial-reg-list__keyword { width: 240px; max-width: 100%; }
.trial-reg-list__select { width: 140px; max-width: 100%; }
.trial-reg-list__spacer { flex: 1; }
.trial-reg-list__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.trial-reg-list__sub { font-size: 12px; color: var(--admin-text-tertiary); }
</style>

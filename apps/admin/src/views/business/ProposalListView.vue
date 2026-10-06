<script setup lang="ts">
/**
 * 提案下載（對應前台「合作夥伴與贊助」頁的「下載提案簡介」按鈕）：
 * 「提案檔案」管理可供下載的簡介檔；「下載名單」是填表下載的訪客（含個人資料），可標記跟進狀態、匯出。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCanExportLeads, useCrudPermissions, useViewUpdatePermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { formatDateTime } from '@/utils/dateTime'
import { saveBlob } from '@/utils/downloadFile'
import {
  deleteProposal,
  exportLeads,
  getLead,
  LEAD_STATUS_OPTIONS,
  listLeadAssignees,
  listLeads,
  listProposals,
  updateLead,
  type LeadAssigneeDto,
  type LeadDetailDto,
  type LeadListItemDto,
  type ProposalListItemDto,
} from '@/api/adminProposals'
import FormField from '@/components/FormField.vue'
import { provideFormErrors } from '@/composables/useFormErrors'

const formErrors = provideFormErrors()

const route = useRoute()
const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const proposalPerm = useCrudPermissions('business.proposal')
const leadPerm = useViewUpdatePermissions('business.lead')
const canExport = useCanExportLeads()
const club = computed(() => activeClubId.value)

const tab = ref<'proposals' | 'leads'>(route.query.tab === 'leads' ? 'leads' : 'proposals')
watch(tab, (t) => router.replace({ query: { tab: t } }))

// ── 提案檔案 ──
const proposals = ref<ProposalListItemDto[]>([])
const proposalLoading = ref(true)
const proposalError = ref<string | null>(null)

async function loadProposals() {
  proposalLoading.value = true
  proposalError.value = null
  try {
    proposals.value = await listProposals(club.value)
  } catch (error) {
    proposals.value = []
    proposalError.value = error instanceof AdminApiError ? error.message : '提案清單載入失敗，請稍後再試'
  } finally {
    proposalLoading.value = false
  }
}

async function handleDeleteProposal(row: ProposalListItemDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除提案「${row.title}」嗎？檔案會一併刪除，但已經產生的下載名單會保留。`, '刪除提案', {
      confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteProposal(club.value, row.id)
    ElMessage.success('已刪除')
    await loadProposals()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
  }
}

// ── 下載名單 ──
const leads = ref<LeadListItemDto[]>([])
const leadLoading = ref(false)
const leadError = ref<string | null>(null)
const leadFilter = reactive({ proposalId: '', status: '', keyword: '', range: null as [string, string] | null })
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const exporting = ref(false)

function filterParams() {
  return {
    proposalId: leadFilter.proposalId || undefined,
    status: leadFilter.status || undefined,
    keyword: leadFilter.keyword.trim() || undefined,
    dateFrom: leadFilter.range?.[0],
    dateTo: leadFilter.range?.[1],
  }
}

async function loadLeads() {
  if (!leadPerm.canView.value) return
  leadLoading.value = true
  leadError.value = null
  try {
    const result = await listLeads(club.value, { ...filterParams(), page: page.value, pageSize: pageSize.value })
    leads.value = result.items
    total.value = result.totalCount
  } catch (error) {
    leads.value = []
    total.value = 0
    leadError.value = error instanceof AdminApiError ? error.message : '下載名單載入失敗，請稍後再試'
  } finally {
    leadLoading.value = false
  }
}

function applyLeadFilter() {
  page.value = 1
  loadLeads()
}

function clearLeadFilter() {
  Object.assign(leadFilter, { proposalId: '', status: '', keyword: '', range: null })
  applyLeadFilter()
}

async function handleExport() {
  exporting.value = true
  try {
    const { blob, filename } = await exportLeads(club.value, filterParams())
    saveBlob(blob, filename ?? '提案下載名單.csv')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '匯出失敗，請稍後再試')
  } finally {
    exporting.value = false
  }
}

// ── 跟進（標記狀態） ──
const assignees = ref<LeadAssigneeDto[]>([])
const followDialog = ref(false)
const followSaving = ref(false)
const followError = ref<string | null>(null)
const follow = reactive({ id: '', company: '', name: '', email: '', proposalTitle: '', proposalVersionNo: null as number | null, sourcePath: '', createdAt: '', status: '新進', assignee: '' as string, note: '', tags: '' })

async function openFollow(row: LeadListItemDto) {
  followError.value = null
  formErrors.clearAll()
  try {
    const d: LeadDetailDto = await getLead(club.value, row.id)
    Object.assign(follow, {
      id: d.id, company: d.company ?? '', name: d.name ?? '', email: d.email ?? '',
      proposalTitle: d.proposalTitle ?? '', proposalVersionNo: d.proposalVersionNo ?? null,
      sourcePath: d.sourcePath ?? '', createdAt: d.createdAt,
      status: d.status || '新進', assignee: d.assigneeAdminUserId ?? '', note: d.internalNote ?? '', tags: d.tags ?? '',
    })
    if (leadPerm.canUpdate.value && assignees.value.length === 0) {
      assignees.value = await listLeadAssignees(club.value).catch(() => [])
    }
    followDialog.value = true
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '載入失敗，請稍後再試')
  }
}

async function saveFollow() {
  followSaving.value = true
  followError.value = null
  formErrors.clearAll()
  try {
    await updateLead(club.value, follow.id, {
      status: follow.status,
      assigneeAdminUserId: follow.assignee || null,
      internalNote: follow.note.trim() || null,
      tags: follow.tags.trim() || null,
    })
    ElMessage.success('已儲存')
    followDialog.value = false
    await loadLeads()
  } catch (error) {
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    followError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    followSaving.value = false
  }
}

function statusTag(status?: string | null): 'info' | 'warning' | 'success' | 'danger' {
  switch (status) {
    case '新進': return 'danger'
    case '處理中': return 'warning'
    case '已回覆': case '已結案': return 'success'
    default: return 'info'
  }
}

onMounted(() => {
  loadProposals()
  loadLeads()
})
watch(club, () => {
  Object.assign(leadFilter, { proposalId: '', status: '', keyword: '', range: null })
  assignees.value = []
  page.value = 1
  loadProposals()
  loadLeads()
})
</script>

<template>
  <div class="proposal-list">
    <PageHeader title="提案下載">
      <template #meta><FrontendUnitBanner module-code="E3" /></template>
    </PageHeader>

    <el-tabs v-model="tab">
      <el-tab-pane label="提案檔案" name="proposals">
        <el-card shadow="never" class="proposal-list__bar">
          <div class="proposal-list__bar-row">
            <p class="proposal-list__hint">
              提案檔案不會有公開網址：訪客要在前台填寫下載表單，才會拿到 30 分鐘內有效的下載連結。可以放不同版本、中英文版本；「累計下載」可用來比較不同版本的成效。
            </p>
            <el-button v-if="proposalPerm.canCreate.value" type="primary" @click="router.push('/business/proposals/new')">+ 新增提案</el-button>
          </div>
        </el-card>
        <el-card v-if="proposalLoading" shadow="never"><el-skeleton :rows="4" animated /></el-card>
        <el-card v-else-if="proposalError" shadow="never"><el-empty :description="proposalError"><el-button type="primary" @click="loadProposals">重新載入</el-button></el-empty></el-card>
        <el-card v-else shadow="never">
          <template v-if="proposals.length > 0">
            <el-table v-if="!isMobile" :data="proposals" row-key="id">
              <el-table-column label="提案名稱" min-width="200"><template #default="{ row }">{{ row.title }}</template></el-table-column>
              <el-table-column label="版本" width="80"><template #default="{ row }">第 {{ row.versionNo }} 版</template></el-table-column>
              <el-table-column label="狀態" width="90">
                <template #default="{ row }"><el-tag :type="row.status === 'published' ? 'success' : 'info'" size="small">{{ row.status === 'published' ? '已發布' : '草稿' }}</el-tag></template>
              </el-table-column>
              <el-table-column label="語言版本" width="110"><template #default="{ row }">{{ row.locales.map((l: string) => (l === 'zh' ? '中文' : '英文')).join('、') || '—' }}</template></el-table-column>
              <el-table-column label="累計下載" width="100"><template #default="{ row }">{{ row.leadCount }} 次</template></el-table-column>
              <el-table-column label="更新時間" width="160"><template #default="{ row }">{{ formatDateTime(row.updatedAt) }}</template></el-table-column>
              <el-table-column label="操作" width="130" fixed="right">
                <template #default="{ row }">
                  <el-button size="small" text type="primary" @click="router.push(`/business/proposals/${row.id}/edit`)">{{ proposalPerm.canUpdate.value ? '編輯' : '檢視' }}</el-button>
                  <el-button v-if="proposalPerm.canDelete.value" size="small" text type="danger" @click="handleDeleteProposal(row)">刪除</el-button>
                </template>
              </el-table-column>
            </el-table>
            <MobileCardList v-else :rows="proposals" row-key="id">
              <template #title="{ row }">{{ row.title }}</template>
              <template #meta="{ row }">
                <el-tag :type="row.status === 'published' ? 'success' : 'info'" size="small">{{ row.status === 'published' ? '已發布' : '草稿' }}</el-tag>
                <span>第 {{ row.versionNo }} 版・累計下載 {{ row.leadCount }} 次</span>
              </template>
              <template #actions="{ row }">
                <el-button size="small" text type="primary" @click="router.push(`/business/proposals/${row.id}/edit`)">{{ proposalPerm.canUpdate.value ? '編輯' : '檢視' }}</el-button>
                <el-button v-if="proposalPerm.canDelete.value" size="small" text type="danger" @click="handleDeleteProposal(row)">刪除</el-button>
              </template>
            </MobileCardList>
          </template>
          <el-empty v-else description="還沒有提案檔案" />
        </el-card>
      </el-tab-pane>

      <el-tab-pane v-if="leadPerm.canView.value" label="下載名單" name="leads">
        <el-alert type="warning" show-icon :closable="false" class="proposal-list__bar" title="這份名單含訪客的姓名、公司與 Email，請僅用於商務跟進，不要外傳。" />
        <el-card shadow="never" class="proposal-list__bar">
          <div class="proposal-list__bar-row">
            <el-input v-model="leadFilter.keyword" placeholder="搜尋公司、姓名或 Email" clearable class="proposal-list__keyword" @keyup.enter="applyLeadFilter" @clear="applyLeadFilter" />
            <el-select v-model="leadFilter.proposalId" placeholder="下載的提案" clearable class="proposal-list__select" @change="applyLeadFilter">
              <el-option v-for="p in proposals" :key="p.id" :label="p.title" :value="p.id" />
            </el-select>
            <el-select v-model="leadFilter.status" placeholder="跟進狀態" clearable class="proposal-list__select" @change="applyLeadFilter">
              <el-option v-for="s in LEAD_STATUS_OPTIONS" :key="s" :label="s" :value="s" />
            </el-select>
            <el-date-picker v-model="leadFilter.range" type="daterange" value-format="YYYY-MM-DD" range-separator="至" start-placeholder="開始日" end-placeholder="結束日" class="proposal-list__range" @change="applyLeadFilter" />
            <el-button type="primary" @click="applyLeadFilter">篩選</el-button>
            <el-button @click="clearLeadFilter">清除</el-button>
            <span class="proposal-list__spacer" />
            <el-button v-if="canExport" :loading="exporting" @click="handleExport">匯出名單 CSV</el-button>
          </div>
        </el-card>
        <el-card v-if="leadLoading" shadow="never"><el-skeleton :rows="5" animated /></el-card>
        <el-card v-else-if="leadError" shadow="never"><el-empty :description="leadError"><el-button type="primary" @click="loadLeads">重新載入</el-button></el-empty></el-card>
        <el-card v-else shadow="never">
          <template v-if="leads.length > 0">
            <el-table v-if="!isMobile" :data="leads" row-key="id">
              <el-table-column label="下載時間" width="160"><template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template></el-table-column>
              <el-table-column label="公司" min-width="140"><template #default="{ row }">{{ row.company || '—' }}</template></el-table-column>
              <el-table-column label="姓名" width="110"><template #default="{ row }">{{ row.name || '—' }}</template></el-table-column>
              <el-table-column label="Email" min-width="180"><template #default="{ row }">{{ row.email || '—' }}</template></el-table-column>
              <el-table-column label="下載的提案" min-width="150"><template #default="{ row }">{{ row.proposalTitle || '（提案已刪除）' }}</template></el-table-column>
              <el-table-column label="來源頁面" min-width="140"><template #default="{ row }">{{ row.sourcePath || '—' }}</template></el-table-column>
              <el-table-column label="跟進狀態" width="100"><template #default="{ row }"><el-tag :type="statusTag(row.status)" size="small">{{ row.status || '新進' }}</el-tag></template></el-table-column>
              <el-table-column label="操作" width="90" fixed="right">
                <template #default="{ row }"><el-button size="small" text type="primary" @click="openFollow(row)">{{ leadPerm.canUpdate.value ? '跟進' : '檢視' }}</el-button></template>
              </el-table-column>
            </el-table>
            <MobileCardList v-else :rows="leads" row-key="id">
              <template #title="{ row }">{{ row.company || row.name || row.email }}</template>
              <template #meta="{ row }">
                <el-tag :type="statusTag(row.status)" size="small">{{ row.status || '新進' }}</el-tag>
                <span>{{ row.proposalTitle || '（提案已刪除）' }}</span>
                <span>{{ formatDateTime(row.createdAt) }}</span>
              </template>
              <template #actions="{ row }"><el-button size="small" text type="primary" @click="openFollow(row)">{{ leadPerm.canUpdate.value ? '跟進' : '檢視' }}</el-button></template>
            </MobileCardList>
            <div class="proposal-list__pagination">
              <el-pagination v-model:current-page="page" v-model:page-size="pageSize" :total="total" :page-sizes="[20, 50, 100]" :layout="isMobile ? 'prev, pager, next' : 'total, sizes, prev, pager, next'" :small="isMobile" @current-change="loadLeads" @size-change="applyLeadFilter" />
            </div>
          </template>
          <el-empty v-else description="目前沒有符合條件的下載紀錄" />
        </el-card>
      </el-tab-pane>
    </el-tabs>

    <el-dialog v-model="followDialog" title="下載紀錄與跟進" width="560px">
      <el-alert v-if="followError" :title="followError" type="warning" show-icon class="proposal-list__bar" @close="followError = null" />
      <el-descriptions :column="1" border size="small" class="proposal-list__bar">
        <el-descriptions-item label="公司">{{ follow.company || '—' }}</el-descriptions-item>
        <el-descriptions-item label="姓名">{{ follow.name || '—' }}</el-descriptions-item>
        <el-descriptions-item label="Email">{{ follow.email || '—' }}</el-descriptions-item>
        <el-descriptions-item label="下載的提案">{{ follow.proposalTitle || '（提案已刪除）' }}{{ follow.proposalVersionNo ? `（第 ${follow.proposalVersionNo} 版）` : '' }}</el-descriptions-item>
        <el-descriptions-item label="來源頁面">{{ follow.sourcePath || '—' }}</el-descriptions-item>
        <el-descriptions-item label="下載時間">{{ formatDateTime(follow.createdAt) }}</el-descriptions-item>
      </el-descriptions>
      <el-form label-position="top" :disabled="!leadPerm.canUpdate.value">
        <FormField field="status" label="跟進狀態">
          <el-select v-model="follow.status" style="width: 100%"><el-option v-for="s in LEAD_STATUS_OPTIONS" :key="s" :label="s" :value="s" /></el-select>
        </FormField>
        <FormField field="assigneeAdminUserId" label="負責人">
          <el-select v-model="follow.assignee" clearable placeholder="尚未指派" style="width: 100%"><el-option v-for="u in assignees" :key="u.id" :label="u.displayName" :value="u.id" /></el-select>
        </FormField>
        <FormField field="tags" label="標籤"><el-input v-model="follow.tags" maxlength="255" placeholder="例如 重點客戶、待報價" /></FormField>
        <el-form-item label="內部備註"><el-input v-model="follow.note" type="textarea" :rows="3" placeholder="只有內部看得到" /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="followDialog = false">關閉</el-button>
        <el-button v-if="leadPerm.canUpdate.value" type="primary" :loading="followSaving" @click="saveFollow">儲存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.proposal-list__bar { margin-bottom: 12px; }
.proposal-list__bar-row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.proposal-list__keyword { width: 240px; max-width: 100%; }
.proposal-list__select { width: 170px; max-width: 100%; }
.proposal-list__range { max-width: 100%; }
.proposal-list__spacer { flex: 1; }
.proposal-list__hint { flex: 1; min-width: 200px; margin: 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.proposal-list__pagination { display: flex; justify-content: flex-end; margin-top: 16px; }
@media (max-width: 767px) {
  .proposal-list__pagination { justify-content: center; }
  :deep(.el-dialog) { width: 94% !important; }
}
</style>

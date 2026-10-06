<script setup lang="ts">
/**
 * 電子報訂閱名單：訂閱者清單、來源摘要、後台新增、退訂與改回訂閱、個資刪除、匯出（須填用途）。
 * 🔴 名單依俱樂部分開：同一個人可以只退訂其中一站，切換站台看到的是各自的名單。
 * 🔴 系統不寄電子報：官網不寄信，寄送由外部電子報平台負責；平台目前尚未串接，「同步名單」只會回報「尚未串接」。
 * 🔴 退訂是法遵事實：曾經退訂的人不能由後台直接加回；改回訂閱必須填寫「訂閱者本人要求」的說明，並會留下紀錄。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import ExportPurposeDialog from '@/components/ExportPurposeDialog.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { usePermission } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import {
  addSubscriber,
  changeSubscriberStatus,
  deleteSubscriber,
  exportSubscribers,
  getEdmStatus,
  getNewsletterSummary,
  listSubscribers,
  syncEdm,
  type EdmStatusDto,
  type NewsletterSummaryDto,
  type SubscriberDto,
} from '@/api/adminNewsletter'
import { formatDateTime } from '@/utils/dateTime'
import FormField from '@/components/FormField.vue'
import { provideFormErrors } from '@/composables/useFormErrors'

const formErrors = provideFormErrors()

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const canView = usePermission('form.newsletter.view')
const canUpdate = usePermission('form.newsletter.update')
const canExport = usePermission('form.newsletter.export')
const club = computed(() => activeClubId.value)
const errText = (e: unknown, f: string) => (e instanceof AdminApiError ? e.message : f)

// ── 摘要與寄送平台狀態 ──
const summary = ref<NewsletterSummaryDto | null>(null)
const edm = ref<EdmStatusDto | null>(null)
const edmResult = ref<string | null>(null)
const syncing = ref(false)
async function loadSummary() {
  try {
    const [s, e] = await Promise.all([getNewsletterSummary(club.value), getEdmStatus(club.value)])
    summary.value = s
    edm.value = e
  } catch {
    summary.value = null
    edm.value = null
  }
}
async function handleSync() {
  syncing.value = true
  edmResult.value = null
  try {
    const r = await syncEdm(club.value)
    edmResult.value = r.configured
      ? `${r.message}（訂閱名單 ${r.subscribedCount} 人、退訂名單 ${r.unsubscribedCount} 人，已同步 ${r.syncedCount} 筆）`
      : r.message
  } catch (e) {
    ElMessage.error(errText(e, '同步失敗，請稍後再試'))
  } finally {
    syncing.value = false
  }
}

// ── 清單 ──
const filters = reactive({ keyword: '', status: '', source: '' })
const rows = ref<SubscriberDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
async function load() {
  if (!canView.value) {
    loading.value = false
    return
  }
  loading.value = true
  loadError.value = null
  try {
    const r = await listSubscribers(club.value, {
      keyword: filters.keyword.trim() || undefined,
      status: filters.status || undefined,
      source: filters.source || undefined,
      page: page.value,
      pageSize: pageSize.value,
    })
    rows.value = r.items
    total.value = r.totalCount
  } catch (e) {
    rows.value = []
    total.value = 0
    loadError.value = errText(e, '訂閱名單載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
function applyFilter() {
  page.value = 1
  load()
}
function clearFilter() {
  Object.assign(filters, { keyword: '', status: '', source: '' })
  applyFilter()
}
function refreshAll() {
  load()
  loadSummary()
}
onMounted(refreshAll)
watch(club, () => {
  Object.assign(filters, { keyword: '', status: '', source: '' })
  edmResult.value = null
  page.value = 1
  refreshAll()
})

// ── 新增 ──
const addVisible = ref(false)
const addForm = reactive({ email: '', source: '' })
const addSaving = ref(false)
const addError = ref<string | null>(null)
function openAdd() {
  Object.assign(addForm, { email: '', source: '' })
  addError.value = null
  formErrors.clearAll()
  addVisible.value = true
}
async function submitAdd() {
  const email = addForm.email.trim()
  if (!/^\S+@\S+\.\S+$/.test(email)) {
    addError.value = '請輸入正確的 Email'
    return
  }
  addSaving.value = true
  addError.value = null
  formErrors.clearAll()
  try {
    await addSubscriber(club.value, { email, source: addForm.source.trim() || undefined })
    ElMessage.success('已加入訂閱名單')
    addVisible.value = false
    refreshAll()
  } catch (e) {
    if (e instanceof AdminApiError && formErrors.applyApiError(e)) return
    addError.value = errText(e, '新增失敗，請稍後再試')
  } finally {
    addSaving.value = false
  }
}

// ── 退訂／改回訂閱／刪除 ──
async function unsubscribe(row: SubscriberDto) {
  try {
    await ElMessageBox.confirm(`確定要把「${row.email}」改為已退訂嗎？`, '退訂', { confirmButtonText: '確定退訂', cancelButtonText: '取消', type: 'warning' })
  } catch {
    return
  }
  try {
    await changeSubscriberStatus(club.value, row.id, { status: 'unsubscribed' })
    ElMessage.success('已退訂')
    refreshAll()
  } catch (e) {
    ElMessage.error(errText(e, '操作失敗，請稍後再試'))
  }
}
const resubVisible = ref(false)
const resubTarget = ref<SubscriberDto | null>(null)
const resubReason = ref('')
const resubSaving = ref(false)
const resubError = ref<string | null>(null)
function openResub(row: SubscriberDto) {
  resubTarget.value = row
  resubReason.value = ''
  resubError.value = null
  formErrors.clearAll()
  resubVisible.value = true
}
async function submitResub() {
  const reason = resubReason.value.trim()
  if (!reason || !resubTarget.value) {
    resubError.value = '請說明是訂閱者本人要求改回訂閱（例如：本人來電要求）'
    return
  }
  resubSaving.value = true
  try {
    await changeSubscriberStatus(club.value, resubTarget.value.id, { status: 'subscribed', reason })
    ElMessage.success('已改回訂閱，系統已留下紀錄')
    resubVisible.value = false
    refreshAll()
  } catch (e) {
    if (e instanceof AdminApiError && formErrors.applyApiError(e)) return
    resubError.value = errText(e, '操作失敗，請稍後再試')
  } finally {
    resubSaving.value = false
  }
}
async function remove(row: SubscriberDto) {
  try {
    await ElMessageBox.confirm(
      `這是個資刪除：「${row.email}」會從名單中完全移除，且不留下退訂紀錄。若只是不想再收信，請改用「退訂」。確定刪除嗎？`,
      '刪除訂閱者',
      { confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning' },
    )
  } catch {
    return
  }
  try {
    await deleteSubscriber(club.value, row.id)
    ElMessage.success('已刪除')
    refreshAll()
  } catch (e) {
    ElMessage.error(errText(e, '刪除失敗，請稍後再試'))
  }
}

// ── 匯出 ──
const exportVisible = ref(false)
const exporting = ref(false)
async function doExport(purpose: string) {
  exporting.value = true
  try {
    await exportSubscribers(club.value, { purpose, status: filters.status || undefined, source: filters.source || undefined, keyword: filters.keyword.trim() || undefined })
    exportVisible.value = false
    ElMessage.success('已匯出')
  } catch (e) {
    ElMessage.error(errText(e, '匯出失敗，請稍後再試'))
  } finally {
    exporting.value = false
  }
}

const statusTagType = (s: string) => (s === 'subscribed' ? 'success' : 'info')
</script>

<template>
  <div class="newsletter">
    <PageHeader title="電子報">
      <template #meta><FrontendUnitBanner module-code="G3" /></template>
    </PageHeader>

    <el-card v-if="!canView" shadow="never"><el-empty description="你的帳號沒有檢視電子報名單的權限" /></el-card>

    <template v-else>
      <el-alert class="newsletter__block" type="info" show-icon :closable="false" title="這是目前站台自己的訂閱名單（台中磐石與台中藍鯨各自獨立，同一個人可以只退訂其中一站）。官網本身不寄電子報，寄送由外部電子報平台負責；後台只維護名單。" />

      <el-alert v-if="edm && !edm.configured" class="newsletter__block" type="warning" show-icon :closable="false" title="電子報寄送平台尚未串接">
        {{ edm.message }}尚未串接前，名單只保存在本系統；「同步名單」不會真的送出任何資料。
      </el-alert>

      <div v-if="summary" class="newsletter__stats newsletter__block">
        <el-card shadow="never"><div class="newsletter__stat-num">{{ summary.subscribedCount }}</div><div class="newsletter__muted">目前訂閱中</div></el-card>
        <el-card shadow="never"><div class="newsletter__stat-num">{{ summary.unsubscribedCount }}</div><div class="newsletter__muted">已退訂</div></el-card>
        <el-card shadow="never" class="newsletter__sources">
          <div class="newsletter__muted">訂閱中的來源分布</div>
          <div class="newsletter__source-tags">
            <el-tag v-for="s in summary.sources" :key="s.sourceLabel" size="small" type="info">{{ s.sourceLabel }}&emsp;{{ s.count }} 人</el-tag>
            <span v-if="summary.sources.length === 0" class="newsletter__muted">目前沒有訂閱中的人</span>
          </div>
        </el-card>
      </div>

      <el-card shadow="never" class="newsletter__block">
        <div class="newsletter__row">
          <el-input v-model="filters.keyword" placeholder="搜尋 Email" clearable class="newsletter__keyword" @keyup.enter="applyFilter" @clear="applyFilter">
            <template #prefix><el-icon><Search /></el-icon></template>
          </el-input>
          <el-select v-model="filters.status" placeholder="狀態" clearable class="newsletter__select" @change="applyFilter">
            <el-option label="已訂閱" value="subscribed" />
            <el-option label="已退訂" value="unsubscribed" />
          </el-select>
          <el-select v-model="filters.source" placeholder="來源" clearable class="newsletter__select" @change="applyFilter">
            <el-option v-for="s in summary?.sources ?? []" :key="s.sourceLabel" :label="s.sourceLabel" :value="s.source ?? ''" :disabled="!s.source" />
          </el-select>
          <el-button type="primary" @click="applyFilter">篩選</el-button>
          <el-button @click="clearFilter">清除</el-button>
          <span class="newsletter__spacer" />
          <el-button v-if="canExport" @click="exportVisible = true">匯出名單</el-button>
          <el-button v-if="canUpdate" :loading="syncing" @click="handleSync">同步名單到寄送平台</el-button>
          <el-button v-if="canUpdate" type="primary" @click="openAdd">+ 新增訂閱者</el-button>
        </div>
        <p v-if="edmResult" class="newsletter__hint">{{ edmResult }}</p>
      </el-card>

      <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
      <el-card v-else-if="loadError" shadow="never"><el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty></el-card>
      <el-card v-else shadow="never">
        <el-empty v-if="rows.length === 0" description="目前沒有符合條件的訂閱者" />
        <template v-else>
          <el-table v-if="!isMobile" :data="rows" row-key="id">
            <el-table-column prop="email" label="Email" min-width="220" />
            <el-table-column prop="sourceLabel" label="來源" width="130" />
            <el-table-column label="狀態" width="100"><template #default="{ row }"><el-tag :type="statusTagType(row.status)" size="small">{{ row.statusLabel }}</el-tag></template></el-table-column>
            <el-table-column label="訂閱時間" width="150"><template #default="{ row }">{{ formatDateTime(row.subscribedAt) || '—' }}</template></el-table-column>
            <el-table-column label="退訂時間" width="150"><template #default="{ row }">{{ formatDateTime(row.unsubscribedAt) || '—' }}</template></el-table-column>
            <el-table-column v-if="canUpdate" label="操作" width="200" fixed="right">
              <template #default="{ row }">
                <el-button v-if="row.status === 'subscribed'" size="small" text type="primary" @click="unsubscribe(row)">退訂</el-button>
                <el-button v-else size="small" text type="primary" @click="openResub(row)">改回訂閱</el-button>
                <el-button size="small" text type="danger" @click="remove(row)">刪除</el-button>
              </template>
            </el-table-column>
          </el-table>
          <MobileCardList v-else :rows="rows" row-key="id">
            <template #title="{ row }">{{ row.email }}</template>
            <template #meta="{ row }">
              <el-tag :type="statusTagType(row.status)" size="small">{{ row.statusLabel }}</el-tag>
              <span>{{ row.sourceLabel }}</span><span>訂閱 {{ formatDateTime(row.subscribedAt) || '—' }}</span>
            </template>
            <template #actions="{ row }">
              <template v-if="canUpdate">
                <el-button v-if="row.status === 'subscribed'" size="small" text type="primary" @click="unsubscribe(row)">退訂</el-button>
                <el-button v-else size="small" text type="primary" @click="openResub(row)">改回訂閱</el-button>
                <el-button size="small" text type="danger" @click="remove(row)">刪除</el-button>
              </template>
            </template>
          </MobileCardList>
          <el-pagination v-model:current-page="page" v-model:page-size="pageSize" :total="total" :page-sizes="[20, 50, 100]" :layout="isMobile ? 'prev, pager, next' : 'total, sizes, prev, pager, next'" :small="isMobile" class="newsletter__pager" @current-change="load" @size-change="applyFilter" />
        </template>
      </el-card>
    </template>

    <el-dialog v-model="addVisible" title="新增訂閱者" width="420px" :fullscreen="isMobile" :close-on-click-modal="false">
      <el-alert v-if="addError" type="error" show-icon :closable="false" :title="addError" class="newsletter__block" />
      <el-form label-position="top" @submit.prevent="submitAdd">
        <FormField field="email" label="Email" required><el-input v-model="addForm.email" placeholder="name@example.com" /></FormField>
        <FormField field="source" label="來源（選填）"><el-input v-model="addForm.source" maxlength="50" placeholder="沒填會記為「後台新增」" /></FormField>
      </el-form>
      <p class="newsletter__hint">已經在名單中的 Email 不能重複加入；曾經退訂的人不能由後台直接加回，需由本人重新訂閱。</p>
      <template #footer>
        <el-button @click="addVisible = false">取消</el-button>
        <el-button type="primary" :loading="addSaving" @click="submitAdd">加入名單</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="resubVisible" title="改回訂閱" width="440px" :fullscreen="isMobile" :close-on-click-modal="false">
      <p class="newsletter__hint">「{{ resubTarget?.email }}」已經退訂。只有訂閱者本人要求時才能改回訂閱，請寫下說明；系統會留下紀錄。</p>
      <el-alert v-if="resubError" type="error" show-icon :closable="false" :title="resubError" class="newsletter__block" />
      <el-form label-position="top"><FormField field="reason" label="說明（必填）" required><el-input v-model="resubReason" type="textarea" :rows="3" maxlength="200" show-word-limit /></FormField></el-form>
      <template #footer>
        <el-button @click="resubVisible = false">取消</el-button>
        <el-button type="primary" :loading="resubSaving" @click="submitResub">確認改回訂閱</el-button>
      </template>
    </el-dialog>

    <ExportPurposeDialog v-model="exportVisible" title="匯出電子報訂閱名單" description="匯出的是目前站台的名單，會套用上方的篩選條件；內容含 Email、來源、狀態與訂閱／退訂時間。" :loading="exporting" @confirm="doExport" />
  </div>
</template>

<style scoped>
.newsletter { min-width: 0; }
.newsletter__block { margin-bottom: 12px; }
.newsletter__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.newsletter__keyword { width: 240px; max-width: 100%; }
.newsletter__select { width: 150px; max-width: 100%; }
.newsletter__spacer { flex: 1; }
.newsletter__stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 12px; }
.newsletter__sources { grid-column: span 2; }
@media (max-width: 767px) { .newsletter__sources { grid-column: auto; } }
.newsletter__stat-num { font-size: 26px; font-weight: 600; color: var(--admin-text-primary); }
.newsletter__source-tags { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 8px; }
.newsletter__muted { font-size: 12px; color: var(--admin-text-tertiary); }
.newsletter__hint { margin: 8px 0 0; font-size: 12px; line-height: 1.6; color: var(--admin-text-tertiary); }
.newsletter__pager { margin-top: 12px; justify-content: flex-end; }
</style>

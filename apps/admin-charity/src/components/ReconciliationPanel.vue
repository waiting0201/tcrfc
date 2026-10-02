<script setup lang="ts">
/**
 * 每日對帳（docs/22-charity-ui.md §3.8，規劃書 §4.5）：放在捐款紀錄底下的獨立頁籤。
 * 系統每天凌晨自動把前一天「本站的付款紀錄」和「金流端的交易明細」逐筆比對；有差異就需要人看。
 * 三種差異用符號＋文字標示（不只靠顏色）。**差異紀錄不能刪除**，只能標記為「已處理」並留備註（會寫入操作紀錄）。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import DangerConfirmDialog from '@/components/DangerConfirmDialog.vue'
import DiscrepancyBadge from '@/components/DiscrepancyBadge.vue'
import SemanticTag from '@/components/SemanticTag.vue'
import EmptyState from '@/components/EmptyState.vue'
import ErrorState from '@/components/ErrorState.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import {
  getReconciliationRun,
  listReconciliationRuns,
  resolveDiscrepancy,
  runReconciliation,
  type Discrepancy,
  type ReconciliationRun,
  type ReconciliationRunDetail,
} from '@/api/reconciliation'
import { AdminApiError } from '@/api/http'
import { formatMoney, formatTaipei, todayTaipei } from '@/utils/format'
import { hasPermission } from '@/auth/session'
import { useBreakpoint } from '@/composables/useBreakpoint'

const emit = defineEmits<{
  (e: 'open-donation', donationId: string): void
  (e: 'changed'): void
}>()

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const isDesktop = computed(() => breakpoint.value === 'desktop')

// 執行對帳與標記差異已處理，後端要求同一個權限（重新確認付款）。
const canAct = computed(() => hasPermission('n3.donation.recheck_payment'))

const filters = reactive({ range: null as [string, string] | null, onlyPending: false })
const items = ref<ReconciliationRun[]>([])
const totalCount = ref(0)
const page = ref(1)
const pageSize = 20
const loading = ref(false)
const loadError = ref('')

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    const result = await listReconciliationRuns({
      from: filters.range?.[0],
      to: filters.range?.[1],
      onlyPending: filters.onlyPending || undefined,
      page: page.value,
      pageSize,
    })
    items.value = result.items
    totalCount.value = result.totalCount
  } catch (error) {
    loadError.value = error instanceof AdminApiError ? error.detail : '查詢時發生問題，請稍後再試一次'
  } finally {
    loading.value = false
  }
}

function search() {
  page.value = 1
  void load()
}

watch(page, () => { void load() })
onMounted(load)

// ── 差異明細 ───────────────────────────────────────────────────────────
const detailVisible = ref(false)
const detail = ref<ReconciliationRunDetail | null>(null)
const detailLoading = ref(false)
const detailError = ref('')
let lastRunId = ''

function reopenRun() {
  void loadRun(lastRunId)
}

function openRun(run: ReconciliationRun) {
  void loadRun(run.id)
}

async function loadRun(id: string) {
  lastRunId = id
  detailVisible.value = true
  detail.value = null
  detailError.value = ''
  detailLoading.value = true
  try {
    detail.value = await getReconciliationRun(id)
  } catch (error) {
    detailError.value = error instanceof AdminApiError ? error.detail : '讀取差異明細時發生問題'
  } finally {
    detailLoading.value = false
  }
}

// ── 標記已處理 ─────────────────────────────────────────────────────────
const resolveVisible = ref(false)
const resolveTarget = ref<Discrepancy | null>(null)

function askResolve(row: Discrepancy) {
  resolveTarget.value = row
  resolveVisible.value = true
}

async function confirmResolve(note: string) {
  const target = resolveTarget.value
  if (!target) return
  if (note.length < 2) {
    ElMessage.warning('處理備註至少 2 個字')
    return
  }
  try {
    await resolveDiscrepancy(target.id, note)
    ElMessage.success('已標記為已處理')
    emit('changed')
    detail.value = await getReconciliationRun(lastRunId)
    await load()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.detail : '標記失敗，請稍後再試')
  }
}

// ── 手動對帳 ───────────────────────────────────────────────────────────
const runVisible = ref(false)
const runDate = ref('')
const running = ref(false)
const runUnavailable = ref('')
const runError = ref('')

function yesterdayTaipei(): string {
  return new Date(Date.parse(`${todayTaipei()}T00:00:00Z`) - 86_400_000).toISOString().slice(0, 10)
}

function openRunDialog() {
  runDate.value = yesterdayTaipei()
  runUnavailable.value = ''
  runError.value = ''
  runVisible.value = true
}

const disableFuture = (d: Date) => d.getTime() > Date.now()

async function doRun() {
  if (running.value || !runDate.value) return
  running.value = true
  runUnavailable.value = ''
  runError.value = ''
  try {
    const r = await runReconciliation(runDate.value)
    runVisible.value = false
    const extra = [r.newDiscrepancies > 0 ? `新增 ${r.newDiscrepancies} 筆差異` : '', r.autoResolved > 0 ? `${r.autoResolved} 筆原有差異已自動結案` : '']
      .filter(Boolean)
      .join('，')
    ElMessage.success(`${r.runOn} 對帳完成：比對 ${r.comparedCount} 筆，相符 ${r.matchedCount} 筆，差異 ${r.discrepancyCount} 筆${extra ? `（${extra}）` : ''}`)
    emit('changed')
    await load()
  } catch (error) {
    if (error instanceof AdminApiError && error.status === 503) {
      // 取不到金流端明細：不是操作失敗，要把原因與「系統做了什麼」講清楚。
      runUnavailable.value = error.detail
      await load()
    } else {
      runError.value = error instanceof AdminApiError ? error.detail : '對帳失敗，請稍後再試'
    }
  } finally {
    running.value = false
  }
}

const pendingOf = (d: ReconciliationRunDetail | null) => d?.discrepancies.filter((x) => x.resolutionStatus === 'pending').length ?? 0
</script>

<template>
  <div>
    <div class="recon__toolbar">
      <el-date-picker v-model="filters.range" type="daterange" value-format="YYYY-MM-DD" range-separator="至" start-placeholder="開始日期" end-placeholder="結束日期" style="max-width: 280px" />
      <el-checkbox v-model="filters.onlyPending">只看還有未處理差異的</el-checkbox>
      <el-button type="primary" @click="search">查詢</el-button>
      <span class="recon__spacer" />
      <el-button v-if="canAct" @click="openRunDialog">立即對帳</el-button>
    </div>
    <p class="recon__hint">系統每天凌晨會自動對前一天的交易對帳。這裡可以查看每天的結果、處理有差異的交易，也能手動重新對帳任何一天。</p>

    <ErrorState v-if="loadError" :text="loadError" @retry="load" />
    <EmptyState v-else-if="!loading && items.length === 0" text="沒有符合條件的對帳紀錄" />
    <template v-else>
      <el-table v-if="!isMobile" v-loading="loading" :data="items" style="width: 100%" @row-click="openRun">
        <el-table-column label="對帳日期" width="120">
          <template #default="{ row }: { row: ReconciliationRun }"><el-link type="primary" :underline="false">{{ row.runOn }}</el-link></template>
        </el-table-column>
        <el-table-column label="狀態" width="110">
          <template #default="{ row }: { row: ReconciliationRun }">
            <SemanticTag :variant="row.status === 'completed' ? 'success' : 'danger'">{{ row.status === 'completed' ? '已完成' : '未能完成' }}</SemanticTag>
          </template>
        </el-table-column>
        <el-table-column label="比對筆數" width="100" align="right">
          <template #default="{ row }: { row: ReconciliationRun }">{{ row.status === 'completed' ? row.comparedCount : '—' }}</template>
        </el-table-column>
        <el-table-column v-if="isDesktop" label="相符" width="90" align="right">
          <template #default="{ row }: { row: ReconciliationRun }">{{ row.status === 'completed' ? row.matchedCount : '—' }}</template>
        </el-table-column>
        <el-table-column label="差異" width="90" align="right">
          <template #default="{ row }: { row: ReconciliationRun }">{{ row.status === 'completed' ? row.discrepancyCount : '—' }}</template>
        </el-table-column>
        <el-table-column label="未處理" width="90" align="right">
          <template #default="{ row }: { row: ReconciliationRun }">
            <SemanticTag v-if="row.pendingCount > 0" variant="warning">{{ row.pendingCount }}</SemanticTag>
            <span v-else>0</span>
          </template>
        </el-table-column>
        <el-table-column v-if="isDesktop" label="執行時間" min-width="150">
          <template #default="{ row }: { row: ReconciliationRun }">{{ formatTaipei(row.ranAt) }}</template>
        </el-table-column>
      </el-table>

      <MobileCardList v-else :items="items">
        <template #default="{ item }: { item: ReconciliationRun }">
          <div class="recon__card" @click="openRun(item)">
            <div class="recon__card-head">
              <strong>{{ item.runOn }}</strong>
              <SemanticTag :variant="item.status === 'completed' ? 'success' : 'danger'">{{ item.status === 'completed' ? '已完成' : '未能完成' }}</SemanticTag>
            </div>
            <p v-if="item.status === 'completed'" class="recon__card-meta">
              比對 {{ item.comparedCount }} 筆・相符 {{ item.matchedCount }} 筆・差異 {{ item.discrepancyCount }} 筆
              <SemanticTag v-if="item.pendingCount > 0" variant="warning">未處理 {{ item.pendingCount }}</SemanticTag>
            </p>
            <p v-else class="recon__card-meta">這一天沒能取得金流端的交易明細。</p>
          </div>
        </template>
      </MobileCardList>
      <el-pagination v-if="totalCount > pageSize" v-model:current-page="page" class="recon__pager" layout="prev, pager, next" :page-size="pageSize" :total="totalCount" />
    </template>

    <el-drawer v-model="detailVisible" title="對帳差異明細" :size="isMobile ? '100%' : '720px'">
      <ErrorState v-if="detailError" :text="detailError" @retry="reopenRun" />
      <div v-else-if="detailLoading || !detail" class="recon__loading"><el-icon class="is-loading" :size="24"><Loading /></el-icon></div>
      <div v-else>
        <div class="recon__detail-head">
          <strong>{{ detail.run.runOn }}</strong>
          <SemanticTag :variant="detail.run.status === 'completed' ? 'success' : 'danger'">{{ detail.run.status === 'completed' ? '已完成' : '未能完成' }}</SemanticTag>
        </div>
        <el-alert
          v-if="detail.run.status === 'failed'"
          type="warning"
          :closable="false"
          show-icon
          title="這一天沒能取得金流端的交易明細，所以沒有進行比對（不會把當天的捐款誤判成差異）。可以稍後按「立即對帳」重新執行。"
          class="recon__alert"
        />
        <el-descriptions v-else :column="2" border size="small">
          <el-descriptions-item label="比對筆數">{{ detail.run.comparedCount }}</el-descriptions-item>
          <el-descriptions-item label="相符">{{ detail.run.matchedCount }}</el-descriptions-item>
          <el-descriptions-item label="差異">{{ detail.run.discrepancyCount }}</el-descriptions-item>
          <el-descriptions-item label="未處理">{{ pendingOf(detail) }}</el-descriptions-item>
          <el-descriptions-item label="執行時間" :span="2">{{ formatTaipei(detail.run.ranAt) }}</el-descriptions-item>
        </el-descriptions>

        <EmptyState v-if="detail.run.status === 'completed' && detail.discrepancies.length === 0" text="這一天兩邊的紀錄完全一致" />
        <ul v-else class="recon__list">
          <li v-for="d in detail.discrepancies" :key="d.id" class="recon__item" :class="{ 'recon__item--done': d.resolutionStatus === 'resolved' }">
            <div class="recon__item-head">
              <DiscrepancyBadge :type="d.type" />
              <SemanticTag :variant="d.resolutionStatus === 'resolved' ? 'success' : 'warning'">{{ d.resolutionStatus === 'resolved' ? '已處理' : '待處理' }}</SemanticTag>
            </div>
            <dl class="recon__facts">
              <div>
                <dt>捐款單</dt>
                <dd>
                  <el-link v-if="d.donationId && d.orderNo" type="primary" :underline="false" @click="emit('open-donation', d.donationId)">{{ d.orderNo }}</el-link>
                  <span v-else>本站查無對應捐款單</span>
                </dd>
              </div>
              <div>
                <dt>本站金額</dt>
                <dd>{{ d.siteAmount !== null ? formatMoney(d.siteAmount) : '—' }}</dd>
              </div>
              <div>
                <dt>金流端金額</dt>
                <dd>{{ d.gatewayAmount !== null ? formatMoney(d.gatewayAmount) : '—' }}</dd>
              </div>
              <div v-if="d.gatewayTransactionId">
                <dt>金流交易編號</dt>
                <dd>{{ d.gatewayTransactionId }}</dd>
              </div>
            </dl>
            <p v-if="d.resolutionStatus === 'resolved'" class="recon__note">
              處理備註：{{ d.resolveNote ?? '—' }}（{{ d.resolvedByName ?? '系統' }}，{{ formatTaipei(d.updatedAt) }}）
            </p>
            <el-button v-else-if="canAct" size="small" @click="askResolve(d)">標記已處理</el-button>
          </li>
        </ul>
        <p class="recon__hint">對帳結果會永久保留供稽核，差異紀錄不能刪除，只能標記為已處理。</p>
      </div>
    </el-drawer>

    <DangerConfirmDialog
      v-model="resolveVisible"
      title="標記差異已處理"
      reason-label="處理備註"
      confirm-text="標記為已處理"
      audit-notice="請寫下處理結果（例如已向金流客服確認、已補開捐款單）。標記後無法改回待處理，這個操作會記入操作紀錄。"
      @confirm="confirmResolve"
    >
      這筆差異處理完了嗎？
    </DangerConfirmDialog>

    <el-dialog v-model="runVisible" title="立即對帳" width="480px">
      <p class="recon__hint recon__hint--dialog">選擇要對帳的日期（預設是昨天）。同一天可以重複對帳：新發現的差異會新增，已經處理過的差異不會被改動，兩邊已經一致的舊差異會自動結案。</p>
      <el-form-item label="對帳日期">
        <el-date-picker v-model="runDate" type="date" value-format="YYYY-MM-DD" :disabled-date="disableFuture" :clearable="false" />
      </el-form-item>
      <el-alert v-if="runUnavailable" type="warning" :closable="false" show-icon title="目前取不到金流端的交易明細，這次沒有完成對帳">
        <p class="recon__unavailable">{{ runUnavailable }}</p>
        <p class="recon__unavailable">
          可能是金流對帳功能還沒正式串接，或金流服務暫時無法連線。系統已把這一天記成「未能完成」的對帳紀錄，
          不會把當天所有捐款誤判成差異，這一天如果先前已經對帳成功，原本的結果也不會被蓋掉。請稍後再試；持續失敗請聯絡系統維護人員。
        </p>
      </el-alert>
      <el-alert v-if="runError" type="error" :closable="false" show-icon :title="runError" />
      <template #footer>
        <el-button @click="runVisible = false">關閉</el-button>
        <el-button type="primary" :loading="running" :disabled="!runDate" @click="doRun">開始對帳</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.recon__toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--charity-admin-space-2);
}

.recon__spacer {
  flex: 1;
}

.recon__hint {
  margin: var(--charity-admin-space-2) 0 var(--charity-admin-space-4);
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
  line-height: 1.7;
}

.recon__hint--dialog {
  margin-top: 0;
}

.recon__pager {
  margin-top: var(--charity-admin-space-4);
  justify-content: center;
}

.recon__loading {
  display: flex;
  justify-content: center;
  padding: var(--charity-admin-space-8);
}

.recon__detail-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: var(--charity-admin-space-3);
}

.recon__alert {
  margin-bottom: var(--charity-admin-space-3);
}

.recon__card {
  cursor: pointer;
}

.recon__card-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.recon__card-meta {
  margin: var(--charity-admin-space-2) 0 0;
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
  line-height: 1.8;
}

.recon__list {
  list-style: none;
  margin: var(--charity-admin-space-4) 0 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: var(--charity-admin-space-3);
}

.recon__item {
  border: 1px solid var(--charity-admin-border);
  border-radius: 6px;
  padding: var(--charity-admin-space-3);
}

.recon__item--done {
  background: var(--charity-admin-bg-surface-2);
}

.recon__item-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: var(--charity-admin-space-2);
}

.recon__facts {
  margin: 0 0 var(--charity-admin-space-2);
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  gap: var(--charity-admin-space-2);
  font-size: 13px;
}

.recon__facts dt {
  color: var(--charity-admin-text-tertiary);
}

.recon__facts dd {
  margin: 0;
  overflow-wrap: anywhere;
}

.recon__note {
  margin: 0;
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
}

.recon__unavailable {
  margin: var(--charity-admin-space-1) 0 0;
  font-size: 13px;
  line-height: 1.7;
}
</style>

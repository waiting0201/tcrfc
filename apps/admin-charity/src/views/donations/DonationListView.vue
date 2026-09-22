<script setup lang="ts">
/**
 * N3 捐款紀錄（docs/22-charity-ui.md §3.7.3／§3.8）：主列表 ＋ 異常佇列（三子頁籤，
 * 第三個子頁籤是對帳批次與差異，§3.8 的「對帳與異常佇列」畫面）＋ 側拉詳情 ＋ 退款流程。
 */
import { computed, reactive, ref } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import PageHeader from '@/components/PageHeader.vue'
import DonationStatusTag from '@/components/DonationStatusTag.vue'
import InvoiceStatusTag from '@/components/InvoiceStatusTag.vue'
import DiscrepancyBadge from '@/components/DiscrepancyBadge.vue'
import DangerConfirmDialog from '@/components/DangerConfirmDialog.vue'
import EmptyState from '@/components/EmptyState.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { DONATIONS, PROJECTS, STORES, reissueInvoice, refundDonation, type Donation } from '@/data/fixtures'
import {
  RECONCILIATION_RUNS,
  RECONCILIATION_DISCREPANCIES,
  markDiscrepancyResolved,
  appendAuditLog,
  type ReconciliationRun,
} from '@/data/reconciliationAudit'
import { formatDateTime, formatMoney } from '@/utils/format'
import { currentUser, hasPermission } from '@/data/session'
import { useBreakpoint } from '@/composables/useBreakpoint'

const route = useRoute()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
// 平板寬度（768–1023px）表格仍是 el-table，但欄位總寬度會超出可視寬度——CSS display:none
// 隱藏儲存格不會讓 el-table 縮小欄位總寬（它的版面計算是照 el-table-column 的數量與 width
// 參數加總，不看 CSS 有沒有把某個儲存格藏起來），所以次要欄位要用 v-if 整欄不渲染，
// 不能只用 CSS 隱藏（這是本次驗收在平板寬度實測抓到的問題，不是理論假設）。
const isDesktop = computed(() => breakpoint.value === 'desktop')

const activeTab = ref(route.query.tab === 'reconciliation' ? 'anomaly' : 'list')
const anomalySubTab = ref(route.query.tab === 'reconciliation' ? 'reconciliation' : 'confirm-failed')

// ── 主列表 ──────────────────────────────────────────────────────────────
const filters = reactive({
  status: '' as Donation['status'] | '',
  projectKey: '',
  storeKey: '',
  invoiceStatus: '' as string,
})

const filteredDonations = computed(() =>
  DONATIONS.filter((d) => {
    if (filters.status && d.status !== filters.status) return false
    if (filters.projectKey && d.projectKey !== filters.projectKey) return false
    if (filters.storeKey && d.storeKey !== filters.storeKey) return false
    if (filters.invoiceStatus) {
      const label = d.invoice?.voidStatus === 'voided' ? 'voided'
        : d.invoice?.voidStatus === 'allowance' ? 'allowance'
        : d.invoice?.issueStatus ?? 'pending'
      if (label !== filters.invoiceStatus) return false
    }
    return true
  }),
)

const detailDonation = ref<Donation | null>(null)
const detailDrawerVisible = computed({
  get: () => detailDonation.value !== null,
  set: (v: boolean) => {
    if (!v) detailDonation.value = null
  },
})
const hiddenFromCreditList = reactive(new Set<string>())

function openDetail(donation: Donation) {
  detailDonation.value = donation
}

function resend(donation: Donation) {
  ElMessage.success(`已重寄感謝信至 ${donation.donorEmail.replace(/(.{2}).+(@.+)/, '$1***$2')}（mockup）`)
}

function toggleCreditListHidden(donation: Donation) {
  if (hiddenFromCreditList.has(donation.orderNo)) {
    hiddenFromCreditList.delete(donation.orderNo)
    ElMessage.success('已重新列入徵信名單')
  } else {
    hiddenFromCreditList.add(donation.orderNo)
    ElMessage.success('已從徵信名單隱藏')
  }
}

function doReissue(donation: Donation) {
  reissueInvoice(donation.orderNo)
  ElMessage.success('已重新開立發票')
}

// ── 退款 ────────────────────────────────────────────────────────────────
const refundDialogVisible = ref(false)
const refundTarget = ref<Donation | null>(null)

function openRefund(donation: Donation) {
  refundTarget.value = donation
  refundDialogVisible.value = true
}

function confirmRefund(reason: string) {
  if (!refundTarget.value) return
  refundDonation(refundTarget.value.orderNo, reason, currentUser.value.username)
  appendAuditLog({
    adminUsername: currentUser.value.username,
    action: 'refund',
    targetType: 'donation',
    targetLabel: refundTarget.value.orderNo,
    changeSummary: `退款金額 ${refundTarget.value.amount} 元，原因：${reason}`,
    purposeNote: null,
    sourceIp: '203.0.113.99',
  })
  ElMessage.success('已完成退款')
  detailDonation.value = null
}

// ── 異常佇列：已扣款但確認失敗（本次種子資料沒有這種案例，見畫面上的空狀態說明） ──────
const confirmFailedDonations = computed(() => [] as Donation[])

// ── 異常佇列：發票開立失敗 ─────────────────────────────────────────────
const invoiceFailedDonations = computed(() => DONATIONS.filter((d) => d.invoice?.issueStatus === 'failed'))

// ── 對帳批次與差異 ─────────────────────────────────────────────────────
const expandedRun = ref<ReconciliationRun | null>(null)

function toggleRun(run: ReconciliationRun) {
  expandedRun.value = expandedRun.value?.id === run.id ? null : run
}

const discrepanciesForRun = computed(() =>
  RECONCILIATION_DISCREPANCIES.filter((d) => d.runId === expandedRun.value?.id),
)

const resolveDialogVisible = ref(false)
const resolveTarget = ref<{ runId: string; type: string } | null>(null)

function openResolve(runId: string, type: 'site_only' | 'gateway_only' | 'amount_mismatch') {
  resolveTarget.value = { runId, type }
  resolveDialogVisible.value = true
}

function confirmResolve(note: string) {
  if (!resolveTarget.value) return
  markDiscrepancyResolved(
    resolveTarget.value.runId,
    resolveTarget.value.type as 'site_only' | 'gateway_only' | 'amount_mismatch',
    note,
    currentUser.value.username,
  )
  appendAuditLog({
    adminUsername: currentUser.value.username,
    action: 'resolve_discrepancy',
    targetType: 'reconciliation_discrepancy',
    targetLabel: `${resolveTarget.value.runId}／${resolveTarget.value.type}`,
    changeSummary: `標記對帳差異已處理：${note}`,
    purposeNote: null,
    sourceIp: '203.0.113.99',
  })
  ElMessage.success('已標記為已處理')
}
</script>

<template>
  <div>
    <PageHeader title="捐款紀錄" frontend-unit="捐款表單／結果頁" />

    <el-tabs v-model="activeTab">
      <el-tab-pane label="捐款紀錄" name="list">
        <div class="donation-list__filters">
          <el-select v-model="filters.status" placeholder="狀態" clearable style="width: 130px">
            <el-option label="已建立" value="created" />
            <el-option label="處理中" value="pending" />
            <el-option label="已完成" value="paid" />
            <el-option label="付款失敗" value="failed" />
            <el-option label="已逾時" value="expired" />
            <el-option label="已退款" value="refunded" />
          </el-select>
          <el-select v-model="filters.projectKey" placeholder="項目" clearable style="width: 200px">
            <el-option v-for="p in PROJECTS" :key="p.key" :label="p.nameZh" :value="p.key" />
          </el-select>
          <el-select v-model="filters.storeKey" placeholder="店家" clearable style="width: 180px">
            <el-option v-for="s in STORES" :key="s.key" :label="s.nameZh" :value="s.key" />
          </el-select>
          <el-select v-model="filters.invoiceStatus" placeholder="發票狀態" clearable style="width: 140px">
            <el-option label="待開立" value="pending" />
            <el-option label="已開立" value="issued" />
            <el-option label="開立失敗" value="failed" />
            <el-option label="已作廢" value="voided" />
            <el-option label="已折讓" value="allowance" />
          </el-select>
        </div>

        <EmptyState v-if="filteredDonations.length === 0" text="沒有符合篩選條件的捐款紀錄" />

        <el-table v-else-if="!isMobile" :data="filteredDonations" style="width: 100%" @row-click="openDetail">
          <el-table-column prop="orderNo" label="單號" width="180" />
          <el-table-column v-if="isDesktop" label="時間" width="150">
            <template #default="{ row }: { row: Donation }">{{ formatDateTime(row.createdAt) }}</template>
          </el-table-column>
          <el-table-column label="金額" width="110">
            <template #default="{ row }: { row: Donation }">{{ formatMoney(row.amount) }}</template>
          </el-table-column>
          <el-table-column v-if="isDesktop" prop="projectName" label="項目" min-width="160" />
          <el-table-column v-if="isDesktop" label="來源店家" width="140">
            <template #default="{ row }: { row: Donation }">{{ row.storeName ?? '無' }}</template>
          </el-table-column>
          <el-table-column label="狀態" width="100">
            <template #default="{ row }: { row: Donation }"><DonationStatusTag :status="row.status" /></template>
          </el-table-column>
          <el-table-column v-if="isDesktop" label="發票狀態" width="100">
            <template #default="{ row }: { row: Donation }">
              <InvoiceStatusTag v-if="row.invoice" :issue-status="row.invoice.issueStatus" :void-status="row.invoice.voidStatus" />
              <span v-else>—</span>
            </template>
          </el-table-column>
          <el-table-column v-if="isDesktop" label="具名／匿名" width="100">
            <template #default="{ row }: { row: Donation }">{{ row.isAnonymous ? '匿名' : '具名' }}</template>
          </el-table-column>
          <el-table-column label="操作" width="80" fixed="right">
            <template #default="{ row }: { row: Donation }">
              <el-button size="small" text type="primary" @click.stop="openDetail(row)">查看</el-button>
            </template>
          </el-table-column>
        </el-table>

        <MobileCardList v-else :items="filteredDonations">
          <template #default="{ item }: { item: Donation }">
            <div class="donation-list__card-head">
              <span class="donation-list__card-order">{{ item.orderNo }}</span>
              <DonationStatusTag :status="item.status" />
            </div>
            <p class="donation-list__card-meta">
              {{ formatMoney(item.amount) }}・{{ item.projectName }}<br>
              來源店家：{{ item.storeName ?? '無' }}・{{ item.isAnonymous ? '匿名' : '具名' }}
            </p>
            <div class="donation-list__card-actions">
              <el-button size="small" text type="primary" @click="openDetail(item)">查看</el-button>
            </div>
          </template>
        </MobileCardList>
      </el-tab-pane>

      <el-tab-pane label="異常佇列" name="anomaly">
        <el-tabs v-model="anomalySubTab" tab-position="top" class="donation-list__anomaly-tabs">
          <el-tab-pane label="已扣款但確認失敗" name="confirm-failed">
            <EmptyState
              v-if="confirmFailedDonations.length === 0"
              text="目前沒有已扣款但確認失敗的案例（本機種子資料未涵蓋這種情境，正式環境若發生會列在這裡並主動通知）"
            />
          </el-tab-pane>

          <el-tab-pane label="發票開立失敗" name="invoice-failed">
            <EmptyState v-if="invoiceFailedDonations.length === 0" text="目前沒有發票開立失敗的案例" />
            <el-table v-else-if="!isMobile" :data="invoiceFailedDonations" style="width: 100%">
              <el-table-column prop="orderNo" label="單號" width="180" />
              <el-table-column prop="projectName" label="項目" min-width="160" />
              <el-table-column label="金額" width="110">
                <template #default="{ row }: { row: Donation }">{{ formatMoney(row.amount) }}</template>
              </el-table-column>
              <el-table-column label="操作" width="140">
                <template #default="{ row }: { row: Donation }">
                  <el-button size="small" type="primary" @click="doReissue(row)">重新開立發票</el-button>
                </template>
              </el-table-column>
            </el-table>
            <MobileCardList v-else :items="invoiceFailedDonations">
              <template #default="{ item }: { item: Donation }">
                <div class="donation-list__card-head">
                  <span class="donation-list__card-order">{{ item.orderNo }}</span>
                  <span>{{ formatMoney(item.amount) }}</span>
                </div>
                <p class="donation-list__card-meta">{{ item.projectName }}</p>
                <div class="donation-list__card-actions">
                  <el-button size="small" type="primary" @click="doReissue(item)">重新開立發票</el-button>
                </div>
              </template>
            </MobileCardList>
          </el-tab-pane>

          <el-tab-pane label="對帳差異" name="reconciliation">
            <p class="donation-list__section-note">
              對帳批次列表（每次比對本站與金流端的交易紀錄），點一列展開差異明細。
              🔴 差異記錄本身不得刪除，只能標記處理狀態。
            </p>
            <el-table v-if="!isMobile" :data="RECONCILIATION_RUNS" style="width: 100%" @row-click="toggleRun">
              <el-table-column label="日期" width="130">
                <template #default="{ row }: { row: ReconciliationRun }">{{ row.runOn }}</template>
              </el-table-column>
              <el-table-column label="比對筆數" width="100">
                <template #default="{ row }: { row: ReconciliationRun }">{{ row.comparedCount }}</template>
              </el-table-column>
              <el-table-column label="相符" width="80">
                <template #default="{ row }: { row: ReconciliationRun }">{{ row.matchedCount }}</template>
              </el-table-column>
              <el-table-column label="差異" width="80">
                <template #default="{ row }: { row: ReconciliationRun }">{{ row.discrepancyCount }}</template>
              </el-table-column>
              <el-table-column label="狀態" width="100">
                <template #default>
                  <el-tag class="charity-tag charity-tag--success" size="small">已完成</el-tag>
                </template>
              </el-table-column>
              <el-table-column label="操作" width="100">
                <template #default="{ row }: { row: ReconciliationRun }">
                  <el-button size="small" text type="primary" @click.stop="toggleRun(row)">
                    {{ expandedRun?.id === row.id ? '收合' : '查看差異' }}
                  </el-button>
                </template>
              </el-table-column>
            </el-table>

            <MobileCardList v-else :items="RECONCILIATION_RUNS">
              <template #default="{ item }: { item: ReconciliationRun }">
                <div class="donation-list__card-head">
                  <span class="donation-list__card-order">{{ item.runOn }}</span>
                  <el-tag class="charity-tag charity-tag--success" size="small">已完成</el-tag>
                </div>
                <p class="donation-list__card-meta">比對 {{ item.comparedCount }} 筆・相符 {{ item.matchedCount }}・差異 {{ item.discrepancyCount }}</p>
                <div class="donation-list__card-actions">
                  <el-button size="small" text type="primary" @click="toggleRun(item)">
                    {{ expandedRun?.id === item.id ? '收合' : '查看差異' }}
                  </el-button>
                </div>
              </template>
            </MobileCardList>

            <div v-if="expandedRun" class="donation-list__discrepancies">
              <EmptyState v-if="discrepanciesForRun.length === 0" text="這個批次沒有差異" />
              <div v-for="(d, i) in discrepanciesForRun" :key="i" class="donation-list__discrepancy-row">
                <DiscrepancyBadge :type="d.type" />
                <span class="donation-list__discrepancy-detail">
                  捐款單：{{ d.donationOrderNo ?? '—' }}
                  本站：{{ d.siteAmount !== null ? formatMoney(d.siteAmount) : '—' }}
                  金流端：{{ d.gatewayAmount !== null ? formatMoney(d.gatewayAmount) : '—' }}
                  <span v-if="d.gatewayTransactionId">・金流交易碼：{{ d.gatewayTransactionId }}</span>
                </span>
                <span v-if="d.resolutionStatus === 'resolved'" class="donation-list__resolved">
                  已處理（{{ d.resolvedBy }}）：{{ d.resolveNote }}
                </span>
                <el-button v-else size="small" @click="openResolve(d.runId, d.type)">標記已處理</el-button>
              </div>
            </div>
          </el-tab-pane>
        </el-tabs>
      </el-tab-pane>
    </el-tabs>

    <!-- 詳情側拉 -->
    <el-drawer v-model="detailDrawerVisible" title="捐款單詳情" size="480px">
      <template v-if="detailDonation">
        <dl class="donation-detail">
          <dt>單號</dt>
          <dd>{{ detailDonation.orderNo }}</dd>
          <dt>狀態</dt>
          <dd><DonationStatusTag :status="detailDonation.status" /></dd>
          <dt>金額</dt>
          <dd>{{ formatMoney(detailDonation.amount) }}</dd>
          <dt>項目</dt>
          <dd>{{ detailDonation.projectName }}</dd>
          <dt>來源店家</dt>
          <dd>{{ detailDonation.storeName ?? '無' }}</dd>
          <dt>捐款人</dt>
          <dd>{{ detailDonation.donorName }}（{{ detailDonation.isAnonymous ? '匿名' : '具名' }}）</dd>
          <dt>Email</dt>
          <dd>{{ detailDonation.donorEmail }}</dd>
          <dt>建立時間</dt>
          <dd>{{ formatDateTime(detailDonation.createdAt) }}</dd>
          <dt>付款完成時間</dt>
          <dd>{{ formatDateTime(detailDonation.paidAt) }}</dd>
          <dt v-if="detailDonation.payment">金流交易識別碼</dt>
          <dd v-if="detailDonation.payment">{{ detailDonation.orderNo }}-GW（mockup 占位值）</dd>
        </dl>

        <h3 class="donation-detail__subtitle">分潤快照</h3>
        <p class="donation-detail__snapshot-note">成立時快照，事後改設定不影響此筆</p>
        <div v-if="detailDonation.split" class="donation-detail__split">
          <div>
            <p class="donation-detail__split-label">店家回饋金</p>
            <p class="donation-detail__split-value">{{ formatMoney(detailDonation.split.storeAmount) }}</p>
          </div>
          <div>
            <p class="donation-detail__split-label">項目撥付金</p>
            <p class="donation-detail__split-value">{{ formatMoney(detailDonation.split.projectAmount) }}</p>
          </div>
          <div>
            <p class="donation-detail__split-label">協會留存</p>
            <p class="donation-detail__split-value">{{ formatMoney(detailDonation.split.associationAmount) }}</p>
          </div>
        </div>

        <h3 v-if="detailDonation.status === 'refunded'" class="donation-detail__subtitle">退款資訊</h3>
        <p v-if="detailDonation.status === 'refunded'" class="donation-detail__snapshot-note">
          {{ detailDonation.refundReason }}（處理人：{{ detailDonation.refundedBy }}）
        </p>

        <h3 class="donation-detail__subtitle">操作</h3>
        <div class="donation-detail__actions">
          <el-button
            v-if="hasPermission('n3.donation.refund') && detailDonation.status !== 'refunded'"
            @click="openRefund(detailDonation)"
          >
            退款
          </el-button>
          <el-tooltip v-else-if="detailDonation.status === 'refunded'" content="此筆已退款">
            <el-button disabled>退款</el-button>
          </el-tooltip>
          <el-button @click="resend(detailDonation)">重寄感謝信</el-button>
          <el-button v-if="detailDonation.invoice?.issueStatus === 'failed'" @click="doReissue(detailDonation)">重新開立發票</el-button>
          <el-button @click="toggleCreditListHidden(detailDonation)">
            {{ hiddenFromCreditList.has(detailDonation.orderNo) ? '重新列入徵信名單' : '隱藏於徵信名單' }}
          </el-button>
        </div>
      </template>
    </el-drawer>

    <DangerConfirmDialog
      v-model="refundDialogVisible"
      title="確認退款"
      reason-label="退款原因"
      confirm-text="確認退款"
      audit-notice="此操作將：作廢或折讓相關發票、沖回店家與項目回饋金、寄送退款通知信，並寫入稽核紀錄"
      @confirm="confirmRefund"
    />

    <DangerConfirmDialog
      v-model="resolveDialogVisible"
      title="標記對帳差異已處理"
      reason-label="處理備註"
      confirm-text="標記已處理"
      audit-notice="此操作將寫入稽核紀錄"
      @confirm="confirmResolve"
    />
  </div>
</template>

<style scoped>
.donation-list__filters {
  display: flex;
  flex-wrap: wrap;
  gap: var(--charity-admin-space-2);
  margin-bottom: var(--charity-admin-space-4);
}

.donation-list__anomaly-tabs {
  margin-top: var(--charity-admin-space-2);
}

.donation-list__section-note {
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
  margin: 0 0 var(--charity-admin-space-3);
}

.donation-list__card-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: var(--charity-admin-space-2);
}

.donation-list__card-order {
  font-weight: 600;
  color: var(--charity-admin-text-primary);
  font-size: 13px;
}

.donation-list__card-meta {
  margin: var(--charity-admin-space-2) 0;
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
  line-height: 1.6;
}

.donation-list__card-actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--charity-admin-space-1);
  padding-top: var(--charity-admin-space-2);
  border-top: 1px solid var(--charity-admin-border);
}

.donation-list__discrepancies {
  margin-top: var(--charity-admin-space-4);
  border-top: 1px solid var(--charity-admin-border);
  padding-top: var(--charity-admin-space-4);
  display: flex;
  flex-direction: column;
  gap: var(--charity-admin-space-3);
}

.donation-list__discrepancy-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--charity-admin-space-3);
  padding: var(--charity-admin-space-2) 0;
  border-bottom: 1px solid var(--charity-admin-border);
}

.donation-list__discrepancy-detail {
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
}

.donation-list__resolved {
  font-size: 12px;
  color: var(--charity-success-text);
}

.donation-detail {
  display: grid;
  grid-template-columns: 100px 1fr;
  gap: var(--charity-admin-space-2) var(--charity-admin-space-3);
  margin: 0;
}

.donation-detail dt {
  color: var(--charity-admin-text-tertiary);
  font-size: 13px;
}

.donation-detail dd {
  margin: 0;
  color: var(--charity-admin-text-primary);
  font-size: 13px;
}

.donation-detail__subtitle {
  font-size: 14px;
  margin: var(--charity-admin-space-5) 0 var(--charity-admin-space-2);
  padding-top: var(--charity-admin-space-3);
  border-top: 1px solid var(--charity-admin-border);
}

.donation-detail__snapshot-note {
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
  margin: 0 0 var(--charity-admin-space-2);
}

.donation-detail__split {
  display: flex;
  gap: var(--charity-admin-space-4);
}

.donation-detail__split-label {
  margin: 0;
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
}

.donation-detail__split-value {
  margin: 2px 0 0;
  font-size: 15px;
  font-weight: 600;
  color: var(--charity-admin-text-primary);
}

.donation-detail__actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--charity-admin-space-2);
}
</style>

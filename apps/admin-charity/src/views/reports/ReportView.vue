<script setup lang="ts">
/** N6 捐款報表（docs/22-charity-ui.md §3.7.6）：五個固定報表，共通篩選列固定在頁首 */
import { computed, ref } from 'vue'
import { ElMessage } from 'element-plus'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { DONATIONS, PROJECTS, STORES } from '@/data/fixtures'
import { appendAuditLog } from '@/data/reconciliationAudit'
import { formatDateTime, formatMoney } from '@/utils/format'
import { currentUser } from '@/data/session'
import { useBreakpoint } from '@/composables/useBreakpoint'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
// 平板寬度（768–1023px）表格仍是 el-table，但欄位總寬度會超出可視寬度——CSS display:none
// 隱藏儲存格不會讓 el-table 縮小欄位總寬（它的版面計算是照 el-table-column 的數量與 width
// 參數加總，不看 CSS 有沒有把某個儲存格藏起來），所以次要欄位要用 v-if 整欄不渲染，
// 不能只用 CSS 隱藏（這是本次驗收在平板寬度實測抓到的問題，不是理論假設）。
const isDesktop = computed(() => breakpoint.value === 'desktop')

const activeReport = ref('overview')

const paidDonations = computed(() => DONATIONS.filter((d) => d.status === 'paid'))

const overview = computed(() => ({
  count: paidDonations.value.length,
  total: paidDonations.value.reduce((sum, d) => sum + d.amount, 0),
  storeShare: paidDonations.value.reduce((sum, d) => sum + (d.split?.storeAmount ?? 0), 0),
  projectShare: paidDonations.value.reduce((sum, d) => sum + (d.split?.projectAmount ?? 0), 0),
  associationShare: paidDonations.value.reduce((sum, d) => sum + (d.split?.associationAmount ?? 0), 0),
}))

const byStore = computed(() =>
  STORES.map((s) => ({
    name: s.nameZh,
    count: s.donationCount,
    total: s.donationTotal,
    payable: s.payableTotal,
  })),
)

const byProject = computed(() =>
  PROJECTS.map((p) => ({
    name: p.nameZh,
    count: p.donationCount,
    total: p.donationTotal,
  })),
)

const byInvoiceStatus = computed(() => {
  const groups: Record<string, number> = { 待開立: 0, 已開立: 0, 開立失敗: 0, 已作廢: 0, 已折讓: 0 }
  for (const d of DONATIONS) {
    if (!d.invoice) continue
    if (d.invoice.voidStatus === 'voided') groups['已作廢']++
    else if (d.invoice.voidStatus === 'allowance') groups['已折讓']++
    else if (d.invoice.issueStatus === 'issued') groups['已開立']++
    else if (d.invoice.issueStatus === 'failed') groups['開立失敗']++
    else groups['待開立']++
  }
  return groups
})

function exportAggregate(name: string) {
  ElMessage.info(`已下載「${name}」彙總報表（mockup，不含個資，不需要二次確認）`)
}

const exportDialogVisible = ref(false)
const purposeNote = ref('')

function openExportDetail() {
  purposeNote.value = ''
  exportDialogVisible.value = true
}

function confirmExportDetail() {
  if (!purposeNote.value.trim()) {
    ElMessage.warning('請填寫用途備註')
    return
  }
  appendAuditLog({
    adminUsername: currentUser.value.username,
    action: 'export_personal_data',
    targetType: 'donation_list_export',
    targetLabel: null,
    changeSummary: `匯出含個資之捐款明細共 ${DONATIONS.length} 筆`,
    purposeNote: purposeNote.value.trim(),
    sourceIp: '203.0.113.99',
  })
  ElMessage.success('已下載逐筆明細（本次匯出已記錄帳號、時間與用途說明）')
  exportDialogVisible.value = false
}
</script>

<template>
  <div>
    <PageHeader title="捐款報表" frontend-unit="（無對應前台頁面，內部報表）" />

    <el-tabs v-model="activeReport">
      <el-tab-pane label="捐款總覽" name="overview">
        <div class="report-overview">
          <div class="report-overview__card">
            <p class="report-overview__label">已完成捐款筆數</p>
            <p class="report-overview__value">{{ overview.count }}</p>
          </div>
          <div class="report-overview__card">
            <p class="report-overview__label">捐款總額</p>
            <p class="report-overview__value">{{ formatMoney(overview.total) }}</p>
          </div>
          <div class="report-overview__card">
            <p class="report-overview__label">店家回饋金合計</p>
            <p class="report-overview__value">{{ formatMoney(overview.storeShare) }}</p>
          </div>
          <div class="report-overview__card">
            <p class="report-overview__label">項目撥付金合計</p>
            <p class="report-overview__value">{{ formatMoney(overview.projectShare) }}</p>
          </div>
          <div class="report-overview__card">
            <p class="report-overview__label">協會留存合計</p>
            <p class="report-overview__value">{{ formatMoney(overview.associationShare) }}</p>
          </div>
        </div>
        <p class="report-view__hint">只計 `已完成` 狀態，已建立／處理中／付款失敗／已逾時不計入金額統計</p>
      </el-tab-pane>

      <el-tab-pane label="依店家" name="by-store">
        <div class="report-view__toolbar">
          <el-button size="small" @click="exportAggregate('依店家')">匯出</el-button>
        </div>
        <el-table v-if="!isMobile" :data="byStore" style="width: 100%">
          <el-table-column prop="name" label="店家" min-width="160" />
          <el-table-column prop="count" label="筆數" width="100" />
          <el-table-column label="捐款總額" width="140">
            <template #default="{ row }">{{ formatMoney(row.total) }}</template>
          </el-table-column>
          <el-table-column label="應付回饋金" width="140">
            <template #default="{ row }">{{ formatMoney(row.payable) }}</template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :items="byStore">
          <template #default="{ item }">
            <p class="report-view__card-title">{{ item.name }}</p>
            <p class="report-view__card-meta">{{ item.count }} 筆・捐款總額 {{ formatMoney(item.total) }}・應付回饋金 {{ formatMoney(item.payable) }}</p>
          </template>
        </MobileCardList>
      </el-tab-pane>

      <el-tab-pane label="依項目" name="by-project">
        <div class="report-view__toolbar">
          <el-button size="small" @click="exportAggregate('依項目')">匯出</el-button>
        </div>
        <el-table v-if="!isMobile" :data="byProject" style="width: 100%">
          <el-table-column prop="name" label="項目" min-width="200" />
          <el-table-column prop="count" label="筆數" width="100" />
          <el-table-column label="捐款總額" width="140">
            <template #default="{ row }">{{ formatMoney(row.total) }}</template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :items="byProject">
          <template #default="{ item }">
            <p class="report-view__card-title">{{ item.name }}</p>
            <p class="report-view__card-meta">{{ item.count }} 筆・捐款總額 {{ formatMoney(item.total) }}</p>
          </template>
        </MobileCardList>
      </el-tab-pane>

      <el-tab-pane label="發票開立狀況" name="invoice-status">
        <div class="report-view__toolbar">
          <el-button size="small" @click="exportAggregate('發票開立狀況')">匯出</el-button>
        </div>
        <el-table :data="Object.entries(byInvoiceStatus).map(([label, count]) => ({ label, count }))" style="width: 100%">
          <el-table-column prop="label" label="狀態" width="140" />
          <el-table-column prop="count" label="筆數" width="100" />
        </el-table>
      </el-tab-pane>

      <el-tab-pane label="逐筆明細" name="detail">
        <div class="report-view__toolbar">
          <el-button size="small" type="primary" @click="openExportDetail">匯出（含個資，需填用途備註）</el-button>
        </div>
        <el-table v-if="!isMobile" :data="DONATIONS" style="width: 100%">
          <el-table-column prop="orderNo" label="單號" width="180" />
          <el-table-column v-if="isDesktop" label="時間" width="150">
            <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
          </el-table-column>
          <el-table-column prop="donorName" label="捐款人" width="140" />
          <el-table-column label="金額" width="110">
            <template #default="{ row }">{{ formatMoney(row.amount) }}</template>
          </el-table-column>
          <el-table-column prop="projectName" label="項目" min-width="160" />
        </el-table>
        <MobileCardList v-else :items="DONATIONS">
          <template #default="{ item }">
            <p class="report-view__card-title">{{ item.orderNo }}・{{ formatMoney(item.amount) }}</p>
            <p class="report-view__card-meta">{{ item.donorName }}・{{ item.projectName }}・{{ formatDateTime(item.createdAt) }}</p>
          </template>
        </MobileCardList>
      </el-tab-pane>
    </el-tabs>

    <el-dialog v-model="exportDialogVisible" title="匯出含個資之捐款明細" width="420px">
      <p class="report-view__notice">本次匯出將記錄您的帳號、時間與用途說明。</p>
      <el-form-item label="用途備註" required>
        <el-input v-model="purposeNote" type="textarea" :rows="3" placeholder="例如：國稅局申報之捐款明細核對需求" />
      </el-form-item>
      <template #footer>
        <el-button @click="exportDialogVisible = false">取消</el-button>
        <el-button type="primary" :disabled="!purposeNote.trim()" @click="confirmExportDetail">確認匯出</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.report-overview {
  display: flex;
  flex-wrap: wrap;
  gap: var(--charity-admin-space-4);
  margin-bottom: var(--charity-admin-space-3);
}

.report-overview__card {
  background: var(--charity-admin-bg-surface);
  border: 1px solid var(--charity-admin-border);
  border-radius: 6px;
  padding: var(--charity-admin-space-4);
  min-width: 160px;
  flex: 1;
}

.report-overview__label {
  margin: 0 0 4px;
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
}

.report-overview__value {
  margin: 0;
  font-size: 20px;
  font-weight: 600;
  color: var(--charity-admin-text-primary);
}

.report-view__hint {
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
}

.report-view__toolbar {
  display: flex;
  justify-content: flex-end;
  margin-bottom: var(--charity-admin-space-3);
}

.report-view__notice {
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
  margin: 0 0 var(--charity-admin-space-3);
}

.report-view__card-title {
  margin: 0;
  font-weight: 600;
  font-size: 13px;
  color: var(--charity-admin-text-primary);
}

.report-view__card-meta {
  margin: var(--charity-admin-space-1) 0 0;
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
}
</style>

<script setup lang="ts">
/** N5 發票與收據管理（docs/22-charity-ui.md §3.7.5） */
import { computed, ref } from 'vue'
import { ElMessage } from 'element-plus'
import PageHeader from '@/components/PageHeader.vue'
import InvoiceStatusTag from '@/components/InvoiceStatusTag.vue'
import DangerConfirmDialog from '@/components/DangerConfirmDialog.vue'
import EmptyState from '@/components/EmptyState.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { DONATIONS, allowanceInvoice, reissueInvoice, voidInvoice, type Donation } from '@/data/fixtures'
import { appendAuditLog } from '@/data/reconciliationAudit'
import { formatDateTime } from '@/utils/format'
import { currentUser, hasPermission } from '@/data/session'
import { useBreakpoint } from '@/composables/useBreakpoint'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
// 平板寬度（768–1023px）表格仍是 el-table，但欄位總寬度會超出可視寬度——CSS display:none
// 隱藏儲存格不會讓 el-table 縮小欄位總寬（它的版面計算是照 el-table-column 的數量與 width
// 參數加總，不看 CSS 有沒有把某個儲存格藏起來），所以次要欄位要用 v-if 整欄不渲染，
// 不能只用 CSS 隱藏（這是本次驗收在平板寬度實測抓到的問題，不是理論假設）。
const isDesktop = computed(() => breakpoint.value === 'desktop')
const invoiced = computed(() => DONATIONS.filter((d) => d.invoice))

const manualDialogVisible = ref(false)
const manualTarget = ref<Donation | null>(null)
const manualNo = ref('')

function openManual(donation: Donation) {
  manualTarget.value = donation
  manualNo.value = donation.invoice?.invoiceNo ?? ''
  manualDialogVisible.value = true
}

function confirmManual() {
  if (!manualTarget.value?.invoice) return
  manualTarget.value.invoice.invoiceNo = manualNo.value
  manualTarget.value.invoice.issueStatus = 'issued'
  ElMessage.success('已手動填入外部號碼')
  manualDialogVisible.value = false
}

function doReissue(donation: Donation) {
  reissueInvoice(donation.orderNo)
  ElMessage.success('已重新開立')
}

const actionDialogVisible = ref(false)
const actionTarget = ref<Donation | null>(null)
const actionKind = ref<'void' | 'allowance'>('void')

function openAction(donation: Donation, kind: 'void' | 'allowance') {
  actionTarget.value = donation
  actionKind.value = kind
  actionDialogVisible.value = true
}

function confirmAction(reason: string) {
  if (!actionTarget.value) return
  if (actionKind.value === 'void') {
    voidInvoice(actionTarget.value.orderNo, reason, currentUser.value.username)
    appendAuditLog({
      adminUsername: currentUser.value.username,
      action: 'void_invoice',
      targetType: 'donation_invoice',
      targetLabel: actionTarget.value.invoice?.invoiceNo ?? actionTarget.value.orderNo,
      changeSummary: `作廢發票，原因：${reason}`,
      purposeNote: null,
      sourceIp: '203.0.113.99',
    })
    ElMessage.success('已作廢')
  } else {
    allowanceInvoice(actionTarget.value.orderNo, reason, currentUser.value.username)
    appendAuditLog({
      adminUsername: currentUser.value.username,
      action: 'allowance_invoice',
      targetType: 'donation_invoice',
      targetLabel: actionTarget.value.invoice?.invoiceNo ?? actionTarget.value.orderNo,
      changeSummary: `折讓發票，原因：${reason}`,
      purposeNote: null,
      sourceIp: '203.0.113.99',
    })
    ElMessage.success('已折讓')
  }
}

function exportCsv() {
  ElMessage.info('mockup 尚未接產檔服務，正式環境會下載 CSV（UTF-8 with BOM）')
}
</script>

<template>
  <div>
    <PageHeader title="發票與收據管理" frontend-unit="結果頁（發票／收據）">
      <template #actions>
        <el-button v-if="hasPermission('n5.donation_invoice.view')" @click="exportCsv">匯出 CSV</el-button>
      </template>
    </PageHeader>

    <EmptyState v-if="invoiced.length === 0" text="目前沒有發票或收據紀錄" />

    <el-table v-else-if="!isMobile" :data="invoiced" style="width: 100%">
      <el-table-column prop="orderNo" label="捐款單號" width="180" />
      <el-table-column label="憑證類型" width="100">
        <template #default="{ row }: { row: Donation }">{{ row.invoice?.invoiceType === 'b2c_invoice' ? '電子發票' : '捐贈收據' }}</template>
      </el-table-column>
      <el-table-column v-if="isDesktop" label="憑證號碼" width="170">
        <template #default="{ row }: { row: Donation }">{{ row.invoice?.invoiceNo ?? '—' }}</template>
      </el-table-column>
      <el-table-column v-if="isDesktop" label="開立時間" width="150">
        <template #default="{ row }: { row: Donation }">{{ formatDateTime(row.invoice?.issuedAt) }}</template>
      </el-table-column>
      <el-table-column label="狀態" width="100">
        <template #default="{ row }: { row: Donation }">
          <InvoiceStatusTag v-if="row.invoice" :issue-status="row.invoice.issueStatus" :void-status="row.invoice.voidStatus" />
        </template>
      </el-table-column>
      <el-table-column v-if="isDesktop" prop="projectName" label="項目" min-width="160" />
      <el-table-column label="操作" width="260" fixed="right">
        <template #default="{ row }: { row: Donation }">
          <el-button v-if="row.invoice?.issueStatus === 'failed'" size="small" text type="primary" @click="doReissue(row)">重新開立</el-button>
          <el-button size="small" text type="primary" @click="openManual(row)">手動填入外部號碼</el-button>
          <el-button v-if="row.invoice?.voidStatus === 'none'" size="small" text @click="openAction(row, 'void')">作廢</el-button>
          <el-button v-if="row.invoice?.voidStatus === 'none'" size="small" text @click="openAction(row, 'allowance')">折讓</el-button>
        </template>
      </el-table-column>
    </el-table>

    <MobileCardList v-else :items="invoiced">
      <template #default="{ item }: { item: Donation }">
        <div class="invoice-list__card-head">
          <span class="invoice-list__card-order">{{ item.orderNo }}</span>
          <InvoiceStatusTag v-if="item.invoice" :issue-status="item.invoice.issueStatus" :void-status="item.invoice.voidStatus" />
        </div>
        <p class="invoice-list__card-meta">
          {{ item.invoice?.invoiceType === 'b2c_invoice' ? '電子發票' : '捐贈收據' }}・{{ item.invoice?.invoiceNo ?? '（尚無號碼）' }}<br>
          {{ item.projectName }}・開立時間 {{ formatDateTime(item.invoice?.issuedAt) }}
        </p>
        <div class="invoice-list__card-actions">
          <el-button v-if="item.invoice?.issueStatus === 'failed'" size="small" text type="primary" @click="doReissue(item)">重新開立</el-button>
          <el-button size="small" text type="primary" @click="openManual(item)">手動填入外部號碼</el-button>
          <el-button v-if="item.invoice?.voidStatus === 'none'" size="small" text @click="openAction(item, 'void')">作廢</el-button>
          <el-button v-if="item.invoice?.voidStatus === 'none'" size="small" text @click="openAction(item, 'allowance')">折讓</el-button>
        </div>
      </template>
    </MobileCardList>

    <el-dialog v-model="manualDialogVisible" title="手動填入外部號碼" width="360px">
      <el-form-item label="憑證號碼">
        <el-input v-model="manualNo" />
      </el-form-item>
      <template #footer>
        <el-button @click="manualDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="confirmManual">確認</el-button>
      </template>
    </el-dialog>

    <DangerConfirmDialog
      v-model="actionDialogVisible"
      :title="actionKind === 'void' ? '作廢發票' : '折讓發票'"
      reason-label="原因"
      :confirm-text="actionKind === 'void' ? '確認作廢' : '確認折讓'"
      audit-notice="此操作將寫入稽核紀錄"
      @confirm="confirmAction"
    />
  </div>
</template>

<style scoped>
.invoice-list__card-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: var(--charity-admin-space-2);
}

.invoice-list__card-order {
  font-weight: 600;
  color: var(--charity-admin-text-primary);
}

.invoice-list__card-meta {
  margin: var(--charity-admin-space-2) 0;
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
  line-height: 1.6;
}

.invoice-list__card-actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--charity-admin-space-1);
  padding-top: var(--charity-admin-space-2);
  border-top: 1px solid var(--charity-admin-border);
}
</style>

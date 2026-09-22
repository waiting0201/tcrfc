<script setup lang="ts">
/**
 * N4 回饋金結算（docs/22-charity-ui.md §3.7.4）：店家／項目結算用 el-tabs 分開，
 * 不合併成一張表——規劃書明訂這是「兩個不同對象的撥付」（docs/10 §4）。
 */
import { computed, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import PageHeader from '@/components/PageHeader.vue'
import SettlementStatusTag from '@/components/SettlementStatusTag.vue'
import EmptyState from '@/components/EmptyState.vue'
import { SETTLEMENTS, executeSettlement, registerSettlementPayment, type Settlement } from '@/data/fixtures'
import { formatDate, formatMoney } from '@/utils/format'
import { hasPermission } from '@/data/session'

const activeTab = ref<'store' | 'project'>('store')

const storeSettlements = computed(() => SETTLEMENTS.filter((s) => s.payeeType === 'store'))
const projectSettlements = computed(() => SETTLEMENTS.filter((s) => s.payeeType === 'project'))

function doExecute(settlement: Settlement) {
  executeSettlement(settlement.id)
  ElMessage.success(`已為「${settlement.payeeName}」執行結算`)
}

const payDialogVisible = ref(false)
const payTarget = ref<Settlement | null>(null)
const payForm = reactive({ remittedOn: '', remitMethod: '銀行轉帳', remitNote: '' })

function openPayDialog(settlement: Settlement) {
  payTarget.value = settlement
  payForm.remittedOn = ''
  payForm.remitMethod = '銀行轉帳'
  payForm.remitNote = ''
  payDialogVisible.value = true
}

function confirmPay() {
  if (!payTarget.value || !payForm.remittedOn) {
    ElMessage.warning('請填寫匯款日期')
    return
  }
  registerSettlementPayment(payTarget.value.id, { ...payForm })
  ElMessage.success('已登記付款，此筆結算單已鎖定不得再編輯金額')
  payDialogVisible.value = false
}

function downloadReport(settlement: Settlement, kind: string) {
  ElMessage.info(`mockup 尚未接產檔服務，正式環境會下載「${settlement.payeeName}」${settlement.periodStart}～${settlement.periodEnd} 的${kind}`)
}
</script>

<template>
  <div>
    <PageHeader title="回饋金結算" frontend-unit="（無對應前台頁面，內部帳務作業）" />

    <el-tabs v-model="activeTab">
      <el-tab-pane label="店家結算" name="store">
        <EmptyState v-if="storeSettlements.length === 0" text="目前沒有店家結算單" />
        <div v-for="s in storeSettlements" :key="s.id" class="settlement-card">
          <div class="settlement-card__head">
            <div>
              <span class="settlement-card__name">{{ s.payeeName }}</span>
              <span class="settlement-card__period">期間：{{ formatDate(s.periodStart) }} ～ {{ formatDate(s.periodEnd) }}</span>
            </div>
            <SettlementStatusTag :status="s.status" />
          </div>
          <div class="settlement-card__lines-wrap">
            <table class="settlement-card__lines">
              <thead>
                <tr>
                  <th>捐款單</th>
                  <th>金額</th>
                  <th>分潤金額</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="line in s.lines" :key="line.donationKey" :class="{ 'settlement-card__line--clawback': line.isClawback }">
                  <td>{{ line.orderNo }}</td>
                  <td>{{ formatMoney(line.amount) }}</td>
                  <td>
                    {{ formatMoney(line.shareAmount) }}
                    <span v-if="line.isClawback" class="settlement-card__clawback-note">沖回原捐款單號：{{ line.orderNo }}</span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
          <div class="settlement-card__foot">
            <span>{{ s.donationCount }} 筆・捐款總額 {{ formatMoney(s.donationTotal) }}・應付金額 {{ formatMoney(s.payableAmount) }}</span>
            <div class="settlement-card__actions">
              <el-button size="small" @click="downloadReport(s, 'PDF 對帳單')">下載對帳單（PDF）</el-button>
              <el-button size="small" @click="downloadReport(s, 'CSV')">下載 CSV</el-button>
              <el-button v-if="hasPermission('n4.settlement.execute') && s.status === 'pending'" size="small" type="primary" @click="doExecute(s)">
                執行結算
              </el-button>
              <el-button v-if="hasPermission('n4.settlement.execute') && s.status === 'settled'" size="small" type="primary" @click="openPayDialog(s)">
                登記已付款
              </el-button>
            </div>
          </div>
          <p v-if="s.status === 'paid'" class="settlement-card__paid-note">
            {{ formatDate(s.remittedOn) }}・{{ s.remitMethod }}・{{ s.remitNote }}
          </p>
        </div>
      </el-tab-pane>

      <el-tab-pane label="項目結算" name="project">
        <EmptyState v-if="projectSettlements.length === 0" text="目前沒有項目結算單" />
        <div v-for="s in projectSettlements" :key="s.id" class="settlement-card">
          <div class="settlement-card__head">
            <div>
              <span class="settlement-card__name">{{ s.payeeName }}</span>
              <span class="settlement-card__period">期間：{{ formatDate(s.periodStart) }} ～ {{ formatDate(s.periodEnd) }}</span>
            </div>
            <SettlementStatusTag :status="s.status" />
          </div>
          <div class="settlement-card__lines-wrap">
            <table class="settlement-card__lines">
              <thead>
                <tr>
                  <th>捐款單</th>
                  <th>金額</th>
                  <th>分潤金額</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="line in s.lines" :key="line.donationKey" :class="{ 'settlement-card__line--clawback': line.isClawback }">
                  <td>{{ line.orderNo }}</td>
                  <td>{{ formatMoney(line.amount) }}</td>
                  <td>
                    {{ formatMoney(line.shareAmount) }}
                    <span v-if="line.isClawback" class="settlement-card__clawback-note">沖回原捐款單號：{{ line.orderNo }}</span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
          <div class="settlement-card__foot">
            <span>{{ s.donationCount }} 筆・捐款總額 {{ formatMoney(s.donationTotal) }}・應付金額 {{ formatMoney(s.payableAmount) }}</span>
            <div class="settlement-card__actions">
              <el-button size="small" @click="downloadReport(s, 'PDF 對帳單')">下載對帳單（PDF）</el-button>
              <el-button size="small" @click="downloadReport(s, 'CSV')">下載 CSV</el-button>
              <el-button v-if="hasPermission('n4.settlement.execute') && s.status === 'pending'" size="small" type="primary" @click="doExecute(s)">
                執行結算
              </el-button>
              <el-button v-if="hasPermission('n4.settlement.execute') && s.status === 'settled'" size="small" type="primary" @click="openPayDialog(s)">
                登記已付款
              </el-button>
            </div>
          </div>
          <p v-if="s.status === 'paid'" class="settlement-card__paid-note">
            {{ formatDate(s.remittedOn) }}・{{ s.remitMethod }}・{{ s.remitNote }}
          </p>
        </div>
      </el-tab-pane>
    </el-tabs>

    <el-dialog v-model="payDialogVisible" title="登記已付款" width="420px">
      <p class="settlement-pay__note">只登記狀態，不經手金流。提交後即鎖定此筆結算單不得再編輯金額。</p>
      <el-form label-position="top">
        <el-form-item label="匯款日期" required>
          <el-date-picker v-model="payForm.remittedOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" />
        </el-form-item>
        <el-form-item label="匯款方式">
          <el-input v-model="payForm.remitMethod" />
        </el-form-item>
        <el-form-item label="備註">
          <el-input v-model="payForm.remitNote" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="payDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="confirmPay">確認登記</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.settlement-card {
  background: var(--charity-admin-bg-surface);
  border: 1px solid var(--charity-admin-border);
  border-radius: 6px;
  padding: var(--charity-admin-space-4);
  margin-bottom: var(--charity-admin-space-4);
}

.settlement-card__head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: var(--charity-admin-space-3);
}

.settlement-card__name {
  font-weight: 600;
  color: var(--charity-admin-text-primary);
  margin-right: var(--charity-admin-space-3);
}

.settlement-card__period {
  font-size: 13px;
  color: var(--charity-admin-text-tertiary);
}

/* CJK 表格在窄螢幕上如果只給 width:100%，儲存格會被硬壓到擠成一團、文字重疊換行——
   要給外層一個可以橫向捲動的容器＋內層表格一個明確的 min-width，而不是讓它硬縮 */
.settlement-card__lines-wrap {
  overflow-x: auto;
  margin-bottom: var(--charity-admin-space-3);
}

.settlement-card__lines {
  width: 100%;
  min-width: 420px;
  border-collapse: collapse;
  font-size: 13px;
}

.settlement-card__lines th {
  text-align: left;
  color: var(--charity-admin-text-tertiary);
  font-weight: 500;
  padding: 4px 8px;
  border-bottom: 1px solid var(--charity-admin-border);
}

.settlement-card__lines td {
  padding: 4px 8px;
  border-bottom: 1px solid var(--charity-admin-border);
  color: var(--charity-admin-text-primary);
}

.settlement-card__line--clawback td {
  color: var(--charity-danger-text);
}

.settlement-card__clawback-note {
  margin-left: 8px;
  font-size: 12px;
  color: var(--charity-danger-text);
}

.settlement-card__foot {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  align-items: center;
  gap: var(--charity-admin-space-2);
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
}

.settlement-card__actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--charity-admin-space-2);
}

.settlement-card__paid-note {
  margin: var(--charity-admin-space-2) 0 0;
  font-size: 12px;
  color: var(--charity-success-text);
}

.settlement-pay__note {
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
  margin: 0 0 var(--charity-admin-space-3);
}
</style>

<script setup lang="ts">
/** N7 站台設定（docs/22-charity-ui.md §3.7.7）＋ 稽核紀錄查詢（§3.9） */
import { computed, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { SETTINGS_I18N, SETTINGS_PLAIN, EMAIL_TEMPLATES } from '@/data/fixtures'
import { AUDIT_LOGS } from '@/data/reconciliationAudit'
import { formatDateTime } from '@/utils/format'
import { hasPermission } from '@/data/session'
import { useBreakpoint } from '@/composables/useBreakpoint'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
// 平板寬度（768–1023px）表格仍是 el-table，但欄位總寬度會超出可視寬度——CSS display:none
// 隱藏儲存格不會讓 el-table 縮小欄位總寬（它的版面計算是照 el-table-column 的數量與 width
// 參數加總，不看 CSS 有沒有把某個儲存格藏起來），所以次要欄位要用 v-if 整欄不渲染，
// 不能只用 CSS 隱藏（這是本次驗收在平板寬度實測抓到的問題，不是理論假設）。
const isDesktop = computed(() => breakpoint.value === 'desktop')

const activeTab = ref('copy')

const copyForm = reactive(
  Object.fromEntries(SETTINGS_I18N.map((s) => [s.key, { zh: s.zh, en: s.en }])),
) as Record<string, { zh: string; en: string }>

const COPY_LABEL: Record<string, string> = {
  'donation.home_intro': '首頁說明文案',
  'donation.thank_you_message_template': '感謝語樣板',
  'donation.notice': '捐款須知',
  'donation.privacy_policy': '隱私權政策',
}

const TEMPLATE_LABEL: Record<string, string> = {
  donation_thanks: '感謝您的捐款',
  invoice_issued: '憑證已開立通知',
  invoice_failed: '發票開立失敗通知',
  refund_notice: '退款通知',
}

const activeEmailTemplate = ref(EMAIL_TEMPLATES[0]?.code ?? '')

const amountLimits = reactive({
  min: Number(SETTINGS_PLAIN.get('donation.default_min_amount') ?? 100),
  max: Number(SETTINGS_PLAIN.get('donation.default_max_amount') ?? 1000000),
})

const creditListOpen = ref(SETTINGS_PLAIN.get('donation.credit_list_display_rule') === 'named_unless_anonymous')

const environment = ref<'test' | 'production'>('test')

function handleSaveCopy() {
  ElMessage.success('已儲存站台文案（mockup，未實際寫入）')
}

function handleSaveTemplates() {
  ElMessage.success('已儲存系統信樣板（mockup，未實際寫入）')
}

function handleSaveLimits() {
  ElMessage.success('已儲存金額上下限預設值（mockup，未實際寫入）')
}

const auditFilters = reactive({ action: '' })
const filteredAuditLogs = computed(() =>
  auditFilters.action ? AUDIT_LOGS.filter((l) => l.action === auditFilters.action) : AUDIT_LOGS,
)

const ACTION_LABEL: Record<string, string> = {
  refund: '退款',
  update_share_pct: '調整分潤比例',
  export_personal_data: '匯出含個資明細',
  void_invoice: '作廢發票',
  allowance_invoice: '折讓發票',
  resolve_discrepancy: '標記對帳差異已處理',
}
</script>

<template>
  <div>
    <PageHeader title="站台設定" frontend-unit="站台文案、系統信、稽核紀錄" />

    <el-tabs v-model="activeTab">
      <el-tab-pane label="站台文案" name="copy">
        <el-form label-position="top" class="site-setting__form">
          <el-form-item v-for="s in SETTINGS_I18N" :key="s.key" :label="COPY_LABEL[s.key] ?? s.key">
            <el-tabs class="site-setting__lang-tabs">
              <el-tab-pane label="中文">
                <el-input v-model="copyForm[s.key].zh" type="textarea" :rows="3" />
              </el-tab-pane>
              <el-tab-pane label="英文">
                <el-input v-model="copyForm[s.key].en" type="textarea" :rows="3" />
              </el-tab-pane>
            </el-tabs>
          </el-form-item>
          <el-button v-if="hasPermission('n7.setting.manage')" type="primary" @click="handleSaveCopy">儲存</el-button>
        </el-form>
      </el-tab-pane>

      <el-tab-pane label="系統信樣板" name="templates">
        <el-tabs v-model="activeEmailTemplate" tab-position="left" class="site-setting__template-tabs">
          <el-tab-pane v-for="t in EMAIL_TEMPLATES" :key="t.code" :label="TEMPLATE_LABEL[t.code] ?? t.code" :name="t.code">
            <el-form label-position="top">
              <el-tabs>
                <el-tab-pane label="中文">
                  <el-form-item label="主旨">
                    <el-input v-model="t.subjectZh" />
                  </el-form-item>
                  <el-form-item label="內文">
                    <el-input v-model="t.bodyZh" type="textarea" :rows="4" />
                  </el-form-item>
                </el-tab-pane>
                <el-tab-pane label="英文">
                  <el-form-item label="主旨">
                    <el-input v-model="t.subjectEn" />
                  </el-form-item>
                  <el-form-item label="內文">
                    <el-input v-model="t.bodyEn" type="textarea" :rows="4" />
                  </el-form-item>
                </el-tab-pane>
              </el-tabs>
              <p class="site-setting__var-hint">變數請用人類可讀的佔位符，例如「訂單編號」，不要顯示程式變數名稱。</p>
            </el-form>
          </el-tab-pane>
        </el-tabs>
        <el-button v-if="hasPermission('n7.setting.manage')" type="primary" @click="handleSaveTemplates">儲存</el-button>
      </el-tab-pane>

      <el-tab-pane label="金額上下限" name="limits">
        <el-form label-position="top" class="site-setting__form">
          <el-form-item label="預設最低金額">
            <el-input-number v-model="amountLimits.min" :min="1" />
          </el-form-item>
          <el-form-item label="預設最高金額">
            <el-input-number v-model="amountLimits.max" :min="amountLimits.min" />
          </el-form-item>
          <el-button v-if="hasPermission('n7.setting.manage')" type="primary" @click="handleSaveLimits">儲存</el-button>
        </el-form>
      </el-tab-pane>

      <el-tab-pane label="徵信名單" name="credit-list">
        <el-form label-position="top" class="site-setting__form">
          <el-form-item label="開放徵信名單頁面">
            <el-switch v-model="creditListOpen" />
          </el-form-item>
          <p class="site-setting__hint">關閉後，前台徵信名單網址會顯示「本頁功能暫未開放」，不是 404（連結可能已被分享出去）。</p>
        </el-form>
      </el-tab-pane>

      <el-tab-pane label="環境切換" name="environment">
        <el-form label-position="top" class="site-setting__form">
          <el-form-item label="金流環境">
            <el-radio-group v-model="environment">
              <el-radio value="test">測試</el-radio>
              <el-radio value="production">正式</el-radio>
            </el-radio-group>
          </el-form-item>
          <el-form-item label="LINE Pay 金鑰">
            <el-input model-value="••••••••" disabled style="max-width: 240px" />
            <el-button v-if="hasPermission('n7.payment_channel.manage')" size="small" class="site-setting__key-btn">更換</el-button>
          </el-form-item>
          <p class="site-setting__hint">🔴 本輪 mockup 不接 LINE Pay，此頁只示範金鑰遮罩與更換入口的版面。</p>
        </el-form>
      </el-tab-pane>

      <el-tab-pane label="操作紀錄" name="audit">
        <p class="site-setting__hint">
          稽核紀錄唯讀，append-only，不可編輯或刪除任何一列。
        </p>
        <div class="site-setting__audit-filter">
          <el-select v-model="auditFilters.action" placeholder="動作類型" clearable style="width: 200px">
            <el-option v-for="(label, code) in ACTION_LABEL" :key="code" :label="label" :value="code" />
          </el-select>
        </div>
        <EmptyState v-if="AUDIT_LOGS.length === 0" text="沒有稽核紀錄" />
        <el-table v-else-if="!isMobile" :data="filteredAuditLogs" style="width: 100%">
          <el-table-column label="時間" width="150">
            <template #default="{ row }">{{ formatDateTime(row.occurredAt) }}</template>
          </el-table-column>
          <el-table-column v-if="isDesktop" prop="adminUsername" label="操作者" width="180" />
          <el-table-column label="動作" width="150">
            <template #default="{ row }">{{ ACTION_LABEL[row.action] ?? row.action }}</template>
          </el-table-column>
          <el-table-column v-if="isDesktop" label="對象" width="200">
            <template #default="{ row }">{{ row.targetLabel ?? '—' }}</template>
          </el-table-column>
          <el-table-column prop="changeSummary" label="變更摘要" min-width="240" />
          <el-table-column v-if="isDesktop" label="用途備註" width="180">
            <template #default="{ row }">{{ row.purposeNote ?? '—' }}</template>
          </el-table-column>
          <el-table-column v-if="isDesktop" prop="sourceIp" label="來源 IP" width="130" />
        </el-table>
        <MobileCardList v-else :items="filteredAuditLogs">
          <template #default="{ item }">
            <p class="site-setting__audit-card-title">{{ ACTION_LABEL[item.action] ?? item.action }}</p>
            <p class="site-setting__audit-card-meta">
              {{ formatDateTime(item.occurredAt) }}・{{ item.adminUsername }}<br>
              對象：{{ item.targetLabel ?? '—' }}<br>
              {{ item.changeSummary }}
            </p>
          </template>
        </MobileCardList>
      </el-tab-pane>
    </el-tabs>
  </div>
</template>

<style scoped>
.site-setting__form {
  max-width: 640px;
}

.site-setting__lang-tabs :deep(.el-tabs__content) {
  padding-top: 4px;
}

.site-setting__template-tabs {
  min-height: 320px;
}

.site-setting__var-hint {
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
  margin: 0;
}

.site-setting__hint {
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
}

.site-setting__key-btn {
  margin-left: var(--charity-admin-space-2);
}

.site-setting__audit-filter {
  margin-bottom: var(--charity-admin-space-3);
}

.site-setting__audit-card-title {
  margin: 0;
  font-weight: 600;
  font-size: 13px;
  color: var(--charity-admin-text-primary);
}

.site-setting__audit-card-meta {
  margin: var(--charity-admin-space-1) 0 0;
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
  line-height: 1.6;
}
</style>

<script setup lang="ts">
/**
 * 操作紀錄（僅系統管理員，唯讀）：退款、分潤設定、個資檢視與匯出、結算、憑證作廢等敏感操作都會留下紀錄。
 * 紀錄只能查詢，不能修改或刪除；摘要與備註裡不會有捐款人的個資明文。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import EmptyState from '@/components/EmptyState.vue'
import ErrorState from '@/components/ErrorState.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { listAuditActions, queryAuditLogs, type AuditLogItem } from '@/api/audit'
import { AdminApiError } from '@/api/http'
import { formatTaipei } from '@/utils/format'
import { useBreakpoint } from '@/composables/useBreakpoint'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const isDesktop = computed(() => breakpoint.value === 'desktop')

// 對象類型的篩選選項（與後端回傳的 targetTypeLabel 同名）。
const TARGET_TYPES = [
  { value: 'donation', label: '捐款' },
  { value: 'donation_store', label: '捐款合作店家' },
  { value: 'donation_project', label: '捐款項目' },
  { value: 'settlement', label: '結算單' },
  { value: 'reconciliation_run', label: '對帳批次' },
  { value: 'reconciliation_discrepancy', label: '對帳差異' },
  { value: 'donation_invoice', label: '憑證' },
  { value: 'setting', label: '站台設定' },
  { value: 'email_template', label: '系統信樣板' },
  { value: 'payment_channel', label: '金流與發票憑證' },
]

const filters = reactive({ range: null as [string, string] | null, action: '', targetType: '', keyword: '' })
const actionOptions = ref<{ action: string; label: string }[]>([])
const items = ref<AuditLogItem[]>([])
const totalCount = ref(0)
const page = ref(1)
const pageSize = 50
const loading = ref(false)
const loadError = ref('')

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    const r = await queryAuditLogs(
      {
        from: filters.range?.[0],
        to: filters.range?.[1],
        action: filters.action || undefined,
        targetType: filters.targetType || undefined,
        keyword: filters.keyword.trim() || undefined,
      },
      page.value,
      pageSize,
    )
    items.value = r.items
    totalCount.value = r.totalCount
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

function reset() {
  Object.assign(filters, { range: null, action: '', targetType: '', keyword: '' })
  search()
}

watch(page, () => { void load() })

onMounted(async () => {
  void load()
  try { actionOptions.value = await listAuditActions() } catch { /* 篩選選項取不到時，仍可用其他條件查詢 */ }
})

const targetText = (r: AuditLogItem) => [r.targetTypeLabel, r.targetLabel].filter(Boolean).join('：')
</script>

<template>
  <div>
    <div class="audit__filters">
      <el-date-picker v-model="filters.range" type="daterange" value-format="YYYY-MM-DD" range-separator="至" start-placeholder="開始日期" end-placeholder="結束日期" style="max-width: 280px" />
      <el-select v-model="filters.action" placeholder="動作" clearable filterable style="width: 190px">
        <el-option v-for="a in actionOptions" :key="a.action" :value="a.action" :label="a.label" />
      </el-select>
      <el-select v-model="filters.targetType" placeholder="對象類型" clearable style="width: 170px">
        <el-option v-for="t in TARGET_TYPES" :key="t.value" :value="t.value" :label="t.label" />
      </el-select>
      <el-input v-model="filters.keyword" placeholder="關鍵字（摘要或備註）" clearable style="width: 200px" @keyup.enter="search" />
      <el-button type="primary" @click="search">查詢</el-button>
      <el-button @click="reset">清除條件</el-button>
    </div>
    <p class="audit__hint">操作紀錄只能查詢，不能修改或刪除。摘要與備註裡不會出現捐款人的個人資料。</p>

    <ErrorState v-if="loadError" :text="loadError" @retry="load" />
    <EmptyState v-else-if="!loading && items.length === 0" text="沒有符合條件的操作紀錄" />
    <template v-else>
      <el-table v-if="!isMobile" v-loading="loading" :data="items" style="width: 100%">
        <el-table-column label="時間" width="140">
          <template #default="{ row }: { row: AuditLogItem }">{{ formatTaipei(row.occurredAt) }}</template>
        </el-table-column>
        <el-table-column label="操作者" width="100">
          <template #default="{ row }: { row: AuditLogItem }">{{ row.adminName ?? '—' }}</template>
        </el-table-column>
        <el-table-column label="動作" min-width="120" prop="actionLabel" />
        <el-table-column label="對象" min-width="140">
          <template #default="{ row }: { row: AuditLogItem }">{{ targetText(row) || '—' }}</template>
        </el-table-column>
        <el-table-column label="變更摘要" min-width="160">
          <template #default="{ row }: { row: AuditLogItem }">{{ row.changeSummary ?? '—' }}</template>
        </el-table-column>
        <el-table-column v-if="isDesktop" label="用途備註" min-width="160">
          <template #default="{ row }: { row: AuditLogItem }">{{ row.purposeNote ?? '—' }}</template>
        </el-table-column>
        <el-table-column v-if="isDesktop" label="來源位址" width="130">
          <template #default="{ row }: { row: AuditLogItem }">{{ row.sourceIp ?? '—' }}</template>
        </el-table-column>
      </el-table>

      <MobileCardList v-else :items="items">
        <template #default="{ item }: { item: AuditLogItem }">
          <strong>{{ item.actionLabel }}</strong>
          <p class="audit__card-meta">
            {{ formatTaipei(item.occurredAt) }}・{{ item.adminName ?? '—' }}<br>
            {{ targetText(item) }}<br>
            <template v-if="item.changeSummary">{{ item.changeSummary }}<br></template>
            <template v-if="item.purposeNote">用途備註：{{ item.purposeNote }}</template>
          </p>
        </template>
      </MobileCardList>
      <el-pagination v-if="totalCount > pageSize" v-model:current-page="page" class="audit__pager" layout="prev, pager, next" :page-size="pageSize" :total="totalCount" />
    </template>
  </div>
</template>

<style scoped>
.audit__filters {
  display: flex;
  flex-wrap: wrap;
  gap: var(--charity-admin-space-2);
}

.audit__hint {
  margin: var(--charity-admin-space-2) 0 var(--charity-admin-space-4);
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
  line-height: 1.7;
}

.audit__pager {
  margin-top: var(--charity-admin-space-4);
  justify-content: center;
}

.audit__card-meta {
  margin: var(--charity-admin-space-2) 0 0;
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
  line-height: 1.8;
  overflow-wrap: anywhere;
}
</style>

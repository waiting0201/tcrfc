<script setup lang="ts">
/**
 * G2 詢問收件匣——列表頁（對應主站規劃書 §4.7 G2；apps/api/README.md「S1-10」）。
 * 依表單類型分頁（9 個固定表單＋「全部」），只顯示這個角色看得到的分頁——後端仍然是真正的邊界，
 * 這裡只是不讓看不到的角色面對一堆點了也是空清單的分頁（見 `useFormsPermissions.ts`）。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { activeClubId } from '@/auth/clubAccess'
import { listAdminEnquiries, downloadAdminEnquiriesCsv, type AdminEnquiryListItemDto } from '@/api/adminEnquiries'
import { AdminApiError } from '@/api/http'
import { useFormsPermissions } from '@/composables/useFormsPermissions'
import { FORM_CODE_ORDER, formCodeLabel, ENQUIRY_STATUS_ORDER, enquiryStatusTagType, type FormCode } from '@/types/forms'
import { formatDateTime } from '@/utils/formatDateTime'

const router = useRouter()
const club = computed(() => activeClubId.value)
const { canViewInbox, canExportInbox, visibleFormCodes } = useFormsPermissions()

const visibleTabs = computed<{ label: string; value: string }[]>(() => {
  const codes: FormCode[] = visibleFormCodes.value === null ? FORM_CODE_ORDER : FORM_CODE_ORDER.filter((c) => visibleFormCodes.value!.has(c))
  return [{ label: '全部', value: '' }, ...codes.map((c) => ({ label: formCodeLabel(c), value: c }))]
})

const activeTab = ref('')
const filters = reactive({ status: '', keyword: '', dateRange: [] as string[] })
const page = ref(1)
const pageSize = ref(20)

const enquiries = ref<AdminEnquiryListItemDto[]>([])
const totalCount = ref(0)
const loading = ref(true)
const loadError = ref<string | null>(null)
const exporting = ref(false)

function currentParams() {
  return {
    formCode: activeTab.value || undefined,
    status: filters.status || undefined,
    keyword: filters.keyword.trim() || undefined,
    dateFrom: filters.dateRange?.[0] || undefined,
    dateTo: filters.dateRange?.[1] || undefined,
    page: page.value,
    pageSize: pageSize.value,
  }
}

async function loadEnquiries() {
  loading.value = true
  loadError.value = null
  try {
    const result = await listAdminEnquiries(club.value, currentParams())
    enquiries.value = result.items
    totalCount.value = result.totalCount
  } catch (error) {
    enquiries.value = []
    totalCount.value = 0
    loadError.value = error instanceof AdminApiError ? error.message : '詢問清單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

function resetAndLoad() {
  page.value = 1
  loadEnquiries()
}

onMounted(loadEnquiries)
watch(club, () => {
  activeTab.value = ''
  filters.status = ''
  filters.keyword = ''
  filters.dateRange = []
  resetAndLoad()
})
watch(activeTab, resetAndLoad)

function applyFilters() {
  resetAndLoad()
}

function clearFilters() {
  filters.status = ''
  filters.keyword = ''
  filters.dateRange = []
  resetAndLoad()
}

function handlePageChange(next: number) {
  page.value = next
  loadEnquiries()
}

function handleView(row: AdminEnquiryListItemDto) {
  router.push(`/inquiries/inbox/${row.id}/edit`)
}

async function handleExport() {
  exporting.value = true
  try {
    await downloadAdminEnquiriesCsv(club.value, currentParams())
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '匯出失敗，請稍後再試')
  } finally {
    exporting.value = false
  }
}
</script>

<template>
  <div class="enquiry-inbox">
    <PageHeader title="詢問收件匣">
      <template #meta>
        <FrontendUnitBanner module-code="G2" />
      </template>
    </PageHeader>

    <el-tabs v-model="activeTab" class="enquiry-inbox__tabs">
      <el-tab-pane v-for="tab in visibleTabs" :key="tab.value" :label="tab.label" :name="tab.value" />
    </el-tabs>

    <el-card shadow="never" class="enquiry-inbox__filters">
      <div class="enquiry-inbox__filter-row">
        <el-select v-model="filters.status" placeholder="狀態" clearable class="enquiry-inbox__filter-select" @change="applyFilters">
          <el-option v-for="s in ENQUIRY_STATUS_ORDER" :key="s" :label="s" :value="s" />
        </el-select>
        <el-date-picker
          v-model="filters.dateRange"
          type="daterange"
          value-format="YYYY-MM-DD"
          start-placeholder="送出時間起"
          end-placeholder="送出時間迄"
          class="enquiry-inbox__filter-date"
          @change="applyFilters"
        />
        <el-input
          v-model="filters.keyword"
          placeholder="關鍵字（搜尋內容、來源頁面、標籤）"
          clearable
          class="enquiry-inbox__filter-keyword"
          @keyup.enter="applyFilters"
          @clear="applyFilters"
        />
        <el-button type="primary" @click="applyFilters">搜尋</el-button>
        <el-button @click="clearFilters">清除</el-button>
        <div class="enquiry-inbox__filter-placeholder" />
        <el-button v-if="canExportInbox" :loading="exporting" @click="handleExport">匯出 CSV</el-button>
      </div>
    </el-card>

    <el-card v-if="loading" shadow="never">
      <el-skeleton :rows="6" animated />
    </el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError">
        <el-button type="primary" @click="loadEnquiries">重新載入</el-button>
      </el-empty>
    </el-card>
    <el-card v-else-if="!canViewInbox" shadow="never">
      <el-empty description="你的帳號沒有這個模組的檢視權限" />
    </el-card>
    <el-card v-else shadow="never">
      <el-table v-if="enquiries.length > 0" :data="enquiries" row-key="id">
        <el-table-column label="來源表單" width="150">
          <template #default="{ row }">{{ row.formNameZh }}</template>
        </el-table-column>
        <el-table-column label="姓名" width="110">
          <template #default="{ row }">{{ row.applicantName || '—' }}</template>
        </el-table-column>
        <el-table-column label="聯絡方式" min-width="140">
          <template #default="{ row }">{{ row.contactInfo || '—' }}</template>
        </el-table-column>
        <el-table-column label="內容摘要" min-width="200">
          <template #default="{ row }">
            <span class="enquiry-inbox__summary">{{ row.contentSummary || '—' }}</span>
          </template>
        </el-table-column>
        <el-table-column label="來源頁面" min-width="140">
          <template #default="{ row }">{{ row.sourcePath || '—' }}</template>
        </el-table-column>
        <el-table-column label="UTM 來源" width="110">
          <template #default="{ row }">{{ row.utmSource || '—' }}</template>
        </el-table-column>
        <el-table-column label="狀態" width="90">
          <template #default="{ row }">
            <el-tag :type="enquiryStatusTagType(row.status ?? '')" size="small">{{ row.status || '—' }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="送出時間" width="160">
          <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="90" fixed="right">
          <template #default="{ row }">
            <el-button size="small" text type="primary" @click="handleView(row)">處理</el-button>
          </template>
        </el-table-column>
      </el-table>
      <el-empty v-else description="找不到符合條件的詢問" />

      <el-pagination
        v-if="totalCount > 0"
        class="enquiry-inbox__pagination"
        background
        layout="total, prev, pager, next"
        :total="totalCount"
        :page-size="pageSize"
        :current-page="page"
        @current-change="handlePageChange"
      />
    </el-card>
  </div>
</template>

<style scoped>
.enquiry-inbox__tabs {
  margin-bottom: 4px;
}

.enquiry-inbox__filters {
  margin-bottom: 12px;
}

.enquiry-inbox__filter-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}

.enquiry-inbox__filter-select {
  width: 130px;
}

.enquiry-inbox__filter-date {
  width: 260px;
}

.enquiry-inbox__filter-keyword {
  width: 240px;
}

.enquiry-inbox__filter-placeholder {
  flex: 1;
}

.enquiry-inbox__summary {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.enquiry-inbox__pagination {
  margin-top: 16px;
  justify-content: flex-end;
}
</style>

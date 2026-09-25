<script setup lang="ts">
/**
 * G2 詢問收件匣——處理頁（對照 apps/api/README.md「S1-10」）。**沒有建立模式**：詢問只能由訪客
 * 透過 10 表單中心送出，後台只能處理既有一筆（狀態／指派負責人／內部備註／標籤），不能新增，也
 * 不能改動來源表單與訪客原始回答內容。
 *
 * 🔴 **「指派負責人」姓名選單只有系統管理員能用**：`GET /api/v1/admin/accounts`（可以把
 * `assigneeAdminUserId` 這個 GUID 對照回姓名、或列出可指派對象）是 `system.account.view`，
 * 僅系統管理員可呼叫（見 `useFormsPermissions.ts` 檔頭的完整說明）。持有 `enquiry.*.update` 但
 * 不是系統管理員的角色（客服／行政、合作球隊管理、學院／課程管理……）因此**沒有任何後端端點
 * 能把指派對象的姓名秀出來，也沒辦法選別人**——這裡對非系統管理員只提供「指派給我自己」與
 * 「取消指派」兩個按鈕（用 `currentAdminUserId` 判斷是否已指派給自己），不假裝能做姓名選單。
 * 這是發現的後端缺口，已在任務報告與 apps/admin/README.md 回報。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useFormsPermissions } from '@/composables/useFormsPermissions'
import { activeClubId, currentAdminUserId } from '@/auth/clubAccess'
import { authUser } from '@/auth/session'
import { getAdminEnquiry, updateAdminEnquiry, type AdminEnquiryAnswerDto } from '@/api/adminEnquiries'
import { listAdminAccounts, type AdminAccountListItemDto } from '@/api/adminAccounts'
import { AdminApiError } from '@/api/http'
import { ENQUIRY_STATUS_ORDER, enquiryStatusTagType, fieldKeyLabel } from '@/types/forms'
import { formatDateTime } from '@/utils/formatDateTime'

const route = useRoute()
const router = useRouter()
const { canUpdateInbox, canPickAssigneeByName } = useFormsPermissions()
const isReadOnly = computed(() => !canUpdateInbox.value)

const enquiryId = ref<string | undefined>(route.params.id as string | undefined)

const formNameZh = ref('')
const sourcePath = ref<string | null>(null)
const utmSource = ref<string | null>(null)
const utmCampaign = ref<string | null>(null)
const createdAt = ref('')
const answers = ref<AdminEnquiryAnswerDto[]>([])

const form = reactive({
  status: '' as string,
  assigneeAdminUserId: null as string | null,
  internalNote: '',
  tags: '',
})
const baselineJson = ref('')

const accountOptions = ref<AdminAccountListItemDto[]>([])

const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
const formError = ref<string | null>(null)

async function loadAccountOptions() {
  if (!canPickAssigneeByName.value) return
  try {
    const result = await listAdminAccounts({ status: 'active', pageSize: 100 })
    accountOptions.value = result.items
  } catch {
    accountOptions.value = []
  }
}

async function loadEnquiry() {
  loadState.value = 'loading'
  try {
    await loadAccountOptions()
    const detail = await getAdminEnquiry(activeClubId.value, enquiryId.value!)
    formNameZh.value = detail.formNameZh
    sourcePath.value = detail.sourcePath ?? null
    utmSource.value = detail.utmSource ?? null
    utmCampaign.value = detail.utmCampaign ?? null
    createdAt.value = detail.createdAt
    answers.value = detail.answers
    form.status = detail.status ?? '新進'
    form.assigneeAdminUserId = detail.assigneeAdminUserId ?? null
    form.internalNote = detail.internalNote ?? ''
    form.tags = detail.tags ?? ''
    baselineJson.value = JSON.stringify(form)
    loadState.value = 'ready'
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'not-found') {
      loadState.value = 'not-found'
    } else {
      loadErrorMessage.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
      loadState.value = 'error'
    }
  }
}

onMounted(loadEnquiry)

const isDirty = computed(() => loadState.value === 'ready' && JSON.stringify(form) !== baselineJson.value)
useUnsavedChanges(isDirty)

const pageTitle = computed(() => `${canUpdateInbox.value ? '處理' : '檢視'}詢問：${formNameZh.value}`)

const isAssignedToSelf = computed(() => !!form.assigneeAdminUserId && form.assigneeAdminUserId === currentAdminUserId.value)
const isAssignedToOther = computed(() => !!form.assigneeAdminUserId && form.assigneeAdminUserId !== currentAdminUserId.value)

function assignToSelf() {
  if (!currentAdminUserId.value) return
  form.assigneeAdminUserId = currentAdminUserId.value
}

function unassign() {
  form.assigneeAdminUserId = null
}

async function handleSave() {
  if (isReadOnly.value) return
  saving.value = true
  formError.value = null
  try {
    await updateAdminEnquiry(activeClubId.value, enquiryId.value!, {
      status: form.status,
      assigneeAdminUserId: form.assigneeAdminUserId,
      internalNote: form.internalNote.trim() || null,
      tags: form.tags.trim() || null,
    })
    ElMessage.success('已儲存')
    baselineJson.value = JSON.stringify(form)
  } catch (error) {
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

function handleBack() {
  router.push('/inquiries/inbox')
}

function retryLoad() {
  loadEnquiry()
}
</script>

<template>
  <div class="enquiry-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="handleBack">
          <el-icon><ArrowLeft /></el-icon>
          返回收件匣
        </el-button>
      </template>
      <template #meta>
        <FrontendUnitBanner module-code="G2" />
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never">
      <el-skeleton :rows="8" animated />
    </el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這筆詢問，可能不屬於你能檢視的表單類別，或不屬於目前選擇的俱樂部。</p>
          <p v-else>{{ loadErrorMessage }}</p>
        </template>
        <el-button v-if="loadState !== 'not-found'" type="primary" @click="retryLoad">重新載入</el-button>
        <el-button v-else type="primary" @click="handleBack">返回收件匣</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert
        v-if="formError"
        :title="formError"
        type="warning"
        show-icon
        class="enquiry-edit__form-error"
        @close="formError = null"
      />
      <el-alert
        v-if="isReadOnly"
        title="你的帳號只有檢視權限，沒有這個模組的處理權限"
        type="info"
        show-icon
        :closable="false"
        class="enquiry-edit__form-error"
      />

      <el-card shadow="never" header="訪客送出的內容" class="enquiry-edit__section">
        <dl class="enquiry-edit__meta">
          <div class="enquiry-edit__meta-row">
            <dt>來源表單</dt>
            <dd>{{ formNameZh }}</dd>
          </div>
          <div class="enquiry-edit__meta-row">
            <dt>來源頁面</dt>
            <dd>{{ sourcePath || '—' }}</dd>
          </div>
          <div class="enquiry-edit__meta-row">
            <dt>UTM 來源／活動</dt>
            <dd>{{ utmSource || '—' }}{{ utmCampaign ? `／${utmCampaign}` : '' }}</dd>
          </div>
          <div class="enquiry-edit__meta-row">
            <dt>送出時間</dt>
            <dd>{{ formatDateTime(createdAt) }}</dd>
          </div>
        </dl>

        <el-table :data="answers" row-key="fieldKey" class="enquiry-edit__answers">
          <el-table-column label="欄位" width="180">
            <template #default="{ row }">{{ fieldKeyLabel(row.fieldKey) }}</template>
          </el-table-column>
          <el-table-column label="訪客填寫的內容">
            <template #default="{ row }">
              <span v-if="row.fieldType === 'consent'">{{ row.value ? '已勾選同意' : '未勾選' }}</span>
              <span v-else>{{ row.value || '（未填寫）' }}</span>
            </template>
          </el-table-column>
        </el-table>
        <p class="enquiry-edit__hint">
          這裡的欄位名稱來自建立表單時輸入的欄位代碼，系統目前沒有另外儲存「問題文字」，看不懂的欄位可以對照「表單設計器」裡的設定。這一區是訪客的原始送出資料，後台無法修改。
        </p>
      </el-card>

      <el-form label-position="top" :disabled="isReadOnly">
        <el-card shadow="never" header="後台處理" class="enquiry-edit__section">
          <el-form-item label="狀態" required>
            <el-select v-model="form.status" style="width: 220px">
              <el-option v-for="s in ENQUIRY_STATUS_ORDER" :key="s" :label="s" :value="s">
                <el-tag :type="enquiryStatusTagType(s)" size="small">{{ s }}</el-tag>
              </el-option>
            </el-select>
          </el-form-item>

          <el-form-item label="指派負責人">
            <template v-if="canPickAssigneeByName">
              <el-select v-model="form.assigneeAdminUserId" clearable filterable placeholder="請選擇負責人（可留空）" style="width: 280px">
                <el-option v-for="a in accountOptions" :key="a.id" :label="a.displayName" :value="a.id" />
              </el-select>
            </template>
            <template v-else>
              <div class="enquiry-edit__assignee">
                <span v-if="isAssignedToSelf">已指派給你自己（{{ authUser?.displayName }}）</span>
                <span v-else-if="isAssignedToOther">已指派給其他人——你的帳號無法查詢帳號清單，看不到對方姓名</span>
                <span v-else>目前未指派</span>
                <el-button size="small" :disabled="isReadOnly || isAssignedToSelf" @click="assignToSelf">指派給我自己</el-button>
                <el-button size="small" :disabled="isReadOnly || !form.assigneeAdminUserId" @click="unassign">取消指派</el-button>
              </div>
              <p class="enquiry-edit__hint">
                只有系統管理員能用姓名選單指派給其他人（帳號清單目前僅系統管理員能查詢）；你的帳號可以指派給自己或取消指派。
              </p>
            </template>
          </el-form-item>

          <el-form-item label="標籤（自由文字，可用逗號分隔多個）">
            <el-input v-model="form.tags" placeholder="例如 需追蹤,VIP" />
          </el-form-item>

          <el-form-item label="內部備註（僅後台看得到，訪客不會收到）">
            <el-input v-model="form.internalNote" type="textarea" :rows="4" />
          </el-form-item>
        </el-card>
      </el-form>

      <div v-if="!isReadOnly" class="enquiry-edit__action-bar">
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.enquiry-edit {
  max-width: 780px;
  margin: 0 auto 88px;
}

.enquiry-edit__form-error {
  margin-bottom: 16px;
}

.enquiry-edit__section {
  margin-bottom: 16px;
}

.enquiry-edit__meta {
  margin: 0 0 12px;
  display: grid;
  grid-template-columns: 140px 1fr;
  row-gap: 6px;
}

.enquiry-edit__meta-row {
  display: contents;
}

.enquiry-edit__meta dt {
  color: var(--admin-text-tertiary);
  font-size: 13px;
}

.enquiry-edit__meta dd {
  margin: 0;
}

.enquiry-edit__answers {
  margin-top: 4px;
}

.enquiry-edit__hint {
  margin: 8px 0 0;
  font-size: 12px;
  color: var(--admin-text-tertiary);
  line-height: 1.6;
}

.enquiry-edit__assignee {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.enquiry-edit__action-bar {
  position: fixed;
  bottom: 0;
  left: var(--admin-sidebar-width-expanded);
  right: 0;
  background: var(--admin-bg-surface-2);
  border-top: 1px solid var(--admin-border);
  padding: 12px 24px;
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  z-index: 10;
}

@media (max-width: 1023px) {
  .enquiry-edit__action-bar {
    left: 0;
  }
}

@media (max-width: 767px) {
  .enquiry-edit__action-bar {
    justify-content: stretch;
  }

  .enquiry-edit__action-bar :deep(.el-button) {
    flex: 1;
  }
}
</style>

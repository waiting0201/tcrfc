<script setup lang="ts">
/**
 * P3 報名管理——後台代填／處理報名頁。對照 apps/api/README.md「S1-9」。
 *
 * 🔴 **健康聲明依後端現況原樣顯示與編輯**（`nvarchar(max)` 明文欄位，未加密、未做欄位級遮罩）
 * ——依任務指示不新增蒐集欄位、不新增同意書上傳，只依 `program.registration.view` 權限碼控管
 * 誰看得到這頁。CSV 匯出（列表頁）已由後端刻意排除這欄，這裡不需要另外處理。
 *
 * ⚠️ 「會員」欄位（`memberId`）本輪不提供選擇介面——K1 會員系統尚未開發，前台也沒有會員登入
 * 能串接，這裡只唯讀顯示既有值（若有），不開放後台指定或搜尋會員。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useProgramPermissions } from '@/composables/useProgramPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { listAdminProgramSessions, type AdminSessionListItemDto } from '@/api/adminProgramSessions'
import { createAdminRegistration, getAdminRegistration, updateAdminRegistration } from '@/api/adminRegistrations'
import { AdminApiError } from '@/api/http'
import { REGISTRATION_STATUS_ORDER, type RegistrationStatus } from '@/types/program'

const route = useRoute()
const router = useRouter()
const { canCreateRegistrations, canProcessRegistrations } = useProgramPermissions()

const isCreate = computed(() => route.name === 'registration-new')
const registrationId = ref<string | undefined>(route.params.id as string | undefined)

const form = reactive({
  sessionId: '',
  applicantName: '',
  phone: '',
  email: '',
  birthOn: null as Date | null,
  guardianName: '',
  guardianPhone: '',
  healthDeclaration: '',
  note: '',
  status: '待確認' as RegistrationStatus,
})
const baselineJson = ref('')
const registrationNo = ref('')
const memberId = ref<string | null>(null)

const sessions = ref<AdminSessionListItemDto[]>([])
const sessionLabelById = computed(() => {
  const map = new Map<string, string>()
  for (const s of sessions.value) {
    map.set(s.id, `${s.programNameZh ?? '（未命名項目）'}（${s.startOn ?? '—'} ～ ${s.endOn ?? '—'}）`)
  }
  return map
})

const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
const formError = ref<string | null>(null)

function toDateOnlyString(date: Date | null): string | null {
  if (!date) return null
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

function fromDateOnlyString(value: string | null | undefined): Date | null {
  if (!value) return null
  const parsed = new Date(`${value}T00:00:00`)
  return Number.isNaN(parsed.getTime()) ? null : parsed
}

async function loadSessions() {
  try {
    sessions.value = await listAdminProgramSessions(activeClubId.value)
  } catch {
    sessions.value = []
  }
}

async function loadRegistration() {
  loadState.value = 'loading'
  try {
    await loadSessions()
    if (!isCreate.value && registrationId.value) {
      const detail = await getAdminRegistration(activeClubId.value, registrationId.value)
      form.sessionId = detail.sessionId ?? ''
      form.applicantName = detail.applicantName
      form.phone = detail.phone ?? ''
      form.email = detail.email ?? ''
      form.birthOn = fromDateOnlyString(detail.birthOn)
      form.guardianName = detail.guardianName ?? ''
      form.guardianPhone = detail.guardianPhone ?? ''
      form.healthDeclaration = detail.healthDeclaration ?? ''
      form.note = detail.note ?? ''
      form.status = detail.status as RegistrationStatus
      registrationNo.value = detail.registrationNo
      memberId.value = detail.memberId ?? null
    } else if (route.query.sessionId) {
      form.sessionId = String(route.query.sessionId)
    }
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

onMounted(loadRegistration)

const isDirty = computed(() => loadState.value === 'ready' && JSON.stringify(form) !== baselineJson.value)
useUnsavedChanges(isDirty)

const pageTitle = computed(() => (isCreate.value ? '新增報名（後台代填）' : `處理報名：${form.applicantName || '（未命名）'}`))
const canEditThisPage = computed(() => (isCreate.value ? canCreateRegistrations.value : canProcessRegistrations.value))
const isReadOnly = computed(() => !canEditThisPage.value)

function validate(): boolean {
  formError.value = null
  if (!form.sessionId) {
    formError.value = '請選擇梯次'
    return false
  }
  if (!form.applicantName.trim()) {
    formError.value = '請輸入報名人姓名'
    return false
  }
  if (!form.phone.trim() && !form.email.trim()) {
    formError.value = '電話與 Email 至少要填一項'
    return false
  }
  return true
}

// 🔴 刻意不標註回傳型別為 `CreateRegistrationPayload`（其 `status` 是可省略／可為 null）——
// `form.status` 一律有值（`RegistrationStatus`，非 null 字串），讓 TypeScript 自行推導出的物件
// 型別可以同時滿足 `CreateRegistrationPayload` 與 `UpdateRegistrationPayload`（後者 `status`
// 是必填字串），建立與處理兩個流程才能共用同一個組裝函式，不必為了型別差異各寫一份。
function buildPayload() {
  return {
    sessionId: form.sessionId,
    memberId: memberId.value,
    applicantName: form.applicantName.trim(),
    phone: form.phone || null,
    email: form.email || null,
    birthOn: toDateOnlyString(form.birthOn),
    guardianName: form.guardianName || null,
    guardianPhone: form.guardianPhone || null,
    healthDeclaration: form.healthDeclaration || null,
    note: form.note || null,
    status: form.status,
  }
}

async function handleSave() {
  if (isReadOnly.value) return
  if (!validate()) return
  saving.value = true
  formError.value = null
  try {
    if (isCreate.value) {
      const created = await createAdminRegistration(activeClubId.value, buildPayload())
      ElMessage.success('已建立')
      router.replace(`/programs/enrollments/${created.id}/edit`)
      registrationId.value = created.id
      registrationNo.value = created.registrationNo
    } else {
      const updated = await updateAdminRegistration(activeClubId.value, registrationId.value!, buildPayload())
      registrationNo.value = updated.registrationNo
      ElMessage.success('已儲存')
    }
    baselineJson.value = JSON.stringify(form)
  } catch (error) {
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

function handleBack() {
  router.push('/programs/enrollments')
}

function retryLoad() {
  loadRegistration()
}
</script>

<template>
  <div class="registration-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="handleBack">
          <el-icon><ArrowLeft /></el-icon>
          返回列表
        </el-button>
      </template>
      <template #meta>
        <FrontendUnitBanner module-code="P3" />
        <span v-if="registrationNo" class="registration-edit__no">報名編號：{{ registrationNo }}</span>
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never">
      <el-skeleton :rows="8" animated />
    </el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這筆報名，可能已經被刪除，或不屬於目前選擇的俱樂部。</p>
          <p v-else>{{ loadErrorMessage }}</p>
        </template>
        <el-button v-if="loadState !== 'not-found'" type="primary" @click="retryLoad">重新載入</el-button>
        <el-button v-else type="primary" @click="handleBack">返回列表</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert
        v-if="formError"
        :title="formError"
        type="warning"
        show-icon
        class="registration-edit__form-error"
        @close="formError = null"
      />
      <el-alert
        v-if="isReadOnly"
        title="你的帳號只有檢視權限，沒有這筆報名的處理權限"
        type="info"
        show-icon
        :closable="false"
        class="registration-edit__form-error"
      />

      <el-form label-position="top" :disabled="isReadOnly">
        <el-card shadow="never" header="梯次與狀態" class="registration-edit__section">
          <el-row :gutter="12">
            <el-col :span="16">
              <el-form-item label="梯次" required>
                <el-select
                  v-model="form.sessionId"
                  filterable
                  style="width: 100%"
                  no-data-text="目前這個俱樂部還沒有任何梯次，請先到「梯次」新增一筆"
                >
                  <el-option v-for="s in sessions" :key="s.id" :label="sessionLabelById.get(s.id)" :value="s.id" />
                </el-select>
                <p v-if="!isCreate" class="registration-edit__hint">更換梯次即為「轉梯次」，會自動調整新舊梯次的已報名數。</p>
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="狀態" required>
                <el-select v-model="form.status" style="width: 100%">
                  <el-option v-for="s in REGISTRATION_STATUS_ORDER" :key="s" :label="s" :value="s" />
                </el-select>
              </el-form-item>
            </el-col>
          </el-row>
          <p v-if="memberId" class="registration-edit__hint">
            這筆報名關聯既有會員（會員系統 K1 尚未開發，這裡僅顯示是否關聯，無法在此變更或搜尋會員）。
          </p>
        </el-card>

        <el-card shadow="never" header="學員資料" class="registration-edit__section">
          <el-row :gutter="12">
            <el-col :span="12">
              <el-form-item label="報名人姓名" required>
                <el-input v-model="form.applicantName" />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="生日">
                <el-date-picker v-model="form.birthOn" type="date" style="width: 100%" />
              </el-form-item>
            </el-col>
          </el-row>
          <el-row :gutter="12">
            <el-col :span="12">
              <el-form-item label="電話">
                <el-input v-model="form.phone" />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="Email">
                <el-input v-model="form.email" />
              </el-form-item>
            </el-col>
          </el-row>
          <p class="registration-edit__hint">電話與 Email 至少要填一項。</p>
          <el-row :gutter="12">
            <el-col :span="12">
              <el-form-item label="家長姓名">
                <el-input v-model="form.guardianName" placeholder="選填，未成年學員建議填寫" />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="家長電話">
                <el-input v-model="form.guardianPhone" placeholder="選填" />
              </el-form-item>
            </el-col>
          </el-row>
        </el-card>

        <el-card shadow="never" header="健康聲明與備註" class="registration-edit__section">
          <el-form-item label="健康聲明">
            <el-input v-model="form.healthDeclaration" type="textarea" :rows="3" placeholder="選填，依報名人填寫內容原樣顯示" />
          </el-form-item>
          <el-form-item label="備註">
            <el-input v-model="form.note" type="textarea" :rows="2" placeholder="選填" />
          </el-form-item>
        </el-card>
      </el-form>

      <div v-if="!isReadOnly" class="registration-edit__action-bar">
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.registration-edit {
  max-width: 780px;
  margin: 0 auto 88px;
}

.registration-edit__no {
  font-size: 13px;
  color: var(--admin-text-secondary);
}

.registration-edit__form-error {
  margin-bottom: 16px;
}

.registration-edit__section {
  margin-bottom: 16px;
}

.registration-edit__hint {
  margin: 6px 0 0;
  font-size: 12px;
  color: var(--admin-text-tertiary);
  line-height: 1.6;
}

.registration-edit__action-bar {
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
  .registration-edit__action-bar {
    left: 0;
  }
}

@media (max-width: 767px) {
  .registration-edit__action-bar {
    justify-content: stretch;
  }

  .registration-edit__action-bar :deep(.el-button) {
    flex: 1;
  }
}
</style>

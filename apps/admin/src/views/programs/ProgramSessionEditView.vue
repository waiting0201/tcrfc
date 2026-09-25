<script setup lang="ts">
/**
 * P2 梯次與場次——編輯頁。對照 apps/api/README.md「S1-9」。
 *
 * 🔴 **名額（已報名數）不可編輯**——`sessions.enrolled_count` 只由報名寫入路徑維護（後台
 * 代填、公開報名、轉梯次），後台這裡只能檢視，見 `apps/api` `CreateAdminSessionRequest` 檔頭
 * 「刻意不開放後台直接填寫」的說明。
 *
 * ⚠️ **場地（`venueId`）本輪不提供選單**——`venues` 是共用主檔，但目前沒有任何後台端點可以
 * 列出場地清單，跟 `MatchEditView.vue` 賽事場地欄位遇到的既有缺口是同一個，這裡沿用同一個
 * 判斷不重複造：不送出 `venueId`（一律維持 `null`），已在 apps/admin/README.md 註記。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useProgramPermissions } from '@/composables/useProgramPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { listAdminPrograms, type AdminProgramListItemDto } from '@/api/adminPrograms'
import {
  createAdminProgramSession,
  getAdminProgramSession,
  updateAdminProgramSession,
  type CreateSessionPayload,
} from '@/api/adminProgramSessions'
import { AdminApiError } from '@/api/http'
import { SESSION_STATUS_ORDER } from '@/types/program'

const route = useRoute()
const router = useRouter()
const { canManageItems: canManageSessions } = useProgramPermissions()

const isCreate = computed(() => route.name === 'program-session-new')
const sessionId = ref<string | undefined>(route.params.id as string | undefined)

const form = reactive({
  programId: '',
  startOn: null as Date | null,
  endOn: null as Date | null,
  weeklySchedule: '',
  capacity: null as number | null,
  price: null as number | null,
  earlyBirdPrice: null as number | null,
  earlyBirdUntil: null as Date | null,
  signupOpensAt: null as Date | null,
  signupClosesAt: null as Date | null,
  status: '' as '' | '開放' | '額滿' | '候補' | '已結束',
})
const baselineJson = ref('')
const enrolledCount = ref(0)

const programs = ref<AdminProgramListItemDto[]>([])
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

function fromIsoString(value: string | null | undefined): Date | null {
  if (!value) return null
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? null : parsed
}

async function loadPrograms() {
  try {
    programs.value = await listAdminPrograms(activeClubId.value)
  } catch {
    programs.value = []
  }
}

async function loadSession() {
  loadState.value = 'loading'
  try {
    await loadPrograms()
    if (!isCreate.value && sessionId.value) {
      const detail = await getAdminProgramSession(activeClubId.value, sessionId.value)
      form.programId = detail.programId
      form.startOn = fromDateOnlyString(detail.startOn)
      form.endOn = fromDateOnlyString(detail.endOn)
      form.weeklySchedule = detail.weeklySchedule ?? ''
      form.capacity = detail.capacity ?? null
      form.price = detail.price ?? null
      form.earlyBirdPrice = detail.earlyBirdPrice ?? null
      form.earlyBirdUntil = fromDateOnlyString(detail.earlyBirdUntil)
      form.signupOpensAt = fromIsoString(detail.signupOpensAt)
      form.signupClosesAt = fromIsoString(detail.signupClosesAt)
      form.status = (detail.status as typeof form.status) ?? ''
      enrolledCount.value = detail.enrolledCount
    } else if (route.query.programId) {
      form.programId = String(route.query.programId)
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

onMounted(loadSession)
watch(activeClubId, () => {
  if (isCreate.value) loadPrograms()
})

const isDirty = computed(() => loadState.value === 'ready' && JSON.stringify(form) !== baselineJson.value)
useUnsavedChanges(isDirty)

const programLabel = computed(() => programs.value.find((p) => p.id === form.programId)?.nameZh ?? '')
const pageTitle = computed(() =>
  isCreate.value ? '新增梯次' : `${canManageSessions.value ? '編輯' : '檢視'}梯次：${programLabel.value || '（未命名項目）'}`,
)
const isReadOnly = computed(() => !canManageSessions.value)

function validateJson(): boolean {
  if (!form.weeklySchedule.trim()) return true
  try {
    JSON.parse(form.weeklySchedule)
    return true
  } catch {
    formError.value = '上課時間表不是合法的 JSON 格式，請確認內容（或留空）'
    return false
  }
}

function validate(): boolean {
  formError.value = null
  if (!form.programId) {
    formError.value = '請選擇所屬課程／營隊項目'
    return false
  }
  if (form.capacity != null && form.capacity < 0) {
    formError.value = '名額上限不能是負數'
    return false
  }
  if (!validateJson()) return false
  return true
}

function buildPayload(): CreateSessionPayload {
  return {
    programId: form.programId,
    venueId: null,
    startOn: toDateOnlyString(form.startOn),
    endOn: toDateOnlyString(form.endOn),
    weeklySchedule: form.weeklySchedule || null,
    capacity: form.capacity,
    price: form.price,
    earlyBirdPrice: form.earlyBirdPrice,
    earlyBirdUntil: toDateOnlyString(form.earlyBirdUntil),
    signupOpensAt: form.signupOpensAt ? form.signupOpensAt.toISOString() : null,
    signupClosesAt: form.signupClosesAt ? form.signupClosesAt.toISOString() : null,
    status: form.status || null,
  }
}

async function handleSave() {
  if (isReadOnly.value) return
  if (!validate()) return
  saving.value = true
  formError.value = null
  try {
    if (isCreate.value) {
      const created = await createAdminProgramSession(activeClubId.value, buildPayload())
      ElMessage.success('已建立')
      router.replace(`/programs/sessions/${created.id}/edit`)
      sessionId.value = created.id
      enrolledCount.value = created.enrolledCount
    } else {
      // `updateAdminProgramSession` 的型別是 `Omit<CreateSessionPayload, 'programId'>`——這裡直接把
      // `buildPayload()` 的結果（含 `programId`，但既然是變數賦值而非物件實字，TypeScript 不會做
      // 多餘屬性檢查）傳進去即可，後端 `UpdateAdminSessionRequest` 本來就沒有這個欄位，多送了也
      // 不會被讀取。不特地解構拿掉，省得為了一個永遠用不到的變數名多繞一手。
      const updated = await updateAdminProgramSession(activeClubId.value, sessionId.value!, buildPayload())
      enrolledCount.value = updated.enrolledCount
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
  router.push('/programs/sessions')
}

function retryLoad() {
  loadSession()
}
</script>

<template>
  <div class="session-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="handleBack">
          <el-icon><ArrowLeft /></el-icon>
          返回列表
        </el-button>
      </template>
      <template #meta>
        <FrontendUnitBanner module-code="P2" />
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never">
      <el-skeleton :rows="8" animated />
    </el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這個梯次，可能已經被刪除，或不屬於目前選擇的俱樂部。</p>
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
        class="session-edit__form-error"
        @close="formError = null"
      />
      <el-alert
        v-if="isReadOnly"
        title="你的帳號只有檢視權限，沒有這個模組的建立／編輯權限"
        type="info"
        show-icon
        :closable="false"
        class="session-edit__form-error"
      />

      <el-form label-position="top" :disabled="isReadOnly">
        <el-card shadow="never" header="基本資料" class="session-edit__section">
          <el-form-item label="所屬課程／營隊項目" required>
            <el-select
              v-model="form.programId"
              filterable
              :disabled="!isCreate || isReadOnly"
              style="width: 100%"
              no-data-text="目前這個俱樂部還沒有任何課程／營隊項目，請先到「項目」新增一筆"
            >
              <el-option v-for="p in programs" :key="p.id" :label="p.nameZh || p.slug" :value="p.id" />
            </el-select>
            <p v-if="!isCreate" class="session-edit__hint">建立後不能更換所屬項目。</p>
          </el-form-item>

          <el-row :gutter="12">
            <el-col :span="12">
              <el-form-item label="開始日期">
                <el-date-picker v-model="form.startOn" type="date" style="width: 100%" />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="結束日期">
                <el-date-picker v-model="form.endOn" type="date" style="width: 100%" />
              </el-form-item>
            </el-col>
          </el-row>

          <el-form-item label="場地">
            <p class="session-edit__hint">場地選單目前沒有可用清單（`venues` 主檔尚無對應後台端點），暫不開放選擇。</p>
          </el-form-item>

          <el-form-item label="上課時間表（JSON，選填）">
            <el-input v-model="form.weeklySchedule" type="textarea" :rows="4" placeholder="例如：{ mon: 18:00-19:30 }（合法 JSON）" />
          </el-form-item>
        </el-card>

        <el-card shadow="never" header="名額與費用" class="session-edit__section">
          <el-row :gutter="12">
            <el-col :span="8">
              <el-form-item label="名額上限">
                <el-input-number v-model="form.capacity" :min="0" style="width: 100%" placeholder="不限" />
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="目前已報名數">
                <el-input :model-value="enrolledCount" disabled />
                <p class="session-edit__hint">由報名寫入路徑自動維護，這裡無法直接修改。</p>
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="狀態">
                <el-select v-model="form.status" clearable placeholder="留空自動判定" style="width: 100%">
                  <el-option v-for="s in SESSION_STATUS_ORDER" :key="s" :label="s" :value="s" />
                </el-select>
                <p class="session-edit__hint">留空時依名額自動推定（額滿即關閉），「候補」與「已結束」需要人工設定。</p>
              </el-form-item>
            </el-col>
          </el-row>
          <el-row :gutter="12">
            <el-col :span="8">
              <el-form-item label="原價">
                <el-input-number v-model="form.price" :min="0" style="width: 100%" />
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="早鳥價">
                <el-input-number v-model="form.earlyBirdPrice" :min="0" style="width: 100%" />
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="早鳥截止日">
                <el-date-picker v-model="form.earlyBirdUntil" type="date" style="width: 100%" />
              </el-form-item>
            </el-col>
          </el-row>
          <el-row :gutter="12">
            <el-col :span="12">
              <el-form-item label="報名開始時間">
                <el-date-picker v-model="form.signupOpensAt" type="datetime" style="width: 100%" />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="報名截止時間">
                <el-date-picker v-model="form.signupClosesAt" type="datetime" style="width: 100%" />
              </el-form-item>
            </el-col>
          </el-row>
        </el-card>
      </el-form>

      <div v-if="!isReadOnly" class="session-edit__action-bar">
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.session-edit {
  max-width: 780px;
  margin: 0 auto 88px;
}

.session-edit__form-error {
  margin-bottom: 16px;
}

.session-edit__section {
  margin-bottom: 16px;
}

.session-edit__hint {
  margin: 6px 0 0;
  font-size: 12px;
  color: var(--admin-text-tertiary);
  line-height: 1.6;
}

.session-edit__action-bar {
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
  .session-edit__action-bar {
    left: 0;
  }
}

@media (max-width: 767px) {
  .session-edit__action-bar {
    justify-content: stretch;
  }

  .session-edit__action-bar :deep(.el-button) {
    flex: 1;
  }
}
</style>

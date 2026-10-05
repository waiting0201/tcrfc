<script setup lang="ts">
/**
 * 試訓報名——後台代填／處理。對照 apps/api/README.md「B1」節「P4 試訓場次」。
 *
 * 🔴 健康聲明依既有課程報名的做法原樣顯示與編輯（整份覆寫），不新增蒐集欄位；
 * 名單匯出與簽到表已由後端排除這欄。
 * 「會員」關聯（memberId）只保留既有值、不在此搜尋指定；非會員也可報名。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import {
  createTrialRegistration,
  getTrial,
  getTrialRegistration,
  updateTrialRegistration,
  type SaveTrialRegistrationPayload,
  type TrialRegistrationDetailDto,
} from '@/api/adminTrials'
import { REGISTRATION_STATUS_ORDER } from '@/types/program'
import { isUnder18, validateContact } from '@/utils/contactValidation'

const props = defineProps<{ id: string }>()

const route = useRoute()
const router = useRouter()
// 🔴 必須是 computed（建立成功後 router.replace 同一元件不會重新掛載）
const isCreate = computed(() => route.name === 'trial-registration-new')
const regId = ref<string | undefined>(route.params.regId as string | undefined)
const { canCreate, canUpdate } = useCrudPermissions('program.trial_registration')
const readOnly = computed(() => (isCreate.value ? !canCreate.value : !canUpdate.value))

const form = reactive({
  applicantName: '',
  phone: '',
  email: '',
  birthOn: '',
  guardianName: '',
  guardianPhone: '',
  healthDeclaration: '',
  note: '',
  status: '待確認',
})
const baselineJson = ref('')
const registrationNo = ref('')
const memberId = ref<string | null>(null)
const trialLabel = ref('')
const trialQuota = ref<{ capacity: number | null; enrolled: number } | null>(null)
/** 會佔用名額的狀態（與狀態說明文字一致）。 */
const OCCUPYING = ['待確認', '已確認', '已繳費', '完成']
const originalStatus = ref<string | null>(null)
/** 依「儲存後」的名額預估：新增、或由不佔名額改為佔名額，會多佔一個。 */
const serverOver = ref<boolean | null>(null)
const quotaWarning = computed(() => {
  const q = trialQuota.value
  if (!q || q.capacity == null) return ''
  // 後端有回 isOverCapacity（已儲存的這筆）就以它為準；沒有、或尚未儲存的新增，才用前端預估
  if (!isCreate.value && serverOver.value !== null && form.status === originalStatus.value) {
    return serverOver.value ? `這場名額 ${q.capacity} 人，目前已報名 ${q.enrolled} 人，已超過名額。後台代填不會被擋，請確認場地與教練是否容納得下。` : ''
  }
  const willOccupy = OCCUPYING.includes(form.status)
  const alreadyCounted = !isCreate.value && originalStatus.value !== null && OCCUPYING.includes(originalStatus.value)
  const after = q.enrolled + (willOccupy && !alreadyCounted ? 1 : 0)
  if (after <= q.capacity) return ''
  return `這場名額 ${q.capacity} 人，目前已報名 ${q.enrolled} 人，儲存後會變成 ${after} 人，超過名額 ${after - q.capacity} 人。後台代填不會被擋，請確認場地與教練是否容納得下。`
})

const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
const formError = ref<string | null>(null)

const listPath = computed(() => `/programs/trials/${props.id}/registrations`)
const pageTitle = computed(() => (isCreate.value ? '新增試訓報名（後台代填）' : `處理試訓報名：${form.applicantName || '（未命名）'}`))

function applyDetail(d: TrialRegistrationDetailDto) {
  form.applicantName = d.applicantName
  form.phone = d.phone ?? ''
  form.email = d.email ?? ''
  form.birthOn = d.birthOn ?? ''
  form.guardianName = d.guardianName ?? ''
  form.guardianPhone = d.guardianPhone ?? ''
  form.healthDeclaration = d.healthDeclaration ?? ''
  form.note = d.note ?? ''
  form.status = d.status
  registrationNo.value = d.registrationNo
  memberId.value = d.memberId ?? null
  originalStatus.value = d.status
  serverOver.value = typeof d.isOverCapacity === 'boolean' ? d.isOverCapacity : null
}

async function load() {
  loadState.value = 'loading'
  try {
    const trial = await getTrial(activeClubId.value, props.id)
    trialLabel.value = `${trial.trialOn} ${trial.teamName || '俱樂部整體'}`
    trialQuota.value = { capacity: trial.capacity ?? null, enrolled: trial.enrolledCount }
    if (!isCreate.value && regId.value) {
      applyDetail(await getTrialRegistration(activeClubId.value, props.id, regId.value))
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
onMounted(load)

const minor = computed(() => isUnder18(form.birthOn))
const isDirty = computed(() => loadState.value === 'ready' && JSON.stringify(form) !== baselineJson.value)
useUnsavedChanges(isDirty)

function validate(): boolean {
  formError.value = null
  if (!form.applicantName.trim()) return (formError.value = '請輸入報名人姓名'), false
  // 與公開報名同一套規則：電話或 Email 至少一項、格式正確、未滿 18 歲須有家長姓名與電話
  const contactError = validateContact(form, { requireGuardianForMinor: true })
  if (contactError) return (formError.value = contactError), false
  return true
}

function buildPayload(): SaveTrialRegistrationPayload {
  const orNull = (v: string) => (v.trim() === '' ? null : v.trim())
  return {
    applicantName: form.applicantName.trim(),
    phone: orNull(form.phone),
    email: orNull(form.email),
    birthOn: form.birthOn || null,
    guardianName: orNull(form.guardianName),
    guardianPhone: orNull(form.guardianPhone),
    healthDeclaration: orNull(form.healthDeclaration),
    note: orNull(form.note),
    memberId: memberId.value,
    status: form.status,
  }
}

async function handleSave() {
  if (readOnly.value || !validate()) return
  saving.value = true
  formError.value = null
  try {
    const wasCreate = isCreate.value
    const saved = wasCreate
      ? await createTrialRegistration(activeClubId.value, props.id, buildPayload())
      : await updateTrialRegistration(activeClubId.value, props.id, regId.value!, buildPayload())
    if (wasCreate) {
      regId.value = saved.id
      router.replace(`/programs/trials/${props.id}/registrations/${saved.id}/edit`)
    }
    applyDetail(saved)
    baselineJson.value = JSON.stringify(form)
    ElMessage.success(wasCreate ? '已建立' : '已儲存')
    // 已報名人數會隨儲存改變，重讀一次讓超額提示維持正確（失敗不影響儲存結果）
    getTrial(activeClubId.value, props.id)
      .then((t) => { trialQuota.value = { capacity: t.capacity ?? null, enrolled: t.enrolledCount } })
      .catch(() => {})
  } catch (error) {
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="trial-reg-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="router.push(listPath)"><el-icon><ArrowLeft /></el-icon>返回名單</el-button>
      </template>
      <template #meta>
        <FrontendUnitBanner module-code="P4" />
        <span v-if="trialLabel" class="trial-reg-edit__meta">試訓場次：{{ trialLabel }}</span>
        <span v-if="registrationNo" class="trial-reg-edit__meta">報名編號：{{ registrationNo }}</span>
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這筆報名或這場試訓，可能已被刪除，或不屬於目前選擇的俱樂部。</p>
          <p v-else>{{ loadErrorMessage }}</p>
        </template>
        <el-button v-if="loadState === 'error'" type="primary" @click="load">重新載入</el-button>
        <el-button v-else type="primary" @click="router.push('/programs/trials')">返回場次列表</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="trial-reg-edit__block" @close="formError = null" />
      <el-alert v-if="quotaWarning" :title="quotaWarning" type="warning" show-icon :closable="false" class="trial-reg-edit__block" />
      <el-alert v-if="readOnly" title="你的帳號只能檢視這筆報名，不能修改。" type="info" show-icon :closable="false" class="trial-reg-edit__block" />

      <el-form label-position="top" :disabled="readOnly">
        <el-card shadow="never" header="報名狀態" class="trial-reg-edit__block">
          <el-form-item label="狀態" required>
            <el-select v-model="form.status" style="width: 200px; max-width: 100%">
              <el-option v-for="s in REGISTRATION_STATUS_ORDER" :key="s" :label="s" :value="s" />
            </el-select>
          </el-form-item>
          <p class="trial-reg-edit__hint">「待確認、已確認、已繳費、完成」會佔用名額，「取消、候補」不佔。候補要遞補時，請回名單按「遞補」。</p>
          <p v-if="memberId" class="trial-reg-edit__hint">這筆報名已關聯會員帳號，這裡不能變更。</p>
        </el-card>

        <el-card shadow="never" header="報名人資料" class="trial-reg-edit__block">
          <p class="trial-reg-edit__hint trial-reg-edit__hint--top">電話與 Email 至少填一項；報名者未滿 18 歲時，家長姓名與電話必填。</p>
          <el-row :gutter="12">
            <el-col :xs="24" :sm="12">
              <el-form-item label="報名人姓名" required><el-input v-model="form.applicantName" /></el-form-item>
            </el-col>
            <el-col :xs="24" :sm="12">
              <el-form-item label="生日"><el-date-picker v-model="form.birthOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item>
            </el-col>
            <el-col :xs="24" :sm="12">
              <el-form-item label="電話" :required="!form.email.trim()"><el-input v-model="form.phone" /></el-form-item>
            </el-col>
            <el-col :xs="24" :sm="12">
              <el-form-item label="Email" :required="!form.phone.trim()"><el-input v-model="form.email" /></el-form-item>
            </el-col>
            <el-col :xs="24" :sm="12">
              <el-form-item label="家長姓名" :required="minor"><el-input v-model="form.guardianName" placeholder="未滿 18 歲必填" /></el-form-item>
            </el-col>
            <el-col :xs="24" :sm="12">
              <el-form-item label="家長電話" :required="minor"><el-input v-model="form.guardianPhone" placeholder="未滿 18 歲必填" /></el-form-item>
            </el-col>
          </el-row>
        </el-card>

        <el-card shadow="never" header="健康聲明與備註" class="trial-reg-edit__block">
          <el-form-item label="健康聲明">
            <el-input v-model="form.healthDeclaration" type="textarea" :rows="3" placeholder="選填，依報名人填寫內容原樣顯示" />
          </el-form-item>
          <el-form-item label="備註">
            <el-input v-model="form.note" type="textarea" :rows="2" placeholder="選填" />
          </el-form-item>
        </el-card>
      </el-form>
      <EditActionBar v-if="!readOnly"><el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button></EditActionBar>
    </template>
  </div>
</template>

<style scoped>
.trial-reg-edit { max-width: 780px; margin: 0 auto 88px; }
.trial-reg-edit__block { margin-bottom: 16px; }
.trial-reg-edit__meta { font-size: 13px; color: var(--admin-text-secondary); margin-left: 12px; }
.trial-reg-edit__hint--top { margin: 0 0 12px; }
.trial-reg-edit__hint { margin: 6px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
</style>

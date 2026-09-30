<script setup lang="ts">
/** 試訓場次——新增／編輯。對照 apps/api/README.md「B1」節「P4 試訓場次」。 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { enOrUndefined } from '@/api/adminCommon'
import { listAdminClubTeams, type AdminTeamAdminListItemDto } from '@/api/adminTeams'
import { listAdminVenues, type AdminVenueListItemDto } from '@/api/adminVenues'
import {
  createTrial,
  getTrial,
  TRIAL_STATUS_ORDER,
  updateTrial,
  type SaveTrialPayload,
  type TrialDetailDto,
  type TrialStatus,
} from '@/api/adminTrials'

const route = useRoute()
const router = useRouter()
// 🔴 必須是 computed（建立成功後 router.replace 同一元件不會重新掛載）
const isCreate = computed(() => route.name === 'trial-new')
const trialId = ref<string | undefined>(route.params.id as string | undefined)
const { canCreate, canUpdate } = useCrudPermissions('program.trial')
const readOnly = computed(() => (isCreate.value ? !canCreate.value : !canUpdate.value))

const form = reactive({
  teamId: '',
  venueId: '',
  trialOn: '',
  capacity: null as number | null,
  deadlineOn: '',
  status: '開放' as TrialStatus,
  audienceZh: '',
  audienceEn: '',
})
const baselineJson = ref('')
/** 載入當下的狀態：更新時只有使用者真的改了才送出，避免蓋掉系統剛自動轉成的「額滿」。 */
const loadedStatus = ref<TrialStatus>('開放')
const enrolledCount = ref(0)
const syncToCalendar = ref(false)
const extraTeamOption = ref<{ id: string; name: string } | null>(null)

const teams = ref<AdminTeamAdminListItemDto[]>([])
const venues = ref<AdminVenueListItemDto[]>([])

const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
const formError = ref<string | null>(null)

const pageTitle = computed(() => (isCreate.value ? '新增試訓場次' : `編輯試訓場次：${form.trialOn || ''}`))

function applyDetail(d: TrialDetailDto) {
  form.teamId = d.teamId ?? ''
  form.venueId = d.venueId ?? ''
  form.trialOn = d.trialOn
  form.capacity = d.capacity ?? null
  form.deadlineOn = d.deadlineOn ?? ''
  form.status = d.status as TrialStatus
  form.audienceZh = d.zh?.audience ?? ''
  form.audienceEn = d.en?.audience ?? ''
  loadedStatus.value = d.status as TrialStatus
  enrolledCount.value = d.enrolledCount
  syncToCalendar.value = d.syncToCalendar
  extraTeamOption.value = d.teamId && d.teamName ? { id: d.teamId, name: d.teamName } : null
}

async function load() {
  loadState.value = 'loading'
  try {
    const [t, v] = await Promise.all([
      listAdminClubTeams(activeClubId.value).catch(() => [] as AdminTeamAdminListItemDto[]),
      listAdminVenues(activeClubId.value).catch(() => [] as AdminVenueListItemDto[]),
    ])
    teams.value = t
    venues.value = v
    if (!isCreate.value && trialId.value) {
      applyDetail(await getTrial(activeClubId.value, trialId.value))
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

const isDirty = computed(() => loadState.value === 'ready' && JSON.stringify(form) !== baselineJson.value)
useUnsavedChanges(isDirty)

const teamOptions = computed(() => {
  const list = teams.value.map((t) => ({ id: t.id, name: t.nameZh || '（未命名）' }))
  const extra = extraTeamOption.value
  if (extra && !list.some((t) => t.id === extra.id)) list.push(extra)
  return list
})

function validate(): boolean {
  formError.value = null
  if (!form.trialOn) return (formError.value = '請選擇試訓日'), false
  if (form.capacity !== null && form.capacity < 1) return (formError.value = '名額至少要 1 人；不設限請留白'), false
  if (form.capacity !== null && !isCreate.value && form.capacity < enrolledCount.value) {
    return (formError.value = `名額不能低於目前已報名的 ${enrolledCount.value} 人`), false
  }
  if (form.deadlineOn && form.deadlineOn > form.trialOn) return (formError.value = '報名截止日不能晚於試訓日'), false
  if (!form.audienceZh.trim()) return (formError.value = '請輸入中文的對象說明'), false
  if (form.audienceZh.length > 255 || form.audienceEn.length > 255) return (formError.value = '對象說明最多 255 字'), false
  return true
}

function buildPayload(): SaveTrialPayload {
  const payload: SaveTrialPayload = {
    teamId: form.teamId || null,
    venueId: form.venueId || null,
    trialOn: form.trialOn,
    capacity: form.capacity,
    deadlineOn: form.deadlineOn || null,
    content: {
      zh: { audience: form.audienceZh.trim() },
      en: enOrUndefined({ audience: form.audienceEn.trim() }, 'audience'),
    },
  }
  if (isCreate.value || form.status !== loadedStatus.value) payload.status = form.status
  return payload
}

async function handleSave() {
  if (readOnly.value || !validate()) return
  saving.value = true
  formError.value = null
  try {
    const wasCreate = isCreate.value
    const saved = wasCreate
      ? await createTrial(activeClubId.value, buildPayload())
      : await updateTrial(activeClubId.value, trialId.value!, buildPayload())
    if (wasCreate) {
      trialId.value = saved.id
      router.replace(`/programs/trials/${saved.id}/edit`)
    }
    applyDetail(saved)
    baselineJson.value = JSON.stringify(form)
    ElMessage.success('已儲存')
  } catch (error) {
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="trial-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="router.push('/programs/trials')"><el-icon><ArrowLeft /></el-icon>返回列表</el-button>
      </template>
      <template #meta><FrontendUnitBanner module-code="P4" /></template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這場試訓，可能已被刪除，或不屬於目前選擇的俱樂部。</p>
          <p v-else>{{ loadErrorMessage }}</p>
        </template>
        <el-button v-if="loadState === 'error'" type="primary" @click="load">重新載入</el-button>
        <el-button v-else type="primary" @click="router.push('/programs/trials')">返回列表</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="trial-edit__block" @close="formError = null" />
      <el-alert v-if="readOnly" title="你的帳號只能檢視試訓場次，不能修改。" type="info" show-icon :closable="false" class="trial-edit__block" />

      <el-form label-position="top" :disabled="readOnly">
        <el-card shadow="never" header="場次資料" class="trial-edit__block">
          <el-row :gutter="12">
            <el-col :xs="24" :sm="12">
              <el-form-item label="試訓日" required>
                <el-date-picker v-model="form.trialOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" />
              </el-form-item>
            </el-col>
            <el-col :xs="24" :sm="12">
              <el-form-item label="報名截止日">
                <el-date-picker v-model="form.deadlineOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" />
              </el-form-item>
            </el-col>
            <el-col :xs="24" :sm="12">
              <el-form-item label="球隊">
                <el-select v-model="form.teamId" clearable filterable placeholder="不選＝俱樂部整體試訓" style="width: 100%">
                  <el-option v-for="t in teamOptions" :key="t.id" :label="t.name" :value="t.id" />
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :xs="24" :sm="12">
              <el-form-item label="場地">
                <el-select v-model="form.venueId" clearable filterable placeholder="選填" style="width: 100%">
                  <el-option v-for="v in venues" :key="v.id" :label="v.nameZh" :value="v.id" />
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :xs="24" :sm="12">
              <el-form-item label="名額">
                <el-input-number v-model="form.capacity" :min="1" :step="1" controls-position="right" placeholder="不限" style="width: 100%" />
              </el-form-item>
            </el-col>
          </el-row>
          <p class="trial-edit__hint">名額留白代表不限。報名截止日不能晚於試訓日；名額不能低於目前已報名的人數{{ isCreate ? '' : `（目前 ${enrolledCount} 人）` }}。</p>
        </el-card>

        <el-card shadow="never" header="報名狀態" class="trial-edit__block">
          <el-form-item label="狀態">
            <el-select v-model="form.status" style="width: 200px; max-width: 100%">
              <el-option v-for="s in TRIAL_STATUS_ORDER" :key="s" :label="s" :value="s" />
            </el-select>
          </el-form-item>
          <p class="trial-edit__hint">報名人數達名額時，系統會自動把「開放」改為「額滿」；之後有人取消不會自動轉回，請人工決定是否重新開放。試訓日與截止日過後，前台會自動停止接受報名。</p>
        </el-card>

        <el-card shadow="never" header="前台顯示" class="trial-edit__block">
          <BilingualShortField
            label="對象說明"
            :zh="form.audienceZh"
            :en="form.audienceEn"
            required
            placeholder="例如：U12 以下男女生，歡迎第一次接觸足球"
            @update:zh="(v) => (form.audienceZh = v)"
            @update:en="(v) => (form.audienceEn = v)"
          />
          <p v-if="!isCreate" class="trial-edit__hint">
            是否同步到行事曆：目前{{ syncToCalendar ? '會' : '不會' }}。這由「行事曆分類設定」裡的總開關決定，不能逐場設定。
          </p>
          <p v-else class="trial-edit__hint">是否同步到行事曆由「行事曆分類設定」裡的總開關決定，不能逐場設定。</p>
        </el-card>
      </el-form>
      <EditActionBar v-if="!readOnly"><el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button></EditActionBar>
    </template>
  </div>
</template>

<style scoped>
.trial-edit { max-width: 780px; margin: 0 auto 88px; }
.trial-edit__block { margin-bottom: 16px; }
.trial-edit__hint { margin: 6px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
</style>

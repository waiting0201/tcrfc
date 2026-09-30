<script setup lang="ts">
/** 會籍方案——新增／編輯。對照 apps/api/README.md「B1」節「K2 會籍與方案」。 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { enOrUndefined, nullIfBlank } from '@/api/adminCommon'
import { listAdminSeasons } from '@/api/adminCompetitions'
import {
  createMembershipPlan,
  getMembershipPlan,
  updateMembershipPlan,
  type MembershipPlanDetailDto,
  type PlanStatus,
  type SavePlanPayload,
} from '@/api/adminMemberships'

const route = useRoute()
const router = useRouter()
// 🔴 必須是 computed（建立成功後 router.replace 同一元件不會重新掛載）
const isCreate = computed(() => route.name === 'membership-plan-new')
const planId = ref<string | undefined>(route.params.id as string | undefined)
const { canCreate, canUpdate } = useCrudPermissions('member.plan')

const form = reactive({
  seasonId: '',
  code: '',
  fee: 0,
  cardQuota: 1,
  jerseyQuota: 0,
  midSeasonRule: '',
  startsOn: '',
  endsOn: '',
  sortOrder: 0,
  status: 'draft' as PlanStatus,
  nameZh: '',
  nameEn: '',
  noteZh: '',
  noteEn: '',
})
const baselineJson = ref('')
const membershipCount = ref(0)
const seasonOptions = ref<{ id: string; code: string }[]>([])
const seasonsFailed = ref(false)

const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
const formError = ref<string | null>(null)

const readOnly = computed(() => (isCreate.value ? !canCreate.value : !canUpdate.value))
const pageTitle = computed(() => (isCreate.value ? '新增方案' : `編輯：${form.nameZh || '（未命名）'}`))
const seasonLocked = computed(() => !isCreate.value && membershipCount.value > 0)

function applyDetail(d: MembershipPlanDetailDto) {
  form.seasonId = d.seasonId
  form.code = d.code
  form.fee = d.fee
  form.cardQuota = d.cardQuota
  form.jerseyQuota = d.jerseyQuota
  form.midSeasonRule = d.midSeasonRule ?? ''
  form.startsOn = d.startsOn ?? ''
  form.endsOn = d.endsOn ?? ''
  form.sortOrder = d.sortOrder
  form.status = d.status
  form.nameZh = d.zh.name ?? ''
  form.nameEn = d.en?.name ?? ''
  form.noteZh = d.zh.benefitNote ?? ''
  form.noteEn = d.en?.benefitNote ?? ''
  membershipCount.value = d.membershipCount
  if (!seasonOptions.value.some((s) => s.id === d.seasonId)) seasonOptions.value.push({ id: d.seasonId, code: d.seasonCode })
}

async function load() {
  loadState.value = 'loading'
  try {
    const seasons = await listAdminSeasons(activeClubId.value).catch(() => null)
    seasonsFailed.value = seasons === null
    seasonOptions.value = (seasons ?? []).map((s) => ({ id: s.id, code: s.code }))
    if (!isCreate.value && planId.value) applyDetail(await getMembershipPlan(activeClubId.value, planId.value))
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

function validate(): boolean {
  formError.value = null
  if (!form.seasonId) return (formError.value = '請選擇球季'), false
  if (!form.nameZh.trim()) return (formError.value = '請輸入中文名稱'), false
  if (!/^[a-z0-9-]{1,32}$/.test(form.code.trim())) return (formError.value = '方案代號只能用小寫英文字母、數字與連字號，最多 32 字'), false
  if (form.fee === null || form.fee < 0) return (formError.value = '費用不能是負數'), false
  if (form.cardQuota < 1 || form.cardQuota > 10) return (formError.value = '會員卡數上限要在 1 到 10 之間'), false
  if (form.jerseyQuota < 0 || form.jerseyQuota > 10) return (formError.value = '球衣件數上限要在 0 到 10 之間'), false
  if (form.startsOn && form.endsOn && form.endsOn < form.startsOn) return (formError.value = '方案結束日不能早於開始日'), false
  return true
}

function buildPayload(): SavePlanPayload {
  return {
    seasonId: form.seasonId,
    code: form.code.trim(),
    fee: form.fee,
    cardQuota: form.cardQuota,
    jerseyQuota: form.jerseyQuota,
    midSeasonRule: nullIfBlank(form.midSeasonRule),
    startsOn: form.startsOn || null,
    endsOn: form.endsOn || null,
    sortOrder: form.sortOrder,
    status: form.status,
    content: {
      zh: { name: form.nameZh.trim(), benefitNote: nullIfBlank(form.noteZh) },
      en: enOrUndefined({ name: form.nameEn.trim(), benefitNote: nullIfBlank(form.noteEn) as string }, 'name', 'benefitNote'),
    },
  }
}

async function handleSave() {
  if (readOnly.value || !validate()) return
  saving.value = true
  formError.value = null
  try {
    const saved = isCreate.value
      ? await createMembershipPlan(activeClubId.value, buildPayload())
      : await updateMembershipPlan(activeClubId.value, planId.value!, buildPayload())
    if (isCreate.value) {
      planId.value = saved.id
      router.replace(`/members/plans/${saved.id}/edit`)
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

const backToList = () => router.push({ path: '/members/plans', query: { tab: 'plans' } })
</script>

<template>
  <div class="plan-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="backToList"><el-icon><ArrowLeft /></el-icon>返回列表</el-button>
      </template>
      <template #meta><FrontendUnitBanner module-code="K2" /></template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這個方案，可能已被刪除，或不屬於目前選擇的俱樂部。</p>
          <p v-else>{{ loadErrorMessage }}</p>
        </template>
        <el-button v-if="loadState === 'error'" type="primary" @click="load">重新載入</el-button>
        <el-button v-else type="primary" @click="backToList">返回列表</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="plan-edit__error" @close="formError = null" />
      <el-alert v-if="readOnly" title="你的帳號只能檢視方案，不能修改。" type="info" show-icon :closable="false" class="plan-edit__error" />
      <el-alert v-if="seasonsFailed" title="球季清單載入失敗，無法更換球季。" type="warning" show-icon :closable="false" class="plan-edit__error" />
      <el-form label-position="top" :disabled="readOnly">
        <el-card shadow="never" header="基本資料" class="plan-edit__section">
          <BilingualShortField label="方案名稱" :zh="form.nameZh" :en="form.nameEn" required @update:zh="(v) => (form.nameZh = v)" @update:en="(v) => (form.nameEn = v)" />
          <BilingualTextareaField label="權益說明" :zh="form.noteZh" :en="form.noteEn" @update:zh="(v) => (form.noteZh = v)" @update:en="(v) => (form.noteEn = v)" />
          <el-row :gutter="12">
            <el-col :xs="24" :sm="12">
              <el-form-item label="球季" required>
                <el-select v-model="form.seasonId" :disabled="seasonLocked" placeholder="選擇球季" style="width: 100%">
                  <el-option v-for="s in seasonOptions" :key="s.id" :label="s.code" :value="s.id" />
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :xs="24" :sm="12">
              <el-form-item label="方案代號（小寫英文、數字與連字號）" required>
                <el-input v-model="form.code" maxlength="32" placeholder="例如 single、family" />
              </el-form-item>
            </el-col>
          </el-row>
          <p class="plan-edit__hint">
            方案代號在同一球季內不能重複。已經有會籍或付款紀錄使用的方案，不能更換球季。
            <template v-if="seasonLocked">這個方案已有 {{ membershipCount }} 份會籍，球季已鎖定。</template>
          </p>
        </el-card>

        <el-card shadow="never" header="費用與額度" class="plan-edit__section">
          <el-row :gutter="12">
            <el-col :xs="24" :sm="8"><el-form-item label="費用（元）" required><el-input-number v-model="form.fee" :min="0" :precision="0" style="width: 100%" /></el-form-item></el-col>
            <el-col :xs="24" :sm="8"><el-form-item label="會員卡數上限（1–10）" required><el-input-number v-model="form.cardQuota" :min="1" :max="10" style="width: 100%" /></el-form-item></el-col>
            <el-col :xs="24" :sm="8"><el-form-item label="球衣件數上限（0–10）" required><el-input-number v-model="form.jerseyQuota" :min="0" :max="10" style="width: 100%" /></el-form-item></el-col>
          </el-row>
          <el-form-item label="期中加入規則">
            <el-input v-model="form.midSeasonRule" type="textarea" :rows="2" maxlength="500" show-word-limit placeholder="例如：球季中途加入，費用不打折" />
          </el-form-item>
          <el-row :gutter="12">
            <el-col :xs="24" :sm="12"><el-form-item label="方案開始日"><el-date-picker v-model="form.startsOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item></el-col>
            <el-col :xs="24" :sm="12"><el-form-item label="方案結束日"><el-date-picker v-model="form.endsOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item></el-col>
          </el-row>
          <p class="plan-edit__hint">開始日、結束日留空時，會籍期間依球季起訖計算。</p>
        </el-card>

        <el-card shadow="never" header="上架設定" class="plan-edit__section">
          <el-form-item label="狀態">
            <el-radio-group v-model="form.status">
              <el-radio value="published">上架</el-radio>
              <el-radio value="draft">下架</el-radio>
            </el-radio-group>
          </el-form-item>
          <p class="plan-edit__hint">只有上架的方案，會員才看得到，客服也才能用它手動開通。</p>
          <el-form-item label="排序值">
            <el-input-number v-model="form.sortOrder" :min="0" />
            <p class="plan-edit__hint">數字小的排前面；也可以在方案列表用上移、下移調整。</p>
          </el-form-item>
        </el-card>
      </el-form>
      <EditActionBar v-if="!readOnly"><el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button></EditActionBar>
    </template>
  </div>
</template>

<style scoped>
.plan-edit { max-width: 780px; margin: 0 auto 88px; }
.plan-edit__error { margin-bottom: 16px; }
.plan-edit__section { margin-bottom: 16px; }
.plan-edit__hint { margin: 6px 0 12px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
</style>

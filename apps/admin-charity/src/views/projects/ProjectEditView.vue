<script setup lang="ts">
/** N2 捐款項目管理 — 編輯頁（docs/22-charity-ui.md §3.7.2）：接真 API。 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import ErrorState from '@/components/ErrorState.vue'
import {
  createProject,
  getCharityRefs,
  getProject,
  publishProject,
  removeProjectCover,
  unpublishProject,
  updateProject,
  uploadProjectCover,
  type CharityRefOptions,
  type ProjectDetail,
  type ProjectInput,
} from '@/api/projects'
import { AdminApiError } from '@/api/http'
import { hasPermission } from '@/auth/session'

// 路由參數名稱沿用 projectKey，值現在是項目編號（API 的識別碼）。
const props = defineProps<{ projectKey?: string }>()
const router = useRouter()

const isEdit = computed(() => Boolean(props.projectKey))
const canSetShare = computed(() => hasPermission('n2.donation_project.share_pct'))
const canPublish = computed(() => hasPermission('n2.donation_project.publish'))

const loading = ref(false)
const loadError = ref('')
const saving = ref(false)
const existing = ref<ProjectDetail | null>(null)
const refs = ref<CharityRefOptions>({ charities: [], programs: [] })

const form = reactive({
  nameZh: '',
  nameEn: '',
  oneLinerZh: '',
  oneLinerEn: '',
  fundUsageZh: '',
  fundUsageEn: '',
  coverAltZh: '',
  coverAltEn: '',
  minAmount: 100 as number | null,
  maxAmount: 100000 as number | null,
  amountOptionsText: '300、500、1000、3000',
  sharePct: 0,
  charityRefCode: '' as string | null,
  programRefCode: '' as string | null,
  invoiceMode: 'b2c_invoice' as 'b2c_invoice' | 'donation_receipt',
  sortOrder: 0,
})

const coverUrl = ref<string | null>(null)
const pendingCover = ref<File | null>(null)
const coverRemoved = ref(false)

function fill(p: ProjectDetail) {
  existing.value = p
  form.nameZh = p.nameZh ?? ''
  form.nameEn = p.nameEn ?? ''
  form.oneLinerZh = p.oneLinerZh ?? ''
  form.oneLinerEn = p.oneLinerEn ?? ''
  form.fundUsageZh = p.fundUsageZh ?? ''
  form.fundUsageEn = p.fundUsageEn ?? ''
  form.coverAltZh = p.coverAltZh ?? ''
  form.coverAltEn = p.coverAltEn ?? ''
  form.minAmount = p.minAmount
  form.maxAmount = p.maxAmount
  form.amountOptionsText = p.amountOptions.join('、')
  form.sharePct = p.projectSharePct
  form.charityRefCode = p.charityRefCode
  form.programRefCode = p.charityProgramRefCode
  form.invoiceMode = p.invoiceMode
  form.sortOrder = p.sortOrder
  coverUrl.value = p.coverUrl
}

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    const [options, project] = await Promise.all([getCharityRefs(), props.projectKey ? getProject(props.projectKey) : Promise.resolve(null)])
    refs.value = options
    if (project) fill(project)
  } catch (error) {
    loadError.value = error instanceof AdminApiError ? error.detail : '讀取資料時發生問題'
  } finally {
    loading.value = false
  }
}

onMounted(load)

const programOptions = computed(() => refs.value.programs.filter((p) => p.charityRefCode === form.charityRefCode))

function onCharityChange() {
  // 換了公益機構，原本選的計畫若不屬於它就清掉（後端也會擋「組合不一致」）。
  if (!programOptions.value.some((p) => p.refCode === form.programRefCode)) form.programRefCode = ''
}

function handleCoverUpdate(file: File | null) {
  if (file) {
    pendingCover.value = file
    coverRemoved.value = false
  } else {
    pendingCover.value = null
    coverRemoved.value = true
    coverUrl.value = null
  }
}

function blank(value: string): string | null {
  const v = value.trim()
  return v === '' ? null : v
}

function parseAmountOptions(): number[] | null {
  const parts = form.amountOptionsText.split(/[、,，\s]+/).map((s) => s.trim()).filter(Boolean)
  const nums = parts.map(Number)
  if (nums.some((n) => !Number.isInteger(n) || n <= 0)) return null
  return nums
}

async function handleSave() {
  if (saving.value) return
  if (!form.nameZh.trim()) {
    ElMessage.warning('請填寫項目名稱（中文）')
    return
  }
  const options = parseAmountOptions()
  if (options === null) {
    ElMessage.warning('金額選項請填正整數，以頓號分隔')
    return
  }
  const input: ProjectInput = {
    nameZh: form.nameZh.trim(),
    nameEn: blank(form.nameEn),
    oneLinerZh: blank(form.oneLinerZh),
    oneLinerEn: blank(form.oneLinerEn),
    // 說明內文（區塊編輯器）本畫面不提供編輯；更新是整筆取代，所以原樣送回，避免被清空。
    descriptionZh: existing.value?.descriptionZh ?? null,
    descriptionEn: existing.value?.descriptionEn ?? null,
    fundUsageZh: blank(form.fundUsageZh),
    fundUsageEn: blank(form.fundUsageEn),
    coverAltZh: blank(form.coverAltZh),
    coverAltEn: blank(form.coverAltEn),
    minAmount: form.minAmount,
    maxAmount: form.maxAmount,
    amountOptions: options,
    invoiceMode: form.invoiceMode,
    charityRefCode: blank(form.charityRefCode ?? ''),
    charityProgramRefCode: blank(form.programRefCode ?? ''),
    sortOrder: form.sortOrder,
  }
  if (canSetShare.value) input.projectSharePct = form.sharePct

  saving.value = true
  try {
    const saved = isEdit.value && props.projectKey ? await updateProject(props.projectKey, input) : await createProject(input)
    try {
      if (pendingCover.value) await uploadProjectCover(saved.id, pendingCover.value)
      else if (coverRemoved.value && existing.value?.coverUrl) await removeProjectCover(saved.id)
    } catch (error) {
      ElMessage.error(`項目資料已儲存，但封面圖處理失敗：${error instanceof AdminApiError ? error.detail : '請稍後再試'}`)
      if (!isEdit.value) await router.replace(`/projects/${saved.id}/edit`)
      else fill(await getProject(saved.id))
      return
    }
    ElMessage.success(isEdit.value ? '已儲存捐款項目' : '已新增捐款項目（尚未上架，儲存後可在列表或這個畫面上架）')
    await router.push('/projects')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.detail : '儲存失敗，請稍後再試')
  } finally {
    saving.value = false
  }
}

async function togglePublish() {
  if (!existing.value) return
  try {
    const updated = existing.value.status === 'published' ? await unpublishProject(existing.value.id) : await publishProject(existing.value.id)
    existing.value = updated
    ElMessage.success(updated.status === 'published' ? '已上架' : '已下架')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.detail : '操作失敗')
  }
}
</script>

<template>
  <div>
    <PageHeader :title="isEdit ? '編輯捐款項目' : '新增捐款項目'" frontend-unit="項目詳情頁">
      <template #back>
        <el-button text @click="router.push('/projects')">返回列表</el-button>
      </template>
    </PageHeader>

    <ErrorState v-if="loadError" :text="loadError" @retry="load" />
    <el-form v-else v-loading="loading" label-position="top" class="project-edit__form">
      <h2 class="project-edit__section-title">基本資訊</h2>
      <BilingualShortField label="項目名稱" :zh="form.nameZh" :en="form.nameEn" required @update:zh="form.nameZh = $event" @update:en="form.nameEn = $event" />
      <BilingualShortField label="一句話說明" :zh="form.oneLinerZh" :en="form.oneLinerEn" @update:zh="form.oneLinerZh = $event" @update:en="form.oneLinerEn = $event" />

      <h2 class="project-edit__section-title">內容</h2>
      <el-tabs>
        <el-tab-pane label="款項用途（中文）">
          <el-input v-model="form.fundUsageZh" type="textarea" :rows="4" />
        </el-tab-pane>
        <el-tab-pane label="款項用途（英文）">
          <el-input v-model="form.fundUsageEn" type="textarea" :rows="4" />
        </el-tab-pane>
      </el-tabs>

      <h2 class="project-edit__section-title">封面圖</h2>
      <ImageUploader :existing-url="coverUrl" variant="photo" :saving="saving" @update:file="handleCoverUpdate" />
      <BilingualShortField label="封面圖替代文字" :zh="form.coverAltZh" :en="form.coverAltEn" @update:zh="form.coverAltZh = $event" @update:en="form.coverAltEn = $event" />

      <h2 class="project-edit__section-title">金額設定</h2>
      <el-row :gutter="16">
        <el-col :sm="12" :xs="24">
          <el-form-item label="最低金額">
            <el-input-number v-model="form.minAmount" :min="1" :value-on-clear="null" style="width: 100%" />
          </el-form-item>
        </el-col>
        <el-col :sm="12" :xs="24">
          <el-form-item label="最高金額">
            <el-input-number v-model="form.maxAmount" :min="form.minAmount ?? 1" :value-on-clear="null" style="width: 100%" />
          </el-form-item>
        </el-col>
      </el-row>
      <el-form-item label="金額選項卡（以頓號分隔）">
        <el-input v-model="form.amountOptionsText" placeholder="例如：300、500、1000、3000" />
      </el-form-item>

      <h2 class="project-edit__section-title">分潤與撥付對象</h2>
      <el-form-item label="項目分潤比例（%）">
        <el-input-number v-if="canSetShare" v-model="form.sharePct" :min="0" :max="100" :step="0.5" :precision="2" />
        <span v-else>{{ form.sharePct }}%（設定分潤需要額外授權，請洽系統管理員）</span>
      </el-form-item>
      <p class="project-edit__validation">店家分潤與項目分潤相加不得超過 100%，儲存時會依合作中店家中分潤最高的一家檢查。</p>
      <el-form-item label="撥付對象（公益機構）">
        <el-select v-model="form.charityRefCode" style="width: 100%" clearable @change="onCharityChange">
          <el-option v-for="c in refs.charities" :key="c.refCode" :value="c.refCode" :label="c.name" />
        </el-select>
      </el-form-item>
      <el-form-item label="撥付對象（公益計畫，選填）">
        <el-select v-model="form.programRefCode" style="width: 100%" clearable>
          <el-option v-for="p in programOptions" :key="p.refCode" :value="p.refCode" :label="p.name" />
        </el-select>
      </el-form-item>
      <p class="project-edit__hint">此清單來自官方網站的公益團體資料（唯讀複本），如需新增或更新請聯絡官網管理端</p>

      <h2 class="project-edit__section-title">憑證模式</h2>
      <el-form-item>
        <el-radio-group v-model="form.invoiceMode">
          <el-radio value="b2c_invoice">電子發票</el-radio>
          <el-radio value="donation_receipt">捐贈收據</el-radio>
        </el-radio-group>
      </el-form-item>

      <h2 v-if="isEdit && canPublish" class="project-edit__section-title">上下架</h2>
      <el-form-item v-if="isEdit && canPublish">
        <el-tag :type="existing?.status === 'published' ? 'success' : 'info'" style="margin-right: 12px">
          {{ existing?.status === 'published' ? '目前已上架，前台看得到' : '目前未上架，前台看不到' }}
        </el-tag>
        <el-button @click="togglePublish">{{ existing?.status === 'published' ? '下架' : '上架' }}</el-button>
      </el-form-item>

      <h2 class="project-edit__section-title">排序</h2>
      <el-form-item label="排序（數字越小越前面）">
        <el-input-number v-model="form.sortOrder" :min="0" />
      </el-form-item>

      <div class="project-edit__actions">
        <el-button @click="router.push('/projects')">取消</el-button>
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </div>
    </el-form>
  </div>
</template>

<style scoped>
.project-edit__form {
  max-width: 760px;
}

.project-edit__section-title {
  font-size: 15px;
  color: var(--charity-admin-text-primary);
  margin: var(--charity-admin-space-6) 0 var(--charity-admin-space-3);
  padding-top: var(--charity-admin-space-3);
  border-top: 1px solid var(--charity-admin-border);
}

.project-edit__section-title:first-child {
  margin-top: 0;
  padding-top: 0;
  border-top: none;
}

.project-edit__validation {
  margin: -8px 0 16px;
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
}

.project-edit__validation--error {
  color: var(--charity-danger-text);
  font-weight: 600;
}

.project-edit__hint {
  margin: -8px 0 16px;
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
}

.project-edit__actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--charity-admin-space-2);
  margin-top: var(--charity-admin-space-6);
  padding-top: var(--charity-admin-space-4);
  border-top: 1px solid var(--charity-admin-border);
}
</style>

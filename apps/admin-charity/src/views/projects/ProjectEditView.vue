<script setup lang="ts">
/**
 * N2 捐款項目管理 — 編輯頁（docs/22-charity-ui.md §3.7.2）：接真 API。
 * 版面依 docs/21 §3／§3.3a 與 docs/22 §3.10：整頁一組語言分頁、主欄一張卡、右側欄「基本設定」＋「發布設定」，
 * 驗證一次檢查全部並標到欄位。欄位鍵沿用 ProjectInput 的屬性名（後端之後補鍵時直接對得上）。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import EditLayout from '@/components/EditLayout.vue'
import FormErrorStatus from '@/components/FormErrorStatus.vue'
import FormField from '@/components/FormField.vue'
import FormSection from '@/components/FormSection.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import ErrorState from '@/components/ErrorState.vue'
import CharityRefSyncPanel from '@/components/CharityRefSyncPanel.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
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

const formErrors = provideFormErrors()
/** 只放沒有欄位歸屬的錯誤（API 回來的訊息）；前端驗證一律進 formErrors。 */
const formError = ref<string | null>(null)

const loading = ref(false)
const loadError = ref('')
const saving = ref(false)
const existing = ref<ProjectDetail | null>(null)
const refs = ref<CharityRefOptions>({ charities: [], programs: [], lastSyncedAt: null })

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
    const [options, project] = await Promise.all([getCharityRefs(props.projectKey), props.projectKey ? getProject(props.projectKey) : Promise.resolve(null)])
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

/** 選單文字：官網已不再公開者加註，且在選單中不可再被新選（只讓既有選擇看得到）。 */
const INACTIVE_NOTE = '（官網已不再公開）'
const optionLabel = (o: { name: string; isActive: boolean }) => (o.isActive ? o.name : `${o.name}${INACTIVE_NOTE}`)

const selectedCharity = computed(() => refs.value.charities.find((c) => c.refCode === form.charityRefCode))
const selectedProgram = computed(() => refs.value.programs.find((p) => p.refCode === form.programRefCode))
const inactiveSelected = computed(() => [selectedCharity.value, selectedProgram.value].filter((o) => o && !o.isActive).map((o) => o!.name))

/**
 * 更新清單後重新載入選單。帶的是「已儲存」的項目編號，但表單上可能有尚未儲存的選擇剛好被官網下架，
 * 所以新清單裡找不到的目前選擇，沿用舊清單的資料並標為已不再公開，避免選單退化成顯示內部代碼。
 */
async function reloadRefs() {
  const previous = refs.value
  try {
    const fresh = await getCharityRefs(props.projectKey)
    const keepCharity = previous.charities.find((c) => c.refCode === form.charityRefCode)
    if (keepCharity && !fresh.charities.some((c) => c.refCode === keepCharity.refCode)) fresh.charities.push({ ...keepCharity, isActive: false })
    const keepProgram = previous.programs.find((p) => p.refCode === form.programRefCode)
    if (keepProgram && !fresh.programs.some((p) => p.refCode === keepProgram.refCode)) fresh.programs.push({ ...keepProgram, isActive: false })
    refs.value = fresh
  } catch {
    // 清單重新載入失敗不影響已完成的更新；保留畫面上原有選單。
    ElMessage.warning('清單已更新，但選單暫時無法重新載入，請重新整理本頁')
  }
}

function onCharityChange() {
  // 換了公益機構，原本選的計畫若不屬於它就清掉（後端也會擋「組合不一致」）。
  if (!programOptions.value.some((p) => p.refCode === form.programRefCode)) form.programRefCode = ''
}

function handleCoverUpdate(file: File | null) {
  formErrors.clear('cover')
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

/** 一次檢查全部（欄位鍵 → 訊息），不要遇到第一個就停。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!form.nameZh.trim()) errors.nameZh = '請填寫項目名稱（中文）'
  const options = parseAmountOptions()
  if (options === null) errors.amountOptions = '金額選項請填正整數，以頓號分隔'
  if (form.minAmount !== null && form.maxAmount !== null && form.minAmount > form.maxAmount) {
    errors.maxAmount = '最高金額不能小於最低金額'
  }
  return errors
}

async function handleSave() {
  if (saving.value) return
  formError.value = null
  if (formErrors.replaceAll(validate())) {
    await formErrors.focusFirst()
    return
  }
  const options = parseAmountOptions() ?? []
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
    // 後端有標欄位就標到欄位；對不到（或沒有欄位資訊）才退回頁首提示
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    formError.value = error instanceof AdminApiError ? error.detail : '儲存失敗，請稍後再試'
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
    <el-form v-else v-loading="loading" label-position="top" @submit.prevent>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="project-edit__alert" @close="formError = null" />

      <LangTabsBar>
        <EditLayout>
          <template #main>
            <el-card shadow="never">
              <FormSection>
                <BilingualShortField field="name" label="項目名稱" required :zh="form.nameZh" :en="form.nameEn" @update:zh="form.nameZh = $event" @update:en="form.nameEn = $event" />
                <template v-if="!isEdit">
                  <BilingualShortField field="oneLiner" label="一句話說明" :zh="form.oneLinerZh" :en="form.oneLinerEn" @update:zh="form.oneLinerZh = $event" @update:en="form.oneLinerEn = $event" />
                  <BilingualTextareaField
                    field="fundUsage"
                    label="款項用途"
                    :rows="4"
                    hint="項目說明的圖文排版，儲存後可到「編輯內文」補上。"
                    :zh="form.fundUsageZh"
                    :en="form.fundUsageEn"
                    @update:zh="form.fundUsageZh = $event"
                    @update:en="form.fundUsageEn = $event"
                  />
                </template>
              </FormSection>
              <FormSection v-if="isEdit && existing" title="內文">
                <p class="project-edit__hint">一句話介紹、項目說明與善款用途（中英文）在獨立的畫面編輯，這裡儲存時不會更動它們。</p>
                <el-button @click="router.push(`/projects/${existing.id}/content`)">編輯內文</el-button>
              </FormSection>
            </el-card>
          </template>

          <template #aside>
            <el-card shadow="never" header="基本設定">
              <FormSection title="封面圖片">
                <FormField field="cover">
                  <ImageUploader :existing-url="coverUrl" variant="photo" :saving="saving" @update:file="handleCoverUpdate" />
                </FormField>
                <BilingualShortField field="coverAlt" label="封面圖替代文字" :zh="form.coverAltZh" :en="form.coverAltEn" @update:zh="form.coverAltZh = $event" @update:en="form.coverAltEn = $event" />
              </FormSection>

              <FormSection title="金額設定">
                <FormField field="minAmount" label="最低金額">
                  <el-input-number v-model="form.minAmount" :min="1" :value-on-clear="null" style="width: 100%" />
                </FormField>
                <FormField field="maxAmount" label="最高金額">
                  <el-input-number v-model="form.maxAmount" :min="form.minAmount ?? 1" :value-on-clear="null" style="width: 100%" />
                </FormField>
                <FormField field="amountOptions" label="金額選項卡（以頓號分隔）">
                  <el-input v-model="form.amountOptionsText" placeholder="例如：300、500、1000、3000" />
                </FormField>
              </FormSection>

              <FormSection title="分潤與撥付對象">
                <FormField field="projectSharePct" label="項目分潤比例（%）">
                  <el-input-number v-if="canSetShare" v-model="form.sharePct" :min="0" :max="100" :step="0.5" :precision="2" />
                  <span v-else>{{ form.sharePct }}%（設定分潤需要額外授權，請洽系統管理員）</span>
                </FormField>
                <p class="project-edit__hint">店家分潤與項目分潤相加不得超過 100%，儲存時會依合作中店家中分潤最高的一家檢查。</p>
                <FormField field="charityRefCode" label="撥付對象（公益機構）">
                  <el-select v-model="form.charityRefCode" style="width: 100%" clearable placeholder="請選擇公益機構" @change="onCharityChange">
                    <el-option v-for="c in refs.charities" :key="c.refCode" :value="c.refCode" :label="optionLabel(c)" :disabled="!c.isActive">
                      <span class="project-edit__opt-name">{{ optionLabel(c) }}</span>
                      <span v-if="c.nameEn" class="project-edit__opt-sub">{{ c.nameEn }}</span>
                    </el-option>
                  </el-select>
                </FormField>
                <FormField field="charityProgramRefCode" label="撥付對象（公益計畫，選填）">
                  <el-select v-model="form.programRefCode" style="width: 100%" clearable placeholder="請選擇公益計畫">
                    <el-option v-for="p in programOptions" :key="p.refCode" :value="p.refCode" :label="optionLabel(p)" :disabled="!p.isActive">
                      <span class="project-edit__opt-name">{{ optionLabel(p) }}</span>
                      <span v-if="p.nameEn" class="project-edit__opt-sub">{{ p.nameEn }}</span>
                    </el-option>
                  </el-select>
                </FormField>
                <p v-if="inactiveSelected.length > 0" class="project-edit__warn" role="status">
                  <span aria-hidden="true">⚠ </span>「{{ inactiveSelected.join('」、「') }}」官網已不再公開。這個項目可以照舊儲存，但新的項目無法再選它，建議改選其他對象。
                </p>
                <p class="project-edit__hint">這份清單來自官方網站，這裡不能直接新增或修改。找不到要的公益機構或計畫時，請先到官網發布，再按下方的「從官網更新清單」。</p>
                <CharityRefSyncPanel :last-synced-at="refs.lastSyncedAt" @synced="reloadRefs" />
              </FormSection>

              <FormSection title="憑證模式">
                <FormField field="invoiceMode">
                  <el-radio-group v-model="form.invoiceMode">
                    <el-radio value="b2c_invoice">電子發票</el-radio>
                    <el-radio value="donation_receipt">捐贈收據</el-radio>
                  </el-radio-group>
                </FormField>
              </FormSection>
            </el-card>

            <el-card shadow="never" header="發布設定">
              <FormSection v-if="isEdit && canPublish" title="上下架">
                <el-tag :type="existing?.status === 'published' ? 'success' : 'info'" class="project-edit__status-tag">
                  <span v-if="existing?.status === 'published'" aria-hidden="true">✓ </span>{{ existing?.status === 'published' ? '目前已上架，前台看得到' : '目前未上架，前台看不到' }}
                </el-tag>
                <el-button @click="togglePublish">{{ existing?.status === 'published' ? '下架' : '上架' }}</el-button>
              </FormSection>
              <FormSection title="排序">
                <FormField field="sortOrder" label="排序（數字越小越前面）">
                  <el-input-number v-model="form.sortOrder" :min="0" />
                </FormField>
              </FormSection>
            </el-card>
          </template>
        </EditLayout>
      </LangTabsBar>

      <EditActionBar>
        <template #status><FormErrorStatus /></template>
        <el-button @click="router.push('/projects')">取消</el-button>
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </EditActionBar>
    </el-form>
  </div>
</template>

<style scoped>
.project-edit__alert {
  margin-bottom: var(--charity-admin-space-3);
}

.project-edit__hint {
  margin: 0 0 var(--charity-admin-space-3);
  font-size: 12px;
  line-height: 1.6;
  color: var(--charity-admin-text-tertiary);
}

.project-edit__warn {
  margin: 0 0 var(--charity-admin-space-3);
  font-size: 13px;
  line-height: 1.6;
  color: var(--charity-warning-text);
}

.project-edit__opt-sub {
  margin-left: var(--charity-admin-space-2);
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
}

.project-edit__status-tag {
  margin: 0 var(--charity-admin-space-3) var(--charity-admin-space-2) 0;
  white-space: normal;
  height: auto;
}
</style>

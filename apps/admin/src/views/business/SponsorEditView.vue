<script setup lang="ts">
/**
 * 贊助商——新增／編輯，含贊助活動與圖集（前台「合作夥伴與贊助」的贊助商區塊）。
 * 聯絡窗口與合約日期只在後台使用，前台不會顯示。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessageBox, ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import EditLayout from '@/components/EditLayout.vue'
import FormErrorStatus from '@/components/FormErrorStatus.vue'
import FormField from '@/components/FormField.vue'
import FormSection from '@/components/FormSection.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import SponsorActivationDialog from './SponsorActivationDialog.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { enOrUndefined, imageIntent, nullIfBlank } from '@/api/adminCommon'
import NewsPicker from '@/components/NewsPicker.vue'
import {
  createSponsor,
  deleteActivation,
  getSponsor,
  listActivations,
  listSponsorPackages,
  updateSponsor,
  type ActivationDto,
  type SaveSponsorPayload,
  type SponsorDetailDto,
  type SponsorPackageListItemDto,
} from '@/api/adminSponsors'

const route = useRoute()
const router = useRouter()
const isCreate = computed(() => route.name === 'sponsor-new')
const sponsorId = ref<string | undefined>(route.params.id as string | undefined)
const { canCreate, canUpdate } = useCrudPermissions('business.sponsor')
const club = computed(() => activeClubId.value)
const TIERS = ['主贊助', '官方贊助', '支持夥伴']

const form = reactive({
  tier: '', contractStartOn: '', contractEndOn: '', expiryAlertOn: '',
  contactName: '', contactPhone: '', contactEmail: '',
  sortOrder: 0,
  nameZh: '', nameEn: '', contentZh: '', contentEn: '',
  packageIds: [] as string[],
  articleIds: [] as string[],
})
const baselineJson = ref('')
const darkFile = ref<File | null>(null)
const lightFile = ref<File | null>(null)
const removeDark = ref(false)
const removeLight = ref(false)
const darkUrl = ref<string | null>(null)
const lightUrl = ref<string | null>(null)
const hasDark = ref(false)
const hasLight = ref(false)
const contractStatus = ref<string>('none')

const packageOptions = ref<SponsorPackageListItemDto[]>([])
const articleSeed = ref<{ id: string; label: string; status?: string }[]>([])

const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
/** 頁首提示：只放沒有欄位歸屬的錯誤；欄位錯誤標在欄位上（formErrors）。 */
const formError = ref<string | null>(null)
const formErrors = provideFormErrors()
// 選檔或移除標誌就清掉該欄位的錯誤
watch([darkFile, removeDark], () => formErrors.clear('logoDark'))
watch([lightFile, removeLight], () => formErrors.clear('logoLight'))
const readOnly = computed(() => (isCreate.value ? !canCreate.value : !canUpdate.value))
const pageTitle = computed(() => (isCreate.value ? '新增贊助商' : `編輯：${form.nameZh || '（未命名）'}`))

const CONTRACT_LABEL: Record<string, string> = { none: '未設定合約', active: '合約進行中', alert: '即將到期', expired: '合約已結束' }

function apply(d: SponsorDetailDto) {
  form.tier = d.tier ?? ''
  form.contractStartOn = d.contractStartOn ?? ''
  form.contractEndOn = d.contractEndOn ?? ''
  form.expiryAlertOn = d.expiryAlertOn ?? ''
  form.contactName = d.contactName ?? ''
  form.contactPhone = d.contactPhone ?? ''
  form.contactEmail = d.contactEmail ?? ''
  form.sortOrder = d.sortOrder
  form.nameZh = d.zh.name ?? ''
  form.nameEn = d.en?.name ?? ''
  form.contentZh = d.zh.content ?? ''
  form.contentEn = d.en?.content ?? ''
  form.packageIds = d.packages.map((p) => p.id)
  form.articleIds = d.articles.map((a) => a.id)
  darkUrl.value = d.logoDarkUrl ?? null
  lightUrl.value = d.logoLightUrl ?? null
  hasDark.value = !!d.logoDarkKey
  hasLight.value = !!d.logoLightKey
  contractStatus.value = d.contractStatus
  articleSeed.value = d.articles.map((a) => ({ id: a.id, label: a.titleZh || a.slug, status: a.status }))
}

async function load() {
  loadState.value = 'loading'
  try {
    packageOptions.value = await listSponsorPackages(club.value).catch(() => [] as SponsorPackageListItemDto[])
    if (!isCreate.value && sponsorId.value) {
      apply(await getSponsor(club.value, sponsorId.value))
      await loadActivations()
    }
    darkFile.value = lightFile.value = null
    removeDark.value = removeLight.value = false
    baselineJson.value = JSON.stringify(form)
    loadState.value = 'ready'
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'not-found') loadState.value = 'not-found'
    else {
      loadErrorMessage.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
      loadState.value = 'error'
    }
  }
}
onMounted(load)

const isDirty = computed(
  () => loadState.value === 'ready' && (JSON.stringify(form) !== baselineJson.value || !!darkFile.value || !!lightFile.value || removeDark.value || removeLight.value),
)
useUnsavedChanges(isDirty)

/** 一次檢查全部，回傳 欄位鍵 → 訊息（鍵只在程式內對照，不顯示）。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!form.nameZh.trim()) errors.nameZh = '請輸入中文名稱'
  if (!form.tier) errors.tier = '請選擇贊助等級'
  if (form.contractStartOn && form.contractEndOn && form.contractEndOn < form.contractStartOn) errors.contractEndOn = '合約結束日不能早於開始日'
  if (form.expiryAlertOn && form.contractEndOn && form.expiryAlertOn > form.contractEndOn) errors.expiryAlertOn = '到期提醒日不能晚於合約結束日'
  if (form.contactEmail.trim() && !/^\S+@\S+\.\S+$/.test(form.contactEmail.trim())) errors.contactEmail = '聯絡 Email 格式不正確'
  return errors
}

function buildPayload(): SaveSponsorPayload {
  const dark = imageIntent(darkFile.value, removeDark.value)
  const light = imageIntent(lightFile.value, removeLight.value)
  return {
    tier: form.tier,
    contractStartOn: form.contractStartOn || null,
    contractEndOn: form.contractEndOn || null,
    expiryAlertOn: form.expiryAlertOn || null,
    contactName: nullIfBlank(form.contactName),
    contactPhone: nullIfBlank(form.contactPhone),
    contactEmail: nullIfBlank(form.contactEmail),
    sortOrder: form.sortOrder,
    content: {
      zh: { name: form.nameZh.trim(), content: nullIfBlank(form.contentZh) },
      en: enOrUndefined({ name: form.nameEn.trim(), content: nullIfBlank(form.contentEn) as string }, 'name', 'content'),
    },
    // 關聯陣列語意：一律明確帶出畫面上的完整清單（空陣列＝清空）
    packageIds: form.packageIds,
    articleIds: form.articleIds,
    removeLogoDark: dark.remove,
    removeLogoLight: light.remove,
  }
}

async function handleSave() {
  if (readOnly.value) return
  formError.value = null
  if (formErrors.replaceAll(validate())) {
    await formErrors.focusFirst()
    return
  }
  saving.value = true
  try {
    const files = { logoDark: darkFile.value, logoLight: lightFile.value }
    const saved = isCreate.value
      ? await createSponsor(club.value, buildPayload(), files)
      : await updateSponsor(club.value, sponsorId.value!, buildPayload(), files)
    if (isCreate.value) {
      sponsorId.value = saved.id
      router.replace(`/business/sponsorships/sponsors/${saved.id}/edit`)
    }
    apply(saved)
    darkFile.value = lightFile.value = null
    removeDark.value = removeLight.value = false
    baselineJson.value = JSON.stringify(form)
    ElMessage.success('已儲存')
  } catch (error) {
    // 後端標到欄位的錯誤直接標在欄位上；對不到欄位的才放頁首
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

function back() {
  router.push({ path: '/business/sponsorships', query: { tab: 'sponsors' } })
}

// ── 贊助活動 ──
const activations = ref<ActivationDto[]>([])
const activationDialog = ref(false)
const editingActivation = ref<ActivationDto | null>(null)

async function loadActivations() {
  if (!sponsorId.value) return
  activations.value = await listActivations(club.value, sponsorId.value)
}

function openActivation(a: ActivationDto | null) {
  editingActivation.value = a
  activationDialog.value = true
}

async function removeActivation(a: ActivationDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除活動「${a.zh.title}」嗎？活動圖片會一併刪除，無法復原。`, '刪除贊助活動', {
      confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteActivation(club.value, sponsorId.value!, a.id)
    await loadActivations()
    ElMessage.success('已刪除')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
  }
}
</script>

<template>
  <div class="sponsor-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="back"><el-icon><ArrowLeft /></el-icon>返回列表</el-button>
      </template>
      <template #meta><FrontendUnitBanner module-code="E2" /></template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這位贊助商，可能已被刪除，或不屬於目前選擇的俱樂部。</p>
          <p v-else>{{ loadErrorMessage }}</p>
        </template>
        <el-button v-if="loadState === 'error'" type="primary" @click="load">重新載入</el-button>
        <el-button v-else type="primary" @click="back">返回列表</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="sponsor-edit__block" @close="formError = null" />
      <el-alert v-if="readOnly" title="你的帳號只能檢視贊助商資料，不能修改。" type="info" show-icon :closable="false" class="sponsor-edit__block" />
      <LangTabsBar>
        <EditLayout>
          <template #main>
            <el-card shadow="never">
              <FormSection>
                <el-form label-position="top" :disabled="readOnly" class="sponsor-edit__form">
                  <BilingualShortField field="name" label="名稱" :zh="form.nameZh" :en="form.nameEn" required @update:zh="(v) => (form.nameZh = v)" @update:en="(v) => (form.nameEn = v)" />
                  <BilingualTextareaField field="content" label="贊助內容" :zh="form.contentZh" :en="form.contentEn" @update:zh="(v) => (form.contentZh = v)" @update:en="(v) => (form.contentEn = v)" />
                </el-form>
              </FormSection>

              <FormSection title="贊助方案與贊助故事">
                <el-form label-position="top" :disabled="readOnly" class="sponsor-edit__form">
                  <FormField field="packageIds" label="包含的贊助方案">
                    <el-select v-model="form.packageIds" multiple filterable placeholder="選擇這位贊助商所屬的方案" style="width: 100%">
                      <el-option v-for="p in packageOptions" :key="p.id" :label="p.nameZh || p.slug" :value="p.id" />
                    </el-select>
                  </FormField>
                  <FormField field="articleIds" label="贊助故事（關聯文章）">
                    <NewsPicker v-model="form.articleIds" :seed="articleSeed" :disabled="readOnly" />
                    <p class="sponsor-edit__hint">可用關鍵字搜尋所有文章；前台只顯示已發布的文章。</p>
                  </FormField>
                </el-form>
              </FormSection>

              <FormSection title="贊助活動與圖集">
                <p v-if="isCreate" class="sponsor-edit__hint">請先儲存贊助商基本資料，儲存後就能新增贊助活動與圖片。</p>
                <template v-else>
                  <div v-if="canUpdate" class="sponsor-edit__head">
                    <el-button size="small" type="primary" @click="openActivation(null)">+ 新增活動</el-button>
                  </div>
                  <el-empty v-if="activations.length === 0" description="還沒有贊助活動" :image-size="64" />
                  <div v-else class="sponsor-edit__activations">
                    <div v-for="a in activations" :key="a.id" class="sponsor-edit__activation">
                      <div class="sponsor-edit__activation-main">
                        <strong>{{ a.zh.title }}</strong>
                        <span class="sponsor-edit__hint">{{ a.happenedOn || '未填日期' }}・{{ a.images.length }} 張圖片</span>
                      </div>
                      <div>
                        <el-button size="small" text type="primary" @click="openActivation(a)">{{ canUpdate ? '編輯' : '檢視' }}</el-button>
                        <el-button v-if="canUpdate" size="small" text type="danger" @click="removeActivation(a)">刪除</el-button>
                      </div>
                    </div>
                  </div>
                </template>
              </FormSection>
            </el-card>
          </template>

          <template #aside>
            <el-form label-position="top" :disabled="readOnly" class="sponsor-edit__form">
              <el-card shadow="never" header="基本設定">
                <FormSection>
                  <FormField field="tier" label="贊助等級" required>
                    <el-select v-model="form.tier" placeholder="請選擇" style="width: 100%" @change="formErrors.clear('tier')"><el-option v-for="t in TIERS" :key="t" :label="t" :value="t" /></el-select>
                  </FormField>
                </FormSection>
                <FormSection title="合約與聯絡窗口">
                  <p v-if="!isCreate" class="sponsor-edit__hint">合約狀態：<el-tag size="small">{{ CONTRACT_LABEL[contractStatus] }}</el-tag></p>
                  <p class="sponsor-edit__hint">這一區只在後台看得到，不會出現在前台。合約結束後，這位贊助商會自動從前台隱藏。</p>
                  <el-form-item label="合約開始日"><el-date-picker v-model="form.contractStartOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" @change="formErrors.clear('contractEndOn')" /></el-form-item>
                  <FormField field="contractEndOn" label="合約結束日"><el-date-picker v-model="form.contractEndOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" @change="formErrors.clear('contractEndOn'); formErrors.clear('expiryAlertOn')" /></FormField>
                  <FormField field="expiryAlertOn" label="到期提醒日"><el-date-picker v-model="form.expiryAlertOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" @change="formErrors.clear('expiryAlertOn')" /></FormField>
                  <FormField field="contactName" label="聯絡人"><el-input v-model="form.contactName" maxlength="64" /></FormField>
                  <FormField field="contactPhone" label="聯絡電話"><el-input v-model="form.contactPhone" maxlength="32" /></FormField>
                  <FormField field="contactEmail" label="聯絡 Email"><el-input v-model="form.contactEmail" /></FormField>
                </FormSection>
                <FormSection title="深色底用標誌">
                  <FormField field="logoDark" label="放在深色背景上的標誌">
                    <ImageUploader v-model:file="darkFile" v-model:remove-cover="removeDark" variant="logo" :min-width="0" :min-height="0" :has-existing-image="hasDark" :existing-preview-url="darkUrl" :disabled="saving || readOnly" />
                  </FormField>
                </FormSection>
                <FormSection title="淺色底用標誌">
                  <FormField field="logoLight" label="放在淺色背景上的標誌">
                    <ImageUploader v-model:file="lightFile" v-model:remove-cover="removeLight" variant="logo" :min-width="0" :min-height="0" :has-existing-image="hasLight" :existing-preview-url="lightUrl" :disabled="saving || readOnly" />
                  </FormField>
                </FormSection>
              </el-card>
              <el-card shadow="never" header="發布設定">
                <FormSection>
                  <el-form-item label="排序值"><el-input-number v-model="form.sortOrder" :min="0" /></el-form-item>
                </FormSection>
              </el-card>
            </el-form>
          </template>
        </EditLayout>
      </LangTabsBar>

      <EditActionBar v-if="!readOnly">
        <template #status><FormErrorStatus /></template>
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </EditActionBar>
    </template>

    <SponsorActivationDialog
      v-if="sponsorId"
      v-model="activationDialog"
      :club-id="club"
      :sponsor-id="sponsorId"
      :can-update="canUpdate"
      :activation="editingActivation"
      :activations="activations"
      :default-sort-order="activations.length"
      :reload="loadActivations"
    />
  </div>
</template>

<style scoped>
.sponsor-edit { max-width: 1200px; margin: 0 auto; }
.sponsor-edit__block { margin-bottom: 16px; }
.sponsor-edit__form { display: contents; }
.sponsor-edit__hint { margin: 4px 0 10px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.sponsor-edit__head { display: flex; justify-content: flex-end; margin-bottom: 8px; }
.sponsor-edit__activations { display: flex; flex-direction: column; gap: 8px; }
.sponsor-edit__activation { display: flex; align-items: center; justify-content: space-between; gap: 8px; flex-wrap: wrap; padding: 8px 10px; border: 1px solid var(--admin-border); border-radius: 4px; }
.sponsor-edit__activation-main { display: flex; flex-direction: column; min-width: 0; }
</style>

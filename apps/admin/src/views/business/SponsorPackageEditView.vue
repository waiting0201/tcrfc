<script setup lang="ts">
/** 贊助方案——新增／編輯（前台「贊助方案」頁的方案卡片）。 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import EditLayout from '@/components/EditLayout.vue'
import FormErrorStatus from '@/components/FormErrorStatus.vue'
import FormField from '@/components/FormField.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { enOrUndefined, nullIfBlank } from '@/api/adminCommon'
import {
  createSponsorPackage,
  getSponsorPackage,
  updateSponsorPackage,
  type SaveSponsorPackagePayload,
  type SponsorPackageDetailDto,
} from '@/api/adminSponsors'

const route = useRoute()
const router = useRouter()
const isCreate = computed(() => route.name === 'sponsor-package-new')
const packageId = ref<string | undefined>(route.params.id as string | undefined)
const { canCreate, canUpdate } = useCrudPermissions('business.sponsor_package')

const form = reactive({
  status: 'draft' as 'draft' | 'published',
  priceMin: null as number | null,
  priceMax: null as number | null,
  isPricePublic: true,
  sortOrder: 0,
  nameZh: '', nameEn: '',
  contentZh: '', contentEn: '',
  benefitZh: '', benefitEn: '',
  audienceZh: '', audienceEn: '',
})
const baselineJson = ref('')
const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
/** 頁首提示：只放沒有欄位歸屬的錯誤；欄位錯誤標在欄位上（formErrors）。 */
const formError = ref<string | null>(null)
const formErrors = provideFormErrors()
const readOnly = computed(() => (isCreate.value ? !canCreate.value : !canUpdate.value))
const pageTitle = computed(() => (isCreate.value ? '新增贊助方案' : `編輯：${form.nameZh || '（未命名）'}`))

function apply(d: SponsorPackageDetailDto) {
  form.status = d.status
  form.priceMin = d.priceMin ?? null
  form.priceMax = d.priceMax ?? null
  form.isPricePublic = d.isPricePublic
  form.sortOrder = d.sortOrder
  form.nameZh = d.zh.name ?? ''
  form.nameEn = d.en?.name ?? ''
  form.contentZh = d.zh.content ?? ''
  form.contentEn = d.en?.content ?? ''
  form.benefitZh = d.zh.benefitList ?? ''
  form.benefitEn = d.en?.benefitList ?? ''
  form.audienceZh = d.zh.audience ?? ''
  form.audienceEn = d.en?.audience ?? ''
}

async function load() {
  loadState.value = 'loading'
  try {
    if (!isCreate.value && packageId.value) apply(await getSponsorPackage(activeClubId.value, packageId.value))
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

const isDirty = computed(() => loadState.value === 'ready' && JSON.stringify(form) !== baselineJson.value)
useUnsavedChanges(isDirty)

/** 一次檢查全部，回傳 欄位鍵 → 訊息（鍵只在程式內對照，不顯示）。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!form.nameZh.trim()) errors.nameZh = '請輸入中文方案名稱'
  if (form.priceMin != null && form.priceMax != null && form.priceMax < form.priceMin) errors.priceMax = '價格上限不能低於下限'
  return errors
}

function buildPayload(): SaveSponsorPackagePayload {
  return {
    priceMin: form.priceMin,
    priceMax: form.priceMax,
    isPricePublic: form.isPricePublic,
    sortOrder: form.sortOrder,
    status: form.status,
    content: {
      zh: { name: form.nameZh.trim(), content: nullIfBlank(form.contentZh), benefitList: nullIfBlank(form.benefitZh), audience: nullIfBlank(form.audienceZh) },
      en: enOrUndefined(
        { name: form.nameEn.trim(), content: nullIfBlank(form.contentEn) as string, benefitList: nullIfBlank(form.benefitEn) as string, audience: nullIfBlank(form.audienceEn) as string },
        'name', 'content', 'benefitList', 'audience',
      ),
    },
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
    const saved = isCreate.value
      ? await createSponsorPackage(activeClubId.value, buildPayload())
      : await updateSponsorPackage(activeClubId.value, packageId.value!, buildPayload())
    if (isCreate.value) {
      packageId.value = saved.id
      router.replace(`/business/sponsorships/packages/${saved.id}/edit`)
    }
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
  router.push({ path: '/business/sponsorships', query: { tab: 'packages' } })
}
</script>

<template>
  <div class="package-edit">
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
          <p v-if="loadState === 'not-found'">找不到這個贊助方案，可能已被刪除，或不屬於目前選擇的俱樂部。</p>
          <p v-else>{{ loadErrorMessage }}</p>
        </template>
        <el-button v-if="loadState === 'error'" type="primary" @click="load">重新載入</el-button>
        <el-button v-else type="primary" @click="back">返回列表</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="package-edit__block" @close="formError = null" />
      <el-alert v-if="readOnly" title="你的帳號只能檢視贊助方案，不能修改。" type="info" show-icon :closable="false" class="package-edit__block" />
      <el-form label-position="top" :disabled="readOnly">
        <LangTabsBar>
          <EditLayout>
            <template #main>
              <el-card shadow="never" header="方案內容">
                <BilingualShortField field="name" label="方案名稱" :zh="form.nameZh" :en="form.nameEn" required @update:zh="(v) => (form.nameZh = v)" @update:en="(v) => (form.nameEn = v)" />
                <BilingualTextareaField field="content" label="方案內容" :zh="form.contentZh" :en="form.contentEn" @update:zh="(v) => (form.contentZh = v)" @update:en="(v) => (form.contentEn = v)" />
                <BilingualTextareaField field="benefit" label="權益清單" :zh="form.benefitZh" :en="form.benefitEn" :rows="5" placeholder="一行寫一項權益" @update:zh="(v) => (form.benefitZh = v)" @update:en="(v) => (form.benefitEn = v)" />
                <BilingualShortField field="audience" label="適合對象" :zh="form.audienceZh" :en="form.audienceEn" @update:zh="(v) => (form.audienceZh = v)" @update:en="(v) => (form.audienceEn = v)" />
              </el-card>
              <el-card shadow="never" header="價格與發布">
                <el-row :gutter="12">
                  <el-col :xs="24" :sm="12"><FormField field="priceMin" label="價格下限（元）"><el-input-number v-model="form.priceMin" :min="0" :controls="false" style="width: 100%" @change="formErrors.clear('priceMax')" /></FormField></el-col>
                  <el-col :xs="24" :sm="12"><FormField field="priceMax" label="價格上限（元）"><el-input-number v-model="form.priceMax" :min="0" :controls="false" style="width: 100%" @change="formErrors.clear('priceMax')" /></FormField></el-col>
                </el-row>
                <el-form-item label="是否公開價格">
                  <el-switch v-model="form.isPricePublic" active-text="前台顯示價格區間" inactive-text="前台不顯示價格" />
                </el-form-item>
                <FormField field="status" label="狀態">
                  <el-radio-group v-model="form.status">
                    <el-radio value="draft">草稿（前台不顯示）</el-radio>
                    <el-radio value="published">已發布</el-radio>
                  </el-radio-group>
                </FormField>
                <el-form-item label="排序值"><el-input-number v-model="form.sortOrder" :min="0" /></el-form-item>
              </el-card>
            </template>
          </EditLayout>
        </LangTabsBar>
      </el-form>
      <EditActionBar v-if="!readOnly">
        <template #status><FormErrorStatus /></template>
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </EditActionBar>
    </template>
  </div>
</template>

<style scoped>
.package-edit { max-width: 780px; margin: 0 auto; }
.package-edit__block { margin-bottom: 16px; }
</style>

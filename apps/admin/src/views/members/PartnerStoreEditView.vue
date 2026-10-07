<script setup lang="ts">
/** 特約店家——新增／編輯。對照 apps/api/README.md「B1」節「K4 特約店家與權益」。 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import SharedContentNotice from '@/components/SharedContentNotice.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import EditLayout from '@/components/EditLayout.vue'
import FormSection from '@/components/FormSection.vue'
import FormErrorStatus from '@/components/FormErrorStatus.vue'
import FormField from '@/components/FormField.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { useIsSuperAdmin } from '@/composables/useRolePermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { enOrUndefined, imageIntent, nullIfBlank } from '@/api/adminCommon'
import {
  createPartnerStore,
  getPartnerStore,
  getPartnerStoreFilters,
  locatePartnerStoreAddress,
  updatePartnerStore,
  type AutoLocateStatus,
  type PartnerStoreDetailDto,
  type SavePartnerStorePayload,
  type StoreStatus,
  type StoreTier,
} from '@/api/adminPartnerStores'

const route = useRoute()
const router = useRouter()
// 🔴 必須是 computed（建立成功後 router.replace 同一元件不會重新掛載）
const isCreate = computed(() => route.name === 'partner-store-new')
const storeId = ref<string | undefined>(route.params.id as string | undefined)
const { canCreate, canUpdate } = useCrudPermissions('member.store')
const isSuperAdmin = useIsSuperAdmin()

const form = reactive({
  slug: '',
  category: '',
  region: '',
  lat: null as number | null,
  lng: null as number | null,
  phone: '',
  businessHours: '',
  mapUrl: '',
  websiteUrl: '',
  applicableTier: 'all' as StoreTier,
  startOn: '',
  endOn: '',
  sortOrder: 0,
  status: 'draft' as StoreStatus,
  isShared: false,
  nameZh: '',
  nameEn: '',
  addressZh: '',
  addressEn: '',
  offerZh: '',
  offerEn: '',
})
const baselineJson = ref('')
const imageFile = ref<File | null>(null)
const removeImage = ref(false)
const imageUrl = ref<string | null>(null)
const hasImage = ref(false)
const categoryOptions = ref<string[]>([])
const regionOptions = ref<string[]>([])
/** 這筆是不是兩隊共用的店家（建立後才有意義）。 */
const loadedShared = ref(false)

/** 「儲存時由地址定位」勾選：預設不勾，不算表單內容（不影響離開提醒），每次載入／儲存後重置。 */
const autoLocate = ref(false)
/** 「由地址定位」按鈕的進行狀態與結果提示（人工確認後再儲存）。 */
const locating = ref(false)
/** 後端回 503 且非 `geocoder_unavailable`＝定位服務尚未串接：按鈕與勾選都停用並說明，不讓人一直試。 */
const locateUnavailable = ref(false)
const locateNotice = ref<{ type: 'success' | 'warning' | 'info'; text: string } | null>(null)

const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
/** 頁首提示：只放沒有對到欄位的錯誤；欄位錯誤標在欄位上（formErrors）。 */
const formError = ref<string | null>(null)
const formErrors = provideFormErrors()
// 選檔或移除照片就清掉該欄位的錯誤
watch([imageFile, removeImage], () => formErrors.clear('image'))

const sharedReadOnly = computed(() => !isCreate.value && loadedShared.value && !isSuperAdmin.value)
const readOnly = computed(() => sharedReadOnly.value || (isCreate.value ? !canCreate.value : !canUpdate.value))
const pageTitle = computed(() => (isCreate.value ? '新增特約店家' : `編輯：${form.nameZh || '（未命名）'}`))

function applyDetail(d: PartnerStoreDetailDto) {
  form.slug = d.slug ?? ''
  form.category = d.category ?? ''
  form.region = d.region ?? ''
  form.lat = d.lat ?? null
  form.lng = d.lng ?? null
  form.phone = d.phone ?? ''
  form.businessHours = d.businessHours ?? ''
  form.mapUrl = d.mapUrl ?? ''
  form.websiteUrl = d.websiteUrl ?? ''
  form.applicableTier = d.applicableTier
  form.startOn = d.startOn ?? ''
  form.endOn = d.endOn ?? ''
  form.sortOrder = d.sortOrder
  form.status = d.status
  form.isShared = d.isShared
  form.nameZh = d.zh.name ?? ''
  form.nameEn = d.en?.name ?? ''
  form.addressZh = d.zh.address ?? ''
  form.addressEn = d.en?.address ?? ''
  form.offerZh = d.zh.offerContent ?? ''
  form.offerEn = d.en?.offerContent ?? ''
  loadedShared.value = d.isShared
  imageUrl.value = d.imageThumbUrl ?? d.imageUrl ?? null
  hasImage.value = !!d.imageKey
}

async function load() {
  loadState.value = 'loading'
  try {
    const f = await getPartnerStoreFilters(activeClubId.value).catch(() => null)
    categoryOptions.value = f?.categories ?? []
    regionOptions.value = f?.regions ?? []
    if (!isCreate.value && storeId.value) applyDetail(await getPartnerStore(activeClubId.value, storeId.value))
    imageFile.value = null
    removeImage.value = false
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

const isDirty = computed(
  () => loadState.value === 'ready' && (JSON.stringify(form) !== baselineJson.value || !!imageFile.value || removeImage.value),
)
useUnsavedChanges(isDirty)

const canLocate = computed(() => !!form.addressZh.trim() && !locating.value && !locateUnavailable.value && !saving.value)

async function handleLocate() {
  const address = form.addressZh.trim()
  if (!address) return
  locating.value = true
  locateNotice.value = null
  try {
    const { lat, lng } = await locatePartnerStoreAddress(activeClubId.value, address)
    form.lat = lat
    form.lng = lng
    formErrors.clear('lat')
    formErrors.clear('lng')
    locateNotice.value = { type: 'success', text: '已依地址填入座標，請對照地圖確認無誤後再儲存。' }
  } catch (error) {
    if (error instanceof AdminApiError && error.status === 503 && (error.body as { code?: string } | undefined)?.code === 'geocoder_unavailable') {
      // 已啟用但供應商暫時故障或額度用盡：不停用按鈕，可稍後再試。
      locateNotice.value = { type: 'warning', text: '定位服務暫時無法使用，請稍後再試或手動輸入座標。' }
    } else if (error instanceof AdminApiError && error.status === 503) {
      locateUnavailable.value = true
      locateNotice.value = { type: 'info', text: '定位服務尚未啟用，請手動輸入座標。' }
    } else if (error instanceof AdminApiError && error.kind === 'not-found') {
      locateNotice.value = { type: 'warning', text: '查無此地址，請手動輸入座標。' }
    } else if (error instanceof AdminApiError && error.kind === 'validation') {
      locateNotice.value = { type: 'warning', text: error.message || '地址格式不正確，請修改後再試。' }
    } else {
      locateNotice.value = { type: 'warning', text: '定位服務暫時無法使用，請手動輸入座標。' }
    }
  } finally {
    locating.value = false
  }
}

const AUTO_LOCATE_NOTICE: Partial<Record<AutoLocateStatus, { type: 'success' | 'warning'; text: string }>> = {
  located: { type: 'success', text: '已依地址填入座標，請確認是否正確；如有誤差可直接修改後再儲存。' },
  not_found: { type: 'warning', text: '查無此地址，請手動輸入座標。' },
  unavailable: { type: 'warning', text: '定位服務暫時無法使用，請手動輸入座標。' },
}

const isHttp = (v: string) => /^https?:\/\//i.test(v.trim())

/** 一次檢查全部，回傳 欄位鍵 → 訊息（鍵只在程式內對照，不顯示）。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!form.nameZh.trim()) errors.nameZh = '請輸入中文名稱'
  if (form.slug.trim() && !/^[a-z0-9-]+$/.test(form.slug.trim())) errors.slug = '網址名稱只能用小寫英文字母、數字與連字號'
  if ((form.lat === null) !== (form.lng === null)) {
    errors[form.lat === null ? 'lat' : 'lng'] = '緯度與經度要一起填寫，或兩個都留空'
  }
  if (form.lat !== null && (form.lat < -90 || form.lat > 90)) errors.lat = '緯度必須介於 -90 到 90 之間'
  if (form.lng !== null && (form.lng < -180 || form.lng > 180)) errors.lng = '經度必須介於 -180 到 180 之間'
  if (form.mapUrl.trim() && !isHttp(form.mapUrl)) errors.mapUrl = '地圖連結必須是以 http:// 或 https:// 開頭的完整網址'
  if (form.websiteUrl.trim() && !isHttp(form.websiteUrl)) errors.websiteUrl = '官網連結必須是以 http:// 或 https:// 開頭的完整網址'
  if (form.startOn && form.endOn && form.endOn < form.startOn) errors.endOn = '合作結束日不能早於開始日'
  return errors
}

function buildPayload(): SavePartnerStorePayload {
  const img = imageIntent(imageFile.value, removeImage.value)
  const payload: SavePartnerStorePayload = {
    category: nullIfBlank(form.category),
    region: nullIfBlank(form.region),
    lat: form.lat,
    lng: form.lng,
    phone: nullIfBlank(form.phone),
    businessHours: nullIfBlank(form.businessHours),
    mapUrl: nullIfBlank(form.mapUrl),
    websiteUrl: nullIfBlank(form.websiteUrl),
    applicableTier: form.applicableTier,
    startOn: form.startOn || null,
    endOn: form.endOn || null,
    sortOrder: form.sortOrder,
    status: form.status,
    removeImage: img.remove,
    content: {
      zh: { name: form.nameZh.trim(), address: nullIfBlank(form.addressZh), offerContent: nullIfBlank(form.offerZh) },
      en: enOrUndefined(
        { name: form.nameEn.trim(), address: nullIfBlank(form.addressEn) as string, offerContent: nullIfBlank(form.offerEn) as string },
        'name', 'address', 'offerContent',
      ),
    },
  }
  // 只在勾選時才送；已有手動座標時後端以手動為準（不會覆寫）
  if (autoLocate.value) payload.autoLocate = true
  if (form.slug.trim()) payload.slug = form.slug.trim()
  // 只有系統管理員能建立兩隊共同的店家；更新時後端會忽略這個欄位
  if (isCreate.value && isSuperAdmin.value && form.isShared) payload.isShared = true
  return payload
}

async function handleSave() {
  if (readOnly.value) return
  formError.value = null
  if (formErrors.replaceAll(validate())) {
    await formErrors.focusFirst()
    return
  }
  saving.value = true
  locateNotice.value = null
  try {
    const saved = isCreate.value
      ? await createPartnerStore(activeClubId.value, buildPayload(), imageFile.value)
      : await updatePartnerStore(activeClubId.value, storeId.value!, buildPayload(), imageFile.value)
    if (isCreate.value) {
      storeId.value = saved.id
      router.replace(`/members/partner-stores/${saved.id}/edit`)
    }
    applyDetail(saved)
    imageFile.value = null
    removeImage.value = false
    baselineJson.value = JSON.stringify(form)
    ElMessage.success('已儲存')
    const hint = saved.autoLocateStatus ? AUTO_LOCATE_NOTICE[saved.autoLocateStatus] : undefined
    if (hint) locateNotice.value = hint
    autoLocate.value = false
  } catch (error) {
    // 後端標到欄位的錯誤直接標在欄位上；對不到欄位的才放頁首
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

const mapSearchUrl = computed(() =>
  form.addressZh.trim() ? `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(form.addressZh.trim())}` : '',
)
const backToList = () => router.push({ path: '/members/partner-stores', query: { tab: 'stores' } })
</script>

<template>
  <div class="store-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="backToList"><el-icon><ArrowLeft /></el-icon>返回列表</el-button>
      </template>
      <template #meta>
        <div class="store-edit__meta">
          <FrontendUnitBanner module-code="K4" />
          <SharedContentNotice v-if="sharedReadOnly" what="店家" />
        </div>
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這家店家，可能已被刪除，或不屬於目前選擇的俱樂部。</p>
          <p v-else>{{ loadErrorMessage }}</p>
        </template>
        <el-button v-if="loadState === 'error'" type="primary" @click="load">重新載入</el-button>
        <el-button v-else type="primary" @click="backToList">返回列表</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="store-edit__error" @close="formError = null" />
      <el-alert v-if="readOnly && !sharedReadOnly" title="你的帳號只能檢視特約店家，不能修改。" type="info" show-icon :closable="false" class="store-edit__error" />
      <el-form label-position="top" :disabled="readOnly">
        <LangTabsBar>
          <EditLayout>
            <template #main>
              <el-card shadow="never">
                <FormSection>
                  <BilingualShortField field="name" label="店家名稱" :zh="form.nameZh" :en="form.nameEn" required @update:zh="(v) => (form.nameZh = v)" @update:en="(v) => (form.nameEn = v)" />
                  <BilingualShortField field="address" label="地址" :zh="form.addressZh" :en="form.addressEn" @update:zh="(v) => (form.addressZh = v)" @update:en="(v) => (form.addressEn = v)" />
                  <BilingualTextareaField field="offer" label="優惠內容" :zh="form.offerZh" :en="form.offerEn" @update:zh="(v) => (form.offerZh = v)" @update:en="(v) => (form.offerEn = v)" />
                </FormSection>
              </el-card>
            </template>

            <template #aside>
              <el-card shadow="never" header="基本設定">
                <FormSection title="店家資料">
                  <el-row :gutter="12">
                    <el-col :span="24">
                      <FormField field="category" label="分類">
                        <el-select v-model="form.category" filterable allow-create clearable default-first-option placeholder="選擇或直接輸入新分類" style="width: 100%">
                          <el-option v-for="c in categoryOptions" :key="c" :label="c" :value="c" />
                        </el-select>
                      </FormField>
                    </el-col>
                    <el-col :span="24">
                      <FormField field="region" label="地區">
                        <el-select v-model="form.region" filterable allow-create clearable default-first-option placeholder="選擇或直接輸入新地區" style="width: 100%">
                          <el-option v-for="r in regionOptions" :key="r" :label="r" :value="r" />
                        </el-select>
                      </FormField>
                    </el-col>
                  </el-row>
                  <el-row :gutter="12">
                    <el-col :span="24"><FormField field="phone" label="電話"><el-input v-model="form.phone" maxlength="32" /></FormField></el-col>
                    <el-col :span="24"><FormField field="businessHours" label="營業時間（自由填寫）"><el-input v-model="form.businessHours" maxlength="500" placeholder="例如：週一至週五 11:00–21:00" /></FormField></el-col>
                    <el-col :span="24"><FormField field="mapUrl" label="地圖連結"><el-input v-model="form.mapUrl" placeholder="https://" /></FormField></el-col>
                    <el-col :span="24"><FormField field="websiteUrl" label="官網連結"><el-input v-model="form.websiteUrl" placeholder="https://" /></FormField></el-col>
                  </el-row>
                </FormSection>

                <FormSection title="地圖座標">
                  <el-row :gutter="12">
                    <el-col :span="24">
                      <FormField field="lat" label="緯度（-90 到 90）"><el-input-number v-model="form.lat" :controls="false" :precision="6" :min="-90" :max="90" :value-on-clear="null" style="width: 100%" placeholder="例如 24.1477" @change="formErrors.clear('lat'); formErrors.clear('lng')" /></FormField>
                    </el-col>
                    <el-col :span="24">
                      <FormField field="lng" label="經度（-180 到 180）"><el-input-number v-model="form.lng" :controls="false" :precision="6" :min="-180" :max="180" :value-on-clear="null" style="width: 100%" placeholder="例如 120.6736" @change="formErrors.clear('lat'); formErrors.clear('lng')" /></FormField>
                    </el-col>
                  </el-row>
                  <div class="store-edit__locate">
                    <el-button size="small" :loading="locating" :disabled="!canLocate" @click="handleLocate">由地址定位</el-button>
                    <span v-if="locateUnavailable" class="store-edit__hint store-edit__hint--inline">定位服務尚未啟用，請手動輸入座標。</span>
                    <span v-else-if="!form.addressZh.trim()" class="store-edit__hint store-edit__hint--inline">請先填寫中文地址。</span>
                  </div>
                  <el-checkbox v-model="autoLocate" :disabled="locateUnavailable">儲存時由地址定位</el-checkbox>
                  <p class="store-edit__hint">
                    緯度與經度要一起填。「由地址定位」只會把結果填入欄位供你確認，不會自動儲存；勾選「儲存時由地址定位」則在儲存時依中文地址補上座標（已手動填寫座標時以手動為準）。找不到或服務無法使用時不影響儲存，請改為手動輸入。
                  </p>
                  <el-alert v-if="locateNotice" :title="locateNotice.text" :type="locateNotice.type" show-icon class="store-edit__error" @close="locateNotice = null" />
                  <el-button v-if="mapSearchUrl" tag="a" :href="mapSearchUrl" target="_blank" rel="noopener noreferrer" size="small">
                    在地圖開啟目前地址查詢（另開分頁，僅供輔助）
                  </el-button>
                </FormSection>

                <FormSection title="合作設定">
                  <el-row :gutter="12">
                    <el-col :span="24">
                      <FormField field="applicableTier" label="適用會員">
                        <el-radio-group v-model="form.applicableTier">
                          <el-radio value="all">全部會員</el-radio>
                          <el-radio value="fan_club">限付費球迷會員</el-radio>
                        </el-radio-group>
                      </FormField>
                    </el-col>
                    <el-col :span="24"><el-form-item label="合作開始日"><el-date-picker v-model="form.startOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" @change="formErrors.clear('endOn')" /></el-form-item></el-col>
                    <el-col :span="24"><FormField field="endOn" label="合作結束日"><el-date-picker v-model="form.endOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" @change="formErrors.clear('endOn')" /></FormField></el-col>
                  </el-row>
                  <p class="store-edit__hint">兩個日期都不填代表長期合作；合作結束後前台不再顯示。</p>
                  <FormField field="slug" label="網址名稱（選填）">
                    <el-input v-model="form.slug" maxlength="80" placeholder="小寫英文、數字與連字號；留空由系統自動產生" />
                  </FormField>
                  <el-form-item v-if="isCreate && isSuperAdmin" label="兩隊共用">
                    <el-checkbox v-model="form.isShared">建立為台中磐石與台中藍鯨共用的店家</el-checkbox>
                    <p class="store-edit__hint">只有系統管理員能建立共用店家，建立後所有俱樂部都看得到，且只有系統管理員能修改；建立之後無法再更改這個設定。</p>
                  </el-form-item>
                  <p v-else-if="!isCreate && loadedShared" class="store-edit__hint">這是兩隊共用的店家。</p>
                </FormSection>

                <FormSection title="店家照片">
                  <FormField field="image" label="店家照片">
                    <ImageUploader v-model:file="imageFile" v-model:remove-cover="removeImage" variant="photo" :min-width="0" :min-height="0" :has-existing-image="hasImage" :existing-preview-url="imageUrl" :disabled="saving || readOnly" />
                  </FormField>
                </FormSection>
              </el-card>

              <el-card shadow="never" header="發布設定">
                <FormField field="status" label="狀態">
                  <el-radio-group v-model="form.status"><el-radio value="published">已發布</el-radio><el-radio value="draft">草稿</el-radio></el-radio-group>
                </FormField>
                <el-form-item label="排序值">
                  <el-input-number v-model="form.sortOrder" :min="0" />
                  <p class="store-edit__hint">數字小的排前面；也可以在店家列表用上移、下移調整。</p>
                </el-form-item>
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
.store-edit { max-width: 1200px; margin: 0 auto; min-width: 0; }
.store-edit__meta { display: flex; flex-direction: column; gap: 4px; }
.store-edit__error { margin-bottom: 16px; }
.store-edit__locate { display: flex; align-items: center; gap: 12px; margin-bottom: 8px; flex-wrap: wrap; }
.store-edit__hint { margin: 6px 0 12px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.store-edit__hint--inline { margin: 0; }
</style>

<script setup lang="ts">
/**
 * I 場地管理——新增／編輯（apps/api/README.md「H 批」§5）。multipart：`payload`（JSON）＋選填檔案 `photo`。
 * 緯度經度同時填或同時空白；英文名稱空白＝刪除英文版；建立時不可勾「移除照片」。
 * 「由地址定位」比照特約店家（`PartnerStoreEditView`）：只把結果填進欄位供人工確認，不寫入；
 * 503 依 `code` 區分——`geocoder_unavailable`＝暫時故障（可再試）、其他＝服務尚未啟用（停用按鈕）。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import {
  createAdminVenue,
  deleteAdminVenue,
  getAdminVenue,
  locateAdminVenueAddress,
  updateAdminVenue,
  type AdminVenueDetail,
  type SaveVenuePayload,
} from '@/api/adminVenues'

const route = useRoute()
const router = useRouter()
// 🔴 必須是 computed（建立成功後 router.replace 同一元件不會重新掛載）
const isCreate = computed(() => route.name === 'settings-venue-new')
const venueId = ref<string | undefined>(route.params.id as string | undefined)
const { canCreate, canUpdate, canDelete } = useCrudPermissions('site.venue')

const form = reactive({
  nameZh: '',
  nameEn: '',
  addressZh: '',
  addressEn: '',
  directionsZh: '',
  directionsEn: '',
  photoAltZh: '',
  photoAltEn: '',
  lat: null as number | null,
  lng: null as number | null,
  sortOrder: 0,
})
const baselineJson = ref('')
const photoFile = ref<File | null>(null)
const removePhoto = ref(false)
const photoUrl = ref<string | null>(null)
const hasPhoto = ref(false)
const usageCount = ref(0)
const isHomeVenue = ref(false)

const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
const deleting = ref(false)
const formError = ref<string | null>(null)

const locating = ref(false)
/** 後端 503 且不是暫時故障＝定位服務尚未啟用：按鈕停用並說明。 */
const locateUnavailable = ref(false)
const locateNotice = ref<{ type: 'success' | 'warning' | 'info'; text: string } | null>(null)

const readOnly = computed(() => (isCreate.value ? !canCreate.value : !canUpdate.value))
const pageTitle = computed(() => (isCreate.value ? '新增場地' : `編輯：${form.nameZh || '（未命名）'}`))

function applyDetail(d: AdminVenueDetail) {
  form.nameZh = d.zh.name ?? ''
  form.addressZh = d.zh.address ?? ''
  form.directionsZh = d.zh.directions ?? ''
  form.photoAltZh = d.zh.photoAlt ?? ''
  form.nameEn = d.en?.name ?? ''
  form.addressEn = d.en?.address ?? ''
  form.directionsEn = d.en?.directions ?? ''
  form.photoAltEn = d.en?.photoAlt ?? ''
  form.lat = d.lat ?? null
  form.lng = d.lng ?? null
  form.sortOrder = d.sortOrder
  photoUrl.value = d.photoUrl ?? null
  hasPhoto.value = !!d.photoUrl
  usageCount.value = d.usageCount
  isHomeVenue.value = d.isHomeVenue
}

async function load() {
  loadState.value = 'loading'
  try {
    if (!isCreate.value && venueId.value) applyDetail(await getAdminVenue(activeClubId.value, venueId.value))
    photoFile.value = null
    removePhoto.value = false
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
  () => loadState.value === 'ready' && (JSON.stringify(form) !== baselineJson.value || !!photoFile.value || removePhoto.value),
)
useUnsavedChanges(isDirty)

const canLocate = computed(() => !!form.addressZh.trim() && !locating.value && !locateUnavailable.value && !saving.value && !readOnly.value)

async function handleLocate() {
  const address = form.addressZh.trim()
  if (!address) return
  locating.value = true
  locateNotice.value = null
  try {
    const { lat, lng } = await locateAdminVenueAddress(activeClubId.value, address)
    form.lat = lat
    form.lng = lng
    locateNotice.value = { type: 'success', text: '已依地址填入座標，請對照地圖確認無誤後再儲存。' }
  } catch (error) {
    const code = error instanceof AdminApiError ? (error.body as { code?: string } | undefined)?.code : undefined
    if (error instanceof AdminApiError && error.status === 503 && code === 'geocoder_unavailable') {
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

function blankToNull(value: string): string | null {
  const v = value.trim()
  return v ? v : null
}

function validate(): boolean {
  if (!form.nameZh.trim()) {
    formError.value = '請填寫場地的中文名稱。'
    return false
  }
  if ((form.lat === null) !== (form.lng === null)) {
    formError.value = '緯度與經度要一起填寫，或一起留空。'
    return false
  }
  const enFilled = [form.addressEn, form.directionsEn, form.photoAltEn].some((v) => v.trim())
  if (!form.nameEn.trim() && enFilled) {
    formError.value = '有填英文內容時，請一併填寫英文名稱；英文名稱留空會刪除整份英文版。'
    return false
  }
  formError.value = null
  return true
}

function buildPayload(): SaveVenuePayload {
  return {
    zh: {
      name: form.nameZh.trim(),
      address: blankToNull(form.addressZh),
      directions: blankToNull(form.directionsZh),
      photoAlt: blankToNull(form.photoAltZh),
    },
    en: form.nameEn.trim()
      ? {
          name: form.nameEn.trim(),
          address: blankToNull(form.addressEn),
          directions: blankToNull(form.directionsEn),
          photoAlt: blankToNull(form.photoAltEn),
        }
      : null,
    lat: form.lat,
    lng: form.lng,
    sortOrder: form.sortOrder,
    // 選了新照片就不送移除（兩者同時給後端會回 400）；建立時不可移除
    removePhoto: !isCreate.value && !photoFile.value && removePhoto.value,
  }
}

async function handleSave() {
  if (readOnly.value || !validate()) return
  saving.value = true
  formError.value = null
  try {
    const saved = isCreate.value
      ? await createAdminVenue(activeClubId.value, buildPayload(), photoFile.value)
      : await updateAdminVenue(activeClubId.value, venueId.value!, buildPayload(), photoFile.value)
    if (isCreate.value) {
      venueId.value = saved.id
      void router.replace(`/settings/venues/${saved.id}/edit`)
    }
    applyDetail(saved)
    photoFile.value = null
    removePhoto.value = false
    baselineJson.value = JSON.stringify(form)
    ElMessage.success('已儲存')
  } catch (error) {
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

async function handleDelete() {
  if (!venueId.value) return
  try {
    await ElMessageBox.confirm(`確定要刪除場地「${form.nameZh}」嗎？此動作無法復原。`, '刪除場地', {
      confirmButtonText: '刪除',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    })
  } catch {
    return
  }
  deleting.value = true
  try {
    await deleteAdminVenue(activeClubId.value, venueId.value)
    baselineJson.value = JSON.stringify(form)
    photoFile.value = null
    removePhoto.value = false
    ElMessage.success('已刪除')
    void router.push('/settings/venues')
  } catch (error) {
    // 409：被引用或登記為主場，訊息說明原因
    formError.value = error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試'
  } finally {
    deleting.value = false
  }
}

const mapSearchUrl = computed(() =>
  form.addressZh.trim() ? `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(form.addressZh.trim())}` : '',
)
const backToList = () => router.push('/settings/venues')
</script>

<template>
  <div class="venue-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="backToList"><el-icon><ArrowLeft /></el-icon>返回列表</el-button>
      </template>
      <template #meta>
        <FrontendUnitBanner module-code="I" />
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這座場地，可能已被刪除。</p>
          <p v-else>{{ loadErrorMessage }}</p>
        </template>
        <el-button v-if="loadState === 'error'" type="primary" @click="load">重新載入</el-button>
        <el-button v-else type="primary" @click="backToList">返回列表</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="venue-edit__error" @close="formError = null" />
      <el-alert v-if="readOnly" title="你的帳號只能檢視場地，不能修改。" type="info" show-icon :closable="false" class="venue-edit__error" />
      <el-alert
        v-if="!isCreate && (usageCount > 0 || isHomeVenue)"
        :title="`這座場地${isHomeVenue ? '是登記的主場，' : ''}${usageCount > 0 ? `目前被 ${usageCount} 筆賽事、課程梯次、試訓或活動使用，` : ''}修改內容會同步套用到這些地方，也無法刪除。`"
        type="info"
        show-icon
        :closable="false"
        class="venue-edit__error"
      />
      <el-form label-position="top" :disabled="readOnly">
        <el-card shadow="never" header="基本資料" class="venue-edit__section">
          <BilingualShortField label="場地名稱" :zh="form.nameZh" :en="form.nameEn" required @update:zh="(v) => (form.nameZh = v)" @update:en="(v) => (form.nameEn = v)" />
          <BilingualShortField label="地址" :zh="form.addressZh" :en="form.addressEn" @update:zh="(v) => (form.addressZh = v)" @update:en="(v) => (form.addressEn = v)" />
          <BilingualTextareaField label="交通說明" :zh="form.directionsZh" :en="form.directionsEn" @update:zh="(v) => (form.directionsZh = v)" @update:en="(v) => (form.directionsEn = v)" />
          <el-form-item label="排序值">
            <el-input-number v-model="form.sortOrder" :min="0" />
            <p class="venue-edit__hint">數字小的排前面。</p>
          </el-form-item>
        </el-card>

        <el-card shadow="never" header="地圖座標" class="venue-edit__section">
          <el-row :gutter="12">
            <el-col :xs="24" :sm="12">
              <el-form-item label="緯度（-90 到 90）"><el-input-number v-model="form.lat" :controls="false" :precision="6" :min="-90" :max="90" :value-on-clear="null" style="width: 100%" placeholder="例如 24.1477" /></el-form-item>
            </el-col>
            <el-col :xs="24" :sm="12">
              <el-form-item label="經度（-180 到 180）"><el-input-number v-model="form.lng" :controls="false" :precision="6" :min="-180" :max="180" :value-on-clear="null" style="width: 100%" placeholder="例如 120.6736" /></el-form-item>
            </el-col>
          </el-row>
          <div class="venue-edit__locate">
            <el-button size="small" :loading="locating" :disabled="!canLocate" @click="handleLocate">由地址定位</el-button>
            <span v-if="locateUnavailable" class="venue-edit__hint venue-edit__hint--inline">定位服務尚未啟用，請手動輸入座標。</span>
            <span v-else-if="!form.addressZh.trim()" class="venue-edit__hint venue-edit__hint--inline">請先在上方填寫中文地址。</span>
          </div>
          <p class="venue-edit__hint">
            緯度與經度要一起填。「由地址定位」只會把結果填入欄位供你確認，不會自動儲存；找不到或服務無法使用時不影響儲存，請改為手動輸入。
          </p>
          <el-alert v-if="locateNotice" :title="locateNotice.text" :type="locateNotice.type" show-icon class="venue-edit__error" @close="locateNotice = null" />
          <el-button v-if="mapSearchUrl" tag="a" :href="mapSearchUrl" target="_blank" rel="noopener noreferrer" size="small">
            在地圖開啟目前地址查詢（另開分頁，僅供輔助）
          </el-button>
        </el-card>

        <el-card shadow="never" header="場地照片" class="venue-edit__section">
          <ImageUploader v-model:file="photoFile" v-model:remove-cover="removePhoto" variant="photo" :min-width="0" :min-height="0" :has-existing-image="hasPhoto" :existing-preview-url="photoUrl" :disabled="saving || readOnly" />
          <BilingualShortField label="照片的替代文字" :zh="form.photoAltZh" :en="form.photoAltEn" @update:zh="(v) => (form.photoAltZh = v)" @update:en="(v) => (form.photoAltEn = v)" />
          <p class="venue-edit__hint">替代文字給看不到圖片的使用者（例如螢幕報讀軟體）閱讀，請簡短描述照片內容。</p>
        </el-card>

        <el-card v-if="!isCreate && canDelete" shadow="never" header="刪除場地" class="venue-edit__section">
          <p class="venue-edit__hint">被賽事、課程梯次、試訓、行事曆活動、球迷會活動使用，或登記為主場的場地不能刪除。</p>
          <el-button type="danger" plain :loading="deleting" @click="handleDelete">刪除這座場地</el-button>
        </el-card>
      </el-form>
      <EditActionBar v-if="!readOnly"><el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button></EditActionBar>
    </template>
  </div>
</template>

<style scoped>
.venue-edit { max-width: 780px; margin: 0 auto 88px; min-width: 0; }
.venue-edit__error { margin-bottom: 16px; }
.venue-edit__section { margin-bottom: 16px; }
.venue-edit__locate { display: flex; align-items: center; gap: 12px; margin-bottom: 8px; flex-wrap: wrap; }
.venue-edit__hint { margin: 6px 0 12px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.venue-edit__hint--inline { margin: 0; }
</style>

<script setup lang="ts">
/**
 * 廣告版位：App 畫面上放廣告的位置與素材規格。
 * 🔴 版位永遠不空白：沒有可投放的檔期時，改顯示自家的備援素材。
 * 🔴 兒童向畫面（課程列表、課程報名表、我的報名）不設版位；不設慈善相關版位，送出時後端會擋。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import MobileCardList from '@/components/MobileCardList.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { AdminApiError } from '@/api/http'
import { nullIfBlank } from '@/api/adminCommon'
import { createAdSlot, deleteAdSlot, listAdSlots, updateAdSlot, type AdSlotDto, type SaveAdSlotPayload } from '@/api/adminAds'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canCreate, canUpdate, canDelete } = useCrudPermissions('ad.slot')
const errText = (e: unknown, f: string) => (e instanceof AdminApiError ? e.message : f)

const rows = ref<AdSlotDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
async function load() {
  loading.value = true
  loadError.value = null
  try {
    rows.value = await listAdSlots()
  } catch (e) {
    rows.value = []
    loadError.value = errText(e, '版位清單載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
onMounted(load)

const specText = (s: AdSlotDto) => {
  const parts: string[] = []
  if (s.aspectRatio) parts.push(`長寬比 ${s.aspectRatio}`)
  if (s.minWidth && s.minHeight) parts.push(`至少 ${s.minWidth}×${s.minHeight}`)
  if (s.maxFileKb) parts.push(`${s.maxFileKb} KB 以內`)
  if (s.allowVideo) parts.push('可放影片')
  return parts.join('・') || '未設定規格'
}

// ── 編輯對話框 ──
const visible = ref(false)
const editing = ref<AdSlotDto | null>(null)
const saving = ref(false)
const formError = ref<string | null>(null)
const image = ref<File | null>(null)
const removeImage = ref(false)
const form = reactive({
  slotCode: '', nameZh: '', nameEn: '', screenCode: '', blockOrder: null as number | null, aspectRatio: '16:9',
  minWidth: null as number | null, minHeight: null as number | null, maxFileKb: null as number | null, allowedFormats: '',
  allowVideo: false, sessionImpressionCap: null as number | null, rotationCap: 1, fallbackLink: '',
  fallbackAltZh: '', fallbackAltEn: '', isActive: true,
})
function openDialog(row: AdSlotDto | null) {
  editing.value = row
  formError.value = null
  image.value = null
  removeImage.value = false
  Object.assign(form, {
    slotCode: row?.slotCode ?? '', nameZh: row?.nameZh ?? '', nameEn: row?.nameEn ?? '', screenCode: row?.screenCode ?? '',
    blockOrder: row?.blockOrder ?? null, aspectRatio: row?.aspectRatio ?? '16:9', minWidth: row?.minWidth ?? null,
    minHeight: row?.minHeight ?? null, maxFileKb: row?.maxFileKb ?? null, allowedFormats: row?.allowedFormats ?? '',
    allowVideo: row?.allowVideo ?? false, sessionImpressionCap: row?.sessionImpressionCap ?? null, rotationCap: row?.rotationCap ?? 1,
    fallbackLink: row?.fallbackLink ?? '', fallbackAltZh: row?.fallbackAltZh ?? '', fallbackAltEn: row?.fallbackAltEn ?? '', isActive: row?.isActive ?? true,
  })
  visible.value = true
}
const readOnly = computed(() => (editing.value ? !canUpdate.value : !canCreate.value))
async function save() {
  if (readOnly.value) return
  if (!editing.value && !/^[a-z0-9]+(_[a-z0-9]+)+$/.test(form.slotCode.trim())) {
    formError.value = '版位代號只能使用小寫英文字母、數字與底線，且不可留空（建立後不能修改）'
    return
  }
  if (!form.nameZh.trim()) {
    formError.value = '請輸入版位的中文名稱'
    return
  }
  if (form.aspectRatio.trim() && !/^\d+:\d+$/.test(form.aspectRatio.trim())) {
    formError.value = '長寬比請用「寬:高」的寫法，例如 16:9'
    return
  }
  if (!Number.isInteger(form.rotationCap) || form.rotationCap < 1 || form.rotationCap > 10) {
    formError.value = '輪播張數上限請填 1 到 10'
    return
  }
  saving.value = true
  formError.value = null
  const altZh = nullIfBlank(form.fallbackAltZh)
  const altEn = nullIfBlank(form.fallbackAltEn)
  const payload: SaveAdSlotPayload = {
    slotCode: editing.value ? undefined : form.slotCode.trim(),
    screenCode: nullIfBlank(form.screenCode),
    blockOrder: form.blockOrder,
    aspectRatio: nullIfBlank(form.aspectRatio),
    minWidth: form.minWidth,
    minHeight: form.minHeight,
    maxFileKb: form.maxFileKb,
    allowedFormats: nullIfBlank(form.allowedFormats),
    allowVideo: form.allowVideo,
    sessionImpressionCap: form.sessionImpressionCap,
    rotationCap: form.rotationCap,
    fallbackLink: nullIfBlank(form.fallbackLink),
    isActive: form.isActive,
    removeFallbackImage: image.value ? undefined : removeImage.value || undefined,
    content: {
      zh: { name: form.nameZh.trim(), fallbackAlt: altZh },
      en: form.nameEn.trim() ? { name: form.nameEn.trim(), fallbackAlt: altEn } : undefined,
    },
  }
  try {
    if (editing.value) await updateAdSlot(editing.value.id, payload, image.value)
    else await createAdSlot(payload, image.value)
    ElMessage.success('已儲存')
    visible.value = false
    await load()
  } catch (e) {
    formError.value = errText(e, '儲存失敗，請稍後再試')
  } finally {
    saving.value = false
  }
}
async function remove(row: AdSlotDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除版位「${row.nameZh || row.slotCode}」嗎？已有檔期的版位不能刪除，請改為停用。`, '刪除版位', {
      confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteAdSlot(row.id)
    ElMessage.success('已刪除')
    await load()
  } catch (e) {
    ElMessage.error(errText(e, '刪除失敗，請稍後再試'))
  }
}
</script>

<template>
  <div>
    <el-card shadow="never" class="slot__block">
      <div class="slot__row">
        <span class="slot__muted">版位就是 App 畫面上放廣告的位置；每個版位規定素材的長寬比、最小尺寸與檔案大小，上傳的素材不符合時會被擋下。</span>
        <span class="slot__spacer" />
        <el-button v-if="canCreate" type="primary" @click="openDialog(null)">+ 新增版位</el-button>
      </div>
    </el-card>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="5" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never"><el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty></el-card>
    <el-card v-else shadow="never">
      <el-empty v-if="rows.length === 0" description="目前沒有廣告版位"><el-button v-if="canCreate" type="primary" @click="openDialog(null)">+ 新增第一個版位</el-button></el-empty>
      <template v-else>
        <el-table v-if="!isMobile" :data="rows" row-key="id">
          <el-table-column label="版位" min-width="180"><template #default="{ row }">{{ row.nameZh || '（未命名）' }}<div class="slot__muted">代號 {{ row.slotCode }}</div></template></el-table-column>
          <el-table-column label="素材規格" min-width="240"><template #default="{ row }">{{ specText(row) }}</template></el-table-column>
          <el-table-column label="同時輪播" width="100"><template #default="{ row }">最多 {{ row.rotationCap }} 張</template></el-table-column>
          <el-table-column label="備援素材" width="100"><template #default="{ row }">{{ row.fallbackImageKey ? '已設定' : '尚未設定' }}</template></el-table-column>
          <el-table-column label="檔期數" width="80" prop="campaignCount" />
          <el-table-column label="狀態" width="90"><template #default="{ row }"><el-tag :type="row.isActive ? 'success' : 'info'" size="small">{{ row.isActive ? '啟用' : '停用' }}</el-tag></template></el-table-column>
          <el-table-column label="操作" width="130" fixed="right">
            <template #default="{ row }">
              <el-button size="small" text type="primary" @click="openDialog(row)">{{ canUpdate ? '編輯' : '檢視' }}</el-button>
              <el-button v-if="canDelete" size="small" text type="danger" :disabled="row.campaignCount > 0" @click="remove(row)">刪除</el-button>
            </template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">{{ row.nameZh || row.slotCode }}</template>
          <template #meta="{ row }">
            <el-tag :type="row.isActive ? 'success' : 'info'" size="small">{{ row.isActive ? '啟用' : '停用' }}</el-tag>
            <span>{{ specText(row) }}</span><span>檔期 {{ row.campaignCount }} 個</span>
          </template>
          <template #actions="{ row }">
            <el-button size="small" text type="primary" @click="openDialog(row)">{{ canUpdate ? '編輯' : '檢視' }}</el-button>
            <el-button v-if="canDelete" size="small" text type="danger" :disabled="row.campaignCount > 0" @click="remove(row)">刪除</el-button>
          </template>
        </MobileCardList>
      </template>
    </el-card>

    <el-dialog v-model="visible" :title="editing ? '編輯版位' : '新增版位'" width="720px" :fullscreen="isMobile" :close-on-click-modal="false">
      <el-alert v-if="formError" type="error" show-icon :closable="false" :title="formError" class="slot__block" />
      <el-alert v-if="readOnly" type="info" show-icon :closable="false" title="你的帳號只有檢視權限" class="slot__block" />
      <el-form label-position="top" :disabled="readOnly">
        <el-row :gutter="12">
          <el-col :xs="24" :sm="12"><el-form-item label="版位代號（建立後不能修改）" required><el-input v-model="form.slotCode" :disabled="!!editing" placeholder="小寫英文字母、數字與底線" /></el-form-item></el-col>
          <el-col :xs="24" :sm="12"><el-form-item label="狀態"><el-switch v-model="form.isActive" active-text="啟用" inactive-text="停用" /></el-form-item></el-col>
        </el-row>
        <BilingualShortField v-model:zh="form.nameZh" v-model:en="form.nameEn" label="版位名稱" required />
        <el-row :gutter="12">
          <el-col :xs="24" :sm="8"><el-form-item label="所在畫面（選填）"><el-input v-model="form.screenCode" /></el-form-item></el-col>
          <el-col :xs="24" :sm="8"><el-form-item label="在畫面中的順序"><el-input-number v-model="form.blockOrder" :min="0" controls-position="right" style="width: 100%" /></el-form-item></el-col>
          <el-col :xs="24" :sm="8"><el-form-item label="圖片長寬比"><el-input v-model="form.aspectRatio" placeholder="例如 16:9" /></el-form-item></el-col>
          <el-col :xs="24" :sm="8"><el-form-item label="圖片最小寬度（像素）"><el-input-number v-model="form.minWidth" :min="1" controls-position="right" style="width: 100%" /></el-form-item></el-col>
          <el-col :xs="24" :sm="8"><el-form-item label="圖片最小高度（像素）"><el-input-number v-model="form.minHeight" :min="1" controls-position="right" style="width: 100%" /></el-form-item></el-col>
          <el-col :xs="24" :sm="8"><el-form-item label="檔案大小上限（KB）"><el-input-number v-model="form.maxFileKb" :min="1" controls-position="right" style="width: 100%" /></el-form-item></el-col>
          <el-col :xs="24" :sm="8"><el-form-item label="允許的檔案格式"><el-input v-model="form.allowedFormats" placeholder="例如 jpg,png,webp" /></el-form-item></el-col>
          <el-col :xs="24" :sm="8"><el-form-item label="同時輪播張數上限（1–10）"><el-input-number v-model="form.rotationCap" :min="1" :max="10" controls-position="right" style="width: 100%" /></el-form-item></el-col>
          <el-col :xs="24" :sm="8"><el-form-item label="每次開啟 App 的曝光上限"><el-input-number v-model="form.sessionImpressionCap" :min="1" controls-position="right" style="width: 100%" /></el-form-item></el-col>
          <el-col :xs="24"><el-form-item label="允許放影片"><el-switch v-model="form.allowVideo" /></el-form-item></el-col>
        </el-row>
        <el-divider>備援素材（沒有可投放的廣告時顯示，不計入曝光）</el-divider>
        <el-form-item label="備援圖片">
          <ImageUploader v-model:file="image" v-model:remove-cover="removeImage" :has-existing-image="!!editing?.fallbackImageKey" :existing-preview-url="editing?.fallbackImageThumbUrl ?? editing?.fallbackImageUrl ?? null" :min-width="form.minWidth ?? 1" :min-height="form.minHeight ?? 1" :disabled="readOnly || saving" />
        </el-form-item>
        <el-row :gutter="12">
          <el-col :xs="24" :sm="12"><el-form-item label="備援圖片替代文字（中文）"><el-input v-model="form.fallbackAltZh" maxlength="200" /></el-form-item></el-col>
          <el-col :xs="24" :sm="12"><el-form-item label="備援圖片替代文字（英文）"><el-input v-model="form.fallbackAltEn" maxlength="200" /></el-form-item></el-col>
          <el-col :xs="24"><el-form-item label="備援素材點擊後前往（選填）"><el-input v-model="form.fallbackLink" placeholder="網址（https:// 開頭）或 App 內頁面連結" /></el-form-item></el-col>
        </el-row>
      </el-form>
      <p class="slot__hint">兒童向的畫面（課程列表、課程報名表、我的報名）不能設廣告版位，也不設慈善相關版位；違反時儲存會被系統擋下。</p>
      <template #footer>
        <el-button @click="visible = false">{{ readOnly ? '關閉' : '取消' }}</el-button>
        <el-button v-if="!readOnly" type="primary" :loading="saving" @click="save">儲存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.slot__block { margin-bottom: 12px; }
.slot__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.slot__spacer { flex: 1; }
.slot__muted { font-size: 12px; color: var(--admin-text-tertiary); }
.slot__hint { margin: 8px 0 0; font-size: 12px; line-height: 1.6; color: var(--admin-text-tertiary); }
</style>

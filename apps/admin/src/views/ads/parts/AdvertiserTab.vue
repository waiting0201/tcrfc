<script setup lang="ts">
/** 廣告主：新增、編輯、刪除。可選擇關聯既有贊助商（只為避免重複維護聯絡窗口，不是合併）。 */
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import MobileCardList from '@/components/MobileCardList.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { AdminApiError } from '@/api/http'
import { nullIfBlank } from '@/api/adminCommon'
import {
  createAdvertiser,
  deleteAdvertiser,
  listAdvertisers,
  listSponsorOptions,
  updateAdvertiser,
  type AdvertiserDto,
  type AdvertiserStatus,
  type SponsorOptionDto,
} from '@/api/adminAds'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canCreate, canUpdate, canDelete } = useCrudPermissions('ad.advertiser')
const errText = (e: unknown, f: string) => (e instanceof AdminApiError ? e.message : f)

const STATUS_OPTIONS: { value: AdvertiserStatus; label: string }[] = [
  { value: 'negotiating', label: '洽談中' },
  { value: 'active', label: '合作中' },
  { value: 'ended', label: '已結束' },
]
const statusTag = (s: string) => (s === 'active' ? 'success' : s === 'ended' ? 'info' : 'warning')

const filters = reactive({ keyword: '', status: '' })
const rows = ref<AdvertiserDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
async function load() {
  loading.value = true
  loadError.value = null
  try {
    rows.value = await listAdvertisers({ keyword: filters.keyword.trim() || undefined, status: filters.status || undefined })
  } catch (e) {
    rows.value = []
    loadError.value = errText(e, '廣告主清單載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
function clearFilter() {
  Object.assign(filters, { keyword: '', status: '' })
  load()
}
onMounted(load)

// ── 編輯對話框 ──
const visible = ref(false)
const editingId = ref<string | null>(null)
const saving = ref(false)
const formError = ref<string | null>(null)
const form = reactive({
  nameZh: '', nameEn: '', taxId: '', contactName: '', contactPhone: '', contactEmail: '', contractNote: '',
  cooperationStartOn: null as string | null, cooperationEndOn: null as string | null,
  sponsorId: '' as string, status: 'negotiating' as AdvertiserStatus,
})
const sponsorOptions = ref<SponsorOptionDto[]>([])
const sponsorLoading = ref(false)
async function searchSponsors(keyword: string) {
  sponsorLoading.value = true
  try {
    sponsorOptions.value = await listSponsorOptions(keyword.trim() || undefined)
  } catch {
    sponsorOptions.value = []
  } finally {
    sponsorLoading.value = false
  }
}
function openDialog(row: AdvertiserDto | null) {
  editingId.value = row?.id ?? null
  formError.value = null
  Object.assign(form, {
    nameZh: row?.nameZh ?? '', nameEn: row?.nameEn ?? '', taxId: row?.taxId ?? '', contactName: row?.contactName ?? '',
    contactPhone: row?.contactPhone ?? '', contactEmail: row?.contactEmail ?? '', contractNote: row?.contractNote ?? '',
    cooperationStartOn: row?.cooperationStartOn ?? null, cooperationEndOn: row?.cooperationEndOn ?? null,
    sponsorId: row?.sponsorId ?? '', status: row?.status ?? 'negotiating',
  })
  sponsorOptions.value = row?.sponsorId && row.sponsorName ? [{ id: row.sponsorId, name: row.sponsorName, clubCode: null }] : []
  visible.value = true
  searchSponsors('')
}
const readOnly = computed(() => (editingId.value ? !canUpdate.value : !canCreate.value))
async function save() {
  if (readOnly.value) return
  if (!form.nameZh.trim()) {
    formError.value = '請輸入廣告主的中文名稱'
    return
  }
  if (form.cooperationStartOn && form.cooperationEndOn && form.cooperationEndOn < form.cooperationStartOn) {
    formError.value = '合作結束日不能早於開始日'
    return
  }
  saving.value = true
  formError.value = null
  const body = {
    taxId: nullIfBlank(form.taxId),
    contactName: nullIfBlank(form.contactName),
    contactPhone: nullIfBlank(form.contactPhone),
    contactEmail: nullIfBlank(form.contactEmail),
    contractNote: nullIfBlank(form.contractNote),
    cooperationStartOn: form.cooperationStartOn || null,
    cooperationEndOn: form.cooperationEndOn || null,
    sponsorId: form.sponsorId || null,
    status: form.status,
    content: { zh: { name: form.nameZh.trim() }, en: form.nameEn.trim() ? { name: form.nameEn.trim() } : undefined },
  }
  try {
    if (editingId.value) await updateAdvertiser(editingId.value, body)
    else await createAdvertiser(body)
    ElMessage.success('已儲存')
    visible.value = false
    await load()
  } catch (e) {
    formError.value = errText(e, '儲存失敗，請稍後再試')
  } finally {
    saving.value = false
  }
}
async function remove(row: AdvertiserDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除廣告主「${row.nameZh || '（未命名）'}」嗎？已有檔期的廣告主不能刪除，請改為「已結束」。`, '刪除廣告主', {
      confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteAdvertiser(row.id)
    ElMessage.success('已刪除')
    await load()
  } catch (e) {
    ElMessage.error(errText(e, '刪除失敗，請稍後再試'))
  }
}
</script>

<template>
  <div>
    <el-card shadow="never" class="adv__block">
      <div class="adv__row">
        <el-input v-model="filters.keyword" placeholder="搜尋廣告主名稱" clearable class="adv__keyword" @keyup.enter="load" @clear="load">
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-select v-model="filters.status" placeholder="合作狀態" clearable class="adv__select" @change="load">
          <el-option v-for="o in STATUS_OPTIONS" :key="o.value" :label="o.label" :value="o.value" />
        </el-select>
        <el-button type="primary" @click="load">篩選</el-button>
        <el-button @click="clearFilter">清除</el-button>
        <span class="adv__spacer" />
        <el-button v-if="canCreate" type="primary" @click="openDialog(null)">+ 新增廣告主</el-button>
      </div>
    </el-card>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="5" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never"><el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty></el-card>
    <el-card v-else shadow="never">
      <el-empty v-if="rows.length === 0" description="目前沒有廣告主">
        <el-button v-if="canCreate" type="primary" @click="openDialog(null)">+ 新增第一個廣告主</el-button>
      </el-empty>
      <template v-else>
        <el-table v-if="!isMobile" :data="rows" row-key="id">
          <el-table-column label="廣告主" min-width="200">
            <template #default="{ row }">{{ row.nameZh || '（未命名）' }}<div class="adv__muted">{{ row.nameEn || '尚未翻譯' }}</div></template>
          </el-table-column>
          <el-table-column label="聯絡窗口" min-width="180">
            <template #default="{ row }">{{ row.contactName || '—' }}<div class="adv__muted">{{ row.contactPhone || row.contactEmail || '' }}</div></template>
          </el-table-column>
          <el-table-column label="合作期間" width="200"><template #default="{ row }">{{ row.cooperationStartOn || '—' }} ～ {{ row.cooperationEndOn || '—' }}</template></el-table-column>
          <el-table-column label="關聯贊助商" min-width="140"><template #default="{ row }">{{ row.sponsorName || '—' }}</template></el-table-column>
          <el-table-column label="狀態" width="100"><template #default="{ row }"><el-tag :type="statusTag(row.status)" size="small">{{ row.statusLabel }}</el-tag></template></el-table-column>
          <el-table-column label="檔期數" width="80" prop="campaignCount" />
          <el-table-column label="操作" width="130" fixed="right">
            <template #default="{ row }">
              <el-button size="small" text type="primary" @click="openDialog(row)">{{ canUpdate ? '編輯' : '檢視' }}</el-button>
              <el-button v-if="canDelete" size="small" text type="danger" :disabled="row.campaignCount > 0" @click="remove(row)">刪除</el-button>
            </template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">{{ row.nameZh || '（未命名）' }}</template>
          <template #meta="{ row }">
            <el-tag :type="statusTag(row.status)" size="small">{{ row.statusLabel }}</el-tag>
            <span>檔期 {{ row.campaignCount }} 個</span><span v-if="row.contactName">{{ row.contactName }}</span>
          </template>
          <template #actions="{ row }">
            <el-button size="small" text type="primary" @click="openDialog(row)">{{ canUpdate ? '編輯' : '檢視' }}</el-button>
            <el-button v-if="canDelete" size="small" text type="danger" :disabled="row.campaignCount > 0" @click="remove(row)">刪除</el-button>
          </template>
        </MobileCardList>
      </template>
    </el-card>

    <el-dialog v-model="visible" :title="editingId ? '編輯廣告主' : '新增廣告主'" width="640px" :fullscreen="isMobile" :close-on-click-modal="false">
      <el-alert v-if="formError" type="error" show-icon :closable="false" :title="formError" class="adv__block" />
      <el-alert v-if="readOnly" type="info" show-icon :closable="false" title="你的帳號只有檢視權限" class="adv__block" />
      <el-form label-position="top" :disabled="readOnly">
        <BilingualShortField v-model:zh="form.nameZh" v-model:en="form.nameEn" label="廣告主名稱" required />
        <el-row :gutter="12">
          <el-col :xs="24" :sm="12"><el-form-item label="統一編號（選填）"><el-input v-model="form.taxId" maxlength="20" /></el-form-item></el-col>
          <el-col :xs="24" :sm="12">
            <el-form-item label="合作狀態">
              <el-select v-model="form.status" style="width: 100%"><el-option v-for="o in STATUS_OPTIONS" :key="o.value" :label="o.label" :value="o.value" /></el-select>
            </el-form-item>
          </el-col>
          <el-col :xs="24" :sm="8"><el-form-item label="聯絡人"><el-input v-model="form.contactName" /></el-form-item></el-col>
          <el-col :xs="24" :sm="8"><el-form-item label="聯絡電話"><el-input v-model="form.contactPhone" /></el-form-item></el-col>
          <el-col :xs="24" :sm="8"><el-form-item label="聯絡 Email"><el-input v-model="form.contactEmail" /></el-form-item></el-col>
          <el-col :xs="24" :sm="12"><el-form-item label="合作開始日"><el-date-picker v-model="form.cooperationStartOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item></el-col>
          <el-col :xs="24" :sm="12"><el-form-item label="合作結束日"><el-date-picker v-model="form.cooperationEndOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item></el-col>
        </el-row>
        <el-form-item label="關聯既有贊助商（選填）">
          <el-select v-model="form.sponsorId" filterable remote clearable :remote-method="searchSponsors" :loading="sponsorLoading" placeholder="輸入贊助商名稱搜尋" style="width: 100%">
            <el-option v-for="s in sponsorOptions" :key="s.id" :label="s.name" :value="s.id" />
          </el-select>
          <div class="adv__hint">關聯只是為了避免重複維護聯絡窗口，不會把兩邊的資料合併。</div>
        </el-form-item>
        <el-form-item label="合約備註"><el-input v-model="form.contractNote" type="textarea" :rows="3" maxlength="500" show-word-limit /></el-form-item>
      </el-form>
      <p class="adv__hint">合作已結束的廣告主不能再建立新的投放檔期。</p>
      <template #footer>
        <el-button @click="visible = false">{{ readOnly ? '關閉' : '取消' }}</el-button>
        <el-button v-if="!readOnly" type="primary" :loading="saving" @click="save">儲存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.adv__block { margin-bottom: 12px; }
.adv__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.adv__keyword { width: 240px; max-width: 100%; }
.adv__select { width: 150px; max-width: 100%; }
.adv__spacer { flex: 1; }
.adv__muted { font-size: 12px; color: var(--admin-text-tertiary); }
.adv__hint { margin-top: 4px; font-size: 12px; line-height: 1.6; color: var(--admin-text-tertiary); }
</style>

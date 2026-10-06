<script setup lang="ts">
/** 公告條：App 首頁上方的一行公告，可設定顯示期間與對象。時間一律是台灣時間。 */
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import MobileCardList from '@/components/MobileCardList.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import FormField from '@/components/FormField.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { usePermission } from '@/composables/useCrudPermissions'
import { availableClubs } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { nullIfBlank } from '@/api/adminCommon'
import { createAnnouncement, deleteAnnouncement, listAnnouncements, updateAnnouncement, type AnnouncementDto, type AudienceTier } from '@/api/adminApp'
import { formatDateTime, pickerDateToUtc, utcToPickerDate } from '@/utils/dateTime'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const canUpdate = usePermission('app.layout.update')
const errText = (e: unknown, f: string) => (e instanceof AdminApiError ? e.message : f)

const TIERS: { value: AudienceTier; label: string }[] = [
  { value: 'all', label: '所有人' }, { value: 'fan_club', label: '付費球迷會員' }, { value: 'registered', label: '已登入但不是付費球迷會員' }, { value: 'anonymous', label: '尚未登入' },
]
const tierText = (v: string) => TIERS.find((t) => t.value === v)?.label ?? v

const rows = ref<AnnouncementDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
async function load() {
  loading.value = true
  loadError.value = null
  try {
    rows.value = await listAnnouncements()
  } catch (e) {
    rows.value = []
    loadError.value = errText(e, '公告清單載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
onMounted(load)

const visible = ref(false)
const editing = ref<AnnouncementDto | null>(null)
const saving = ref(false)
/** 只放沒有對到欄位的錯誤。 */
const formError = ref<string | null>(null)
const formErrors = provideFormErrors()
const form = reactive({
  messageZh: '', messageEn: '', linkUrl: '', startsAt: null as Date | null, endsAt: null as Date | null,
  audienceTier: 'all' as AudienceTier, audienceClubCode: '', isEnabled: true,
})
function openDialog(row: AnnouncementDto | null) {
  editing.value = row
  formError.value = null
  formErrors.clearAll()
  Object.assign(form, {
    messageZh: row?.messageZh ?? '', messageEn: row?.messageEn ?? '', linkUrl: row?.linkUrl ?? '', startsAt: utcToPickerDate(row?.startsAt), endsAt: utcToPickerDate(row?.endsAt),
    audienceTier: row?.audienceTier ?? 'all', audienceClubCode: row?.audienceClubCode ?? '', isEnabled: row?.isEnabled ?? true,
  })
  visible.value = true
}
async function save() {
  if (!canUpdate.value) return
  formError.value = null
  const errors: Record<string, string> = {}
  if (!form.messageZh.trim()) errors.messageZh = '請輸入中文公告內容'
  if (form.startsAt && form.endsAt && form.endsAt.getTime() <= form.startsAt.getTime()) errors.endsAt = '結束時間必須晚於開始時間'
  const link = form.linkUrl.trim()
  if (link && !/^(tcrfc:\/\/|https?:\/\/)/i.test(link)) errors.linkUrl = '連結格式不正確：請填網址（https:// 開頭）或 App 內頁面連結'
  if (formErrors.replaceAll(errors)) {
    await formErrors.focusFirst()
    return
  }
  saving.value = true
  const body = {
    message: { zh: form.messageZh.trim(), en: form.messageEn.trim() || undefined },
    linkUrl: nullIfBlank(form.linkUrl),
    startsAt: pickerDateToUtc(form.startsAt),
    endsAt: pickerDateToUtc(form.endsAt),
    audienceTier: form.audienceTier,
    audienceClubCode: form.audienceClubCode || null,
    isEnabled: form.isEnabled,
  }
  try {
    if (editing.value) await updateAnnouncement(editing.value.id, body)
    else await createAnnouncement(body)
    ElMessage.success('已儲存')
    visible.value = false
    await load()
  } catch (e) {
    if (e instanceof AdminApiError && formErrors.applyApiError(e)) return
    formError.value = errText(e, '儲存失敗，請稍後再試')
  } finally {
    saving.value = false
  }
}
async function remove(row: AnnouncementDto) {
  try {
    await ElMessageBox.confirm('確定要刪除這則公告嗎？', '刪除公告', { confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning' })
  } catch {
    return
  }
  try {
    await deleteAnnouncement(row.id)
    ElMessage.success('已刪除')
    await load()
  } catch (e) {
    ElMessage.error(errText(e, '刪除失敗，請稍後再試'))
  }
}
const periodText = (r: AnnouncementDto) => (r.startsAt || r.endsAt ? `${formatDateTime(r.startsAt) || '不限'} ～ ${formatDateTime(r.endsAt) || '不限'}` : '不限期間')
</script>

<template>
  <div>
    <el-card shadow="never" class="an__block">
      <div class="an__row">
        <span class="an__muted">公告條會顯示在 App 首頁上方。對象不是「所有人」的公告，App 需要辨識使用者身分才會依對象篩選。</span>
        <span class="an__spacer" />
        <el-button v-if="canUpdate" type="primary" @click="openDialog(null)">+ 新增公告</el-button>
      </div>
    </el-card>
    <el-card v-if="loading" shadow="never"><el-skeleton :rows="4" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never"><el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty></el-card>
    <el-card v-else shadow="never">
      <el-empty v-if="rows.length === 0" description="目前沒有公告" />
      <template v-else>
        <el-table v-if="!isMobile" :data="rows" row-key="id">
          <el-table-column label="公告內容" min-width="240"><template #default="{ row }">{{ row.messageZh }}<div class="an__muted">{{ row.messageEn || '尚未翻譯' }}</div></template></el-table-column>
          <el-table-column label="顯示期間（台灣時間）" min-width="230"><template #default="{ row }">{{ periodText(row) }}</template></el-table-column>
          <el-table-column label="對象" min-width="140"><template #default="{ row }">{{ tierText(row.audienceTier) }}</template></el-table-column>
          <el-table-column label="狀態" width="110"><template #default="{ row }"><el-tag :type="row.isActiveNow ? 'success' : 'info'" size="small">{{ row.isActiveNow ? '顯示中' : row.isEnabled ? '未在期間內' : '已關閉' }}</el-tag></template></el-table-column>
          <el-table-column v-if="canUpdate" label="操作" width="120" fixed="right">
            <template #default="{ row }"><el-button size="small" text type="primary" @click="openDialog(row)">編輯</el-button><el-button size="small" text type="danger" @click="remove(row)">刪除</el-button></template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">{{ row.messageZh }}</template>
          <template #meta="{ row }"><el-tag :type="row.isActiveNow ? 'success' : 'info'" size="small">{{ row.isActiveNow ? '顯示中' : row.isEnabled ? '未在期間內' : '已關閉' }}</el-tag><span>{{ periodText(row) }}</span><span>對象：{{ tierText(row.audienceTier) }}</span></template>
          <template #actions="{ row }"><template v-if="canUpdate"><el-button size="small" text type="primary" @click="openDialog(row)">編輯</el-button><el-button size="small" text type="danger" @click="remove(row)">刪除</el-button></template></template>
        </MobileCardList>
      </template>
    </el-card>

    <el-dialog v-model="visible" :title="editing ? '編輯公告' : '新增公告'" width="600px" :fullscreen="isMobile" :close-on-click-modal="false" destroy-on-close>
      <el-alert v-if="formError" type="error" show-icon :closable="false" :title="formError" class="an__block" />
      <el-form label-position="top">
        <LangTabsBar variant="bare">
          <BilingualShortField v-model:zh="form.messageZh" v-model:en="form.messageEn" field="message" label="公告內容（200 字內）" required />
          <FormField field="linkUrl" label="點擊後前往（選填）"><el-input v-model="form.linkUrl" placeholder="網址（https:// 開頭）或 App 內頁面連結" /></FormField>
          <el-row :gutter="12">
            <el-col :xs="24" :sm="12"><el-form-item label="開始時間（台灣時間，選填）"><el-date-picker v-model="form.startsAt" type="datetime" style="width: 100%" /></el-form-item></el-col>
            <el-col :xs="24" :sm="12"><FormField field="endsAt" label="結束時間（台灣時間，選填）"><el-date-picker v-model="form.endsAt" type="datetime" style="width: 100%" @change="formErrors.clear('endsAt')" /></FormField></el-col>
            <el-col :xs="24" :sm="12"><FormField field="audienceTier" label="公告對象"><el-select v-model="form.audienceTier" style="width: 100%"><el-option v-for="t in TIERS" :key="t.value" :label="t.label" :value="t.value" /></el-select></FormField></el-col>
            <el-col :xs="24" :sm="12"><FormField field="audienceClubCode" label="限定俱樂部（選填）"><el-select v-model="form.audienceClubCode" clearable placeholder="不限" style="width: 100%"><el-option v-for="c in availableClubs" :key="c.code" :label="c.name" :value="c.code" /></el-select></FormField></el-col>
          </el-row>
          <el-form-item label="啟用"><el-switch v-model="form.isEnabled" /></el-form-item>
        </LangTabsBar>
      </el-form>
      <template #footer>
        <el-button @click="visible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="save">儲存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.an__block { margin-bottom: 12px; }
.an__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.an__spacer { flex: 1; }
.an__muted { font-size: 12px; line-height: 1.6; color: var(--admin-text-tertiary); }
</style>

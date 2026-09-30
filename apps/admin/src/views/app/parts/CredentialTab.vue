<script setup lang="ts">
/**
 * 金鑰與憑證列管：推播金鑰、開發者帳號、地圖服務金鑰等的到期與輪替提醒。
 * 🔴 這裡只登記管理資訊（名稱、編號、到期日、輪替週期），**絕對不要貼上金鑰本身**。
 * 🔴 部分金鑰沒有到期日：沒填到期日時，以「上次輪替日（沒有就用建立日）＋輪替週期」推算；已屆期或 60 天內屆期會提醒。
 * 🔴 輪替與修改只有系統管理員能做，並會留下紀錄。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { usePermission } from '@/composables/useCrudPermissions'
import { AdminApiError } from '@/api/http'
import { nullIfBlank } from '@/api/adminCommon'
import { createCredential, deleteCredential, listCredentials, rotateCredential, updateCredential, type CredentialDto } from '@/api/adminApp'
import { taipeiToday } from '@/utils/dateTime'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const canUpdate = usePermission('app.credential.update')
const errText = (e: unknown, f: string) => (e instanceof AdminApiError ? e.message : f)

const KINDS = [
  { value: 'apns_key', label: '蘋果推播金鑰' },
  { value: 'fcm_credential', label: 'Google 推播認證資料' },
  { value: 'apple_developer_program', label: '蘋果開發者計畫' },
  { value: 'google_play_account', label: 'Google Play 帳號' },
  { value: 'maps_api_key', label: '地圖服務金鑰' },
  { value: 'other', label: '其他' },
]
const kindText = (r: CredentialDto) => KINDS.find((k) => k.value === r.kind)?.label ?? r.kindLabel
const healthTag = (h: string) => (h === 'ok' ? 'success' : h === 'due_soon' ? 'warning' : h === 'overdue' ? 'danger' : 'info')
const healthText = (r: CredentialDto) => (r.health === 'ok' ? '正常' : r.health === 'due_soon' ? '即將屆期' : r.health === 'overdue' ? '已屆期' : '未設定期限')
const dueText = (r: CredentialDto) => (r.nextDueOn ? `${r.nextDueOn}${r.daysUntilDue != null ? `（${r.daysUntilDue >= 0 ? `還有 ${r.daysUntilDue} 天` : `已逾 ${-r.daysUntilDue} 天`}）` : ''}` : '—')

const rows = ref<CredentialDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
async function load() {
  loading.value = true
  loadError.value = null
  try {
    rows.value = await listCredentials()
  } catch (e) {
    rows.value = []
    loadError.value = errText(e, '憑證列管清單載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
onMounted(load)

const visible = ref(false)
const editing = ref<CredentialDto | null>(null)
const saving = ref(false)
const formError = ref<string | null>(null)
const form = reactive({
  kind: 'apns_key', label: '', externalRef: '', createdOn: null as string | null, lastRotatedOn: null as string | null,
  expiresOn: null as string | null, rotationPeriodDays: null as number | null, note: '',
})
function openDialog(row: CredentialDto | null) {
  editing.value = row
  formError.value = null
  Object.assign(form, {
    kind: row?.kind ?? 'apns_key', label: row?.label ?? '', externalRef: row?.externalRef ?? '', createdOn: row?.createdOn ?? null,
    lastRotatedOn: row?.lastRotatedOn ?? null, expiresOn: row?.expiresOn ?? null, rotationPeriodDays: row?.rotationPeriodDays ?? null, note: row?.note ?? '',
  })
  visible.value = true
}
async function save() {
  if (!canUpdate.value) return
  if (!form.label.trim()) return void (formError.value = '請輸入名稱')
  if (!form.expiresOn && !form.rotationPeriodDays) return void (formError.value = '請至少填「到期日」或「輪替週期（天）」其中一項，系統才能提醒屆期')
  saving.value = true
  formError.value = null
  const body = {
    kind: form.kind, label: form.label.trim(), externalRef: nullIfBlank(form.externalRef), createdOn: form.createdOn || null,
    lastRotatedOn: form.lastRotatedOn || null, expiresOn: form.expiresOn || null, rotationPeriodDays: form.rotationPeriodDays, note: nullIfBlank(form.note),
  }
  try {
    if (editing.value) await updateCredential(editing.value.id, body)
    else await createCredential(body)
    ElMessage.success('已儲存')
    visible.value = false
    await load()
  } catch (e) {
    formError.value = errText(e, '儲存失敗，請稍後再試')
  } finally {
    saving.value = false
  }
}
async function remove(row: CredentialDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除「${row.label}」的列管紀錄嗎？（只刪除列管資訊，不影響實際的金鑰）`, '刪除列管', { confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning' })
  } catch {
    return
  }
  try {
    await deleteCredential(row.id)
    ElMessage.success('已刪除')
    await load()
  } catch (e) {
    ElMessage.error(errText(e, '刪除失敗，請稍後再試'))
  }
}

// ── 輪替 ──
const rotateVisible = ref(false)
const rotateTarget = ref<CredentialDto | null>(null)
const rotateExpires = ref<string | null>(null)
const rotating = ref(false)
const rotateError = ref<string | null>(null)
function openRotate(row: CredentialDto) {
  rotateTarget.value = row
  rotateExpires.value = null
  rotateError.value = null
  rotateVisible.value = true
}
async function doRotate() {
  if (!rotateTarget.value) return
  if (rotateExpires.value && rotateExpires.value <= taipeiToday()) return void (rotateError.value = '新的到期日必須晚於今天')
  rotating.value = true
  try {
    await rotateCredential(rotateTarget.value.id, rotateExpires.value || undefined)
    ElMessage.success('已記錄輪替')
    rotateVisible.value = false
    await load()
  } catch (e) {
    rotateError.value = errText(e, '記錄失敗，請稍後再試')
  } finally {
    rotating.value = false
  }
}
</script>

<template>
  <div>
    <el-alert class="cr__block" type="warning" show-icon :closable="false" title="這裡只登記管理資訊，請勿貼上金鑰或密碼本身" description="列管的目的是提醒「哪把金鑰什麼時候要換」。金鑰本身請存放在雲端的密鑰保管服務，不要輸入到後台的任何欄位。" />
    <el-alert v-if="!canUpdate" class="cr__block" type="info" show-icon :closable="false" title="金鑰列管只有系統管理員能修改，你的帳號只能檢視。" />
    <el-card shadow="never" class="cr__block">
      <div class="cr__row">
        <span class="cr__muted">依下次屆期日排序，最急的在最前面。已屆期或 60 天內屆期會提醒。</span>
        <span class="cr__spacer" />
        <el-button v-if="canUpdate" type="primary" @click="openDialog(null)">+ 新增列管</el-button>
      </div>
    </el-card>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="4" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never"><el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty></el-card>
    <el-card v-else shadow="never">
      <el-empty v-if="rows.length === 0" description="還沒有列管的金鑰或憑證" />
      <template v-else>
        <el-table v-if="!isMobile" :data="rows" row-key="id">
          <el-table-column label="名稱" min-width="200"><template #default="{ row }">{{ row.label }}<div class="cr__muted">{{ kindText(row) }}<template v-if="row.externalRef">・編號 {{ row.externalRef }}</template></div></template></el-table-column>
          <el-table-column label="狀態" width="120"><template #default="{ row }"><el-tag :type="healthTag(row.health)" size="small">{{ healthText(row) }}</el-tag></template></el-table-column>
          <el-table-column label="下次屆期" min-width="200"><template #default="{ row }">{{ dueText(row) }}<div class="cr__muted">{{ row.expiresOn ? '依到期日' : row.rotationPeriodDays ? `依輪替週期 ${row.rotationPeriodDays} 天` : '' }}</div></template></el-table-column>
          <el-table-column label="上次輪替" width="120"><template #default="{ row }">{{ row.lastRotatedOn || '—' }}</template></el-table-column>
          <el-table-column v-if="canUpdate" label="操作" width="200" fixed="right">
            <template #default="{ row }"><el-button size="small" text type="primary" @click="openRotate(row)">記錄輪替</el-button><el-button size="small" text type="primary" @click="openDialog(row)">編輯</el-button><el-button size="small" text type="danger" @click="remove(row)">刪除</el-button></template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">{{ row.label }}</template>
          <template #meta="{ row }"><el-tag :type="healthTag(row.health)" size="small">{{ healthText(row) }}</el-tag><span>{{ kindText(row) }}</span><span>下次屆期 {{ dueText(row) }}</span></template>
          <template #actions="{ row }"><template v-if="canUpdate"><el-button size="small" text type="primary" @click="openRotate(row)">記錄輪替</el-button><el-button size="small" text type="primary" @click="openDialog(row)">編輯</el-button><el-button size="small" text type="danger" @click="remove(row)">刪除</el-button></template></template>
        </MobileCardList>
      </template>
    </el-card>

    <el-dialog v-model="visible" :title="editing ? '編輯列管' : '新增列管'" width="560px" :fullscreen="isMobile" :close-on-click-modal="false">
      <el-alert v-if="formError" type="error" show-icon :closable="false" :title="formError" class="cr__block" />
      <el-form label-position="top">
        <el-row :gutter="12">
          <el-col :xs="24" :sm="12"><el-form-item label="種類"><el-select v-model="form.kind" style="width: 100%"><el-option v-for="k in KINDS" :key="k.value" :label="k.label" :value="k.value" /></el-select></el-form-item></el-col>
          <el-col :xs="24" :sm="12"><el-form-item label="名稱" required><el-input v-model="form.label" maxlength="100" /></el-form-item></el-col>
          <el-col :xs="24"><el-form-item label="金鑰編號（選填，只填編號，不是金鑰本身）"><el-input v-model="form.externalRef" maxlength="100" /></el-form-item></el-col>
          <el-col :xs="24" :sm="8"><el-form-item label="建立日"><el-date-picker v-model="form.createdOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item></el-col>
          <el-col :xs="24" :sm="8"><el-form-item label="上次輪替日"><el-date-picker v-model="form.lastRotatedOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item></el-col>
          <el-col :xs="24" :sm="8"><el-form-item label="到期日（沒有就留空）"><el-date-picker v-model="form.expiresOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item></el-col>
          <el-col :xs="24" :sm="12"><el-form-item label="輪替週期（天）"><el-input-number v-model="form.rotationPeriodDays" :min="1" controls-position="right" style="width: 100%" /></el-form-item></el-col>
        </el-row>
        <el-form-item label="備註"><el-input v-model="form.note" type="textarea" :rows="2" maxlength="300" /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="visible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="save">儲存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="rotateVisible" title="記錄輪替" width="440px" :fullscreen="isMobile" :close-on-click-modal="false">
      <p class="cr__muted">「{{ rotateTarget?.label }}」已經完成換新。系統會把「上次輪替日」記為今天（台灣日期），並留下紀錄。</p>
      <el-alert v-if="rotateError" type="error" show-icon :closable="false" :title="rotateError" class="cr__block" />
      <el-form label-position="top"><el-form-item label="新的到期日（選填，必須晚於今天）"><el-date-picker v-model="rotateExpires" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item></el-form>
      <template #footer>
        <el-button @click="rotateVisible = false">取消</el-button>
        <el-button type="primary" :loading="rotating" @click="doRotate">確認記錄</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.cr__block { margin-bottom: 12px; }
.cr__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.cr__spacer { flex: 1; }
.cr__muted { font-size: 12px; line-height: 1.6; color: var(--admin-text-tertiary); }
</style>

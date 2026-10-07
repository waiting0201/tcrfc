<script setup lang="ts">
/**
 * 功能開關：遠端開關 App 的功能。同名開關同時有「全部平台」與單一平台的設定時，單一平台的值優先。
 * 🔴 付款模式是唯一的三態開關（關閉／外開瀏覽器付款／App 內付款），而且只能降級：不能新增為「App 內付款」，
 *    也不能從「外開瀏覽器付款」或「關閉」改成「App 內付款」——以外開送審、通過後再遠端開啟 App 內付款是違規，會被商店下架。
 * 🔴 存檔後要同步到靜態設定檔；同步目前尚未串接（存檔本身仍會成功）。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import MobileCardList from '@/components/MobileCardList.vue'
import EdgeNotice from './EdgeNotice.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { usePermission } from '@/composables/useCrudPermissions'
import { AdminApiError } from '@/api/http'
import { formatDateTime } from '@/utils/dateTime'
import { nullIfBlank } from '@/api/adminCommon'
import { createFlag, deleteFlag, listFlags, updateFlag, type EdgePublishDto, type FlagDto, type FlagPlatform } from '@/api/adminApp'
import FormField from '@/components/FormField.vue'
import { provideFormErrors } from '@/composables/useFormErrors'

const formErrors = provideFormErrors()

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const canUpdate = usePermission('app.config.update')
const errText = (e: unknown, f: string) => (e instanceof AdminApiError ? e.message : f)
const PAYMENT_KEY = 'payment_mode'
const PAYMENT_OPTIONS = [
  { value: 'off', label: '關閉付款' },
  { value: 'external', label: '外開瀏覽器付款' },
  { value: 'inapp', label: 'App 內付款' },
]
/** 已知開關的白話名稱（值仍以原代碼送出）；未知開關退回後端的說明文字，並去掉括號內的技術代碼。 */
const FLAG_LABELS: Record<string, string> = {
  ads_enabled: '廣告版位總開關',
  map_enabled: '附近店家地圖',
  biometric_unlock_enabled: '會員卡生物辨識快速開啟',
  [PAYMENT_KEY]: '付款模式',
}
const CUSTOM_FLAG = '__custom__'
const flagOptions = Object.entries(FLAG_LABELS).map(([value, label]) => ({ value, label }))
function flagTitle(row: FlagDto): string {
  const known = FLAG_LABELS[row.flagKey]
  if (known) return known
  const cleaned = (row.description ?? '').replace(/（[A-Za-z_／/]+）/g, '').split('：')[0]?.trim()
  return cleaned || '（未命名開關）'
}
const paymentText = (v: string | null) => PAYMENT_OPTIONS.find((o) => o.value === v)?.label ?? v ?? ''

const rows = ref<FlagDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
const edge = ref<EdgePublishDto | null>(null)
async function load() {
  loading.value = true
  loadError.value = null
  try {
    rows.value = await listFlags()
  } catch (e) {
    rows.value = []
    loadError.value = errText(e, '功能開關載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
onMounted(load)

const platformText = (p: string) => (p === 'ios' ? 'iPhone' : p === 'android' ? 'Android' : '全部平台')

async function toggle(row: FlagDto, value: boolean) {
  try {
    edge.value = (await updateFlag(row.id, { isEnabled: value, stringValue: row.stringValue, description: row.description })).edgePublish
    row.isEnabled = value
    ElMessage.success('已更新')
  } catch (e) {
    ElMessage.error(errText(e, '更新失敗，請稍後再試'))
    await load()
  }
}

// ── 編輯 ──
const visible = ref(false)
const editing = ref<FlagDto | null>(null)
const saving = ref(false)
const formError = ref<string | null>(null)
const flagChoice = ref<string>('ads_enabled')
const form = reactive({ flagKey: '', platform: 'all' as FlagPlatform, isEnabled: true, stringValue: 'external', description: '' })
const isPayment = computed(() => (editing.value ? editing.value.flagKey : form.flagKey.trim()) === PAYMENT_KEY)
/** 只能降級：目前不是 App 內付款時，不提供「App 內付款」選項。 */
const paymentOptions = computed(() => PAYMENT_OPTIONS.map((o) => ({ ...o, disabled: o.value === 'inapp' && editing.value?.stringValue !== 'inapp' })))
function openDialog(row: FlagDto | null) {
  editing.value = row
  formError.value = null
  formErrors.clearAll()
  Object.assign(form, { flagKey: row?.flagKey ?? '', platform: row?.platform ?? 'all', isEnabled: row?.isEnabled ?? true, stringValue: row?.stringValue ?? 'external', description: row?.description ?? '' })
  flagChoice.value = row ? (FLAG_LABELS[row.flagKey] ? row.flagKey : CUSTOM_FLAG) : flagOptions[0]!.value
  if (!row) form.flagKey = flagOptions[0]!.value
  visible.value = true
}
function onFlagChoice(v: string) {
  form.flagKey = v === CUSTOM_FLAG ? '' : v
}
async function save() {
  if (!canUpdate.value) return
  formError.value = null
  formErrors.clearAll()
  if (!editing.value && !/^[a-z0-9]+(_[a-z0-9]+)+$/.test(form.flagKey.trim())) {
    formErrors.set('flagKey', '識別名稱請用小寫英數字與底線、至少兩段（由 App 工程團隊提供），建立後不能修改')
    return void (await formErrors.focusFirst())
  }
  saving.value = true
  const body = {
    flagKey: editing.value ? undefined : form.flagKey.trim(),
    platform: editing.value ? undefined : form.platform,
    isEnabled: form.isEnabled,
    stringValue: isPayment.value ? form.stringValue : null,
    description: nullIfBlank(form.description),
  }
  try {
    const r = editing.value ? await updateFlag(editing.value.id, body) : await createFlag(body)
    edge.value = r.edgePublish
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
async function remove(row: FlagDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除開關「${flagTitle(row)}」（${platformText(row.platform)}）嗎？App 會改用預設值。`, '刪除功能開關', { confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning' })
  } catch {
    return
  }
  try {
    edge.value = (await deleteFlag(row.id)).edgePublish
    ElMessage.success('已刪除')
    await load()
  } catch (e) {
    ElMessage.error(errText(e, '刪除失敗，請稍後再試'))
  }
}
</script>

<template>
  <div>
    <EdgeNotice :edge="edge" />
    <el-alert v-if="!canUpdate" class="fl__block" type="info" show-icon :closable="false" title="功能開關只有系統管理員能修改，你的帳號只能檢視。" />
    <el-card shadow="never" class="fl__block">
      <div class="fl__row">
        <span class="fl__muted">同一個開關同時有「全部平台」和單一平台的設定時，單一平台的值優先。付款模式只能降級，不能改成「App 內付款」。</span>
        <span class="fl__spacer" />
        <el-button v-if="canUpdate" type="primary" @click="openDialog(null)">+ 新增開關</el-button>
      </div>
    </el-card>
    <el-card v-if="loading" shadow="never"><el-skeleton :rows="5" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never"><el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty></el-card>
    <el-card v-else shadow="never">
      <el-empty v-if="rows.length === 0" description="還沒有功能開關" />
      <template v-else>
        <el-table v-if="!isMobile" :data="rows" row-key="id">
          <el-table-column label="開關" min-width="240"><template #default="{ row }">{{ flagTitle(row) }}</template></el-table-column>
          <el-table-column label="適用平台" width="110"><template #default="{ row }">{{ platformText(row.platform) }}</template></el-table-column>
          <el-table-column label="狀態" min-width="180">
            <template #default="{ row }">
              <el-tag v-if="row.flagKey === PAYMENT_KEY" :type="row.stringValue === 'inapp' ? 'warning' : 'info'" size="small">{{ paymentText(row.stringValue) }}</el-tag>
              <el-switch v-else :model-value="row.isEnabled" :disabled="!canUpdate" @change="(v: boolean | string | number) => toggle(row, !!v)" />
            </template>
          </el-table-column>
          <el-table-column label="更新時間（台灣時間）" width="170"><template #default="{ row }">{{ formatDateTime(row.updatedAt) }}</template></el-table-column>
          <el-table-column v-if="canUpdate" label="操作" width="120" fixed="right"><template #default="{ row }"><el-button size="small" text type="primary" @click="openDialog(row)">編輯</el-button><el-button size="small" text type="danger" @click="remove(row)">刪除</el-button></template></el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">{{ flagTitle(row) }}</template>
          <template #meta="{ row }">
            <span>{{ platformText(row.platform) }}</span>
            <el-tag v-if="row.flagKey === PAYMENT_KEY" :type="row.stringValue === 'inapp' ? 'warning' : 'info'" size="small">{{ paymentText(row.stringValue) }}</el-tag>
            <el-tag v-else :type="row.isEnabled ? 'success' : 'info'" size="small">{{ row.isEnabled ? '開啟' : '關閉' }}</el-tag>
          </template>
          <template #actions="{ row }">
            <template v-if="canUpdate">
              <el-button v-if="row.flagKey !== PAYMENT_KEY" size="small" text @click="toggle(row, !row.isEnabled)">{{ row.isEnabled ? '關閉' : '開啟' }}</el-button>
              <el-button size="small" text type="primary" @click="openDialog(row)">編輯</el-button>
              <el-button size="small" text type="danger" @click="remove(row)">刪除</el-button>
            </template>
          </template>
        </MobileCardList>
      </template>
    </el-card>

    <el-dialog v-model="visible" :title="editing ? '編輯功能開關' : '新增功能開關'" width="520px" :fullscreen="isMobile" :close-on-click-modal="false">
      <el-alert v-if="formError" type="error" show-icon :closable="false" :title="formError" class="fl__block" />
      <el-form label-position="top">
        <el-form-item label="開關項目（建立後不能修改）" required>
          <el-select v-model="flagChoice" :disabled="!!editing" style="width: 100%" @change="onFlagChoice">
            <el-option v-for="o in flagOptions" :key="o.value" :label="o.label" :value="o.value" />
            <el-option label="其他（由 App 工程團隊提供識別名稱）" :value="CUSTOM_FLAG" />
          </el-select>
        </el-form-item>
        <FormField v-if="flagChoice === CUSTOM_FLAG" field="flagKey" label="識別名稱（建立後不能修改）" required>
          <el-input v-model="form.flagKey" :disabled="!!editing" placeholder="請向 App 工程團隊確認" />
        </FormField>
        <FormField field="platform" label="適用平台（建立後不能修改）">
          <el-select v-model="form.platform" :disabled="!!editing" style="width: 100%"><el-option label="全部平台" value="all" /><el-option label="iPhone（iOS）" value="ios" /><el-option label="Android" value="android" /></el-select>
        </FormField>
        <FormField v-if="isPayment" field="stringValue" label="付款模式">
          <el-select v-model="form.stringValue" style="width: 100%"><el-option v-for="o in paymentOptions" :key="o.value" :label="o.label" :value="o.value" :disabled="o.disabled" /></el-select>
          <div class="fl__muted">只能降級：不能新增為「App 內付款」，也不能從其他模式改成「App 內付款」。</div>
        </FormField>
        <el-form-item v-else label="狀態"><el-switch v-model="form.isEnabled" active-text="開啟" inactive-text="關閉" /></el-form-item>
        <el-form-item label="說明"><el-input v-model="form.description" maxlength="200" placeholder="這個開關控制什麼功能" /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="visible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="save">儲存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.fl__block { margin-bottom: 12px; }
.fl__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.fl__spacer { flex: 1; }
.fl__muted { font-size: 12px; line-height: 1.6; color: var(--admin-text-tertiary); }
</style>

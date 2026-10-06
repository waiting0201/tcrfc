<script setup lang="ts">
/** 上架版本清單與門檻設定。版本號用「主.次.修」，建置號不參與比較。 */
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import MobileCardList from '@/components/MobileCardList.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import FormField from '@/components/FormField.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import EdgeNotice from './EdgeNotice.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { usePermission } from '@/composables/useCrudPermissions'
import { AdminApiError } from '@/api/http'
import { nullIfBlank } from '@/api/adminCommon'
import {
  createRelease, deleteRelease, listReleases, setReleaseFlags, updateRelease,
  type EdgePublishDto, type ReleaseDto, type ReleasePlatform, type ReleaseStatus, type SaveReleasePayload,
} from '@/api/adminApp'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const canUpdate = usePermission('app.release.update')
const errText = (e: unknown, f: string) => (e instanceof AdminApiError ? e.message : f)

const STATUS_OPTIONS: { value: ReleaseStatus; label: string }[] = [
  { value: 'testing', label: '測試中' }, { value: 'live', label: '已上架' }, { value: 'withdrawn', label: '已下架' },
]
const statusTag = (s: string) => (s === 'live' ? 'success' : s === 'withdrawn' ? 'info' : 'warning')

const platform = ref('')
const rows = ref<ReleaseDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
const edge = ref<EdgePublishDto | null>(null)
async function load() {
  loading.value = true
  loadError.value = null
  try {
    rows.value = await listReleases(platform.value || undefined)
  } catch (e) {
    rows.value = []
    loadError.value = errText(e, '版本清單載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
onMounted(load)

// ── 門檻 ──
async function setFlag(row: ReleaseDto, which: 'min' | 'recommended', on: boolean) {
  const isMin = which === 'min' ? on : row.isMinSupported
  const isRec = which === 'recommended' ? on : row.isRecommended
  let confirmForceUpdate = false
  if (which === 'min' && on) {
    try {
      await ElMessageBox.confirm(
        `設定後，${row.platformLabel} 低於 ${row.version} 的舊版 App 一啟動就會被強制要求更新，而且不能略過。設錯會讓舊版使用者完全無法使用 App。確定要把 ${row.version} 設為最低支援版本嗎？`,
        '設為最低支援版本（強制更新）',
        { confirmButtonText: '我確定，強制舊版更新', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning' },
      )
    } catch {
      return
    }
    confirmForceUpdate = true
  }
  try {
    const r = await setReleaseFlags(row.id, { isMinSupported: isMin, isRecommended: isRec, confirmForceUpdate })
    edge.value = r.edgePublish
    ElMessage.success('已更新')
    await load()
  } catch (e) {
    ElMessage.error(errText(e, '設定失敗，請稍後再試'))
  }
}

// ── 版本編輯 ──
const visible = ref(false)
const editing = ref<ReleaseDto | null>(null)
const saving = ref(false)
/** 只放沒有對到欄位的錯誤。 */
const formError = ref<string | null>(null)
const formErrors = provideFormErrors()
const form = reactive({
  platform: 'ios' as ReleasePlatform, version: '', buildNumber: null as number | null, releasedOn: null as string | null, status: 'testing' as ReleaseStatus,
  whatsNewZh: '', whatsNewEn: '', forceZh: '', forceEn: '', recommendZh: '', recommendEn: '',
})
function openDialog(row: ReleaseDto | null) {
  editing.value = row
  formError.value = null
  formErrors.clearAll()
  Object.assign(form, {
    platform: row?.platform ?? (platform.value === 'android' ? 'android' : 'ios'), version: row?.version ?? '',
    buildNumber: row?.buildNumber != null ? Number(row.buildNumber) : null, releasedOn: row?.releasedOn ?? null, status: row?.status ?? 'testing',
    whatsNewZh: row?.zh?.whatsNew ?? '', whatsNewEn: row?.en?.whatsNew ?? '', forceZh: row?.zh?.forceMessage ?? '', forceEn: row?.en?.forceMessage ?? '',
    recommendZh: row?.zh?.recommendMessage ?? '', recommendEn: row?.en?.recommendMessage ?? '',
  })
  visible.value = true
}
async function save() {
  if (!canUpdate.value) return
  formError.value = null
  const errors: Record<string, string> = {}
  if (!/^\d+\.\d+\.\d+$/.test(form.version.trim())) errors.version = '版本號請用「主.次.修」的寫法，例如 1.2.0'
  if (formErrors.replaceAll(errors)) {
    await formErrors.focusFirst()
    return
  }
  saving.value = true
  const en = { whatsNew: nullIfBlank(form.whatsNewEn), forceMessage: nullIfBlank(form.forceEn), recommendMessage: nullIfBlank(form.recommendEn) }
  const payload: SaveReleasePayload = {
    platform: form.platform,
    version: form.version.trim(),
    buildNumber: form.buildNumber,
    releasedOn: form.releasedOn || null,
    status: form.status,
    content: {
      zh: { whatsNew: nullIfBlank(form.whatsNewZh), forceMessage: nullIfBlank(form.forceZh), recommendMessage: nullIfBlank(form.recommendZh) },
      en: en.whatsNew || en.forceMessage || en.recommendMessage ? en : undefined,
    },
  }
  try {
    if (editing.value) edge.value = (await updateRelease(editing.value.id, payload)).edgePublish
    else await createRelease(payload)
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
async function remove(row: ReleaseDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除 ${row.platformLabel} ${row.version} 嗎？只有尚未上架、也不是門檻的版本能刪除。`, '刪除版本', {
      confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteRelease(row.id)
    ElMessage.success('已刪除')
    await load()
  } catch (e) {
    ElMessage.error(errText(e, '刪除失敗，請稍後再試'))
  }
}
</script>

<template>
  <div>
    <el-alert v-if="!canUpdate" class="rl__block" type="info" show-icon :closable="false" title="版本與門檻只有系統管理員能修改，你的帳號只能檢視。" />
    <EdgeNotice :edge="edge" />
    <el-card shadow="never" class="rl__block">
      <div class="rl__row">
        <el-select v-model="platform" placeholder="全部平台" clearable class="rl__select" @change="load"><el-option label="iPhone（iOS）" value="ios" /><el-option label="Android" value="android" /></el-select>
        <span class="rl__muted">「最低支援版本」：低於它的舊版一啟動就強制更新、不能略過。「建議版本」：低於它會建議更新，可以略過，每 7 天再提醒一次。只有已上架的版本能設為門檻。</span>
        <span class="rl__spacer" />
        <el-button v-if="canUpdate" type="primary" @click="openDialog(null)">+ 新增版本</el-button>
      </div>
    </el-card>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="5" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never"><el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty></el-card>
    <el-card v-else shadow="never">
      <el-empty v-if="rows.length === 0" description="還沒有版本紀錄" />
      <template v-else>
        <el-table v-if="!isMobile" :data="rows" row-key="id">
          <el-table-column label="平台" width="110" prop="platformLabel" />
          <el-table-column label="版本" min-width="140"><template #default="{ row }">{{ row.version }}<span v-if="row.buildNumber" class="rl__muted">（建置 {{ row.buildNumber }}）</span></template></el-table-column>
          <el-table-column label="上架日" width="120"><template #default="{ row }">{{ row.releasedOn || '—' }}</template></el-table-column>
          <el-table-column label="狀態" width="100"><template #default="{ row }"><el-tag :type="statusTag(row.status)" size="small">{{ row.statusLabel }}</el-tag></template></el-table-column>
          <el-table-column label="門檻" min-width="200">
            <template #default="{ row }">
              <el-tag v-if="row.isMinSupported" type="danger" size="small">最低支援版本</el-tag>
              <el-tag v-if="row.isRecommended" type="success" size="small" class="rl__tag">建議版本</el-tag>
              <span v-if="!row.isMinSupported && !row.isRecommended" class="rl__muted">—</span>
            </template>
          </el-table-column>
          <el-table-column v-if="canUpdate" label="操作" width="300" fixed="right">
            <template #default="{ row }">
              <el-button size="small" text type="primary" @click="openDialog(row)">編輯</el-button>
              <template v-if="row.status === 'live'">
                <el-button v-if="!row.isMinSupported" size="small" text type="danger" @click="setFlag(row, 'min', true)">設為最低支援</el-button>
                <el-button v-else size="small" text @click="setFlag(row, 'min', false)">取消最低支援</el-button>
                <el-button v-if="!row.isRecommended" size="small" text type="primary" @click="setFlag(row, 'recommended', true)">設為建議版本</el-button>
                <el-button v-else size="small" text @click="setFlag(row, 'recommended', false)">取消建議</el-button>
              </template>
              <el-button v-if="row.status !== 'live' && !row.isMinSupported && !row.isRecommended" size="small" text type="danger" @click="remove(row)">刪除</el-button>
            </template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">{{ row.platformLabel }} {{ row.version }}</template>
          <template #meta="{ row }">
            <el-tag :type="statusTag(row.status)" size="small">{{ row.statusLabel }}</el-tag>
            <el-tag v-if="row.isMinSupported" type="danger" size="small">最低支援版本</el-tag>
            <el-tag v-if="row.isRecommended" type="success" size="small">建議版本</el-tag>
          </template>
          <template #actions="{ row }">
            <template v-if="canUpdate">
              <el-button size="small" text type="primary" @click="openDialog(row)">編輯</el-button>
              <template v-if="row.status === 'live'">
                <el-button v-if="!row.isMinSupported" size="small" text type="danger" @click="setFlag(row, 'min', true)">設為最低支援</el-button>
                <el-button v-else size="small" text @click="setFlag(row, 'min', false)">取消最低支援</el-button>
                <el-button v-if="!row.isRecommended" size="small" text type="primary" @click="setFlag(row, 'recommended', true)">設為建議版本</el-button>
                <el-button v-else size="small" text @click="setFlag(row, 'recommended', false)">取消建議</el-button>
              </template>
              <el-button v-if="row.status !== 'live' && !row.isMinSupported && !row.isRecommended" size="small" text type="danger" @click="remove(row)">刪除</el-button>
            </template>
          </template>
        </MobileCardList>
      </template>
    </el-card>

    <el-dialog v-model="visible" :title="editing ? '編輯版本' : '新增版本'" width="720px" :fullscreen="isMobile" :close-on-click-modal="false" destroy-on-close>
      <el-alert v-if="formError" type="error" show-icon :closable="false" :title="formError" class="rl__block" />
      <el-form label-position="top">
        <el-row :gutter="12">
          <el-col :xs="24" :sm="8"><el-form-item label="平台（建立後不能修改）"><el-select v-model="form.platform" :disabled="!!editing" style="width: 100%"><el-option label="iPhone（iOS）" value="ios" /><el-option label="Android" value="android" /></el-select></el-form-item></el-col>
          <el-col :xs="24" :sm="8"><FormField field="version" label="版本號（建立後不能修改）" required><el-input v-model="form.version" :disabled="!!editing" placeholder="例如 1.2.0" /></FormField></el-col>
          <el-col :xs="24" :sm="8"><FormField field="buildNumber" label="建置號（選填）"><el-input-number v-model="form.buildNumber" :min="0" controls-position="right" style="width: 100%" /></FormField></el-col>
          <el-col :xs="24" :sm="12"><el-form-item label="上架日"><el-date-picker v-model="form.releasedOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item></el-col>
          <el-col :xs="24" :sm="12"><FormField field="status" label="狀態"><el-select v-model="form.status" style="width: 100%"><el-option v-for="o in STATUS_OPTIONS" :key="o.value" :label="o.label" :value="o.value" /></el-select></FormField></el-col>
        </el-row>
        <LangTabsBar variant="bare">
          <BilingualTextareaField v-model:zh="form.whatsNewZh" v-model:en="form.whatsNewEn" field="whatsNew" label="更新內容說明" />
          <BilingualTextareaField v-model:zh="form.forceZh" v-model:en="form.forceEn" field="forceMessage" label="強制更新時顯示的訊息" />
          <BilingualTextareaField v-model:zh="form.recommendZh" v-model:en="form.recommendEn" field="recommendMessage" label="建議更新時顯示的訊息" />
        </LangTabsBar>
      </el-form>
      <p class="rl__muted">被設為門檻的版本，不能改成「測試中」或「已下架」。</p>
      <template #footer>
        <el-button @click="visible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="save">儲存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.rl__block { margin-bottom: 12px; }
.rl__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.rl__select { width: 170px; max-width: 100%; }
.rl__spacer { flex: 1; }
.rl__muted { font-size: 12px; line-height: 1.6; color: var(--admin-text-tertiary); }
.rl__tag { margin-left: 6px; }
</style>

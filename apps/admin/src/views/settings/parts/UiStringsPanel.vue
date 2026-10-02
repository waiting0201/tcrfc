<script setup lang="ts">
/**
 * 多語系 → 介面字串翻譯表（規劃書 §4.9「按鈕、表單標籤、提示訊息、錯誤訊息等介面文案的雙語對照維護」）。
 *
 * 權限（apps/api/README.md「H 批」§5）：
 * - `site.string.update`：新增、刪除、改繁中原文與分組，也能改其他語系。
 * - `site.string.translate`（翻譯人員）：**只能改非繁中語系**，伺服器強制——改繁中原文或分組整個請求 403。
 *   所以翻譯人員的編輯視窗把繁中與分組唯讀，送出時也只送非繁中語系。
 * - 只有 `view`：全部唯讀。
 * 空白＝刪除該語系翻譯（繁中不能清空）。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { usePermission } from '@/composables/useCrudPermissions'
import { AdminApiError } from '@/api/http'
import {
  createAdminUiString,
  deleteAdminUiString,
  listAdminUiStringGroups,
  listAdminUiStrings,
  updateAdminUiString,
  type AdminLocale,
  type AdminUiString,
} from '@/api/adminSiteSettings'

const props = defineProps<{ club: string; locales: AdminLocale[] }>()

const PAGE_SIZE = 50
const KEY_RE = /^[a-z0-9][a-z0-9_-]*(\.[a-z0-9][a-z0-9_-]*)*$/

const canUpdate = usePermission('site.string.update')
const canTranslate = usePermission('site.string.translate')
/** 能編輯非繁中語系：有更新或翻譯權限。 */
const canEditTranslations = computed(() => canUpdate.value || canTranslate.value)

const enabledLocales = computed(() => props.locales.filter((l) => l.isEnabled).sort((a, b) => a.sortOrder - b.sortOrder))
const defaultLocale = computed(() => props.locales.find((l) => l.isDefault)?.code ?? 'zh-Hant')
const otherLocales = computed(() => enabledLocales.value.filter((l) => l.code !== defaultLocale.value))

const rows = ref<AdminUiString[]>([])
const total = ref(0)
const page = ref(1)
const state = ref<'loading' | 'ready' | 'error'>('loading')
const errorMessage = ref('')
const groups = ref<string[]>([])
const groupFilter = ref('')
const missingFilter = ref('')
const keyword = ref('')

let seq = 0
async function load() {
  const my = ++seq
  state.value = 'loading'
  try {
    const result = await listAdminUiStrings(props.club, {
      group: groupFilter.value || undefined,
      keyword: keyword.value.trim() || undefined,
      missing: missingFilter.value || undefined,
      page: page.value,
      pageSize: PAGE_SIZE,
    })
    if (my !== seq) return
    rows.value = result.items
    total.value = result.totalCount
    state.value = 'ready'
  } catch (error) {
    if (my !== seq) return
    errorMessage.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
    state.value = 'error'
  }
}
async function loadGroups() {
  try {
    groups.value = await listAdminUiStringGroups(props.club)
  } catch {
    groups.value = []
  }
}
onMounted(() => { void load(); void loadGroups() })
watch(() => props.club, () => { page.value = 1; void load(); void loadGroups() })
watch([groupFilter, missingFilter], () => { page.value = 1; void load() })
watch(page, load)

let keywordTimer: ReturnType<typeof setTimeout> | undefined
function onKeyword() {
  clearTimeout(keywordTimer)
  keywordTimer = setTimeout(() => { page.value = 1; void load() }, 350)
}

const localeName = (code: string): string => props.locales.find((l) => l.code === code)?.name ?? code

// ───────────── 編輯／新增視窗 ─────────────

const dialogOpen = ref(false)
const dialogMode = ref<'create' | 'edit'>('edit')
const editing = ref<AdminUiString | null>(null)
const form = reactive({ key: '', group: '', values: {} as Record<string, string> })
const saving = ref(false)
const dialogError = ref<string | null>(null)

/** 翻譯人員（沒有更新權限）不能動繁中與分組。 */
const sourceLocked = computed(() => dialogMode.value === 'edit' && !canUpdate.value)

function openCreate() {
  dialogMode.value = 'create'
  editing.value = null
  form.key = ''
  form.group = ''
  form.values = Object.fromEntries(enabledLocales.value.map((l) => [l.code, '']))
  dialogError.value = null
  dialogOpen.value = true
}

function openEdit(row: AdminUiString) {
  dialogMode.value = 'edit'
  editing.value = row
  form.key = row.key
  form.group = row.group ?? ''
  form.values = Object.fromEntries(enabledLocales.value.map((l) => [l.code, row.values[l.code] ?? '']))
  dialogError.value = null
  dialogOpen.value = true
}

function validate(): boolean {
  if (dialogMode.value === 'create') {
    const key = form.key.trim()
    if (!key || key.length > 128 || !KEY_RE.test(key)) {
      dialogError.value = '字串代號只能用小寫英文、數字、底線、連字號，並以句點分段（例如 button.submit），最長 128 字。'
      return false
    }
  }
  if (!sourceLocked.value && !(form.values[defaultLocale.value] ?? '').trim()) {
    dialogError.value = `請填寫${localeName(defaultLocale.value)}原文，原文不能留空。`
    return false
  }
  dialogError.value = null
  return true
}

async function handleSave() {
  if (!validate()) return
  saving.value = true
  try {
    if (dialogMode.value === 'create') {
      const values: Record<string, string | null> = {}
      for (const l of enabledLocales.value) values[l.code] = form.values[l.code]?.trim() ? form.values[l.code].trim() : null
      await createAdminUiString(props.club, { key: form.key.trim(), group: form.group.trim() || null, values })
      ElMessage.success('已新增')
    } else if (editing.value) {
      // 翻譯人員只送非繁中語系（空白＝刪除該語系翻譯）；有更新權限者全送
      const values: Record<string, string | null> = {}
      for (const l of enabledLocales.value) {
        if (sourceLocked.value && l.code === defaultLocale.value) continue
        values[l.code] = form.values[l.code]?.trim() ? form.values[l.code].trim() : null
      }
      const body: { group?: string | null; values: Record<string, string | null> } = { values }
      if (!sourceLocked.value) body.group = form.group.trim() || null
      await updateAdminUiString(props.club, editing.value.id, body)
      ElMessage.success('已儲存')
    }
    dialogOpen.value = false
    await load()
    void loadGroups()
  } catch (error) {
    // 409：代號重複；403：翻譯人員的請求動到繁中或分組
    dialogError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

async function handleDelete(row: AdminUiString) {
  try {
    await ElMessageBox.confirm(`確定要刪除字串「${row.key}」嗎？使用到這個字串的畫面會改回顯示預設文字。`, '刪除字串', {
      confirmButtonText: '刪除',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteAdminUiString(props.club, row.id)
    ElMessage.success('已刪除')
    await load()
    void loadGroups()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
  }
}

function isMissing(row: AdminUiString, code: string): boolean {
  return !(row.values[code] ?? '').trim()
}
</script>

<template>
  <div class="ui-strings">
    <el-alert
      v-if="!canUpdate && canTranslate"
      title="你的帳號是翻譯人員：可以填寫與修改其他語言的翻譯，繁體中文原文與分組不能更動。"
      type="info"
      show-icon
      :closable="false"
      class="ui-strings__block"
    />
    <p class="ui-strings__hint">這裡維護按鈕、表單標籤、提示與錯誤訊息等介面文字的各語言對照。翻譯留空代表沒有翻譯，網站會改顯示繁體中文。</p>

    <div class="ui-strings__filters">
      <el-select v-model="groupFilter" clearable placeholder="全部分組" aria-label="分組">
        <el-option v-for="g in groups" :key="g" :label="g" :value="g" />
      </el-select>
      <el-select v-model="missingFilter" clearable placeholder="全部狀態" aria-label="翻譯狀態">
        <el-option v-for="l in otherLocales" :key="l.code" :label="`只看缺${l.name}`" :value="l.code" />
      </el-select>
      <el-input v-model="keyword" clearable placeholder="搜尋字串代號或內容" aria-label="搜尋字串" @input="onKeyword" @clear="onKeyword" />
      <el-button v-if="canUpdate" type="primary" @click="openCreate">+ 新增字串</el-button>
    </div>

    <el-skeleton v-if="state === 'loading' && rows.length === 0" :rows="6" animated />
    <el-empty v-else-if="state === 'error'" :image-size="64" :description="errorMessage">
      <el-button type="primary" @click="load">重新載入</el-button>
    </el-empty>
    <div v-else v-loading="state === 'loading'">
      <el-empty v-if="rows.length === 0" description="沒有符合條件的字串" :image-size="64" />
      <el-table v-else :data="rows" row-key="id" border size="small">
        <el-table-column label="字串代號" min-width="200">
          <template #default="{ row }">
            <code class="ui-strings__key">{{ row.key }}</code>
            <div v-if="row.group" class="ui-strings__group">{{ row.group }}</div>
          </template>
        </el-table-column>
        <el-table-column v-for="l in enabledLocales" :key="l.code" :label="l.name" min-width="200">
          <template #default="{ row }">
            <span v-if="!isMissing(row, l.code)" class="ui-strings__value">{{ row.values[l.code] }}</span>
            <el-tag v-else size="small" type="warning">尚未翻譯</el-tag>
          </template>
        </el-table-column>
        <el-table-column v-if="canEditTranslations" label="操作" width="130" fixed="right">
          <template #default="{ row }">
            <el-button size="small" text type="primary" @click="openEdit(row)">編輯</el-button>
            <el-button v-if="canUpdate" size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
          </template>
        </el-table-column>
      </el-table>
      <el-pagination
        v-if="total > PAGE_SIZE"
        v-model:current-page="page"
        class="ui-strings__pager"
        background
        layout="prev, pager, next, total"
        :page-size="PAGE_SIZE"
        :total="total"
      />
    </div>

    <el-dialog v-model="dialogOpen" :title="dialogMode === 'create' ? '新增介面字串' : '編輯介面字串'" width="min(600px, 94vw)" :close-on-click-modal="false">
      <el-alert v-if="dialogError" :title="dialogError" type="warning" show-icon class="ui-strings__block" @close="dialogError = null" />
      <el-form label-position="top">
        <el-form-item label="字串代號">
          <el-input v-model="form.key" :disabled="dialogMode === 'edit'" maxlength="128" placeholder="例如 button.submit" />
          <p v-if="dialogMode === 'create'" class="ui-strings__hint">建立後不能更改。只能用小寫英文、數字、底線、連字號，並以句點分段。</p>
        </el-form-item>
        <el-form-item label="分組">
          <el-input v-model="form.group" :disabled="sourceLocked" maxlength="64" placeholder="例如 按鈕、表單（可留空）" list="ui-string-groups" />
          <datalist id="ui-string-groups"><option v-for="g in groups" :key="g" :value="g" /></datalist>
        </el-form-item>
        <el-form-item v-for="l in enabledLocales" :key="l.code" :label="`${l.name}${l.code === defaultLocale ? '（原文，必填）' : ''}`">
          <el-input
            v-model="form.values[l.code]"
            type="textarea"
            :rows="2"
            :disabled="l.code === defaultLocale && sourceLocked"
            maxlength="2000"
            :placeholder="l.code === defaultLocale ? '' : '留空代表沒有翻譯'"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogOpen = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.ui-strings { min-width: 0; }
.ui-strings__block { margin-bottom: 12px; }
.ui-strings__hint { margin: 0 0 12px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.ui-strings__filters { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 12px; }
.ui-strings__filters > .el-select, .ui-strings__filters > .el-input { min-width: 160px; flex: 1 1 160px; max-width: 280px; }
.ui-strings__key { font-size: 12px; overflow-wrap: anywhere; }
.ui-strings__group { font-size: 12px; color: var(--admin-text-tertiary); }
.ui-strings__value { white-space: pre-wrap; overflow-wrap: anywhere; }
.ui-strings__pager { margin-top: 12px; justify-content: flex-end; flex-wrap: wrap; }
</style>

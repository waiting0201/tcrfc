<script setup lang="ts">
/**
 * 多語系 → 語系設定：啟用語系與備援規則、未翻譯時的處理方式、日期／數字格式。
 * - 不提供新增語系（apps/api/README.md「H 批」待決 2）；預設語系不能停用、不能設備援；備援不能是自己、
 *   必須是啟用中的語系、不能成環；被別的語系當備援時不能停用（後端把關，訊息直接顯示）。
 * - 日期／數字格式：留空＝清除（使用系統預設）。「字型設定」規劃書沒有可選項目，本期不做（待決）。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { usePermission } from '@/composables/useCrudPermissions'
import { AdminApiError } from '@/api/http'
import {
  getAdminI18nSettings,
  updateAdminI18nSettings,
  updateAdminLocale,
  type AdminI18nSettings,
  type AdminLocale,
} from '@/api/adminSiteSettings'
import FormField from '@/components/FormField.vue'
import { provideFormErrors } from '@/composables/useFormErrors'

const formErrors = provideFormErrors()

const props = defineProps<{ club: string; locales: AdminLocale[] }>()
const emit = defineEmits<{ (e: 'locales-changed'): void }>()

const canUpdate = usePermission('site.locale.update')

interface LocaleRow {
  code: string
  name: string
  isDefault: boolean
  isEnabled: boolean
  fallbackCode: string | null
  sortOrder: number
  baseline: string
  saving: boolean
}

const rows = ref<LocaleRow[]>([])
const rowError = ref<string | null>(null)

function rowSnap(r: Pick<LocaleRow, 'name' | 'isEnabled' | 'fallbackCode' | 'sortOrder'>): string {
  return JSON.stringify([r.name, r.isEnabled, r.fallbackCode, r.sortOrder])
}

function syncRows() {
  rows.value = props.locales.map((l) => {
    const r = { code: l.code, name: l.name, isDefault: l.isDefault, isEnabled: l.isEnabled, fallbackCode: l.fallbackCode ?? null, sortOrder: l.sortOrder }
    return { ...r, baseline: rowSnap(r), saving: false }
  })
}
onMounted(syncRows)
// 外層重新載入語系後同步
watch(() => props.locales, syncRows)

function fallbackOptions(row: LocaleRow) {
  return rows.value.filter((r) => r.code !== row.code && r.isEnabled)
}

function isRowDirty(row: LocaleRow): boolean {
  return rowSnap(row) !== row.baseline
}

async function saveRow(row: LocaleRow) {
  if (!canUpdate.value) return
  if (!row.name.trim()) {
    rowError.value = '語系名稱不能空白。'
    return
  }
  row.saving = true
  rowError.value = null
  try {
    await updateAdminLocale(props.club, row.code, {
      name: row.name.trim(),
      isEnabled: row.isEnabled,
      fallbackCode: row.isDefault ? null : row.fallbackCode,
      sortOrder: row.sortOrder,
    })
    ElMessage.success('已儲存')
    emit('locales-changed')
  } catch (error) {
    rowError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    row.saving = false
  }
}

// ───────────── 未翻譯處理與格式 ─────────────

const settings = reactive<AdminI18nSettings>({ fallbackMode: 'show_default', dateFormatZh: '', dateFormatEn: '', numberFormatZh: '', numberFormatEn: '' })
const settingsBaseline = ref('')
const settingsState = ref<'loading' | 'ready' | 'error'>('loading')
const settingsError = ref('')
const savingSettings = ref(false)
const settingsFormError = ref<string | null>(null)

const DATE_PRESETS_ZH = ['YYYY/MM/DD', 'YYYY-MM-DD', 'YYYY 年 M 月 D 日']
const DATE_PRESETS_EN = ['MMM D, YYYY', 'MMMM D, YYYY', 'D MMM YYYY', 'YYYY-MM-DD']
const NUMBER_PRESETS = ['1,234.56', '1.234,56', '1 234,56']

function applySettings(d: AdminI18nSettings) {
  settings.fallbackMode = d.fallbackMode
  settings.dateFormatZh = d.dateFormatZh ?? ''
  settings.dateFormatEn = d.dateFormatEn ?? ''
  settings.numberFormatZh = d.numberFormatZh ?? ''
  settings.numberFormatEn = d.numberFormatEn ?? ''
  settingsBaseline.value = JSON.stringify(settings)
}

async function loadSettings() {
  settingsState.value = 'loading'
  try {
    applySettings(await getAdminI18nSettings(props.club))
    settingsState.value = 'ready'
  } catch (error) {
    settingsError.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
    settingsState.value = 'error'
  }
}
onMounted(loadSettings)
watch(() => props.club, loadSettings)

const settingsDirty = computed(() => settingsState.value === 'ready' && JSON.stringify(settings) !== settingsBaseline.value)

async function saveSettings() {
  if (!canUpdate.value) return
  savingSettings.value = true
  settingsFormError.value = null
  formErrors.clearAll()
  try {
    applySettings(
      await updateAdminI18nSettings(props.club, {
        fallbackMode: settings.fallbackMode,
        dateFormatZh: settings.dateFormatZh || null,
        dateFormatEn: settings.dateFormatEn || null,
        numberFormatZh: settings.numberFormatZh || null,
        numberFormatEn: settings.numberFormatEn || null,
      }),
    )
    ElMessage.success('已儲存')
  } catch (error) {
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    settingsFormError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    savingSettings.value = false
  }
}
</script>

<template>
  <div class="locale-config">
    <el-card shadow="never" header="啟用的語言" class="locale-config__block">
      <p class="locale-config__hint">
        繁體中文是預設語言，不能停用。若某個語言的內容還沒翻譯，會依「備援語言」顯示該語言的內容。目前不提供新增語言。
      </p>
      <el-alert v-if="rowError" :title="rowError" type="warning" show-icon class="locale-config__block" @close="rowError = null" />
      <el-table :data="rows" row-key="code">
        <el-table-column label="語言名稱" min-width="160">
          <template #default="{ row }">
            <el-input v-model="row.name" :disabled="!canUpdate" maxlength="64" aria-label="語言名稱" />
            <el-tag v-if="row.isDefault" size="small" type="success" class="locale-config__tag">預設語言</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="啟用" width="90">
          <template #default="{ row }"><el-switch v-model="row.isEnabled" :disabled="!canUpdate || row.isDefault" /></template>
        </el-table-column>
        <el-table-column label="備援語言" min-width="170">
          <template #default="{ row }">
            <span v-if="row.isDefault" class="locale-config__hint">（預設語言不需要）</span>
            <el-select v-else v-model="row.fallbackCode" clearable :disabled="!canUpdate" placeholder="不設定" aria-label="備援語言">
              <el-option v-for="o in fallbackOptions(row)" :key="o.code" :label="o.name" :value="o.code" />
            </el-select>
          </template>
        </el-table-column>
        <el-table-column label="排序" width="130">
          <template #default="{ row }"><el-input-number v-model="row.sortOrder" :min="0" :disabled="!canUpdate" controls-position="right" size="small" /></template>
        </el-table-column>
        <el-table-column v-if="canUpdate" label="操作" width="100" fixed="right">
          <template #default="{ row }">
            <el-button size="small" type="primary" :disabled="!isRowDirty(row)" :loading="row.saving" @click="saveRow(row)">儲存</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-card shadow="never" header="未翻譯時的處理與顯示格式" class="locale-config__block">
      <el-skeleton v-if="settingsState === 'loading'" :rows="4" animated />
      <el-empty v-else-if="settingsState === 'error'" :image-size="64" :description="settingsError">
        <el-button type="primary" @click="loadSettings">重新載入</el-button>
      </el-empty>
      <template v-else>
        <el-alert v-if="settingsFormError" :title="settingsFormError" type="warning" show-icon class="locale-config__block" @close="settingsFormError = null" />
        <el-form label-position="top" :disabled="!canUpdate">
          <FormField field="fallbackMode" label="內容尚未翻譯時">
            <el-radio-group v-model="settings.fallbackMode">
              <el-radio value="show_default">顯示繁體中文內容</el-radio>
              <el-radio value="hide">隱藏該頁面</el-radio>
            </el-radio-group>
            <p class="locale-config__hint">這裡只儲存您的選擇。目前前台一律先顯示繁體中文內容；「隱藏該頁面」尚未套用到各內容頁，詳見待決事項。</p>
          </FormField>
          <el-row :gutter="16">
            <el-col :xs="24" :sm="12">
              <FormField field="dateFormatZh" label="日期格式（中文版）">
                <el-select v-model="settings.dateFormatZh" clearable filterable allow-create default-first-option placeholder="使用預設格式">
                  <el-option v-for="f in DATE_PRESETS_ZH" :key="f" :label="f" :value="f" />
                </el-select>
              </FormField>
            </el-col>
            <el-col :xs="24" :sm="12">
              <FormField field="dateFormatEn" label="日期格式（英文版）">
                <el-select v-model="settings.dateFormatEn" clearable filterable allow-create default-first-option placeholder="使用預設格式">
                  <el-option v-for="f in DATE_PRESETS_EN" :key="f" :label="f" :value="f" />
                </el-select>
              </FormField>
            </el-col>
            <el-col :xs="24" :sm="12">
              <FormField field="numberFormatZh" label="數字格式（中文版）">
                <el-select v-model="settings.numberFormatZh" clearable placeholder="使用預設格式">
                  <el-option v-for="f in NUMBER_PRESETS" :key="f" :label="f" :value="f" />
                </el-select>
              </FormField>
            </el-col>
            <el-col :xs="24" :sm="12">
              <FormField field="numberFormatEn" label="數字格式（英文版）">
                <el-select v-model="settings.numberFormatEn" clearable placeholder="使用預設格式">
                  <el-option v-for="f in NUMBER_PRESETS" :key="f" :label="f" :value="f" />
                </el-select>
              </FormField>
            </el-col>
          </el-row>
          <p class="locale-config__hint">
            日期格式用 YYYY（年）、MM／M（月）、DD／D（日）、MMM／MMMM（英文月份）組合，必須同時包含年、月、日，分隔字元可用空白、/、-、.、,、年、月、日。留空代表清除設定、使用預設。
          </p>
        </el-form>
        <el-button v-if="canUpdate" type="primary" :loading="savingSettings" :disabled="!settingsDirty" @click="saveSettings">儲存這一區</el-button>
      </template>
    </el-card>
  </div>
</template>

<style scoped>
.locale-config__block { margin-bottom: 16px; }
.locale-config__hint { margin: 6px 0 12px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.locale-config__tag { margin-top: 4px; }
</style>

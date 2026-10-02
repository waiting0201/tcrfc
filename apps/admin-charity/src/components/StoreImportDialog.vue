<script setup lang="ts">
/**
 * 店家批次匯入（規劃書 §6.1「批次匯入店家資料（CSV）」）。流程：下載範本 → 填好 → 上傳。
 * 任一列有錯整批都不會寫入，所以畫面要一次列出「所有」錯誤列（列號與 Excel 一致，表頭是第 1 列），
 * 讓人改完整份檔案再上傳一次，不必一次修一個再重傳。
 * 匯入只做新增：繁中店名＋地址相同視為重複；預設重複算錯誤（所以同一份檔案重複匯入不會產生重複店家），
 * 勾選「略過重複」才會把重複的列跳過、其餘照常匯入。
 */
import { computed, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { downloadStoreImportTemplate, importStores, type StoreImportResult } from '@/api/stores'
import { AdminApiError } from '@/api/http'

const MAX_BYTES = 1024 * 1024

const props = defineProps<{ modelValue: boolean }>()
const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'imported'): void
}>()

const visible = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
})

const file = ref<File | null>(null)
const skipDuplicates = ref(false)
const busy = ref(false)
const templateBusy = ref(false)
const result = ref<StoreImportResult | null>(null)
const fatalError = ref('')
const fileInput = ref<HTMLInputElement | null>(null)

watch(
  () => props.modelValue,
  (open) => {
    if (open) reset()
  },
)

function reset() {
  file.value = null
  skipDuplicates.value = false
  result.value = null
  fatalError.value = ''
  if (fileInput.value) fileInput.value.value = ''
}

function onPick(event: Event) {
  const picked = (event.target as HTMLInputElement).files?.[0] ?? null
  result.value = null
  fatalError.value = ''
  if (picked && picked.size > MAX_BYTES) {
    fatalError.value = '檔案超過 1 MB，請拆成幾份再分次匯入。'
    file.value = null
    if (fileInput.value) fileInput.value.value = ''
    return
  }
  file.value = picked
}

async function downloadTemplate() {
  templateBusy.value = true
  try {
    await downloadStoreImportTemplate()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.detail : '範本下載失敗，請稍後再試')
  } finally {
    templateBusy.value = false
  }
}

async function submit() {
  if (!file.value || busy.value) return
  busy.value = true
  result.value = null
  fatalError.value = ''
  try {
    const r = await importStores(file.value, skipDuplicates.value)
    result.value = r
    if (r.errors.length === 0) {
      ElMessage.success(`已匯入 ${r.importedCount} 家店家`)
      emit('imported')
    }
  } catch (error) {
    fatalError.value = error instanceof AdminApiError ? error.detail : '匯入失敗，請稍後再試'
  } finally {
    busy.value = false
  }
}

const hasErrors = computed(() => (result.value?.errors.length ?? 0) > 0)
const succeeded = computed(() => result.value !== null && !hasErrors.value)
</script>

<template>
  <el-dialog v-model="visible" title="批次匯入店家" width="640px" class="store-import" destroy-on-close>
    <ol class="store-import__steps">
      <li>
        先下載範本，照格式填好店家資料（第 1 列是欄位名稱，請不要更動）。
        <el-button size="small" :loading="templateBusy" @click="downloadTemplate">下載範本</el-button>
      </li>
      <li>
        選擇填好的檔案（CSV 格式，UTF-8 編碼，最多 500 列、1 MB）。
        <div class="store-import__pick">
          <input ref="fileInput" type="file" accept=".csv,text/csv" aria-label="選擇店家資料檔案" @change="onPick">
        </div>
      </li>
      <li>
        <el-checkbox v-model="skipDuplicates">略過重複的店家（店名與地址相同者），其餘照常匯入</el-checkbox>
        <p class="store-import__hint">沒有勾選時，只要有重複的店家，整份檔案都不會匯入。已經匯入過的檔案再傳一次不會產生重複的店家。</p>
      </li>
    </ol>
    <p class="store-import__hint">店家的 QR Code 網址由系統自動產生，檔案裡不需要填。店家分潤填 0 或留白代表沒有分潤；填入分潤需要額外授權。</p>

    <el-alert v-if="fatalError" type="error" :closable="false" show-icon :title="fatalError" class="store-import__alert" />

    <el-alert
      v-if="succeeded && result"
      type="success"
      :closable="false"
      show-icon
      :title="`匯入完成：成功 ${result.importedCount} 家${result.skipped.length > 0 ? `，略過 ${result.skipped.length} 列重複` : ''}`"
      class="store-import__alert"
    />

    <template v-if="hasErrors && result">
      <el-alert
        type="error"
        :closable="false"
        show-icon
        :title="`整份檔案沒有匯入：共有 ${result.errors.length} 列需要修正。請全部改好後再上傳一次。`"
        class="store-import__alert"
      />
      <el-table :data="result.errors" size="small" max-height="260" class="store-import__table">
        <el-table-column label="第幾列" width="90" prop="rowNumber" />
        <el-table-column label="問題" prop="reason" min-width="240" />
      </el-table>
    </template>

    <template v-if="result && result.skipped.length > 0">
      <p class="store-import__subtitle">已略過的重複列（{{ result.skipped.length }} 列）</p>
      <el-table :data="result.skipped" size="small" max-height="200" class="store-import__table">
        <el-table-column label="第幾列" width="90" prop="rowNumber" />
        <el-table-column label="原因" prop="reason" min-width="240" />
      </el-table>
    </template>

    <template #footer>
      <el-button @click="visible = false">{{ succeeded ? '關閉' : '取消' }}</el-button>
      <el-button v-if="!succeeded" type="primary" :disabled="!file" :loading="busy" @click="submit">開始匯入</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.store-import__steps {
  margin: 0 0 var(--charity-admin-space-3);
  padding-left: 20px;
  display: flex;
  flex-direction: column;
  gap: var(--charity-admin-space-3);
  font-size: 14px;
  line-height: 1.7;
}

.store-import__pick {
  margin-top: var(--charity-admin-space-2);
}

.store-import__hint {
  margin: var(--charity-admin-space-1) 0 0;
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
  line-height: 1.7;
}

.store-import__alert {
  margin-top: var(--charity-admin-space-3);
}

.store-import__table {
  margin-top: var(--charity-admin-space-3);
}

.store-import__subtitle {
  margin: var(--charity-admin-space-4) 0 0;
  font-size: 13px;
  font-weight: 600;
}
</style>

<script setup lang="ts">
/**
 * 商品尺寸對照表編輯器：可增刪欄、增刪列的表格（欄名與儲存格都是輸入框），加「單位」「備註」。
 * 輸出形狀見 `@/utils/sizeChart`（`{ columns, rows, unit?, note? }`，前台會畫成尺碼表）。
 * 放在編輯頁主欄（寬元件，docs/21 §3.3a）。以 v-model 傳入編輯狀態，每次修改都 emit 新物件（不改 props）。
 * 認不得的舊資料由父頁傳 `unrecognized`：顯示白話提示與「清空重新填寫」，不顯示原始內容。
 */
import { cloneSizeChart, type SizeChartState } from '@/utils/sizeChart'

const props = defineProps<{ modelValue: SizeChartState; unrecognized?: boolean }>()
const emit = defineEmits<{ 'update:modelValue': [value: SizeChartState]; reset: [] }>()

function update(fn: (draft: SizeChartState) => void) {
  const draft = cloneSizeChart(props.modelValue)
  fn(draft)
  emit('update:modelValue', draft)
}

const addColumn = () => update((d) => { d.columns.push(''); d.rows.forEach((r) => r.push('')) })
const removeColumn = (i: number) => update((d) => { d.columns.splice(i, 1); d.rows.forEach((r) => r.splice(i, 1)) })
const addRow = () => update((d) => { d.rows.push(Array<string>(d.columns.length).fill('')) })
const removeRow = (i: number) => update((d) => { d.rows.splice(i, 1) })
const setColumn = (i: number, v: string) => update((d) => { d.columns[i] = v })
const setCell = (r: number, c: number, v: string) => update((d) => { d.rows[r]![c] = v })
const setUnit = (v: string) => update((d) => { d.unit = v })
const setNote = (v: string) => update((d) => { d.note = v })
</script>

<template>
  <div class="size-chart-editor">
    <div v-if="unrecognized" class="size-chart-editor__legacy">
      <el-alert type="warning" show-icon :closable="false" title="這件商品原本存的尺寸資料格式系統無法辨識，所以沒辦法在這裡顯示或修改。不處理的話，儲存時會維持原樣；要改用表格重新填寫，請按下方按鈕（原本的資料會被清除）。" />
      <el-button class="size-chart-editor__reset" @click="emit('reset')">清空重新填寫</el-button>
    </div>

    <template v-else>
      <p class="size-chart-editor__hint">前台會把這張表顯示成尺碼表。先新增欄位並填欄名，再新增資料列；整張表留空並儲存即不顯示尺碼表。</p>

      <div v-if="modelValue.columns.length > 0" class="size-chart-editor__scroll">
        <table class="size-chart-editor__table">
          <thead>
            <tr>
              <th v-for="(col, ci) in modelValue.columns" :key="ci" scope="col">
                <el-input :model-value="col" size="small" :aria-label="`第 ${ci + 1} 欄的欄名`" placeholder="欄名" maxlength="32" @update:model-value="(v: string) => setColumn(ci, v)" />
                <el-button size="small" text type="danger" @click="removeColumn(ci)">刪除此欄</el-button>
              </th>
              <th class="size-chart-editor__op" scope="col"><span class="size-chart-editor__sr">操作</span></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(row, ri) in modelValue.rows" :key="ri">
              <td v-for="(_, ci) in modelValue.columns" :key="ci">
                <el-input :model-value="row[ci] ?? ''" size="small" :aria-label="`第 ${ri + 1} 列，${modelValue.columns[ci] || `第 ${ci + 1} 欄`}`" maxlength="64" @update:model-value="(v: string) => setCell(ri, ci, v)" />
              </td>
              <td class="size-chart-editor__op"><el-button size="small" text type="danger" @click="removeRow(ri)">刪除此列</el-button></td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="size-chart-editor__actions">
        <el-button size="small" @click="addColumn">+ 新增欄位</el-button>
        <el-button size="small" :disabled="modelValue.columns.length === 0" @click="addRow">+ 新增一列</el-button>
      </div>

      <div class="size-chart-editor__meta">
        <el-form-item label="單位（選填）"><el-input :model-value="modelValue.unit" maxlength="16" placeholder="例如 cm" @update:model-value="setUnit" /></el-form-item>
        <el-form-item label="備註（選填）"><el-input :model-value="modelValue.note" maxlength="200" placeholder="例如 實際尺寸可能有 1–2 公分誤差" @update:model-value="setNote" /></el-form-item>
      </div>
    </template>
  </div>
</template>

<style scoped>
.size-chart-editor__hint { margin: 4px 0 10px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.size-chart-editor__scroll { overflow-x: auto; margin-bottom: 8px; }
.size-chart-editor__table { border-collapse: collapse; width: 100%; min-width: max-content; }
.size-chart-editor__table th,
.size-chart-editor__table td { padding: 4px 6px; border: 1px solid var(--admin-border); vertical-align: top; min-width: 120px; }
.size-chart-editor__table th { background: var(--admin-surface-muted, transparent); font-weight: 500; }
.size-chart-editor__table .size-chart-editor__op { min-width: 88px; width: 88px; text-align: center; }
.size-chart-editor__actions { display: flex; gap: 8px; margin-bottom: 12px; }
.size-chart-editor__meta { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 0 16px; }
.size-chart-editor__legacy { display: flex; flex-direction: column; gap: 8px; align-items: flex-start; }
.size-chart-editor__reset { margin-top: 2px; }
.size-chart-editor__sr { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); }
</style>

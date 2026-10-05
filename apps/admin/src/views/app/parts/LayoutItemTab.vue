<script setup lang="ts">
/** 首頁區塊、快捷入口、「更多」分頁項目共用的清單：開關、上下移動排序、編輯名稱與連結；後兩類可新增與刪除。 */
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import MobileCardList from '@/components/MobileCardList.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { usePermission } from '@/composables/useCrudPermissions'
import { AdminApiError } from '@/api/http'
import { nullIfBlank } from '@/api/adminCommon'
import {
  createLayoutItem, deleteLayoutItem, listDeepLinks, listLayoutItems, reorderLayoutItems, updateLayoutItem,
  type DeepLinkDto, type LayoutItemDto, type LayoutKind,
} from '@/api/adminApp'

const props = defineProps<{ kind: LayoutKind }>()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const canUpdate = usePermission('app.layout.update')
const errText = (e: unknown, f: string) => (e instanceof AdminApiError ? e.message : f)
const fixedKind = computed(() => props.kind === 'home_section')
const KIND_TEXT: Record<LayoutKind, string> = { home_section: '首頁區塊', quick_entry: '快捷入口', more_item: '「更多」分頁項目' }

const rows = ref<LayoutItemDto[]>([])
const deepLinks = ref<DeepLinkDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
async function load() {
  loading.value = true
  loadError.value = null
  try {
    rows.value = await listLayoutItems(props.kind)
  } catch (e) {
    rows.value = []
    loadError.value = errText(e, '清單載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
onMounted(async () => {
  load()
  deepLinks.value = await listDeepLinks().catch(() => [])
})

async function toggle(row: LayoutItemDto, value: boolean) {
  try {
    await updateLayoutItem(row.id, { isEnabled: value, deepLinkId: row.deepLinkId, iconKey: row.iconKey, label: { zh: row.labelZh ?? '', en: row.labelEn ?? undefined } })
    row.isEnabled = value
    ElMessage.success(value ? '已開啟' : '已關閉')
  } catch (e) {
    ElMessage.error(errText(e, '更新失敗，請稍後再試'))
    await load()
  }
}
async function move(index: number, delta: number) {
  const target = index + delta
  if (target < 0 || target >= rows.value.length) return
  const next = [...rows.value]
  ;[next[index], next[target]] = [next[target], next[index]]
  try {
    rows.value = await reorderLayoutItems(props.kind, next.map((r) => r.id))
    ElMessage.success('已更新順序')
  } catch (e) {
    ElMessage.error(errText(e, '排序失敗，請稍後再試'))
    await load()
  }
}

// ── 編輯 ──
const visible = ref(false)
const editing = ref<LayoutItemDto | null>(null)
const saving = ref(false)
const formError = ref<string | null>(null)
const form = reactive({ itemKey: '', labelZh: '', labelEn: '', deepLinkId: '' as string, iconKey: '', isEnabled: true })
function openDialog(row: LayoutItemDto | null) {
  editing.value = row
  formError.value = null
  Object.assign(form, { itemKey: row?.itemKey ?? '', labelZh: row?.labelZh ?? '', labelEn: row?.labelEn ?? '', deepLinkId: row?.deepLinkId ?? '', iconKey: row?.iconKey ?? '', isEnabled: row?.isEnabled ?? true })
  visible.value = true
}
/** 項目識別碼由系統產生，使用者不需要填：優先沿用所選連結的識別名稱，已被占用或沒選連結就用隨機碼。 */
function autoItemKey(): string {
  const used = new Set(rows.value.map((r) => r.itemKey))
  const fromLink = deepLinks.value.find((d) => d.id === form.deepLinkId)?.code
  if (fromLink && !used.has(fromLink)) return fromLink
  let key: string
  do {
    key = `item_${Math.random().toString(36).slice(2, 8)}`
  } while (used.has(key))
  return key
}
async function save() {
  if (!canUpdate.value) return
  if (!form.labelZh.trim()) return void (formError.value = '請輸入中文名稱')
  saving.value = true
  formError.value = null
  const body = {
    deepLinkId: form.deepLinkId || null,
    iconKey: nullIfBlank(form.iconKey),
    isEnabled: form.isEnabled,
    label: { zh: form.labelZh.trim(), en: form.labelEn.trim() || undefined },
  }
  try {
    if (editing.value) await updateLayoutItem(editing.value.id, body)
    else await createLayoutItem({ ...body, kind: props.kind, itemKey: autoItemKey() })
    ElMessage.success('已儲存')
    visible.value = false
    await load()
  } catch (e) {
    formError.value = errText(e, '儲存失敗，請稍後再試')
  } finally {
    saving.value = false
  }
}
async function remove(row: LayoutItemDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除「${row.labelZh}」嗎？`, `刪除${KIND_TEXT[props.kind]}`, { confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning' })
  } catch {
    return
  }
  try {
    await deleteLayoutItem(row.id)
    ElMessage.success('已刪除')
    await load()
  } catch (e) {
    ElMessage.error(errText(e, '刪除失敗，請稍後再試'))
  }
}
const linkText = (r: LayoutItemDto) => deepLinks.value.find((d) => d.id === r.deepLinkId)?.labelZh ?? (r.deepLinkId ? '（已設定）' : '未設定')
</script>

<template>
  <div>
    <el-card shadow="never" class="li__block">
      <div class="li__row">
        <span class="li__muted"><template v-if="fixedKind">首頁固定九個區塊，只能開關、調整順序、改名稱與連結。</template><template v-else>{{ KIND_TEXT[kind] }}可以新增、刪除、開關與調整順序。順序由上到下就是 App 上由前到後。</template></span>
        <span class="li__spacer" />
        <el-button v-if="canUpdate && !fixedKind" type="primary" @click="openDialog(null)">+ 新增{{ KIND_TEXT[kind] }}</el-button>
      </div>
    </el-card>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="5" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never"><el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty></el-card>
    <el-card v-else shadow="never">
      <el-empty v-if="rows.length === 0" :description="`目前沒有${KIND_TEXT[kind]}`" />
      <template v-else>
        <el-table v-if="!isMobile" :data="rows" row-key="id">
          <el-table-column label="順序" width="60" type="index" />
          <el-table-column label="名稱" min-width="200"><template #default="{ row }">{{ row.labelZh || '（未命名）' }}<div class="li__muted">{{ row.labelEn || '尚未翻譯' }}</div></template></el-table-column>
          <el-table-column label="點擊後前往" min-width="160"><template #default="{ row }">{{ linkText(row) }}</template></el-table-column>
          <el-table-column label="顯示在 App" width="110"><template #default="{ row }"><el-switch :model-value="row.isEnabled" :disabled="!canUpdate" @change="(v: boolean | string | number) => toggle(row, !!v)" /></template></el-table-column>
          <el-table-column v-if="canUpdate" label="操作" width="230" fixed="right">
            <template #default="{ row, $index }">
              <el-button size="small" text :disabled="$index === 0" @click="move($index, -1)">上移</el-button>
              <el-button size="small" text :disabled="$index === rows.length - 1" @click="move($index, 1)">下移</el-button>
              <el-button size="small" text type="primary" @click="openDialog(row)">編輯</el-button>
              <el-button v-if="!row.isFixed" size="small" text type="danger" @click="remove(row)">刪除</el-button>
            </template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">{{ row.labelZh || '（未命名）' }}</template>
          <template #meta="{ row }"><el-tag :type="row.isEnabled ? 'success' : 'info'" size="small">{{ row.isEnabled ? '顯示中' : '已關閉' }}</el-tag><span>前往：{{ linkText(row) }}</span></template>
          <template #actions="{ row }">
            <template v-if="canUpdate">
              <el-button size="small" text @click="toggle(row, !row.isEnabled)">{{ row.isEnabled ? '關閉' : '開啟' }}</el-button>
              <el-button size="small" text :disabled="rows.indexOf(row) === 0" @click="move(rows.indexOf(row), -1)">上移</el-button>
              <el-button size="small" text :disabled="rows.indexOf(row) === rows.length - 1" @click="move(rows.indexOf(row), 1)">下移</el-button>
              <el-button size="small" text type="primary" @click="openDialog(row)">編輯</el-button>
              <el-button v-if="!row.isFixed" size="small" text type="danger" @click="remove(row)">刪除</el-button>
            </template>
          </template>
        </MobileCardList>
      </template>
    </el-card>

    <el-dialog v-model="visible" :title="editing ? `編輯${KIND_TEXT[kind]}` : `新增${KIND_TEXT[kind]}`" width="560px" :fullscreen="isMobile" :close-on-click-modal="false">
      <el-alert v-if="formError" type="error" show-icon :closable="false" :title="formError" class="li__block" />
      <el-form label-position="top">
        <BilingualShortField v-model:zh="form.labelZh" v-model:en="form.labelEn" label="名稱" required />
        <el-form-item label="點擊後前往">
          <el-select v-model="form.deepLinkId" clearable placeholder="不設定" style="width: 100%"><el-option v-for="d in deepLinks.filter((x) => x.isActive)" :key="d.id" :label="d.labelZh || '（未命名連結）'" :value="d.id" /></el-select>
        </el-form-item>
        <el-form-item v-if="!fixedKind" label="圖示（選填）"><el-input v-model="form.iconKey" placeholder="選填，圖示名稱請向 App 工程團隊確認" /></el-form-item>
        <el-form-item label="顯示在 App"><el-switch v-model="form.isEnabled" /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="visible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="save">儲存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.li__block { margin-bottom: 12px; }
.li__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.li__spacer { flex: 1; }
.li__muted { font-size: 12px; line-height: 1.6; color: var(--admin-text-tertiary); }
</style>

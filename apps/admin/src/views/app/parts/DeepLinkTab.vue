<script setup lang="ts">
/** App 內連結對照表：App 內頁面的連結（tcrfc:// 開頭）與對應的網頁網址；有版面項目使用中的連結不能刪除。 */
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import MobileCardList from '@/components/MobileCardList.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { usePermission } from '@/composables/useCrudPermissions'
import { AdminApiError } from '@/api/http'
import { nullIfBlank } from '@/api/adminCommon'
import { createDeepLink, deleteDeepLink, listDeepLinks, updateDeepLink, type DeepLinkDto } from '@/api/adminApp'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const canUpdate = usePermission('app.layout.update')
const errText = (e: unknown, f: string) => (e instanceof AdminApiError ? e.message : f)

const rows = ref<DeepLinkDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
async function load() {
  loading.value = true
  loadError.value = null
  try {
    rows.value = await listDeepLinks()
  } catch (e) {
    rows.value = []
    loadError.value = errText(e, '連結清單載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
onMounted(load)

const visible = ref(false)
const editing = ref<DeepLinkDto | null>(null)
const saving = ref(false)
const formError = ref<string | null>(null)
const form = reactive({ code: '', appLink: 'tcrfc://', webUrl: '', requiresLogin: false, isActive: true, labelZh: '', labelEn: '' })
function openDialog(row: DeepLinkDto | null) {
  editing.value = row
  formError.value = null
  Object.assign(form, { code: row?.code ?? '', appLink: row?.appLink ?? 'tcrfc://', webUrl: row?.webUrl ?? '', requiresLogin: row?.requiresLogin ?? false, isActive: row?.isActive ?? true, labelZh: row?.labelZh ?? '', labelEn: row?.labelEn ?? '' })
  visible.value = true
}
async function save() {
  if (!canUpdate.value) return
  if (!/^[a-z0-9]+(_[a-z0-9]+)*$/.test(form.code.trim())) return void (formError.value = '連結代號請用小寫英數字與底線，例如 my_orders')
  if (!form.appLink.trim().startsWith('tcrfc://')) return void (formError.value = 'App 內連結必須以 tcrfc:// 開頭')
  if (!form.labelZh.trim()) return void (formError.value = '請輸入中文名稱')
  saving.value = true
  formError.value = null
  const body = {
    code: form.code.trim(), appLink: form.appLink.trim(), webUrl: nullIfBlank(form.webUrl), requiresLogin: form.requiresLogin, isActive: form.isActive,
    label: { zh: form.labelZh.trim(), en: form.labelEn.trim() || undefined },
  }
  try {
    if (editing.value) await updateDeepLink(editing.value.id, body)
    else await createDeepLink(body)
    ElMessage.success('已儲存')
    visible.value = false
    await load()
  } catch (e) {
    formError.value = errText(e, '儲存失敗，請稍後再試')
  } finally {
    saving.value = false
  }
}
async function remove(row: DeepLinkDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除連結「${row.labelZh || row.code}」嗎？`, '刪除連結', { confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning' })
  } catch {
    return
  }
  try {
    await deleteDeepLink(row.id)
    ElMessage.success('已刪除')
    await load()
  } catch (e) {
    ElMessage.error(errText(e, '刪除失敗，請稍後再試'))
  }
}
</script>

<template>
  <div>
    <el-card shadow="never" class="dl__block">
      <div class="dl__row">
        <span class="dl__muted">App 內每個頁面都有一條「tcrfc://」開頭的連結，推播、公告與快捷入口都用這張表指向頁面；「網頁網址」是沒有安裝 App 時改開的網頁。</span>
        <span class="dl__spacer" />
        <el-button v-if="canUpdate" type="primary" @click="openDialog(null)">+ 新增連結</el-button>
      </div>
    </el-card>
    <el-card v-if="loading" shadow="never"><el-skeleton :rows="5" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never"><el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty></el-card>
    <el-card v-else shadow="never">
      <el-empty v-if="rows.length === 0" description="還沒有連結" />
      <template v-else>
        <el-table v-if="!isMobile" :data="rows" row-key="id">
          <el-table-column label="名稱" min-width="150"><template #default="{ row }">{{ row.labelZh || row.code }}<div class="dl__muted">代號 {{ row.code }}</div></template></el-table-column>
          <el-table-column label="App 內連結" min-width="200" prop="appLink" />
          <el-table-column label="網頁網址" min-width="200"><template #default="{ row }">{{ row.webUrl || '—' }}</template></el-table-column>
          <el-table-column label="需要登入" width="90"><template #default="{ row }">{{ row.requiresLogin ? '是' : '否' }}</template></el-table-column>
          <el-table-column label="使用中" width="90"><template #default="{ row }"><el-tag :type="row.isActive ? 'success' : 'info'" size="small">{{ row.isActive ? '啟用' : '停用' }}</el-tag></template></el-table-column>
          <el-table-column label="被引用" width="80"><template #default="{ row }">{{ row.usedByCount }} 處</template></el-table-column>
          <el-table-column v-if="canUpdate" label="操作" width="120" fixed="right">
            <template #default="{ row }">
              <el-button size="small" text type="primary" @click="openDialog(row)">編輯</el-button>
              <el-button size="small" text type="danger" :disabled="row.usedByCount > 0" @click="remove(row)">刪除</el-button>
            </template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">{{ row.labelZh || row.code }}</template>
          <template #meta="{ row }"><el-tag :type="row.isActive ? 'success' : 'info'" size="small">{{ row.isActive ? '啟用' : '停用' }}</el-tag><span>{{ row.appLink }}</span><span>被引用 {{ row.usedByCount }} 處</span></template>
          <template #actions="{ row }">
            <template v-if="canUpdate">
              <el-button size="small" text type="primary" @click="openDialog(row)">編輯</el-button>
              <el-button size="small" text type="danger" :disabled="row.usedByCount > 0" @click="remove(row)">刪除</el-button>
            </template>
          </template>
        </MobileCardList>
      </template>
    </el-card>

    <el-dialog v-model="visible" :title="editing ? '編輯連結' : '新增連結'" width="560px" :fullscreen="isMobile" :close-on-click-modal="false">
      <el-alert v-if="formError" type="error" show-icon :closable="false" :title="formError" class="dl__block" />
      <el-form label-position="top">
        <BilingualShortField v-model:zh="form.labelZh" v-model:en="form.labelEn" label="名稱" required />
        <el-form-item label="連結代號（不能重複）" required><el-input v-model="form.code" placeholder="例如 my_orders" /></el-form-item>
        <el-form-item label="App 內連結（tcrfc:// 開頭）" required><el-input v-model="form.appLink" /></el-form-item>
        <el-form-item label="網頁網址（選填）"><el-input v-model="form.webUrl" placeholder="沒有安裝 App 時改開的網頁" /></el-form-item>
        <el-form-item label="進入前需要登入"><el-switch v-model="form.requiresLogin" /></el-form-item>
        <el-form-item label="啟用"><el-switch v-model="form.isActive" /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="visible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="save">儲存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.dl__block { margin-bottom: 12px; }
.dl__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.dl__spacer { flex: 1; }
.dl__muted { font-size: 12px; line-height: 1.6; color: var(--admin-text-tertiary); }
</style>

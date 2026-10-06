<script setup lang="ts">
/** 維護模式：全部平台、iPhone、Android 三個範圍各自開關。開啟時 App 啟動會顯示維護訊息、無法使用。 */
import { computed, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import EdgeNotice from './EdgeNotice.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import { usePermission } from '@/composables/useCrudPermissions'
import { AdminApiError } from '@/api/http'
import { listMaintenance, saveMaintenance, type EdgePublishDto, type MaintenanceDto } from '@/api/adminApp'

const canUpdate = usePermission('app.release.update')
const formErrors = provideFormErrors()
const errText = (e: unknown, f: string) => (e instanceof AdminApiError ? e.message : f)

interface Row extends MaintenanceDto { saving: boolean; error: string | null; dirty: boolean }
const rows = ref<Row[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
const edge = ref<EdgePublishDto | null>(null)
const toRow = (m: MaintenanceDto): Row => ({ ...m, messageZh: m.messageZh ?? '', messageEn: m.messageEn ?? '', saving: false, error: null, dirty: false })
async function load() {
  loading.value = true
  loadError.value = null
  try {
    rows.value = (await listMaintenance()).map(toRow)
  } catch (e) {
    rows.value = []
    loadError.value = errText(e, '維護模式載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
onMounted(load)
const anyEnabled = computed(() => rows.value.some((r) => r.enabled))

async function save(row: Row, enabled: boolean) {
  row.error = null
  const index = rows.value.indexOf(row)
  formErrors.clear(`rows[${index}].messageZh`)
  formErrors.clear(`rows[${index}].messageEn`)
  if (enabled && !(row.messageZh ?? '').trim()) {
    formErrors.set(`rows[${index}].messageZh`, '開啟維護模式必須填寫繁體中文的維護訊息')
    await formErrors.focusFirst()
    return
  }
  if (enabled && !row.enabled) {
    try {
      await ElMessageBox.confirm(`開啟後，「${row.scopeLabel}」的 App 使用者會立刻看到維護訊息、無法使用 App。確定要開啟嗎？`, '開啟維護模式', {
        confirmButtonText: '開啟維護模式', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning',
      })
    } catch {
      return
    }
  }
  row.saving = true
  try {
    const r = await saveMaintenance(row.scope, { enabled, messageZh: row.messageZh?.trim() || null, messageEn: row.messageEn?.trim() || null })
    Object.assign(row, toRow(r.value))
    edge.value = r.edgePublish
    ElMessage.success(enabled ? '已開啟維護模式' : '已儲存')
  } catch (e) {
    // 後端的維護端點只回 messageZh／messageEn（範圍在路由上、沒有列索引），這裡補上列索引再標到該卡的欄位
    if (e instanceof AdminApiError && e.fieldErrors) {
      const scoped = Object.fromEntries(Object.entries(e.fieldErrors).map(([k, v]) => [k.startsWith('rows[') ? k : `rows[${index}].${k}`, v]))
      if (formErrors.applyApiError({ fieldErrors: scoped })) return
    }
    row.error = errText(e, '儲存失敗，請稍後再試')
  } finally {
    row.saving = false
  }
}
</script>

<template>
  <div>
    <EdgeNotice :edge="edge" />
    <el-alert v-if="anyEnabled" class="mt__block" type="error" show-icon :closable="false" title="目前有維護模式開啟中，對應範圍的 App 使用者無法使用。" />
    <el-alert v-if="!canUpdate" class="mt__block" type="info" show-icon :closable="false" title="維護模式只有系統管理員能修改，你的帳號只能檢視。" />
    <el-card v-if="loading" shadow="never"><el-skeleton :rows="4" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never"><el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty></el-card>
    <LangTabsBar v-else>
      <el-card v-for="(r, i) in rows" :key="r.scope" shadow="never" class="mt__block">
        <div class="mt__head">
          <strong>{{ r.scopeLabel }}</strong>
          <el-tag :type="r.enabled ? 'danger' : 'success'" size="small">{{ r.enabled ? '維護中' : '正常運作' }}</el-tag>
        </div>
        <el-alert v-if="r.error" type="error" show-icon :closable="false" :title="r.error" class="mt__block" />
        <el-form label-position="top" :disabled="!canUpdate">
          <BilingualTextareaField :zh="r.messageZh ?? ''" :en="r.messageEn ?? ''" :field="`rows[${i}].message`" label="維護訊息" :rows="2" :maxlength="300" @update:zh="(v) => (r.messageZh = v)" @update:en="(v) => (r.messageEn = v)" />
          <p class="mt__hint">開啟維護模式時，必須填寫繁體中文的維護訊息。</p>
        </el-form>
        <div v-if="canUpdate" class="mt__actions">
          <el-button :loading="r.saving" @click="save(r, r.enabled)">只儲存訊息</el-button>
          <el-button v-if="!r.enabled" type="danger" :loading="r.saving" @click="save(r, true)">開啟維護模式</el-button>
          <el-button v-else type="success" :loading="r.saving" @click="save(r, false)">結束維護模式</el-button>
        </div>
      </el-card>
    </LangTabsBar>
  </div>
</template>

<style scoped>
.mt__block { margin-bottom: 12px; }
.mt__head { display: flex; align-items: center; gap: 10px; margin-bottom: 10px; }
.mt__hint { margin: 0 0 12px; font-size: 12px; color: var(--admin-text-tertiary); }
.mt__actions { display: flex; flex-wrap: wrap; gap: 8px; }
</style>

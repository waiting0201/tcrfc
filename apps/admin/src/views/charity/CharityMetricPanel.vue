<script setup lang="ts">
/** 影響力數據：可自訂統計項目。金額類項目預設不公開，要公開必須明確打開「公開顯示」。 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import BilingualShortField from '@/components/BilingualShortField.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { enOrUndefined, nullIfBlank } from '@/api/adminCommon'
import { createMetric, deleteMetric, listMetrics, listPrograms, updateMetric, type MetricDto, type ProgramListItemDto } from '@/api/adminCharity'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canCreate, canUpdate, canDelete } = useCrudPermissions('charity.content')
const club = computed(() => activeClubId.value)

const rows = ref<MetricDto[]>([])
const programs = ref<ProgramListItemDto[]>([])
const programFilter = ref('')
const loading = ref(true)
const loadError = ref<string | null>(null)

async function load() {
  loading.value = true
  loadError.value = null
  try {
    rows.value = await listMetrics(club.value, programFilter.value || undefined)
  } catch (error) {
    rows.value = []
    loadError.value = error instanceof AdminApiError ? error.message : '清單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}
async function init() {
  programs.value = (await listPrograms(club.value, { pageSize: 100 }).catch(() => null))?.items ?? []
  await load()
}
onMounted(init)
watch(club, () => {
  programFilter.value = ''
  init()
})

const dialog = ref(false)
const saving = ref(false)
const dialogError = ref<string | null>(null)
const form = reactive({ id: null as string | null, isShared: false, programId: '', value: null as number | null, isPublic: false, sortOrder: 0, nameZh: '', nameEn: '', unitZh: '', unitEn: '' })
const dialogReadOnly = computed(() => form.isShared || (form.id ? !canUpdate.value : !canCreate.value))

function open(m: MetricDto | null) {
  dialogError.value = null
  Object.assign(form, {
    id: m?.id ?? null,
    isShared: m?.isShared ?? false,
    programId: m?.charityProgramId ?? '',
    value: m?.value ?? null,
    isPublic: m?.isPublic ?? false, // 金額類預設不公開：新增一律先不公開
    sortOrder: m?.sortOrder ?? rows.value.length,
    nameZh: m?.zh.name ?? '', nameEn: m?.en?.name ?? '',
    unitZh: m?.zh.unit ?? '', unitEn: m?.en?.unit ?? '',
  })
  dialog.value = true
}

async function save() {
  if (!form.nameZh.trim()) return void (dialogError.value = '請輸入中文項目名稱')
  saving.value = true
  dialogError.value = null
  const payload = {
    charityProgramId: form.programId || null,
    value: form.value,
    isPublic: form.isPublic,
    sortOrder: form.sortOrder,
    content: {
      zh: { name: form.nameZh.trim(), unit: nullIfBlank(form.unitZh) },
      en: enOrUndefined({ name: form.nameEn.trim(), unit: nullIfBlank(form.unitEn) as string }, 'name', 'unit'),
    },
  }
  try {
    if (form.id) await updateMetric(club.value, form.id, payload)
    else await createMetric(club.value, payload)
    ElMessage.success('已儲存')
    dialog.value = false
    await load()
  } catch (error) {
    dialogError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

async function remove(m: MetricDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除數據項目「${m.zh.name}」嗎？`, '刪除數據項目', {
      confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteMetric(club.value, m.id)
    ElMessage.success('已刪除')
    await load()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
  }
}

const valueLabel = (m: MetricDto) => (m.value == null ? '—' : `${m.value.toLocaleString()}${m.zh.unit ?? ''}`)
</script>

<template>
  <div>
    <el-card shadow="never" class="panel__block">
      <div class="panel__row">
        <el-select v-model="programFilter" placeholder="所屬計畫" clearable filterable class="panel__select" @change="load">
          <el-option v-for="p in programs" :key="p.id" :label="p.nameZh || '（未命名）'" :value="p.id" />
        </el-select>
        <span class="panel__spacer" />
        <el-button v-if="canCreate" type="primary" @click="open(null)">+ 新增數據項目</el-button>
      </div>
      <p class="panel__hint">前台「影響力數據」只顯示打開「公開顯示」的項目。金額類的項目預設不公開，請確認後再打開。</p>
    </el-card>
    <el-card v-if="loading" shadow="never"><el-skeleton :rows="4" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never"><el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty></el-card>
    <el-card v-else shadow="never">
      <template v-if="rows.length > 0">
        <el-table v-if="!isMobile" :data="rows" row-key="id">
          <el-table-column label="項目" min-width="180">
            <template #default="{ row }">{{ row.zh.name }}<el-tag v-if="row.isShared" type="info" size="small" class="panel__tag">兩隊共用</el-tag></template>
          </el-table-column>
          <el-table-column label="數值" width="130"><template #default="{ row }">{{ valueLabel(row) }}</template></el-table-column>
          <el-table-column label="所屬計畫" min-width="160"><template #default="{ row }">{{ row.programNameZh || '整體統計' }}</template></el-table-column>
          <el-table-column label="前台" width="90"><template #default="{ row }"><el-tag :type="row.isPublic ? 'success' : 'info'" size="small">{{ row.isPublic ? '公開' : '不公開' }}</el-tag></template></el-table-column>
          <el-table-column label="操作" width="130" fixed="right">
            <template #default="{ row }">
              <el-button size="small" text type="primary" @click="open(row)">{{ canUpdate && !row.isShared ? '編輯' : '檢視' }}</el-button>
              <el-button v-if="canDelete && !row.isShared" size="small" text type="danger" @click="remove(row)">刪除</el-button>
            </template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">{{ row.zh.name }}：{{ valueLabel(row) }}</template>
          <template #meta="{ row }"><el-tag :type="row.isPublic ? 'success' : 'info'" size="small">{{ row.isPublic ? '公開' : '不公開' }}</el-tag><span>{{ row.programNameZh || '整體統計' }}</span></template>
          <template #actions="{ row }">
            <el-button size="small" text type="primary" @click="open(row)">{{ canUpdate && !row.isShared ? '編輯' : '檢視' }}</el-button>
            <el-button v-if="canDelete && !row.isShared" size="small" text type="danger" @click="remove(row)">刪除</el-button>
          </template>
        </MobileCardList>
      </template>
      <el-empty v-else description="還沒有數據項目" />
    </el-card>

    <el-dialog v-model="dialog" :title="form.id ? '數據項目' : '新增數據項目'" width="560px">
      <el-alert v-if="dialogError" :title="dialogError" type="warning" show-icon class="panel__block" @close="dialogError = null" />
      <el-alert v-if="form.isShared" title="這是台中磐石與台中藍鯨共用的項目，只能檢視。" type="info" show-icon :closable="false" class="panel__block" />
      <el-form label-position="top" :disabled="dialogReadOnly">
        <BilingualShortField label="項目名稱" :zh="form.nameZh" :en="form.nameEn" required placeholder="例如 受惠人數" @update:zh="(v) => (form.nameZh = v)" @update:en="(v) => (form.nameEn = v)" />
        <BilingualShortField label="單位" :zh="form.unitZh" :en="form.unitEn" placeholder="例如 人、場、元" @update:zh="(v) => (form.unitZh = v)" @update:en="(v) => (form.unitEn = v)" />
        <el-row :gutter="12">
          <el-col :xs="24" :sm="12"><el-form-item label="數值"><el-input-number v-model="form.value" :controls="false" style="width: 100%" /></el-form-item></el-col>
          <el-col :xs="24" :sm="12"><el-form-item label="排序值"><el-input-number v-model="form.sortOrder" :min="0" /></el-form-item></el-col>
        </el-row>
        <el-form-item label="所屬計畫">
          <el-select v-model="form.programId" clearable filterable placeholder="不選＝整體統計項目" style="width: 100%">
            <el-option v-for="p in programs" :key="p.id" :label="p.nameZh || '（未命名）'" :value="p.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="前台公開"><el-switch v-model="form.isPublic" active-text="公開顯示" inactive-text="不公開" /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialog = false">關閉</el-button>
        <el-button v-if="!dialogReadOnly" type="primary" :loading="saving" @click="save">儲存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.panel__block { margin-bottom: 12px; }
.panel__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.panel__select { width: 220px; max-width: 100%; }
.panel__spacer { flex: 1; }
.panel__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.panel__tag { margin-left: 6px; }
@media (max-width: 767px) { :deep(.el-dialog) { width: 94% !important; } }
</style>

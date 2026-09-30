<script setup lang="ts">
/**
 * 「會員權益」分頁：權益對照表（掛在方案底下），在對話框內新增／編輯，不另開頁。
 * 「一般會員」「付費球迷會員」兩欄是前台對照表的兩欄內容（例如 ✓、✗、9 折）。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import MobileCardList from '@/components/MobileCardList.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { enOrUndefined, nullIfBlank } from '@/api/adminCommon'
import { listMembershipPlans, type MembershipPlanListItemDto } from '@/api/adminMemberships'
import {
  createMembershipBenefit,
  deleteMembershipBenefit,
  getMembershipBenefit,
  listBenefitGroups,
  listMembershipBenefits,
  reorderMembershipBenefits,
  updateMembershipBenefit,
  type BenefitGroup,
  type BenefitGroupOptionDto,
  type MembershipBenefitListItemDto,
  type SaveBenefitPayload,
  type StoreStatus,
} from '@/api/adminPartnerStores'
import { errorMessage } from './membershipHelpers'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canCreate, canUpdate, canDelete } = useCrudPermissions('member.benefit')
const club = computed(() => activeClubId.value)

const plans = ref<MembershipPlanListItemDto[]>([])
const groups = ref<BenefitGroupOptionDto[]>([])
const rows = ref<MembershipBenefitListItemDto[]>([])
const filters = ref({ planId: '', group: '', status: '' })
const loading = ref(true)
const loadError = ref<string | null>(null)
const reordering = ref(false)

/** 重排只在「選定單一方案、沒有其他篩選」時開放（API 需要 planId，且要看得到該方案全部條目）。 */
const canReorder = computed(() => !!filters.value.planId && !filters.value.group && !filters.value.status)

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const f = filters.value
    rows.value = await listMembershipBenefits(club.value, {
      planId: f.planId || undefined,
      group: f.group || undefined,
      status: f.status || undefined,
    })
  } catch (error) {
    rows.value = []
    loadError.value = errorMessage(error, '權益清單載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
async function loadRefs() {
  const [p, g] = await Promise.allSettled([listMembershipPlans(club.value), listBenefitGroups(club.value)])
  plans.value = p.status === 'fulfilled' ? p.value : []
  groups.value = g.status === 'fulfilled' ? g.value : []
}
onMounted(() => {
  loadRefs()
  load()
})
watch(club, () => {
  filters.value = { planId: '', group: '', status: '' }
  loadRefs()
  load()
})
function clearFilters() {
  filters.value = { planId: '', group: '', status: '' }
  load()
}
const planLabel = (p: MembershipPlanListItemDto) => `${p.seasonCode}｜${p.nameZh || p.code}`

async function move(index: number, delta: -1 | 1) {
  const target = index + delta
  if (!canReorder.value || target < 0 || target >= rows.value.length) return
  const ids = rows.value.map((r) => r.id)
  ;[ids[index], ids[target]] = [ids[target], ids[index]]
  reordering.value = true
  try {
    await reorderMembershipBenefits(club.value, filters.value.planId, ids)
    await load()
  } catch (error) {
    ElMessage.error(errorMessage(error, '調整順序失敗，請稍後再試'))
  } finally {
    reordering.value = false
  }
}
const idx = (row: MembershipBenefitListItemDto) => rows.value.findIndex((r) => r.id === row.id)

async function handleDelete(row: MembershipBenefitListItemDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除權益「${row.nameZh || '（未命名）'}」嗎？無法復原。`, '刪除權益', {
      confirmButtonText: '刪除',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteMembershipBenefit(club.value, row.id)
    ElMessage.success('已刪除')
    await load()
  } catch (error) {
    ElMessage.error(errorMessage(error, '刪除失敗，請稍後再試'))
  }
}

// ── 編輯對話框 ──
const dialogOpen = ref(false)
const editingId = ref<string | null>(null)
const editingSort = ref<number | undefined>(undefined)
const dialogLoading = ref(false)
const saving = ref(false)
const dialogError = ref<string | null>(null)
const form = reactive({
  planId: '',
  group: 'member_card' as BenefitGroup,
  status: 'published' as StoreStatus,
  nameZh: '',
  nameEn: '',
  descZh: '',
  descEn: '',
  freeZh: '',
  freeEn: '',
  paidZh: '',
  paidEn: '',
})
const readOnly = computed(() => (editingId.value ? !canUpdate.value : !canCreate.value))

function resetForm() {
  Object.assign(form, {
    planId: filters.value.planId || '',
    group: groups.value[0]?.code ?? 'member_card',
    status: 'published',
    nameZh: '', nameEn: '', descZh: '', descEn: '', freeZh: '', freeEn: '', paidZh: '', paidEn: '',
  })
}
function openCreate() {
  editingId.value = null
  editingSort.value = undefined
  resetForm()
  dialogError.value = null
  dialogOpen.value = true
}
async function openEdit(row: MembershipBenefitListItemDto) {
  editingId.value = row.id
  editingSort.value = row.sortOrder
  resetForm()
  dialogError.value = null
  dialogOpen.value = true
  dialogLoading.value = true
  try {
    const d = await getMembershipBenefit(club.value, row.id)
    Object.assign(form, {
      planId: d.planId,
      group: d.group,
      status: d.status,
      nameZh: d.zh.name ?? '',
      nameEn: d.en?.name ?? '',
      descZh: d.zh.description ?? '',
      descEn: d.en?.description ?? '',
      freeZh: d.zh.freeValue ?? '',
      freeEn: d.en?.freeValue ?? '',
      paidZh: d.zh.paidValue ?? '',
      paidEn: d.en?.paidValue ?? '',
    })
  } catch (error) {
    dialogError.value = errorMessage(error, '資料載入失敗，請關閉後重試')
  } finally {
    dialogLoading.value = false
  }
}

async function save() {
  dialogError.value = null
  if (!form.planId) return void (dialogError.value = '請選擇這個權益屬於哪個方案')
  if (!form.nameZh.trim()) return void (dialogError.value = '請輸入中文名稱')
  const payload: SaveBenefitPayload = {
    planId: form.planId,
    group: form.group,
    status: form.status,
    content: {
      zh: {
        name: form.nameZh.trim(),
        description: nullIfBlank(form.descZh),
        freeValue: nullIfBlank(form.freeZh),
        paidValue: nullIfBlank(form.paidZh),
      },
      en: enOrUndefined(
        {
          name: form.nameEn.trim(),
          description: nullIfBlank(form.descEn) as string,
          freeValue: nullIfBlank(form.freeEn) as string,
          paidValue: nullIfBlank(form.paidEn) as string,
        },
        'name', 'description', 'freeValue', 'paidValue',
      ),
    },
  }
  if (editingId.value && editingSort.value !== undefined) payload.sortOrder = editingSort.value
  saving.value = true
  try {
    if (editingId.value) await updateMembershipBenefit(club.value, editingId.value, payload)
    else await createMembershipBenefit(club.value, payload)
    ElMessage.success('已儲存')
    dialogOpen.value = false
    await load()
  } catch (error) {
    dialogError.value = errorMessage(error, '儲存失敗，請稍後再試')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="ben-tab">
    <el-card shadow="never" class="ben-tab__bar">
      <div class="ben-tab__row">
        <el-select v-model="filters.planId" placeholder="全部方案" clearable class="ben-tab__plan" @change="load">
          <el-option v-for="p in plans" :key="p.id" :label="planLabel(p)" :value="p.id" />
        </el-select>
        <el-select v-model="filters.group" placeholder="分組" clearable class="ben-tab__select" @change="load">
          <el-option v-for="g in groups" :key="g.code" :label="g.label" :value="g.code" />
        </el-select>
        <el-select v-model="filters.status" placeholder="狀態" clearable class="ben-tab__select" @change="load">
          <el-option label="已發布" value="published" />
          <el-option label="草稿" value="draft" />
        </el-select>
        <el-button @click="clearFilters">清除</el-button>
        <span class="ben-tab__spacer" />
        <el-button v-if="canCreate" type="primary" @click="openCreate">+ 新增權益</el-button>
      </div>
      <p class="ben-tab__hint">
        權益對照表掛在各方案底下，前台會依方案顯示「一般會員」與「付費球迷會員」兩欄的內容。
        {{ canUpdate ? (canReorder ? '' : '要調整順序，請先只選一個方案（不要再加分組或狀態篩選）。') : '' }}
      </p>
    </el-card>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>
    <el-card v-else shadow="never">
      <template v-if="rows.length > 0">
        <el-table v-if="!isMobile" :data="rows" row-key="id">
          <el-table-column label="方案" min-width="150"><template #default="{ row }">{{ row.seasonCode }}｜{{ row.planName || row.planCode }}</template></el-table-column>
          <el-table-column label="分組" width="110" prop="groupLabel" />
          <el-table-column label="權益名稱" min-width="150"><template #default="{ row }">{{ row.nameZh || '（未命名）' }}</template></el-table-column>
          <el-table-column label="一般會員" width="110"><template #default="{ row }">{{ row.freeValueZh || '—' }}</template></el-table-column>
          <el-table-column label="付費球迷會員" width="120"><template #default="{ row }">{{ row.paidValueZh || '—' }}</template></el-table-column>
          <el-table-column label="狀態" width="90">
            <template #default="{ row }"><el-tag :type="row.status === 'published' ? 'success' : 'info'" size="small">{{ row.statusLabel || (row.status === 'published' ? '已發布' : '草稿') }}</el-tag></template>
          </el-table-column>
          <el-table-column v-if="canUpdate" label="順序" width="96">
            <template #default="{ $index }">
              <el-button size="small" text :disabled="!canReorder || reordering || $index === 0" aria-label="上移" @click="move($index, -1)"><el-icon><ArrowUp /></el-icon></el-button>
              <el-button size="small" text :disabled="!canReorder || reordering || $index === rows.length - 1" aria-label="下移" @click="move($index, 1)"><el-icon><ArrowDown /></el-icon></el-button>
            </template>
          </el-table-column>
          <el-table-column label="操作" width="130" fixed="right">
            <template #default="{ row }">
              <el-button size="small" text type="primary" @click="openEdit(row)">{{ canUpdate ? '編輯' : '檢視' }}</el-button>
              <el-button v-if="canDelete" size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
            </template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">{{ row.nameZh || '（未命名）' }}</template>
          <template #meta="{ row }">
            <span>{{ row.seasonCode }}｜{{ row.planName || row.planCode }}</span>
            <span>{{ row.groupLabel }}</span>
            <span>一般：{{ row.freeValueZh || '—' }}</span>
            <span>付費：{{ row.paidValueZh || '—' }}</span>
            <el-tag :type="row.status === 'published' ? 'success' : 'info'" size="small">{{ row.statusLabel || (row.status === 'published' ? '已發布' : '草稿') }}</el-tag>
          </template>
          <template #actions="{ row }">
            <el-button size="small" text type="primary" @click="openEdit(row)">{{ canUpdate ? '編輯' : '檢視' }}</el-button>
            <el-button v-if="canDelete" size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
            <template v-if="canUpdate && canReorder">
              <el-button size="small" text :disabled="reordering || idx(row) === 0" @click="move(idx(row), -1)">上移</el-button>
              <el-button size="small" text :disabled="reordering || idx(row) === rows.length - 1" @click="move(idx(row), 1)">下移</el-button>
            </template>
          </template>
        </MobileCardList>
      </template>
      <el-empty v-else description="目前沒有符合條件的權益">
        <el-button v-if="canCreate" type="primary" @click="openCreate">+ 新增權益</el-button>
      </el-empty>
    </el-card>

    <el-dialog v-model="dialogOpen" :title="editingId ? (readOnly ? '檢視權益' : '編輯權益') : '新增權益'" width="min(620px, 94vw)" :close-on-click-modal="false">
      <el-alert v-if="dialogError" :title="dialogError" type="warning" show-icon class="ben-tab__alert" @close="dialogError = null" />
      <el-skeleton v-if="dialogLoading" :rows="6" animated />
      <el-form v-else label-position="top" :disabled="readOnly">
        <el-row :gutter="12">
          <el-col :xs="24" :sm="12">
            <el-form-item label="所屬方案" required>
              <el-select v-model="form.planId" :disabled="!!editingId" placeholder="選擇方案" style="width: 100%">
                <el-option v-for="p in plans" :key="p.id" :label="planLabel(p)" :value="p.id" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :xs="24" :sm="12">
            <el-form-item label="分組" required>
              <el-select v-model="form.group" style="width: 100%"><el-option v-for="g in groups" :key="g.code" :label="g.label" :value="g.code" /></el-select>
            </el-form-item>
          </el-col>
        </el-row>
        <p v-if="editingId" class="ben-tab__hint">權益建立後，不能更換所屬方案。</p>
        <BilingualShortField label="權益名稱" :zh="form.nameZh" :en="form.nameEn" required @update:zh="(v) => (form.nameZh = v)" @update:en="(v) => (form.nameEn = v)" />
        <BilingualTextareaField label="說明" :zh="form.descZh" :en="form.descEn" :rows="2" @update:zh="(v) => (form.descZh = v)" @update:en="(v) => (form.descEn = v)" />
        <BilingualShortField label="一般會員的內容（例如 ✓、✗、9 折）" :zh="form.freeZh" :en="form.freeEn" @update:zh="(v) => (form.freeZh = v)" @update:en="(v) => (form.freeEn = v)" />
        <BilingualShortField label="付費球迷會員的內容" :zh="form.paidZh" :en="form.paidEn" @update:zh="(v) => (form.paidZh = v)" @update:en="(v) => (form.paidEn = v)" />
        <p class="ben-tab__hint">英文名稱有填，才會產生英文版；分組名稱的英文由系統自動帶入。</p>
        <el-form-item label="狀態">
          <el-radio-group v-model="form.status"><el-radio value="published">已發布</el-radio><el-radio value="draft">草稿</el-radio></el-radio-group>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button :disabled="saving" @click="dialogOpen = false">{{ readOnly ? '關閉' : '取消' }}</el-button>
        <el-button v-if="!readOnly" type="primary" :loading="saving" :disabled="dialogLoading" @click="save">儲存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.ben-tab { min-width: 0; }
.ben-tab__bar { margin-bottom: 12px; }
.ben-tab__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.ben-tab__plan { width: 220px; max-width: 100%; }
.ben-tab__select { width: 140px; max-width: 100%; }
.ben-tab__spacer { flex: 1; }
.ben-tab__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.ben-tab__alert { margin-bottom: 12px; }
</style>

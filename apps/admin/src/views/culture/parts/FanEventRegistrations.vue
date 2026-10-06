<script setup lang="ts">
/**
 * 球迷會活動的報名名單（含候補）。
 * 姓名、電話、Email 依「檢視完整個資」權限遮罩；沒有這個權限時，搜尋只比對會員編號。
 * 名額由系統計算：額滿時後台代填的新報名自動進候補；轉入「已報名／已到場」若名額已滿會被擋下。
 * 報名通知信本系統不寄，需要聯繫請依名單電話處理。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions, usePermission } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { nullIfBlank } from '@/api/adminCommon'
import { listMembers, type MemberListItemDto } from '@/api/adminMembers'
import {
  createFanEventRegistration,
  FAN_EVENT_REGISTRATION_STATUS_OPTIONS,
  listFanEventRegistrations,
  updateFanEventRegistration,
  type FanEventRegistrationDto,
  type FanEventRegistrationStatus,
} from '@/api/adminFanEvents'
import FormField from '@/components/FormField.vue'
import { provideFormErrors } from '@/composables/useFormErrors'

const formErrors = provideFormErrors()

const props = defineProps<{ eventId: string; isPaidMembersOnly: boolean }>()
const emit = defineEmits<{ (e: 'changed'): void }>()

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canUpdate } = useCrudPermissions('culture.fan_event')
const canReveal = usePermission('member.pii.reveal')
const canSearchMembers = usePermission('member.account.view')
const club = computed(() => activeClubId.value)

const filters = reactive({ status: '', keyword: '' })
const rows = ref<FanEventRegistrationDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)

async function load() {
  loading.value = true
  loadError.value = null
  try {
    rows.value = await listFanEventRegistrations(club.value, props.eventId, {
      status: filters.status || undefined,
      keyword: filters.keyword.trim() || undefined,
    })
  } catch (error) {
    rows.value = []
    loadError.value = error instanceof AdminApiError ? error.message : '報名名單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}
onMounted(load)
watch(() => props.eventId, load)

const counts = computed(() => ({
  registered: rows.value.filter((r) => r.status === 'registered').length,
  waitlist: rows.value.filter((r) => r.status === 'waitlist').length,
  attended: rows.value.filter((r) => r.status === 'attended').length,
}))
function statusTag(status: string): 'success' | 'warning' | 'info' | 'danger' | undefined {
  if (status === 'registered') return 'success'
  if (status === 'attended') return 'success'
  if (status === 'waitlist') return 'warning'
  return 'info'
}

async function setStatus(row: FanEventRegistrationDto, status: FanEventRegistrationStatus) {
  if (status === row.status) return
  const label = FAN_EVENT_REGISTRATION_STATUS_OPTIONS.find((o) => o.value === status)?.label ?? status
  try {
    await ElMessageBox.confirm(`確定要把這筆報名改為「${label}」嗎？`, '變更報名狀態', { confirmButtonText: '確定', cancelButtonText: '取消', type: 'warning' })
  } catch {
    return
  }
  try {
    await updateFanEventRegistration(club.value, props.eventId, row.id, { status })
    ElMessage.success('已更新')
    await load()
    emit('changed')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '更新失敗，請稍後再試')
  }
}

// ── 備註 ──
const noteOpen = ref(false)
const noteRow = ref<FanEventRegistrationDto | null>(null)
const noteText = ref('')
const noteSaving = ref(false)
function openNote(row: FanEventRegistrationDto) {
  noteRow.value = row
  noteText.value = row.note ?? ''
  noteOpen.value = true
}
async function saveNote() {
  const row = noteRow.value
  if (!row) return
  noteSaving.value = true
  try {
    // 備註空字串＝清除（契約：省略＝不變）
    await updateFanEventRegistration(club.value, props.eventId, row.id, { status: row.status, note: noteText.value.trim() })
    noteOpen.value = false
    ElMessage.success('已儲存')
    await load()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試')
  } finally {
    noteSaving.value = false
  }
}

// ── 後台代填 ──
const addOpen = ref(false)
const addSaving = ref(false)
const addError = ref<string | null>(null)
const addForm = reactive({ isMember: false, memberId: '', applicantName: '', phone: '', email: '', note: '' })
const memberOptions = ref<MemberListItemDto[]>([])
const searching = ref(false)

function openAdd() {
  Object.assign(addForm, { isMember: false, memberId: '', applicantName: '', phone: '', email: '', note: '' })
  addError.value = null
  formErrors.clearAll()
  memberOptions.value = []
  addOpen.value = true
}
async function searchMembers(keyword: string) {
  const k = keyword.trim()
  if (!k || !canSearchMembers.value) {
    memberOptions.value = []
    return
  }
  searching.value = true
  try {
    memberOptions.value = (await listMembers(club.value, { keyword: k }, 1, 10)).items
  } catch {
    memberOptions.value = []
  } finally {
    searching.value = false
  }
}
async function saveAdd() {
  addError.value = null
  formErrors.clearAll()
  if (addForm.isMember) {
    if (!addForm.memberId) return (addError.value = '請先搜尋並選擇會員')
  } else {
    if (props.isPaidMembersOnly) return (addError.value = '這場活動限付費會員，請改選「會員報名」')
    if (!addForm.applicantName.trim()) return (addError.value = '請輸入姓名')
    if (!addForm.phone.trim() && !addForm.email.trim()) return (addError.value = '電話與 Email 至少要填一項')
  }
  addSaving.value = true
  try {
    const body = addForm.isMember
      ? { memberId: addForm.memberId, note: nullIfBlank(addForm.note) ?? undefined }
      : {
          applicantName: addForm.applicantName.trim(),
          phone: nullIfBlank(addForm.phone) ?? undefined,
          email: nullIfBlank(addForm.email) ?? undefined,
          note: nullIfBlank(addForm.note) ?? undefined,
        }
    const created = await createFanEventRegistration(club.value, props.eventId, body)
    addOpen.value = false
    ElMessage.success(created?.status === 'waitlist' ? '名額已滿，已加入候補' : '已新增報名')
    await load()
    emit('changed')
  } catch (error) {
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    addError.value = error instanceof AdminApiError ? error.message : '新增失敗，請稍後再試'
  } finally {
    addSaving.value = false
  }
}
</script>

<template>
  <div class="regs">
    <div class="regs__row">
      <el-input v-model="filters.keyword" :placeholder="canReveal ? '搜尋姓名、Email、電話或會員編號' : '搜尋會員編號'" clearable class="regs__keyword" @keyup.enter="load" @clear="load">
        <template #prefix><el-icon><Search /></el-icon></template>
      </el-input>
      <el-select v-model="filters.status" placeholder="狀態" clearable class="regs__select" @change="load">
        <el-option v-for="o in FAN_EVENT_REGISTRATION_STATUS_OPTIONS" :key="o.value" :label="o.label" :value="o.value" />
      </el-select>
      <el-button type="primary" @click="load">篩選</el-button>
      <span class="regs__spacer" />
      <el-button v-if="canUpdate" type="primary" @click="openAdd">+ 代填報名</el-button>
    </div>
    <p class="regs__hint">
      已報名 {{ counts.registered }}・已到場 {{ counts.attended }}・候補 {{ counts.waitlist }}（依目前篩選結果）。
      名額額滿後新報名自動進候補；系統不會寄送報名或遞補通知，需要聯繫請依名單電話處理。
      <template v-if="!canReveal">你的帳號沒有檢視完整個資的權限，姓名、電話與 Email 顯示為遮罩，搜尋只比對會員編號。</template>
    </p>

    <el-skeleton v-if="loading" :rows="4" animated />
    <el-empty v-else-if="loadError" :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    <el-empty v-else-if="rows.length === 0" description="目前沒有報名紀錄" :image-size="64" />
    <template v-else>
      <el-table v-if="!isMobile" :data="rows" row-key="id">
        <el-table-column label="報名人" min-width="140">
          <template #default="{ row }">
            <div>{{ row.applicantName || '—' }}</div>
            <div class="regs__muted">{{ row.isMember ? `會員 ${row.memberNo || ''}` : '非會員' }}</div>
          </template>
        </el-table-column>
        <el-table-column label="聯絡方式" min-width="170">
          <template #default="{ row }">
            <div>{{ row.phone || '—' }}</div>
            <div class="regs__muted">{{ row.email || '—' }}</div>
          </template>
        </el-table-column>
        <el-table-column label="狀態" width="90"><template #default="{ row }"><el-tag :type="statusTag(row.status)" size="small">{{ row.statusLabel }}</el-tag></template></el-table-column>
        <el-table-column label="備註" min-width="120"><template #default="{ row }">{{ row.note || '—' }}</template></el-table-column>
        <el-table-column v-if="canUpdate" label="操作" width="260" fixed="right">
          <template #default="{ row }">
            <el-button v-if="row.status === 'waitlist'" size="small" text type="primary" @click="setStatus(row, 'registered')">遞補</el-button>
            <el-button v-if="row.status === 'registered'" size="small" text type="primary" @click="setStatus(row, 'attended')">標為到場</el-button>
            <el-button v-if="row.status === 'registered'" size="small" text @click="setStatus(row, 'waitlist')">改為候補</el-button>
            <el-button v-if="row.status !== 'cancelled'" size="small" text type="danger" @click="setStatus(row, 'cancelled')">取消報名</el-button>
            <el-button v-else size="small" text type="primary" @click="setStatus(row, 'registered')">恢復報名</el-button>
            <el-button size="small" text @click="openNote(row)">備註</el-button>
          </template>
        </el-table-column>
      </el-table>
      <MobileCardList v-else :rows="rows" row-key="id">
        <template #title="{ row }">{{ row.applicantName || '—' }}</template>
        <template #meta="{ row }">
          <el-tag :type="statusTag(row.status)" size="small">{{ row.statusLabel }}</el-tag>
          <span>{{ row.isMember ? `會員 ${row.memberNo || ''}` : '非會員' }}</span>
          <span>{{ row.phone || row.email || '無聯絡方式' }}</span>
        </template>
        <template #actions="{ row }">
          <template v-if="canUpdate">
            <el-button v-if="row.status === 'waitlist'" size="small" text type="primary" @click="setStatus(row, 'registered')">遞補</el-button>
            <el-button v-if="row.status === 'registered'" size="small" text type="primary" @click="setStatus(row, 'attended')">標為到場</el-button>
            <el-button v-if="row.status !== 'cancelled'" size="small" text type="danger" @click="setStatus(row, 'cancelled')">取消報名</el-button>
            <el-button v-else size="small" text type="primary" @click="setStatus(row, 'registered')">恢復報名</el-button>
            <el-button size="small" text @click="openNote(row)">備註</el-button>
          </template>
        </template>
      </MobileCardList>
    </template>

    <el-dialog v-model="noteOpen" title="報名備註" width="420px" :close-on-click-modal="false" class="regs__dialog">
      <el-input v-model="noteText" type="textarea" :rows="3" maxlength="500" show-word-limit placeholder="清空後儲存＝清除備註" />
      <template #footer>
        <el-button @click="noteOpen = false">取消</el-button>
        <el-button type="primary" :loading="noteSaving" @click="saveNote">儲存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="addOpen" title="代填報名" width="480px" :close-on-click-modal="false" class="regs__dialog">
      <el-alert v-if="addError" :title="addError" type="warning" show-icon class="regs__block" @close="addError = null" />
      <el-form label-position="top">
        <el-form-item label="報名身分">
          <el-radio-group v-model="addForm.isMember">
            <el-radio-button :value="false">非會員</el-radio-button>
            <el-radio-button :value="true">會員</el-radio-button>
          </el-radio-group>
        </el-form-item>
        <template v-if="addForm.isMember">
          <p v-if="!canSearchMembers" class="regs__hint">你的帳號沒有查詢會員名單的權限，無法代填會員報名。</p>
          <FormField v-else field="memberId" label="搜尋會員">
            <el-select v-model="addForm.memberId" filterable remote clearable :remote-method="searchMembers" :loading="searching" placeholder="輸入會員編號搜尋" style="width: 100%">
              <el-option v-for="m in memberOptions" :key="m.id" :label="`${m.memberNo} ${m.name || ''}`" :value="m.id" />
            </el-select>
          </FormField>
          <p v-if="isPaidMembersOnly" class="regs__hint">這場活動限付費會員，沒有有效球迷會員會籍的會員會被系統擋下。</p>
        </template>
        <template v-else>
          <p v-if="isPaidMembersOnly" class="regs__hint">這場活動限付費會員，不能代填非會員。</p>
          <FormField field="applicantName" label="姓名" required><el-input v-model="addForm.applicantName" maxlength="64" /></FormField>
          <el-row :gutter="12">
            <el-col :xs="24" :sm="12"><FormField field="phone" label="電話"><el-input v-model="addForm.phone" maxlength="32" /></FormField></el-col>
            <el-col :xs="24" :sm="12"><FormField field="email" label="Email"><el-input v-model="addForm.email" maxlength="128" /></FormField></el-col>
          </el-row>
          <p class="regs__hint">電話與 Email 至少要填一項。</p>
        </template>
        <FormField field="note" label="備註"><el-input v-model="addForm.note" type="textarea" :rows="2" maxlength="500" /></FormField>
      </el-form>
      <template #footer>
        <el-button @click="addOpen = false">取消</el-button>
        <el-button type="primary" :loading="addSaving" @click="saveAdd">新增報名</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.regs__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.regs__keyword { width: 240px; max-width: 100%; }
.regs__select { width: 130px; max-width: 100%; }
.regs__spacer { flex: 1; }
.regs__hint { margin: 8px 0 10px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.regs__muted { font-size: 12px; color: var(--admin-text-tertiary); }
.regs__block { margin-bottom: 12px; }
@media (max-width: 767px) { :deep(.el-dialog) { width: 94% !important; } }
</style>

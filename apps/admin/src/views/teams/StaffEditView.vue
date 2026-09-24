<script setup lang="ts">
/**
 * C3 教練與團隊成員——編輯頁。對照 apps/api/README.md「S1-7」「S1-7a」。
 *
 * 共同（兩隊共用）的教練資料顯示為唯讀並說明原因：`staff.club_id IS NULL` 的列透過俱樂部範圍
 * 端點寫入一律被後端擋下（`SharedStaffReadOnlyException`，403，見 apps/api/README.md「受限
 * 欄位處理」），這裡在編輯頁提前用 `isReadOnly` 鎖住整張表單並顯示原因，避免使用者填完整份
 * 表單才在按下儲存的瞬間被拒絕。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { activeClubId } from '@/auth/clubAccess'
import { listAdminClubTeams, type AdminTeamAdminListItemDto } from '@/api/adminTeams'
import { createAdminStaff, getAdminStaff, updateAdminStaff, type SaveStaffPayload } from '@/api/adminStaff'
import { AdminApiError } from '@/api/http'
import {
  PORTRAIT_CONSENT_STATUS_LABEL,
  PORTRAIT_CONSENT_STATUS_ORDER,
  STAFF_GROUP_OPTIONS,
  type PortraitConsentStatus,
  type StaffTeamAssignment,
} from '@/types/team'

const route = useRoute()
const router = useRouter()

// 🔴 必須是 computed 不能是一次性求值的 const：建立成功後 handleSave() 呼叫
// `router.replace('/teams/staff/:id/edit')`，Vue Router 對同一個元件實例的路由切換預設不會
// 重新掛載（component reuse），若 isCreate 只在 setup 當下算一次，之後緊接著再按一次「儲存」
// 會誤判成仍在建立模式，重複呼叫 createAdminStaff 產生第二筆重複資料。比照
// `CompetitionEditView.vue`／`MatchEditView.vue` 既有寫法（`docs/18` 回報項）。
const isCreate = computed(() => route.name === 'staff-new')
const staffId = ref<string | undefined>(route.params.id as string | undefined)

const form = reactive({
  staffGroup: '' as string,
  licence: '',
  portraitConsentStatus: 'not_consented' as PortraitConsentStatus,
  nameZh: '',
  nameEn: '',
  titleZh: '',
  titleEn: '',
  bioZh: '',
  bioEn: '',
  teams: [] as StaffTeamAssignment[],
})
const baselineJson = ref('')
const photoKey = ref<string | null>(null)
const photoFile = ref<File | null>(null)
const removePhoto = ref(false)
const isShared = ref(false)

const teams = ref<AdminTeamAdminListItemDto[]>([])
const pendingTeamId = ref<string | null>(null)
const pendingRoleCode = ref('')

const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
const formError = ref<string | null>(null)

async function loadStaff() {
  loadState.value = 'loading'
  try {
    teams.value = await listAdminClubTeams(activeClubId.value)
    if (!isCreate.value && staffId.value) {
      const detail = await getAdminStaff(activeClubId.value, staffId.value)
      form.staffGroup = detail.staffGroup ?? ''
      form.licence = detail.licence ?? ''
      form.portraitConsentStatus = detail.portraitConsentStatus as PortraitConsentStatus
      form.nameZh = detail.zh.name ?? ''
      form.nameEn = detail.en?.name ?? ''
      form.titleZh = detail.zh.title ?? ''
      form.titleEn = detail.en?.title ?? ''
      form.bioZh = detail.zh.bio ?? ''
      form.bioEn = detail.en?.bio ?? ''
      form.teams = detail.teams.map((t) => ({ teamId: t.teamId, teamCode: t.teamCode, roleCode: t.roleCode ?? '' }))
      photoKey.value = detail.photoKey ?? null
      isShared.value = detail.isShared
    }
    photoFile.value = null
    removePhoto.value = false
    baselineJson.value = JSON.stringify(form)
    loadState.value = 'ready'
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'not-found') {
      loadState.value = 'not-found'
    } else {
      loadErrorMessage.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
      loadState.value = 'error'
    }
  }
}

onMounted(loadStaff)

const isDirty = computed(
  () => loadState.value === 'ready' && (JSON.stringify(form) !== baselineJson.value || photoFile.value !== null || removePhoto.value),
)
useUnsavedChanges(isDirty)

const isReadOnly = computed(() => loadState.value === 'ready' && isShared.value)
const pageTitle = computed(() => (isCreate.value ? '新增教練與團隊成員' : `編輯：${form.nameZh || '（未命名）'}`))

function teamLabel(id: string): string {
  const found = teams.value.find((t) => t.id === id)
  return found ? found.nameZh || found.code : id
}

function addTeamAssignment() {
  if (!pendingTeamId.value) return
  if (form.teams.some((t) => t.teamId === pendingTeamId.value)) {
    ElMessage.warning('這支球隊已經加過了')
    return
  }
  const found = teams.value.find((t) => t.id === pendingTeamId.value)
  form.teams.push({ teamId: pendingTeamId.value, teamCode: found?.code ?? '', roleCode: pendingRoleCode.value })
  pendingTeamId.value = null
  pendingRoleCode.value = ''
}

function removeTeamAssignment(index: number) {
  form.teams.splice(index, 1)
}

function isEnEmpty(): boolean {
  return !form.nameEn.trim() && !form.titleEn.trim() && !form.bioEn.trim()
}

function validate(): boolean {
  formError.value = null
  if (!form.nameZh.trim()) {
    formError.value = '請輸入中文姓名'
    return false
  }
  return true
}

function buildPayload(): SaveStaffPayload {
  return {
    staffGroup: form.staffGroup || null,
    licence: form.licence || null,
    // 🔴 一律明確帶出，理由同 PlayerEditView：省略會被後端回退成 not_consented。
    portraitConsentStatus: form.portraitConsentStatus,
    content: {
      zh: { name: form.nameZh.trim(), title: form.titleZh || null, bio: form.bioZh || null },
      en: isEnEmpty() ? undefined : { name: form.nameEn || null, title: form.titleEn || null, bio: form.bioEn || null },
    },
    // 一律明確帶出目前畫面上的完整陣列（跟新聞標籤／關聯同一種既有語意）。
    teams: form.teams.map((t) => ({ teamId: t.teamId, roleCode: t.roleCode || null })),
  }
}

async function handleSave() {
  if (isReadOnly.value) return
  if (!validate()) return
  saving.value = true
  formError.value = null
  try {
    if (isCreate.value) {
      const created = await createAdminStaff(activeClubId.value, buildPayload(), photoFile.value)
      ElMessage.success('已建立')
      router.replace(`/teams/staff/${created.id}/edit`)
      staffId.value = created.id
      photoKey.value = created.photoKey ?? null
    } else {
      const updated = await updateAdminStaff(
        activeClubId.value,
        staffId.value!,
        { ...buildPayload(), removePhoto: removePhoto.value },
        photoFile.value,
      )
      photoKey.value = updated.photoKey ?? null
      ElMessage.success('已儲存')
    }
    photoFile.value = null
    removePhoto.value = false
    baselineJson.value = JSON.stringify(form)
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'forbidden') {
      await ElMessageBox.alert(error.message, '沒有編輯權限', { confirmButtonText: '我知道了' })
    } else {
      formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
    }
  } finally {
    saving.value = false
  }
}

function handleBack() {
  router.push('/teams/staff')
}

function retryLoad() {
  loadStaff()
}
</script>

<template>
  <div class="staff-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="handleBack">
          <el-icon><ArrowLeft /></el-icon>
          返回列表
        </el-button>
      </template>
      <template #meta>
        <FrontendUnitBanner module-code="C3" />
        <span v-if="loadState === 'ready' && isShared" class="staff-edit__shared-note">
          <el-tag type="info" size="small">共用內容（唯讀）</el-tag>
          這是台中磐石與台中藍鯨共用的教練／團隊成員資料，你的帳號僅能檢視，如需修改請聯繫系統管理員
        </span>
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never">
      <el-skeleton :rows="8" animated />
    </el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這筆資料，可能已經被刪除，或不屬於目前選擇的俱樂部。</p>
          <p v-else>{{ loadErrorMessage }}</p>
        </template>
        <el-button v-if="loadState !== 'not-found'" type="primary" @click="retryLoad">重新載入</el-button>
        <el-button v-else type="primary" @click="handleBack">返回列表</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert
        v-if="formError"
        :title="formError"
        type="warning"
        show-icon
        class="staff-edit__form-error"
        @close="formError = null"
      />

      <el-form label-position="top" :disabled="isReadOnly">
        <el-card shadow="never" header="基本資料" class="staff-edit__section">
          <el-row :gutter="12">
            <el-col :span="12">
              <el-form-item label="分組">
                <el-select v-model="form.staffGroup" clearable placeholder="請選擇分組" style="width: 100%">
                  <el-option v-for="g in STAFF_GROUP_OPTIONS" :key="g" :label="g" :value="g" />
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="證照">
                <el-input v-model="form.licence" placeholder="例如 AFC A 級" />
              </el-form-item>
            </el-col>
          </el-row>

          <BilingualShortField
            label="姓名"
            :zh="form.nameZh"
            :en="form.nameEn"
            required
            @update:zh="(v) => (form.nameZh = v)"
            @update:en="(v) => (form.nameEn = v)"
          />
          <BilingualShortField
            label="職稱"
            :zh="form.titleZh"
            :en="form.titleEn"
            @update:zh="(v) => (form.titleZh = v)"
            @update:en="(v) => (form.titleEn = v)"
          />
          <BilingualTextareaField
            label="簡介"
            :zh="form.bioZh"
            :en="form.bioEn"
            @update:zh="(v) => (form.bioZh = v)"
            @update:en="(v) => (form.bioEn = v)"
          />
        </el-card>

        <el-card shadow="never" header="負責梯隊" class="staff-edit__section">
          <div class="staff-edit__team-add">
            <el-select v-model="pendingTeamId" filterable placeholder="選擇球隊" class="staff-edit__team-select">
              <el-option v-for="t in teams" :key="t.id" :label="t.nameZh || t.code" :value="t.id" />
            </el-select>
            <el-input v-model="pendingRoleCode" placeholder="角色說明（選填，例如：主教練）" class="staff-edit__role-input" />
            <el-button :disabled="!pendingTeamId" @click="addTeamAssignment">加入</el-button>
          </div>
          <div v-if="form.teams.length > 0" class="staff-edit__team-list">
            <el-tag
              v-for="(assignment, index) in form.teams"
              :key="assignment.teamId"
              closable
              class="staff-edit__team-tag"
              @close="removeTeamAssignment(index)"
            >
              {{ teamLabel(assignment.teamId) }}{{ assignment.roleCode ? `（${assignment.roleCode}）` : '' }}
            </el-tag>
          </div>
          <p v-else class="staff-edit__hint">目前沒有負責任何梯隊。</p>
        </el-card>

        <el-card shadow="never" header="照片與肖像同意" class="staff-edit__section">
          <el-form-item label="照片">
            <ImageUploader
              v-model:file="photoFile"
              v-model:remove-cover="removePhoto"
              :has-existing-image="!!photoKey"
              :disabled="saving || isReadOnly"
            />
          </el-form-item>
          <el-form-item label="肖像同意">
            <el-radio-group v-model="form.portraitConsentStatus">
              <el-radio v-for="s in PORTRAIT_CONSENT_STATUS_ORDER" :key="s" :value="s">
                {{ PORTRAIT_CONSENT_STATUS_LABEL[s] }}
              </el-radio>
            </el-radio-group>
            <p class="staff-edit__hint">未同意時前台不顯示照片（會改用預設圖或純文字卡呈現）。</p>
          </el-form-item>
        </el-card>
      </el-form>

      <div v-if="!isReadOnly" class="staff-edit__action-bar">
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.staff-edit {
  max-width: 780px;
  margin: 0 auto 88px;
}

.staff-edit__shared-note {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  color: var(--admin-text-secondary);
  flex-wrap: wrap;
}

.staff-edit__form-error {
  margin-bottom: 16px;
}

.staff-edit__section {
  margin-bottom: 16px;
}

.staff-edit__hint {
  margin: 6px 0 0;
  font-size: 12px;
  color: var(--admin-text-tertiary);
  line-height: 1.6;
}

.staff-edit__team-add {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.staff-edit__team-select {
  flex: 1;
  min-width: 180px;
}

.staff-edit__role-input {
  flex: 1;
  min-width: 180px;
}

.staff-edit__team-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 12px;
}

.staff-edit__action-bar {
  position: fixed;
  bottom: 0;
  left: var(--admin-sidebar-width-expanded);
  right: 0;
  background: var(--admin-bg-surface-2);
  border-top: 1px solid var(--admin-border);
  padding: 12px 24px;
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  z-index: 10;
}

@media (max-width: 1023px) {
  .staff-edit__action-bar {
    left: 0;
  }
}

@media (max-width: 767px) {
  .staff-edit__action-bar {
    justify-content: stretch;
  }

  .staff-edit__action-bar :deep(.el-button) {
    flex: 1;
  }
}
</style>

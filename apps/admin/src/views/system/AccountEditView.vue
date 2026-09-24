<script setup lang="ts">
/**
 * J1 帳號編輯頁 ＋ J4「掛在帳號底下」的俱樂部授權／球隊授權（分頁）。
 *
 * ✅ 球隊授權分頁已改接 `GET /admin/teams?clubCode=` 下拉選單（S1-4 續作補上，取代先前
 * 「沒有清單、只能自己貼識別碼」的暫時作法，見 apps/api/README.md「前端回報缺口②之二」）。
 * **只列出這個帳號目前已授權俱樂部底下的球隊**——後端也會擋（球隊授權必須落在帳號目前有效的
 * 俱樂部授權範圍內），這裡在畫面上先把選項收斂到範圍內，避免使用者選了一個必然會被拒絕的球隊
 * 才在送出後才看到錯誤訊息。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import {
  createAdminAccount,
  getAdminAccount,
  listAccountClubGrants,
  listAccountTeamGrants,
  resetAdminAccountPassword,
  resetAdminAccountTwoFactor,
  revokeAccountClubGrant,
  revokeAccountTeamGrant,
  setAdminAccountStatus,
  updateAdminAccount,
  upsertAccountClubGrant,
  upsertAccountTeamGrant,
  type AdminAccountClubGrantDto,
  type AdminAccountTeamGrantDto,
} from '@/api/adminAccounts'
import { listAdminClubs, type AdminClubListItemDto } from '@/api/adminClubs'
import { listAdminRoles, type AdminRoleListItemDto } from '@/api/adminRoles'
import { listAdminTeams, type AdminTeamListItemDto } from '@/api/adminTeams'
import { AdminApiError } from '@/api/http'

const route = useRoute()
const router = useRouter()

const isCreate = computed(() => route.name === 'system-account-new')
const accountId = ref<string | undefined>(route.params.id as string | undefined)

interface FormState {
  username: string
  displayName: string
  email: string
  primaryClubId: string
  isSuperAdmin: boolean
  roleCodes: string[]
  initialPassword: string
}

function emptyForm(): FormState {
  return { username: '', displayName: '', email: '', primaryClubId: '', isSuperAdmin: false, roleCodes: [], initialPassword: '' }
}

const form = reactive<FormState>(emptyForm())
const baselineJson = ref(JSON.stringify(form))
const accountStatus = ref<'active' | 'disabled'>('active')

const clubs = ref<AdminClubListItemDto[]>([])
const roles = ref<AdminRoleListItemDto[]>([])

const loadState = ref<'loading' | 'ready' | 'error'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
const formError = ref<string | null>(null)

async function loadReferenceData() {
  const [clubList, roleList] = await Promise.all([listAdminClubs(), listAdminRoles()])
  clubs.value = clubList
  roles.value = roleList
}

async function loadAccount() {
  loadState.value = 'loading'
  try {
    await loadReferenceData()
    if (!isCreate.value && accountId.value) {
      const detail = await getAdminAccount(accountId.value)
      form.username = detail.username
      form.displayName = detail.displayName
      form.email = detail.email ?? ''
      form.primaryClubId = detail.primaryClubId ?? ''
      form.isSuperAdmin = detail.isSuperAdmin
      form.roleCodes = [...detail.roleCodes]
      accountStatus.value = detail.status
      await Promise.all([loadClubGrants(), loadTeamGrants(), loadAvailableTeams()])
    }
    baselineJson.value = JSON.stringify(form)
    loadState.value = 'ready'
  } catch (error) {
    loadErrorMessage.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
    loadState.value = 'error'
  }
}

onMounted(loadAccount)

const isDirty = computed(() => loadState.value === 'ready' && JSON.stringify(form) !== baselineJson.value)
useUnsavedChanges(isDirty)

const pageTitle = computed(() => (isCreate.value ? '新增帳號' : `編輯帳號：${form.username}`))

function validate(): boolean {
  formError.value = null
  if (isCreate.value && !form.username.trim()) {
    formError.value = '請輸入帳號'
    return false
  }
  if (!form.displayName.trim()) {
    formError.value = '請輸入姓名'
    return false
  }
  if (isCreate.value && form.initialPassword.length < 10) {
    formError.value = '初始密碼長度至少需要 10 個字元'
    return false
  }
  return true
}

async function handleSave() {
  if (!validate()) return
  saving.value = true
  try {
    if (isCreate.value) {
      const created = await createAdminAccount({
        username: form.username.trim(),
        displayName: form.displayName.trim(),
        email: form.email || null,
        primaryClubId: form.primaryClubId || null,
        initialPassword: form.initialPassword,
        isSuperAdmin: form.isSuperAdmin,
        roleCodes: form.roleCodes,
      })
      ElMessage.success('已建立帳號，請透過站外管道把初始密碼轉交給使用者')
      router.replace(`/system/accounts/${created.id}/edit`)
      accountId.value = created.id
      form.initialPassword = ''
      baselineJson.value = JSON.stringify(form)
      await loadAccount()
    } else {
      await updateAdminAccount(accountId.value!, {
        displayName: form.displayName.trim(),
        email: form.email || null,
        primaryClubId: form.primaryClubId || null,
        isSuperAdmin: form.isSuperAdmin,
        roleCodes: form.roleCodes,
      })
      ElMessage.success('已儲存')
      baselineJson.value = JSON.stringify(form)
    }
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'validation') {
      formError.value = error.message
    } else if (error instanceof AdminApiError && error.kind === 'unknown') {
      formError.value = error.message
    } else {
      ElMessage.error(error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試')
    }
  } finally {
    saving.value = false
  }
}

async function toggleStatus() {
  const next = accountStatus.value === 'active' ? 'disabled' : 'active'
  const actionLabel = next === 'disabled' ? '停用' : '啟用'
  try {
    await ElMessageBox.confirm(`確定要${actionLabel}這個帳號嗎？`, `確認${actionLabel}`, {
      confirmButtonText: actionLabel,
      cancelButtonText: '取消',
      type: 'warning',
    })
  } catch {
    return
  }
  try {
    await setAdminAccountStatus(accountId.value!, next)
    accountStatus.value = next
    ElMessage.success(`已${actionLabel}`)
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : `${actionLabel}失敗，請稍後再試`)
  }
}

async function handleResetPassword() {
  let newPassword = ''
  try {
    const result = await ElMessageBox.prompt('請輸入新密碼（至少 10 個字元），設定後請透過站外管道轉交給使用者。', '重設密碼', {
      confirmButtonText: '重設',
      cancelButtonText: '取消',
      inputType: 'password',
      inputValidator: (value: string) => (value && value.length >= 10) || '密碼長度至少需要 10 個字元',
    })
    newPassword = result.value
  } catch {
    return
  }
  try {
    await resetAdminAccountPassword(accountId.value!, newPassword)
    ElMessage.success('密碼已重設，該帳號目前所有登入工作階段已被強制登出')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '重設失敗，請稍後再試')
  }
}

async function handleResetTotp() {
  try {
    await ElMessageBox.confirm('確定要重設這個帳號的兩階段驗證設定嗎？重設後下次登入需要重新設定。', '確認重設', {
      confirmButtonText: '重設',
      cancelButtonText: '取消',
      type: 'warning',
    })
  } catch {
    return
  }
  try {
    await resetAdminAccountTwoFactor(accountId.value!)
    ElMessage.success('已重設兩階段驗證設定')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '重設失敗，請稍後再試')
  }
}

// ── 俱樂部授權 ────────────────────────────────────────────────────────────
const clubGrants = ref<AdminAccountClubGrantDto[]>([])
const newClubGrant = reactive({ clubId: '', expiresOn: '' })
const clubGrantSubmitting = ref(false)

async function loadClubGrants() {
  clubGrants.value = await listAccountClubGrants(accountId.value!)
}

async function submitClubGrant() {
  if (!newClubGrant.clubId) {
    ElMessage.warning('請選擇要授權的俱樂部')
    return
  }
  clubGrantSubmitting.value = true
  try {
    await upsertAccountClubGrant(accountId.value!, {
      clubId: newClubGrant.clubId,
      expiresOn: newClubGrant.expiresOn || null,
    })
    ElMessage.success('已新增俱樂部授權')
    newClubGrant.clubId = ''
    newClubGrant.expiresOn = ''
    await loadClubGrants()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '新增失敗，請稍後再試')
  } finally {
    clubGrantSubmitting.value = false
  }
}

async function revokeClubGrant(grant: AdminAccountClubGrantDto) {
  try {
    await ElMessageBox.confirm(`確定要撤銷「${grant.clubCode}」的俱樂部授權嗎？`, '確認撤銷', {
      confirmButtonText: '撤銷',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    })
  } catch {
    return
  }
  try {
    await revokeAccountClubGrant(accountId.value!, grant.clubId)
    ElMessage.success('已撤銷')
    await loadClubGrants()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '撤銷失敗，請稍後再試')
  }
}

// ── 球隊授權 ──────────────────────────────────────────────────────────────
const teamGrants = ref<AdminAccountTeamGrantDto[]>([])
const newTeamGrant = reactive({ teamId: '', expiresOn: '' })
const teamGrantSubmitting = ref(false)

/** 跨俱樂部的球隊清單（`GET /admin/teams`，未帶 `clubCode` ＝ 全部俱樂部），畫面上再依這個帳號
 * 目前有效的俱樂部授權篩選成「可選」的子集合，見下方 `selectableTeams`。 */
const allTeams = ref<AdminTeamListItemDto[]>([])
const teamsLoadError = ref<string | null>(null)

async function loadAvailableTeams() {
  teamsLoadError.value = null
  try {
    allTeams.value = await listAdminTeams()
  } catch (error) {
    allTeams.value = []
    teamsLoadError.value = error instanceof AdminApiError ? error.message : '球隊清單載入失敗，請稍後再試'
  }
}

/** 這個帳號目前有效（未撤銷、未過期）的俱樂部授權——球隊授權只能落在這個範圍內（後端也會擋，
 * 見 apps/api/README.md「只能授權該帳號目前有效俱樂部授權範圍內的球隊」）。 */
const authorizedClubIds = computed(() => new Set(clubGrants.value.filter((g) => g.isCurrentlyEffective).map((g) => g.clubId)))

/** 依俱樂部分組，讓畫面在系統管理員授權跨俱樂部球隊時能一眼看出球隊屬於哪一隊。 */
const selectableTeamGroups = computed(() => {
  const groups = new Map<string, { clubCode: string; clubNameZh?: string | null; teams: AdminTeamListItemDto[] }>()
  for (const team of allTeams.value) {
    if (!authorizedClubIds.value.has(team.clubId)) continue
    const group = groups.get(team.clubId) ?? { clubCode: team.clubCode, clubNameZh: team.clubNameZh, teams: [] }
    group.teams.push(team)
    groups.set(team.clubId, group)
  }
  return [...groups.values()]
})

function teamOptionLabel(team: AdminTeamListItemDto): string {
  return team.nameZh ? `${team.nameZh}（${team.code}）` : team.code
}

async function loadTeamGrants() {
  teamGrants.value = await listAccountTeamGrants(accountId.value!)
}

async function submitTeamGrant() {
  if (!newTeamGrant.teamId) {
    ElMessage.warning('請選擇要授權的球隊')
    return
  }
  teamGrantSubmitting.value = true
  try {
    await upsertAccountTeamGrant(accountId.value!, {
      teamId: newTeamGrant.teamId,
      expiresOn: newTeamGrant.expiresOn || null,
    })
    ElMessage.success('已新增球隊授權')
    newTeamGrant.teamId = ''
    newTeamGrant.expiresOn = ''
    await loadTeamGrants()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '新增失敗，請稍後再試（請確認這個帳號已被授權該球隊所屬的俱樂部）')
  } finally {
    teamGrantSubmitting.value = false
  }
}

async function revokeTeamGrant(grant: AdminAccountTeamGrantDto) {
  try {
    await ElMessageBox.confirm(`確定要撤銷「${grant.teamCode}」的球隊授權嗎？`, '確認撤銷', {
      confirmButtonText: '撤銷',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    })
  } catch {
    return
  }
  try {
    await revokeAccountTeamGrant(accountId.value!, grant.teamId)
    ElMessage.success('已撤銷')
    await loadTeamGrants()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '撤銷失敗，請稍後再試')
  }
}

function handleBack() {
  router.push('/system/accounts')
}
</script>

<template>
  <div class="account-edit">
    <PageHeader :title="pageTitle">
      <template #back>
        <el-button text @click="handleBack">
          <el-icon><ArrowLeft /></el-icon>
          返回列表
        </el-button>
      </template>
      <template #meta>
        <FrontendUnitBanner module-code="J1" />
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never">
      <el-skeleton :rows="8" animated />
    </el-card>

    <el-card v-else-if="loadState === 'error'" shadow="never">
      <el-empty :image-size="96" :description="loadErrorMessage">
        <el-button type="primary" @click="loadAccount">重新載入</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert
        v-if="formError"
        :title="formError"
        type="warning"
        show-icon
        class="account-edit__form-error"
        @close="formError = null"
      />

      <el-card shadow="never" header="基本資料" class="account-edit__section">
        <el-form label-position="top">
          <el-form-item label="帳號" required>
            <el-input v-model="form.username" :disabled="!isCreate" placeholder="登入用帳號，建立後不可修改" />
          </el-form-item>
          <el-form-item label="姓名" required>
            <el-input v-model="form.displayName" />
          </el-form-item>
          <el-form-item label="Email">
            <el-input v-model="form.email" placeholder="選填" />
          </el-form-item>
          <el-form-item label="預設俱樂部">
            <el-select v-model="form.primaryClubId" placeholder="登入後站台切換器的初始站台（選填）" clearable style="width: 100%">
              <el-option v-for="club in clubs" :key="club.id" :label="club.nameZh ?? club.code" :value="club.id" />
            </el-select>
          </el-form-item>
          <el-form-item v-if="isCreate" label="初始密碼" required>
            <el-input v-model="form.initialPassword" type="password" show-password placeholder="至少 10 個字元，建立後請透過站外管道轉交" />
          </el-form-item>
          <el-form-item label="系統管理員">
            <el-switch v-model="form.isSuperAdmin" />
            <span class="account-edit__hint">系統管理員可存取全部俱樂部與全部模組，跳過角色權限檢查</span>
          </el-form-item>
          <el-form-item label="角色">
            <el-select v-model="form.roleCodes" multiple placeholder="請選擇角色" style="width: 100%">
              <el-option v-for="role in roles" :key="role.code" :label="role.nameZh" :value="role.code" />
            </el-select>
          </el-form-item>
        </el-form>
        <div class="account-edit__actions">
          <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
          <template v-if="!isCreate">
            <el-button @click="toggleStatus">{{ accountStatus === 'active' ? '停用帳號' : '啟用帳號' }}</el-button>
            <el-button @click="handleResetPassword">重設密碼</el-button>
            <el-button @click="handleResetTotp">重設兩階段驗證</el-button>
          </template>
        </div>
      </el-card>

      <el-card v-if="!isCreate" shadow="never" header="俱樂部授權" class="account-edit__section">
        <p class="account-edit__hint">指派這個帳號可以在站台切換器操作哪些俱樂部（J4）。</p>
        <el-table :data="clubGrants" size="small" class="account-edit__grant-table">
          <el-table-column label="俱樂部" prop="clubCode" width="120" />
          <el-table-column label="授權起日" prop="grantedOn" width="120" />
          <el-table-column label="到期日" width="120">
            <template #default="{ row }">{{ row.expiresOn ?? '無期限' }}</template>
          </el-table-column>
          <el-table-column label="狀態" width="100">
            <template #default="{ row }">
              <el-tag :type="row.isCurrentlyEffective ? 'success' : 'info'" size="small">
                {{ row.isCurrentlyEffective ? '生效中' : (row.isActive ? '已過期' : '已撤銷') }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="操作" width="100">
            <template #default="{ row }">
              <el-button v-if="row.isActive" size="small" text type="danger" @click="revokeClubGrant(row)">撤銷</el-button>
            </template>
          </el-table-column>
        </el-table>
        <div class="account-edit__grant-form">
          <el-select v-model="newClubGrant.clubId" placeholder="選擇俱樂部" style="width: 200px">
            <el-option v-for="club in clubs" :key="club.id" :label="club.nameZh ?? club.code" :value="club.id" />
          </el-select>
          <el-date-picker v-model="newClubGrant.expiresOn" type="date" value-format="YYYY-MM-DD" placeholder="到期日（選填，無期限請留空）" />
          <el-button type="primary" :loading="clubGrantSubmitting" @click="submitClubGrant">新增授權</el-button>
        </div>
      </el-card>

      <el-card v-if="!isCreate" shadow="never" header="球隊授權" class="account-edit__section">
        <p class="account-edit__hint">供「學院管理者不得改動一線隊賽程」這類列級限制使用（J4）。</p>
        <el-table :data="teamGrants" size="small" class="account-edit__grant-table">
          <el-table-column label="球隊" prop="teamCode" width="160" />
          <el-table-column label="所屬俱樂部" prop="clubCode" width="120" />
          <el-table-column label="到期日" width="120">
            <template #default="{ row }">{{ row.expiresOn ?? '無期限' }}</template>
          </el-table-column>
          <el-table-column label="狀態" width="100">
            <template #default="{ row }">
              <el-tag :type="row.isCurrentlyEffective ? 'success' : 'info'" size="small">
                {{ row.isCurrentlyEffective ? '生效中' : (row.isActive ? '已過期' : '已撤銷') }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="操作" width="100">
            <template #default="{ row }">
              <el-button v-if="row.isActive" size="small" text type="danger" @click="revokeTeamGrant(row)">撤銷</el-button>
            </template>
          </el-table-column>
        </el-table>
        <template v-if="authorizedClubIds.size === 0">
          <p class="account-edit__hint">請先在上方新增俱樂部授權，才能選擇該俱樂部底下的球隊。</p>
        </template>
        <template v-else>
          <div class="account-edit__grant-form">
            <el-select v-model="newTeamGrant.teamId" placeholder="選擇球隊" filterable style="width: 260px" :no-data-text="teamsLoadError ?? '目前已授權的俱樂部底下還沒有任何球隊'">
              <el-option-group v-for="group in selectableTeamGroups" :key="group.clubCode" :label="group.clubNameZh ?? group.clubCode">
                <el-option v-for="team in group.teams" :key="team.id" :label="teamOptionLabel(team)" :value="team.id" />
              </el-option-group>
            </el-select>
            <el-date-picker v-model="newTeamGrant.expiresOn" type="date" value-format="YYYY-MM-DD" placeholder="到期日（選填）" />
            <el-button type="primary" :loading="teamGrantSubmitting" @click="submitTeamGrant">新增授權</el-button>
          </div>
          <p v-if="teamsLoadError" class="account-edit__note">{{ teamsLoadError }}</p>
        </template>
      </el-card>
    </template>
  </div>
</template>

<style scoped>
.account-edit__section {
  margin-bottom: 12px;
}

.account-edit__form-error {
  margin-bottom: 12px;
}

.account-edit__hint {
  font-size: 12px;
  color: var(--admin-text-tertiary);
  margin-left: 8px;
}

.account-edit__actions {
  margin-top: 16px;
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.account-edit__grant-table {
  margin-bottom: 12px;
}

.account-edit__grant-form {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  align-items: center;
}

.account-edit__note {
  font-size: 12px;
  color: var(--admin-text-tertiary);
  margin-top: 8px;
}
</style>

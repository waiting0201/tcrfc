<script setup lang="ts">
/**
 * 榮譽與里程碑（對應前台「關於台中磐石」的榮譽紀錄與里程碑時間軸）。
 * 榮譽依球隊授權：只能新增、修改自己有權管理的球隊的榮譽，其他球隊的榮譽只能檢視。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { useWritableTeamScope } from '@/composables/useWritableTeamScope'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { listAdminSeasons, type AdminSeasonListItemDto } from '@/api/adminCompetitions'
import { listAdminClubTeams, type AdminTeamAdminListItemDto } from '@/api/adminTeams'
import {
  createAchievement,
  deleteAchievement,
  deleteMilestone,
  listAchievements,
  listMilestones,
  updateAchievement,
  type AchievementDto,
  type MilestoneDto,
} from '@/api/adminHonours'
import FormField from '@/components/FormField.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import { provideFormErrors } from '@/composables/useFormErrors'

const formErrors = provideFormErrors()

const route = useRoute()
const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const achievementPerm = useCrudPermissions('team.achievement')
const milestonePerm = useCrudPermissions('team.milestone')
const { writableTeams, loadWritableTeams, isWritable } = useWritableTeamScope('team')
const club = computed(() => activeClubId.value)

const tab = ref<'achievements' | 'milestones'>(route.query.tab === 'milestones' ? 'milestones' : 'achievements')
watch(tab, (t) => router.replace({ query: { tab: t } }))

const teams = ref<AdminTeamAdminListItemDto[]>([])
const seasons = ref<AdminSeasonListItemDto[]>([])

// ── 榮譽 ──
const achievements = ref<AchievementDto[]>([])
const achLoading = ref(true)
const achError = ref<string | null>(null)
const achFilter = reactive({ teamId: '', seasonId: '' })

async function loadAchievements() {
  achLoading.value = true
  achError.value = null
  try {
    achievements.value = await listAchievements(club.value, { teamId: achFilter.teamId || undefined, seasonId: achFilter.seasonId || undefined })
  } catch (error) {
    achievements.value = []
    achError.value = error instanceof AdminApiError ? error.message : '榮譽清單載入失敗，請稍後再試'
  } finally {
    achLoading.value = false
  }
}

const dialog = ref(false)
const saving = ref(false)
const dialogError = ref<string | null>(null)
const form = reactive({ id: null as string | null, seasonId: '', teamId: '', year: null as number | null, competitionName: '', competitionNameEn: '', placing: '', placingEn: '' })
// 既有榮譽的球隊不在可寫範圍內＝唯讀
const dialogReadOnly = computed(() => {
  if (form.id) return !achievementPerm.canUpdate.value || !isWritable(form.teamId)
  return !achievementPerm.canCreate.value
})

function openDialog(a: AchievementDto | null) {
  dialogError.value = null
  formErrors.clearAll()
  Object.assign(form, {
    id: a?.id ?? null,
    seasonId: a?.seasonId ?? '',
    teamId: a?.teamId ?? '',
    year: a?.year ?? null,
    competitionName: a?.competitionName ?? '',
    placing: a?.placing ?? '',
    competitionNameEn: a?.competitionNameEn ?? '',
    placingEn: a?.placingEn ?? '',
  })
  dialog.value = true
}

async function saveAchievement() {
  // 逐欄錯誤（S2-20 的 FormField／useFormErrors）：一次檢查全部、標到欄位、捲到第一處並自動切語言。
  const errors: Record<string, string> = {}
  if (!form.seasonId) errors.seasonId = '請選擇球季'
  if (!form.teamId) errors.teamId = '請選擇球隊'
  if (!form.competitionName.trim()) errors.competitionName = '請輸入賽事名稱（中文）'
  else if (form.competitionName.trim().length > 128) errors.competitionName = '賽事名稱（中文）最多 128 字'
  if (form.competitionNameEn.trim().length > 128) errors.competitionNameEn = '賽事名稱（英文）最多 128 字'
  if (!form.placing.trim()) errors.placing = '請輸入名次（中文），例如「冠軍」'
  else if (form.placing.trim().length > 64) errors.placing = '名次（中文）最多 64 字'
  if (form.placingEn.trim().length > 64) errors.placingEn = '名次（英文）最多 64 字'
  if (formErrors.replaceAll(errors)) {
    dialogError.value = null
    await formErrors.focusFirst()
    return
  }
  saving.value = true
  dialogError.value = null
  formErrors.clearAll()
  const payload = { seasonId: form.seasonId, teamId: form.teamId, year: form.year, competitionName: form.competitionName.trim(), competitionNameEn: form.competitionNameEn.trim() || null, placing: form.placing.trim(), placingEn: form.placingEn.trim() || null }
  try {
    if (form.id) await updateAchievement(club.value, form.id, payload)
    else await createAchievement(club.value, payload)
    ElMessage.success('已儲存')
    dialog.value = false
    await loadAchievements()
  } catch (error) {
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    dialogError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

async function removeAchievement(a: AchievementDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除「${a.competitionName}・${a.placing}」這筆榮譽嗎？`, '刪除榮譽', {
      confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteAchievement(club.value, a.id)
    ElMessage.success('已刪除')
    await loadAchievements()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
  }
}

const teamName = (a: AchievementDto) => a.teamNameZh || teams.value.find((t) => t.id === a.teamId)?.nameZh || '（未命名球隊）'

// ── 里程碑 ──
const milestones = ref<MilestoneDto[]>([])
const msLoading = ref(true)
const msError = ref<string | null>(null)

async function loadMilestones() {
  msLoading.value = true
  msError.value = null
  try {
    milestones.value = await listMilestones(club.value)
  } catch (error) {
    milestones.value = []
    msError.value = error instanceof AdminApiError ? error.message : '里程碑清單載入失敗，請稍後再試'
  } finally {
    msLoading.value = false
  }
}

async function removeMilestone(m: MilestoneDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除里程碑「${m.zh.title}」嗎？圖片會一併刪除，無法復原。`, '刪除里程碑', {
      confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteMilestone(club.value, m.id)
    ElMessage.success('已刪除')
    await loadMilestones()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
  }
}

async function loadLookups() {
  const [t, s] = await Promise.all([
    listAdminClubTeams(club.value).catch(() => [] as AdminTeamAdminListItemDto[]),
    listAdminSeasons(club.value).catch(() => [] as AdminSeasonListItemDto[]),
    loadWritableTeams(club.value).catch(() => undefined),
  ])
  teams.value = t
  seasons.value = s
}

function loadAll() {
  loadLookups()
  loadAchievements()
  loadMilestones()
}
onMounted(loadAll)
watch(club, () => {
  Object.assign(achFilter, { teamId: '', seasonId: '' })
  loadAll()
})
</script>

<template>
  <div class="honours">
    <PageHeader title="榮譽與里程碑">
      <template #meta><FrontendUnitBanner module-code="C5" /></template>
    </PageHeader>

    <el-tabs v-model="tab">
      <el-tab-pane label="榮譽" name="achievements">
        <el-card shadow="never" class="honours__block">
          <div class="honours__row">
            <el-select v-model="achFilter.teamId" placeholder="球隊" clearable class="honours__select" @change="loadAchievements">
              <el-option v-for="t in teams" :key="t.id" :label="t.nameZh || '（未命名球隊）'" :value="t.id" />
            </el-select>
            <el-select v-model="achFilter.seasonId" placeholder="球季" clearable class="honours__select" @change="loadAchievements">
              <el-option v-for="s in seasons" :key="s.id" :label="s.code" :value="s.id" />
            </el-select>
            <span class="honours__spacer" />
            <el-button v-if="achievementPerm.canCreate.value" type="primary" @click="openDialog(null)">+ 新增榮譽</el-button>
          </div>
          <p class="honours__hint">前台「關於」頁依年份由新到舊列出榮譽。你只能修改自己有權管理的球隊；其他球隊的榮譽只能檢視。</p>
        </el-card>
        <el-card v-if="achLoading" shadow="never"><el-skeleton :rows="5" animated /></el-card>
        <el-card v-else-if="achError" shadow="never"><el-empty :description="achError"><el-button type="primary" @click="loadAchievements">重新載入</el-button></el-empty></el-card>
        <el-card v-else shadow="never">
          <template v-if="achievements.length > 0">
            <el-table v-if="!isMobile" :data="achievements" row-key="id">
              <el-table-column label="年份" width="80"><template #default="{ row }">{{ row.year ?? '—' }}</template></el-table-column>
              <el-table-column label="賽事" min-width="200"><template #default="{ row }">{{ row.competitionName }}</template></el-table-column>
              <el-table-column label="名次" width="110"><template #default="{ row }">{{ row.placing }}</template></el-table-column>
              <el-table-column label="球隊" min-width="140"><template #default="{ row }">{{ teamName(row) }}</template></el-table-column>
              <el-table-column label="操作" width="130" fixed="right">
                <template #default="{ row }">
                  <el-button size="small" text type="primary" @click="openDialog(row)">{{ achievementPerm.canUpdate.value && isWritable(row.teamId) ? '編輯' : '檢視' }}</el-button>
                  <el-button v-if="achievementPerm.canDelete.value && isWritable(row.teamId)" size="small" text type="danger" @click="removeAchievement(row)">刪除</el-button>
                </template>
              </el-table-column>
            </el-table>
            <MobileCardList v-else :rows="achievements" row-key="id">
              <template #title="{ row }">{{ row.competitionName }}・{{ row.placing }}</template>
              <template #meta="{ row }"><span>{{ row.year ?? '未填年份' }}</span><span>{{ teamName(row) }}</span></template>
              <template #actions="{ row }">
                <el-button size="small" text type="primary" @click="openDialog(row)">{{ achievementPerm.canUpdate.value && isWritable(row.teamId) ? '編輯' : '檢視' }}</el-button>
                <el-button v-if="achievementPerm.canDelete.value && isWritable(row.teamId)" size="small" text type="danger" @click="removeAchievement(row)">刪除</el-button>
              </template>
            </MobileCardList>
          </template>
          <el-empty v-else description="還沒有榮譽紀錄" />
        </el-card>
      </el-tab-pane>

      <el-tab-pane label="里程碑" name="milestones">
        <el-card shadow="never" class="honours__block">
          <div class="honours__row">
            <p class="honours__hint honours__hint--flex">前台「關於」頁的時間軸依日期由舊到新排列，只顯示勾選「顯示在前台時間軸」的里程碑。</p>
            <el-button v-if="milestonePerm.canCreate.value" type="primary" @click="router.push('/teams/honours/milestones/new')">+ 新增里程碑</el-button>
          </div>
        </el-card>
        <el-card v-if="msLoading" shadow="never"><el-skeleton :rows="5" animated /></el-card>
        <el-card v-else-if="msError" shadow="never"><el-empty :description="msError"><el-button type="primary" @click="loadMilestones">重新載入</el-button></el-empty></el-card>
        <el-card v-else shadow="never">
          <template v-if="milestones.length > 0">
            <el-table v-if="!isMobile" :data="milestones" row-key="id">
              <el-table-column label="圖片" width="84">
                <template #default="{ row }"><img v-if="row.imageThumbUrl" :src="row.imageThumbUrl" alt="" class="honours__thumb"><span v-else class="honours__muted">—</span></template>
              </el-table-column>
              <el-table-column label="日期" width="120"><template #default="{ row }">{{ row.happenedOn }}</template></el-table-column>
              <el-table-column label="標題" min-width="220"><template #default="{ row }">{{ row.zh.title }}</template></el-table-column>
              <el-table-column label="前台時間軸" width="110"><template #default="{ row }"><el-tag :type="row.isVisible ? 'success' : 'info'" size="small">{{ row.isVisible ? '顯示' : '隱藏' }}</el-tag></template></el-table-column>
              <el-table-column label="操作" width="130" fixed="right">
                <template #default="{ row }">
                  <el-button size="small" text type="primary" @click="router.push(`/teams/honours/milestones/${row.id}/edit`)">{{ milestonePerm.canUpdate.value ? '編輯' : '檢視' }}</el-button>
                  <el-button v-if="milestonePerm.canDelete.value" size="small" text type="danger" @click="removeMilestone(row)">刪除</el-button>
                </template>
              </el-table-column>
            </el-table>
            <MobileCardList v-else :rows="milestones" row-key="id">
              <template #title="{ row }">{{ row.zh.title }}</template>
              <template #meta="{ row }"><span>{{ row.happenedOn }}</span><el-tag :type="row.isVisible ? 'success' : 'info'" size="small">{{ row.isVisible ? '顯示' : '隱藏' }}</el-tag></template>
              <template #actions="{ row }">
                <el-button size="small" text type="primary" @click="router.push(`/teams/honours/milestones/${row.id}/edit`)">{{ milestonePerm.canUpdate.value ? '編輯' : '檢視' }}</el-button>
                <el-button v-if="milestonePerm.canDelete.value" size="small" text type="danger" @click="removeMilestone(row)">刪除</el-button>
              </template>
            </MobileCardList>
          </template>
          <el-empty v-else description="還沒有里程碑" />
        </el-card>
      </el-tab-pane>
    </el-tabs>

    <el-dialog v-model="dialog" :title="form.id ? '榮譽' : '新增榮譽'" width="480px">
      <el-alert v-if="dialogError" :title="dialogError" type="warning" show-icon class="honours__block" @close="dialogError = null" />
      <el-alert v-if="form.id && dialogReadOnly" title="你沒有這支球隊的管理範圍，只能檢視。" type="info" show-icon :closable="false" class="honours__block" />
      <el-form label-position="top" :disabled="dialogReadOnly">
       <LangTabsBar variant="bare">
        <FormField field="teamId" label="球隊" required>
          <el-select v-model="form.teamId" filterable placeholder="請選擇球隊" style="width: 100%" @change="formErrors.clear('teamId')">
            <el-option v-for="t in (form.id ? teams : writableTeams)" :key="t.id" :label="t.nameZh || '（未命名球隊）'" :value="t.id" :disabled="!isWritable(t.id)" />
          </el-select>
        </FormField>
        <FormField field="seasonId" label="球季" required>
          <el-select v-model="form.seasonId" placeholder="請選擇球季" style="width: 100%" @change="formErrors.clear('seasonId')"><el-option v-for="s in seasons" :key="s.id" :label="s.code" :value="s.id" /></el-select>
        </FormField>
        <FormField field="year" label="年份"><el-input-number v-model="form.year" :min="1900" :max="2200" :controls="false" placeholder="不填＝球季開始年份" style="width: 100%" /></FormField>
        <BilingualShortField
            field-zh="competitionName"
            field-en="competitionNameEn"
            label="賽事名稱"
            :zh="form.competitionName"
            :en="form.competitionNameEn"
            required
            :maxlength="128"
            placeholder="例如 企業甲級聯賽"
            @update:zh="(v: string) => (form.competitionName = v)"
            @update:en="(v: string) => (form.competitionNameEn = v)"
          />
          <BilingualShortField
            field-zh="placing"
            field-en="placingEn"
            label="名次"
            :zh="form.placing"
            :en="form.placingEn"
            required
            :maxlength="64"
            placeholder="例如 冠軍"
            @update:zh="(v: string) => (form.placing = v)"
            @update:en="(v: string) => (form.placingEn = v)"
          />
       </LangTabsBar>
      </el-form>
      <template #footer>
        <el-button @click="dialog = false">關閉</el-button>
        <el-button v-if="!dialogReadOnly" type="primary" :loading="saving" @click="saveAchievement">儲存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.honours__block { margin-bottom: 12px; }
.honours__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.honours__select { width: 170px; max-width: 100%; }
.honours__spacer { flex: 1; }
.honours__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.honours__hint--flex { flex: 1; min-width: 200px; margin: 0; }
.honours__thumb { width: 56px; height: 40px; object-fit: cover; border-radius: 3px; background: var(--admin-lightbox-neutral); }
.honours__muted { color: var(--admin-text-tertiary); }
@media (max-width: 767px) { :deep(.el-dialog) { width: 94% !important; } }
</style>

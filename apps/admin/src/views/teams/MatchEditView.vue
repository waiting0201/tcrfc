<script setup lang="ts">
/**
 * C4「賽程與賽果」——編輯頁。對照 apps/api/README.md「S1-8」。
 *
 * 🔵 **場地：「選擇既有場地」與自由文字並存**（S1-12d 後續補完，2026-09-29，
 * `GET /api/v1/admin/{club}/venues` 上線後）：`matches.venue_id` 外鍵指向既有 `venues` 共用主檔，
 * `venue`／`venueEn` 則是獨立的顯示用文字欄位，兩者依既有 DDL 註解（`db/club-schema.sql`
 * `matches_i18n` 表頭「venue 為顯示用文字欄位，與 matches.venue_id（結構化主場地）並存」）本來
 * 就是刻意並存、不是互斥的兩套機制——場地清單挑一筆既有場地即可設定 `venueId`（主要用於主場，
 * 客場可能是清單裡沒有的場地，仍要保留文字欄位），文字欄位維持可自由填寫／覆寫顯示內容
 * （例如球隊代管的場地慣用簡稱、或客場賽事清單裡沒有的場地名稱）。選擇既有場地時會把中英文
 * 名稱帶入文字欄位方便一次填好，仍可手動再改。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import FormErrorStatus from '@/components/FormErrorStatus.vue'
import FormField from '@/components/FormField.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { activeClubId } from '@/auth/clubAccess'
import { listAdminClubTeams, type AdminTeamAdminListItemDto } from '@/api/adminTeams'
import { useWritableTeamScope } from '@/composables/useWritableTeamScope'
import { listAdminSeasons, listAdminCompetitions, type AdminSeasonListItemDto, type AdminCompetitionListItemDto } from '@/api/adminCompetitions'
import { listAdminPlayers, type AdminPlayerListItemDto } from '@/api/adminPlayers'
import { listAdminVenues, type AdminVenueListItemDto } from '@/api/adminVenues'
import {
  createAdminMatch,
  getAdminMatch,
  updateAdminMatch,
  type AdminMatchGoalInput,
  type AdminMatchCardInput,
  type AdminMatchLineupInput,
  type SaveMatchPayload,
} from '@/api/adminMatches'
import { AdminApiError } from '@/api/http'
import {
  MATCH_CARD_TYPE_ORDER,
  MATCH_COMPETITION_TAG_ORDER,
  MATCH_HOME_AWAY_ORDER,
  MATCH_STATUS_ORDER,
  matchCardTypeLabel,
  matchCompetitionTagLabel,
  matchHomeAwayLabel,
  matchStatusLabel,
} from '@/types/match'

const route = useRoute()
const router = useRouter()

// 🔴 必須是 computed 不能是一次性求值的 const：建立成功後 handleSave() 呼叫
// `router.replace('/teams/matches/:id/edit')`，Vue Router 對同一個元件實例的路由切換預設
// 不會重新掛載（component reuse），若 isCreate 只在 setup 當下算一次，之後緊接著再按一次
// 「儲存」會誤判成仍在建立模式，重複呼叫 createAdminMatch 產生第二筆重複資料，而不是更新
// 剛剛那筆——這個情境在無頭瀏覽器連續操作「建立→立刻改延賽→再存一次」時實測踩到。
// 比照 `CompetitionEditView.vue` 已經用 computed 的既有寫法；`FaqEditView.vue`／
// `NewsEditView.vue`／`PageEditView.vue`／`PlayerEditView.vue`／`StaffEditView.vue`／
// `TeamEditView.vue` 原本也是同一種一次性 const 寫法，已一併改正（見各檔案同一處註解）。
const isCreate = computed(() => route.name === 'match-new')
const matchId = ref<string | undefined>(route.params.id as string | undefined)

type GoalRow = AdminMatchGoalInput
type CardRow = AdminMatchCardInput
type LineupRow = AdminMatchLineupInput

const form = reactive({
  seasonId: '',
  competitionId: '' as string | '',
  teamIds: [] as string[],
  matchOn: '',
  kickoff: '',
  homeAway: '' as string | '',
  opponent: '',
  opponentEn: '',
  venueId: null as string | null,
  venue: '',
  venueEn: '',
  competitionTag: '' as string | '',
  status: 'scheduled',
  scoreHome: null as number | null,
  scoreAway: null as number | null,
  roundNo: null as number | null,
  matchNo: null as number | null,
  originalMatchOn: '',
  originalKickoff: '',
  goals: [] as GoalRow[],
  cards: [] as CardRow[],
  lineups: [] as LineupRow[],
})
const baselineJson = ref('')

const seasons = ref<AdminSeasonListItemDto[]>([])
const competitions = ref<AdminCompetitionListItemDto[]>([])
const teams = ref<AdminTeamAdminListItemDto[]>([])
// 「所屬球隊」只列出這個帳號能寫的球隊（S1-8 續作新增的端點），見 useWritableTeamScope 檔頭說明。
const { loadWritableTeams, outOfScopeIds, buildOptions } = useWritableTeamScope('match')
const matchPlayers = ref<AdminPlayerListItemDto[]>([])
// 全站場地清單（S1-12d 後續補完新增的唯讀端點），供「選擇既有場地」下拉選單使用；與俱樂部
// 無關，載入失敗不影響其餘欄位（比照既有 loadPlayersForTeams 的靜默降級寫法）。
const venues = ref<AdminVenueListItemDto[]>([])

const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
/** 頁首提示：只放沒有欄位歸屬的錯誤；欄位錯誤標在欄位上（formErrors）。 */
const formError = ref<string | null>(null)
const formErrors = provideFormErrors()

async function loadCompetitionsForSeason(seasonId: string) {
  if (!seasonId) {
    competitions.value = []
    return
  }
  try {
    competitions.value = await listAdminCompetitions(activeClubId.value, seasonId)
  } catch {
    competitions.value = []
  }
}

async function loadPlayersForTeams(teamIds: string[]) {
  if (teamIds.length === 0) {
    matchPlayers.value = []
    return
  }
  try {
    const lists = await Promise.all(teamIds.map((teamId) => listAdminPlayers(activeClubId.value, { teamId })))
    const merged = lists.flat()
    // 跨梯隊友誼賽可能兩支球隊都選了同一位球員（理論上不會，但保守去重一次，用 id 當 key）。
    const byId = new Map(merged.map((p) => [p.id, p]))
    matchPlayers.value = [...byId.values()]
  } catch {
    matchPlayers.value = []
  }
}

function playerLabel(id: string): string {
  const p = matchPlayers.value.find((x) => x.id === id)
  if (!p) return id
  const teamCode = teams.value.find((t) => t.id === p.teamId)?.code ?? p.teamCode
  const name = p.nameZh || '（未命名）'
  return p.shirtNo ? `${name}（${teamCode} #${p.shirtNo}）` : `${name}（${teamCode}）`
}

/** 選擇既有場地時，把中英文名稱帶入顯示用文字欄位方便一次填好（仍可手動再改，兩者本來就是
 * 並存而非互斥的欄位，見檔頭說明）。清空選擇（`clearable`）不會清空已填的文字欄位。 */
function handleVenuePicked(venueId: string | null) {
  if (!venueId) return
  const picked = venues.value.find((v) => v.id === venueId)
  if (!picked) return
  form.venue = picked.nameZh
  form.venueEn = picked.nameEn ?? ''
}

async function loadMatch() {
  loadState.value = 'loading'
  try {
    ;[teams.value] = await Promise.all([listAdminClubTeams(activeClubId.value), loadWritableTeams(activeClubId.value)])
    seasons.value = await listAdminSeasons(activeClubId.value)
    try {
      venues.value = await listAdminVenues(activeClubId.value)
    } catch {
      venues.value = []
    }

    if (!isCreate.value && matchId.value) {
      const detail = await getAdminMatch(activeClubId.value, matchId.value)
      form.seasonId = detail.seasonId
      form.competitionId = detail.competitionId ?? ''
      form.teamIds = [...detail.teamIds]
      form.matchOn = detail.matchOn
      form.kickoff = detail.kickoff ?? ''
      form.homeAway = detail.homeAway ?? ''
      form.opponent = detail.opponent ?? ''
      form.opponentEn = detail.opponentEn ?? ''
      form.venueId = detail.venueId ?? null
      form.venue = detail.venue ?? ''
      form.venueEn = detail.venueEn ?? ''
      form.competitionTag = detail.competitionTag ?? ''
      form.status = detail.status
      form.scoreHome = detail.scoreHome ?? null
      form.scoreAway = detail.scoreAway ?? null
      form.roundNo = detail.roundNo ?? null
      form.matchNo = detail.matchNo ?? null
      form.originalMatchOn = detail.originalMatchOn ?? ''
      form.originalKickoff = detail.originalKickoff ?? ''
      form.goals = detail.goals.map((g) => ({ playerId: g.playerId, minute: g.minute ?? null, goalType: g.goalType ?? '' }))
      form.cards = detail.cards.map((c) => ({ playerId: c.playerId, cardType: c.cardType, minute: c.minute ?? null }))
      form.lineups = detail.lineups.map((l) => ({ playerId: l.playerId, isStarter: l.isStarter }))
      await Promise.all([loadCompetitionsForSeason(form.seasonId), loadPlayersForTeams(form.teamIds)])
    }
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

onMounted(loadMatch)

watch(
  () => form.seasonId,
  (seasonId, previous) => {
    if (loadState.value !== 'ready') return
    if (seasonId !== previous) form.competitionId = ''
    loadCompetitionsForSeason(seasonId)
  },
)

watch(
  () => [...form.teamIds],
  () => {
    if (loadState.value !== 'ready') return
    loadPlayersForTeams(form.teamIds)
  },
)

watch(
  () => form.status,
  (status) => {
    if (status !== 'postponed') {
      form.originalMatchOn = ''
      form.originalKickoff = ''
    }
  },
)

const isDirty = computed(() => loadState.value === 'ready' && JSON.stringify(form) !== baselineJson.value)
useUnsavedChanges(isDirty)

const pageTitle = computed(() => (isCreate.value ? '新增賽事' : `編輯賽事：${form.matchOn}｜${form.opponent || '（未命名對手）'}`))
const isPostponed = computed(() => form.status === 'postponed')

// 🔴 既有的參賽球隊清單裡只要有一支不在這個帳號的可寫清單內（例如學院管理者打開一線隊也
// 參與的跨梯隊友誼賽），後端 `AdminMatchesRepository.UpdateAsync` 一律先檢查「既有」全部參賽
// 球隊是否都在授權範圍內，範圍外時整筆更新（不只換球隊，改比分等其他欄位也一樣）都會被 403
// 擋下——因此這裡整頁鎖成唯讀，不是只鎖「所屬球隊」這一個欄位。
const outOfScopeTeamIds = computed(() => (loadState.value === 'ready' && !isCreate.value ? outOfScopeIds(form.teamIds) : []))
const outOfScopeTeamNames = computed(() =>
  outOfScopeTeamIds.value.map((id) => teams.value.find((t) => t.id === id)?.nameZh || teams.value.find((t) => t.id === id)?.code || id),
)
const isReadOnly = computed(() => loadState.value === 'ready' && outOfScopeTeamIds.value.length > 0)
const teamSelectOptions = computed(() => buildOptions(teams.value, form.teamIds))

/** 一次檢查全部，回傳 欄位鍵 → 訊息（鍵只在程式內對照，不顯示）。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!form.seasonId) errors.seasonId = '請選擇賽季'
  if (form.teamIds.length === 0) errors.teamIds = '請至少選擇一支所屬球隊'
  if (!form.matchOn) errors.matchOn = '請選擇日期'
  if (!form.opponent.trim()) errors.opponent = '請輸入對手'
  if (isPostponed.value && !form.originalMatchOn) {
    errors.originalMatchOn = '狀態為「延賽」時必須填寫原定日期'
  }
  if (!isPostponed.value && (form.originalMatchOn || form.originalKickoff)) {
    errors.status = '只有狀態為「延賽」時才能填寫原定日期／時間，請先清空或改回延賽狀態'
  }
  if (form.goals.some((g) => !g.playerId)) errors.goals = '每一筆進球紀錄都要選擇球員'
  if (form.cards.some((c) => !c.playerId)) errors.cards = '每一筆卡牌紀錄都要選擇球員'
  if (form.lineups.some((l) => !l.playerId)) errors.lineups = '出賽名單裡每一列都要選擇球員'
  return errors
}

function buildPayload(): SaveMatchPayload {
  return {
    seasonId: form.seasonId,
    competitionId: form.competitionId || null,
    teamIds: form.teamIds,
    matchOn: form.matchOn,
    kickoff: form.kickoff || null,
    homeAway: form.homeAway || null,
    opponent: form.opponent.trim(),
    opponentEn: form.opponentEn || null,
    venueId: form.venueId || null,
    venue: form.venue || null,
    venueEn: form.venueEn || null,
    competitionTag: form.competitionTag || null,
    status: form.status,
    scoreHome: form.scoreHome,
    scoreAway: form.scoreAway,
    roundNo: form.roundNo,
    matchNo: form.matchNo,
    originalMatchOn: isPostponed.value ? form.originalMatchOn || null : null,
    originalKickoff: isPostponed.value ? form.originalKickoff || null : null,
    goals: form.goals.map((g) => ({ playerId: g.playerId, minute: g.minute, goalType: g.goalType || null })),
    cards: form.cards.map((c) => ({ playerId: c.playerId, cardType: c.cardType, minute: c.minute })),
    lineups: form.lineups.map((l) => ({ playerId: l.playerId, isStarter: l.isStarter })),
  }
}

async function handleSave() {
  if (isReadOnly.value) return
  formError.value = null
  if (formErrors.replaceAll(validate())) {
    await formErrors.focusFirst()
    return
  }
  saving.value = true
  try {
    if (isCreate.value) {
      const created = await createAdminMatch(activeClubId.value, buildPayload())
      ElMessage.success('已建立')
      router.replace(`/teams/matches/${created.id}/edit`)
      matchId.value = created.id
    } else {
      await updateAdminMatch(activeClubId.value, matchId.value!, buildPayload())
      ElMessage.success('已儲存')
    }
    baselineJson.value = JSON.stringify(form)
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'forbidden') {
      await ElMessageBox.alert(error.message, '沒有權限', { confirmButtonText: '我知道了' })
    } else if (error instanceof AdminApiError && formErrors.applyApiError(error)) {
      // 已標到欄位
    } else {
      formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
    }
  } finally {
    saving.value = false
  }
}

function handleBack() {
  router.push('/teams/matches')
}

function retryLoad() {
  loadMatch()
}

function clearRowErrors() {
  formErrors.clear('goals')
  formErrors.clear('cards')
  formErrors.clear('lineups')
}
function addGoal() {
  form.goals.push({ playerId: '', minute: null, goalType: '' })
  formErrors.clear('goals')
}
function addCard() {
  form.cards.push({ playerId: '', cardType: 'yellow', minute: null })
  formErrors.clear('cards')
}
function addLineup() {
  form.lineups.push({ playerId: '', isStarter: true })
  formErrors.clear('lineups')
}
</script>

<template>
  <div class="match-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="handleBack">
          <el-icon><ArrowLeft /></el-icon>
          返回列表
        </el-button>
      </template>
      <template #meta>
        <FrontendUnitBanner module-code="C4" />
        <span v-if="loadState === 'ready' && isReadOnly" class="match-edit__locked-note">
          <el-tag type="info" size="small">唯讀</el-tag>
          你的帳號沒有「{{ outOfScopeTeamNames.join('、') }}」的球隊授權範圍，這筆賽事僅能檢視，如需修改請聯繫系統管理員
        </span>
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never">
      <el-skeleton :rows="10" animated />
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
        class="match-edit__form-error"
        @close="formError = null"
      />

      <el-form label-position="top" :disabled="isReadOnly">
        <LangTabsBar>
          <el-card shadow="never" header="基本資料" class="match-edit__section">
            <el-row :gutter="12">
              <el-col :span="12">
                <FormField field="seasonId" label="賽季" required>
                  <el-select v-model="form.seasonId" placeholder="請選擇賽季" filterable style="width: 100%" @change="formErrors.clear('seasonId')">
                    <el-option v-for="s in seasons" :key="s.id" :label="s.code" :value="s.id" />
                  </el-select>
                </FormField>
              </el-col>
              <el-col :span="12">
                <FormField field="competitionId" label="賽事系列（選填）">
                  <el-select
                    v-model="form.competitionId"
                    clearable
                    filterable
                    placeholder="不指定即可（用下方賽事類型分類）"
                    style="width: 100%"
                    :no-data-text="form.seasonId ? '這個賽季還沒有任何賽事系列' : '請先選擇賽季'"
                    @change="formErrors.clear('competitionId')"
                  >
                    <el-option v-for="c in competitions" :key="c.id" :label="c.nameZh || c.code" :value="c.id" />
                  </el-select>
                </FormField>
              </el-col>
            </el-row>

            <FormField field="teamIds" label="所屬球隊（跨梯隊友誼賽可複選多支）" required>
              <el-select
                v-model="form.teamIds"
                multiple
                filterable
                placeholder="請選擇球隊"
                style="width: 100%"
                no-data-text="你的帳號目前沒有任何可以寫入的球隊，請聯繫系統管理員確認球隊授權"
                @change="formErrors.clear('teamIds')"
              >
                <el-option v-for="t in teamSelectOptions" :key="t.id" :label="t.label" :value="t.id" :disabled="t.disabled" />
              </el-select>
            </FormField>

            <el-row :gutter="12">
              <el-col :span="12">
                <FormField field="matchOn" label="日期" required>
                  <el-date-picker v-model="form.matchOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" placeholder="請選擇日期" @change="formErrors.clear('matchOn')" />
                </FormField>
              </el-col>
              <el-col :span="12">
                <FormField field="kickoff" label="時間（選填）">
                  <el-input v-model="form.kickoff" placeholder="例如 19:00" />
                </FormField>
              </el-col>
            </el-row>

            <el-row :gutter="12">
              <el-col :span="8">
                <FormField field="homeAway" label="主客場（選填）">
                  <el-select v-model="form.homeAway" clearable placeholder="不指定" style="width: 100%" @change="formErrors.clear('homeAway')">
                    <el-option v-for="v in MATCH_HOME_AWAY_ORDER" :key="v" :label="matchHomeAwayLabel(v)" :value="v" />
                  </el-select>
                </FormField>
              </el-col>
              <el-col :span="8">
                <FormField field="competitionTag" label="賽事類型（選填）">
                  <el-select v-model="form.competitionTag" clearable placeholder="不指定" style="width: 100%" @change="formErrors.clear('competitionTag')">
                    <el-option v-for="v in MATCH_COMPETITION_TAG_ORDER" :key="v" :label="matchCompetitionTagLabel(v)" :value="v" />
                  </el-select>
                </FormField>
              </el-col>
              <el-col :span="8">
                <FormField field="status" label="狀態" required>
                  <el-select v-model="form.status" style="width: 100%" @change="formErrors.clear('status')">
                    <el-option v-for="v in MATCH_STATUS_ORDER" :key="v" :label="matchStatusLabel(v)" :value="v" />
                  </el-select>
                </FormField>
              </el-col>
            </el-row>

            <BilingualShortField
              field="opponent"
              field-zh="opponent"
              field-en="opponentEn"
              label="對手"
              :zh="form.opponent"
              :en="form.opponentEn"
              required
              @update:zh="(v) => (form.opponent = v)"
              @update:en="(v) => (form.opponentEn = v)"
            />
            <FormField field="venueId" label="選擇既有場地（選填，客場等清單裡沒有的場地可略過，直接填下方文字欄位）">
              <el-select
                v-model="form.venueId"
                placeholder="選擇既有場地"
                filterable
                clearable
                style="width: 100%"
                @change="(v: string | null) => { formErrors.clear('venueId'); handleVenuePicked(v) }"
              >
                <el-option v-for="v in venues" :key="v.id" :label="v.nameZh" :value="v.id" />
              </el-select>
            </FormField>
            <BilingualShortField
              field="venue"
              field-zh="venue"
              field-en="venueEn"
              label="場地顯示文字（選填）"
              :zh="form.venue"
              :en="form.venueEn"
              @update:zh="(v) => (form.venue = v)"
              @update:en="(v) => (form.venueEn = v)"
            />

            <el-row :gutter="12">
              <el-col :span="12">
                <FormField field="matchNo" label="場次編號（選填，同賽季同賽事系列不可重複）">
                  <el-input-number v-model="form.matchNo" :min="1" style="width: 100%" />
                </FormField>
              </el-col>
              <el-col :span="12">
                <el-form-item label="輪次（選填）">
                  <el-input-number v-model="form.roundNo" :min="1" style="width: 100%" />
                </el-form-item>
              </el-col>
            </el-row>

            <template v-if="isPostponed">
              <el-alert
                title="狀態為「延賽」，請填寫原定日期時間，前台賽事卡片會顯示這場比賽原本排定的時間。"
                type="warning"
                show-icon
                :closable="false"
                class="match-edit__postponed-hint"
              />
              <el-row :gutter="12">
                <el-col :span="12">
                  <FormField field="originalMatchOn" label="原定日期" required>
                    <el-date-picker v-model="form.originalMatchOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" placeholder="請選擇原定日期" @change="formErrors.clear('originalMatchOn'); formErrors.clear('status')" />
                  </FormField>
                </el-col>
                <el-col :span="12">
                  <FormField field="originalKickoff" label="原定時間（選填）">
                    <el-input v-model="form.originalKickoff" placeholder="例如 19:00" />
                  </FormField>
                </el-col>
              </el-row>
            </template>
          </el-card>

          <el-card shadow="never" header="比分" class="match-edit__section">
            <el-row :gutter="12">
              <el-col :span="12">
                <el-form-item label="我方進球（選填）">
                  <el-input-number v-model="form.scoreHome" :min="0" style="width: 100%" />
                </el-form-item>
              </el-col>
              <el-col :span="12">
                <el-form-item label="對方進球（選填）">
                  <el-input-number v-model="form.scoreAway" :min="0" style="width: 100%" />
                </el-form-item>
              </el-col>
            </el-row>
          </el-card>

          <el-card shadow="never" header="進球者與時間" class="match-edit__section">
            <p v-if="form.teamIds.length === 0" class="match-edit__hint">請先選擇所屬球隊，才能選擇球員。</p>
            <FormField v-if="form.goals.length > 0" field="goals">
              <el-table v-if="form.goals.length > 0" :data="form.goals" class="match-edit__table">
                <el-table-column label="球員" min-width="200">
                  <template #default="{ row }">
                    <el-select v-model="row.playerId" filterable placeholder="選擇球員" style="width: 100%" @change="clearRowErrors">
                      <el-option v-for="p in matchPlayers" :key="p.id" :label="playerLabel(p.id)" :value="p.id" />
                    </el-select>
                  </template>
                </el-table-column>
                <el-table-column label="時間（分鐘，選填）" width="160">
                  <template #default="{ row }">
                    <el-input-number v-model="row.minute" :min="0" :max="130" style="width: 100%" />
                  </template>
                </el-table-column>
                <el-table-column label="類型（選填）" min-width="160">
                  <template #default="{ row }">
                    <el-input v-model="row.goalType" placeholder="例如：頭槌、點球、烏龍球" />
                  </template>
                </el-table-column>
                <el-table-column v-if="!isReadOnly" label="操作" width="80">
                  <template #default="{ $index }">
                    <el-button text type="danger" @click="form.goals.splice($index, 1); formErrors.clear('goals')">移除</el-button>
                  </template>
                </el-table-column>
              </el-table>
            </FormField>
            <el-button v-if="!isReadOnly" :disabled="form.teamIds.length === 0" @click="addGoal">+ 新增進球紀錄</el-button>
          </el-card>

          <el-card shadow="never" header="卡牌" class="match-edit__section">
            <FormField v-if="form.cards.length > 0" field="cards">
              <el-table v-if="form.cards.length > 0" :data="form.cards" class="match-edit__table">
                <el-table-column label="球員" min-width="200">
                  <template #default="{ row }">
                    <el-select v-model="row.playerId" filterable placeholder="選擇球員" style="width: 100%" @change="clearRowErrors">
                      <el-option v-for="p in matchPlayers" :key="p.id" :label="playerLabel(p.id)" :value="p.id" />
                    </el-select>
                  </template>
                </el-table-column>
                <el-table-column label="卡牌顏色" width="140">
                  <template #default="{ row }">
                    <el-select v-model="row.cardType" style="width: 100%">
                      <el-option v-for="v in MATCH_CARD_TYPE_ORDER" :key="v" :label="matchCardTypeLabel(v)" :value="v" />
                    </el-select>
                  </template>
                </el-table-column>
                <el-table-column label="時間（分鐘，選填）" width="160">
                  <template #default="{ row }">
                    <el-input-number v-model="row.minute" :min="0" :max="130" style="width: 100%" />
                  </template>
                </el-table-column>
                <el-table-column v-if="!isReadOnly" label="操作" width="80">
                  <template #default="{ $index }">
                    <el-button text type="danger" @click="form.cards.splice($index, 1); formErrors.clear('cards')">移除</el-button>
                  </template>
                </el-table-column>
              </el-table>
            </FormField>
            <el-button v-if="!isReadOnly" :disabled="form.teamIds.length === 0" @click="addCard">+ 新增卡牌紀錄</el-button>
          </el-card>

          <el-card shadow="never" header="出賽名單" class="match-edit__section">
            <FormField v-if="form.lineups.length > 0" field="lineups">
              <el-table v-if="form.lineups.length > 0" :data="form.lineups" class="match-edit__table">
                <el-table-column label="球員" min-width="200">
                  <template #default="{ row }">
                    <el-select v-model="row.playerId" filterable placeholder="選擇球員" style="width: 100%" @change="clearRowErrors">
                      <el-option v-for="p in matchPlayers" :key="p.id" :label="playerLabel(p.id)" :value="p.id" />
                    </el-select>
                  </template>
                </el-table-column>
                <el-table-column label="先發／替補" width="160">
                  <template #default="{ row }">
                    <el-radio-group v-model="row.isStarter">
                      <el-radio :value="true">先發</el-radio>
                      <el-radio :value="false">替補</el-radio>
                    </el-radio-group>
                  </template>
                </el-table-column>
                <el-table-column v-if="!isReadOnly" label="操作" width="80">
                  <template #default="{ $index }">
                    <el-button text type="danger" @click="form.lineups.splice($index, 1); formErrors.clear('lineups')">移除</el-button>
                  </template>
                </el-table-column>
              </el-table>
            </FormField>
            <el-button v-if="!isReadOnly" :disabled="form.teamIds.length === 0" @click="addLineup">+ 新增出賽名單</el-button>
          </el-card>
        </LangTabsBar>
      </el-form>

      <EditActionBar v-if="!isReadOnly">
        <template #status><FormErrorStatus /></template>
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </EditActionBar>
    </template>
  </div>
</template>

<style scoped>
.match-edit {
  max-width: 900px;
  margin: 0 auto 88px;
}

.match-edit__locked-note {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  color: var(--admin-text-secondary);
  flex-wrap: wrap;
}

.match-edit__form-error {
  margin-bottom: 16px;
}

.match-edit__section {
  margin-bottom: 16px;
}

.match-edit__postponed-hint {
  margin-bottom: 12px;
}

.match-edit__hint {
  margin: 0 0 8px;
  font-size: 12px;
  color: var(--admin-text-tertiary);
}

.match-edit__table {
  margin-bottom: 12px;
}
</style>

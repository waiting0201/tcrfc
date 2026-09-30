<script setup lang="ts">
/**
 * `L3` 分類設定（主站規劃書 §4.12 L3；apps/api/README.md「B1」L3 一節）。
 * 三個分頁：前台顯示設定（預設檢視／範圍／隊別、首頁與一線隊頁顯示哪些隊別、試訓是否同步）、
 * 隊別分類（前台顯示名稱、代表色、排序、是否出現在選單）、賽事與活動類型（兩隊共用，只有系統管理員能改）。
 *
 * 「不公開」的隊別不會出現在前台選單，它的行事曆訂閱連結也會失效——畫面在開關旁明講。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import {
  createCalendarEventType,
  deleteCalendarEventType,
  getCalendarSettings,
  listCalendarEventTypeIcons,
  reorderCalendarEventTypes,
  updateCalendarEventType,
  updateCalendarSettings,
  updateCalendarTeamSettings,
  type AdminEventTypeDto,
  type CalendarEventTypeIconDto,
  type CalendarSettingsDto,
  type CalendarTeamSettingDto,
} from '@/api/adminCalendar'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { usePermission } from '@/composables/useCrudPermissions'
import { useIsSuperAdmin } from '@/composables/useRolePermissions'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const club = computed(() => activeClubId.value)
const canView = usePermission('calendar.setting.view')
const canUpdate = usePermission('calendar.setting.update')
/** 賽事與活動類型是兩隊共用的資料，只有系統管理員能寫（後端規則）。 */
const canEditTypes = useIsSuperAdmin()

const tab = ref('display')
const settings = ref<CalendarSettingsDto | null>(null)
const icons = ref<CalendarEventTypeIconDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)

// ── 顯示設定 ─────────────────────────────────────────────────────────────────────────
const display = reactive({
  defaultView: 'list',
  defaultRange: 'upcoming',
  defaultTeamCode: 'all',
  homeTeamCodes: [] as string[],
  firstTeamCode: '' as string,
  syncTrials: false,
})
const savingDisplay = ref(false)

const VIEW_OPTIONS = [
  { value: 'list', label: '列表' },
  { value: 'month', label: '月曆' },
]
const RANGE_OPTIONS = [
  { value: 'upcoming', label: '即將到來的賽事與活動' },
  { value: 'this_month', label: '本月' },
  { value: 'next_30_days', label: '未來 30 天' },
  { value: 'season', label: '整個球季' },
]

// ── 隊別分類（可編輯的複本）───────────────────────────────────────────────────────────
interface TeamDraft {
  teamId: string
  teamNameZh: string
  effectiveColour: string | null
  displayNameZh: string
  displayNameEn: string
  colour: string | null
  sortOrder: number | null
  isPublic: boolean
}
const teamDrafts = ref<TeamDraft[]>([])
const savingTeams = ref(false)

function applySettings(s: CalendarSettingsDto) {
  settings.value = s
  display.defaultView = s.defaultView
  display.defaultRange = s.defaultRange
  display.defaultTeamCode = s.defaultTeamCode || 'all'
  display.homeTeamCodes = [...s.homeTeamCodes]
  display.firstTeamCode = s.firstTeamCode ?? ''
  display.syncTrials = s.syncTrials
  teamDrafts.value = s.teams.map((t: CalendarTeamSettingDto) => ({
    teamId: t.teamId,
    teamNameZh: t.teamNameZh,
    effectiveColour: t.effectiveColour ?? null,
    displayNameZh: t.displayNameZh ?? '',
    displayNameEn: t.displayNameEn ?? '',
    colour: t.colour ?? null,
    sortOrder: t.sortOrder ?? null,
    isPublic: t.isPublic,
  }))
  eventTypes.value = s.eventTypes
}

const teamOptions = computed(() => (settings.value?.teams ?? []).map((t) => ({ code: t.code, label: t.displayNameZh || t.teamNameZh })))

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const [s, ic] = await Promise.all([
      getCalendarSettings(club.value),
      listCalendarEventTypeIcons(club.value).catch(() => [] as CalendarEventTypeIconDto[]),
    ])
    applySettings(s)
    icons.value = ic
  } catch (error) {
    loadError.value = error instanceof AdminApiError ? error.message : '設定載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}
onMounted(load)
watch(club, load)

async function saveDisplay() {
  savingDisplay.value = true
  try {
    const result = await updateCalendarSettings(club.value, {
      defaultView: display.defaultView,
      defaultRange: display.defaultRange,
      defaultTeamCode: display.defaultTeamCode,
      homeTeamCodes: display.homeTeamCodes,
      firstTeamCode: display.firstTeamCode || null,
      syncTrials: display.syncTrials,
    })
    applySettings(result)
    ElMessage.success('已儲存')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試')
  } finally {
    savingDisplay.value = false
  }
}

async function saveTeams() {
  savingTeams.value = true
  try {
    const result = await updateCalendarTeamSettings(
      club.value,
      teamDrafts.value.map((t) => ({
        teamId: t.teamId,
        displayNameZh: t.displayNameZh.trim() || null,
        displayNameEn: t.displayNameEn.trim() || null,
        colour: t.colour || null,
        sortOrder: t.sortOrder,
        isPublic: t.isPublic,
      })),
    )
    applySettings(result)
    ElMessage.success('已儲存')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試')
  } finally {
    savingTeams.value = false
  }
}

// ── 賽事與活動類型 ─────────────────────────────────────────────────────────────────────
const eventTypes = ref<AdminEventTypeDto[]>([])
const iconLabelByCode = computed(() => new Map(icons.value.map((i) => [i.code, i.label])))

const typeDialog = ref(false)
const typeEditingId = ref<string | null>(null)
const typeSaving = ref(false)
const typeError = ref<string | null>(null)
const typeForm = reactive({ code: '', nameZh: '', nameEn: '', colour: null as string | null, icon: '' as string, isPublic: true, sortOrder: 0 })

function openTypeDialog(row?: AdminEventTypeDto) {
  typeEditingId.value = row?.id ?? null
  typeForm.code = row?.code ?? ''
  typeForm.nameZh = row?.nameZh ?? ''
  typeForm.nameEn = row?.nameEn ?? ''
  typeForm.colour = row?.colour ?? null
  typeForm.icon = row?.icon ?? ''
  typeForm.isPublic = row?.isPublic ?? true
  typeForm.sortOrder = row?.sortOrder ?? eventTypes.value.length
  typeError.value = null
  typeDialog.value = true
}

async function saveType() {
  typeError.value = null
  if (!typeForm.nameZh.trim()) return void (typeError.value = '請輸入中文名稱')
  if (!typeEditingId.value && !/^[a-z][a-z0-9_-]*$/.test(typeForm.code)) {
    return void (typeError.value = '類型代碼要以小寫英文字母開頭，只能用小寫英數、底線與連字號')
  }
  typeSaving.value = true
  try {
    const payload = {
      code: typeForm.code,
      nameZh: typeForm.nameZh.trim(),
      nameEn: typeForm.nameEn.trim() || null,
      colour: typeForm.colour || null,
      icon: typeForm.icon || null,
      isPublic: typeForm.isPublic,
      sortOrder: typeForm.sortOrder,
    }
    if (typeEditingId.value) await updateCalendarEventType(club.value, typeEditingId.value, payload)
    else await createCalendarEventType(club.value, payload)
    typeDialog.value = false
    ElMessage.success('已儲存')
    await load()
  } catch (error) {
    typeError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    typeSaving.value = false
  }
}

async function removeType(row: AdminEventTypeDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除「${row.nameZh || '（未命名）'}」嗎？`, '刪除類型', {
      confirmButtonText: '刪除',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteCalendarEventType(club.value, row.id)
    ElMessage.success('已刪除')
    await load()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
  }
}

async function moveType(index: number, delta: -1 | 1) {
  const target = index + delta
  if (target < 0 || target >= eventTypes.value.length) return
  const ids = eventTypes.value.map((t) => t.id)
  ;[ids[index], ids[target]] = [ids[target], ids[index]]
  try {
    await reorderCalendarEventTypes(club.value, ids)
    await load()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '調整順序失敗，請稍後再試')
  }
}
</script>

<template>
  <div class="calendar-categories">
    <PageHeader title="分類設定">
      <template #meta><FrontendUnitBanner module-code="L3" /></template>
    </PageHeader>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>

    <el-card v-else-if="!canView" shadow="never"><el-empty description="你的帳號沒有查看分類設定的權限" /></el-card>

    <el-card v-else shadow="never">
      <el-alert v-if="!canUpdate" title="你的帳號只能檢視這些設定，不能修改。" type="info" show-icon :closable="false" class="calendar-categories__alert" />
      <el-tabs v-model="tab">
        <!-- 顯示設定 -->
        <el-tab-pane label="前台顯示設定" name="display">
          <el-form label-position="top" :disabled="!canUpdate" class="calendar-categories__form">
            <el-row :gutter="16">
              <el-col :xs="24" :sm="12">
                <el-form-item label="行事曆預設檢視">
                  <el-radio-group v-model="display.defaultView">
                    <el-radio-button v-for="o in VIEW_OPTIONS" :key="o.value" :value="o.value">{{ o.label }}</el-radio-button>
                  </el-radio-group>
                </el-form-item>
              </el-col>
              <el-col :xs="24" :sm="12">
                <el-form-item label="預設顯示範圍">
                  <el-select v-model="display.defaultRange" style="width: 100%">
                    <el-option v-for="o in RANGE_OPTIONS" :key="o.value" :label="o.label" :value="o.value" />
                  </el-select>
                </el-form-item>
              </el-col>
              <el-col :xs="24" :sm="12">
                <el-form-item label="預設顯示的球隊">
                  <el-select v-model="display.defaultTeamCode" style="width: 100%">
                    <el-option label="全部球隊" value="all" />
                    <el-option v-for="t in teamOptions" :key="t.code" :label="t.label" :value="t.code" />
                  </el-select>
                </el-form-item>
              </el-col>
              <el-col :xs="24" :sm="12">
                <el-form-item label="一線隊頁面固定顯示的球隊">
                  <el-select v-model="display.firstTeamCode" clearable placeholder="不指定" style="width: 100%">
                    <el-option v-for="t in teamOptions" :key="t.code" :label="t.label" :value="t.code" />
                  </el-select>
                  <p class="calendar-categories__hint">各梯隊頁面會自動顯示自己那一隊，不需要設定。</p>
                </el-form-item>
              </el-col>
            </el-row>
            <el-form-item label="首頁近期賽事顯示哪些球隊">
              <el-select v-model="display.homeTeamCodes" multiple clearable placeholder="不選＝顯示全部公開的球隊" style="width: 100%">
                <el-option v-for="t in teamOptions" :key="t.code" :label="t.label" :value="t.code" />
              </el-select>
            </el-form-item>
            <el-form-item label="試訓是否同步到行事曆">
              <el-switch v-model="display.syncTrials" active-text="同步" inactive-text="不同步" />
              <p class="calendar-categories__hint">
                開啟後，這個俱樂部所有試訓場次都會以整天活動出現在前台行事曆與訂閱內容；關閉則不出現。這個開關會立刻套用到所有場次。預設為關閉。
              </p>
            </el-form-item>
            <el-button v-if="canUpdate" type="primary" :loading="savingDisplay" @click="saveDisplay">儲存顯示設定</el-button>
          </el-form>
        </el-tab-pane>

        <!-- 隊別分類 -->
        <el-tab-pane label="隊別分類" name="teams">
          <p class="calendar-categories__hint">
            這裡決定每支球隊在前台行事曆上的名稱、代表色與排列順序。欄位留空代表沿用球隊本身的設定。
            關閉「前台選單顯示」的球隊不會出現在前台選單，它的行事曆訂閱連結也會失效。
          </p>
          <el-empty v-if="teamDrafts.length === 0" description="這個俱樂部還沒有球隊" />
          <div v-else class="calendar-categories__team-grid">
            <el-card v-for="t in teamDrafts" :key="t.teamId" shadow="never" class="calendar-categories__team-card">
              <div class="calendar-categories__team-title">
                <span class="calendar-categories__dot" :style="{ background: t.colour || t.effectiveColour || 'var(--admin-text-tertiary)' }" />
                {{ t.teamNameZh }}
              </div>
              <el-form label-position="top" :disabled="!canUpdate">
                <el-form-item label="前台顯示名稱（中文）">
                  <el-input v-model="t.displayNameZh" :placeholder="t.teamNameZh" maxlength="64" />
                </el-form-item>
                <el-form-item label="前台顯示名稱（英文）"><el-input v-model="t.displayNameEn" maxlength="64" /></el-form-item>
                <el-row :gutter="12">
                  <el-col :span="12">
                    <el-form-item label="代表色">
                      <el-color-picker v-model="t.colour" color-format="hex" />
                      <el-button v-if="t.colour" size="small" text @click="t.colour = null">沿用球隊設定</el-button>
                    </el-form-item>
                  </el-col>
                  <el-col :span="12">
                    <el-form-item label="排序值">
                      <el-input-number v-model="t.sortOrder" :min="0" controls-position="right" style="width: 100%" />
                    </el-form-item>
                  </el-col>
                </el-row>
                <el-form-item>
                  <el-switch v-model="t.isPublic" active-text="前台選單顯示" inactive-text="不公開" />
                </el-form-item>
              </el-form>
            </el-card>
          </div>
          <el-button v-if="canUpdate && teamDrafts.length > 0" type="primary" :loading="savingTeams" @click="saveTeams">儲存隊別分類</el-button>
        </el-tab-pane>

        <!-- 類型 -->
        <el-tab-pane label="賽事與活動類型" name="types">
          <el-alert
            v-if="!canEditTypes"
            title="賽事與活動類型是台中磐石與台中藍鯨共用的，只有系統管理員能新增或修改。"
            type="info"
            show-icon
            :closable="false"
            class="calendar-categories__alert"
          />
          <div class="calendar-categories__toolbar">
            <span class="calendar-categories__hint">類型的圖示只能從系統預設的圖示中挑選，不能上傳圖片。</span>
            <el-button v-if="canEditTypes" type="primary" @click="openTypeDialog()">+ 新增類型</el-button>
          </div>
          <template v-if="eventTypes.length > 0">
            <el-table v-if="!isMobile" :data="eventTypes" row-key="id">
              <el-table-column label="顏色" width="70">
                <template #default="{ row }">
                  <span class="calendar-categories__dot calendar-categories__dot--lg" :style="{ background: row.colour || 'var(--admin-text-tertiary)' }" />
                </template>
              </el-table-column>
              <el-table-column label="名稱" min-width="160">
                <template #default="{ row }">
                  {{ row.nameZh }}
                  <div v-if="row.nameEn" class="calendar-categories__hint">{{ row.nameEn }}</div>
                </template>
              </el-table-column>
              <el-table-column label="圖示" width="120">
                <template #default="{ row }">{{ (row.icon && iconLabelByCode.get(row.icon)) || '—' }}</template>
              </el-table-column>
              <el-table-column label="前台" width="90">
                <template #default="{ row }">
                  <el-tag :type="row.isPublic ? 'success' : 'info'" size="small">{{ row.isPublic ? '公開' : '不公開' }}</el-tag>
                </template>
              </el-table-column>
              <el-table-column label="使用中" width="100">
                <template #default="{ row }">{{ row.usageCount ?? 0 }} 個活動</template>
              </el-table-column>
              <el-table-column v-if="canEditTypes" label="順序" width="96">
                <template #default="{ $index }">
                  <el-button size="small" text :disabled="$index === 0" aria-label="上移" @click="moveType($index, -1)"><el-icon><ArrowUp /></el-icon></el-button>
                  <el-button size="small" text :disabled="$index === eventTypes.length - 1" aria-label="下移" @click="moveType($index, 1)"><el-icon><ArrowDown /></el-icon></el-button>
                </template>
              </el-table-column>
              <el-table-column v-if="canEditTypes" label="操作" width="130" fixed="right">
                <template #default="{ row }">
                  <el-button size="small" text type="primary" @click="openTypeDialog(row)">編輯</el-button>
                  <el-button size="small" text type="danger" @click="removeType(row)">刪除</el-button>
                </template>
              </el-table-column>
            </el-table>
            <MobileCardList v-else :rows="eventTypes" row-key="id">
              <template #title="{ row }">{{ row.nameZh }}</template>
              <template #meta="{ row }">
                <span class="calendar-categories__dot" :style="{ background: row.colour || 'var(--admin-text-tertiary)' }" />
                <el-tag :type="row.isPublic ? 'success' : 'info'" size="small">{{ row.isPublic ? '公開' : '不公開' }}</el-tag>
                <span>使用中 {{ row.usageCount ?? 0 }} 個活動</span>
              </template>
              <template #actions="{ row }">
                <template v-if="canEditTypes">
                  <el-button size="small" text type="primary" @click="openTypeDialog(row)">編輯</el-button>
                  <el-button size="small" text type="danger" @click="removeType(row)">刪除</el-button>
                </template>
              </template>
            </MobileCardList>
          </template>
          <el-empty v-else description="還沒有任何類型" />
        </el-tab-pane>
      </el-tabs>
    </el-card>

    <el-dialog v-model="typeDialog" :title="typeEditingId ? '編輯類型' : '新增類型'" width="480px" :close-on-click-modal="false">
      <el-alert v-if="typeError" :title="typeError" type="warning" show-icon :closable="false" class="calendar-categories__alert" />
      <el-form label-position="top">
        <el-form-item label="類型代碼（全站不可重複，建立後不能修改）">
          <el-input v-model="typeForm.code" :disabled="!!typeEditingId" maxlength="32" placeholder="例如 fan-day" />
        </el-form-item>
        <el-form-item label="中文名稱" required><el-input v-model="typeForm.nameZh" maxlength="64" /></el-form-item>
        <el-form-item label="英文名稱"><el-input v-model="typeForm.nameEn" maxlength="64" /></el-form-item>
        <el-row :gutter="12">
          <el-col :span="12">
            <el-form-item label="顏色"><el-color-picker v-model="typeForm.colour" color-format="hex" /></el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="排序值"><el-input-number v-model="typeForm.sortOrder" :min="0" controls-position="right" style="width: 100%" /></el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="圖示">
          <el-select v-model="typeForm.icon" clearable placeholder="不使用圖示" style="width: 100%">
            <el-option v-for="i in icons" :key="i.code" :label="i.label" :value="i.code" />
          </el-select>
        </el-form-item>
        <el-form-item><el-switch v-model="typeForm.isPublic" active-text="前台公開" inactive-text="不公開" /></el-form-item>
      </el-form>
      <template #footer>
        <el-button :disabled="typeSaving" @click="typeDialog = false">取消</el-button>
        <el-button type="primary" :loading="typeSaving" @click="saveType">儲存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.calendar-categories__alert { margin-bottom: 12px; }
.calendar-categories__form { max-width: 860px; }
.calendar-categories__hint { margin: 4px 0 8px; font-size: 12px; line-height: 1.6; color: var(--admin-text-tertiary); }
.calendar-categories__toolbar { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 8px; margin-bottom: 8px; }
.calendar-categories__team-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(min(100%, 300px), 1fr)); gap: 12px; margin: 12px 0 16px; }
.calendar-categories__team-card { min-width: 0; }
.calendar-categories__team-title { display: flex; align-items: center; gap: 8px; font-weight: 600; margin-bottom: 10px; }
.calendar-categories__dot { display: inline-block; width: 10px; height: 10px; border-radius: 50%; flex: none; }
.calendar-categories__dot--lg { width: 16px; height: 16px; }
</style>

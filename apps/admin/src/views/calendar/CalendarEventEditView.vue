<script setup lang="ts">
/**
 * `L2` 自建事件——編輯頁。對照 apps/api/README.md「S1-11」。
 *
 * ⚠️ **場地選單本輪不提供選擇介面**——跟 `MatchEditView.vue`／`ProgramSessionEditView.vue` 遇到的
 * 既有缺口相同：`venues` 是共用主檔，但整個系統目前沒有任何後台端點可以列出場地清單，沒有清單就
 * 做不出有意義的選單，`venueId` 因此一律不送出（省略＝維持不變／建立時等同不設定）。
 *
 * ⚠️ **L2 的 `.ics` 下載本輪未實作**——後端只做了單場賽事的 `.ics`（規劃書明確要求），自建事件
 * 加入行事曆的能力規劃書沒有明文要求，見 apps/api/README.md「S1-11」「規劃書沒寫清楚」第 7 點。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useCalendarPermissions } from '@/composables/useCalendarPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { listAdminClubTeams, type AdminTeamAdminListItemDto } from '@/api/adminTeams'
import {
  createAdminCalendarCustomEvent,
  getAdminCalendarCustomEvent,
  listAdminCalendarEventTypes,
  updateAdminCalendarCustomEvent,
  type AdminEventTypeDto,
  type SaveCalendarCustomEventPayload,
} from '@/api/adminCalendar'
import { AdminApiError } from '@/api/http'
import { REPEAT_RULE_LABEL, REPEAT_RULE_ORDER, type RepeatRule } from '@/types/calendar'

const route = useRoute()
const router = useRouter()
const { canManageCustomEvents } = useCalendarPermissions()

// 🔴 必須是 computed，不能是一次性求值的 const——理由同 `ProgramItemEditView.vue`／
// `MatchEditView.vue` 檔頭說明：建立成功後 `router.replace` 不會重新掛載這個元件實例。
const isCreate = computed(() => route.name === 'calendar-event-new')
const eventId = ref<string | undefined>(route.params.id as string | undefined)

function toDateOnlyString(date: Date | null): string | null {
  if (!date) return null
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

function fromDateOnlyString(value: string | null | undefined): Date | null {
  if (!value) return null
  const parsed = new Date(`${value}T00:00:00`)
  return Number.isNaN(parsed.getTime()) ? null : parsed
}

function fromIsoString(value: string | null | undefined): Date | null {
  if (!value) return null
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? null : parsed
}

const form = reactive({
  eventTypeId: '' as string,
  startsAt: null as Date | null,
  endsAt: null as Date | null,
  isAllDay: false,
  repeatRule: '' as RepeatRule | '',
  repeatUntil: null as Date | null,
  exceptionDates: [] as string[],
  isPublic: true,
  ctaUrl: '',
  teamIds: [] as string[],
  titleZh: '',
  titleEn: '',
  descriptionZh: '',
  descriptionEn: '',
})
const baselineJson = ref('')
const coverKey = ref<string | null>(null)
const coverFile = ref<File | null>(null)
const removeCover = ref(false)
const exceptionDatePicker = ref<Date | null>(null)

const eventTypes = ref<AdminEventTypeDto[]>([])
const teams = ref<AdminTeamAdminListItemDto[]>([])
const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
const formError = ref<string | null>(null)

async function loadOptions() {
  try {
    const [types, teamList] = await Promise.all([
      listAdminCalendarEventTypes(activeClubId.value),
      listAdminClubTeams(activeClubId.value),
    ])
    eventTypes.value = types
    teams.value = teamList
  } catch {
    eventTypes.value = []
    teams.value = []
  }
}

async function loadEvent() {
  loadState.value = 'loading'
  try {
    await loadOptions()
    if (!isCreate.value && eventId.value) {
      const detail = await getAdminCalendarCustomEvent(activeClubId.value, eventId.value)
      form.eventTypeId = detail.eventTypeId ?? ''
      form.startsAt = fromIsoString(detail.startsAt)
      form.endsAt = fromIsoString(detail.endsAt)
      form.isAllDay = detail.isAllDay
      form.repeatRule = (detail.repeatRule as RepeatRule) ?? ''
      form.repeatUntil = fromDateOnlyString(detail.repeatUntil)
      form.exceptionDates = [...detail.exceptionDates]
      form.isPublic = detail.isPublic
      form.ctaUrl = detail.ctaUrl ?? ''
      form.teamIds = [...detail.teamIds]
      form.titleZh = detail.zh.title ?? ''
      form.descriptionZh = detail.zh.description ?? ''
      form.titleEn = detail.en?.title ?? ''
      form.descriptionEn = detail.en?.description ?? ''
      coverKey.value = detail.coverKey ?? null
    } else {
      form.isPublic = true
    }
    coverFile.value = null
    removeCover.value = false
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

onMounted(loadEvent)

const isDirty = computed(
  () => loadState.value === 'ready' && (JSON.stringify(form) !== baselineJson.value || coverFile.value !== null || removeCover.value),
)
useUnsavedChanges(isDirty)

const pageTitle = computed(() =>
  isCreate.value ? '新增自建事件' : `${canManageCustomEvents.value ? '編輯' : '檢視'}自建事件：${form.titleZh || '（未命名活動）'}`,
)
const isReadOnly = computed(() => !canManageCustomEvents.value)

function addExceptionDate() {
  if (!exceptionDatePicker.value) return
  const s = toDateOnlyString(exceptionDatePicker.value)!
  if (!form.exceptionDates.includes(s)) form.exceptionDates.push(s)
  exceptionDatePicker.value = null
}

function removeExceptionDate(value: string) {
  form.exceptionDates = form.exceptionDates.filter((d) => d !== value)
}

function validate(): boolean {
  formError.value = null
  if (!form.startsAt) {
    formError.value = '請選擇開始時間'
    return false
  }
  if (form.endsAt && form.endsAt < form.startsAt) {
    formError.value = '結束時間不能早於開始時間'
    return false
  }
  if (!form.titleZh.trim()) {
    formError.value = '請輸入中文標題'
    return false
  }
  if (form.descriptionEn.trim() && !form.titleEn.trim()) {
    formError.value = '有英文說明時請一併填寫英文標題（或清空英文說明）'
    return false
  }
  return true
}

function buildPayload(): SaveCalendarCustomEventPayload {
  return {
    eventTypeId: form.eventTypeId || null,
    startsAt: form.startsAt!.toISOString(),
    endsAt: form.endsAt ? form.endsAt.toISOString() : null,
    isAllDay: form.isAllDay,
    repeatRule: form.repeatRule || null,
    repeatUntil: form.repeatRule ? toDateOnlyString(form.repeatUntil) : null,
    exceptionDates: form.exceptionDates,
    isPublic: form.isPublic,
    ctaUrl: form.ctaUrl.trim() || null,
    teamIds: form.teamIds,
    content: {
      zh: { title: form.titleZh.trim(), description: form.descriptionZh || null },
      en: form.titleEn.trim() ? { title: form.titleEn.trim(), description: form.descriptionEn || null } : undefined,
    },
  }
}

async function handleSave() {
  if (isReadOnly.value) return
  if (!validate()) return
  saving.value = true
  formError.value = null
  try {
    if (isCreate.value) {
      const created = await createAdminCalendarCustomEvent(activeClubId.value, buildPayload(), coverFile.value)
      ElMessage.success('已建立')
      router.replace(`/calendar/events/${created.id}/edit`)
      eventId.value = created.id
      coverKey.value = created.coverKey ?? null
    } else {
      const updated = await updateAdminCalendarCustomEvent(
        activeClubId.value,
        eventId.value!,
        { ...buildPayload(), removeCover: removeCover.value },
        coverFile.value,
      )
      coverKey.value = updated.coverKey ?? null
      ElMessage.success('已儲存')
    }
    coverFile.value = null
    removeCover.value = false
    baselineJson.value = JSON.stringify(form)
  } catch (error) {
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

function handleBack() {
  router.push('/calendar/events')
}

function retryLoad() {
  loadEvent()
}
</script>

<template>
  <div class="calendar-event-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="handleBack">
          <el-icon><ArrowLeft /></el-icon>
          返回列表
        </el-button>
      </template>
      <template #meta>
        <FrontendUnitBanner module-code="L2" />
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never">
      <el-skeleton :rows="8" animated />
    </el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這筆自建事件，可能已經被刪除，或不屬於目前選擇的俱樂部。</p>
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
        class="calendar-event-edit__form-error"
        @close="formError = null"
      />
      <el-alert
        v-if="isReadOnly"
        title="你的帳號只有檢視權限，沒有這個模組的建立／編輯權限"
        type="info"
        show-icon
        :closable="false"
        class="calendar-event-edit__form-error"
      />

      <el-form label-position="top" :disabled="isReadOnly">
        <el-card shadow="never" header="時間與重複規則" class="calendar-event-edit__section">
          <el-row :gutter="12">
            <el-col :span="8">
              <el-form-item label="開始時間" required>
                <el-date-picker v-model="form.startsAt" type="datetime" style="width: 100%" placeholder="選擇開始時間" />
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="結束時間">
                <el-date-picker v-model="form.endsAt" type="datetime" style="width: 100%" placeholder="選填，支援跨日" />
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="全天活動">
                <el-switch v-model="form.isAllDay" />
              </el-form-item>
            </el-col>
          </el-row>

          <el-row :gutter="12">
            <el-col :span="8">
              <el-form-item label="重複規則">
                <el-select v-model="form.repeatRule" clearable placeholder="不重複" style="width: 100%">
                  <el-option v-for="r in REPEAT_RULE_ORDER" :key="r" :label="REPEAT_RULE_LABEL[r]" :value="r" />
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="重複結束日期">
                <el-date-picker
                  v-model="form.repeatUntil"
                  type="date"
                  :disabled="!form.repeatRule || isReadOnly"
                  style="width: 100%"
                  placeholder="留空表示不設結束日期"
                />
              </el-form-item>
            </el-col>
          </el-row>

          <el-form-item v-if="form.repeatRule" label="例外日期（這幾天不會出現重複發生的活動）">
            <div class="calendar-event-edit__exception-row">
              <el-date-picker v-model="exceptionDatePicker" type="date" placeholder="選擇日期" :disabled="isReadOnly" />
              <el-button :disabled="!exceptionDatePicker || isReadOnly" @click="addExceptionDate">加入</el-button>
            </div>
            <div v-if="form.exceptionDates.length > 0" class="calendar-event-edit__exception-tags">
              <el-tag
                v-for="d in form.exceptionDates"
                :key="d"
                closable
                :disable-transitions="true"
                @close="removeExceptionDate(d)"
              >
                {{ d }}
              </el-tag>
            </div>
          </el-form-item>
        </el-card>

        <el-card shadow="never" header="分類、地點與所屬隊別" class="calendar-event-edit__section">
          <el-row :gutter="12">
            <el-col :span="12">
              <el-form-item label="分類">
                <el-select v-model="form.eventTypeId" clearable placeholder="請選擇分類" style="width: 100%">
                  <el-option v-for="t in eventTypes" :key="t.id" :label="t.nameZh || t.code" :value="t.id" />
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="是否公開於前台">
                <el-switch v-model="form.isPublic" active-text="公開" inactive-text="不公開" />
              </el-form-item>
            </el-col>
          </el-row>

          <el-form-item label="場地">
            <p class="calendar-event-edit__hint">
              場地選單目前沒有可用清單（`venues` 主檔尚無對應後台端點），暫不開放選擇。
            </p>
          </el-form-item>

          <el-form-item label="所屬隊別（可複選；留空＝俱樂部活動）">
            <el-select v-model="form.teamIds" multiple filterable placeholder="留空表示俱樂部活動" style="width: 100%">
              <el-option v-for="t in teams" :key="t.id" :label="t.nameZh || t.code" :value="t.id" />
            </el-select>
          </el-form-item>

          <el-form-item label="外部連結或 CTA 網址">
            <el-input v-model="form.ctaUrl" placeholder="選填，例如報名連結" />
          </el-form-item>
        </el-card>

        <el-card shadow="never" header="標題、說明與封面圖" class="calendar-event-edit__section">
          <BilingualShortField
            label="標題"
            :zh="form.titleZh"
            :en="form.titleEn"
            required
            @update:zh="(v) => (form.titleZh = v)"
            @update:en="(v) => (form.titleEn = v)"
          />
          <BilingualTextareaField
            label="說明"
            :zh="form.descriptionZh"
            :en="form.descriptionEn"
            @update:zh="(v) => (form.descriptionZh = v)"
            @update:en="(v) => (form.descriptionEn = v)"
          />

          <el-form-item label="封面圖">
            <ImageUploader
              v-model:file="coverFile"
              v-model:remove-cover="removeCover"
              :has-existing-image="!!coverKey"
              :disabled="saving || isReadOnly"
            />
          </el-form-item>
        </el-card>
      </el-form>

      <div v-if="!isReadOnly" class="calendar-event-edit__action-bar">
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.calendar-event-edit {
  max-width: 780px;
  margin: 0 auto 88px;
}

.calendar-event-edit__form-error {
  margin-bottom: 16px;
}

.calendar-event-edit__section {
  margin-bottom: 16px;
}

.calendar-event-edit__hint {
  margin: 0 0 8px;
  font-size: 12px;
  color: var(--admin-text-tertiary);
  line-height: 1.6;
}

.calendar-event-edit__exception-row {
  display: flex;
  gap: 8px;
  align-items: center;
}

.calendar-event-edit__exception-tags {
  margin-top: 8px;
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.calendar-event-edit__action-bar {
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
  .calendar-event-edit__action-bar {
    left: 0;
  }
}

@media (max-width: 767px) {
  .calendar-event-edit__action-bar {
    justify-content: stretch;
  }

  .calendar-event-edit__action-bar :deep(.el-button) {
    flex: 1;
  }
}
</style>

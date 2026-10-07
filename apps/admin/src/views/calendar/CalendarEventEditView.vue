<script setup lang="ts">
/**
 * `L2` 自建事件——編輯頁。對照 apps/api/README.md「S1-11」。
 *
 * 場地選單來自場地管理清單（`listAdminVenues`），不選＝不設定場地。
 *
 * ⚠️ **L2 的 `.ics` 下載本輪未實作**——後端只做了單場賽事的 `.ics`（規劃書明確要求），自建事件
 * 加入行事曆的能力規劃書沒有明文要求，見 apps/api/README.md「S1-11」「規劃書沒寫清楚」第 7 點。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import EditLayout from '@/components/EditLayout.vue'
import FormSection from '@/components/FormSection.vue'
import FormErrorStatus from '@/components/FormErrorStatus.vue'
import FormField from '@/components/FormField.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useCalendarPermissions } from '@/composables/useCalendarPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { listAdminVenues, type AdminVenueListItemDto } from '@/api/adminVenues'
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
import { dateOnlyToPickerDate as fromDateOnlyString, pickerDateToAllDayUtc, pickerDateToDateOnly as toDateOnlyString, pickerDateToUtc, utcToPickerDate } from '@/utils/dateTime'

const route = useRoute()
const router = useRouter()
const { canManageCustomEvents } = useCalendarPermissions()

// 🔴 必須是 computed，不能是一次性求值的 const——理由同 `ProgramItemEditView.vue`／
// `MatchEditView.vue` 檔頭說明：建立成功後 `router.replace` 不會重新掛載這個元件實例。
const isCreate = computed(() => route.name === 'calendar-event-new')
const eventId = ref<string | undefined>(route.params.id as string | undefined)

const form = reactive({
  eventTypeId: '' as string,
  venueId: '' as string,
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
/** 既有圖片的預覽網址（後端附帶的 coverThumbUrl／coverUrl，沒有就是 null）。 */
const coverPreviewUrl = ref<string | null>(null)
const coverFile = ref<File | null>(null)
const removeCover = ref(false)
const exceptionDatePicker = ref<Date | null>(null)

const eventTypes = ref<AdminEventTypeDto[]>([])
const teams = ref<AdminTeamAdminListItemDto[]>([])
const venues = ref<AdminVenueListItemDto[]>([])
const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
/** 頁首提示：只放「沒有對到欄位」的錯誤；欄位錯誤標在欄位上（formErrors）。 */
const formError = ref<string | null>(null)
const formErrors = provideFormErrors()
// 選檔或移除封面圖就清掉該欄位的錯誤
watch([coverFile, removeCover], () => formErrors.clear('cover'))

async function loadOptions() {
  try {
    const [types, teamList, venueList] = await Promise.all([
      listAdminCalendarEventTypes(activeClubId.value),
      listAdminClubTeams(activeClubId.value),
      listAdminVenues(activeClubId.value).catch(() => [] as AdminVenueListItemDto[]),
    ])
    eventTypes.value = types
    teams.value = teamList
    venues.value = venueList
  } catch {
    eventTypes.value = []
    teams.value = []
    venues.value = []
  }
}

async function loadEvent() {
  loadState.value = 'loading'
  try {
    await loadOptions()
    if (!isCreate.value && eventId.value) {
      const detail = await getAdminCalendarCustomEvent(activeClubId.value, eventId.value)
      form.eventTypeId = detail.eventTypeId ?? ''
      form.venueId = detail.venueId ?? ''
      form.startsAt = utcToPickerDate(detail.startsAt)
      form.endsAt = utcToPickerDate(detail.endsAt)
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
      coverPreviewUrl.value = detail.coverThumbUrl ?? detail.coverUrl ?? null
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

/** 一次檢查全部，回傳 欄位鍵 → 訊息（鍵只在程式內對照，不顯示）。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!form.startsAt) errors.startsAt = '請選擇開始時間'
  else if (form.endsAt && form.endsAt < form.startsAt) errors.endsAt = '結束時間不能早於開始時間'
  if (!form.titleZh.trim()) errors.titleZh = '請輸入中文標題'
  const cta = form.ctaUrl.trim()
  if (cta && !/^https?:\/\/\S+$/i.test(cta) && !(/^\/(?![/\\])\S*$/.test(cta))) {
    errors.ctaUrl = '連結要以 https:// 或 http:// 開頭的完整網址，或以 / 開頭的站內路徑'
  }
  if (form.descriptionEn.trim() && !form.titleEn.trim()) {
    errors.titleEn = '有英文說明時請一併填寫英文標題（或清空英文說明）'
  }
  return errors
}

function buildPayload(): SaveCalendarCustomEventPayload {
  return {
    eventTypeId: form.eventTypeId || null,
    venueId: form.venueId || null,
    // 全天活動：後端以「UTC 日期」整天計，送選擇日期的 00:00Z；一般活動送台灣時間換算後的 UTC
    startsAt: (form.isAllDay ? pickerDateToAllDayUtc(form.startsAt) : pickerDateToUtc(form.startsAt))!,
    endsAt: form.isAllDay ? pickerDateToAllDayUtc(form.endsAt) : pickerDateToUtc(form.endsAt),
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
  formError.value = null
  if (formErrors.replaceAll(validate())) {
    await formErrors.focusFirst()
    return
  }
  saving.value = true
  try {
    if (isCreate.value) {
      const created = await createAdminCalendarCustomEvent(activeClubId.value, buildPayload(), coverFile.value)
      ElMessage.success('已建立')
      router.replace(`/calendar/events/${created.id}/edit`)
      eventId.value = created.id
      coverKey.value = created.coverKey ?? null
      coverPreviewUrl.value = created.coverThumbUrl ?? created.coverUrl ?? null
    } else {
      const updated = await updateAdminCalendarCustomEvent(
        activeClubId.value,
        eventId.value!,
        { ...buildPayload(), removeCover: removeCover.value },
        coverFile.value,
      )
      coverKey.value = updated.coverKey ?? null
      coverPreviewUrl.value = updated.coverThumbUrl ?? updated.coverUrl ?? null
      ElMessage.success('已儲存')
    }
    coverFile.value = null
    removeCover.value = false
    baselineJson.value = JSON.stringify(form)
  } catch (error) {
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
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
        <LangTabsBar>
          <EditLayout>
            <template #main>
              <el-card shadow="never">
                <FormSection>
                  <BilingualShortField
                    field="title"
                    label="標題"
                    :zh="form.titleZh"
                    :en="form.titleEn"
                    required
                    @update:zh="(v) => (form.titleZh = v)"
                    @update:en="(v) => (form.titleEn = v)"
                  />
                  <BilingualTextareaField
                    field="description"
                    label="說明"
                    :zh="form.descriptionZh"
                    :en="form.descriptionEn"
                    @update:zh="(v) => (form.descriptionZh = v)"
                    @update:en="(v) => (form.descriptionEn = v)"
                  />
                </FormSection>
              </el-card>
            </template>

            <template #aside>
              <el-card shadow="never" header="基本設定">
                <FormSection title="時間與重複規則">
                  <el-row :gutter="12">
                    <el-col :span="24">
                      <FormField field="startsAt" label="開始時間" required>
                        <el-date-picker v-model="form.startsAt" type="datetime" style="width: 100%" placeholder="選擇開始時間" @change="formErrors.clear('startsAt')" />
                      </FormField>
                    </el-col>
                    <el-col :span="24">
                      <FormField field="endsAt" label="結束時間">
                        <el-date-picker v-model="form.endsAt" type="datetime" style="width: 100%" placeholder="選填，支援跨日" @change="formErrors.clear('endsAt')" />
                      </FormField>
                    </el-col>
                    <el-col :span="24">
                      <el-form-item label="全天活動">
                        <el-switch v-model="form.isAllDay" />
                      </el-form-item>
                    </el-col>
                  </el-row>

                  <el-row :gutter="12">
                    <el-col :span="24">
                      <FormField field="repeatRule" label="重複規則">
                        <el-select v-model="form.repeatRule" clearable placeholder="不重複" style="width: 100%">
                          <el-option v-for="r in REPEAT_RULE_ORDER" :key="r" :label="REPEAT_RULE_LABEL[r]" :value="r" />
                        </el-select>
                      </FormField>
                    </el-col>
                    <el-col :span="24">
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
                </FormSection>

                <FormSection title="分類、地點與所屬隊別">
                  <FormField field="eventTypeId" label="分類">
                    <el-select v-model="form.eventTypeId" clearable placeholder="請選擇分類" style="width: 100%">
                      <el-option v-for="t in eventTypes" :key="t.id" :label="t.nameZh || t.code" :value="t.id" />
                    </el-select>
                  </FormField>

                  <FormField field="venueId" label="場地">
                    <el-select v-model="form.venueId" clearable filterable placeholder="選填，不指定場地" style="width: 100%">
                      <el-option v-for="v in venues" :key="v.id" :label="v.nameZh" :value="v.id" />
                    </el-select>
                  </FormField>

                  <FormField field="teamIds" label="所屬隊別（可複選；留空＝俱樂部活動）">
                    <el-select v-model="form.teamIds" multiple filterable placeholder="留空表示俱樂部活動" style="width: 100%">
                      <el-option v-for="t in teams" :key="t.id" :label="t.nameZh || t.code" :value="t.id" />
                    </el-select>
                  </FormField>

                  <FormField field="ctaUrl" label="外部連結或 CTA 網址">
                    <el-input v-model="form.ctaUrl" placeholder="選填，例如 https://… 或 /zh/programs/" />
                    <p class="calendar-event-edit__hint">以 https:// 或 http:// 開頭的完整網址，或以 / 開頭的站內路徑。</p>
                  </FormField>
                </FormSection>

                <FormSection title="封面圖">
                  <FormField field="cover" label="封面圖">
                    <ImageUploader
                      v-model:file="coverFile"
                      v-model:remove-cover="removeCover"
                      :has-existing-image="!!coverKey"
                      :existing-preview-url="coverPreviewUrl"
                      :disabled="saving || isReadOnly"
                    />
                  </FormField>
                </FormSection>
              </el-card>

              <el-card shadow="never" header="發布設定">
                <el-form-item label="是否公開於前台">
                  <el-switch v-model="form.isPublic" active-text="公開" inactive-text="不公開" />
                </el-form-item>
              </el-card>
            </template>
          </EditLayout>
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
.calendar-event-edit {
  max-width: 1200px;
  margin: 0 auto;
}

.calendar-event-edit__form-error {
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
</style>

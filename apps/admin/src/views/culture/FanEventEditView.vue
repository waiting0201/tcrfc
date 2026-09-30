<script setup lang="ts">
/**
 * 球迷會活動——新增／編輯，含封面、活動回顧圖集、關聯報導與報名名單。
 * 時間以台灣時間輸入，送出時換成標準時間；發布必須填開始時間。
 * 圖集與報名操作立即生效，其餘欄位按「儲存」才送出。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import GalleryManager from '@/components/GalleryManager.vue'
import NewsPicker from '@/components/NewsPicker.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import FanEventRegistrations from './parts/FanEventRegistrations.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { enOrUndefined, nullIfBlank } from '@/api/adminCommon'
import { listAdminVenues, type AdminVenueListItemDto } from '@/api/adminVenues'
import {
  addFanEventImages,
  createFanEvent,
  deleteFanEventImage,
  getFanEvent,
  reorderFanEventImages,
  updateFanEvent,
  type FanEventDetailDto,
  type FanEventStatus,
} from '@/api/adminFanEvents'
import { localInputToUtc, utcToLocalInput } from '@/utils/formatDateTime'

const route = useRoute()
const router = useRouter()
const isCreate = computed(() => route.name === 'fan-event-new')
const eventId = ref<string | undefined>(route.params.id as string | undefined)
const { canCreate, canUpdate } = useCrudPermissions('culture.fan_event')
const club = computed(() => activeClubId.value)

const form = reactive({
  slug: '',
  startsAt: '', endsAt: '', registrationDeadlineAt: '',
  unlimited: true, capacity: 30,
  isPaidMembersOnly: false,
  venueId: '',
  status: 'draft' as FanEventStatus,
  nameZh: '', nameEn: '', descZh: '', descEn: '', locZh: '', locEn: '',
  articleIds: [] as string[],
})
const baselineJson = ref('')
const coverFile = ref<File | null>(null)
const removeCover = ref(false)
const coverUrl = ref<string | null>(null)
const hasCover = ref(false)
const images = ref<FanEventDetailDto['images']>([])
const articleSeed = ref<{ id: string; label: string; status?: string }[]>([])
const venues = ref<AdminVenueListItemDto[]>([])
const counts = reactive({ registered: 0, waitlist: 0 })

const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
const formError = ref<string | null>(null)
const readOnly = computed(() => (isCreate.value ? !canCreate.value : !canUpdate.value))
const pageTitle = computed(() => (isCreate.value ? '新增活動' : `編輯：${form.nameZh || '（未命名）'}`))
const tab = ref<'info' | 'registrations'>('info')

function apply(d: FanEventDetailDto) {
  form.slug = d.slug ?? ''
  form.startsAt = utcToLocalInput(d.startsAt)
  form.endsAt = utcToLocalInput(d.endsAt)
  form.registrationDeadlineAt = utcToLocalInput(d.registrationDeadlineAt)
  form.unlimited = d.capacity === null || d.capacity === undefined
  form.capacity = d.capacity ?? 30
  form.isPaidMembersOnly = d.isPaidMembersOnly
  form.venueId = d.venueId ?? ''
  form.status = d.status
  form.nameZh = d.zh?.name ?? ''
  form.nameEn = d.en?.name ?? ''
  form.descZh = d.zh?.description ?? ''
  form.descEn = d.en?.description ?? ''
  form.locZh = d.zh?.location ?? ''
  form.locEn = d.en?.location ?? ''
  form.articleIds = d.articles.map((a) => a.id)
  articleSeed.value = d.articles.map((a) => ({ id: a.id, label: a.titleZh || a.slug, status: a.status }))
  coverUrl.value = d.coverUrl ?? d.coverThumbUrl ?? null
  hasCover.value = !!d.coverKey
  images.value = d.images ?? []
  counts.registered = d.registeredCount
  counts.waitlist = d.waitlistCount
}

async function load() {
  loadState.value = 'loading'
  try {
    venues.value = await listAdminVenues(club.value).catch(() => [] as AdminVenueListItemDto[])
    if (!isCreate.value && eventId.value) apply(await getFanEvent(club.value, eventId.value))
    coverFile.value = null
    removeCover.value = false
    baselineJson.value = JSON.stringify(form)
    loadState.value = 'ready'
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'not-found') loadState.value = 'not-found'
    else {
      loadErrorMessage.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
      loadState.value = 'error'
    }
  }
}
onMounted(load)
watch(club, (_, old) => {
  // 切換俱樂部：編輯中的活動屬於原本的俱樂部，回列表
  if (old && !isCreate.value) router.push('/culture/fan-events')
})

const isDirty = computed(() => loadState.value === 'ready' && (JSON.stringify(form) !== baselineJson.value || !!coverFile.value || removeCover.value))
useUnsavedChanges(isDirty)

function validate(): string | null {
  if (!form.nameZh.trim()) return '請輸入中文活動名稱'
  if (form.status === 'published' && !form.startsAt) return '發布前必須填寫開始時間'
  const start = form.startsAt ? new Date(form.startsAt).getTime() : null
  const end = form.endsAt ? new Date(form.endsAt).getTime() : null
  const deadline = form.registrationDeadlineAt ? new Date(form.registrationDeadlineAt).getTime() : null
  if (start !== null && end !== null && end < start) return '結束時間不能早於開始時間'
  if (start !== null && deadline !== null && deadline > start) return '報名截止時間不能晚於開始時間'
  if (!form.unlimited && (!Number.isInteger(form.capacity) || form.capacity < 1)) return '名額必須是 1 以上的整數'
  if (!form.unlimited && form.capacity < counts.registered) return `名額不可低於目前已報名人數（${counts.registered} 人）`
  return null
}

async function handleSave() {
  if (readOnly.value) return
  formError.value = validate()
  if (formError.value) return
  saving.value = true
  const payload = {
    slug: nullIfBlank(form.slug) ?? undefined,
    startsAt: localInputToUtc(form.startsAt),
    endsAt: localInputToUtc(form.endsAt),
    registrationDeadlineAt: localInputToUtc(form.registrationDeadlineAt),
    capacity: form.unlimited ? null : form.capacity,
    isPaidMembersOnly: form.isPaidMembersOnly,
    venueId: form.venueId || null,
    status: form.status,
    removeCover: coverFile.value ? false : removeCover.value,
    // 關聯報導：一律明確帶出畫面上的完整清單（空陣列＝清空）
    articleIds: form.articleIds,
    content: {
      zh: { name: form.nameZh.trim(), description: nullIfBlank(form.descZh), location: nullIfBlank(form.locZh) },
      en: enOrUndefined({ name: form.nameEn.trim(), description: nullIfBlank(form.descEn) as string, location: nullIfBlank(form.locEn) as string }, 'name', 'description', 'location'),
    },
  }
  try {
    const wasCreate = isCreate.value
    const saved = wasCreate
      ? await createFanEvent(club.value, payload, coverFile.value)
      : await updateFanEvent(club.value, eventId.value!, payload, coverFile.value)
    if (wasCreate) {
      eventId.value = saved.id
      router.replace(`/culture/fan-events/${saved.id}/edit`)
    }
    apply(saved)
    coverFile.value = null
    removeCover.value = false
    baselineJson.value = JSON.stringify(form)
    ElMessage.success(wasCreate ? '已建立，接著可以加入活動圖集與查看報名' : '已儲存')
  } catch (error) {
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

function back() {
  router.push('/culture/fan-events')
}

async function refreshDetail() {
  if (!eventId.value) return
  const d = await getFanEvent(club.value, eventId.value)
  images.value = d.images ?? []
  counts.registered = d.registeredCount
  counts.waitlist = d.waitlistCount
}
async function galleryUpload(file: File) {
  await addFanEventImages(club.value, eventId.value!, [file])
  await refreshDetail()
}
async function galleryRemove(id: string) {
  await deleteFanEventImage(club.value, eventId.value!, id)
  await refreshDetail()
}
async function galleryReorder(ids: string[]) {
  await reorderFanEventImages(club.value, eventId.value!, ids)
  await refreshDetail()
}
</script>

<template>
  <div class="event-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="back"><el-icon><ArrowLeft /></el-icon>返回列表</el-button>
      </template>
      <template #meta><FrontendUnitBanner module-code="F2" /></template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這場活動，可能已被刪除，或不屬於目前選擇的俱樂部。</p>
          <p v-else>{{ loadErrorMessage }}</p>
        </template>
        <el-button v-if="loadState === 'error'" type="primary" @click="load">重新載入</el-button>
        <el-button v-else type="primary" @click="back">返回列表</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="event-edit__block" @close="formError = null" />
      <el-alert v-if="readOnly" title="你的帳號只能檢視球迷會活動，不能修改。" type="info" show-icon :closable="false" class="event-edit__block" />

      <el-tabs v-model="tab">
        <el-tab-pane label="活動資料" name="info">
          <el-form label-position="top" :disabled="readOnly">
            <el-card shadow="never" header="基本資料" class="event-edit__block">
              <BilingualShortField label="活動名稱" :zh="form.nameZh" :en="form.nameEn" required @update:zh="(v) => (form.nameZh = v)" @update:en="(v) => (form.nameEn = v)" />
              <BilingualTextareaField label="活動介紹" :zh="form.descZh" :en="form.descEn" :rows="4" @update:zh="(v) => (form.descZh = v)" @update:en="(v) => (form.descEn = v)" />
              <BilingualShortField label="活動地點" :zh="form.locZh" :en="form.locEn" @update:zh="(v) => (form.locZh = v)" @update:en="(v) => (form.locEn = v)" />
              <el-form-item label="場地（選填）">
                <el-select v-model="form.venueId" clearable filterable placeholder="不指定場地資料" style="width: 100%">
                  <el-option v-for="v in venues" :key="v.id" :label="v.nameZh" :value="v.id" />
                </el-select>
              </el-form-item>
              <el-form-item label="網址名稱（選填）">
                <el-input v-model="form.slug" maxlength="128" placeholder="留空由系統自動產生" />
              </el-form-item>
            </el-card>

            <el-card shadow="never" class="event-edit__block">
              <template #header>
                <span>時間、名額與狀態</span>
                <span v-if="!isCreate" class="event-edit__stat">已報名 {{ counts.registered }} 人・候補 {{ counts.waitlist }} 人</span>
              </template>
              <el-row :gutter="12">
                <el-col :xs="24" :sm="8"><el-form-item label="開始時間（台灣時間）"><el-date-picker v-model="form.startsAt" type="datetime" value-format="YYYY-MM-DDTHH:mm:ss" style="width: 100%" /></el-form-item></el-col>
                <el-col :xs="24" :sm="8"><el-form-item label="結束時間"><el-date-picker v-model="form.endsAt" type="datetime" value-format="YYYY-MM-DDTHH:mm:ss" style="width: 100%" /></el-form-item></el-col>
                <el-col :xs="24" :sm="8"><el-form-item label="報名截止時間"><el-date-picker v-model="form.registrationDeadlineAt" type="datetime" value-format="YYYY-MM-DDTHH:mm:ss" style="width: 100%" /></el-form-item></el-col>
              </el-row>
              <el-row :gutter="12">
                <el-col :xs="24" :sm="12">
                  <el-form-item label="名額">
                    <div class="event-edit__capacity">
                      <el-switch v-model="form.unlimited" active-text="不限名額" />
                      <el-input-number v-if="!form.unlimited" v-model="form.capacity" :min="1" :step="1" />
                    </div>
                  </el-form-item>
                </el-col>
                <el-col :xs="24" :sm="12"><el-form-item label="報名資格"><el-switch v-model="form.isPaidMembersOnly" active-text="限付費會員報名" /></el-form-item></el-col>
              </el-row>
              <el-form-item label="狀態">
                <el-radio-group v-model="form.status">
                  <el-radio-button value="draft">草稿</el-radio-button>
                  <el-radio-button value="published">已發布</el-radio-button>
                </el-radio-group>
              </el-form-item>
              <p class="event-edit__hint">發布必須填寫開始時間。名額不可低於已報名人數；已有報名紀錄的活動不能刪除，請改為草稿。</p>
            </el-card>

            <el-card shadow="never" header="封面圖片" class="event-edit__block">
              <ImageUploader v-model:file="coverFile" v-model:remove-cover="removeCover" :min-width="0" :min-height="0" :has-existing-image="hasCover" :existing-preview-url="coverUrl" :disabled="saving || readOnly" />
            </el-card>

            <el-card shadow="never" header="活動回顧的關聯報導" class="event-edit__block">
              <NewsPicker v-model="form.articleIds" :seed="articleSeed" :disabled="readOnly" />
              <p class="event-edit__hint">前台只顯示已發布的報導。</p>
            </el-card>
          </el-form>

          <el-card shadow="never" header="活動回顧圖集" class="event-edit__block">
            <p v-if="isCreate" class="event-edit__hint">請先按「儲存」建立活動，儲存後就能加入圖集。</p>
            <GalleryManager v-else :images="images.map((i) => ({ id: i.id, thumbUrl: i.imageThumbUrl, imageUrl: i.imageUrl }))" :disabled="!canUpdate" :on-upload="galleryUpload" :on-remove="galleryRemove" :on-reorder="galleryReorder" />
          </el-card>
        </el-tab-pane>

        <el-tab-pane label="報名名單" name="registrations" lazy>
          <el-card shadow="never">
            <p v-if="isCreate" class="event-edit__hint">請先儲存活動，儲存後就能查看與管理報名名單。</p>
            <FanEventRegistrations v-else :event-id="eventId!" :is-paid-members-only="form.isPaidMembersOnly" @changed="refreshDetail" />
          </el-card>
        </el-tab-pane>
      </el-tabs>
      <EditActionBar v-if="!readOnly && tab === 'info'"><el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button></EditActionBar>
    </template>
  </div>
</template>

<style scoped>
.event-edit { max-width: 900px; margin: 0 auto 88px; }
.event-edit__block { margin-bottom: 16px; }
.event-edit__hint { margin: 4px 0 10px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.event-edit__stat { margin-left: 12px; font-size: 12px; color: var(--admin-text-tertiary); }
.event-edit__capacity { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; }
</style>

<script setup lang="ts">
/**
 * 新增／編輯推播（只有草稿能修改；待覆核的要先退回）。
 * 🔴 文案須雙語：英文缺漏時，使用英文的裝置會收到中文。
 * 🔴 分眾只用會籍層級、追蹤球隊與俱樂部歸屬（不做行為定向）；送審前可以先試算預估人數（只算「權杖有效且已允許推播」的裝置）。
 * 🔴 標題或內文出現中獎、得獎等字樣，或連結指向抽獎公布文章，送出時系統會擋下。
 * 🔴 預定發送時間是台灣時間，不能是過去；沒填就是核可後立刻發送。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { usePermission } from '@/composables/useCrudPermissions'
import { availableClubs } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { nullIfBlank } from '@/api/adminCommon'
import { listAdminClubTeams } from '@/api/adminTeams'
import {
  createPushMessage, estimatePush, getPushMessage, listDeepLinks, updatePushMessage,
  type AudienceTier, type DeepLinkDto, type PushDetailDto, type PushEstimateDto,
} from '@/api/adminApp'
import { nowAsPickerDate, pickerDateToUtc, utcToPickerDate } from '@/utils/dateTime'
import { AUDIENCE_TIERS } from './pushLabels'

const route = useRoute()
const router = useRouter()
const canCreate = usePermission('app.push.create')
// 🔴 必須是 computed：建立成功後 router.replace 不會重新掛載這個元件（同 FanEventEditView 的說明）
const isCreate = computed(() => route.name === 'app-push-new')
const pushId = ref<string | undefined>(route.params.id as string | undefined)

const form = reactive({
  titleZh: '', titleEn: '', bodyZh: '', bodyEn: '', imageAltZh: '', imageAltEn: '', deepLink: '',
  audienceTier: 'all' as AudienceTier, audienceClubCode: '', teamCodes: [] as string[], scheduledAt: null as Date | null,
})
const image = ref<File | null>(null)
const removeImage = ref(false)
const hasImage = ref(false)
const imageUrl = ref<string | null>(null)
const baseline = ref('')
const state = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadError = ref('')
const saving = ref(false)
const formError = ref<string | null>(null)
const status = ref('draft')
const deepLinks = ref<DeepLinkDto[]>([])
const teamOptions = ref<{ code: string; label: string }[]>([])
const readOnly = computed(() => !canCreate.value || status.value !== 'draft')
const pageTitle = computed(() => (isCreate.value ? '新增推播' : `編輯推播：${form.titleZh || '（未填標題）'}`))

function apply(d: PushDetailDto) {
  status.value = d.status
  Object.assign(form, {
    titleZh: d.content.zh.title ?? '', titleEn: d.content.en?.title ?? '', bodyZh: d.content.zh.body ?? '', bodyEn: d.content.en?.body ?? '',
    imageAltZh: d.content.zh.imageAlt ?? '', imageAltEn: d.content.en?.imageAlt ?? '', deepLink: d.deepLink ?? '',
    audienceTier: d.audienceTier, audienceClubCode: d.audienceClubCode ?? '', teamCodes: [...d.audienceTeamCodes], scheduledAt: utcToPickerDate(d.scheduledAt),
  })
  hasImage.value = !!d.imageKey
  imageUrl.value = d.imageUrl
}
async function load() {
  state.value = 'loading'
  try {
    deepLinks.value = await listDeepLinks().catch(() => [])
    const teams = await Promise.all(availableClubs.value.map((c) => listAdminClubTeams(c.code).then((ts) => ts.map((t) => ({ code: t.code, label: `${c.name}・${t.nameZh || t.code}` }))).catch(() => [])))
    teamOptions.value = teams.flat()
    if (!isCreate.value && pushId.value) apply(await getPushMessage(pushId.value))
    image.value = null
    removeImage.value = false
    baseline.value = JSON.stringify(form)
    state.value = 'ready'
  } catch (e) {
    if (e instanceof AdminApiError && e.kind === 'not-found') state.value = 'not-found'
    else {
      loadError.value = e instanceof AdminApiError ? e.message : '推播資料載入失敗，請稍後再試'
      state.value = 'error'
    }
  }
}
onMounted(load)

const isDirty = computed(() => state.value === 'ready' && (JSON.stringify(form) !== baseline.value || !!image.value || removeImage.value))
useUnsavedChanges(isDirty)

// ── 預估觸及 ──
const estimate = ref<PushEstimateDto | null>(null)
const estimating = ref(false)
const estimateError = ref<string | null>(null)
watch(() => [form.audienceTier, form.audienceClubCode, form.teamCodes.join(',')], () => (estimate.value = null))
async function doEstimate() {
  estimating.value = true
  estimateError.value = null
  try {
    estimate.value = await estimatePush({ audienceTier: form.audienceTier, audienceClubCode: form.audienceClubCode || null, audienceTeamCodes: form.teamCodes })
  } catch (e) {
    estimate.value = null
    estimateError.value = e instanceof AdminApiError ? e.message : '試算失敗，請稍後再試'
  } finally {
    estimating.value = false
  }
}

function validate(): string | null {
  if (!form.titleZh.trim()) return '請輸入中文標題'
  if (!form.bodyZh.trim()) return '請輸入中文內文'
  if (form.titleEn.trim() && !form.bodyEn.trim()) return '有英文標題時請一併填寫英文內文（或清空英文標題）'
  const link = form.deepLink.trim()
  if (link && !/^(tcrfc:\/\/|https?:\/\/)/i.test(link)) return '連結格式不正確：請填網址（https:// 開頭）或 App 內頁面連結'
  if (form.scheduledAt && form.scheduledAt.getTime() <= nowAsPickerDate().getTime()) return '預定發送時間不能是過去，沒填就是核可後立刻發送'
  return null
}
async function save() {
  if (readOnly.value) return
  formError.value = validate()
  if (formError.value) return
  saving.value = true
  const hasEn = form.titleEn.trim() || form.bodyEn.trim()
  const payload = {
    deepLink: nullIfBlank(form.deepLink),
    audienceTier: form.audienceTier,
    audienceClubCode: form.audienceClubCode || null,
    audienceTeamCodes: form.teamCodes,
    scheduledAt: pickerDateToUtc(form.scheduledAt),
    removeImage: image.value ? undefined : removeImage.value || undefined,
    content: {
      zh: { title: form.titleZh.trim(), body: form.bodyZh.trim(), imageAlt: nullIfBlank(form.imageAltZh) },
      en: hasEn ? { title: form.titleEn.trim(), body: form.bodyEn.trim(), imageAlt: nullIfBlank(form.imageAltEn) } : undefined,
    },
  }
  try {
    if (isCreate.value) {
      const created = await createPushMessage(payload, image.value)
      ElMessage.success('已建立草稿')
      pushId.value = created.id
      apply(created)
      image.value = null
      removeImage.value = false
      baseline.value = JSON.stringify(form)
      await router.replace(`/app/push/${created.id}`)
    } else {
      const updated = await updatePushMessage(pushId.value!, payload, image.value)
      ElMessage.success('已儲存')
      apply(updated)
      image.value = null
      removeImage.value = false
      baseline.value = JSON.stringify(form)
    }
  } catch (e) {
    formError.value = e instanceof AdminApiError ? e.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}
const back = () => router.push(isCreate.value || !pushId.value ? '/app/push' : `/app/push/${pushId.value}`)
</script>

<template>
  <div class="pe">
    <PageHeader :title="pageTitle">
      <template #back><el-button text @click="back">← 返回</el-button></template>
      <template #meta><FrontendUnitBanner module-code="M3" /></template>
    </PageHeader>

    <el-card v-if="state === 'loading'" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="state === 'not-found'" shadow="never"><el-empty description="找不到這則推播"><el-button type="primary" @click="router.push('/app/push')">回推播清單</el-button></el-empty></el-card>
    <el-card v-else-if="state === 'error'" shadow="never"><el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty></el-card>

    <template v-else>
      <el-alert v-if="formError" type="error" show-icon :closable="false" :title="formError" class="pe__block" />
      <el-alert v-if="!canCreate" type="info" show-icon :closable="false" title="你的帳號沒有建立推播的權限" class="pe__block" />
      <el-alert v-else-if="status !== 'draft'" type="info" show-icon :closable="false" title="只有草稿能修改。待覆核的推播請先退回，再回來修改。" class="pe__block" />
      <el-alert type="warning" show-icon :closable="false" class="pe__block" title="推播不能用來公布中獎名單：標題或內文出現「中獎」「得獎」「獲獎」「抽中」等字樣，或連結指向抽獎公布文章，系統會擋下。" />

      <el-form label-position="top" :disabled="readOnly">
        <el-card shadow="never" header="推播內容" class="pe__block">
          <BilingualShortField v-model:zh="form.titleZh" v-model:en="form.titleEn" label="標題（120 字內）" required />
          <BilingualTextareaField v-model:zh="form.bodyZh" v-model:en="form.bodyEn" label="內文（500 字內）" required :rows="4" />
          <el-form-item label="推播圖片（選填）">
            <ImageUploader v-model:file="image" v-model:remove-cover="removeImage" :has-existing-image="hasImage" :existing-preview-url="imageUrl" :min-width="1" :min-height="1" :disabled="readOnly || saving" />
          </el-form-item>
          <BilingualShortField v-model:zh="form.imageAltZh" v-model:en="form.imageAltEn" label="圖片替代文字" />
          <el-form-item label="點擊後前往（選填）">
            <el-select v-model="form.deepLink" filterable allow-create clearable default-first-option placeholder="選擇 App 內頁面，或自行輸入連結" style="width: 100%">
              <el-option v-for="d in deepLinks.filter((x) => x.isActive)" :key="d.id" :label="d.labelZh || d.code" :value="d.appLink" />
            </el-select>
            <div class="pe__hint">可以填網頁網址（https:// 開頭），或 App 內頁面連結（不確定時請向 App 工程團隊確認）。</div>
          </el-form-item>
        </el-card>

        <el-card shadow="never" header="發送對象與時間" class="pe__block">
          <el-row :gutter="12">
            <el-col :xs="24" :sm="12"><el-form-item label="會籍層級"><el-select v-model="form.audienceTier" style="width: 100%"><el-option v-for="t in AUDIENCE_TIERS" :key="t.value" :label="t.label" :value="t.value" /></el-select></el-form-item></el-col>
            <el-col :xs="24" :sm="12"><el-form-item label="限定俱樂部（選填，用於一般公告）"><el-select v-model="form.audienceClubCode" clearable placeholder="不限" style="width: 100%"><el-option v-for="c in availableClubs" :key="c.code" :label="c.name" :value="c.code" /></el-select></el-form-item></el-col>
            <el-col :xs="24"><el-form-item label="追蹤球隊（選填，只算有開啟該隊推播的裝置）"><el-select v-model="form.teamCodes" multiple filterable clearable placeholder="不限" style="width: 100%"><el-option v-for="t in teamOptions" :key="t.code" :label="t.label" :value="t.code" /></el-select></el-form-item></el-col>
            <el-col :xs="24" :sm="12"><el-form-item label="預定發送時間（台灣時間，選填）"><el-date-picker v-model="form.scheduledAt" type="datetime" placeholder="沒填＝核可後立刻發送" style="width: 100%" /></el-form-item></el-col>
          </el-row>
          <div class="pe__estimate">
            <el-button :loading="estimating" @click="doEstimate">試算預估人數</el-button>
            <span v-if="estimate" class="pe__num">預估可觸及 {{ estimate.total.toLocaleString() }} 台裝置</span>
            <span v-if="estimateError" class="pe__err">{{ estimateError }}</span>
          </div>
          <el-table v-if="estimate && estimate.breakdown.length > 0" :data="estimate.breakdown" size="small" class="pe__table">
            <el-table-column prop="platformLabel" label="平台" width="110" />
            <el-table-column prop="localeLabel" label="語言" width="100" />
            <el-table-column label="裝置數"><template #default="{ row }">{{ row.sent.toLocaleString() }}</template></el-table-column>
          </el-table>
          <p class="pe__hint">試算只算「推播權杖有效且已允許推播」的裝置，不會列出任何裝置清單。分眾不使用第三方的主題訂閱，付費狀態不會送進推播服務商。</p>
        </el-card>
      </el-form>
    </template>

    <EditActionBar v-if="state === 'ready'">
      <el-button @click="back">取消</el-button>
      <el-button v-if="!readOnly" type="primary" :loading="saving" @click="save">儲存草稿</el-button>
    </EditActionBar>
  </div>
</template>

<style scoped>
.pe { min-width: 0; padding-bottom: 72px; }
.pe__block { margin-bottom: 12px; }
.pe__hint { margin-top: 4px; font-size: 12px; line-height: 1.6; color: var(--admin-text-tertiary); }
.pe__estimate { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; }
.pe__num { font-weight: 600; }
.pe__err { color: var(--el-color-danger); font-size: 13px; }
.pe__table { margin-top: 10px; max-width: 420px; }
</style>

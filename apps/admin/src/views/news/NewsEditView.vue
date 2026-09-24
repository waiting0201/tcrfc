<script setup lang="ts">
import { computed, reactive, ref, shallowRef } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import StatusTag from '@/components/StatusTag.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { activeClubId } from '@/auth/clubAccess'
import {
  articleToSavePayload,
  createAdminNews,
  detailDtoToArticle,
  fetchTagSuggestions,
  getAdminNewsById,
  publishAdminNews,
  scheduleAdminNews,
  updateAdminNews,
} from '@/api/adminNews'
import {
  listMatchRelationOptions,
  listPlayerRelationOptions,
  listTeamRelationOptions,
  type RelationTargetOption,
} from '@/api/adminRelationTargets'
import { AdminApiError } from '@/api/http'
import {
  CORE_VALUE_TAG_LABEL,
  CORE_VALUE_TAG_ORDER,
  NEWS_CATEGORY_LABEL,
  RELATION_TARGET_TYPE_LABEL,
  RELATION_TARGET_TYPE_ORDER,
  RELATION_TARGET_TYPES_AVAILABLE,
  type CoreValueTag,
  type NewsArticle,
  type NewsCategory,
  type NewsRelation,
  type NewsTag,
  type RelationTargetType,
} from '@/types/news'

const route = useRoute()
const router = useRouter()

const isCreate = route.name === 'news-new'
const paramId = route.params.id as string | undefined
/** 建立成功之後，接下來的寫入呼叫要用到的文章 id（建立前為 undefined）。
 * 不直接依賴路由參數，因為建立成功後用 router.replace() 換網址，元件不會重新掛載。 */
const currentId = ref<string | undefined>(paramId)

function emptyArticle(): NewsArticle {
  return {
    id: '',
    title: { zh: '', en: '' },
    urlName: '',
    category: 'club',
    coverImageUrl: null,
    coverKey: null,
    isFeatured: false,
    status: 'draft',
    isSharedContent: false,
    updatedAt: '',
    content: { zh: '', en: '' },
    summary: { zh: '', en: '' },
    seoTitle: { zh: '', en: '' },
    seoDescription: { zh: '', en: '' },
    tags: [],
    coreValueTags: [],
    relations: [],
    viewCount: 0,
  }
}

type LoadState = 'loading' | 'not-found' | 'error' | 'ready'
const loadState = ref<LoadState>('loading')
const loadErrorMessage = ref('')

const baseline = shallowRef<NewsArticle>(emptyArticle())
const form = reactive<NewsArticle>(emptyArticle())

/**
 * 封面圖片的「這次瀏覽階段的意圖」——刻意不放進 `form`（`NewsArticle` 沒有 `File` 這種欄位，
 * 也不該有，`coverKey` 只在伺服器端寫入成功後才會有新值，見 `articleToSavePayload` 的說明）。
 * `coverFile` 非 `null` 時是「選了要換的新圖」，`removeCover` 為真時是「儲存時清空封面圖片」，
 * 兩者不會同時成立（`ImageUploader.vue` 選新檔案時會連帶取消 `removeCover`）。兩者都要在
 * 載入資料與存檔成功後重置，見 `applyLoadedArticle`，這是「離開或取消表單不留下任何檔案」的
 * 落地方式之一：這兩個值只是本機記憶體狀態，從來沒有被送出過就被捨棄，不留下任何痕跡。
 */
const coverFile = ref<File | null>(null)
const removeCover = ref(false)

function applyLoadedArticle(article: NewsArticle) {
  baseline.value = article
  Object.assign(form, structuredClone(article))
  currentId.value = article.id
  coverFile.value = null
  removeCover.value = false
}

// ── 標籤（S1-5）──────────────────────────────────────────────────────────────────

/** 既有標籤建議清單（自動完成用），來源見 `fetchTagSuggestions` 的說明——不是新端點，
 * 是彙整這個俱樂部所有文章目前掛的標籤。載入失敗不影響編輯頁其餘功能，靜默留空即可
 * （使用者仍然可以直接打字新增標籤，只是少了既有標籤的自動完成建議）。 */
const tagSuggestions = ref<NewsTag[]>([])

async function loadTagSuggestions() {
  try {
    tagSuggestions.value = await fetchTagSuggestions(activeClubId.value)
  } catch {
    tagSuggestions.value = []
  }
}

/** 給 el-select 顯示用的名稱（既有標籤顯示中文名稱，缺中文名稱時退而求其次顯示網址名稱）。 */
function tagDisplayName(tag: NewsTag): string {
  return tag.nameZh?.trim() || tag.slug
}

/**
 * 把使用者打的中文字轉成網址安全的識別碼（`tags.slug` 的格式：小寫英文字母、數字、連字號）。
 * 純中文輸入轉不出任何英數字時，退而求其次用一段隨機英數字串頂著——使用者從頭到尾只看得到、
 * 只打得出中文名稱，這串字只是資料庫欄位需要的識別碼，畫面上不會顯示（見 `tagDisplayName`）。
 * 對照 `apps/api` 的 `Features/AdminNews/TagSlugFormat.cs`。
 */
function slugifyTagName(input: string): string {
  const base = input
    .trim()
    .toLowerCase()
    .normalize('NFKD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
  return base || `tag-${Math.random().toString(36).slice(2, 8)}`
}

/** 把使用者在多選框裡打的一段文字，解析成「沿用既有標籤」或「新標籤」。找既有標籤時同時看
 * 建議清單與目前表單上已經有的標籤（避免建議清單載入失敗、或標籤是這次瀏覽階段才剛加上的
 * 情況找不到）。找到既有標籤時**忽略**這次輸入的名稱，比照後端「名稱由標籤自己管理」的規則
 * （apps/api/README.md「我的判斷」第 5 點），避免同一個標籤的顯示名稱被不同文章各自覆寫。 */
function resolveTagInput(name: string): NewsTag {
  const trimmed = name.trim()
  const existing = [...tagSuggestions.value, ...form.tags].find((t) => tagDisplayName(t) === trimmed)
  if (existing) return { slug: existing.slug, nameZh: existing.nameZh, nameEn: existing.nameEn }
  return { slug: slugifyTagName(trimmed), nameZh: trimmed }
}

const tagNames = computed<string[]>({
  get: () => form.tags.map(tagDisplayName),
  set: (names) => {
    form.tags = names.map((name) => resolveTagInput(name))
  },
})

const tagSuggestionNames = computed(() => tagSuggestions.value.map(tagDisplayName))

// ── 核心價值標籤（S1-5）───────────────────────────────────────────────────────────

function toggleCoreValueTag(tag: CoreValueTag, checked: boolean) {
  if (checked) {
    if (!form.coreValueTags.includes(tag)) form.coreValueTags.push(tag)
  } else {
    form.coreValueTags = form.coreValueTags.filter((t) => t !== tag)
  }
}

// ── 關聯（S1-5）───────────────────────────────────────────────────────────────────
// ⚠️ 只有「球員」「賽事」兩種真的能選，見 @/types/news 的 RELATION_TARGET_TYPES_AVAILABLE
// 檔頭說明（球隊／課程／夥伴目前沒有這個帳號打得到的唯讀清單）。

const relationOptionsCache = reactive<Partial<Record<RelationTargetType, RelationTargetOption[]>>>({})
const relationOptionsLoading = ref(false)
const pendingRelationType = ref<RelationTargetType>('player')
const pendingRelationTargetId = ref<string | null>(null)

async function ensureRelationOptionsLoaded(type: RelationTargetType) {
  if (!RELATION_TARGET_TYPES_AVAILABLE.includes(type) || relationOptionsCache[type]) return
  relationOptionsLoading.value = true
  try {
    const club = activeClubId.value
    if (type === 'player') {
      relationOptionsCache[type] = await listPlayerRelationOptions(club)
    } else if (type === 'team') {
      relationOptionsCache[type] = await listTeamRelationOptions(club)
    } else {
      relationOptionsCache[type] = await listMatchRelationOptions(club)
    }
  } catch {
    relationOptionsCache[type] = []
    ElMessage.error('讀取清單失敗，請稍後再試')
  } finally {
    relationOptionsLoading.value = false
  }
}

const currentRelationOptions = computed(() => relationOptionsCache[pendingRelationType.value] ?? [])

function onRelationTypeChange(type: RelationTargetType) {
  pendingRelationTargetId.value = null
  ensureRelationOptionsLoaded(type)
}

function addRelation() {
  if (!pendingRelationTargetId.value) return
  const targetType = pendingRelationType.value
  const targetId = pendingRelationTargetId.value
  if (form.relations.some((r) => r.targetType === targetType && r.targetId === targetId)) {
    ElMessage.warning('這筆關聯已經加過了')
    return
  }
  const option = currentRelationOptions.value.find((o) => o.id === targetId)
  form.relations.push({ targetType, targetId, targetLabel: option?.label })
  pendingRelationTargetId.value = null
}

function removeRelation(index: number) {
  form.relations.splice(index, 1)
}

/** 顯示一筆關聯的名稱。優先用剛剛加入時記下的 `targetLabel`；不是本次瀏覽階段加入的（例如
 * 剛從伺服器載入既有文章），改從已載入的選項清單依 `targetId` 反查；兩者都找不到（清單還沒
 * 載入，或這種目標類型目前根本沒有清單可查）就老實顯示「尚無法顯示名稱」，不假裝有名稱。 */
function resolveRelationLabel(relation: NewsRelation): string {
  if (relation.targetLabel) return relation.targetLabel
  const options = relationOptionsCache[relation.targetType]
  const found = options?.find((o) => o.id === relation.targetId)
  if (found) return found.label
  return RELATION_TARGET_TYPES_AVAILABLE.includes(relation.targetType)
    ? '（讀取中或找不到，可能已被刪除）'
    : '（此類型目前尚無法顯示名稱）'
}

async function loadArticle() {
  loadState.value = 'loading'
  const club = activeClubId.value
  loadTagSuggestions()
  // 選擇器預設就停在「球員」這個類型，但 el-select 的 @change 只在使用者真的切換型別時才會
  // 觸發——不主動預先載入一次，新增文章時第一次打開球員選單會是空的，要先切成別的類型再切
  // 回來才會有資料，這是使用者根本不會做的操作。兩種模式（建立／編輯）都需要這行。
  ensureRelationOptionsLoaded(pendingRelationType.value)
  if (isCreate) {
    loadState.value = 'ready'
    return
  }
  try {
    const detail = await getAdminNewsById(club, currentId.value!)
    const article = detailDtoToArticle(detail)
    applyLoadedArticle(article)
    loadState.value = 'ready'
    // 既有關聯用到哪幾種可查詢的類型，先把清單載回來，畫面上才顯示得出名稱而不是一片空白。
    const typesInUse = new Set(article.relations.map((r) => r.targetType))
    for (const type of typesInUse) {
      if (RELATION_TARGET_TYPES_AVAILABLE.includes(type)) ensureRelationOptionsLoaded(type)
    }
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'not-found') {
      loadState.value = 'not-found'
    } else {
      loadErrorMessage.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
      loadState.value = 'error'
    }
  }
}

loadArticle()

const saving = ref(false)
const slugError = ref<string | null>(null)
const formError = ref<string | null>(null)
const scheduleDialogVisible = ref(false)
const scheduleDateTime = ref<Date | null>(null)

// 選了新封面圖片或標記清空封面，即使其餘欄位都沒變，也算未儲存的變更——不然「選了圖片但沒按
// 儲存就離開」不會觸發離開提醒，使用者會誤以為圖片已經生效。
const isDirty = computed(() =>
  loadState.value === 'ready'
  && (JSON.stringify(form) !== JSON.stringify(baseline.value) || coverFile.value !== null || removeCover.value),
)
useUnsavedChanges(isDirty)

const pageTitle = computed(() => (isCreate ? '新增文章' : '編輯文章'))
// apps/api 的 /publish 同時接受 draft／scheduled 兩種起始狀態（見 apps/api/README.md「狀態轉換規則」），
// 所以草稿與排程中都應該能直接按「發布」立刻生效（排程中的文章常見的操作就是「其實想現在就發」）。
// 已發布的文章不重複提供這顆按鈕（原本互動就是這樣設計，避免跟「儲存變更」的語意混淆）。
const mainActionLabel = computed(() => (form.status === 'published' ? '儲存變更' : '發布'))
const canPreview = computed(() => form.status === 'published')
const previewUrl = computed(() => (canPreview.value ? `/zh/news/${form.urlName}/` : undefined))
const isReadOnly = computed(() => loadState.value === 'ready' && form.isSharedContent)

function isEnEmpty(article: NewsArticle): boolean {
  return (
    !article.title.en.trim()
    && !article.content.en.trim()
    && !article.summary.en.trim()
    && !article.seoTitle.en.trim()
    && !article.seoDescription.en.trim()
  )
}

/** 原本有英文內容、現在四個英文欄位全部清空了：儲存會真的把英文版本從資料庫刪掉（PUT 是整份
 * 取代語意），先跟使用者確認一次，不要讓這個動作在使用者沒意識到的情況下發生（任務指示）。 */
async function confirmEnglishRemovalIfNeeded(): Promise<boolean> {
  if (!isEnEmpty(baseline.value) && isEnEmpty(form)) {
    try {
      await ElMessageBox.confirm(
        '偵測到英文內容的所有欄位都被清空了。儲存後，這篇文章的英文版本會被移除（不是暫時留白，是整份刪除）。確定要繼續嗎？',
        '確認移除英文版本',
        { confirmButtonText: '確定移除', cancelButtonText: '取消，先不儲存', type: 'warning' },
      )
      return true
    } catch {
      return false
    }
  }
  return true
}

function validateBeforeSave(): boolean {
  slugError.value = null
  formError.value = null
  if (!form.title.zh.trim()) {
    formError.value = '請輸入中文標題'
    return false
  }
  if (!form.urlName.trim()) {
    formError.value = '請輸入網址名稱'
    return false
  }
  return true
}

async function reloadFromServerDiscardingLocalChanges() {
  await loadArticle()
}

async function handleSaveError(error: unknown) {
  if (!(error instanceof AdminApiError)) {
    ElMessage.error('儲存失敗，請稍後再試')
    return
  }
  switch (error.kind) {
    case 'slug-conflict':
      slugError.value = error.message
      break
    case 'validation':
      formError.value = error.message
      break
    case 'concurrency-conflict':
      try {
        await ElMessageBox.confirm(
          '這篇文章已經被其他人（或你自己開的另一個分頁）變更過，繼續儲存會覆蓋掉對方的修改，系統不允許這樣做。'
          + '你可以重新載入最新的內容（目前畫面上未儲存的修改會遺失），或是先留在這裡，自行把想保留的內容複製下來再手動處理。',
          '資料已被變更',
          { confirmButtonText: '重新載入最新內容', cancelButtonText: '留在這裡', type: 'warning' },
        )
        await reloadFromServerDiscardingLocalChanges()
      } catch {
        // 使用者選擇留在這裡，不動作。
      }
      break
    case 'forbidden':
      await ElMessageBox.alert(error.message, '沒有編輯權限', { confirmButtonText: '我知道了' })
      break
    case 'featured-limit':
      await ElMessageBox.alert(error.message, '置頂精選已達上限', { confirmButtonText: '我知道了' })
      break
    case 'status-conflict':
      await ElMessageBox.alert(error.message, '無法這樣操作', { confirmButtonText: '我知道了' })
      break
    case 'not-found':
      await ElMessageBox.alert('這篇文章已經找不到了，可能已被刪除。', '找不到這篇文章', { confirmButtonText: '返回列表' })
      router.push('/content/news')
      break
    case 'network':
      ElMessage.error(error.message)
      break
    default:
      ElMessage.error(error.message || '儲存失敗，請稍後再試')
  }
}

/**
 * 統一的儲存流程：先確認英文清空的意圖 → 驗證必填 → 建立或整份取代內容 → （選擇性）呼叫狀態轉換。
 * 建立與更新都可能改變 `updatedAt`（並行權杖），狀態轉換一律用「這次寫入拿回來的最新值」，
 * 不是畫面上舊的 baseline，避免自己把自己判定成並行衝突。
 */
async function saveAndMaybeTransition(transition?: { kind: 'publish' } | { kind: 'schedule'; publishAt: string }) {
  if (isReadOnly.value) return
  if (!validateBeforeSave()) return
  if (!(await confirmEnglishRemovalIfNeeded())) return

  saving.value = true
  try {
    const club = activeClubId.value
    const payload = articleToSavePayload(form)
    let saved: NewsArticle

    if (isCreate && !currentId.value) {
      // 建立沒有「清空封面」這個概念（根本還沒有既有封面可清），coverFile 有值就附上，沒有就是
      // 「這篇文章沒有封面圖片」（apps/api/README.md「給前端接的契約」）。
      const created = await createAdminNews(club, payload, coverFile.value)
      saved = detailDtoToArticle(created)
    } else {
      const updated = await updateAdminNews(
        club,
        currentId.value!,
        {
          ...payload,
          expectedUpdatedAt: baseline.value.updatedAt,
          removeCover: removeCover.value,
        },
        coverFile.value,
      )
      saved = detailDtoToArticle(updated)
    }

    if (transition?.kind === 'publish') {
      const published = await publishAdminNews(club, saved.id, saved.updatedAt)
      saved = detailDtoToArticle(published)
    } else if (transition?.kind === 'schedule') {
      const scheduled = await scheduleAdminNews(club, saved.id, saved.updatedAt, transition.publishAt)
      saved = detailDtoToArticle(scheduled)
    }

    const wasCreate = isCreate && !currentId.value
    applyLoadedArticle(saved)
    slugError.value = null
    formError.value = null

    ElMessage.success(transition?.kind === 'publish' ? '已發布' : transition?.kind === 'schedule' ? '已排程發布' : '已儲存')

    if (wasCreate) {
      router.replace(`/content/news/${saved.id}/edit`)
    }
  } catch (error) {
    await handleSaveError(error)
  } finally {
    saving.value = false
  }
}

function handleSaveDraft() {
  saveAndMaybeTransition()
}

function handleMainAction() {
  if (form.status === 'published') {
    saveAndMaybeTransition()
  } else {
    saveAndMaybeTransition({ kind: 'publish' })
  }
}

function openScheduleDialog() {
  scheduleDateTime.value = null
  scheduleDialogVisible.value = true
}

function confirmSchedule() {
  if (!scheduleDateTime.value) {
    ElMessage.warning('請選擇排程發布的日期與時間')
    return
  }
  if (scheduleDateTime.value.getTime() <= Date.now()) {
    ElMessage.warning('排程發布時間必須晚於現在')
    return
  }
  const publishAt = scheduleDateTime.value.toISOString()
  scheduleDialogVisible.value = false
  saveAndMaybeTransition({ kind: 'schedule', publishAt })
}

function handleBack() {
  router.push('/content/news')
}

function handlePreview() {
  if (!previewUrl.value) return
  window.open(previewUrl.value, '_blank', 'noopener')
}

function retryLoad() {
  loadArticle()
}
</script>

<template>
  <div class="news-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="handleBack">
          <el-icon><ArrowLeft /></el-icon>
          返回列表
        </el-button>
      </template>
      <template #meta>
        <FrontendUnitBanner
          module-code="B2"
          :record-published="loadState === 'ready' ? form.status === 'published' : undefined"
          :record-url="previewUrl"
        />
        <span v-if="loadState === 'ready'" class="news-edit__status-line">
          <StatusTag :status="form.status" :status-at="form.statusAt" />
          <span v-if="form.isSharedContent" class="news-edit__shared-note">
            <el-tag type="info" size="small">共用內容（唯讀）</el-tag>
            這是兩隊共用的內容，你的帳號僅能檢視
          </span>
        </span>
      </template>
    </PageHeader>

    <!-- 載入中：骨架，不是空白畫面（docs/21 §10） -->
    <el-card v-if="loadState === 'loading'" shadow="never">
      <el-skeleton :rows="8" animated />
    </el-card>

    <!-- 找不到這篇文章／查詢失敗：各自給明確說明，不得只顯示空白或籠統的錯誤（docs/21 §10） -->
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #image>
          <el-icon :size="48" color="var(--admin-text-tertiary)"><WarningFilled /></el-icon>
        </template>
        <template #description>
          <p v-if="loadState === 'not-found'" class="news-edit__state-text">
            找不到這篇文章，可能已經被刪除，或不屬於目前選擇的俱樂部。
          </p>
          <p v-else class="news-edit__state-text">{{ loadErrorMessage }}</p>
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
        class="news-edit__form-error"
        @close="formError = null"
      />

      <el-form label-position="top" class="news-edit__form" :disabled="isReadOnly">
        <el-card shadow="never" header="基本資訊" class="news-edit__section">
          <el-form-item label="分類" required>
            <el-select v-model="form.category" placeholder="請選擇分類" style="width: 240px; max-width: 100%">
              <el-option
                v-for="(label, value) in NEWS_CATEGORY_LABEL"
                :key="value"
                :label="label"
                :value="value as NewsCategory"
              />
            </el-select>
          </el-form-item>

          <BilingualShortField
            label="標題"
            :zh="form.title.zh"
            :en="form.title.en"
            required
            placeholder="請輸入標題"
            @update:zh="(v) => (form.title.zh = v)"
            @update:en="(v) => (form.title.en = v)"
          />

          <el-form-item label="網址名稱" required :error="slugError ?? undefined">
            <el-input
              v-model="form.urlName"
              placeholder="例如：tcrfc-vs-trencin-2026"
              @update:model-value="slugError = null"
            />
          </el-form-item>
        </el-card>

        <el-card shadow="never" header="內容" class="news-edit__section">
          <el-tabs class="news-edit__content-tabs">
            <el-tab-pane label="中文內容">
              <el-input
                v-model="form.content.zh"
                type="textarea"
                :rows="10"
                placeholder="請輸入中文內容（此畫面以文字框代替正式的富文本編輯器）"
              />
            </el-tab-pane>
            <el-tab-pane>
              <template #label>
                英文內容
                <el-tag v-if="!form.content.en.trim()" size="small" type="info">尚未翻譯</el-tag>
              </template>
              <el-input
                v-model="form.content.en"
                type="textarea"
                :rows="10"
                placeholder="Enter English content"
              />
            </el-tab-pane>
          </el-tabs>
        </el-card>

        <el-card shadow="never" header="封面圖片" class="news-edit__section">
          <el-form-item label="封面圖片">
            <ImageUploader
              v-model:file="coverFile"
              v-model:remove-cover="removeCover"
              :has-existing-image="!!form.coverKey"
              :existing-preview-url="form.coverImageUrl"
              :disabled="saving"
            />
          </el-form-item>
        </el-card>

        <el-card shadow="never" header="標籤" class="news-edit__section">
          <el-form-item label="標籤">
            <el-select
              v-model="tagNames"
              multiple
              filterable
              allow-create
              default-first-option
              placeholder="輸入名稱後按 Enter 新增，或從既有標籤選擇"
              style="width: 100%"
            >
              <el-option v-for="name in tagSuggestionNames" :key="name" :label="name" :value="name" />
            </el-select>
          </el-form-item>
          <p class="news-edit__hint">直接輸入文字按 Enter 就能新增標籤；輸入跟現有標籤相同的名稱會自動沿用同一個標籤，不會重複建立。</p>

          <el-form-item label="核心價值標籤" class="news-edit__corevalue">
            <div class="news-edit__corevalue-list">
              <el-checkbox
                v-for="tag in CORE_VALUE_TAG_ORDER"
                :key="tag"
                :model-value="form.coreValueTags.includes(tag)"
                @change="(checked: boolean) => toggleCoreValueTag(tag, checked)"
              >
                {{ CORE_VALUE_TAG_LABEL[tag] }}
              </el-checkbox>
            </div>
          </el-form-item>
        </el-card>

        <el-card shadow="never" header="關聯" class="news-edit__section">
          <p class="news-edit__hint">
            可以把這篇文章跟球員、球隊、賽事互相關聯。課程、夥伴這兩種類型後台目前還沒有清單可以查詢，暫不開放選擇。
          </p>
          <div class="news-edit__relation-add">
            <el-select v-model="pendingRelationType" style="width: 120px" @change="onRelationTypeChange">
              <el-option
                v-for="t in RELATION_TARGET_TYPE_ORDER"
                :key="t"
                :label="RELATION_TARGET_TYPE_LABEL[t]"
                :value="t"
                :disabled="!RELATION_TARGET_TYPES_AVAILABLE.includes(t)"
              />
            </el-select>
            <el-select
              v-model="pendingRelationTargetId"
              filterable
              clearable
              placeholder="搜尋並選擇"
              class="news-edit__relation-target-select"
              :loading="relationOptionsLoading"
              :disabled="!RELATION_TARGET_TYPES_AVAILABLE.includes(pendingRelationType)"
              no-data-text="沒有可選擇的資料"
              no-match-text="找不到符合的資料"
            >
              <el-option v-for="opt in currentRelationOptions" :key="opt.id" :label="opt.label" :value="opt.id" />
            </el-select>
            <el-button :disabled="!pendingRelationTargetId" @click="addRelation">加入</el-button>
          </div>
          <div v-if="form.relations.length > 0" class="news-edit__relation-list">
            <el-tag
              v-for="(relation, index) in form.relations"
              :key="`${relation.targetType}-${relation.targetId}`"
              closable
              class="news-edit__relation-tag"
              @close="removeRelation(index)"
            >
              {{ RELATION_TARGET_TYPE_LABEL[relation.targetType] }}：{{ resolveRelationLabel(relation) }}
            </el-tag>
          </div>
          <p v-else class="news-edit__hint">目前沒有任何關聯。</p>
        </el-card>

        <el-card shadow="never" header="發布設定" class="news-edit__section">
          <el-form-item label="置頂精選">
            <el-switch v-model="form.isFeatured" />
          </el-form-item>
          <p class="news-edit__hint">首頁置頂精選同時最多 3 篇（逐俱樂部各自計算），超過會在儲存時提醒。</p>

          <el-form-item v-if="currentId" label="瀏覽數">
            <span class="news-edit__view-count">{{ form.viewCount.toLocaleString('zh-Hant') }}</span>
          </el-form-item>
        </el-card>
      </el-form>

      <div v-if="!isReadOnly" class="news-edit__action-bar">
        <el-tooltip v-if="!canPreview" content="尚未發布，暫不提供預覽" placement="top">
          <span>
            <el-button disabled>預覽</el-button>
          </span>
        </el-tooltip>
        <el-button v-else @click="handlePreview">預覽</el-button>

        <el-button :loading="saving" @click="handleSaveDraft">儲存草稿</el-button>

        <el-dropdown trigger="click" split-button type="primary" :disabled="saving" @click="handleMainAction">
          {{ mainActionLabel }}
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item @click="openScheduleDialog">排程發布</el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>
      </div>
      <div v-else class="news-edit__action-bar">
        <el-button v-if="canPreview" @click="handlePreview">預覽</el-button>
      </div>
    </template>

    <el-dialog v-model="scheduleDialogVisible" title="排程發布" width="360px">
      <el-form-item label="發布日期與時間" style="margin-bottom: 0">
        <el-date-picker
          v-model="scheduleDateTime"
          type="datetime"
          placeholder="選擇日期與時間"
          style="width: 100%"
        />
      </el-form-item>
      <template #footer>
        <el-button @click="scheduleDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="confirmSchedule">確認排程</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.news-edit {
  max-width: 860px;
  margin: 0 auto 88px;
}

.news-edit__status-line {
  display: flex;
  align-items: center;
  gap: 12px;
  font-size: 13px;
  color: var(--admin-text-secondary);
  flex-wrap: wrap;
}

.news-edit__shared-note {
  display: flex;
  align-items: center;
  gap: 6px;
}

.news-edit__state-text {
  font-size: 14px;
  line-height: 1.7;
}

.news-edit__form-error {
  margin-bottom: 16px;
}

.news-edit__section {
  margin-bottom: 16px;
}

.news-edit__hint {
  margin: 4px 0 0;
  font-size: 12px;
  color: var(--admin-text-tertiary);
}

.news-edit__corevalue {
  margin-top: 12px;
}

.news-edit__corevalue-list {
  display: flex;
  flex-wrap: wrap;
  gap: 4px 20px;
}

.news-edit__relation-add {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.news-edit__relation-target-select {
  flex: 1;
  min-width: 200px;
}

.news-edit__relation-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 12px;
}

.news-edit__relation-tag {
  max-width: 100%;
}

.news-edit__view-count {
  font-size: 14px;
  color: var(--admin-text-secondary);
}

.news-edit__action-bar {
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
  .news-edit__action-bar {
    left: 0;
  }
}

@media (max-width: 767px) {
  .news-edit__action-bar {
    justify-content: stretch;
  }

  .news-edit__action-bar :deep(.el-button),
  .news-edit__action-bar :deep(.el-dropdown) {
    flex: 1;
  }
}
</style>

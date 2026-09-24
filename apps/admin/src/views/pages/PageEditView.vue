<script setup lang="ts">
/**
 * B1 頁面管理編輯頁：網址名稱、SEO 設定、區塊化內容編輯器（新增／排序／刪除 12 種區塊）、
 * 發布／排程、版本歷程與還原、預覽連結。對照規劃書 §4.2 B1（約行 1011–1014）與
 * apps/api/README.md「S1-4：B1 頁面管理」。版面沿用既有 `NewsEditView.vue` 的編輯頁標準型
 * （分段卡片／離開未儲存提醒／固定底部操作列），圖片一律「選檔不上傳、儲存才上傳」。
 */
import { computed, reactive, ref, shallowRef } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import StatusTag from '@/components/StatusTag.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import PageBlockEditor from '@/components/pageBlocks/PageBlockEditor.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { activeClubId } from '@/auth/clubAccess'
import {
  createAdminPage,
  getAdminPage,
  getAdminPageVersion,
  listAdminPageVersions,
  publishAdminPage,
  restoreAdminPageVersion,
  scheduleAdminPage,
  updateAdminPage,
  type AdminPageBlockDto,
  type AdminPageDetailDto,
  type AdminPageVersionDetailDto,
  type AdminPageVersionListItemDto,
} from '@/api/adminPages'
import { AdminApiError } from '@/api/http'
import { parseBlockFromDto, serializeBlocksForSubmit, PageBlockValidationError } from '@/utils/pageBlockSerializer'
import { createEmptyBlock, PAGE_BLOCK_TYPE_LABEL, PAGE_BLOCK_TYPES, type PageBlockState, type PageBlockType } from '@/types/pageBlocks'
import { formatDateTime } from '@/utils/formatDateTime'

const route = useRoute()
const router = useRouter()

// 🔴 必須是 computed 不能是一次性求值的 const：建立成功後 saveAndMaybeTransition() 呼叫
// `router.replace('/content/pages/:id/edit')`，Vue Router 對同一個元件實例的路由切換預設不會
// 重新掛載（component reuse），若 isCreate 只在 setup 當下算一次，畫面上依賴它的 `pageTitle`
// 與「預覽連結」「版本歷程」兩個 `v-if` 區塊在儲存成功後會繼續停在「建立中」的樣子（儲存流程
// 本身另外用 `!currentId.value` 擋住不會真的重複建立，但畫面顯示是錯的）。比照
// `CompetitionEditView.vue`／`MatchEditView.vue` 既有寫法（`docs/18` 回報項）。
const isCreate = computed(() => route.name === 'page-new')
const paramId = route.params.id as string | undefined
const currentId = ref<string | undefined>(paramId)

interface PageFormState {
  slug: string
  status: 'draft' | 'published' | 'scheduled'
  publishedAt: string | null
  updatedAt: string
  seoTitleZh: string
  seoDescriptionZh: string
  seoTitleEn: string
  seoDescriptionEn: string
  latestVersionNo: number
  previewToken: string | null
}

function emptyForm(): PageFormState {
  return {
    slug: '',
    status: 'draft',
    publishedAt: null,
    updatedAt: '',
    seoTitleZh: '',
    seoDescriptionZh: '',
    seoTitleEn: '',
    seoDescriptionEn: '',
    latestVersionNo: 0,
    previewToken: null,
  }
}

type LoadState = 'loading' | 'not-found' | 'error' | 'ready'
const loadState = ref<LoadState>('loading')
const loadErrorMessage = ref('')

const form = reactive<PageFormState>(emptyForm())
const blocks = ref<PageBlockState[]>([])
/** 用來判斷「離開未儲存」與 SEO 英文是否被整批清空的基準快照。 */
const baselineJson = shallowRef('')

function snapshotJson(): string {
  return JSON.stringify({ slug: form.slug, seoTitleZh: form.seoTitleZh, seoDescriptionZh: form.seoDescriptionZh, seoTitleEn: form.seoTitleEn, seoDescriptionEn: form.seoDescriptionEn, blocks: blocks.value })
}

function applyLoadedPage(dto: AdminPageDetailDto) {
  form.slug = dto.slug
  form.status = dto.status
  form.publishedAt = dto.publishedAt ?? null
  form.updatedAt = dto.updatedAt
  form.seoTitleZh = dto.zh.seoTitle ?? ''
  form.seoDescriptionZh = dto.zh.seoDescription ?? ''
  form.seoTitleEn = dto.en?.seoTitle ?? ''
  form.seoDescriptionEn = dto.en?.seoDescription ?? ''
  form.latestVersionNo = dto.latestVersionNo
  form.previewToken = dto.previewToken ?? null
  blocks.value = dto.blocks.map(parseBlockFromDto)
  currentId.value = dto.id
  baselineJson.value = snapshotJson()
  hadEnSeoAtLoad.value = !!(dto.en?.seoTitle?.trim() || dto.en?.seoDescription?.trim())
}

async function loadPage() {
  loadState.value = 'loading'
  if (isCreate.value) {
    blocks.value = []
    baselineJson.value = snapshotJson()
    loadState.value = 'ready'
    return
  }
  try {
    const detail = await getAdminPage(activeClubId.value, currentId.value!)
    applyLoadedPage(detail)
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

loadPage()

const saving = ref(false)
const slugError = ref<string | null>(null)
const formError = ref<string | null>(null)
const scheduleDialogVisible = ref(false)
const scheduleDateTime = ref<Date | null>(null)

const isDirty = computed(() => loadState.value === 'ready' && snapshotJson() !== baselineJson.value)
useUnsavedChanges(isDirty)

const pageTitle = computed(() => (isCreate.value ? '新增頁面' : `編輯頁面：${form.slug || '（尚未命名）'}`))
const mainActionLabel = computed(() => (form.status === 'published' ? '儲存變更' : '發布'))
const canPreview = computed(() => form.status === 'published')
const frontendPreviewUrl = computed(() => (canPreview.value ? `/zh/${form.slug.replace(/^\/+|\/+$/g, '')}/` : undefined))

function isEnSeoEmpty(): boolean {
  return !form.seoTitleEn.trim() && !form.seoDescriptionEn.trim()
}
const hadEnSeoAtLoad = ref(false)

async function confirmEnglishSeoRemovalIfNeeded(): Promise<boolean> {
  if (hadEnSeoAtLoad.value && isEnSeoEmpty()) {
    try {
      await ElMessageBox.confirm(
        '偵測到英文 SEO 欄位都被清空了。儲存後，這個頁面的英文 SEO 設定會被移除（不是暫時留白，是整份刪除）。確定要繼續嗎？',
        '確認移除英文 SEO 設定',
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
  if (!form.slug.trim()) {
    formError.value = '請輸入網址名稱'
    return false
  }
  return true
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
          '這個頁面已經被其他人（或你自己開的另一個分頁）變更過，繼續儲存會覆蓋掉對方的修改，系統不允許這樣做。'
          + '你可以重新載入最新的內容（目前畫面上未儲存的修改會遺失），或是先留在這裡，自行把想保留的內容複製下來再手動處理。',
          '資料已被變更',
          { confirmButtonText: '重新載入最新內容', cancelButtonText: '留在這裡', type: 'warning' },
        )
        await loadPage()
      } catch {
        // 使用者選擇留在這裡，不動作。
      }
      break
    case 'forbidden':
      await ElMessageBox.alert(error.message, '沒有編輯權限', { confirmButtonText: '我知道了' })
      break
    case 'status-conflict':
      await ElMessageBox.alert(error.message, '無法這樣操作', { confirmButtonText: '我知道了' })
      break
    case 'not-found':
      await ElMessageBox.alert('這個頁面已經找不到了，可能已被刪除。', '找不到這個頁面', { confirmButtonText: '返回列表' })
      router.push('/content/pages')
      break
    case 'network':
      ElMessage.error(error.message)
      break
    default:
      ElMessage.error(error.message || '儲存失敗，請稍後再試')
  }
}

async function saveAndMaybeTransition(transition?: { kind: 'publish' } | { kind: 'schedule'; publishAt: string }) {
  if (!validateBeforeSave()) return

  let blocksPayload
  let files: Record<string, File>
  try {
    const serialized = serializeBlocksForSubmit(blocks.value)
    blocksPayload = serialized.blocks
    files = serialized.files
  } catch (error) {
    formError.value = error instanceof PageBlockValidationError ? error.message : '內容區塊驗證失敗，請檢查後再試'
    return
  }

  if (!(await confirmEnglishSeoRemovalIfNeeded())) return

  saving.value = true
  try {
    const club = activeClubId.value
    const seo = {
      zh: { seoTitle: form.seoTitleZh || null, seoDescription: form.seoDescriptionZh || null },
      en: isEnSeoEmpty() ? undefined : { seoTitle: form.seoTitleEn || null, seoDescription: form.seoDescriptionEn || null },
    }
    const payload = { slug: form.slug.trim(), seo, blocks: blocksPayload }

    let saved: AdminPageDetailDto
    if (isCreate.value && !currentId.value) {
      saved = await createAdminPage(club, payload, files)
    } else {
      saved = await updateAdminPage(club, currentId.value!, { ...payload, expectedUpdatedAt: form.updatedAt }, files)
    }

    if (transition?.kind === 'publish') {
      saved = await publishAdminPage(club, saved.id, saved.updatedAt)
    } else if (transition?.kind === 'schedule') {
      saved = await scheduleAdminPage(club, saved.id, saved.updatedAt, transition.publishAt)
    }

    const wasCreate = isCreate.value && !currentId.value
    applyLoadedPage(saved)
    hadEnSeoAtLoad.value = !isEnSeoEmpty()
    slugError.value = null
    formError.value = null

    ElMessage.success(transition?.kind === 'publish' ? '已發布' : transition?.kind === 'schedule' ? '已排程發布' : '已儲存')

    if (wasCreate) {
      router.replace(`/content/pages/${saved.id}/edit`)
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
  router.push('/content/pages')
}

function handlePreview() {
  if (!frontendPreviewUrl.value) return
  window.open(frontendPreviewUrl.value, '_blank', 'noopener')
}

function retryLoad() {
  loadPage()
}

// ── 內容區塊：新增／排序／刪除 ────────────────────────────────────────────
const addBlockType = ref<PageBlockType>('text')

function addBlock() {
  blocks.value.push(createEmptyBlock(addBlockType.value))
}

function moveBlock(index: number, delta: number) {
  const target = index + delta
  if (target < 0 || target >= blocks.value.length) return
  const [item] = blocks.value.splice(index, 1)
  blocks.value.splice(target, 0, item)
}

async function removeBlock(index: number) {
  try {
    await ElMessageBox.confirm('確定要刪除這個區塊嗎？', '確認刪除', {
      confirmButtonText: '刪除',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    })
  } catch {
    return
  }
  blocks.value.splice(index, 1)
}

// ── 預覽連結（顯示並可複製） ─────────────────────────────────────────────
const previewLocale = ref<'zh' | 'en'>('zh')
const previewLinkUrl = computed(() => (form.previewToken ? `/${previewLocale.value}/preview/${form.previewToken}` : null))

async function copyPreviewLink() {
  if (!previewLinkUrl.value) return
  try {
    await navigator.clipboard.writeText(previewLinkUrl.value)
    ElMessage.success('已複製預覽連結')
  } catch {
    ElMessage.error('複製失敗，請手動選取上方文字複製')
  }
}

// ── 版本歷程與還原 ─────────────────────────────────────────────────────
const versionsDialogVisible = ref(false)
const versions = ref<AdminPageVersionListItemDto[]>([])
const versionsLoading = ref(false)
const restoringVersionNo = ref<number | null>(null)

const viewingVersion = ref<AdminPageVersionDetailDto | null>(null)
const viewingVersionLoading = ref(false)

async function openVersionsDialog() {
  versionsDialogVisible.value = true
  viewingVersion.value = null
  versionsLoading.value = true
  try {
    const result = await listAdminPageVersions(activeClubId.value, currentId.value!, 1, 50)
    versions.value = result.items
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '版本歷程載入失敗，請稍後再試')
  } finally {
    versionsLoading.value = false
  }
}

async function viewVersion(versionNo: number) {
  viewingVersionLoading.value = true
  viewingVersion.value = null
  try {
    viewingVersion.value = await getAdminPageVersion(activeClubId.value, currentId.value!, versionNo)
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '版本內容載入失敗，請稍後再試')
  } finally {
    viewingVersionLoading.value = false
  }
}

async function restoreVersion(versionNo: number) {
  try {
    await ElMessageBox.confirm(
      `確定要還原到版本 ${versionNo} 嗎？系統會以這個版本的內容建立一筆新版本，不會刪除中間的版本紀錄，也不會改變目前的發布狀態。`,
      '確認還原',
      { confirmButtonText: '還原', cancelButtonText: '取消', type: 'warning' },
    )
  } catch {
    return
  }
  restoringVersionNo.value = versionNo
  try {
    const restored = await restoreAdminPageVersion(activeClubId.value, currentId.value!, versionNo, form.updatedAt)
    applyLoadedPage(restored)
    ElMessage.success('已還原，內容已更新為新的版本')
    versionsDialogVisible.value = false
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'concurrency-conflict') {
      ElMessage.warning('這個頁面已被變更，請重新載入後再試一次')
      versionsDialogVisible.value = false
      await loadPage()
    } else {
      ElMessage.error(error instanceof AdminApiError ? error.message : '還原失敗，請稍後再試')
    }
  } finally {
    restoringVersionNo.value = null
  }
}

function blockTypeLabel(blockType: string): string {
  return PAGE_BLOCK_TYPE_LABEL[blockType as PageBlockType] ?? blockType
}

function truncate(text: string, max = 24): string {
  const trimmed = text.trim()
  return trimmed.length > max ? `${trimmed.slice(0, max)}…` : trimmed
}

function bilingualZh(value: unknown): string {
  return value && typeof value === 'object' && 'zh' in (value as Record<string, unknown>)
    ? String((value as { zh?: unknown }).zh ?? '')
    : ''
}

/** 版本內容的簡短摘要，供「檢視內容」時快速核對，不是完整還原編輯器畫面（規劃書只要求
 * 「版本歷程與還原」，沒有要求逐版重新渲染完整的區塊編輯器）。 */
function summarizeBlock(dto: AdminPageBlockDto): string {
  const label = blockTypeLabel(dto.blockType)
  const c = (dto.content ?? {}) as Record<string, unknown>
  switch (dto.blockType) {
    case 'text':
    case 'text_image':
      return `${label}：${truncate(bilingualZh(c.body)) || '（未填寫內文）'}`
    case 'gallery':
      return `${label}：共 ${Array.isArray(c.images) ? c.images.length : 0} 張圖片`
    case 'video_embed':
      return `${label}：${c.provider ?? ''} ／ ${c.videoId ?? ''}`
    case 'quote':
      return `${label}：${truncate(bilingualZh(c.text)) || '（未填寫）'}`
    case 'cta':
      return `${label}：${truncate(bilingualZh(c.buttonLabel)) || '（未填寫按鈕文字）'}`
    case 'accordion_faq':
      return `${label}：共 ${Array.isArray(c.items) ? c.items.length : 0} 筆問答`
    case 'timeline':
      return `${label}：共 ${Array.isArray(c.items) ? c.items.length : 0} 筆事件`
    case 'steps':
      return `${label}：共 ${Array.isArray(c.items) ? c.items.length : 0} 個步驟`
    case 'stat_cards':
      return `${label}：共 ${Array.isArray(c.items) ? c.items.length : 0} 張數據卡`
    case 'table':
      return `${label}：${Array.isArray(c.headers) ? c.headers.length : 0} 欄 × ${Array.isArray(c.rows) ? c.rows.length : 0} 列`
    case 'file_download':
      return `${label}：${truncate(bilingualZh(c.label)) || '（未填寫檔案名稱）'}`
    default:
      return label
  }
}
</script>

<template>
  <div class="page-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="handleBack">
          <el-icon><ArrowLeft /></el-icon>
          返回列表
        </el-button>
      </template>
      <template #meta>
        <FrontendUnitBanner
          module-code="B1"
          :record-published="loadState === 'ready' ? form.status === 'published' : undefined"
          :record-url="frontendPreviewUrl"
        />
        <span v-if="loadState === 'ready'" class="page-edit__status-line">
          <StatusTag :status="form.status" :status-at="form.publishedAt ?? undefined" />
        </span>
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never">
      <el-skeleton :rows="10" animated />
    </el-card>

    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #image>
          <el-icon :size="48" color="var(--admin-text-tertiary)"><WarningFilled /></el-icon>
        </template>
        <template #description>
          <p v-if="loadState === 'not-found'" class="page-edit__state-text">
            找不到這個頁面，可能已經被刪除，或不屬於目前選擇的俱樂部。
          </p>
          <p v-else class="page-edit__state-text">{{ loadErrorMessage }}</p>
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
        class="page-edit__form-error"
        @close="formError = null"
      />

      <el-form label-position="top" class="page-edit__form">
        <el-card shadow="never" header="基本資訊" class="page-edit__section">
          <el-form-item label="網址名稱" required :error="slugError ?? undefined">
            <el-input
              v-model="form.slug"
              placeholder="例如：about/history（可用斜線表示分層路徑）"
              @update:model-value="slugError = null"
            />
          </el-form-item>
        </el-card>

        <el-card shadow="never" header="搜尋引擎摘要資料（SEO）" class="page-edit__section">
          <BilingualShortField
            label="SEO 標題"
            :zh="form.seoTitleZh"
            :en="form.seoTitleEn"
            placeholder="選填，未填寫時由搜尋引擎自行判斷"
            @update:zh="(v) => (form.seoTitleZh = v)"
            @update:en="(v) => (form.seoTitleEn = v)"
          />
          <BilingualTextareaField
            label="SEO 描述"
            :zh="form.seoDescriptionZh"
            :en="form.seoDescriptionEn"
            placeholder="選填，建議 80–120 字"
            :rows="3"
            @update:zh="(v) => (form.seoDescriptionZh = v)"
            @update:en="(v) => (form.seoDescriptionEn = v)"
          />
        </el-card>

        <el-card shadow="never" header="內容區塊" class="page-edit__section">
          <p v-if="blocks.length === 0" class="page-edit__hint">這個頁面目前還沒有任何內容區塊，從下方選一種類型開始新增。</p>

          <div v-for="(block, index) in blocks" :key="block.localKey" class="page-edit__block">
            <div class="page-edit__block-toolbar">
              <span class="page-edit__block-type">{{ blockTypeLabel(block.blockType) }}</span>
              <div class="page-edit__block-actions">
                <el-button size="small" text :disabled="index === 0" @click="moveBlock(index, -1)">上移</el-button>
                <el-button size="small" text :disabled="index === blocks.length - 1" @click="moveBlock(index, 1)">下移</el-button>
                <el-button size="small" text type="danger" @click="removeBlock(index)">刪除區塊</el-button>
              </div>
            </div>
            <PageBlockEditor :block-type="block.blockType" :content="block.content" />
          </div>

          <div class="page-edit__add-block">
            <el-select v-model="addBlockType" style="width: 200px">
              <el-option v-for="type in PAGE_BLOCK_TYPES" :key="type" :label="PAGE_BLOCK_TYPE_LABEL[type]" :value="type" />
            </el-select>
            <el-button type="primary" plain @click="addBlock">+ 新增區塊</el-button>
          </div>
        </el-card>

        <el-card v-if="!isCreate" shadow="never" header="預覽連結" class="page-edit__section">
          <template v-if="form.previewToken">
            <p class="page-edit__hint">未發布也可以分享這個連結，讓其他人看到目前儲存的最新內容。</p>
            <div class="page-edit__preview-link">
              <el-radio-group v-model="previewLocale" size="small">
                <el-radio-button value="zh">中文版</el-radio-button>
                <el-radio-button value="en">英文版</el-radio-button>
              </el-radio-group>
              <el-input :model-value="previewLinkUrl ?? ''" readonly class="page-edit__preview-link-input">
                <template #append>
                  <el-button @click="copyPreviewLink">複製</el-button>
                </template>
              </el-input>
            </div>
            <p class="page-edit__hint">
              以上是官網的路徑，實際分享時請自行接上官網網域（本機開發環境不保證能直接打開）。
            </p>
          </template>
          <p v-else class="page-edit__hint">儲存後才會產生預覽連結。</p>
        </el-card>
      </el-form>

      <div class="page-edit__action-bar">
        <el-button v-if="!isCreate" @click="openVersionsDialog">版本歷程</el-button>
        <el-tooltip v-if="!canPreview" content="尚未發布，暫不提供正式網址預覽" placement="top">
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

    <el-dialog v-model="versionsDialogVisible" title="版本歷程" width="640px">
      <el-skeleton v-if="versionsLoading" :rows="4" animated />
      <el-table v-else :data="versions" size="small">
        <el-table-column label="版本" width="80">
          <template #default="{ row }">第 {{ row.versionNo }} 版</template>
        </el-table-column>
        <el-table-column label="建立時間" min-width="160">
          <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="180">
          <template #default="{ row }">
            <el-button size="small" text @click="viewVersion(row.versionNo)">檢視內容</el-button>
            <el-button
              size="small"
              text
              type="primary"
              :loading="restoringVersionNo === row.versionNo"
              :disabled="row.versionNo === form.latestVersionNo"
              @click="restoreVersion(row.versionNo)"
            >
              還原
            </el-button>
          </template>
        </el-table-column>
      </el-table>

      <el-divider v-if="viewingVersionLoading || viewingVersion" />

      <el-skeleton v-if="viewingVersionLoading" :rows="3" animated />
      <div v-else-if="viewingVersion" class="page-edit__version-detail">
        <h4>第 {{ viewingVersion.versionNo }} 版內容摘要</h4>
        <p class="page-edit__hint">SEO 標題（中文）：{{ viewingVersion.zh.seoTitle || '（未設定）' }}</p>
        <ul class="page-edit__version-block-list">
          <li v-for="(block, i) in viewingVersion.blocks" :key="block.id">{{ i + 1 }}. {{ summarizeBlock(block) }}</li>
          <li v-if="viewingVersion.blocks.length === 0">（這個版本沒有任何內容區塊）</li>
        </ul>
      </div>

      <template #footer>
        <el-button @click="versionsDialogVisible = false">關閉</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.page-edit {
  max-width: 900px;
  margin: 0 auto 88px;
}

.page-edit__status-line {
  display: flex;
  align-items: center;
  gap: 12px;
  font-size: 13px;
  color: var(--admin-text-secondary);
}

.page-edit__state-text {
  font-size: 14px;
  line-height: 1.7;
}

.page-edit__form-error {
  margin-bottom: 16px;
}

.page-edit__section {
  margin-bottom: 16px;
}

.page-edit__hint {
  margin: 4px 0 0;
  font-size: 12px;
  color: var(--admin-text-tertiary);
}

.page-edit__block {
  border: 1px solid var(--admin-border);
  border-radius: 4px;
  padding: 16px;
  margin-bottom: 16px;
}

.page-edit__block-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 12px;
  padding-bottom: 8px;
  border-bottom: 1px solid var(--admin-border);
}

.page-edit__block-type {
  font-weight: 500;
  color: var(--admin-text-primary);
}

.page-edit__add-block {
  display: flex;
  gap: 8px;
}

.page-edit__preview-link {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.page-edit__preview-link-input {
  flex: 1;
  min-width: 240px;
}

.page-edit__version-detail h4 {
  margin: 0 0 8px;
}

.page-edit__version-block-list {
  margin: 0;
  padding-left: 20px;
  font-size: 13px;
  line-height: 1.8;
}

.page-edit__action-bar {
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
  .page-edit__action-bar {
    left: 0;
  }
}

@media (max-width: 767px) {
  .page-edit__action-bar {
    justify-content: stretch;
    flex-wrap: wrap;
  }

  .page-edit__action-bar :deep(.el-button),
  .page-edit__action-bar :deep(.el-dropdown) {
    flex: 1;
  }
}
</style>

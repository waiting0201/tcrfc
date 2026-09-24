<script setup lang="ts">
/**
 * B3 首頁編排（對應主站規劃書 §4.2 B3，行 999–1002；apps/api/README.md「S1-6：B3 首頁編排」）。
 * 這裡管理的是前台首頁：Hero 輪播（含排序、圖片、標題、CTA、上架期間、雙語替代文字）與
 * 九大區塊的開關與排序。單頁式管理（不像新聞／頁面管理有列表／編輯兩個路由）——
 * 輪播與區塊本身筆數固定或很少，不需要獨立的編輯頁與分頁機制。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import VideoUploader from '@/components/VideoUploader.vue'
import { activeClubId } from '@/auth/clubAccess'
import {
  createAdminBanner,
  deleteAdminBanner,
  getAdminBanner,
  listAdminBanners,
  listAdminHomeSections,
  publishAdminBanner,
  unpublishAdminBanner,
  updateAdminBanner,
  updateAdminHomeSection,
  type AdminBannerListItemDto,
  type AdminHomeSectionDto,
  type SaveBannerPayload,
} from '@/api/adminHome'
import { AdminApiError } from '@/api/http'

const club = computed(() => activeClubId.value)

// ── Hero 輪播 ─────────────────────────────────────────────────────────────────────

const banners = ref<AdminBannerListItemDto[]>([])
const bannersLoading = ref(true)
const bannersError = ref<string | null>(null)

async function loadBanners() {
  bannersLoading.value = true
  bannersError.value = null
  try {
    banners.value = await listAdminBanners(club.value)
  } catch (error) {
    banners.value = []
    bannersError.value = error instanceof AdminApiError ? error.message : '輪播清單載入失敗，請稍後再試'
  } finally {
    bannersLoading.value = false
  }
}

const sortedBanners = computed(() => [...banners.value].sort((a, b) => a.sortOrder - b.sortOrder))

interface BannerFormState {
  id: string | null
  mediaType: 'image' | 'video'
  startAt: Date | null
  endAt: Date | null
  sortOrder: number
  titleZh: string
  titleEn: string
  subtitleZh: string
  subtitleEn: string
  altZh: string
  altEn: string
  cta1LabelZh: string
  cta1LabelEn: string
  cta1Url: string
  cta2LabelZh: string
  cta2LabelEn: string
  cta2Url: string
}

function emptyBannerForm(): BannerFormState {
  return {
    id: null,
    mediaType: 'image',
    startAt: null,
    endAt: null,
    sortOrder: banners.value.length,
    titleZh: '',
    titleEn: '',
    subtitleZh: '',
    subtitleEn: '',
    altZh: '',
    altEn: '',
    cta1LabelZh: '',
    cta1LabelEn: '',
    cta1Url: '',
    cta2LabelZh: '',
    cta2LabelEn: '',
    cta2Url: '',
  }
}

const bannerDialogVisible = ref(false)
const bannerDialogMode = ref<'create' | 'edit'>('create')
const bannerDialogLoading = ref(false)
const bannerSaving = ref(false)
const bannerFormError = ref<string | null>(null)
const bannerForm = reactive<BannerFormState>(emptyBannerForm())
const bannerImageFile = ref<File | null>(null)
const bannerHasExistingImage = ref(false)
const bannerVideoFile = ref<File | null>(null)
const bannerHasExistingVideo = ref(false)

// 素材種類切回「圖片」時，這次瀏覽階段選過的影片檔案要一併清空——後端契約是
// `mediaType='image'` 時不可以帶 `video` 欄位（見 apps/api/README.md「S1-7b」）。
watch(
  () => bannerForm.mediaType,
  (mediaType) => {
    if (mediaType === 'image') bannerVideoFile.value = null
  },
)

function openCreateBannerDialog() {
  bannerDialogMode.value = 'create'
  Object.assign(bannerForm, emptyBannerForm())
  bannerImageFile.value = null
  bannerHasExistingImage.value = false
  bannerVideoFile.value = null
  bannerHasExistingVideo.value = false
  bannerFormError.value = null
  bannerDialogVisible.value = true
}

async function openEditBannerDialog(row: AdminBannerListItemDto) {
  bannerDialogMode.value = 'edit'
  bannerFormError.value = null
  bannerImageFile.value = null
  bannerVideoFile.value = null
  bannerDialogVisible.value = true
  bannerDialogLoading.value = true
  try {
    const detail = await getAdminBanner(club.value, row.id)
    Object.assign(bannerForm, {
      id: detail.id,
      mediaType: detail.mediaType,
      startAt: detail.startAt ? new Date(detail.startAt) : null,
      endAt: detail.endAt ? new Date(detail.endAt) : null,
      sortOrder: detail.sortOrder,
      titleZh: detail.zh.title ?? '',
      titleEn: detail.en?.title ?? '',
      subtitleZh: detail.zh.subtitle ?? '',
      subtitleEn: detail.en?.subtitle ?? '',
      altZh: detail.zh.imageAlt ?? '',
      altEn: detail.en?.imageAlt ?? '',
      cta1LabelZh: detail.zh.cta1Label ?? '',
      cta1LabelEn: detail.en?.cta1Label ?? '',
      cta1Url: detail.zh.cta1Url ?? '',
      cta2LabelZh: detail.zh.cta2Label ?? '',
      cta2LabelEn: detail.en?.cta2Label ?? '',
      cta2Url: detail.zh.cta2Url ?? '',
    })
    bannerHasExistingImage.value = true
    bannerHasExistingVideo.value = detail.mediaType === 'video' && Boolean(detail.videoKey)
  } catch (error) {
    bannerFormError.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
  } finally {
    bannerDialogLoading.value = false
  }
}

function closeBannerDialog() {
  bannerDialogVisible.value = false
}

/** `banners.image_key` 是 `NOT NULL`（後端契約只有「換一張」或「維持原圖」兩態，沒有「移除」，
 * 見 `apps/api/README.md` `UpdateBannerRequest` 的說明），`ImageUploader` 元件本身不知道這個
 * 差異，既有圖片一律顯示「移除」按鈕——這裡攔截這個意圖並提示改用「更換圖片」，不是靜默忽略。 */
function handleBannerRemoveCoverAttempt(value: boolean) {
  if (value) {
    ElMessage.warning('輪播圖片為必填欄位，無法移除，請直接選擇新圖片替換。')
  }
}

function buildBannerPayload(): SaveBannerPayload {
  const enEmpty =
    !bannerForm.titleEn.trim()
    && !bannerForm.subtitleEn.trim()
    && !bannerForm.altEn.trim()
    && !bannerForm.cta1LabelEn.trim()
    && !bannerForm.cta2LabelEn.trim()

  return {
    mediaType: bannerForm.mediaType,
    startAt: bannerForm.startAt ? bannerForm.startAt.toISOString() : null,
    endAt: bannerForm.endAt ? bannerForm.endAt.toISOString() : null,
    sortOrder: bannerForm.sortOrder,
    content: {
      zh: {
        title: bannerForm.titleZh || null,
        subtitle: bannerForm.subtitleZh || null,
        imageAlt: bannerForm.altZh || null,
        cta1Label: bannerForm.cta1LabelZh || null,
        cta1Url: bannerForm.cta1Url || null,
        cta2Label: bannerForm.cta2LabelZh || null,
        cta2Url: bannerForm.cta2Url || null,
      },
      en: enEmpty
        ? undefined
        : {
            title: bannerForm.titleEn || null,
            subtitle: bannerForm.subtitleEn || null,
            imageAlt: bannerForm.altEn || null,
            cta1Label: bannerForm.cta1LabelEn || null,
            cta1Url: bannerForm.cta1Url || null,
            cta2Label: bannerForm.cta2LabelEn || null,
            cta2Url: bannerForm.cta2Url || null,
          },
    },
  }
}

async function saveBanner() {
  if (bannerForm.startAt && bannerForm.endAt && bannerForm.startAt.getTime() >= bannerForm.endAt.getTime()) {
    bannerFormError.value = '上架時間必須早於下架時間'
    return
  }
  if (bannerDialogMode.value === 'create' && !bannerImageFile.value) {
    bannerFormError.value = bannerForm.mediaType === 'video' ? '請選擇影片海報圖' : '請選擇輪播圖片'
    return
  }
  if (bannerForm.mediaType === 'video' && !bannerVideoFile.value && !bannerHasExistingVideo.value) {
    bannerFormError.value = '素材種類為「影片」時，必須上傳影片檔案'
    return
  }
  bannerSaving.value = true
  bannerFormError.value = null
  try {
    const payload = buildBannerPayload()
    if (bannerDialogMode.value === 'create') {
      await createAdminBanner(club.value, payload, bannerImageFile.value!, bannerVideoFile.value)
      ElMessage.success('已存為草稿，發布後才會在前台顯示。')
    } else {
      await updateAdminBanner(club.value, bannerForm.id!, payload, bannerImageFile.value, bannerVideoFile.value)
      ElMessage.success('已儲存')
    }
    bannerDialogVisible.value = false
    await loadBanners()
  } catch (error) {
    bannerFormError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    bannerSaving.value = false
  }
}

async function handleDeleteBanner(row: AdminBannerListItemDto) {
  try {
    await ElMessageBox.confirm(
      `確定要刪除這則輪播「${row.titleZh || '（未命名）'}」嗎？這個動作無法復原。`,
      '確認刪除',
      { confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning' },
    )
  } catch {
    return
  }
  try {
    await deleteAdminBanner(club.value, row.id)
    ElMessage.success('已刪除')
    await loadBanners()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
  }
}

function bannerPeriodLabel(row: AdminBannerListItemDto): string {
  if (!row.startAt && !row.endAt) return '不限期間'
  const start = row.startAt ? row.startAt.slice(0, 10) : '（不限起始）'
  const end = row.endAt ? row.endAt.slice(0, 10) : '（不限結束）'
  return `${start} ～ ${end}`
}

const bannerStatusLabel = (status: string): string => (status === 'published' ? '已發布' : '草稿')

/**
 * 「目前是否在前台顯示」只是畫面上的提示，用瀏覽器當下時間跟 `status`／`startAt`／`endAt`
 * 粗略推算——真正的判斷（含時區與資料庫時鐘）在後端的公開端點（apps/api/README.md「S1-7b」）。
 */
function bannerVisibilityLabel(row: AdminBannerListItemDto): string {
  if (row.status !== 'published') return '不顯示（草稿）'
  const now = Date.now()
  if (row.startAt && now < new Date(row.startAt).getTime()) return '不顯示（尚未到上架時間）'
  if (row.endAt && now > new Date(row.endAt).getTime()) return '不顯示（已過下架時間）'
  return '顯示中'
}

function bannerVisibilityTagType(row: AdminBannerListItemDto): 'success' | 'info' {
  return bannerVisibilityLabel(row) === '顯示中' ? 'success' : 'info'
}

async function handlePublishBanner(row: AdminBannerListItemDto) {
  try {
    await publishAdminBanner(club.value, row.id)
    ElMessage.success('已發布')
    await loadBanners()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '發布失敗，請稍後再試')
  }
}

async function handleUnpublishBanner(row: AdminBannerListItemDto) {
  try {
    await unpublishAdminBanner(club.value, row.id)
    ElMessage.success('已改回草稿')
    await loadBanners()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '改回草稿失敗，請稍後再試')
  }
}

// ── 首頁九大區塊 ───────────────────────────────────────────────────────────────────

const sections = ref<AdminHomeSectionDto[]>([])
const sectionsLoading = ref(true)
const sectionsError = ref<string | null>(null)
/** 每個區塊自己的儲存中狀態，避免一個區塊儲存時整張表被鎖住。 */
const sectionSaving = reactive<Record<string, boolean>>({})

async function loadSections() {
  sectionsLoading.value = true
  sectionsError.value = null
  try {
    const list = await listAdminHomeSections(club.value)
    sections.value = [...list].sort((a, b) => a.sortOrder - b.sortOrder)
  } catch (error) {
    sections.value = []
    sectionsError.value = error instanceof AdminApiError ? error.message : '首頁區塊載入失敗，請稍後再試'
  } finally {
    sectionsLoading.value = false
  }
}

async function saveSection(section: AdminHomeSectionDto) {
  sectionSaving[section.sectionCode] = true
  try {
    const updated = await updateAdminHomeSection(club.value, section.sectionCode, {
      isEnabled: section.isEnabled,
      sortOrder: section.sortOrder,
      featuredBannerId: section.sectionCode === 'hero' ? section.featuredBannerId ?? null : null,
    })
    const index = sections.value.findIndex((s) => s.sectionCode === section.sectionCode)
    if (index !== -1) sections.value[index] = updated
    ElMessage.success(`已更新「${section.nameZh}」`)
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '更新失敗，請稍後再試')
    await loadSections()
  } finally {
    sectionSaving[section.sectionCode] = false
  }
}

function onToggleSection(section: AdminHomeSectionDto) {
  saveSection(section)
}

const bannerOptionsForFeatured = computed(() =>
  sortedBanners.value.map((b) => ({ id: b.id, label: b.titleZh?.trim() || '（未命名輪播）' })),
)

async function bootstrap() {
  await Promise.all([loadBanners(), loadSections()])
}

onMounted(bootstrap)
watch(club, bootstrap)
</script>

<template>
  <div class="home-layout">
    <PageHeader title="首頁編排">
      <template #meta>
        <FrontendUnitBanner module-code="B3" />
      </template>
    </PageHeader>

    <el-card shadow="never" header="Hero 輪播" class="home-layout__section">
      <template #header>
        <div class="home-layout__card-header">
          <span>Hero 輪播</span>
          <el-button type="primary" size="small" @click="openCreateBannerDialog">+ 新增輪播</el-button>
        </div>
      </template>

      <el-skeleton v-if="bannersLoading" :rows="4" animated />
      <el-empty v-else-if="bannersError" :description="bannersError">
        <el-button type="primary" @click="loadBanners">重新載入</el-button>
      </el-empty>
      <el-empty v-else-if="sortedBanners.length === 0" description="目前還沒有任何輪播圖">
        <el-button type="primary" @click="openCreateBannerDialog">+ 新增第一則輪播</el-button>
      </el-empty>
      <el-table v-else :data="sortedBanners" row-key="id">
        <el-table-column label="排序" width="72">
          <template #default="{ row }">{{ row.sortOrder }}</template>
        </el-table-column>
        <el-table-column label="標題" min-width="180">
          <template #default="{ row }">{{ row.titleZh || '（未命名）' }}</template>
        </el-table-column>
        <el-table-column label="狀態" width="90">
          <template #default="{ row }">
            <el-tag :type="row.status === 'published' ? 'success' : 'info'" size="small">
              {{ bannerStatusLabel(row.status) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="上架期間" min-width="200">
          <template #default="{ row }">{{ bannerPeriodLabel(row) }}</template>
        </el-table-column>
        <el-table-column label="目前是否在前台顯示" min-width="160">
          <template #default="{ row }">
            <el-tag :type="bannerVisibilityTagType(row)" size="small">{{ bannerVisibilityLabel(row) }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="220" fixed="right">
          <template #default="{ row }">
            <el-button
              v-if="row.status === 'draft'"
              size="small"
              text
              type="primary"
              @click="handlePublishBanner(row)"
            >
              發布
            </el-button>
            <el-button v-else size="small" text @click="handleUnpublishBanner(row)">改回草稿</el-button>
            <el-button size="small" text type="primary" @click="openEditBannerDialog(row)">編輯</el-button>
            <el-button size="small" text type="danger" @click="handleDeleteBanner(row)">刪除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-card shadow="never" header="首頁區塊開關與排序" class="home-layout__section">
      <el-skeleton v-if="sectionsLoading" :rows="6" animated />
      <el-empty v-else-if="sectionsError" :description="sectionsError">
        <el-button type="primary" @click="loadSections">重新載入</el-button>
      </el-empty>
      <el-table v-else :data="sections" row-key="sectionCode">
        <el-table-column label="區塊" min-width="140">
          <template #default="{ row }">{{ row.nameZh }}</template>
        </el-table-column>
        <el-table-column label="啟用" width="90">
          <template #default="{ row }">
            <el-switch v-model="row.isEnabled" :loading="sectionSaving[row.sectionCode]" @change="onToggleSection(row)" />
          </template>
        </el-table-column>
        <el-table-column label="排序" width="140">
          <template #default="{ row }">
            <el-input-number v-model="row.sortOrder" :min="0" size="small" controls-position="right" />
          </template>
        </el-table-column>
        <el-table-column label="精選輪播" min-width="220">
          <template #default="{ row }">
            <el-select
              v-if="row.sectionCode === 'hero'"
              v-model="row.featuredBannerId"
              placeholder="不指定（依排序顯示全部）"
              clearable
              filterable
              style="width: 100%"
            >
              <el-option v-for="opt in bannerOptionsForFeatured" :key="opt.id" :label="opt.label" :value="opt.id" />
            </el-select>
            <span v-else class="home-layout__muted">此區塊目前沒有可指定的精選內容欄位，內容依各自模組的既有機制決定（例如最新消息沿用文章的置頂精選）</span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="90" fixed="right">
          <template #default="{ row }">
            <el-button size="small" text type="primary" :loading="sectionSaving[row.sectionCode]" @click="saveSection(row)">
              儲存
            </el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-dialog
      v-model="bannerDialogVisible"
      :title="bannerDialogMode === 'create' ? '新增輪播' : '編輯輪播'"
      width="640px"
      :close-on-click-modal="false"
      @close="closeBannerDialog"
    >
      <el-skeleton v-if="bannerDialogLoading" :rows="6" animated />
      <template v-else>
        <el-alert
          v-if="bannerFormError"
          :title="bannerFormError"
          type="warning"
          show-icon
          class="home-layout__form-error"
          @close="bannerFormError = null"
        />
        <el-form label-position="top">
          <el-form-item label="素材種類" required>
            <el-radio-group v-model="bannerForm.mediaType" :disabled="bannerSaving">
              <el-radio value="image">圖片</el-radio>
              <el-radio value="video">影片</el-radio>
            </el-radio-group>
          </el-form-item>

          <el-form-item :label="bannerForm.mediaType === 'video' ? '影片海報圖' : '輪播圖片'" required>
            <ImageUploader
              v-model:file="bannerImageFile"
              :remove-cover="false"
              :has-existing-image="bannerHasExistingImage"
              :disabled="bannerSaving"
              @update:remove-cover="handleBannerRemoveCoverAttempt"
            />
            <p v-if="bannerForm.mediaType === 'video'" class="home-layout__hint">
              影片模式仍必須提供一張圖片，作為影片載入前與行動網路關閉自動播放時顯示的海報畫面。
            </p>
          </el-form-item>

          <el-form-item v-if="bannerForm.mediaType === 'video'" label="輪播影片" required>
            <VideoUploader
              v-model:file="bannerVideoFile"
              :has-existing-video="bannerHasExistingVideo"
              :disabled="bannerSaving"
            />
          </el-form-item>

          <BilingualShortField
            label="標題"
            :zh="bannerForm.titleZh"
            :en="bannerForm.titleEn"
            @update:zh="(v) => (bannerForm.titleZh = v)"
            @update:en="(v) => (bannerForm.titleEn = v)"
          />
          <BilingualShortField
            label="副標題"
            :zh="bannerForm.subtitleZh"
            :en="bannerForm.subtitleEn"
            @update:zh="(v) => (bannerForm.subtitleZh = v)"
            @update:en="(v) => (bannerForm.subtitleEn = v)"
          />
          <BilingualShortField
            label="圖片替代文字"
            :zh="bannerForm.altZh"
            :en="bannerForm.altEn"
            placeholder="給看不到圖片的使用者與搜尋引擎的文字說明"
            @update:zh="(v) => (bannerForm.altZh = v)"
            @update:en="(v) => (bannerForm.altEn = v)"
          />

          <el-row :gutter="12">
            <el-col :span="12">
              <BilingualShortField
                label="按鈕一文字"
                :zh="bannerForm.cta1LabelZh"
                :en="bannerForm.cta1LabelEn"
                @update:zh="(v) => (bannerForm.cta1LabelZh = v)"
                @update:en="(v) => (bannerForm.cta1LabelEn = v)"
              />
            </el-col>
            <el-col :span="12">
              <el-form-item label="按鈕一連結">
                <el-input v-model="bannerForm.cta1Url" placeholder="/zh/..." />
              </el-form-item>
            </el-col>
          </el-row>
          <el-row :gutter="12">
            <el-col :span="12">
              <BilingualShortField
                label="按鈕二文字"
                :zh="bannerForm.cta2LabelZh"
                :en="bannerForm.cta2LabelEn"
                @update:zh="(v) => (bannerForm.cta2LabelZh = v)"
                @update:en="(v) => (bannerForm.cta2LabelEn = v)"
              />
            </el-col>
            <el-col :span="12">
              <el-form-item label="按鈕二連結">
                <el-input v-model="bannerForm.cta2Url" placeholder="/zh/..." />
              </el-form-item>
            </el-col>
          </el-row>

          <el-row :gutter="12">
            <el-col :span="16">
              <el-form-item label="上架期間（不設定＝不限期間）">
                <div class="home-layout__date-range">
                  <el-date-picker v-model="bannerForm.startAt" type="datetime" placeholder="上架時間" style="width: 100%" />
                  <span>～</span>
                  <el-date-picker v-model="bannerForm.endAt" type="datetime" placeholder="下架時間" style="width: 100%" />
                </div>
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="排序">
                <el-input-number v-model="bannerForm.sortOrder" :min="0" style="width: 100%" />
              </el-form-item>
            </el-col>
          </el-row>
        </el-form>
      </template>
      <template #footer>
        <el-button @click="closeBannerDialog">取消</el-button>
        <el-button type="primary" :loading="bannerSaving" @click="saveBanner">儲存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.home-layout__section {
  margin-bottom: 16px;
}

.home-layout__card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.home-layout__form-error {
  margin-bottom: 12px;
}

.home-layout__hint {
  margin: 8px 0 0;
  font-size: 12px;
  color: var(--admin-text-tertiary);
  line-height: 1.6;
}

.home-layout__muted {
  font-size: 12px;
  color: var(--admin-text-tertiary);
}

.home-layout__date-range {
  display: flex;
  align-items: center;
  gap: 8px;
}
</style>

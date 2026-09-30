<script setup lang="ts">
/**
 * 漫畫集數——新增／編輯，含封面與內頁上傳排序、發布下架。
 * 新集數要先儲存為草稿、上傳至少一張內頁後才能發布；已發布的集數不能把內頁刪光。
 * 內頁的新增、移除、排序立即生效（每張圖一支獨立端點），封面與其他欄位按「儲存」才送出。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import GalleryManager from '@/components/GalleryManager.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { useClubFeatures } from '@/composables/useClubFeatures'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { enOrUndefined } from '@/api/adminCommon'
import {
  addMangaPages,
  createMangaEpisode,
  deleteMangaPage,
  getMangaEpisode,
  reorderMangaPages,
  updateMangaEpisode,
  type MangaEpisodeDetailDto,
  type MangaEpisodeStatus,
} from '@/api/adminManga'

const route = useRoute()
const router = useRouter()
const isCreate = computed(() => route.name === 'manga-episode-new')
const episodeId = ref<string | undefined>(route.params.id as string | undefined)
const { canCreate, canUpdate } = useCrudPermissions('culture.comic')
const { comicAvailable } = useClubFeatures()
const club = computed(() => activeClubId.value)

const form = reactive({
  episodeNo: 1,
  publishedOn: '',
  status: 'draft' as MangaEpisodeStatus,
  titleZh: '',
  titleEn: '',
})
const baselineJson = ref('')
const coverFile = ref<File | null>(null)
const removeCover = ref(false)
const coverUrl = ref<string | null>(null)
const hasCover = ref(false)
const pages = ref<MangaEpisodeDetailDto['pages']>([])
const meta = reactive({ isLatest: false, viewCount: 0 })

const loadState = ref<'loading' | 'ready' | 'error' | 'not-found' | 'unavailable'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
const formError = ref<string | null>(null)
const readOnly = computed(() => (isCreate.value ? !canCreate.value : !canUpdate.value))
const pageTitle = computed(() => (isCreate.value ? '新增集數' : `編輯：第 ${form.episodeNo} 集`))

function apply(d: MangaEpisodeDetailDto) {
  form.episodeNo = d.episodeNo
  form.publishedOn = d.publishedOn ?? ''
  form.status = d.status
  form.titleZh = d.zh?.title ?? ''
  form.titleEn = d.en?.title ?? ''
  coverUrl.value = d.coverThumbUrl ?? d.coverUrl ?? null
  hasCover.value = !!d.coverKey
  pages.value = d.pages ?? []
  meta.isLatest = d.isLatest
  meta.viewCount = d.viewCount
}

async function load() {
  if (!comicAvailable.value) {
    loadState.value = 'unavailable'
    return
  }
  loadState.value = 'loading'
  try {
    if (!isCreate.value && episodeId.value) apply(await getMangaEpisode(club.value, episodeId.value))
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

const isDirty = computed(() => loadState.value === 'ready' && (JSON.stringify(form) !== baselineJson.value || !!coverFile.value || removeCover.value))
useUnsavedChanges(isDirty)

function validate(): string | null {
  if (!form.titleZh.trim()) return '請輸入中文標題'
  if (form.titleZh.trim().length > 128 || form.titleEn.trim().length > 128) return '標題最多 128 字'
  if (!Number.isInteger(form.episodeNo) || form.episodeNo < 1) return '集數必須是 1 以上的整數'
  if (isCreate.value && form.status === 'published') return '新集數還沒有內頁，請先存成草稿、上傳內頁後再發布'
  if (form.status === 'published' && pages.value.length === 0) return '發布前必須至少有一張內頁，請先上傳'
  return null
}

async function handleSave() {
  if (readOnly.value) return
  formError.value = validate()
  if (formError.value) return
  saving.value = true
  const payload = {
    episodeNo: form.episodeNo,
    publishedOn: form.publishedOn || null,
    status: form.status,
    removeCover: coverFile.value ? false : removeCover.value,
    content: {
      zh: { title: form.titleZh.trim() },
      en: enOrUndefined({ title: form.titleEn.trim() }, 'title'),
    },
  }
  try {
    const wasCreate = isCreate.value
    const saved = wasCreate
      ? await createMangaEpisode(club.value, payload, coverFile.value)
      : await updateMangaEpisode(club.value, episodeId.value!, payload, coverFile.value)
    if (wasCreate) {
      episodeId.value = saved.id
      router.replace(`/culture/manga/episodes/${saved.id}/edit`)
    }
    apply(saved)
    coverFile.value = null
    removeCover.value = false
    baselineJson.value = JSON.stringify(form)
    ElMessage.success(wasCreate ? '已建立，接著可以上傳內頁' : '已儲存')
  } catch (error) {
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

function back() {
  router.push({ path: '/culture/manga', query: { tab: 'episodes' } })
}

async function refreshPages() {
  if (!episodeId.value) return
  const d = await getMangaEpisode(club.value, episodeId.value)
  pages.value = d.pages ?? []
  meta.isLatest = d.isLatest
}
async function pageUpload(file: File) {
  await addMangaPages(club.value, episodeId.value!, [file])
  await refreshPages()
}
async function pageRemove(id: string) {
  await deleteMangaPage(club.value, episodeId.value!, id)
  await refreshPages()
}
async function pageReorder(ids: string[]) {
  await reorderMangaPages(club.value, episodeId.value!, ids)
  await refreshPages()
}
</script>

<template>
  <div class="episode-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="back"><el-icon><ArrowLeft /></el-icon>返回列表</el-button>
      </template>
      <template #meta><FrontendUnitBanner module-code="F1" /></template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'unavailable'">台中藍鯨不設漫畫，這個功能只在台中磐石使用。</p>
          <p v-else-if="loadState === 'not-found'">找不到這一集，可能已被刪除，或不屬於目前選擇的俱樂部。</p>
          <p v-else>{{ loadErrorMessage }}</p>
        </template>
        <el-button v-if="loadState === 'error'" type="primary" @click="load">重新載入</el-button>
        <el-button v-else type="primary" @click="back">返回列表</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="episode-edit__block" @close="formError = null" />
      <el-alert v-if="readOnly" title="你的帳號只能檢視漫畫，不能修改。" type="info" show-icon :closable="false" class="episode-edit__block" />
      <el-form label-position="top" :disabled="readOnly">
        <el-card shadow="never" class="episode-edit__block">
          <template #header>
            <span>集數資料</span>
            <el-tag v-if="meta.isLatest" size="small" type="warning" class="episode-edit__tag">最新一集（系統自動判定）</el-tag>
            <span v-if="!isCreate" class="episode-edit__views">閱讀數 {{ meta.viewCount }}</span>
          </template>
          <BilingualShortField label="標題" :zh="form.titleZh" :en="form.titleEn" required @update:zh="(v) => (form.titleZh = v)" @update:en="(v) => (form.titleEn = v)" />
          <el-row :gutter="12">
            <el-col :xs="24" :sm="8">
              <el-form-item label="集數" required>
                <el-input-number v-model="form.episodeNo" :min="1" :step="1" style="width: 100%" />
              </el-form-item>
            </el-col>
            <el-col :xs="24" :sm="8">
              <el-form-item label="發布日">
                <el-date-picker v-model="form.publishedOn" type="date" value-format="YYYY-MM-DD" placeholder="發布時沒填就用今天" style="width: 100%" />
              </el-form-item>
            </el-col>
            <el-col :xs="24" :sm="8">
              <el-form-item label="狀態">
                <el-radio-group v-model="form.status">
                  <el-radio-button value="draft">草稿</el-radio-button>
                  <el-radio-button value="published">已發布</el-radio-button>
                </el-radio-group>
              </el-form-item>
            </el-col>
          </el-row>
          <p class="episode-edit__hint">同一個俱樂部的集數不能重複。發布必須至少有一張內頁；改回「草稿」就是下架。全部集數免費公開閱讀。</p>
        </el-card>

        <el-card shadow="never" header="封面圖片" class="episode-edit__block">
          <ImageUploader v-model:file="coverFile" v-model:remove-cover="removeCover" :min-width="0" :min-height="0" :has-existing-image="hasCover" :existing-preview-url="coverUrl" :disabled="saving || readOnly" />
        </el-card>
      </el-form>

      <el-card shadow="never" class="episode-edit__block">
        <template #header>內頁（依順序閱讀）</template>
        <p v-if="isCreate" class="episode-edit__hint">請先按「儲存」建立這一集，儲存後就能上傳內頁。</p>
        <template v-else>
          <p class="episode-edit__hint">目前 {{ pages.length }} 頁。已發布的集數不能把內頁刪光；一次最多上傳 60 張。</p>
          <GalleryManager :images="pages.map((p) => ({ id: p.id, thumbUrl: p.imageThumbUrl, imageUrl: p.imageUrl }))" :disabled="!canUpdate" :on-upload="pageUpload" :on-remove="pageRemove" :on-reorder="pageReorder" />
        </template>
      </el-card>
      <EditActionBar v-if="!readOnly"><el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button></EditActionBar>
    </template>
  </div>
</template>

<style scoped>
.episode-edit { max-width: 820px; margin: 0 auto 88px; }
.episode-edit__block { margin-bottom: 16px; }
.episode-edit__hint { margin: 4px 0 10px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.episode-edit__tag { margin-left: 8px; }
.episode-edit__views { margin-left: 12px; font-size: 12px; color: var(--admin-text-tertiary); }
</style>

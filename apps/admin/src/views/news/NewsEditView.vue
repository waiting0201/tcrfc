<script setup lang="ts">
import { computed, reactive, ref, shallowRef, toRaw } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import StatusTag from '@/components/StatusTag.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { getNewsById, upsertNews, createEmptyArticle } from '@/data/newsStore'
import { NEWS_CATEGORY_LABEL, type NewsArticle, type NewsCategory } from '@/types/news'
import type { ContentStatus } from '@/types/common'

const route = useRoute()
const router = useRouter()

const isCreate = route.name === 'news-new'
const paramId = route.params.id as string | undefined

// ⚠️ getNewsById 回傳的是 newsStore（reactive）裡的項目，本身是一個 Vue reactive Proxy。
// 瀏覽器原生 structuredClone 沒辦法複製 Proxy（會丟 DataCloneError），要先用 toRaw() 拿回
// 未包裝的原始物件才能複製。baseline 用 shallowRef 而不是 ref，同樣是為了不讓 Vue 把這份
// 「用來比對是否變更過」的快照本身也變成深層 reactive Proxy。
const existing = !isCreate && paramId ? getNewsById(paramId) : undefined
const initialArticle = existing ? structuredClone(toRaw(existing)) : createEmptyArticle()
const baseline = shallowRef<NewsArticle>(initialArticle)
const form = reactive<NewsArticle>(structuredClone(initialArticle))

const pendingImageFile = ref<File | null>(null)
const saving = ref(false)
const imageSaveError = ref<string | null>(null)
const scheduleDialogVisible = ref(false)
const scheduleDateTime = ref<Date | null>(null)

const isDirty = computed(
  () => JSON.stringify(form) !== JSON.stringify(baseline.value) || pendingImageFile.value !== null,
)
useUnsavedChanges(isDirty)

const pageTitle = computed(() => (isCreate ? '新增文章' : '編輯文章'))

const mainActionLabel = computed(() => (form.status === 'draft' ? '發布' : '儲存變更'))

const canPreview = computed(() => form.status === 'published')
const previewUrl = computed(() => (canPreview.value ? `/zh/news/${form.urlName}/` : undefined))

function nowString(): string {
  return new Date().toISOString().slice(0, 16).replace('T', ' ')
}

function persist(nextStatus?: ContentStatus, scheduledAt?: string) {
  saving.value = true
  imageSaveError.value = null

  // 模擬「按下儲存才真的上傳」：這裡沒有真的後端，用假的非同步延遲＋
  // 用選檔時已經產生的預覽 URL 代替「上傳完成後拿到的正式圖片網址」
  window.setTimeout(() => {
    if (pendingImageFile.value) {
      form.coverImageUrl = URL.createObjectURL(pendingImageFile.value)
    }
    if (nextStatus === 'published') {
      form.status = 'published'
      form.statusAt = nowString()
      form.statusBy = undefined
    } else if (nextStatus === 'scheduled' && scheduledAt) {
      form.status = 'scheduled'
      form.statusAt = scheduledAt
    }
    form.updatedAt = nowString()

    const saved = structuredClone(toRaw(form))
    upsertNews(saved)
    baseline.value = structuredClone(saved)
    Object.assign(form, structuredClone(saved))
    pendingImageFile.value = null
    saving.value = false

    ElMessage.success(nextStatus === 'published' ? '已發布' : nextStatus === 'scheduled' ? '已排程發布' : '已儲存')

    if (isCreate) {
      router.replace(`/content/news/${saved.id}/edit`)
    }
  }, 500)
}

function handleSaveDraft() {
  persist()
}

function handleMainAction() {
  if (form.status === 'draft') {
    persist('published')
  } else {
    persist()
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
  const iso = scheduleDateTime.value.toISOString().slice(0, 16).replace('T', ' ')
  scheduleDialogVisible.value = false
  persist('scheduled', iso)
}

function handleBack() {
  router.push('/content/news')
}

function handlePreview() {
  if (!previewUrl.value) return
  window.open(previewUrl.value, '_blank', 'noopener')
}

function handleImageFile(file: File | null) {
  pendingImageFile.value = file
  if (!file) form.coverImageUrl = existing?.coverImageUrl ?? null
}
</script>

<template>
  <div class="news-edit">
    <div class="news-edit__header">
      <div class="news-edit__header-top">
        <el-button text @click="handleBack">
          <el-icon><ArrowLeft /></el-icon>
          返回列表
        </el-button>
        <h1 class="news-edit__title">{{ pageTitle }}</h1>
        <FrontendUnitBanner
          module-code="B2"
          :record-published="form.status === 'published'"
          :record-url="previewUrl"
        />
      </div>
      <div class="news-edit__status-line">
        <StatusTag :status="form.status" :status-at="form.statusAt" :status-by="form.statusBy" />
        <span v-if="form.isSharedContent" class="news-edit__shared-note">
          <el-tag type="info" size="small">共用內容（唯讀）</el-tag>
          這是兩隊共用的內容，你的帳號僅能檢視
        </span>
      </div>
    </div>

    <el-form label-position="top" class="news-edit__form">
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

        <el-form-item label="網址名稱" required>
          <el-input v-model="form.urlName" placeholder="例如：tcrfc-vs-trencin-2026" />
        </el-form-item>
      </el-card>

      <el-card shadow="never" header="內容" class="news-edit__section">
        <el-tabs class="news-edit__content-tabs">
          <el-tab-pane label="中文內容">
            <el-input
              v-model="form.content.zh"
              type="textarea"
              :rows="10"
              placeholder="請輸入中文內容（此 mockup 以文字框代替正式的富文本編輯器）"
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
        <div class="news-edit__image-row">
          <ImageUploader
            :existing-url="form.coverImageUrl"
            :saving="saving"
            :save-error="imageSaveError"
            @update:file="handleImageFile"
          />
          <div class="news-edit__image-alt">
            <BilingualShortField
              label="圖片說明文字"
              :zh="form.coverImageAlt.zh"
              :en="form.coverImageAlt.en"
              :required="Boolean(form.coverImageUrl)"
              placeholder="描述這張圖片的內容"
              @update:zh="(v) => (form.coverImageAlt.zh = v)"
              @update:en="(v) => (form.coverImageAlt.en = v)"
            />
          </div>
        </div>
      </el-card>

      <el-card shadow="never" header="發布設定" class="news-edit__section">
        <el-form-item label="不讓搜尋引擎收錄">
          <el-switch v-model="form.noIndex" />
        </el-form-item>
        <el-form-item label="正規網址（選填）">
          <el-input v-model="form.canonicalUrl" placeholder="https://www.tcrfc.tw/..." />
        </el-form-item>
      </el-card>
    </el-form>

    <div class="news-edit__action-bar">
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

.news-edit__header-top {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.news-edit__title {
  font-size: 18px;
  margin: 0;
  flex: 1;
}

.news-edit__status-line {
  display: flex;
  align-items: center;
  gap: 12px;
  margin: 8px 0 16px;
  padding-left: 4px;
  font-size: 13px;
  color: var(--el-text-color-secondary);
  flex-wrap: wrap;
}

.news-edit__shared-note {
  display: flex;
  align-items: center;
  gap: 6px;
}

.news-edit__section {
  margin-bottom: 16px;
}

.news-edit__image-row {
  display: flex;
  gap: 24px;
  flex-wrap: wrap;
}

.news-edit__image-alt {
  flex: 1;
  min-width: 240px;
}

.news-edit__action-bar {
  position: fixed;
  bottom: 0;
  left: var(--admin-sidebar-width-expanded);
  right: 0;
  background: #fff;
  border-top: 1px solid var(--el-border-color-lighter);
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

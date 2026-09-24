<script setup lang="ts">
/**
 * B4 常見問題——題目編輯頁。對照 apps/api/README.md「S1-6」「S1-7a」。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { activeClubId } from '@/auth/clubAccess'
import {
  createAdminFaq,
  getAdminFaq,
  listAdminFaqCategories,
  listAdminFaqEmbedSlots,
  updateAdminFaq,
  type AdminFaqCategoryListItemDto,
  type AdminFaqEmbedSlotDto,
} from '@/api/adminFaq'
import { AdminApiError } from '@/api/http'

const route = useRoute()
const router = useRouter()

const isCreate = route.name === 'faq-new'
const faqId = ref<string | undefined>(route.params.id as string | undefined)

const form = reactive({
  slug: '',
  categoryIds: [] as string[],
  sortOrder: 0,
  status: 'draft' as 'draft' | 'published',
  embedSlotIds: [] as string[],
  questionZh: '',
  questionEn: '',
  answerZh: '',
  answerEn: '',
})
const baselineJson = ref('')
const isShared = ref(false)
const viewCount = ref(0)
const helpfulCount = ref(0)
const unhelpfulCount = ref(0)

const categories = ref<AdminFaqCategoryListItemDto[]>([])
const embedSlots = ref<AdminFaqEmbedSlotDto[]>([])

const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
const formError = ref<string | null>(null)

async function loadLookups() {
  const [categoryList, embedSlotList] = await Promise.all([listAdminFaqCategories(), listAdminFaqEmbedSlots()])
  categories.value = categoryList.sort((a, b) => a.sortOrder - b.sortOrder)
  embedSlots.value = embedSlotList
}

async function loadFaq() {
  loadState.value = 'loading'
  try {
    await loadLookups()
    if (!isCreate && faqId.value) {
      const detail = await getAdminFaq(activeClubId.value, faqId.value)
      form.slug = detail.slug
      form.categoryIds = detail.categories.map((c) => c.id)
      form.sortOrder = detail.sortOrder
      form.status = detail.status
      form.embedSlotIds = detail.embedSlots.map((s) => s.id)
      form.questionZh = detail.zh.question ?? ''
      form.questionEn = detail.en?.question ?? ''
      form.answerZh = detail.zh.answer ?? ''
      form.answerEn = detail.en?.answer ?? ''
      isShared.value = detail.isShared
      viewCount.value = detail.viewCount
      helpfulCount.value = detail.helpfulCount
      unhelpfulCount.value = detail.unhelpfulCount
    }
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

onMounted(loadFaq)

const isDirty = computed(() => loadState.value === 'ready' && JSON.stringify(form) !== baselineJson.value)
useUnsavedChanges(isDirty)

const isReadOnly = computed(() => loadState.value === 'ready' && isShared.value)
const pageTitle = computed(() => (isCreate ? '新增題目' : '編輯題目'))

function isEnEmpty(): boolean {
  return !form.questionEn.trim() && !form.answerEn.trim()
}

function validate(): boolean {
  formError.value = null
  if (!form.slug.trim()) {
    formError.value = '請輸入網址名稱'
    return false
  }
  if (form.categoryIds.length === 0) {
    formError.value = '請至少選擇一個分類（沒有分類的題目在前台主題導覽完全找不到）'
    return false
  }
  if (!form.questionZh.trim() || !form.answerZh.trim()) {
    formError.value = '請輸入中文問題與答案'
    return false
  }
  return true
}

async function handleSave() {
  if (isReadOnly.value) return
  if (!validate()) return
  saving.value = true
  try {
    const payload = {
      slug: form.slug.trim(),
      categoryIds: form.categoryIds,
      sortOrder: form.sortOrder,
      status: form.status,
      embedSlotIds: form.embedSlotIds,
      content: {
        zh: { question: form.questionZh, answer: form.answerZh },
        en: isEnEmpty() ? undefined : { question: form.questionEn || null, answer: form.answerEn || null },
      },
    }
    if (isCreate) {
      const created = await createAdminFaq(activeClubId.value, payload)
      ElMessage.success('已建立')
      router.replace(`/content/faq/${created.id}/edit`)
      faqId.value = created.id
    } else {
      await updateAdminFaq(activeClubId.value, faqId.value!, payload)
      ElMessage.success('已儲存')
    }
    baselineJson.value = JSON.stringify(form)
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'forbidden') {
      await ElMessageBox.alert(error.message, '沒有編輯權限', { confirmButtonText: '我知道了' })
    } else {
      formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
    }
  } finally {
    saving.value = false
  }
}

function handleBack() {
  router.push('/content/faq')
}

function retryLoad() {
  loadFaq()
}
</script>

<template>
  <div class="faq-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="handleBack">
          <el-icon><ArrowLeft /></el-icon>
          返回列表
        </el-button>
      </template>
      <template #meta>
        <FrontendUnitBanner module-code="B4" />
        <el-tag v-if="loadState === 'ready' && isShared" type="info" size="small">共用內容（唯讀）</el-tag>
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never">
      <el-skeleton :rows="8" animated />
    </el-card>

    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這一題，可能已經被刪除，或不屬於目前選擇的俱樂部。</p>
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
        class="faq-edit__form-error"
        @close="formError = null"
      />

      <el-form label-position="top" :disabled="isReadOnly">
        <el-card shadow="never" header="基本資訊" class="faq-edit__section">
          <el-form-item label="網址名稱" required>
            <el-input v-model="form.slug" placeholder="例如 how-to-join" />
          </el-form-item>
          <el-form-item label="所屬分類（可複選）" required>
            <el-select v-model="form.categoryIds" multiple filterable placeholder="請選擇分類" style="width: 100%">
              <el-option v-for="c in categories" :key="c.id" :label="c.nameZh || c.slug" :value="c.id" />
            </el-select>
          </el-form-item>
          <el-row :gutter="12">
            <el-col :span="12">
              <el-form-item label="排序">
                <el-input-number v-model="form.sortOrder" :min="0" style="width: 100%" />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="狀態">
                <el-radio-group v-model="form.status">
                  <el-radio value="published">顯示</el-radio>
                  <el-radio value="draft">隱藏</el-radio>
                </el-radio-group>
              </el-form-item>
            </el-col>
          </el-row>
        </el-card>

        <el-card shadow="never" header="題目內容" class="faq-edit__section">
          <BilingualShortField
            label="問題"
            :zh="form.questionZh"
            :en="form.questionEn"
            required
            @update:zh="(v) => (form.questionZh = v)"
            @update:en="(v) => (form.questionEn = v)"
          />
          <BilingualTextareaField
            label="答案"
            :zh="form.answerZh"
            :en="form.answerEn"
            required
            :rows="6"
            placeholder="可包含連結、圖片或檔案的說明文字（此畫面以文字框代替正式的富文本編輯器）"
            @update:zh="(v) => (form.answerZh = v)"
            @update:en="(v) => (form.answerEn = v)"
          />
        </el-card>

        <el-card shadow="never" header="嵌入設定" class="faq-edit__section">
          <p class="faq-edit__hint">
            這題會依所屬分類自動出現在對應頁面的常見問題快捷區塊；下方可以額外指定這題也出現在其他掛載點（疊加，不是取代）。
          </p>
          <el-form-item label="額外指定出現的頁面">
            <el-select v-model="form.embedSlotIds" multiple placeholder="不指定即可（維持只依分類自動對應）" style="width: 100%">
              <el-option v-for="slot in embedSlots" :key="slot.id" :label="slot.name" :value="slot.id" />
            </el-select>
          </el-form-item>
        </el-card>

        <el-card v-if="!isCreate" shadow="never" header="成效數據" class="faq-edit__section">
          <div class="faq-edit__stats">
            <span>瀏覽數：{{ viewCount.toLocaleString('zh-Hant') }}</span>
            <span>👍 有幫助：{{ helpfulCount.toLocaleString('zh-Hant') }}</span>
            <span>👎 沒有幫助：{{ unhelpfulCount.toLocaleString('zh-Hant') }}</span>
          </div>
        </el-card>
      </el-form>

      <div v-if="!isReadOnly" class="faq-edit__action-bar">
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.faq-edit {
  max-width: 780px;
  margin: 0 auto 88px;
}

.faq-edit__form-error {
  margin-bottom: 16px;
}

.faq-edit__section {
  margin-bottom: 16px;
}

.faq-edit__hint {
  margin: 0 0 12px;
  font-size: 12px;
  color: var(--admin-text-tertiary);
  line-height: 1.6;
}

.faq-edit__stats {
  display: flex;
  gap: 24px;
  font-size: 14px;
  color: var(--admin-text-secondary);
}

.faq-edit__action-bar {
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
  .faq-edit__action-bar {
    left: 0;
  }
}

@media (max-width: 767px) {
  .faq-edit__action-bar {
    justify-content: stretch;
  }

  .faq-edit__action-bar :deep(.el-button) {
    flex: 1;
  }
}
</style>

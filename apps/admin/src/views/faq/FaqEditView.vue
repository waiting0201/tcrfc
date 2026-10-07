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
import EditActionBar from '@/components/EditActionBar.vue'
import FormErrorStatus from '@/components/FormErrorStatus.vue'
import FormField from '@/components/FormField.vue'
import FormSection from '@/components/FormSection.vue'
import EditLayout from '@/components/EditLayout.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
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

// 🔴 必須是 computed 不能是一次性求值的 const：建立成功後 handleSave() 呼叫
// `router.replace('/content/faq/:id/edit')`，Vue Router 對同一個元件實例的路由切換預設不會
// 重新掛載（component reuse），若 isCreate 只在 setup 當下算一次，之後緊接著再按一次「儲存」
// 會誤判成仍在建立模式，重複呼叫 createAdminFaq 產生第二筆重複資料。比照
// `CompetitionEditView.vue`／`MatchEditView.vue` 既有寫法（`docs/18` 回報項）。
const isCreate = computed(() => route.name === 'faq-new')
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
/** 頁首提示：只放「沒有對到欄位」的錯誤；欄位錯誤標在欄位上（formErrors）。 */
const formError = ref<string | null>(null)
const formErrors = provideFormErrors()

async function loadLookups() {
  const [categoryList, embedSlotList] = await Promise.all([listAdminFaqCategories(), listAdminFaqEmbedSlots()])
  categories.value = categoryList.sort((a, b) => a.sortOrder - b.sortOrder)
  embedSlots.value = embedSlotList
}

async function loadFaq() {
  loadState.value = 'loading'
  try {
    await loadLookups()
    if (!isCreate.value && faqId.value) {
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
const pageTitle = computed(() => (isCreate.value ? '新增題目' : '編輯題目'))

function isEnEmpty(): boolean {
  return !form.questionEn.trim() && !form.answerEn.trim()
}

/** 一次檢查全部必填，回傳 欄位鍵 → 訊息（鍵只在程式內對照，不顯示）。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!form.slug.trim()) errors.slug = '請輸入網址名稱'
  if (form.categoryIds.length === 0) errors.categoryIds = '請至少選擇一個分類（沒有分類的題目在前台主題導覽完全找不到）'
  if (!form.questionZh.trim()) errors.questionZh = '請輸入中文問題'
  if (!form.answerZh.trim()) errors.answerZh = '請輸入中文答案'
  return errors
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
    if (isCreate.value) {
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
    } else if (error instanceof AdminApiError && formErrors.applyApiError(error)) {
      return
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
        <LangTabsBar>
          <EditLayout>
            <template #main>
              <el-card shadow="never">
                <FormSection>
                  <BilingualShortField
                    field="question"
                    label="問題"
                    :zh="form.questionZh"
                    :en="form.questionEn"
                    required
                    @update:zh="(v) => (form.questionZh = v)"
                    @update:en="(v) => (form.questionEn = v)"
                  />
                  <BilingualTextareaField
                    field="answer"
                    label="答案"
                    :zh="form.answerZh"
                    :en="form.answerEn"
                    required
                    :rows="6"
                    placeholder="可包含連結、圖片或檔案的說明文字（此畫面以文字框代替正式的富文本編輯器）"
                    @update:zh="(v) => (form.answerZh = v)"
                    @update:en="(v) => (form.answerEn = v)"
                  />
                </FormSection>
              </el-card>
            </template>

            <template #aside>
              <el-card shadow="never" header="基本設定">
                <FormSection>
                  <FormField field="slug" label="網址名稱" required>
                    <el-input v-model="form.slug" placeholder="例如 how-to-join" />
                  </FormField>
                  <FormField field="categoryIds" label="所屬分類（可複選）" required>
                    <el-select
                      v-model="form.categoryIds"
                      multiple
                      filterable
                      placeholder="請選擇分類"
                      style="width: 100%"
                      @change="formErrors.clear('categoryIds')"
                    >
                      <el-option v-for="c in categories" :key="c.id" :label="c.nameZh || c.slug" :value="c.id" />
                    </el-select>
                  </FormField>
                </FormSection>
                <FormSection title="嵌入設定">
                  <p class="faq-edit__hint">
                    這題會依所屬分類自動出現在對應頁面的常見問題快捷區塊；下方可以額外指定這題也出現在其他掛載點（疊加，不是取代）。
                  </p>
                  <FormField field="embedSlotIds" label="額外指定出現的頁面">
                    <el-select v-model="form.embedSlotIds" multiple placeholder="不指定即可（維持只依分類自動對應）" style="width: 100%">
                      <el-option v-for="slot in embedSlots" :key="slot.id" :label="slot.name" :value="slot.id" />
                    </el-select>
                  </FormField>
                </FormSection>
              </el-card>
              <el-card shadow="never" header="發布設定">
                <FormSection>
                  <el-form-item label="排序">
                    <el-input-number v-model="form.sortOrder" :min="0" style="width: 100%" />
                  </el-form-item>
                  <FormField field="status" label="狀態">
                    <el-radio-group v-model="form.status">
                      <el-radio value="published">顯示</el-radio>
                      <el-radio value="draft">隱藏</el-radio>
                    </el-radio-group>
                  </FormField>
                </FormSection>
                <FormSection v-if="!isCreate" title="成效數據">
                  <div class="faq-edit__stats">
                    <span>瀏覽數：{{ viewCount.toLocaleString('zh-Hant') }}</span>
                    <span>👍 有幫助：{{ helpfulCount.toLocaleString('zh-Hant') }}</span>
                    <span>👎 沒有幫助：{{ unhelpfulCount.toLocaleString('zh-Hant') }}</span>
                  </div>
                </FormSection>
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
.faq-edit {
  max-width: 780px;
  margin: 0 auto 88px;
}

.faq-edit__form-error {
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
</style>

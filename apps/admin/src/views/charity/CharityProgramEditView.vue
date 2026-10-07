<script setup lang="ts">
/**
 * 慈善計畫——新增／編輯（前台慈善計畫列表與詳情）。
 * 「緣起與內容」以純文字編輯（空行分段）；前台慈善計畫詳情不渲染頁面區塊格式，所以不用區塊編輯器。
 * 舊資料若是區塊格式：顯示成純文字，沒改就原樣送回、改了才改存純文字（見 utils/charityContent.ts）。
 * 共用計畫整頁唯讀。
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
import SharedContentNotice from '@/components/SharedContentNotice.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import EditLayout from '@/components/EditLayout.vue'
import FormErrorStatus from '@/components/FormErrorStatus.vue'
import FormField from '@/components/FormField.vue'
import FormSection from '@/components/FormSection.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { enOrUndefined, nullIfBlank } from '@/api/adminCommon'
import { decodeCharityContent, encodeCharityContent, type ContentField } from '@/utils/charityContent'
import NewsPicker from '@/components/NewsPicker.vue'
import { listPartners } from '@/api/adminPartners'
import { listSponsors } from '@/api/adminSponsors'
import {
  addProgramImage,
  createProgram,
  deleteProgramImage,
  getProgram,
  listOrgs,
  reorderProgramImages,
  updateProgram,
  type GalleryImageDto,
  type LinkRefDto,
  type OrgListItemDto,
  type ProgramDetailDto,
} from '@/api/adminCharity'

interface Option { id: string; label: string }

const route = useRoute()
const router = useRouter()
const isCreate = computed(() => route.name === 'charity-program-new')
const programId = ref<string | undefined>(route.params.id as string | undefined)
const { canCreate, canUpdate } = useCrudPermissions('charity.content')
const club = computed(() => activeClubId.value)

const form = reactive({
  charityId: '', status: 'draft' as 'draft' | 'published', startOn: '', endOn: '', sortOrder: 0, isPinned: false,
  nameZh: '', nameEn: '', audienceZh: '', audienceEn: '', contentZh: '', contentEn: '', donationZh: '', donationEn: '',
  partnerIds: [] as string[], sponsorIds: [] as string[], articleIds: [] as string[],
})
const baselineJson = ref('')
const coverFile = ref<File | null>(null)
const removeCover = ref(false)
const coverUrl = ref<string | null>(null)
const hasCover = ref(false)
const gallery = ref<GalleryImageDto[]>([])
const isShared = ref(false)
const progress = ref<'ongoing' | 'completed'>('ongoing')
const orgs = ref<OrgListItemDto[]>([])
const partnerOptions = ref<Option[]>([])
const sponsorOptions = ref<Option[]>([])
const articleSeed = ref<{ id: string; label: string }[]>([])
const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
/** 頁首提示：只放沒有欄位歸屬的錯誤；欄位錯誤標在欄位上（formErrors）。 */
const formError = ref<string | null>(null)
const formErrors = provideFormErrors()
// 選檔或移除封面就清掉該欄位的錯誤
watch([coverFile, removeCover], () => formErrors.clear('cover'))
const readOnly = computed(() => isShared.value || (isCreate.value ? !canCreate.value : !canUpdate.value))
const pageTitle = computed(() => (isCreate.value ? '新增慈善計畫' : `${readOnly.value ? '檢視' : '編輯'}：${form.nameZh || '（未命名）'}`))

/** 緣起與內容（中英各一份）：文字框綁 `contentZh/contentEn`，其餘狀態見 utils/charityContent.ts。 */
const contentZhState = ref<ContentField | null>(null)
const contentEnState = ref<ContentField | null>(null)
function contentField(lang: 'zh' | 'en'): ContentField {
  const base = (lang === 'zh' ? contentZhState : contentEnState).value ?? decodeCharityContent(null, lang)
  return { ...base, text: lang === 'zh' ? form.contentZh : form.contentEn }
}
const structuredZh = computed(() => !!contentZhState.value?.structured)
const structuredEn = computed(() => !!contentEnState.value?.structured)

function mergeOptions(target: Option[], refs: LinkRefDto[]) {
  for (const r of refs) if (!target.some((o) => o.id === r.id)) target.push({ id: r.id, label: r.title || r.slug })
}

function apply(d: ProgramDetailDto) {
  form.charityId = d.charityId
  form.status = d.status
  form.startOn = d.startOn ?? ''
  form.endOn = d.endOn ?? ''
  form.sortOrder = d.sortOrder
  form.isPinned = d.isPinned
  form.nameZh = d.zh.name ?? ''
  form.nameEn = d.en?.name ?? ''
  form.audienceZh = d.zh.targetAudience ?? ''
  form.audienceEn = d.en?.targetAudience ?? ''
  form.donationZh = d.zh.donationContent ?? ''
  form.donationEn = d.en?.donationContent ?? ''
  contentZhState.value = decodeCharityContent(d.zh.content, 'zh')
  contentEnState.value = decodeCharityContent(d.en?.content, 'en')
  form.contentZh = contentZhState.value.text
  form.contentEn = contentEnState.value.text
  form.partnerIds = d.partners.map((x) => x.id)
  form.sponsorIds = d.sponsors.map((x) => x.id)
  form.articleIds = d.articles.map((x) => x.id)
  mergeOptions(partnerOptions.value, d.partners)
  mergeOptions(sponsorOptions.value, d.sponsors)
  articleSeed.value = d.articles.map((r) => ({ id: r.id, label: r.title || r.slug }))
  coverUrl.value = d.coverThumbUrl ?? d.coverUrl ?? null
  hasCover.value = !!d.coverKey
  gallery.value = d.images
  isShared.value = d.isShared
  progress.value = d.progress
}

async function load() {
  loadState.value = 'loading'
  try {
    const [o, partners, sponsors] = await Promise.all([
      listOrgs(club.value).catch(() => [] as OrgListItemDto[]),
      listPartners(club.value).catch(() => []),
      listSponsors(club.value).catch(() => []),
    ])
    orgs.value = o
    partnerOptions.value = partners.map((p) => ({ id: p.id, label: p.nameZh || p.slug }))
    sponsorOptions.value = sponsors.map((s) => ({ id: s.id, label: s.nameZh || s.slug }))
    if (!isCreate.value && programId.value) apply(await getProgram(club.value, programId.value))
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

/** 一次檢查全部，回傳 欄位鍵 → 訊息（鍵只在程式內對照，不顯示）。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!form.nameZh.trim()) errors.nameZh = '請輸入中文計畫名稱'
  if (!form.charityId) errors.charityId = '請選擇受贈的公益團體'
  if (form.startOn && form.endOn && form.endOn < form.startOn) errors.endOn = '結束日不能早於開始日'
  return errors
}

async function handleSave() {
  if (readOnly.value) return
  formError.value = null
  if (formErrors.replaceAll(validate())) {
    await formErrors.focusFirst()
    return
  }
  saving.value = true
  try {
    const payload = {
      charityId: form.charityId,
      startOn: form.startOn || null,
      endOn: form.endOn || null,
      status: form.status,
      sortOrder: form.sortOrder,
      isPinned: form.isPinned,
      content: {
        zh: {
          name: form.nameZh.trim(),
          targetAudience: nullIfBlank(form.audienceZh),
          content: encodeCharityContent(contentField('zh')),
          donationContent: nullIfBlank(form.donationZh),
        },
        en: enOrUndefined(
          {
            name: form.nameEn.trim(),
            targetAudience: nullIfBlank(form.audienceEn) as string,
            content: encodeCharityContent(contentField('en')) as string,
            donationContent: nullIfBlank(form.donationEn) as string,
          },
          'name', 'targetAudience', 'content', 'donationContent',
        ),
      },
      partnerIds: form.partnerIds,
      sponsorIds: form.sponsorIds,
      articleIds: form.articleIds,
      removeCover: coverFile.value ? false : removeCover.value,
    }
    const saved = isCreate.value
      ? await createProgram(club.value, payload, coverFile.value)
      : await updateProgram(club.value, programId.value!, payload, coverFile.value)
    if (isCreate.value) {
      programId.value = saved.id
      router.replace(`/content/charity/programs/${saved.id}/edit`)
    }
    apply(saved)
    coverFile.value = null
    removeCover.value = false
    baselineJson.value = JSON.stringify(form)
    ElMessage.success('已儲存')
  } catch (error) {
    // 後端標到欄位的錯誤直接標在欄位上；對不到欄位的才放頁首
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

async function galleryUpload(file: File) {
  gallery.value = (await addProgramImage(club.value, programId.value!, file)).images
}
async function galleryRemove(id: string) {
  await deleteProgramImage(club.value, programId.value!, id)
  gallery.value = gallery.value.filter((g) => g.id !== id)
}
async function galleryReorder(ids: string[]) {
  gallery.value = (await reorderProgramImages(club.value, programId.value!, ids)).images
}

const back = () => router.push({ path: '/content/charity', query: { tab: 'programs' } })
</script>

<template>
  <div class="program-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="back"><el-icon><ArrowLeft /></el-icon>返回列表</el-button>
      </template>
      <template #meta>
        <FrontendUnitBanner module-code="B5" />
        <SharedContentNotice v-if="loadState === 'ready' && isShared" what="計畫" />
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never"><el-skeleton :rows="10" animated /></el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這個慈善計畫，可能已被刪除，或不屬於目前選擇的俱樂部。</p>
          <p v-else>{{ loadErrorMessage }}</p>
        </template>
        <el-button v-if="loadState === 'error'" type="primary" @click="load">重新載入</el-button>
        <el-button v-else type="primary" @click="back">返回列表</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="program-edit__block" @close="formError = null" />
      <el-form label-position="top" :disabled="readOnly">
        <LangTabsBar>
          <EditLayout>
            <template #main>
              <el-card shadow="never">
                <FormSection>
                  <BilingualShortField field="name" label="計畫名稱" :zh="form.nameZh" :en="form.nameEn" required @update:zh="(v) => (form.nameZh = v)" @update:en="(v) => (form.nameEn = v)" />
                  <BilingualShortField field="audience" label="對象" :zh="form.audienceZh" :en="form.audienceEn" placeholder="例如 偏鄉國小學童" @update:zh="(v) => (form.audienceZh = v)" @update:en="(v) => (form.audienceEn = v)" />
                  <BilingualTextareaField field="content" label="緣起與內容" :zh="form.contentZh" :en="form.contentEn" :rows="6" @update:zh="(v) => (form.contentZh = v)" @update:en="(v) => (form.contentEn = v)" />
                  <el-alert v-if="structuredZh || structuredEn" type="warning" show-icon :closable="false" title="這個計畫的「緣起與內容」原本含有標題或清單排版，文字框顯示的是其中的純文字（標題與清單項目各成一段）。沒有修改這段文字就維持原排版；只要修改，儲存後就會改成純文字段落，標題與清單的排版會消失。" class="program-edit__block" />
                  <BilingualTextareaField field="donation" label="捐助內容" :zh="form.donationZh" :en="form.donationEn" :rows="2" @update:zh="(v) => (form.donationZh = v)" @update:en="(v) => (form.donationEn = v)" />
                </FormSection>

                <FormSection title="贊助夥伴與關聯報導">
                  <FormField field="partnerIds" label="贊助夥伴（合作夥伴）">
                    <el-select v-model="form.partnerIds" multiple filterable placeholder="選擇參與的合作夥伴" style="width: 100%"><el-option v-for="o in partnerOptions" :key="o.id" :label="o.label" :value="o.id" /></el-select>
                  </FormField>
                  <FormField field="sponsorIds" label="贊助夥伴（贊助商）">
                    <el-select v-model="form.sponsorIds" multiple filterable placeholder="選擇參與的贊助商" style="width: 100%"><el-option v-for="o in sponsorOptions" :key="o.id" :label="o.label" :value="o.id" /></el-select>
                  </FormField>
                  <FormField field="articleIds" label="關聯報導">
                    <NewsPicker v-model="form.articleIds" :seed="articleSeed" :disabled="readOnly" />
                    <p class="program-edit__hint">可用關鍵字搜尋所有文章；前台只顯示已發布的報導。</p>
                  </FormField>
                </FormSection>
              </el-card>
            </template>

            <template #aside>
              <el-card shadow="never" header="基本設定">
                <FormSection>
                  <FormField field="charityId" label="受贈公益團體" required>
                    <el-select v-model="form.charityId" filterable placeholder="請選擇" style="width: 100%" @change="formErrors.clear('charityId')"><el-option v-for="o in orgs" :key="o.id" :label="o.nameZh || '（未命名）'" :value="o.id" /></el-select>
                  </FormField>
                  <el-form-item label="開始日"><el-date-picker v-model="form.startOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" @change="formErrors.clear('endOn')" /></el-form-item>
                  <FormField field="endOn" label="結束日"><el-date-picker v-model="form.endOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" @change="formErrors.clear('endOn')" /></FormField>
                  <p v-if="!isCreate" class="program-edit__hint">目前進行狀況：{{ progress === 'ongoing' ? '進行中' : '已完成' }}（沒填結束日或結束日還沒到＝進行中，由系統依日期自動判斷）。</p>
                </FormSection>
                <FormSection title="封面圖片">
                  <FormField field="cover" label="封面圖片">
                    <ImageUploader v-model:file="coverFile" v-model:remove-cover="removeCover" :min-width="0" :min-height="0" :has-existing-image="hasCover" :existing-preview-url="coverUrl" :disabled="saving || readOnly" />
                  </FormField>
                </FormSection>
                <FormSection title="活動圖集">
                  <p class="program-edit__hint">這裡的變更會立即儲存，不需要按下方的儲存</p>
                  <p v-if="isCreate" class="program-edit__hint">請先儲存基本資料，才能管理相簿</p>
                  <GalleryManager v-else :images="gallery" :disabled="readOnly" :on-upload="galleryUpload" :on-remove="galleryRemove" :on-reorder="galleryReorder" />
                </FormSection>
              </el-card>
              <el-card shadow="never" header="發布設定">
                <FormSection>
                  <FormField field="status" label="發布狀態">
                    <el-radio-group v-model="form.status"><el-radio value="draft">草稿（前台不顯示）</el-radio><el-radio value="published">已發布</el-radio></el-radio-group>
                  </FormField>
                  <el-form-item label="排序值"><el-input-number v-model="form.sortOrder" :min="0" /></el-form-item>
                  <el-form-item label="置頂"><el-switch v-model="form.isPinned" active-text="排在列表最前面" /></el-form-item>
                </FormSection>
              </el-card>
            </template>
          </EditLayout>
        </LangTabsBar>
      </el-form>
      <EditActionBar v-if="!readOnly">
        <template #status><FormErrorStatus /></template>
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </EditActionBar>
    </template>
  </div>
</template>

<style scoped>
.program-edit { max-width: 1200px; margin: 0 auto; }
.program-edit__block { margin-bottom: 16px; }
.program-edit__hint { margin: 4px 0 10px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
</style>

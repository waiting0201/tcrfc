<script setup lang="ts">
/**
 * 慈善計畫——新增／編輯（前台慈善計畫列表與詳情）。
 * 「緣起與內容」目前以純文字編輯（區塊編輯器的內容格式待前台確定，見 apps/admin/README.md）。
 * 共用計畫整頁唯讀。
 */
import { computed, onMounted, reactive, ref } from 'vue'
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
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { enOrUndefined, nullIfBlank } from '@/api/adminCommon'
import { listAdminNews } from '@/api/adminNews'
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
const articleOptions = ref<Option[]>([])
const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
const formError = ref<string | null>(null)
const readOnly = computed(() => isShared.value || (isCreate.value ? !canCreate.value : !canUpdate.value))
const pageTitle = computed(() => (isCreate.value ? '新增慈善計畫' : `${readOnly.value ? '檢視' : '編輯'}：${form.nameZh || '（未命名）'}`))

/** 緣起與內容：後端要求整段是合法 JSON。目前以「JSON 字串（純文字）」存取；讀到已是其他 JSON 結構的舊資料時
 * 不轉換、原樣保留（畫面唯讀提示），避免誤把區塊資料壓扁。 */
function decodeContent(raw: string | null | undefined): { text: string; structured: boolean } {
  if (!raw) return { text: '', structured: false }
  try {
    const parsed = JSON.parse(raw)
    return typeof parsed === 'string' ? { text: parsed, structured: false } : { text: raw, structured: true }
  } catch {
    return { text: raw, structured: false }
  }
}
const rawContentZh = ref<string | null>(null)
const rawContentEn = ref<string | null>(null)
const structuredZh = ref(false)
const structuredEn = ref(false)

function encodeContent(text: string, structured: boolean, raw: string | null): string | null {
  if (structured) return raw
  const t = text.trim()
  return t ? JSON.stringify(t) : null
}

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
  const cz = decodeContent(d.zh.content)
  const ce = decodeContent(d.en?.content)
  form.contentZh = cz.text
  form.contentEn = ce.text
  structuredZh.value = cz.structured
  structuredEn.value = ce.structured
  rawContentZh.value = d.zh.content ?? null
  rawContentEn.value = d.en?.content ?? null
  form.partnerIds = d.partners.map((x) => x.id)
  form.sponsorIds = d.sponsors.map((x) => x.id)
  form.articleIds = d.articles.map((x) => x.id)
  mergeOptions(partnerOptions.value, d.partners)
  mergeOptions(sponsorOptions.value, d.sponsors)
  mergeOptions(articleOptions.value, d.articles)
  coverUrl.value = d.coverUrl ?? null
  hasCover.value = !!d.coverKey
  gallery.value = d.images
  isShared.value = d.isShared
  progress.value = d.progress
}

async function load() {
  loadState.value = 'loading'
  try {
    const [o, partners, sponsors, news] = await Promise.all([
      listOrgs(club.value).catch(() => [] as OrgListItemDto[]),
      listPartners(club.value).catch(() => []),
      listSponsors(club.value).catch(() => []),
      listAdminNews(club.value, { pageSize: 100 }).catch(() => null),
    ])
    orgs.value = o
    partnerOptions.value = partners.map((p) => ({ id: p.id, label: p.nameZh || p.slug }))
    sponsorOptions.value = sponsors.map((s) => ({ id: s.id, label: s.nameZh || s.slug }))
    articleOptions.value = (news?.items ?? []).map((n) => ({ id: n.id, label: n.titleZh || n.slug }))
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

function validate(): string | null {
  if (!form.nameZh.trim()) return '請輸入中文計畫名稱'
  if (!form.charityId) return '請選擇受贈的公益團體'
  if (form.startOn && form.endOn && form.endOn < form.startOn) return '結束日不能早於開始日'
  return null
}

async function handleSave() {
  if (readOnly.value) return
  formError.value = validate()
  if (formError.value) return
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
          content: encodeContent(form.contentZh, structuredZh.value, rawContentZh.value),
          donationContent: nullIfBlank(form.donationZh),
        },
        en: enOrUndefined(
          {
            name: form.nameEn.trim(),
            targetAudience: nullIfBlank(form.audienceEn) as string,
            content: encodeContent(form.contentEn, structuredEn.value, rawContentEn.value) as string,
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
        <el-card shadow="never" header="計畫內容" class="program-edit__block">
          <BilingualShortField label="計畫名稱" :zh="form.nameZh" :en="form.nameEn" required @update:zh="(v) => (form.nameZh = v)" @update:en="(v) => (form.nameEn = v)" />
          <el-form-item label="受贈公益團體" required>
            <el-select v-model="form.charityId" filterable placeholder="請選擇" style="width: 100%"><el-option v-for="o in orgs" :key="o.id" :label="o.nameZh || '（未命名）'" :value="o.id" /></el-select>
          </el-form-item>
          <BilingualShortField label="對象" :zh="form.audienceZh" :en="form.audienceEn" placeholder="例如 偏鄉國小學童" @update:zh="(v) => (form.audienceZh = v)" @update:en="(v) => (form.audienceEn = v)" />
          <BilingualTextareaField label="緣起與內容" :zh="form.contentZh" :en="form.contentEn" :rows="6" @update:zh="(v) => (form.contentZh = v)" @update:en="(v) => (form.contentEn = v)" />
          <el-alert v-if="structuredZh || structuredEn" type="warning" show-icon :closable="false" title="這個計畫的「緣起與內容」是進階排版格式，這裡的文字框顯示的是原始內容；儲存時會維持原樣，不會被改寫。" class="program-edit__block" />
          <BilingualTextareaField label="捐助內容" :zh="form.donationZh" :en="form.donationEn" :rows="2" @update:zh="(v) => (form.donationZh = v)" @update:en="(v) => (form.donationEn = v)" />
        </el-card>

        <el-card shadow="never" header="期間與發布" class="program-edit__block">
          <el-row :gutter="12">
            <el-col :xs="24" :sm="12"><el-form-item label="開始日"><el-date-picker v-model="form.startOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item></el-col>
            <el-col :xs="24" :sm="12"><el-form-item label="結束日"><el-date-picker v-model="form.endOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item></el-col>
          </el-row>
          <p v-if="!isCreate" class="program-edit__hint">目前進行狀況：{{ progress === 'ongoing' ? '進行中' : '已完成' }}（沒填結束日或結束日還沒到＝進行中，由系統依日期自動判斷）。</p>
          <el-form-item label="發布狀態">
            <el-radio-group v-model="form.status"><el-radio value="draft">草稿（前台不顯示）</el-radio><el-radio value="published">已發布</el-radio></el-radio-group>
          </el-form-item>
          <el-row :gutter="12">
            <el-col :xs="24" :sm="12"><el-form-item label="排序值"><el-input-number v-model="form.sortOrder" :min="0" /></el-form-item></el-col>
            <el-col :xs="24" :sm="12"><el-form-item label="置頂"><el-switch v-model="form.isPinned" active-text="排在列表最前面" /></el-form-item></el-col>
          </el-row>
        </el-card>

        <el-card shadow="never" header="贊助夥伴與關聯報導" class="program-edit__block">
          <el-form-item label="贊助夥伴（合作夥伴）">
            <el-select v-model="form.partnerIds" multiple filterable placeholder="選擇參與的合作夥伴" style="width: 100%"><el-option v-for="o in partnerOptions" :key="o.id" :label="o.label" :value="o.id" /></el-select>
          </el-form-item>
          <el-form-item label="贊助夥伴（贊助商）">
            <el-select v-model="form.sponsorIds" multiple filterable placeholder="選擇參與的贊助商" style="width: 100%"><el-option v-for="o in sponsorOptions" :key="o.id" :label="o.label" :value="o.id" /></el-select>
          </el-form-item>
          <el-form-item label="關聯報導">
            <el-select v-model="form.articleIds" multiple filterable placeholder="選擇相關的新聞與故事" style="width: 100%"><el-option v-for="o in articleOptions" :key="o.id" :label="o.label" :value="o.id" /></el-select>
            <p class="program-edit__hint">只列出最近的 100 篇文章；前台只顯示已發布的報導。</p>
          </el-form-item>
        </el-card>

        <el-card shadow="never" header="封面圖片" class="program-edit__block">
          <ImageUploader v-model:file="coverFile" v-model:remove-cover="removeCover" :min-width="0" :min-height="0" :has-existing-image="hasCover" :existing-preview-url="coverUrl" :disabled="saving || readOnly" />
        </el-card>
      </el-form>

      <el-card shadow="never" header="活動圖集" class="program-edit__block">
        <p v-if="isCreate" class="program-edit__hint">請先儲存計畫，儲存後就能加入活動圖片。</p>
        <GalleryManager v-else :images="gallery" :disabled="readOnly" :on-upload="galleryUpload" :on-remove="galleryRemove" :on-reorder="galleryReorder" />
      </el-card>
      <EditActionBar v-if="!readOnly"><el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button></EditActionBar>
    </template>
  </div>
</template>

<style scoped>
.program-edit { max-width: 820px; margin: 0 auto 88px; }
.program-edit__block { margin-bottom: 16px; }
.program-edit__hint { margin: 4px 0 10px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
</style>
